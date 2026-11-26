import Foundation
import GameKit
import UIKit

// Endless ranking on Game Center: sign-in sheet over the game (no browser),
// score submission, top entries and the native leaderboard. Account and save
// stay on Unity Cloud; this is only the ranking. Results go back to Unity as
// JSON through UnitySendMessage(receiver, "OnGamesEvent").

@_silgen_name("UnitySendMessage")
private func unitySendMessage(_ object: UnsafePointer<CChar>, _ method: UnsafePointer<CChar>, _ json: UnsafePointer<CChar>)

private final class Ranking: NSObject, GKGameCenterControllerDelegate {
    static let shared = Ranking()
    var receiver = ""
    var handlerSet = false
    var waiting = false
    var interactiveWanted = false
    var pendingSheet: UIViewController?

    func send(_ payload: [String: Any]) {
        guard !receiver.isEmpty,
              let data = try? JSONSerialization.data(withJSONObject: payload),
              let json = String(data: data, encoding: .utf8) else { return }
        receiver.withCString { object in
            "OnGamesEvent".withCString { method in
                json.withCString { unitySendMessage(object, method, $0) }
            }
        }
    }

    func sendAuth() {
        let player = GKLocalPlayer.local
        send(["type": "auth", "ok": player.isAuthenticated, "name": player.isAuthenticated ? player.displayName : ""])
    }

    func topController() -> UIViewController? {
        let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
        let windows = scenes.flatMap { $0.windows }
        var top = (windows.first { $0.isKeyWindow } ?? windows.first)?.rootViewController
        while let presented = top?.presentedViewController { top = presented }
        return top
    }

    func present(_ controller: UIViewController) {
        guard let top = topController() else { send(["type": "error", "error": "noController"]); return }
        top.present(controller, animated: true)
    }

    func signIn(interactive: Bool) {
        if GKLocalPlayer.local.isAuthenticated { sendAuth(); return }
        interactiveWanted = interactiveWanted || interactive
        if interactive, let sheet = pendingSheet {
            pendingSheet = nil
            present(sheet) // The handler below reports the outcome.
            return
        }
        if waiting { return }
        if handlerSet {
            // Game Center calls the handler once per launch; after a cancel the
            // player has to sign in from the iPhone settings.
            send(["type": "auth", "ok": false, "name": ""])
            return
        }
        handlerSet = true
        waiting = true
        GKLocalPlayer.local.authenticateHandler = { [weak self] sheet, _ in
            DispatchQueue.main.async {
                guard let self else { return }
                self.waiting = false
                if let sheet {
                    if self.interactiveWanted { self.present(sheet) } else {
                        self.pendingSheet = sheet
                        self.send(["type": "auth", "ok": false, "name": ""])
                    }
                    return
                }
                self.interactiveWanted = false
                self.sendAuth()
            }
        }
    }

    func submit(_ leaderboard: String, _ score: Int) {
        guard GKLocalPlayer.local.isAuthenticated else {
            send(["type": "submitted", "ok": false])
            return
        }
        GKLeaderboard.submitScore(score, context: 0, player: GKLocalPlayer.local, leaderboardIDs: [leaderboard]) { error in
            DispatchQueue.main.async { self.send(["type": "submitted", "ok": error == nil]) }
        }
    }

    static func row(_ entry: GKLeaderboard.Entry, me: Bool) -> [String: Any] {
        ["rank": entry.rank, "name": entry.player.displayName, "score": entry.score, "me": me]
    }

    func loadTop(_ leaderboard: String, _ count: Int) {
        GKLeaderboard.loadLeaderboards(IDs: [leaderboard]) { boards, error in
            guard error == nil, let board = boards?.first else {
                DispatchQueue.main.async { self.send(["type": "error", "error": "loadLeaderboards"]) }
                return
            }
            board.loadEntries(for: .global, timeScope: .allTime,
                              range: NSRange(location: 1, length: max(1, count))) { local, entries, _, error in
                DispatchQueue.main.async {
                    guard error == nil else { self.send(["type": "error", "error": "loadEntries"]); return }
                    let mine = local?.player.gamePlayerID
                    var payload: [String: Any] = [
                        "type": "scores",
                        "entries": (entries ?? []).map { Ranking.row($0, me: $0.player.gamePlayerID == mine) }
                    ]
                    if let local { payload["player"] = Ranking.row(local, me: true) }
                    self.send(payload)
                }
            }
        }
    }

    func show(_ leaderboard: String) {
        let controller = GKGameCenterViewController(leaderboardID: leaderboard, playerScope: .global, timeScope: .allTime)
        controller.gameCenterDelegate = self
        present(controller)
    }

    func gameCenterViewControllerDidFinish(_ gameCenterViewController: GKGameCenterViewController) {
        gameCenterViewController.dismiss(animated: true)
    }
}

@_cdecl("AstroRankingConfigure")
public func astroRankingConfigure(_ receiver: UnsafePointer<CChar>) {
    let name = String(cString: receiver)
    DispatchQueue.main.async { Ranking.shared.receiver = name }
}

@_cdecl("AstroRankingSignIn")
public func astroRankingSignIn(_ interactive: Bool) {
    DispatchQueue.main.async { Ranking.shared.signIn(interactive: interactive) }
}

@_cdecl("AstroRankingSubmit")
public func astroRankingSubmit(_ leaderboard: UnsafePointer<CChar>, _ score: Int64) {
    let id = String(cString: leaderboard)
    DispatchQueue.main.async { Ranking.shared.submit(id, Int(score)) }
}

@_cdecl("AstroRankingLoadTop")
public func astroRankingLoadTop(_ leaderboard: UnsafePointer<CChar>, _ count: Int32) {
    let id = String(cString: leaderboard)
    DispatchQueue.main.async { Ranking.shared.loadTop(id, Int(count)) }
}

@_cdecl("AstroRankingShow")
public func astroRankingShow(_ leaderboard: UnsafePointer<CChar>) {
    let id = String(cString: leaderboard)
    DispatchQueue.main.async { Ranking.shared.show(id) }
}

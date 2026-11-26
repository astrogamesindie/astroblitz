import Foundation
import StoreKit

@_silgen_name("UnitySendMessage")
private func unitySendMessage(_ object: UnsafePointer<CChar>, _ method: UnsafePointer<CChar>, _ json: UnsafePointer<CChar>)

@MainActor
private final class PremiumStore {
    static var shared: PremiumStore?
    let receiver: String
    let productID: String
    var product: Product?
    var updates: Task<Void, Never>?
    var running = false
    var closed = false

    init(receiver: String, productID: String) {
        self.receiver = receiver
        self.productID = productID
        updates = Task { [weak self] in
            for await result in Transaction.updates {
                guard let self, !self.closed else { return }
                guard case .verified(let transaction) = result else {
                    self.send("error")
                    continue
                }
                guard transaction.productID == self.productID else { continue }
                await transaction.finish()
                await self.entitlements()
            }
        }
    }

    func send(_ type: String, owned: Bool = false, price: String = "") {
        guard !closed,
              let data = try? JSONSerialization.data(withJSONObject: ["type": type, "owned": owned, "price": price]),
              let json = String(data: data, encoding: .utf8) else { return }
        receiver.withCString { object in
            "OnBillingEvent".withCString { method in
                json.withCString { unitySendMessage(object, method, $0) }
            }
        }
    }

    func entitlements() async {
        var owned = false
        for await result in Transaction.currentEntitlements {
            switch result {
            case .verified(let transaction):
                if transaction.productID == productID,
                   transaction.productType == .nonConsumable,
                   transaction.revocationDate == nil {
                    owned = true
                }
            case .unverified(let transaction, _):
                if transaction.productID == productID {
                    send("error") // Never grant or revoke on an unverified result.
                    return
                }
            }
        }
        send("owned", owned: owned)
    }

    func refresh(restore: Bool) async {
        guard !running else { return }
        running = true
        defer { running = false }
        do {
            if restore { try await AppStore.sync() }
            product = try await Product.products(for: [productID]).first { $0.id == productID && $0.type == .nonConsumable }
            send("product", price: product?.displayPrice ?? "")
            await entitlements()
            if product == nil { send("error") }
        } catch {
            send("error")
        }
    }

    func buy() async {
        guard !running, let product, AppStore.canMakePayments else { send("error"); return }
        running = true
        defer { running = false }
        do {
            switch try await product.purchase() {
            case .success(let result):
                guard case .verified(let transaction) = result,
                      transaction.productID == productID else { send("error"); return }
                await transaction.finish()
                await entitlements()
            case .pending: send("pending")
            case .userCancelled: send("cancelled")
            @unknown default: send("error")
            }
        } catch { send("error") }
    }

    func dispose() {
        closed = true
        updates?.cancel()
        updates = nil
    }
}

@MainActor @_cdecl("AstroPremiumConfigure")
public func configurePremium(_ receiver: UnsafePointer<CChar>, _ product: UnsafePointer<CChar>) {
    let name = String(cString: receiver)
    let id = String(cString: product)
    PremiumStore.shared?.dispose()
    PremiumStore.shared = PremiumStore(receiver: name, productID: id)
}

@_cdecl("AstroPremiumRefresh")
public func refreshPremium(_ restore: Bool) {
    Task { @MainActor in await PremiumStore.shared?.refresh(restore: restore) }
}

@_cdecl("AstroPremiumBuy")
public func buyPremium() {
    Task { @MainActor in await PremiumStore.shared?.buy() }
}

@MainActor @_cdecl("AstroPremiumDispose")
public func disposePremium() {
    PremiumStore.shared?.dispose()
    PremiumStore.shared = nil
}

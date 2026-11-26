import Foundation
import StoreKit

// Ships sold as non-consumable products (ship_bastion, ...). StoreKit 2.
// Events to Unity (OnShipStoreEvent): product (price), products (query done),
// owned (true/false per product), pending, cancelled, error.

@_silgen_name("UnitySendMessage")
private func shipStoreSendMessage(_ object: UnsafePointer<CChar>, _ method: UnsafePointer<CChar>, _ json: UnsafePointer<CChar>)

@MainActor
private final class ShipStore {
    static var shared: ShipStore?
    let receiver: String
    let productIDs: [String]
    var products: [String: Product] = [:]
    var updates: Task<Void, Never>?
    var running = false
    var closed = false

    init(receiver: String, productIDs: [String]) {
        self.receiver = receiver
        self.productIDs = productIDs
        updates = Task { [weak self] in
            for await result in Transaction.updates {
                guard let self, !self.closed else { return }
                guard case .verified(let transaction) = result else { continue }
                guard self.productIDs.contains(transaction.productID) else { continue }
                await transaction.finish()
                await self.entitlements()
            }
        }
    }

    func send(_ type: String, product: String = "", owned: Bool = false, price: String = "") {
        guard !closed,
              let data = try? JSONSerialization.data(withJSONObject: ["type": type, "product": product, "owned": owned, "price": price]),
              let json = String(data: data, encoding: .utf8) else { return }
        receiver.withCString { object in
            "OnShipStoreEvent".withCString { method in
                json.withCString { shipStoreSendMessage(object, method, $0) }
            }
        }
    }

    func entitlements() async {
        var owned = Set<String>()
        for await result in Transaction.currentEntitlements {
            switch result {
            case .verified(let transaction):
                if productIDs.contains(transaction.productID),
                   transaction.productType == .nonConsumable,
                   transaction.revocationDate == nil {
                    owned.insert(transaction.productID)
                }
            case .unverified(let transaction, _):
                if productIDs.contains(transaction.productID) {
                    send("error", product: transaction.productID) // Never grant or revoke on an unverified result.
                    return
                }
            }
        }
        for id in productIDs { send("owned", product: id, owned: owned.contains(id)) }
    }

    func refresh(restore: Bool) async {
        guard !running else { return }
        running = true
        defer { running = false }
        do {
            if restore { try await AppStore.sync() }
            let found = try await Product.products(for: productIDs)
            products = [:]
            for item in found where item.type == .nonConsumable {
                products[item.id] = item
                send("product", product: item.id, price: item.displayPrice)
            }
            send("products")
            await entitlements()
        } catch {
            send("error")
        }
    }

    func buy(_ id: String) async {
        guard !running, let product = products[id], AppStore.canMakePayments else { send("error", product: id); return }
        running = true
        defer { running = false }
        do {
            switch try await product.purchase() {
            case .success(let result):
                guard case .verified(let transaction) = result,
                      transaction.productID == id else { send("error", product: id); return }
                await transaction.finish()
                await entitlements()
            case .pending: send("pending", product: id)
            case .userCancelled: send("cancelled", product: id)
            @unknown default: send("error", product: id)
            }
        } catch { send("error", product: id) }
    }

    func dispose() {
        closed = true
        updates?.cancel()
        updates = nil
    }
}

@MainActor @_cdecl("AstroShipsConfigure")
public func configureShips(_ receiver: UnsafePointer<CChar>, _ products: UnsafePointer<CChar>) {
    let name = String(cString: receiver)
    let ids = String(cString: products).split(separator: ",").map { String($0) }.filter { !$0.isEmpty }
    ShipStore.shared?.dispose()
    ShipStore.shared = ShipStore(receiver: name, productIDs: ids)
}

@_cdecl("AstroShipsRefresh")
public func refreshShips(_ restore: Bool) {
    Task { @MainActor in await ShipStore.shared?.refresh(restore: restore) }
}

@_cdecl("AstroShipsBuy")
public func buyShip(_ product: UnsafePointer<CChar>) {
    let id = String(cString: product)
    Task { @MainActor in await ShipStore.shared?.buy(id) }
}

@MainActor @_cdecl("AstroShipsDispose")
public func disposeShips() {
    ShipStore.shared?.dispose()
    ShipStore.shared = nil
}

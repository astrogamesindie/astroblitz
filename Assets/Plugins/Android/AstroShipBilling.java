package com.astrocrawler.store;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;
import com.unity3d.player.UnityPlayer;
import com.android.billingclient.api.*;
import org.json.JSONObject;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * JNI bridge for the ships sold as one-time products (ship_bastion, ...).
 * Several products, never consumed. Events to Unity: product (price),
 * owned (true/false per product), pending, cancelled, error.
 */
public final class AstroShipBilling implements PurchasesUpdatedListener {
    private final Activity activity;
    private final String receiver;
    private final List<String> productIds = new ArrayList<>();
    private final BillingClient client;
    private final Set<String> acknowledging = new HashSet<>();
    private final Handler retries = new Handler(Looper.getMainLooper());
    private final Map<String, ProductDetails> details = new HashMap<>();
    private final Map<String, String> offerTokens = new HashMap<>();
    private boolean connecting;
    private volatile boolean closed;

    public AstroShipBilling(Activity activity, String receiver, String productIdsCsv) {
        this.activity = activity;
        this.receiver = receiver;
        for (String id : productIdsCsv.split(",")) if (!id.trim().isEmpty()) productIds.add(id.trim());
        client = BillingClient.newBuilder(activity).setListener(this)
            .enablePendingPurchases(PendingPurchasesParams.newBuilder().enableOneTimeProducts().build())
            .enableAutoServiceReconnection().build();
    }

    private void send(String type, String product, boolean owned, String price) {
        if (closed) return;
        try {
            JSONObject json = new JSONObject();
            json.put("type", type);
            json.put("product", product);
            json.put("owned", owned);
            json.put("price", price);
            UnityPlayer.UnitySendMessage(receiver, "OnShipStoreEvent", json.toString());
        } catch (Exception ignored) { /* Constant keys and strings cannot fail JSON encoding. */ }
    }

    public void refresh() {
        activity.runOnUiThread(() -> {
            if (closed || connecting) return;
            if (client.isReady()) { query(); return; }
            connecting = true;
            client.startConnection(new BillingClientStateListener() {
                @Override public void onBillingSetupFinished(BillingResult result) {
                    connecting = false;
                    if (closed) return;
                    if (result.getResponseCode() == BillingClient.BillingResponseCode.OK) query();
                    else send("error", "", false, "");
                }
                @Override public void onBillingServiceDisconnected() { connecting = false; }
            });
        });
    }

    private void query() {
        List<QueryProductDetailsParams.Product> list = new ArrayList<>();
        for (String id : productIds) {
            list.add(QueryProductDetailsParams.Product.newBuilder()
                .setProductId(id).setProductType(BillingClient.ProductType.INAPP).build());
        }
        client.queryProductDetailsAsync(QueryProductDetailsParams.newBuilder().setProductList(list).build(),
            (result, products) -> {
                if (closed) return;
                if (result.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                    send("error", "", false, ""); return;
                }
                for (ProductDetails item : products.getProductDetailsList()) {
                    if (!productIds.contains(item.getProductId())) continue;
                    List<ProductDetails.OneTimePurchaseOfferDetails> offers = item.getOneTimePurchaseOfferDetailsList();
                    if (offers == null) continue;
                    for (ProductDetails.OneTimePurchaseOfferDetails offer : offers) {
                        if (offer.getRentalDetails() != null || offer.getPreorderDetails() != null) continue;
                        details.put(item.getProductId(), item);
                        offerTokens.put(item.getProductId(), offer.getOfferToken());
                        send("product", item.getProductId(), false, offer.getFormattedPrice());
                        break;
                    }
                }
                send("products", "", false, "");
            });
        client.queryPurchasesAsync(QueryPurchasesParams.newBuilder()
            .setProductType(BillingClient.ProductType.INAPP).build(), (result, purchases) -> {
                if (closed) return;
                if (result.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                    send("error", "", false, ""); return;
                }
                // Only a complete store answer can revoke: every ship not found is reported as not owned.
                Set<String> purchased = new HashSet<>();
                for (Purchase purchase : purchases) {
                    if (purchase.getPurchaseState() != Purchase.PurchaseState.PURCHASED) continue;
                    for (String id : purchase.getProducts()) if (productIds.contains(id)) purchased.add(id);
                }
                for (String id : productIds) if (!purchased.contains(id)) send("owned", id, false, "");
                for (Purchase purchase : purchases) process(purchase);
            });
    }

    public void purchase(String productId) {
        activity.runOnUiThread(() -> {
            if (closed) return;
            ProductDetails item = details.get(productId);
            if (item == null) { send("error", productId, false, ""); return; }
            BillingFlowParams.ProductDetailsParams.Builder params = BillingFlowParams.ProductDetailsParams
                .newBuilder().setProductDetails(item);
            String token = offerTokens.get(productId);
            if (token != null && !token.isEmpty()) params.setOfferToken(token);
            BillingResult result = client.launchBillingFlow(activity, BillingFlowParams.newBuilder()
                .setProductDetailsParamsList(java.util.Collections.singletonList(params.build())).build());
            handleResult(result, productId);
        });
    }

    private void handleResult(BillingResult result, String productId) {
        int code = result.getResponseCode();
        if (code == BillingClient.BillingResponseCode.ITEM_ALREADY_OWNED) refresh();
        else if (code == BillingClient.BillingResponseCode.USER_CANCELED) send("cancelled", productId, false, "");
        else if (code != BillingClient.BillingResponseCode.OK) send("error", productId, false, "");
    }

    @Override public void onPurchasesUpdated(BillingResult result, List<Purchase> purchases) {
        if (closed) return;
        if (result.getResponseCode() == BillingClient.BillingResponseCode.OK && purchases != null) {
            for (Purchase purchase : purchases) process(purchase);
        } else handleResult(result, "");
    }

    private String shipOf(Purchase purchase) {
        for (String id : purchase.getProducts()) if (productIds.contains(id)) return id;
        return null;
    }

    private void process(Purchase purchase) {
        String id = shipOf(purchase);
        if (closed || id == null) return;
        if (purchase.getPurchaseState() == Purchase.PurchaseState.PENDING) {
            send("pending", id, false, ""); return;
        }
        if (purchase.getPurchaseState() != Purchase.PurchaseState.PURCHASED) return;
        if (purchase.isAcknowledged()) { send("owned", id, true, ""); return; }
        String token = purchase.getPurchaseToken();
        synchronized (acknowledging) { if (!acknowledging.add(token)) return; }
        acknowledge(token, id, 0);
    }

    private void acknowledge(String token, String id, int attempt) {
        if (closed) return;
        client.acknowledgePurchase(AcknowledgePurchaseParams.newBuilder().setPurchaseToken(token).build(), result -> {
            if (closed) return;
            if (result.getResponseCode() != BillingClient.BillingResponseCode.OK && attempt < 2) {
                retries.postDelayed(() -> acknowledge(token, id, attempt + 1), 2000L << attempt);
                return;
            }
            synchronized (acknowledging) { acknowledging.remove(token); }
            // Unacknowledged purchases are refunded by Google after 3 days: grant only once acknowledged.
            if (result.getResponseCode() == BillingClient.BillingResponseCode.OK) send("owned", id, true, "");
            else send("error", id, false, "");
        });
    }

    public void dispose() {
        closed = true;
        retries.removeCallbacksAndMessages(null);
        activity.runOnUiThread(client::endConnection);
    }
}

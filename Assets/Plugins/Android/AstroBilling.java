package com.astrocrawler.store;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;
import com.unity3d.player.UnityPlayer;
import com.android.billingclient.api.*;
import org.json.JSONObject;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/** JNI bridge for permanent Premium; retains legacy remove_ads ownership. Never consumes. */
public final class AstroBilling implements PurchasesUpdatedListener {
    private final Activity activity;
    private final String receiver;
    private final String productId;
    private final BillingClient client;
    private final Set<String> acknowledging = new HashSet<>();
    private final Handler retries = new Handler(Looper.getMainLooper());
    private volatile ProductDetails details;
    private volatile String offerToken;
    private boolean connecting;
    private volatile boolean closed;

    public AstroBilling(Activity activity, String receiver, String productId) {
        this.activity = activity;
        this.receiver = receiver;
        this.productId = productId;
        client = BillingClient.newBuilder(activity).setListener(this)
            .enablePendingPurchases(PendingPurchasesParams.newBuilder().enableOneTimeProducts().build())
            .enableAutoServiceReconnection().build();
    }

    private void send(String type, boolean owned, String price) {
        if (closed) return;
        try {
            JSONObject json = new JSONObject();
            json.put("type", type);
            json.put("owned", owned);
            json.put("price", price);
            UnityPlayer.UnitySendMessage(receiver, "OnBillingEvent", json.toString());
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
                    else send("error", false, "");
                }
                @Override public void onBillingServiceDisconnected() { connecting = false; }
            });
        });
    }

    private void query() {
        details = null;
        offerToken = null;
        send("product", false, "");
        QueryProductDetailsParams.Product product = QueryProductDetailsParams.Product.newBuilder()
            .setProductId(productId).setProductType(BillingClient.ProductType.INAPP).build();
        client.queryProductDetailsAsync(QueryProductDetailsParams.newBuilder()
            .setProductList(Collections.singletonList(product)).build(), (result, products) -> {
                if (closed) return;
                if (result.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                    send("error", false, ""); return;
                }
                for (ProductDetails item : products.getProductDetailsList()) {
                    if (!productId.equals(item.getProductId())) continue;
                    List<ProductDetails.OneTimePurchaseOfferDetails> offers = item.getOneTimePurchaseOfferDetailsList();
                    if (offers != null) {
                        for (ProductDetails.OneTimePurchaseOfferDetails offer : offers) {
                            if (offer.getRentalDetails() != null || offer.getPreorderDetails() != null) continue;
                            // Use the exact eligible permanent offer displayed, including its token.
                            details = item;
                            offerToken = offer.getOfferToken();
                            send("product", false, offer.getFormattedPrice());
                            return;
                        }
                    }
                }
                send("error", false, "");
            });
        client.queryPurchasesAsync(QueryPurchasesParams.newBuilder()
            .setProductType(BillingClient.ProductType.INAPP).build(), (result, purchases) -> {
                if (closed) return;
                if (result.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                    send("error", false, ""); return;
                }
                boolean purchased = false;
                boolean pending = false;
                for (Purchase purchase : purchases) {
                    if (!isPremium(purchase)) continue;
                    purchased |= purchase.getPurchaseState() == Purchase.PurchaseState.PURCHASED;
                    pending |= purchase.getPurchaseState() == Purchase.PurchaseState.PENDING;
                }
                // Only a successful complete store query can revoke the offline entitlement.
                if (!purchased) send("owned", false, "");
                for (Purchase purchase : purchases) process(purchase);
                if (pending) send("pending", false, "");
            });
    }

    public void purchase() {
        activity.runOnUiThread(() -> {
            if (closed) return;
            if (details == null) { send("error", false, ""); return; }
            BillingFlowParams.ProductDetailsParams.Builder item = BillingFlowParams.ProductDetailsParams
                .newBuilder().setProductDetails(details);
            if (offerToken != null && !offerToken.isEmpty()) item.setOfferToken(offerToken);
            BillingResult result = client.launchBillingFlow(activity, BillingFlowParams.newBuilder()
                .setProductDetailsParamsList(Collections.singletonList(item.build())).build());
            handleResult(result);
        });
    }

    private void handleResult(BillingResult result) {
        int code = result.getResponseCode();
        if (code == BillingClient.BillingResponseCode.ITEM_ALREADY_OWNED) refresh();
        else if (code == BillingClient.BillingResponseCode.USER_CANCELED) send("cancelled", false, "");
        else if (code != BillingClient.BillingResponseCode.OK) send("error", false, "");
    }

    @Override public void onPurchasesUpdated(BillingResult result, List<Purchase> purchases) {
        if (closed) return;
        if (result.getResponseCode() == BillingClient.BillingResponseCode.OK && purchases != null) {
            for (Purchase purchase : purchases) process(purchase);
        } else handleResult(result);
    }

    private void process(Purchase purchase) {
        if (closed || !isPremium(purchase)) return;
        if (purchase.getPurchaseState() == Purchase.PurchaseState.PENDING) {
            send("pending", false, ""); return;
        }
        if (purchase.getPurchaseState() != Purchase.PurchaseState.PURCHASED) return;
        if (purchase.isAcknowledged()) { send("owned", true, ""); return; }
        String token = purchase.getPurchaseToken();
        synchronized (acknowledging) { if (!acknowledging.add(token)) return; }
        acknowledge(token, 0);
    }

    private boolean isPremium(Purchase purchase) {
        return purchase.getProducts().contains(productId) || purchase.getProducts().contains("remove_ads");
    }

    private void acknowledge(String token, int attempt) {
        if (closed) return;
        client.acknowledgePurchase(AcknowledgePurchaseParams.newBuilder().setPurchaseToken(token).build(), result -> {
            if (closed) return;
            if (result.getResponseCode() != BillingClient.BillingResponseCode.OK && attempt < 2) {
                retries.postDelayed(() -> acknowledge(token, attempt + 1), 2000L << attempt);
                return;
            }
            synchronized (acknowledging) { acknowledging.remove(token); }
            if (result.getResponseCode() == BillingClient.BillingResponseCode.OK) send("owned", true, "");
            else send("error", false, ""); // Retried by restore/next foreground; never grant pending payments.
        });
    }

    public void dispose() {
        closed = true;
        retries.removeCallbacksAndMessages(null);
        activity.runOnUiThread(client::endConnection);
    }
}

package com.astrocrawler.ads;

import android.app.Activity;
import android.graphics.Insets;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.widget.FrameLayout;
import com.unity3d.player.UnityPlayer;
import com.google.android.gms.ads.*;
import com.google.android.gms.ads.rewarded.*;
import com.google.android.ump.*;
import org.json.JSONObject;

/** All ad operations run on Android's UI thread; Unity callbacks run on Unity's thread. */
public final class AstroAds {
    private final Activity activity;
    private final String receiver, bannerId, rewardedId;
    private final ConsentInformation consent;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private boolean closed, enabled, menuVisible, initialized, initializing, consentStarted;
    private boolean loadingReward, showingReward;
    private boolean consentInProgress;
    private long nextRewardRetry, nextBannerRetry;
    private int generation;
    private AdView banner;
    private FrameLayout bannerHost;
    private RewardedAd rewarded;
    private long rewardLoadedAt;

    public AstroAds(Activity activity, String receiver, String bannerId, String rewardedId) {
        this.activity = activity;
        this.receiver = receiver;
        this.bannerId = bannerId;
        this.rewardedId = rewardedId;
        consent = UserMessagingPlatform.getConsentInformation(activity);
    }

    private void send(String type, int request, int height) { send(type, request, height, ""); }

    private void diagnostic(String detail) {
        Log.w("AstroAds", detail);
        send("diagnostic", 0, 0, detail);
    }

    private void send(String type, int request, int height, String detail) {
        if (closed) return;
        try {
            JSONObject value = new JSONObject();
            value.put("type", type); value.put("request", request); value.put("height", height);
            value.put("detail", detail);
            value.put("privacyRequired", consent.getPrivacyOptionsRequirementStatus()
                == ConsentInformation.PrivacyOptionsRequirementStatus.REQUIRED);
            UnityPlayer.UnitySendMessage(receiver, "OnAdsEvent", value.toString());
        } catch (Exception ignored) { }
    }

    public void setEnabled(boolean value) {
        activity.runOnUiThread(() -> {
            if (closed || enabled == value) return;
            enabled = value;
            generation++;
            nextRewardRetry = 0; nextBannerRetry = 0;
            if (!value) {
                destroyBanner(); rewarded = null; loadingReward = false;
                handler.removeCallbacksAndMessages(null);
                send("rewardUnavailable", 0, 0);
            } else gatherConsent();
        });
    }

    private boolean allowed() { return !closed && enabled && consent.canRequestAds(); }

    private void gatherConsent() {
        if (closed || !enabled || consentInProgress) return;
        if (consentStarted) { initialize(); return; }
        consentInProgress = true;
        consentStarted = true;
        consent.requestConsentInfoUpdate(activity, new ConsentRequestParameters.Builder().build(), () -> {
            if (closed) return;
            UserMessagingPlatform.loadAndShowConsentFormIfRequired(activity, error -> {
                consentInProgress = false;
                if (error != null) {
                    consentStarted = false;
                    diagnostic("UMP form " + error.getErrorCode() + ": " + error.getMessage());
                    scheduleConsentRetry();
                }
                send("privacy", 0, 0);
                initialize();
            });
        }, error -> {
            consentInProgress = false; consentStarted = false;
            diagnostic("UMP update " + error.getErrorCode() + ": " + error.getMessage());
            send("privacy", 0, 0); initialize(); scheduleConsentRetry();
        });
    }

    private void scheduleConsentRetry() {
        handler.postDelayed(() -> { if (!closed && enabled && !consentStarted) gatherConsent(); }, 60000);
    }

    public void retry() {
        activity.runOnUiThread(() -> {
            if (closed || !enabled || showingReward) return;
            nextRewardRetry = 0; nextBannerRetry = 0;
            if (!consent.canRequestAds()) { consentStarted = false; gatherConsent(); }
            else initialize();
        });
    }

    private void initialize() {
        if (!allowed()) { send("consentUnavailable", 0, 0); return; }
        if (initialized) { reconcile(); return; }
        if (initializing) return;
        initializing = true;
        new Thread(() -> MobileAds.initialize(activity, status -> activity.runOnUiThread(() -> {
            initializing = false;
            if (closed) return;
            initialized = true;
            reconcile();
        })), "AstroAdsInit").start();
    }

    public void privacyOptions() {
        activity.runOnUiThread(() -> {
            if (closed || consent.getPrivacyOptionsRequirementStatus()
                != ConsentInformation.PrivacyOptionsRequirementStatus.REQUIRED) return;
            generation++;
            destroyBanner(); rewarded = null; loadingReward = false;
            send("rewardUnavailable", 0, 0);
            UserMessagingPlatform.showPrivacyOptionsForm(activity, error -> {
                send("privacy", 0, 0); initialize();
            });
        });
    }

    public void setMenuVisible(boolean visible) {
        activity.runOnUiThread(() -> {
            if (closed) return;
            menuVisible = visible;
            if (!visible) destroyBanner();
            else reconcile();
        });
    }

    private void reconcile() {
        if (!initialized || !allowed()) return;
        if (menuVisible && !showingReward) createBanner();
        loadReward();
    }

    /**
     * The menu banner: the large anchored adaptive size when this SDK has it
     * (a taller full-width banner), the regular anchored adaptive one
     * otherwise. Looked up by reflection so an SDK without it still builds.
     */
    private static AdSize bannerSize(Activity activity, int widthDp) {
        try {
            java.lang.reflect.Method large = AdSize.class.getMethod(
                "getLargeAnchoredAdaptiveBannerAdSize", android.content.Context.class, int.class);
            Object size = large.invoke(null, activity, widthDp);
            if (size instanceof AdSize) return (AdSize) size;
        } catch (Exception ignored) {
        }
        return AdSize.getCurrentOrientationAnchoredAdaptiveBannerAdSize(activity, widthDp);
    }

    private void createBanner() {
        if (banner != null || !allowed() || !menuVisible || showingReward) return;
        if (System.currentTimeMillis() < nextBannerRetry) return;
        ViewGroup root = activity.findViewById(android.R.id.content);
        int widthPx = root.getWidth();
        if (widthPx <= 0) { handler.postDelayed(this::reconcile, 500); return; }
        int left = 0, right = 0, bottom = 0;
        WindowInsets window = root.getRootWindowInsets();
        if (window != null && Build.VERSION.SDK_INT >= 30) {
            Insets insets = window.getInsets(WindowInsets.Type.systemBars() | WindowInsets.Type.displayCutout());
            left = insets.left; right = insets.right; bottom = insets.bottom;
        } else if (window != null) {
            left = window.getSystemWindowInsetLeft(); right = window.getSystemWindowInsetRight();
            bottom = window.getSystemWindowInsetBottom();
        }
        int widthDp = Math.max(1, (int)((widthPx - left - right) / activity.getResources().getDisplayMetrics().density));
        AdSize size = bannerSize(activity, widthDp);
        bannerHost = new FrameLayout(activity);
        FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(widthPx - left - right,
            ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL);
        params.leftMargin = left; params.rightMargin = right; params.bottomMargin = bottom;
        root.addView(bannerHost, params);
        AdView view = new AdView(activity);
        banner = view;
        view.setAdUnitId(bannerId); view.setAdSize(size);
        bannerHost.addView(view, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.CENTER));
        bannerHost.setVisibility(View.INVISIBLE);
        final int bottomInset = bottom;
        view.setAdListener(new AdListener() {
            @Override public void onAdLoaded() {
                if (view != banner || !allowed() || !menuVisible || showingReward) return;
                bannerHost.setVisibility(View.VISIBLE);
                send("banner", 0, size.getHeightInPixels(activity) + bottomInset);
            }
            @Override public void onAdFailedToLoad(LoadAdError error) {
                if (view != banner) return;
                diagnostic("Banner " + error.getDomain() + "/" + error.getCode() + ": " + error.getMessage());
                nextBannerRetry = System.currentTimeMillis() + 60000;
                destroyBanner();
                // Backoff; never retry each frame or grant a reward on failure.
                handler.postDelayed(() -> { if (menuVisible) reconcile(); }, 60000);
            }
        });
        view.loadAd(new AdRequest.Builder().build());
    }

    private void destroyBanner() {
        AdView old = banner; banner = null;
        if (old != null) old.destroy();
        if (bannerHost != null) {
            if (bannerHost.getParent() instanceof ViewGroup) ((ViewGroup)bannerHost.getParent()).removeView(bannerHost);
            bannerHost = null;
        }
        send("banner", 0, 0);
    }

    private void loadReward() {
        if (!allowed() || loadingReward || showingReward) return;
        if (System.currentTimeMillis() < nextRewardRetry) return;
        if (rewarded != null && System.currentTimeMillis() - rewardLoadedAt < 3500000L) return;
        rewarded = null;
        loadingReward = true;
        send("rewardUnavailable", 0, 0);
        int requestGeneration = generation;
        RewardedAd.load(activity, rewardedId, new AdRequest.Builder().build(), new RewardedAdLoadCallback() {
            @Override public void onAdLoaded(RewardedAd ad) {
                if (requestGeneration != generation || !allowed()) return;
                loadingReward = false; rewarded = ad; rewardLoadedAt = System.currentTimeMillis();
                nextRewardRetry = 0;
                send("rewardReady", 0, 0);
            }
            @Override public void onAdFailedToLoad(LoadAdError error) {
                if (requestGeneration != generation || closed) return;
                loadingReward = false; rewarded = null;
                diagnostic("Rewarded " + error.getDomain() + "/" + error.getCode() + ": " + error.getMessage());
                nextRewardRetry = System.currentTimeMillis() + 60000;
                send("rewardUnavailable", 0, 0);
                handler.postDelayed(AstroAds.this::loadReward, 60000);
            }
        });
    }

    public void showReward(int request) {
        activity.runOnUiThread(() -> {
            if (!allowed() || showingReward || rewarded == null
                || System.currentTimeMillis() - rewardLoadedAt >= 3500000L) {
                send("failed", request, 0); loadReward(); return;
            }
            RewardedAd ad = rewarded; rewarded = null; showingReward = true;
            destroyBanner(); send("rewardUnavailable", 0, 0);
            ad.setFullScreenContentCallback(new FullScreenContentCallback() {
                @Override public void onAdDismissedFullScreenContent() {
                    showingReward = false; send("closed", request, 0); reconcile();
                }
                @Override public void onAdFailedToShowFullScreenContent(AdError error) {
                    diagnostic("Rewarded show " + error.getDomain() + "/" + error.getCode() + ": " + error.getMessage());
                    showingReward = false; send("failed", request, 0); reconcile();
                }
            });
            // Only this callback grants anything. Closing/loading/showing is never a reward.
            ad.show(activity, reward -> { if (allowed()) send("earned", request, 0); });
        });
    }

    public void resume() {
        activity.runOnUiThread(() -> {
            if (!closed) {
                if (banner != null) banner.resume();
                if (enabled && !consentStarted) gatherConsent();
                else reconcile();
            }
        });
    }
    public void refreshLayout() {
        activity.runOnUiThread(() -> {
            if (!closed && menuVisible) { destroyBanner(); reconcile(); }
        });
    }
    public void pause() { activity.runOnUiThread(() -> { if (banner != null) banner.pause(); }); }
    public void dispose() {
        activity.runOnUiThread(() -> {
            closed = true; generation++; handler.removeCallbacksAndMessages(null);
            destroyBanner(); rewarded = null;
        });
    }
}

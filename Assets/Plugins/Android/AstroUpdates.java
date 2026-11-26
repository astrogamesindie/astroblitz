package com.astrocrawler.updates;

import android.app.Activity;
import com.unity3d.player.UnityPlayer;
import com.google.android.play.core.appupdate.*;
import com.google.android.play.core.install.model.AppUpdateType;
import com.google.android.play.core.install.model.UpdateAvailability;
import org.json.JSONObject;

/** Play Store determines the version eligible for this user/device/track. */
public final class AstroUpdates {
    private final Activity activity;
    private final String receiver;
    private final AppUpdateManager manager;
    private boolean closed, flowActive;

    public AstroUpdates(Activity activity, String receiver) {
        this.activity = activity;
        this.receiver = receiver;
        manager = AppUpdateManagerFactory.create(activity);
    }

    private void send(String type, int request, int version) {
        if (closed) return;
        try {
            JSONObject json = new JSONObject();
            json.put("type", type); json.put("request", request); json.put("version", version);
            UnityPlayer.UnitySendMessage(receiver, "OnAndroidUpdateEvent", json.toString());
        } catch (Exception ignored) { }
    }

    private boolean available(AppUpdateInfo info) {
        return info.updateAvailability() == UpdateAvailability.UPDATE_AVAILABLE
            || info.updateAvailability() == UpdateAvailability.DEVELOPER_TRIGGERED_UPDATE_IN_PROGRESS;
    }

    public void check(int request, boolean resume) {
        activity.runOnUiThread(() -> {
            if (closed) return;
            manager.getAppUpdateInfo().addOnSuccessListener(info -> {
                if (closed) return;
                if (available(info)) {
                    send("required", request, info.availableVersionCode());
                    if (flowActive) send("launching", request, info.availableVersionCode());
                    if (resume && !flowActive && info.updateAvailability()
                        == UpdateAvailability.DEVELOPER_TRIGGERED_UPDATE_IN_PROGRESS) start(info, request);
                } else if (info.updateAvailability() == UpdateAvailability.UPDATE_NOT_AVAILABLE)
                    send("current", request, 0);
                else send("error", request, 0);
            }).addOnFailureListener(error -> send("error", request, 0));
        });
    }

    public void update(int request) {
        activity.runOnUiThread(() -> {
            if (closed || flowActive) return;
            // AppUpdateInfo is single-use; always request a fresh one for retries.
            manager.getAppUpdateInfo().addOnSuccessListener(info -> {
                if (closed) return;
                if (!available(info)) {
                    send(info.updateAvailability() == UpdateAvailability.UPDATE_NOT_AVAILABLE
                        ? "current" : "error", request, 0);
                    return;
                }
                send("required", request, info.availableVersionCode());
                if (info.isUpdateTypeAllowed(AppUpdateType.IMMEDIATE)) start(info, request);
                else send("store", request, 0);
            }).addOnFailureListener(error -> send("error", request, 0));
        });
    }

    private void start(AppUpdateInfo info, int request) {
        if (closed || flowActive) return;
        flowActive = true;
        send("launching", request, info.availableVersionCode());
        manager.startUpdateFlow(info, activity, AppUpdateOptions.newBuilder(AppUpdateType.IMMEDIATE).build())
            .addOnSuccessListener(result -> {
                flowActive = false;
                // A successful immediate update normally restarts the app before this callback.
                if (result == Activity.RESULT_OK) check(request, false);
                else send("cancelled", request, 0);
            }).addOnFailureListener(error -> {
                flowActive = false; send("error", request, 0);
            });
    }

    public void dispose() { activity.runOnUiThread(() -> closed = true); }
}

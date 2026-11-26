package com.astrocrawler.display;

import android.app.Activity;
import android.os.Build;
import android.view.View;
import android.view.Window;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.WindowManager;

/** Restore the game's immersive window after rotation, browser sign-in and native ads. */
public final class AstroFullscreen {
    private AstroFullscreen() {}

    public static void apply(Activity activity) {
        if (activity == null) return;
        activity.runOnUiThread(() -> {
            if (activity.isFinishing() || activity.isDestroyed()) return;
            hideBars(activity);
            // Unity and native SDKs can restore window flags on the following frames.
            View decor = activity.getWindow().getDecorView();
            decor.postDelayed(() -> hideBars(activity), 300);
            decor.postDelayed(() -> hideBars(activity), 1000);
        });
    }

    @SuppressWarnings("deprecation")
    private static void hideBars(Activity activity) {
        if (activity.isFinishing() || activity.isDestroyed()) return;
        Window window = activity.getWindow();
        View decor = window.getDecorView();
        window.addFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN);
        if (Build.VERSION.SDK_INT >= 30) {
            window.setDecorFitsSystemWindows(false);
            WindowInsetsController controller = decor.getWindowInsetsController();
            if (controller != null) {
                controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
                controller.hide(WindowInsets.Type.systemBars());
            }
        } else {
            decor.setSystemUiVisibility(View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                | View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_STABLE | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION);
        }
    }
}

package com.astrocrawler.games;

import android.app.Activity;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.os.Bundle;
import com.google.android.gms.games.AnnotatedData;
import com.google.android.gms.games.LeaderboardsClient;
import com.google.android.gms.games.PlayGames;
import com.google.android.gms.games.PlayGamesSdk;
import com.google.android.gms.games.leaderboard.LeaderboardScore;
import com.google.android.gms.games.leaderboard.LeaderboardScoreBuffer;
import com.google.android.gms.games.leaderboard.LeaderboardVariant;
import com.unity3d.player.UnityPlayer;
import org.json.JSONArray;
import org.json.JSONObject;

/**
 * JNI bridge for the endless ranking on Play Games Services v2: sign-in inside the app
 * (no browser), score submission, top scores (two pages of 25) and the native leaderboard.
 * Every result goes back to Unity as JSON through UnitySendMessage(receiver, "OnGamesEvent").
 */
public final class AstroGames {
    private static final int PAGE = 25;
    private static final int SHOW_REQUEST = 9104;
    private final Activity activity;
    private final String receiver;
    private final String leaderboardId;
    private final boolean configured;
    private volatile boolean closed;

    public AstroGames(Activity activity, String receiver, String leaderboardId) {
        this.activity = activity;
        this.receiver = receiver;
        this.leaderboardId = leaderboardId;
        configured = hasAppId(activity);
        if (configured) PlayGamesSdk.initialize(activity);
    }

    /** Without the APP_ID meta-data the SDK throws; report "not configured" instead. */
    private static boolean hasAppId(Activity activity) {
        try {
            ApplicationInfo info = activity.getPackageManager()
                .getApplicationInfo(activity.getPackageName(), PackageManager.GET_META_DATA);
            Bundle meta = info.metaData;
            Object id = meta != null ? meta.get("com.google.android.gms.games.APP_ID") : null;
            return id != null && id.toString().trim().length() > 0;
        } catch (Exception e) {
            return false;
        }
    }

    private void send(JSONObject json) {
        if (closed) return;
        UnityPlayer.UnitySendMessage(receiver, "OnGamesEvent", json.toString());
    }

    private void error(String what) {
        try {
            JSONObject json = new JSONObject();
            json.put("type", "error");
            json.put("error", what);
            send(json);
        } catch (Exception ignored) { }
    }

    private boolean ready() {
        if (configured) return true;
        error("notConfigured");
        return false;
    }

    public void signIn(final boolean interactive) {
        if (!ready()) return;
        activity.runOnUiThread(() -> {
            com.google.android.gms.games.GamesSignInClient client = PlayGames.getGamesSignInClient(activity);
            (interactive ? client.signIn() : client.isAuthenticated()).addOnCompleteListener(task -> {
                boolean ok = task.isSuccessful() && task.getResult() != null && task.getResult().isAuthenticated();
                if (!ok) { auth(false, ""); return; }
                PlayGames.getPlayersClient(activity).getCurrentPlayer().addOnCompleteListener(player ->
                    auth(true, player.isSuccessful() && player.getResult() != null
                        ? player.getResult().getDisplayName() : ""));
            });
        });
    }

    private void auth(boolean ok, String name) {
        try {
            JSONObject json = new JSONObject();
            json.put("type", "auth");
            json.put("ok", ok);
            json.put("name", name == null ? "" : name);
            send(json);
        } catch (Exception ignored) { }
    }

    public void submit(final long score) {
        if (!ready() || score <= 0) return;
        activity.runOnUiThread(() -> PlayGames.getLeaderboardsClient(activity)
            .submitScoreImmediate(leaderboardId, score)
            .addOnCompleteListener(task -> {
                try {
                    JSONObject json = new JSONObject();
                    json.put("type", "submitted");
                    json.put("ok", task.isSuccessful());
                    send(json);
                } catch (Exception ignored) { }
            }));
    }

    /** Top scores, all time, public collection; Play Games returns at most 25 per page. */
    public void loadTop(final int count) {
        if (!ready()) return;
        activity.runOnUiThread(() -> {
            final LeaderboardsClient client = PlayGames.getLeaderboardsClient(activity);
            client.loadTopScores(leaderboardId, LeaderboardVariant.TIME_SPAN_ALL_TIME,
                    LeaderboardVariant.COLLECTION_PUBLIC, Math.min(PAGE, count), true)
                .addOnCompleteListener(first -> {
                    if (!first.isSuccessful() || first.getResult() == null || first.getResult().get() == null) {
                        error("loadTop");
                        return;
                    }
                    final LeaderboardsClient.LeaderboardScores page = first.getResult().get();
                    final JSONArray entries = new JSONArray();
                    LeaderboardScoreBuffer buffer = page.getScores();
                    int read = append(entries, buffer, count);
                    if (read < PAGE || entries.length() >= count) {
                        page.release();
                        loadPlayer(client, entries);
                        return;
                    }
                    client.loadMoreScores(buffer, Math.min(PAGE, count - entries.length()),
                            com.google.android.gms.games.PageDirection.NEXT)
                        .addOnCompleteListener(more -> {
                            if (more.isSuccessful() && more.getResult() != null && more.getResult().get() != null) {
                                LeaderboardsClient.LeaderboardScores next = more.getResult().get();
                                appendFrom(entries, next.getScores(), read, count);
                                next.release();
                            }
                            page.release();
                            loadPlayer(client, entries);
                        });
                });
        });
    }

    /** The new buffer repeats the first page, so reading starts after it. */
    private static void appendFrom(JSONArray entries, LeaderboardScoreBuffer buffer, int skip, int count) {
        for (int i = skip; i < buffer.getCount() && entries.length() < count; i++) {
            entries.put(entry(buffer.get(i), false));
        }
    }

    private static int append(JSONArray entries, LeaderboardScoreBuffer buffer, int count) {
        int read = buffer.getCount();
        for (int i = 0; i < read && entries.length() < count; i++) entries.put(entry(buffer.get(i), false));
        return read;
    }

    private static JSONObject entry(LeaderboardScore score, boolean me) {
        JSONObject json = new JSONObject();
        try {
            json.put("rank", score.getRank());
            json.put("name", score.getScoreHolderDisplayName());
            json.put("score", score.getRawScore());
            json.put("me", me);
        } catch (Exception ignored) { }
        return json;
    }

    private void loadPlayer(LeaderboardsClient client, final JSONArray entries) {
        client.loadCurrentPlayerLeaderboardScore(leaderboardId, LeaderboardVariant.TIME_SPAN_ALL_TIME,
                LeaderboardVariant.COLLECTION_PUBLIC)
            .addOnCompleteListener(task -> {
                try {
                    JSONObject json = new JSONObject();
                    json.put("type", "scores");
                    LeaderboardScore mine = task.isSuccessful() && task.getResult() != null
                        ? task.getResult().get() : null;
                    if (mine != null) {
                        json.put("player", entry(mine, true));
                        for (int i = 0; i < entries.length(); i++) {
                            JSONObject row = entries.optJSONObject(i);
                            if (row != null && row.optLong("rank") == mine.getRank()
                                && row.optLong("score") == mine.getRawScore()) row.put("me", true);
                        }
                    }
                    json.put("entries", entries);
                    send(json);
                } catch (Exception e) {
                    error("player");
                }
            });
    }

    public void show() {
        if (!ready()) return;
        activity.runOnUiThread(() -> PlayGames.getLeaderboardsClient(activity)
            .getLeaderboardIntent(leaderboardId)
            .addOnSuccessListener(intent -> activity.startActivityForResult(intent, SHOW_REQUEST))
            .addOnFailureListener(e -> error("show")));
    }

    public void dispose() {
        closed = true;
    }
}

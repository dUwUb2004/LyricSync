package com.skdushow.mc2pc;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.media.MediaMetadata;
import android.media.session.MediaController;
import android.media.session.PlaybackState;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.service.notification.NotificationListenerService;
import android.util.Log;

import androidx.core.app.NotificationCompat;

import java.util.Collections;
import java.util.List;

public class MusicNotificationListener extends NotificationListenerService {

    private static final String TAG = "USB_MUSIC";
    private static final String CHANNEL_ID = "mc2pc";
    private static final int NOTIFICATION_ID = 1;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private MediaController activeController;

    @Override
    public void onListenerConnected() {
        super.onListenerConnected();
        Log.i(TAG, "onListenerConnected fired");

        List<MediaController> list = getActiveSessions();
        Log.i(TAG, "Active sessions size: " + list.size());

        if (!list.isEmpty()) {
            activeController = list.get(0);
            Log.i(TAG, "Active controller: " + activeController.getPackageName());
            activeController.registerCallback(callback);
        } else {
            Log.i(TAG, "No active media session");
        }

        handler.post(ticker);
    }

    private List<MediaController> getActiveSessions() {
        android.media.session.MediaSessionManager msm =
                (android.media.session.MediaSessionManager) getSystemService(Context.MEDIA_SESSION_SERVICE);
        return msm == null ? Collections.emptyList()
                : msm.getActiveSessions(new android.content.ComponentName(this, MusicNotificationListener.class));
    }

    private final MediaController.Callback callback = new MediaController.Callback() {
        @Override
        public void onMetadataChanged(MediaMetadata metadata) {
            pushState();
        }

        @Override
        public void onPlaybackStateChanged(PlaybackState state) {
            pushState();
        }
    };

    private final Runnable ticker = new Runnable() {
        @Override
        public void run() {
            pushState();
            handler.postDelayed(this, 1000);
        }
    };

    private void pushState() {
        if (activeController == null) return;
        MediaMetadata meta = activeController.getMetadata();
        PlaybackState state = activeController.getPlaybackState();
        if (meta == null || state == null) return;

        // 把 JSON 直接写进 Logcat，电脑用 adb logcat 读取
        Log.i(TAG, String.format("{\"title\":\"%s\",\"artist\":\"%s\",\"album\":\"%s\",\"position\":%d,\"state\":%b}",
                meta.getString(MediaMetadata.METADATA_KEY_TITLE),
                meta.getString(MediaMetadata.METADATA_KEY_ARTIST),
                meta.getString(MediaMetadata.METADATA_KEY_ALBUM),
                state.getPosition(),
                state.getState() == PlaybackState.STATE_PLAYING));
    }


}

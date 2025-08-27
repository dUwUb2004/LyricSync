package com.skdushow.mc2pc;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.Service;
import android.content.Intent;
import android.os.IBinder;
import android.util.Log;

import java.io.IOException;
import java.lang.ref.WeakReference;
import java.net.ServerSocket;
import java.net.Socket;
import java.util.Scanner;

public class TcpService extends Service {

    private static final int PORT = 51234;
    private static final String TAG = "TcpService";

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        startForeground(1, buildNotification());
        new Thread(this::runServer).start();
        return START_STICKY;
    }

    private void runServer() {
        try (ServerSocket server = new ServerSocket(PORT)) {
            Log.d(TAG, "TCP server listening on " + PORT);
            while (true) {
                try (Socket client = server.accept();
                     Scanner sc = new Scanner(client.getInputStream())) {
                    int key = sc.nextInt();
                    MediaHelper.sendKey(key);
                } catch (Exception ignored) {}
            }
        } catch (IOException e) {
            Log.e(TAG, "TCP server error", e);
        }
    }

    private Notification buildNotification() {
        NotificationChannel ch = new NotificationChannel("tcp", "TCP 服务", NotificationManager.IMPORTANCE_LOW);
        NotificationManager nm = getSystemService(NotificationManager.class);
        nm.createNotificationChannel(ch);
        return new Notification.Builder(this, "tcp")
                .setSmallIcon(android.R.drawable.ic_dialog_info)
                .setContentTitle("歌词同步")
                .setContentText("正在监听 51234 端口...")
                .build();
    }

    @Override
    public IBinder onBind(Intent intent) { return null; }

    /* ---------- 工具类：跨 Service 共享 MediaController ---------- */
    public static final class MediaHelper {
        private static WeakReference<android.media.session.MediaController> sCtrl =
                new WeakReference<>(null);

        public static void setActiveController(android.media.session.MediaController c) {
            sCtrl = new WeakReference<>(c);
        }

        public static void sendKey(int keyCode) {
            android.media.session.MediaController c = sCtrl.get();
            if (c == null) return;
            android.media.session.MediaController.TransportControls tc = c.getTransportControls();
            switch (keyCode) {
                case 85:
                    // 兼容写法：先 play，再 pause（系统会内部切换）
                    tc.play();
                    tc.pause();
                    break;
                case 87:
                    tc.skipToNext();
                    break;
                case 88:
                    tc.skipToPrevious();
                    break;
            }
        }
    }
}
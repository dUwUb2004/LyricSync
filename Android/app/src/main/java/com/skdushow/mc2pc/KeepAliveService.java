package com.skdushow.mc2pc;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Intent;
import android.os.Build;
import android.os.IBinder;
import android.util.Log;

import androidx.core.app.NotificationCompat;

public class KeepAliveService extends Service {

    private static final String TAG = "KeepAliveService";
    private static final String CHANNEL_ID = "keep_alive_channel";
    private static final int NOTIFICATION_ID = 1001;

    @Override
    public void onCreate() {
        super.onCreate();
        Log.d(TAG, "保活服务已创建");
        createNotificationChannel();
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        Log.d(TAG, "保活服务已启动");
        
        // 检查是否是停止服务的请求
        if (intent != null && "STOP_SERVICE".equals(intent.getAction())) {
            Log.d(TAG, "收到停止服务请求");
            stopSelf();
            return START_NOT_STICKY;
        }
        
        // 创建并显示通知
        Notification notification = buildNotification();
        startForeground(NOTIFICATION_ID, notification);
        
        // 返回START_STICKY确保服务被杀死后会重启
        return START_STICKY;
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onDestroy() {
        super.onDestroy();
        Log.d(TAG, "保活服务已销毁");
    }

    private void createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            NotificationChannel channel = new NotificationChannel(
                    CHANNEL_ID,
                    getString(R.string.keep_alive_channel_name),
                    NotificationManager.IMPORTANCE_LOW
            );
            channel.setDescription(getString(R.string.keep_alive_channel_description));
            channel.setShowBadge(false);
            channel.setSound(null, null);
            channel.enableVibration(false);
            
            NotificationManager notificationManager = getSystemService(NotificationManager.class);
            if (notificationManager != null) {
                notificationManager.createNotificationChannel(channel);
            }
        }
    }

    private Notification buildNotification() {
        // 创建点击通知时打开应用的Intent
        Intent intent = new Intent(this, MainActivity.class);
        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
        PendingIntent pendingIntent = PendingIntent.getActivity(
                this, 
                0, 
                intent, 
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE
        );

        // 构建通知
        NotificationCompat.Builder builder = new NotificationCompat.Builder(this, CHANNEL_ID)
                .setSmallIcon(android.R.drawable.ic_dialog_info)
                .setContentTitle(getString(R.string.keep_alive_notification_title))
                .setContentText(getString(R.string.keep_alive_notification_text))
                .setPriority(NotificationCompat.PRIORITY_LOW)
                .setOngoing(true) // 设置为持续通知，用户无法滑动删除
                .setAutoCancel(false)
                .setContentIntent(pendingIntent);

        // 添加操作按钮
        Intent stopIntent = new Intent(this, KeepAliveService.class);
        stopIntent.setAction("STOP_SERVICE");
        PendingIntent stopPendingIntent = PendingIntent.getService(
                this, 
                1, 
                stopIntent, 
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE
        );
        
        builder.addAction(
                android.R.drawable.ic_menu_close_clear_cancel,
                getString(R.string.stop_service),
                stopPendingIntent
        );

        return builder.build();
    }

    @Override
    public void onTaskRemoved(Intent rootIntent) {
        super.onTaskRemoved(rootIntent);
        Log.d(TAG, "应用从最近任务中移除，但服务继续运行");
        
        // 即使应用被从最近任务中移除，服务仍然继续运行
        // 这样可以确保音乐监听功能不受影响
    }
}

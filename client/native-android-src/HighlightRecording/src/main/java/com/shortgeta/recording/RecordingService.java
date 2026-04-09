package com.shortgeta.recording;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.Service;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.os.Build;
import android.os.IBinder;
import android.util.Log;

import androidx.core.app.NotificationCompat;

/**
 * Foreground Service for MediaProjection.
 *
 * Android 14+ 부터 MediaProjection 사용 시 foregroundServiceType="mediaProjection"
 * Service 가 필수. AndroidManifest.xml 에 선언되어야 한다.
 *
 * 본 service 는 별도 logic 을 갖지 않고 단지 foreground 상태를 유지해서
 * MediaProjection 의 화면 캡처가 백그라운드에서도 동작하게 한다.
 * 실제 녹화 logic 은 HighlightRecorder.java.
 */
public class RecordingService extends Service {
    private static final String TAG = "ShortGetaRecService";
    private static final String CHANNEL_ID = "shortgeta_recording";
    private static final int NOTIF_ID = 1001;

    @Override
    public void onCreate() {
        super.onCreate();
        createChannel();
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        Log.i(TAG, "onStartCommand");
        Notification notif = buildNotification();
        if (Build.VERSION.SDK_INT >= 29) {
            startForeground(NOTIF_ID, notif, ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION);
        } else {
            startForeground(NOTIF_ID, notif);
        }
        return START_NOT_STICKY;
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onDestroy() {
        Log.i(TAG, "onDestroy");
        stopForeground(STOP_FOREGROUND_REMOVE);
        super.onDestroy();
    }

    private void createChannel() {
        if (Build.VERSION.SDK_INT >= 26) {
            NotificationChannel channel = new NotificationChannel(
                CHANNEL_ID, "숏게타 녹화", NotificationManager.IMPORTANCE_LOW);
            channel.setDescription("하이라이트 녹화 서비스");
            NotificationManager nm = getSystemService(NotificationManager.class);
            if (nm != null) nm.createNotificationChannel(channel);
        }
    }

    private Notification buildNotification() {
        return new NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_media_play)
            .setContentTitle("숏게타")
            .setContentText("하이라이트 녹화 중")
            .setOngoing(true)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .build();
    }
}

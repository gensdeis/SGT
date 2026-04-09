package com.shortgeta.recording;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.media.projection.MediaProjection;
import android.media.projection.MediaProjectionManager;
import android.os.Bundle;
import android.util.Log;

/**
 * MediaProjection 권한 요청 helper Activity.
 *
 * 흐름:
 *   1. HighlightRecorder.startRecording 호출 시 projection == null 이면
 *      Intent 로 이 Activity 를 띄움
 *   2. onCreate 에서 createScreenCaptureIntent 로 시스템 다이얼로그 표시
 *   3. 사용자 승인 → onActivityResult → MediaProjection 생성
 *   4. HighlightRecorder.INSTANCE.setProjection() 호출
 *   5. finish() — Unity 화면으로 복귀
 *
 * AndroidManifest.xml 에 declare 필요:
 *   <activity
 *       android:name="com.shortgeta.recording.RecordingPermissionActivity"
 *       android:theme="@android:style/Theme.Translucent.NoTitleBar"
 *       android:exported="false" />
 */
public class RecordingPermissionActivity extends Activity {
    private static final String TAG = "ShortGetaPermAct";
    private static final int REQ_CODE = 9001;

    /**
     * Unity / Java 측에서 호출. 현재 Activity context 로 본 helper 시작.
     */
    public static void requestPermission(Activity host) {
        Intent intent = new Intent(host, RecordingPermissionActivity.class);
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        host.startActivity(intent);
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        try {
            MediaProjectionManager pm =
                (MediaProjectionManager) getSystemService(Context.MEDIA_PROJECTION_SERVICE);
            Intent captureIntent = pm.createScreenCaptureIntent();
            startActivityForResult(captureIntent, REQ_CODE);
        } catch (Exception e) {
            Log.e(TAG, "createScreenCaptureIntent failed: " + e.getMessage(), e);
            finish();
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQ_CODE) {
            finish();
            return;
        }
        if (resultCode != Activity.RESULT_OK || data == null) {
            Log.w(TAG, "user denied screen capture");
            finish();
            return;
        }
        try {
            MediaProjectionManager pm =
                (MediaProjectionManager) getSystemService(Context.MEDIA_PROJECTION_SERVICE);
            MediaProjection projection = pm.getMediaProjection(resultCode, data);
            if (HighlightRecorder.INSTANCE != null) {
                HighlightRecorder.INSTANCE.setProjection(projection);
                Log.i(TAG, "projection forwarded to HighlightRecorder");
            } else {
                Log.w(TAG, "HighlightRecorder.INSTANCE is null");
            }
        } catch (Exception e) {
            Log.e(TAG, "getMediaProjection failed: " + e.getMessage(), e);
        }
        finish();
    }
}

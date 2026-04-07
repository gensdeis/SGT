package com.shortgeta.recording;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.PixelFormat;
import android.hardware.display.DisplayManager;
import android.hardware.display.VirtualDisplay;
import android.media.Image;
import android.media.ImageReader;
import android.media.projection.MediaProjection;
import android.media.projection.MediaProjectionManager;
import android.os.Build;
import android.os.Environment;
import android.os.Handler;
import android.os.Looper;
import android.util.DisplayMetrics;
import android.util.Log;
import android.view.WindowManager;

import java.io.File;
import java.io.FileOutputStream;
import java.nio.ByteBuffer;
import java.util.LinkedList;

/**
 * 숏게타 9:16 highlight recorder.
 *
 * 동작:
 *   1. startRecording(tag, captureSec): MediaProjection 요청 → ImageReader 등록
 *      → 100ms 마다 frame 캡처 → 메모리 ring buffer (30 frames = 3s)
 *   2. stopRecording(): VirtualDisplay close, ring buffer 유지
 *   3. flushLastClipPath(): ring buffer 의 30 frames 를 jpeg 으로 저장,
 *      디렉토리 경로 반환 (Iter 2B'' 에서 MP4 로 인코딩 예정)
 *
 * 권한:
 *   AndroidManifest 에 FOREGROUND_SERVICE_MEDIA_PROJECTION (Android 14+) 필요
 *   사용자가 첫 호출 시 시스템 다이얼로그로 화면 캡처 권한 승인
 */
public class HighlightRecorder {
    private static final String TAG = "ShortGetaRecorder";
    private static final int MAX_FRAMES = 30; // 3s × 10fps
    private static final long FRAME_INTERVAL_MS = 100;

    private final Activity activity;
    private MediaProjectionManager projectionManager;
    private MediaProjection projection;
    private ImageReader imageReader;
    private VirtualDisplay virtualDisplay;
    private Handler captureHandler;
    private Runnable captureRunnable;

    private int width;
    private int height;
    private int density;

    private final LinkedList<byte[]> ringBuffer = new LinkedList<>();
    private String currentTag = "untagged";
    private String lastClipDir = null;
    private boolean recording = false;

    public HighlightRecorder(Activity activity) {
        this.activity = activity;
        this.projectionManager =
            (MediaProjectionManager) activity.getSystemService(Context.MEDIA_PROJECTION_SERVICE);

        // 9:16 강제 (720x1280)
        this.width = 720;
        this.height = 1280;
        DisplayMetrics dm = new DisplayMetrics();
        activity.getWindowManager().getDefaultDisplay().getMetrics(dm);
        this.density = dm.densityDpi;

        this.captureHandler = new Handler(Looper.getMainLooper());
    }

    /**
     * MediaProjection 권한이 이미 부여된 상태로 호출되어야 한다.
     * Iter 2B' MVP 에서는 권한 요청 흐름 단순화를 위해 RecordingPermissionActivity
     * (별도 helper) 를 거쳐 호출하는 것을 권장. 그 helper 는 본 iter 범위 외.
     *
     * @param tag        세션 식별 (gameId-timestamp 등)
     * @param captureSec 보관할 직전 N 초 (현재 무시, MAX_FRAMES 고정)
     */
    public void startRecording(final String tag, final int captureSec) {
        if (recording) {
            Log.w(TAG, "already recording");
            return;
        }
        currentTag = (tag == null || tag.isEmpty()) ? "untagged" : tag;
        ringBuffer.clear();

        // ImageReader 생성
        imageReader = ImageReader.newInstance(width, height, PixelFormat.RGBA_8888, 2);

        // VirtualDisplay 생성 (projection 은 외부에서 setProjection 으로 주입되어야 함)
        if (projection == null) {
            Log.e(TAG, "MediaProjection not granted; call setProjection() first");
            return;
        }
        virtualDisplay = projection.createVirtualDisplay(
            "ShortGetaCapture",
            width, height, density,
            DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
            imageReader.getSurface(), null, null);

        recording = true;

        // 100ms 마다 frame 캡처
        captureRunnable = new Runnable() {
            @Override
            public void run() {
                if (!recording) return;
                try {
                    Image image = imageReader.acquireLatestImage();
                    if (image != null) {
                        byte[] jpeg = imageToJpeg(image);
                        image.close();
                        synchronized (ringBuffer) {
                            ringBuffer.add(jpeg);
                            while (ringBuffer.size() > MAX_FRAMES) {
                                ringBuffer.removeFirst();
                            }
                        }
                    }
                } catch (Exception e) {
                    Log.w(TAG, "capture frame failed: " + e.getMessage());
                }
                captureHandler.postDelayed(this, FRAME_INTERVAL_MS);
            }
        };
        captureHandler.postDelayed(captureRunnable, FRAME_INTERVAL_MS);
        Log.i(TAG, "startRecording tag=" + currentTag);
    }

    public void stopRecording() {
        if (!recording) return;
        recording = false;
        captureHandler.removeCallbacks(captureRunnable);
        if (virtualDisplay != null) { virtualDisplay.release(); virtualDisplay = null; }
        if (imageReader != null) { imageReader.close(); imageReader = null; }
        Log.i(TAG, "stopRecording, frames=" + ringBuffer.size());
    }

    /**
     * Ring buffer 의 frame 들을 jpeg sequence 로 디스크에 저장.
     * @return 저장 디렉토리 경로 (Iter 2B'' 에서 MP4 통합 예정)
     */
    public String flushLastClipPath() {
        synchronized (ringBuffer) {
            if (ringBuffer.isEmpty()) return null;

            File baseDir = new File(activity.getExternalFilesDir(null), "highlights");
            if (!baseDir.exists()) baseDir.mkdirs();

            String safeTag = currentTag.replaceAll("[^a-zA-Z0-9_-]", "_");
            String dirName = System.currentTimeMillis() + "_" + safeTag;
            File dir = new File(baseDir, dirName);
            dir.mkdirs();

            int i = 0;
            for (byte[] frame : ringBuffer) {
                File f = new File(dir, String.format("frame_%03d.jpg", i++));
                try (FileOutputStream fos = new FileOutputStream(f)) {
                    fos.write(frame);
                } catch (Exception e) {
                    Log.w(TAG, "write frame failed: " + e.getMessage());
                }
            }

            ringBuffer.clear();
            lastClipDir = dir.getAbsolutePath();
            Log.i(TAG, "flushLastClipPath → " + lastClipDir);
            return lastClipDir;
        }
    }

    /**
     * MediaProjection 권한 결과를 외부 (Activity onActivityResult) 에서 전달.
     * 일반적으로 별도 RecordingPermissionActivity 가 처리 후 setProjection 호출.
     */
    public void setProjection(MediaProjection projection) {
        this.projection = projection;
    }

    private byte[] imageToJpeg(Image image) throws Exception {
        Image.Plane[] planes = image.getPlanes();
        ByteBuffer buffer = planes[0].getBuffer();
        int pixelStride = planes[0].getPixelStride();
        int rowStride = planes[0].getRowStride();
        int rowPadding = rowStride - pixelStride * width;

        Bitmap bmp = Bitmap.createBitmap(
            width + rowPadding / pixelStride, height,
            Bitmap.Config.ARGB_8888);
        bmp.copyPixelsFromBuffer(buffer);

        Bitmap cropped = (rowPadding == 0) ? bmp : Bitmap.createBitmap(bmp, 0, 0, width, height);

        java.io.ByteArrayOutputStream baos = new java.io.ByteArrayOutputStream();
        cropped.compress(Bitmap.CompressFormat.JPEG, 80, baos);
        bmp.recycle();
        if (cropped != bmp) cropped.recycle();
        return baos.toByteArray();
    }
}

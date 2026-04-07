package com.shortgeta.recording;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.util.Log;

import java.io.File;
import java.nio.ByteBuffer;
import java.util.LinkedList;

/**
 * jpeg byte[] 시퀀스를 H.264 + MP4 로 인코딩.
 *
 * Iter 2B'''' — 본격 구현이지만 컴파일/실기기 검증은 사용자 환경에서.
 *
 * 동작:
 *   1. encodeJpegSequence(framesJpeg, outputPath)
 *   2. 각 jpeg → BitmapFactory.decodeByteArray
 *   3. ARGB → YUV420 (semi-planar NV12) 변환
 *   4. MediaCodec H.264 ByteBuffer 입력
 *   5. MediaMuxer 로 mp4 컨테이너 작성
 *
 * 가정:
 *   - 입력 jpeg 는 720x1280 (HighlightRecorder 가 강제)
 *   - 30 fps 출력
 *   - 비디오 트랙 only (음성 없음)
 *   - 첫 프레임만 키프레임
 *
 * 알려진 한계:
 *   - YUV 변환이 main thread block — 백그라운드 thread 권장
 *   - Surface input encoder 가 더 빠르지만 ImageReader → Bitmap 경로 사용
 *   - bitrate 고정 (2 Mbps)
 */
public class Mp4Encoder {
    private static final String TAG = "ShortGetaMp4";
    private static final String MIME = "video/avc";
    private static final int WIDTH = 720;
    private static final int HEIGHT = 1280;
    private static final int FRAME_RATE = 30;
    private static final int I_FRAME_INTERVAL = 1; // 1초 마다 키프레임
    private static final int BIT_RATE = 2_000_000;

    /**
     * @param framesJpeg ring buffer 의 jpeg byte 배열들 (시간순)
     * @param outputPath 결과 mp4 파일 경로
     * @return 성공 시 outputPath, 실패 시 null
     */
    public static String encodeJpegSequence(LinkedList<byte[]> framesJpeg, String outputPath) {
        if (framesJpeg == null || framesJpeg.isEmpty()) return null;

        MediaCodec codec = null;
        MediaMuxer muxer = null;
        int videoTrackIndex = -1;
        boolean muxerStarted = false;

        try {
            MediaFormat format = MediaFormat.createVideoFormat(MIME, WIDTH, HEIGHT);
            format.setInteger(MediaFormat.KEY_COLOR_FORMAT,
                MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420SemiPlanar);
            format.setInteger(MediaFormat.KEY_BIT_RATE, BIT_RATE);
            format.setInteger(MediaFormat.KEY_FRAME_RATE, FRAME_RATE);
            format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, I_FRAME_INTERVAL);

            codec = MediaCodec.createEncoderByType(MIME);
            codec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
            codec.start();

            // 출력 디렉토리 보장
            File outFile = new File(outputPath);
            File parent = outFile.getParentFile();
            if (parent != null && !parent.exists()) parent.mkdirs();

            muxer = new MediaMuxer(outputPath, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);

            MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
            int frameIdx = 0;
            long frameTimeUs = 1_000_000L / FRAME_RATE;

            for (byte[] jpeg : framesJpeg) {
                Bitmap bmp = BitmapFactory.decodeByteArray(jpeg, 0, jpeg.length);
                if (bmp == null) continue;

                // bitmap → YUV420 NV12
                int[] argb = new int[WIDTH * HEIGHT];
                bmp.getPixels(argb, 0, WIDTH, 0, 0, WIDTH, HEIGHT);
                bmp.recycle();

                byte[] yuv = new byte[WIDTH * HEIGHT * 3 / 2];
                argbToNv12(argb, yuv, WIDTH, HEIGHT);

                int inputIndex = codec.dequeueInputBuffer(10_000);
                if (inputIndex >= 0) {
                    ByteBuffer inputBuffer = codec.getInputBuffer(inputIndex);
                    if (inputBuffer != null) {
                        inputBuffer.clear();
                        inputBuffer.put(yuv);
                        long ptsUs = frameIdx * frameTimeUs;
                        codec.queueInputBuffer(inputIndex, 0, yuv.length, ptsUs, 0);
                    }
                }

                // 출력 drain
                drainEncoder(codec, muxer, info, false, videoTrackIndexHolder);
                if (videoTrackIndexHolder[0] >= 0 && !muxerStarted) {
                    muxer.start();
                    muxerStarted = true;
                    videoTrackIndex = videoTrackIndexHolder[0];
                }

                frameIdx++;
            }

            // EOS 신호
            int inputIndex = codec.dequeueInputBuffer(10_000);
            if (inputIndex >= 0) {
                codec.queueInputBuffer(inputIndex, 0, 0, frameIdx * frameTimeUs,
                    MediaCodec.BUFFER_FLAG_END_OF_STREAM);
            }

            drainEncoder(codec, muxer, info, true, videoTrackIndexHolder);

            Log.i(TAG, "encoded " + frameIdx + " frames → " + outputPath);
            return outputPath;
        } catch (Exception e) {
            Log.e(TAG, "encode failed: " + e.getMessage(), e);
            return null;
        } finally {
            if (codec != null) {
                try { codec.stop(); codec.release(); } catch (Exception ignored) {}
            }
            if (muxer != null && muxerStarted) {
                try { muxer.stop(); muxer.release(); } catch (Exception ignored) {}
            }
        }
    }

    // muxer.addTrack 결과 인덱스를 nested helper 에서 main 으로 전달.
    private static final int[] videoTrackIndexHolder = new int[]{-1};

    private static void drainEncoder(MediaCodec codec, MediaMuxer muxer,
                                      MediaCodec.BufferInfo info, boolean endOfStream,
                                      int[] trackIndexOut) {
        while (true) {
            int outputIndex = codec.dequeueOutputBuffer(info, endOfStream ? 10_000 : 0);
            if (outputIndex == MediaCodec.INFO_TRY_AGAIN_LATER) {
                if (!endOfStream) return;
                // 더 기다림
                continue;
            } else if (outputIndex == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                MediaFormat newFormat = codec.getOutputFormat();
                trackIndexOut[0] = muxer.addTrack(newFormat);
            } else if (outputIndex >= 0) {
                ByteBuffer encoded = codec.getOutputBuffer(outputIndex);
                if (encoded != null && info.size > 0 && (info.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) == 0) {
                    encoded.position(info.offset);
                    encoded.limit(info.offset + info.size);
                    if (trackIndexOut[0] >= 0) {
                        muxer.writeSampleData(trackIndexOut[0], encoded, info);
                    }
                }
                codec.releaseOutputBuffer(outputIndex, false);
                if ((info.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) {
                    return;
                }
            }
        }
    }

    /**
     * ARGB int[] → YUV420 NV12 (Y plane + interleaved UV plane).
     * 표준 BT.601 행렬.
     */
    private static void argbToNv12(int[] argb, byte[] yuv, int width, int height) {
        int frameSize = width * height;
        int yIndex = 0;
        int uvIndex = frameSize;

        for (int j = 0; j < height; j++) {
            for (int i = 0; i < width; i++) {
                int color = argb[j * width + i];
                int r = (color >> 16) & 0xFF;
                int g = (color >> 8) & 0xFF;
                int b = color & 0xFF;

                int y = (77 * r + 150 * g + 29 * b + 128) >> 8;
                yuv[yIndex++] = (byte) clamp(y, 0, 255);

                if ((j & 1) == 0 && (i & 1) == 0) {
                    int u = ((-43 * r - 84 * g + 127 * b + 128) >> 8) + 128;
                    int v = ((127 * r - 106 * g - 21 * b + 128) >> 8) + 128;
                    yuv[uvIndex++] = (byte) clamp(u, 0, 255);
                    yuv[uvIndex++] = (byte) clamp(v, 0, 255);
                }
            }
        }
    }

    private static int clamp(int v, int min, int max) {
        return v < min ? min : (v > max ? max : v);
    }
}

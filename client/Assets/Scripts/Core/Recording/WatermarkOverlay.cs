using UnityEngine;

namespace ShortGeta.Core.Recording
{
    // 워터마크 오버레이 — Texture2D 우하단에 단순 색상 박스 + (옵션) 텍스트 합성.
    //
    // Iter 2B' MVP 단순화: 텍스트 렌더링 (TextMeshPro / TextGenerator) 은 무겁고
    // EditMode 에서 호출되므로 단색 박스만 합성. 실제 로고/텍스트는 후속.
    //
    // 효과: PNG 시퀀스 우하단 약 15% × 5% 영역에 검정 반투명 박스가 보임.
    public static class WatermarkOverlay
    {
        // ApplyInPlace: src texture 의 우하단에 watermark 박스를 직접 그림.
        // 새 Texture2D 를 만들지 않으므로 메모리 절약.
        public static void ApplyInPlace(Texture2D src, Color? color = null)
        {
            if (src == null) return;
            try
            {
                Color c = color ?? new Color(0f, 0f, 0f, 0.6f);
                int w = src.width;
                int h = src.height;

                int boxW = Mathf.Max(8, w / 6);   // ~16% 폭
                int boxH = Mathf.Max(4, h / 22);  // ~4.5% 높이
                int x0 = w - boxW - Mathf.Max(2, w / 50);
                int y0 = Mathf.Max(2, h / 50);

                // 작은 영역만 SetPixels — 큰 텍스처에서도 빠름
                Color[] block = new Color[boxW * boxH];
                Color[] orig = src.GetPixels(x0, y0, boxW, boxH);
                for (int i = 0; i < block.Length; i++)
                {
                    // alpha blend (수동)
                    Color o = orig[i];
                    block[i] = new Color(
                        o.r * (1 - c.a) + c.r * c.a,
                        o.g * (1 - c.a) + c.g * c.a,
                        o.b * (1 - c.a) + c.b * c.a,
                        1f);
                }

                // 옆에 작은 흰색 W 모양 (간략 로고 placeholder)
                int markX = x0 + 4;
                int markY = y0 + 4;
                int markSize = boxH - 8;
                if (markSize > 4)
                {
                    for (int dy = 0; dy < markSize; dy++)
                    {
                        for (int dx = 0; dx < markSize; dx++)
                        {
                            // 단순 W 형태: 양쪽 vertical + 가운데 V
                            bool onShape =
                                dx == 0 || dx == markSize - 1 ||
                                (dy < markSize / 2 && (dx == markSize / 4 || dx == 3 * markSize / 4));
                            if (onShape && (markX - x0 + dx) < boxW && (markY - y0 + dy) < boxH)
                            {
                                int idx = (markY - y0 + dy) * boxW + (markX - x0 + dx);
                                if (idx >= 0 && idx < block.Length)
                                    block[idx] = Color.white;
                            }
                        }
                    }
                }

                src.SetPixels(x0, y0, boxW, boxH, block);
                src.Apply(false);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Watermark] ApplyInPlace failed: {e.Message}");
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace ShortGeta.Core.UI
{
    // 런타임 procedural sprite 생성기.
    // 9-slice rounded rect + 원형 스프라이트를 캐시하여 반환.
    // 외부 에셋 없이 코드만으로 라운드 UI 구성 가능.
    public static class RoundedSpriteFactory
    {
        private static readonly Dictionary<int, Sprite> _cache = new();

        // 라운드 사각형 9-slice sprite. radius 는 corner 반경(px).
        public static Sprite GetRounded(int radius)
        {
            if (radius < 1) radius = 1;
            int key = radius;
            if (_cache.TryGetValue(key, out var s)) return s;

            int size = radius * 2 + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 중심 사각형: (radius, radius) ~ (size-1-radius, size-1-radius)
                    int cx = x < radius ? radius : (x > size - 1 - radius ? size - 1 - radius : x);
                    int cy = y < radius ? radius : (y > size - 1 - radius ? size - 1 - radius : y);
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float a;
                    if (dist <= radius - 1f) a = 1f;
                    else if (dist >= radius) a = 0f;
                    else a = radius - dist; // 1px AA
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            _cache[key] = sprite;
            return sprite;
        }

        // 원형 스프라이트.
        public static Sprite GetCircle(int size = 128)
        {
            int key = -size; // 음수 key 로 rounded 와 분리
            if (_cache.TryGetValue(key, out var s)) return s;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a;
                    if (dist <= r - 1f) a = 1f;
                    else if (dist >= r) a = 0f;
                    else a = r - dist;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            _cache[key] = sprite;
            return sprite;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Aethoria.UI
{
    // 그림 파일이 아직 없을 때 쓰는 코드로 그린 UI 조각들. 그림이 들어오면(Resources 경로) 그쪽을 먼저 쓴다.
    //  - 둥근 판(9-slice 테두리가 있어 늘려도 모서리가 안 깨짐)
    //  - 작은 픽셀 아이콘(문자열 그림 → 텍스처)
    public static class UiArt
    {
        private static readonly Dictionary<string, Sprite> cache = new();

        // Resources에 그림이 있으면 그것을, 없으면 fallback을 만든다.
        public static Sprite LoadOr(string resourcesPath, System.Func<Sprite> fallback)
        {
            if (cache.TryGetValue(resourcesPath, out var cached) && cached != null) return cached;
            var sprite = Resources.Load<Sprite>(resourcesPath);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(resourcesPath);
                if (texture != null)
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
            if (sprite == null) sprite = fallback();
            cache[resourcesPath] = sprite;
            return sprite;
        }

        // 테두리 선이 있는 둥근 사각형(가운데는 fill, 가장자리 border). 9-slice로 늘려 쓴다.
        public static Sprite RoundedPanel(Color fill, Color border, int radius = 8, int borderWidth = 2)
        {
            string key = $"rounded_{fill}_{border}_{radius}_{borderWidth}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 모서리 원까지의 거리(가장자리 안쪽이면 음수).
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - radius;
                    float outside = Mathf.Clamp01(d + 0.5f);
                    float inBorder = Mathf.Clamp01(d + borderWidth + 0.5f);
                    Color c = Color.Lerp(fill, border, inBorder);
                    c.a *= 1f - outside;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            cache[key] = sprite;
            return sprite;
        }

        // 문자열 그림('#' = 색, '+' = 밝은 색, 'o' = 어두운 색, 그 밖 = 투명)으로 픽셀 아이콘을 만든다.
        // 한 칸을 scale 픽셀로 키우고, 바깥에 1칸짜리 검은 외곽선을 둘러 어디서나 읽히게 한다.
        public static Sprite PixelIcon(string key, string[] rows, Color color, int scale = 3)
        {
            if (cache.TryGetValue("icon_" + key, out var cached) && cached != null) return cached;
            int h = rows.Length, w = 0;
            foreach (var r in rows) w = Mathf.Max(w, r.Length);
            int tw = (w + 2) * scale, th = (h + 2) * scale;
            var texture = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            var pixels = new Color[tw * th];
            Color light = Color.Lerp(color, Color.white, 0.55f);
            Color dark = Color.Lerp(color, Color.black, 0.45f);
            Color outline = new Color(0.04f, 0.02f, 0.06f, 0.95f);

            Color Cell(int cx, int cy)
            {
                if (cy < 0 || cy >= h || cx < 0 || cx >= rows[cy].Length) return Color.clear;
                return rows[cy][cx] switch { '#' => color, '+' => light, 'o' => dark, _ => Color.clear };
            }

            for (int cy = -1; cy <= h; cy++)
            {
                for (int cx = -1; cx <= w; cx++)
                {
                    Color c = Cell(cx, cy);
                    if (c.a <= 0f)
                    {
                        // 이웃에 칠한 칸이 있으면 외곽선.
                        bool near = false;
                        for (int dy = -1; dy <= 1 && !near; dy++)
                            for (int dx = -1; dx <= 1 && !near; dx++)
                                if (Cell(cx + dx, cy + dy).a > 0f) near = true;
                        if (!near) continue;
                        c = outline;
                    }
                    int px0 = (cx + 1) * scale, py0 = (h - cy) * scale;   // 문자열은 위에서부터, 텍스처는 아래에서부터
                    for (int py = 0; py < scale; py++)
                        for (int px = 0; px < scale; px++)
                            pixels[(py0 + py) * tw + px0 + px] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 100f);
            cache["icon_" + key] = sprite;
            return sprite;
        }
    }
}

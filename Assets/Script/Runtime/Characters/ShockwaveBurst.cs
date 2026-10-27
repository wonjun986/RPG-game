using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // 포효/각성 같은 큰 충격 연출: 한 점에서 가장자리가 부드러운 고리가 연달아 퍼져 나가고, 화면 전체가 한 번 번쩍인다.
    // 황금 모래룡의 포효(모래빛)와 루나벨의 2페이즈 각성(핏빛)이 색과 크기만 바꿔 같이 쓴다.
    // 코루틴은 runner(보통 연출을 부르는 쪽 컴포넌트)에서 돈다. 고리는 parent 아래에 붙여 스테이지와 함께 지워지게 한다.
    public static class ShockwaveBurst
    {
        public struct Settings
        {
            public int rings;
            public float interval;
            public float duration;
            public float maxSize;       // 고리 지름(유닛)
            public float squash;        // 세로 비율(1이면 정원, 작을수록 납작)
            public Color ringColor;
            public Color flashColor;    // a가 번쩍임 세기(0이면 번쩍이지 않는다)
            public float flashDuration;
            public int sortingOrder;
        }

        private static Sprite ringSprite;
        private static Sprite flashSprite;

        public static void Play(MonoBehaviour runner, Vector3 origin, Transform parent, Settings settings)
        {
            if (runner == null) return;
            if (settings.flashColor.a > 0f) runner.StartCoroutine(ScreenFlash(settings));
            runner.StartCoroutine(Rings(runner, origin, parent, settings));
        }

        private static IEnumerator Rings(MonoBehaviour runner, Vector3 origin, Transform parent, Settings settings)
        {
            var sprite = RingSprite();
            for (int i = 0; i < settings.rings; i++)
            {
                if (runner == null) yield break;
                runner.StartCoroutine(ExpandRing(sprite, origin, parent, i == 0 ? 1.15f : 1f, settings));
                yield return new WaitForSeconds(settings.interval);
            }
        }

        private static IEnumerator ExpandRing(Sprite sprite, Vector3 origin, Transform parent, float sizeScale, Settings settings)
        {
            var go = new GameObject("Shockwave");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = origin;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = settings.sortingOrder;
            Color c = settings.ringColor;
            for (float t = 0f; t < settings.duration; t += Time.deltaTime)
            {
                if (go == null) yield break;
                float k = t / settings.duration;
                float size = Mathf.Lerp(0.6f, settings.maxSize * sizeScale, 1f - (1f - k) * (1f - k)); // 처음에 빠르게 퍼진다
                go.transform.localScale = new Vector3(size, size * settings.squash, 1f);
                renderer.color = new Color(c.r, c.g, c.b, c.a * (1f - k));
                yield return null;
            }
            if (go != null) Object.Destroy(go);
        }

        // 카메라 앞을 통째로 덮는 번쩍임.
        private static IEnumerator ScreenFlash(Settings settings)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            var go = new GameObject("ShockwaveFlash");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 1f);
            go.transform.localScale = new Vector3(40f, 40f, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = FlashSprite();
            renderer.sortingOrder = settings.sortingOrder + 3;
            Color c = settings.flashColor;
            for (float t = 0f; t < settings.flashDuration; t += Time.deltaTime)
            {
                if (go == null) yield break;
                renderer.color = new Color(c.r, c.g, c.b, c.a * (1f - t / settings.flashDuration));
                yield return null;
            }
            if (go != null) Object.Destroy(go);
        }

        // 가장자리가 부드러운 고리(지름 1유닛) 그림을 한 번만 만들어 둔다.
        private static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float center = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center; // 0~1
                    float band = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.12f);                      // 바깥쪽 굵은 띠
                    float inner = Mathf.Clamp01(1f - Mathf.Abs(d - 0.6f) / 0.25f) * 0.25f;              // 안쪽 옅은 일렁임
                    byte a = (byte)(Mathf.Clamp01(band + inner) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return ringSprite;
        }

        private static Sprite FlashSprite()
        {
            if (flashSprite != null) return flashSprite;
            var texture = Texture2D.whiteTexture;
            flashSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            return flashSprite;
        }
    }
}

using System.Collections;
using UnityEngine;

namespace Aethoria.Fx
{
    // 보스 강공격 예고:
    //  - FlashBody: 몸 위에 같은 그림의 붉은 실루엣을 겹쳐 두세 번 번쩍인다.
    //  - GroundZone: 맞을 바닥 범위에 붉은 띠를 깔고, 공격 순간까지 점점 차오르다가 사라진다.
    public static class DangerFx
    {
        public static readonly Color DangerColor = new Color(1f, 0.15f, 0.2f);
        private static Material silhouetteMaterial;
        private static Sprite zoneSprite;

        // 그림의 알파만 쓰고 색은 꼭짓점 색으로 칠하는 셰이더(글자용 내장 셰이더). 없으면 그냥 붉게 물들인다.
        private static Material SilhouetteMaterial
        {
            get
            {
                if (silhouetteMaterial == null)
                {
                    var shader = Shader.Find("GUI/Text Shader");
                    if (shader != null) silhouetteMaterial = new Material(shader);
                }
                return silhouetteMaterial;
            }
        }

        public static void FlashBody(SpriteRenderer target, float duration, int flashes = 3)
        {
            if (target == null) return;
            var runner = target.GetComponent<FlashRunner>();
            if (runner == null) runner = target.gameObject.AddComponent<FlashRunner>();
            runner.Flash(target, duration, flashes);
        }

        public static void GroundZone(Vector2 center, float width, float duration, int sortingOrder = 4)
        {
            if (zoneSprite == null) zoneSprite = MakeZoneSprite();
            var sr = FxUtil.MakeSprite("DangerZone", zoneSprite, center, new Color(DangerColor.r, DangerColor.g, DangerColor.b, 0f), sortingOrder);
            sr.transform.localScale = new Vector3(width, 0.5f, 1f);
            sr.gameObject.AddComponent<ZoneRunner>().Init(sr, width, duration);
        }

        // 가운데가 진하고 위로 옅어지는 납작한 띠(1x1유닛, 아래 가운데 기준).
        private static Sprite MakeZoneSprite()
        {
            const int w = 64, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float x = (i + 0.5f) / w * 2f - 1f;
                float y = (j + 0.5f) / h;
                float edge = Mathf.Clamp01((1f - Mathf.Abs(x)) * 8f);
                float rim = Mathf.Abs(x) > 0.93f ? 1f : 0f;
                float a = edge * Mathf.Lerp(0.85f, 0f, y) + rim * (1f - y) * 0.6f;
                px[j * w + i] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
        }

        private class FlashRunner : MonoBehaviour
        {
            private SpriteRenderer overlay;
            private Coroutine routine;

            public void Flash(SpriteRenderer target, float duration, int flashes)
            {
                if (routine != null) StopCoroutine(routine);
                routine = StartCoroutine(Run(target, duration, Mathf.Max(1, flashes)));
            }

            private IEnumerator Run(SpriteRenderer target, float duration, int flashes)
            {
                var mat = SilhouetteMaterial;
                Color original = target.color;
                if (mat != null && overlay == null)
                {
                    var go = new GameObject("DangerFlash");
                    go.transform.SetParent(target.transform, false);
                    overlay = go.AddComponent<SpriteRenderer>();
                    overlay.sharedMaterial = mat;
                }
                float t = 0f;
                while (t < duration && target != null)
                {
                    t += Time.deltaTime;
                    float wave = Mathf.Abs(Mathf.Sin(t / duration * flashes * Mathf.PI));
                    if (overlay != null)
                    {
                        overlay.sprite = target.sprite;
                        overlay.flipX = target.flipX;
                        overlay.flipY = target.flipY;
                        overlay.sortingLayerID = target.sortingLayerID;
                        overlay.sortingOrder = target.sortingOrder + 1;
                        overlay.color = new Color(DangerColor.r, DangerColor.g, DangerColor.b, wave * 0.65f);
                    }
                    else
                    {
                        target.color = Color.Lerp(original, DangerColor, wave * 0.7f);
                    }
                    yield return null;
                }
                if (overlay != null) overlay.color = Color.clear;
                else if (target != null) target.color = original;
                routine = null;
            }

            private void OnDisable()
            {
                if (overlay != null) overlay.color = Color.clear;
            }
        }

        private class ZoneRunner : MonoBehaviour
        {
            private SpriteRenderer sr;
            private float width, duration, age;

            public void Init(SpriteRenderer renderer, float w, float d)
            {
                sr = renderer;
                width = w;
                duration = Mathf.Max(0.1f, d);
            }

            private void Update()
            {
                age += Time.deltaTime;
                float k = age / duration;
                if (k < 1f)
                {
                    // 차오르며 깜빡임이 빨라진다
                    float pulse = 0.65f + 0.35f * Mathf.Sin(age * Mathf.Lerp(10f, 30f, k));
                    sr.color = new Color(DangerColor.r, DangerColor.g, DangerColor.b, Mathf.Clamp01(k * 3f) * 0.75f * pulse);
                    sr.transform.localScale = new Vector3(width * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(k * 2.5f)), 0.25f + 0.35f * k, 1f);
                }
                else
                {
                    float f = (age - duration) / 0.2f;
                    var c = sr.color;
                    c.a = Mathf.Lerp(0.85f, 0f, f);
                    sr.color = c;
                    if (f >= 1f) Destroy(gameObject);
                }
            }
        }
    }
}

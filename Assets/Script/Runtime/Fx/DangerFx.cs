using System.Collections;
using UnityEngine;

namespace Aethoria.Fx
{
    // 보스 강공격 예고:
    //  - FlashBody: 몸 위에 같은 그림의 붉은 실루엣을 겹쳐 두세 번 번쩍인다.
    //  - GroundZone: 맞을 바닥 범위에 붉은 띠를 깔고, 공격 순간까지 점점 차오르다가 사라진다.
    //  - Area: 공격(투사체·회오리·참격 등)이 지나갈 범위에 붉은 상자를 깔고 가운데에 "!"를 띄운다. 공격 순간까지 차오르며 깜빡인다.
    //  모든 보스 공격은 보스 몸이 아니라 이 범위 표시(GroundZone/Area 또는 패턴 자체의 예고)로 알린다.
    public static class DangerFx
    {
        public static readonly Color DangerColor = new Color(1f, 0.15f, 0.2f);
        private static Material silhouetteMaterial;
        private static Sprite zoneSprite;
        private static Sprite markSprite;
        private static Sprite boxSprite;
        private const float MarkSize = 0.62f;   // "!" 표시 키(유닛)

        // center/size: 맞는 범위(유닛). angle: 기울기(도, 대각선으로 날아가는 공격용). duration: 공격이 나가기까지 남은 시간.
        // 돌려준 오브젝트를 지우면 예고가 바로 사라진다(패턴이 도중에 바뀔 때).
        public static GameObject Area(Vector2 center, Vector2 size, float duration, float angle = 0f, int sortingOrder = 4)
        {
            if (duration <= 0f || size.x <= 0f || size.y <= 0f) return null;
            if (boxSprite == null) boxSprite = MakeBoxSprite();
            var sr = FxUtil.MakeSprite("DangerArea", boxSprite, center, new Color(DangerColor.r, DangerColor.g, DangerColor.b, 0f), sortingOrder);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            var mark = SpawnMark(center, sortingOrder + 1);
            sr.gameObject.AddComponent<AreaRunner>().Init(sr, size, duration, mark);
            return sr.gameObject;
        }

        // "!" 표시(범위 가운데). 위아래가 늘 바로 서 있게 따로 만든다.
        private static SpriteRenderer SpawnMark(Vector2 at, int sortingOrder)
        {
            if (markSprite == null) markSprite = MakeMarkSprite();
            var go = new GameObject("DangerMark");
            go.transform.position = new Vector3(at.x, at.y - MarkSize * 0.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = markSprite;
            sr.sortingOrder = sortingOrder;
            sr.color = new Color(1f, 1f, 1f, 0f);
            return sr;
        }

        // 옅게 칠한 안쪽 + 진한 테두리(9칸 자르기라 크기를 바꿔도 테두리 굵기가 같다). 1유닛 = 32px.
        private static Sprite MakeBoxSprite()
        {
            const int s = 32, edge = 3;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[s * s];
            for (int j = 0; j < s; j++)
            for (int i = 0; i < s; i++)
            {
                int d = Mathf.Min(Mathf.Min(i, s - 1 - i), Mathf.Min(j, s - 1 - j));
                px[j * s + i] = new Color(1f, 1f, 1f, d < edge ? 1f : 0.38f);
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
        }

        // 진홍색 원(짙은 테두리) 안에 흰 느낌표. 아래 가운데 기준, 키 1유닛.
        private static Sprite MakeMarkSprite()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[s * s];
            var fill = new Color(0.86f, 0.08f, 0.14f);
            var rim = new Color(0.25f, 0.02f, 0.05f);
            for (int j = 0; j < s; j++)
            for (int i = 0; i < s; i++)
            {
                float x = (i + 0.5f) / s * 2f - 1f, y = (j + 0.5f) / s * 2f - 1f;
                float r = Mathf.Sqrt(x * x + y * y);
                // 바깥 테두리 → 붉은 원 → 흰 느낌표(막대 + 점), 가장자리는 1픽셀 정도 부드럽게.
                float circle = Mathf.Clamp01((0.98f - r) * s * 0.5f);
                Color c = Color.Lerp(rim, fill, Mathf.Clamp01((0.84f - r) * s * 0.5f));
                float bar = Mathf.Clamp01((0.13f - Mathf.Abs(x) + (y - 0.05f) * 0.06f) * s * 0.5f) * Mathf.Clamp01((y + 0.08f) * s * 0.5f) * Mathf.Clamp01((0.62f - y) * s * 0.5f);
                float dot = Mathf.Clamp01((0.14f - Mathf.Sqrt(x * x + (y + 0.42f) * (y + 0.42f))) * s * 0.5f);
                c = Color.Lerp(c, Color.white, Mathf.Max(bar, dot));
                c.a = circle;
                px[j * s + i] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0f), s);
        }

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
            // 바닥 띠 위에도 "!"를 띄운다(따로 사라지게 같은 시간짜리 빈 상자에 붙인다).
            var mark = SpawnMark(center + Vector2.up * 0.75f, sortingOrder + 1);
            mark.gameObject.AddComponent<AreaRunner>().Init(null, Vector2.zero, duration, mark);
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

        // 범위 상자: 공격 순간까지 차오르며 점점 빠르게 깜빡이고, 그때 빠르게 사라진다. "!"는 튀어나오듯 커졌다가 함께 깜빡인다.
        // box가 null이면 "!"만 다룬다(GroundZone용).
        private class AreaRunner : MonoBehaviour
        {
            private void OnDestroy()
            {
                if (box != null && mark != null && mark.gameObject != gameObject) Destroy(mark.gameObject);
            }

            private SpriteRenderer box, mark;
            private Vector2 size;
            private float duration, age;

            public void Init(SpriteRenderer area, Vector2 areaSize, float d, SpriteRenderer markRenderer)
            {
                box = area;
                size = areaSize;
                duration = Mathf.Max(0.1f, d);
                mark = markRenderer;
            }

            private void Update()
            {
                age += Time.deltaTime;
                float k = Mathf.Clamp01(age / duration);
                float blink = 0.65f + 0.35f * Mathf.Sin(age * Mathf.Lerp(10f, 30f, k));
                float fade = 1f;
                if (age > duration)
                {
                    fade = 1f - (age - duration) / 0.15f;
                    if (fade <= 0f)
                    {
                        if (mark != null) Destroy(mark.gameObject);
                        if (box != null) Destroy(box.gameObject);
                        Destroy(this);
                        return;
                    }
                }
                if (box != null)
                {
                    box.color = new Color(DangerColor.r, DangerColor.g, DangerColor.b, Mathf.Clamp01(k * 3f) * 0.8f * blink * fade);
                    // 공격 쪽으로 살짝 부풀며 차오른다.
                    box.size = new Vector2(size.x, size.y) * Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(k * 2.5f));
                }
                if (mark != null)
                {
                    float pop = age < 0.15f ? Mathf.Lerp(0.2f, 1.25f, age / 0.15f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((age - 0.15f) / 0.1f));
                    mark.transform.localScale = Vector3.one * MarkSize * pop * (1f + 0.08f * Mathf.Sin(age * Mathf.Lerp(14f, 34f, k)));
                    mark.color = new Color(1f, 1f, 1f, (0.75f + 0.25f * blink) * fade);
                }
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

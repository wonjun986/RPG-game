using UnityEngine;
using UnityEngine.UI;
using Aethoria.Fx;

namespace Aethoria.UI
{
    // 타이틀 화면(UI/Title.png)을 살아 움직이게 하는 연출. 그림 한 장 위에 얹는 효과들이라 원화는 그대로다.
    //  - 로고("Black Requiem")가 은은하게 맥동하고, 몇 초마다 빛줄기가 왼쪽에서 오른쪽으로 훑고 지나간다(UI/Title_LogoGlow = 로고 글자 모양).
    //  - 달빛이 천천히 밝아졌다 어두워진다.
    //  - 그림 속 꽃잎처럼 하얀 꽃잎이 바람에 흩날리고, 보랏빛 불씨가 아래에서 피어오른다.
    //  - 바닥에 안개가 천천히 흘러간다.
    // 배경 바로 위, 메뉴 버튼/글자보다 아래에 깐다. 모두 실제 시간으로 움직인다.
    public class TitleAmbience : MonoBehaviour
    {
        private const string LogoGlowPath = "UI/Title/Title_LogoGlow";
        private const string FogPath = "Backgrounds/Parallax/Forest/forest_fog";

        // 원화 속 위치(화면 비율, 왼쪽 아래 기준).
        private static readonly Vector2 MoonCenter = new Vector2(0.535f, 0.965f);
        private const float LogoLeft = 0.53f, LogoRight = 0.985f;

        private const int PetalCount = 22;
        private const int EmberCount = 30;
        private const float SweepInterval = 5.5f;   // 빛줄기가 다시 지나가기까지(초)
        private const float SweepDuration = 1.3f;
        private const float SweepWidth = 0.07f;     // 빛줄기 폭(화면 폭 비율)

        private RectTransform root;
        private Image moonGlow;
        private RawImage logoGlow;
        private RectTransform sweepMask;
        private RectTransform sweepImage;
        private RawImage fog;

        private struct Particle
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Pos;      // 화면 비율 좌표
            public Vector2 Vel;      // 화면 비율/초
            public float Age, Life, Phase, Spin, Size, BaseAlpha;
        }

        // 로고가 번쩍인 시각(Time.unscaledTime). 음수면 아직 안 번쩍였다.
        private float flashStart = -1f;

        // 로고 빛 모양에 십자가 실루엣도 들어 있어서, 시작 연출에서 십자가가 꽂히기 전에는 로고 빛을 숨겨 둔다.
        private bool logoGlowHidden;

        // 타이틀 시작 연출이 로고를 드러낼 때(십자가가 꽂히는 순간) 부른다. 숨겨 둔 로고 빛도 다시 켠다.
        public void FlashLogo()
        {
            flashStart = Time.unscaledTime;
            SetLogoGlowHidden(false);
        }

        public void SetLogoGlowHidden(bool hidden)
        {
            logoGlowHidden = hidden;
            if (logoGlow != null) logoGlow.enabled = !hidden;
            if (hidden && sweepMask != null) sweepMask.gameObject.SetActive(false);
        }

        private Particle[] petals;
        private Particle[] embers;
        private static Sprite petalSprite;

        public static TitleAmbience Create(Transform canvas)
        {
            var go = new GameObject("TitleAmbience", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas, false);
            Stretch(rect);
            var ambience = go.AddComponent<TitleAmbience>();
            ambience.root = rect;
            ambience.Build();
            return ambience;
        }

        private void Build()
        {
            // 달빛
            moonGlow = MakeImage("MoonGlow", FxUtil.SoftCircleSprite, new Color(0.82f, 0.7f, 1f, 0.2f));
            var moonRect = moonGlow.rectTransform;
            moonRect.anchorMin = moonRect.anchorMax = MoonCenter;
            moonRect.sizeDelta = new Vector2(520f, 520f);

            // 바닥 안개(좌우가 이어지는 안개 띠를 반복 무늬로 흘린다)
            var fogTexture = Resources.Load<Texture2D>(FogPath);
            if (fogTexture != null)
            {
                fogTexture.wrapMode = TextureWrapMode.Repeat;
                var fogGO = new GameObject("Fog", typeof(RectTransform), typeof(RawImage));
                var fogRect = (RectTransform)fogGO.transform;
                fogRect.SetParent(root, false);
                fogRect.anchorMin = new Vector2(0f, -0.08f);
                fogRect.anchorMax = new Vector2(1f, 0.3f);
                fogRect.offsetMin = fogRect.offsetMax = Vector2.zero;
                fog = fogGO.GetComponent<RawImage>();
                fog.texture = fogTexture;
                fog.color = new Color(0.85f, 0.75f, 1f, 0.38f);
                fog.raycastTarget = false;
                fog.uvRect = new Rect(0f, 0f, 1.4f, 1f);
            }

            // 로고 맥동 + 빛줄기
            var logoTexture = Resources.Load<Texture2D>(LogoGlowPath);
            if (logoTexture != null)
            {
                logoGlow = MakeRaw("LogoGlow", root, logoTexture, new Color(0.85f, 0.7f, 1f, 0f));
                Stretch(logoGlow.rectTransform);

                var maskGO = new GameObject("LogoSweep", typeof(RectTransform), typeof(RectMask2D));
                sweepMask = (RectTransform)maskGO.transform;
                sweepMask.SetParent(root, false);
                sweepMask.offsetMin = sweepMask.offsetMax = Vector2.zero;
                maskGO.GetComponent<RectMask2D>().softness = new Vector2Int(36, 0);
                var sweep = MakeRaw("Shine", sweepMask, logoTexture, new Color(1f, 0.95f, 1f, 0.95f));
                sweepImage = sweep.rectTransform;
                maskGO.SetActive(false);
            }

            // ??=는 C# null만 봐서, 플레이 모드를 끌 때 Unity가 지운 스프라이트(가짜 null)를 그대로 다시 써서 흰 사각형으로 보였다.
            // Unity의 == null(지워진 오브젝트도 null로 본다)로 확인해 다시 만든다.
            if (petalSprite == null) petalSprite = MakePetalSprite();
            petals = new Particle[PetalCount];
            for (int i = 0; i < PetalCount; i++)
            {
                petals[i] = NewParticle("Petal", petalSprite, new Color(1f, 0.94f, 1f, 1f));
                ResetPetal(ref petals[i], scatter: true);
            }
            embers = new Particle[EmberCount];
            for (int i = 0; i < EmberCount; i++)
            {
                embers[i] = NewParticle("Ember", FxUtil.SoftCircleSprite, new Color(0.78f, 0.45f, 1f, 1f));
                ResetEmber(ref embers[i], scatter: true);
            }
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            if (moonGlow != null)
            {
                float breath = 0.5f + 0.5f * Mathf.Sin(t * 0.7f);
                moonGlow.color = new Color(0.82f, 0.7f, 1f, 0.12f + 0.14f * breath);
                moonGlow.rectTransform.localScale = Vector3.one * (0.95f + 0.08f * breath);
            }

            if (fog != null)
            {
                var uv = fog.uvRect;
                uv.x = (t * 0.012f) % 1f;
                fog.uvRect = uv;
                fog.color = new Color(0.85f, 0.75f, 1f, 0.3f + 0.08f * Mathf.Sin(t * 0.4f));
            }

            if (logoGlow != null && !logoGlowHidden)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 1.6f);
                // 시작 연출의 로고 번쩍임: 확 밝아졌다가 천천히 평소 맥동으로 가라앉는다.
                float flash = flashStart < 0f ? 0f : Mathf.Exp(-(t - flashStart) * 2.2f);
                logoGlow.color = new Color(0.85f + 0.15f * flash, 0.7f + 0.3f * flash, 1f, Mathf.Min(1f, 0.12f + 0.28f * pulse + 0.9f * flash));
                // 빛줄기는 번쩍임과 같은 순간에 지나가도록 주기 기준을 그때로 맞춘다.
                UpdateSweep(flashStart < 0f ? t : t - flashStart);
            }

            for (int i = 0; i < petals.Length; i++) UpdatePetal(ref petals[i], dt);
            for (int i = 0; i < embers.Length; i++) UpdateEmber(ref embers[i], dt);
        }

        // 빛줄기: SweepInterval마다 SweepDuration 동안 로고 왼쪽 끝에서 오른쪽 끝까지 띠 모양 창(RectMask2D)을 옮긴다.
        // 창 안의 그림은 화면 전체 크기로 펼쳐 둬서(창이 움직여도 제자리) 로고 글자 모양만 밝게 드러난다.
        private void UpdateSweep(float t)
        {
            float cycle = t % SweepInterval;
            bool active = cycle < SweepDuration;
            if (sweepMask.gameObject.activeSelf != active) sweepMask.gameObject.SetActive(active);
            if (!active) return;

            float k = cycle / SweepDuration;
            k = k * k * (3f - 2f * k);
            float u = Mathf.Lerp(LogoLeft - SweepWidth, LogoRight, k);
            sweepMask.anchorMin = new Vector2(u, 0f);
            sweepMask.anchorMax = new Vector2(u + SweepWidth, 1f);
            // 창(폭 SweepWidth) 기준으로 화면 전체를 덮도록 자식 앵커를 거꾸로 잡는다.
            sweepImage.anchorMin = new Vector2(-u / SweepWidth, 0f);
            sweepImage.anchorMax = new Vector2((1f - u) / SweepWidth, 1f);
            sweepImage.offsetMin = sweepImage.offsetMax = Vector2.zero;
        }

        // ---- 꽃잎: 왼쪽 위에서 바람을 타고 오른쪽 아래로, 흔들리며 뒤집히듯 돈다 ----

        private void ResetPetal(ref Particle p, bool scatter)
        {
            p.Pos = scatter ? new Vector2(Random.Range(-0.1f, 1f), Random.Range(0f, 1.1f))
                            : new Vector2(Random.Range(-0.25f, 0.75f), Random.Range(1.03f, 1.15f));
            p.Vel = new Vector2(Random.Range(0.035f, 0.075f), -Random.Range(0.05f, 0.09f));
            p.Phase = Random.Range(0f, Mathf.PI * 2f);
            p.Spin = Random.Range(1.5f, 3.5f) * (Random.value < 0.5f ? -1f : 1f);
            p.Size = Random.Range(18f, 34f); // 그림 속 꽃잎(약 30px)과 비슷하게
            p.BaseAlpha = Random.Range(0.55f, 0.95f);
            p.Age = 0f;
        }

        private void UpdatePetal(ref Particle p, float dt)
        {
            p.Age += dt;
            float sway = Mathf.Sin(p.Age * 1.7f + p.Phase);
            p.Pos += new Vector2(p.Vel.x + sway * 0.02f, p.Vel.y) * dt;
            if (p.Pos.y < -0.06f || p.Pos.x > 1.08f) ResetPetal(ref p, scatter: false);

            p.Rect.anchorMin = p.Rect.anchorMax = p.Pos;
            p.Rect.sizeDelta = new Vector2(p.Size, p.Size * 0.6f);
            p.Rect.localRotation = Quaternion.Euler(0f, 0f, p.Age * p.Spin * 40f + p.Phase * 57f);
            // 뒤집히듯 납작해졌다 펴진다.
            p.Rect.localScale = new Vector3(Mathf.Lerp(0.25f, 1f, Mathf.Abs(Mathf.Cos(p.Age * p.Spin + p.Phase))), 1f, 1f);
            float fadeIn = Mathf.Clamp01(p.Age / 0.8f);
            p.Image.color = new Color(1f, 0.94f, 1f, p.BaseAlpha * fadeIn);
        }

        // ---- 불씨: 아래에서 천천히 피어올라 깜빡이다 사라진다 ----

        private void ResetEmber(ref Particle p, bool scatter)
        {
            p.Pos = new Vector2(Random.Range(0f, 1f), scatter ? Random.Range(0f, 0.7f) : Random.Range(-0.03f, 0.12f));
            p.Vel = new Vector2(Random.Range(-0.008f, 0.012f), Random.Range(0.035f, 0.08f));
            p.Life = Random.Range(3f, 6.5f);
            p.Age = scatter ? Random.Range(0f, p.Life) : 0f;
            p.Phase = Random.Range(0f, Mathf.PI * 2f);
            p.Size = Random.Range(8f, 18f);
            p.BaseAlpha = Random.Range(0.45f, 0.95f);
        }

        private void UpdateEmber(ref Particle p, float dt)
        {
            p.Age += dt;
            if (p.Age >= p.Life) { ResetEmber(ref p, scatter: false); }
            p.Pos += new Vector2(p.Vel.x + Mathf.Sin(p.Age * 2.1f + p.Phase) * 0.006f, p.Vel.y) * dt;

            float k = p.Age / p.Life;
            float envelope = Mathf.Clamp01(k / 0.15f) * Mathf.Clamp01((1f - k) / 0.35f);
            float flicker = 0.7f + 0.3f * Mathf.Sin(p.Age * 11f + p.Phase * 3f);
            p.Rect.anchorMin = p.Rect.anchorMax = p.Pos;
            p.Rect.sizeDelta = Vector2.one * p.Size * (1f - 0.4f * k);
            p.Image.color = new Color(0.95f, 0.72f, 1f, p.BaseAlpha * envelope * flicker); // 배경 보라에 묻히지 않게 밝은 분홍빛 보라
        }

        // ---- 만들기 도우미 ----

        private Particle NewParticle(string name, Sprite sprite, Color color)
        {
            var image = MakeImage(name, sprite, color);
            return new Particle { Rect = image.rectTransform, Image = image };
        }

        private Image MakeImage(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RawImage MakeRaw(string name, Transform parent, Texture texture, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var raw = go.GetComponent<RawImage>();
            raw.texture = texture;
            raw.color = color;
            raw.raycastTarget = false;
            return raw;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // 가장자리가 부드러운 작은 꽃잎(한쪽 끝이 살짝 뾰족한 타원).
        private static Sprite MakePetalSprite()
        {
            const int W = 32, H = 20;
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f;
                    float v = (y + 0.5f) / H * 2f - 1f;
                    // 오른쪽 끝으로 갈수록 가늘어지는 꽃잎 모양
                    float halfHeight = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)) * (1f - 0.35f * Mathf.Max(0f, u));
                    float d = Mathf.Abs(v) - halfHeight;
                    float alpha = Mathf.Clamp01(-d * 4f);
                    float shade = 0.85f + 0.15f * (1f - Mathf.Abs(v));
                    px[y * W + x] = new Color(shade, shade, shade, alpha);
                }
            }
            texture.SetPixels(px);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}

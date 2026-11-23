using System.Collections.Generic;
using UnityEngine;

namespace Aethoria.Fx
{
    // 추가 그림 없이 코드로 만든 텍스처(부드러운 원, 반짝이, 빛기둥, 동전 등)와 파티클 버스트를 한곳에서 만든다.
    // 모든 이펙트는 Sprites/Default(조명 영향 없음) 재질을 써서 2D 조명 밝기와 상관없이 같은 색으로 보인다.
    public static class FxUtil
    {
        private static readonly Dictionary<Texture, Material> materials = new Dictionary<Texture, Material>();
        private static Material spriteMaterial;
        private static Texture2D softCircle, sparkle, pillar, coin, gem, ring;
        private static Sprite softCircleSprite, sparkleSprite, pillarSprite, coinSprite, gemSprite, ringSprite;

        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null) spriteMaterial = new Material(Shader.Find("Sprites/Default"));
                return spriteMaterial;
            }
        }

        public static Material MaterialFor(Texture texture)
        {
            if (texture == null) return SpriteMaterial;
            if (materials.TryGetValue(texture, out var mat) && mat != null) return mat;
            mat = new Material(SpriteMaterial) { mainTexture = texture };
            materials[texture] = mat;
            return mat;
        }

        public static Texture2D SoftCircle => softCircle != null ? softCircle : softCircle = MakeTexture(64, 64, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01(1f - r);
            return new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
        });

        // 십자로 뻗은 반짝이(가운데는 동그란 심).
        public static Texture2D Sparkle => sparkle != null ? sparkle : sparkle = MakeTexture(64, 64, (x, y) =>
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float cross = Mathf.Max(Mathf.Exp(-ay * 14f) * (1f - ax), Mathf.Exp(-ax * 14f) * (1f - ay));
            float core = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) * 3f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(cross * 1.2f + core));
        });

        // 아래가 진하고 위로 갈수록 사라지는 세로 빛기둥.
        public static Texture2D Pillar => pillar != null ? pillar : pillar = MakeTexture(32, 128, (x, y) =>
        {
            float horizontal = Mathf.Exp(-x * x * 5f);
            float v = (y + 1f) * 0.5f; // 0(아래)~1(위)
            float vertical = Mathf.Clamp01(1f - v) * Mathf.Clamp01(v * 12f);
            return new Color(1f, 1f, 1f, horizontal * vertical);
        });

        public static Texture2D Coin => coin != null ? coin : coin = MakeTexture(32, 32, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            if (r > 0.95f) return Color.clear;
            var rim = new Color(0.72f, 0.48f, 0.08f);
            var face = new Color(1f, 0.82f, 0.25f);
            var c = r > 0.75f ? rim : face;
            float shine = Mathf.Clamp01(1f - Mathf.Sqrt((x + 0.35f) * (x + 0.35f) + (y - 0.35f) * (y - 0.35f)) * 2.5f);
            c = Color.Lerp(c, Color.white, shine * 0.7f);
            c.a = Mathf.Clamp01((0.95f - r) * 20f);
            return c;
        });

        // 아이콘이 없는 아이템 대신 쓰는 보석 모양.
        public static Texture2D Gem => gem != null ? gem : gem = MakeTexture(32, 32, (x, y) =>
        {
            float d = Mathf.Abs(x) + Mathf.Abs(y);
            if (d > 0.9f) return Color.clear;
            float light = Mathf.Clamp01(0.6f + (y - x) * 0.4f);
            return new Color(light, light, light, Mathf.Clamp01((0.9f - d) * 15f));
        });

        public static Texture2D Ring => ring != null ? ring : ring = MakeTexture(64, 64, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Exp(-Mathf.Pow((r - 0.8f) * 9f, 2f));
            return new Color(1f, 1f, 1f, a);
        });

        public static Sprite SoftCircleSprite => softCircleSprite != null ? softCircleSprite : softCircleSprite = ToSprite(SoftCircle, 64f);
        public static Sprite SparkleSprite => sparkleSprite != null ? sparkleSprite : sparkleSprite = ToSprite(Sparkle, 64f);
        public static Sprite PillarSprite => pillarSprite != null ? pillarSprite : pillarSprite = ToSprite(Pillar, 32f, new Vector2(0.5f, 0f));
        public static Sprite CoinSprite => coinSprite != null ? coinSprite : coinSprite = ToSprite(Coin, 128f);
        public static Sprite GemSprite => gemSprite != null ? gemSprite : gemSprite = ToSprite(Gem, 80f);
        public static Sprite RingSprite => ringSprite != null ? ringSprite : ringSprite = ToSprite(Ring, 64f);

        private static Sprite ToSprite(Texture2D texture, float ppu, Vector2? pivot = null)
        {
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot ?? new Vector2(0.5f, 0.5f), ppu);
        }

        // f(x, y): x, y는 -1~1(가운데가 0).
        private static Texture2D MakeTexture(int width, int height, System.Func<float, float, Color> f)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width * height];
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    float x = (i + 0.5f) / width * 2f - 1f;
                    float y = (j + 0.5f) / height * 2f - 1f;
                    pixels[j * width + i] = f(x, y);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // 한 번 터지고 사라지는 파티클.
        public struct BurstSpec
        {
            public int count;
            public float lifeMin, lifeMax;
            public float speedMin, speedMax;
            public float sizeMin, sizeMax;
            public float sizeEnd;      // 수명 끝의 크기 배율(먼지는 커지며 옅어지고, 불꽃은 작아진다)
            public float gravity;      // 양수면 떨어지고 음수면 떠오른다
            public float drag;
            public float arcStart, arc; // 뿜는 방향(도, 0 = 오른쪽, 반시계)
            public float radius;       // 뿜는 원 반지름
            public Color colorA, colorB;
            public Texture2D texture;
            public int sortingOrder;
        }

        public static ParticleSystem Burst(Vector3 position, BurstSpec s)
        {
            var go = new GameObject("FxBurst");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = Mathf.Max(0.05f, s.lifeMax);
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.lifeMin, s.lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.speedMin, s.speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(s.sizeMin, s.sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(s.colorA, s.colorB);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = s.gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = Mathf.Max(8, s.count);

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.001f, s.radius);
            shape.arc = s.arc <= 0f ? 360f : s.arc;
            shape.rotation = new Vector3(0f, 0f, s.arcStart);

            ApplyFadeAndSize(ps, s.sizeEnd <= 0f ? 1f : s.sizeEnd);

            if (s.drag > 0f)
            {
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.drag = new ParticleSystem.MinMaxCurve(s.drag);
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialFor(s.texture != null ? s.texture : SoftCircle);
            renderer.sortingOrder = s.sortingOrder;

            ps.Play();
            ps.Emit(s.count);
            return ps;
        }

        public static void ApplyFadeAndSize(ParticleSystem ps, float sizeEnd, float fadeInEnd = 0f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                fadeInEnd > 0f
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeInEnd), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, sizeEnd));
        }

        // 그림 한 장짜리 이펙트(빛기둥 등)용 스프라이트 오브젝트.
        public static SpriteRenderer MakeSprite(string name, Sprite sprite, Vector3 position, Color color, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = SpriteMaterial;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        // 렌더러의 정렬 순서 바로 위/아래에 이펙트를 겹치게 할 때 쓴다.
        public static int OrderOf(Component c, int offset = 0)
        {
            var sr = c != null ? c.GetComponentInChildren<SpriteRenderer>() : null;
            return (sr != null ? sr.sortingOrder : 0) + offset;
        }
    }
}

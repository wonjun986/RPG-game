using UnityEngine;
using UnityEngine.Rendering.Universal;
using Aethoria.Bootstrap;
using Aethoria.Fx;

namespace Aethoria.Stages
{
    // 마을 장식 소품 공통: 그림(아래 가운데 피벗)을 원하는 높이로 세우고, 그림 픽셀 좌표를 로컬 좌표로 바꿔 준다.
    public static class VillageProps
    {
        public const string Folder = "Backgrounds/Parallax/Village/Props/";

        // position: 밑면 가운데. height: 화면에 보일 높이(칸). 그림이 없으면 null.
        public static SpriteRenderer SpawnSprite(Transform parent, string name, Vector3 position, float height, int sortingOrder)
        {
            var sprite = Resources.Load<Sprite>(Folder + name);
            if (sprite == null) return null;
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = position;
            float scale = height / sprite.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        // 그림 속 픽셀(왼쪽 위 기준) → 그 스프라이트 오브젝트의 로컬 좌표(피벗 = 아래 가운데).
        public static Vector3 PixelToLocal(Sprite sprite, float px, float py)
        {
            Vector2 size = sprite.rect.size;
            return new Vector3((px - size.x / 2f) / sprite.pixelsPerUnit, (size.y - py) / sprite.pixelsPerUnit, 0f);
        }
    }

    // 고딕 가로등: 등불 자리에 따뜻한 빛(조명 + 번짐)을 달고 촛불처럼 일렁이게 한다.
    public class StreetLamp : MonoBehaviour
    {
        private static readonly Vector2 FlamePixel = new Vector2(201f, 413f); // village_prop_lamp(371x2085) 속 불꽃
        private static readonly Color FlameColor = new Color(1f, 0.72f, 0.38f);

        private Light2D flameLight;
        private SpriteRenderer glow;
        private float baseIntensity;
        private float seed;

        public static StreetLamp Spawn(Transform parent, Vector3 position, float height, int sortingOrder)
        {
            var sr = VillageProps.SpawnSprite(parent, "village_prop_lamp", position, height, sortingOrder);
            if (sr == null) return null;
            var lamp = sr.gameObject.AddComponent<StreetLamp>();
            lamp.Build(sr, sortingOrder);
            return lamp;
        }

        private void Build(SpriteRenderer sr, int sortingOrder)
        {
            seed = Random.Range(0f, 100f);
            Vector3 flame = VillageProps.PixelToLocal(sr.sprite, FlamePixel.x, FlamePixel.y);
            float inv = 1f / transform.localScale.x; // 자식은 부모 배율을 받으므로 월드 크기로 맞춰 나눈다
            baseIntensity = 0.85f;
            flameLight = SceneLighting.AddPointLight(transform, flame, FlameColor, 3.4f * inv, baseIntensity);

            glow = FxUtil.MakeSprite("LampGlow", FxUtil.SoftCircleSprite, transform.TransformPoint(flame), new Color(FlameColor.r, FlameColor.g, FlameColor.b, 0.35f), sortingOrder + 2, transform);
            glow.transform.localScale = Vector3.one * (1.1f * inv);
        }

        private void Update()
        {
            float t = Time.time + seed;
            float flicker = 0.88f + 0.07f * Mathf.Sin(t * 13f) + 0.05f * Mathf.Sin(t * 29f + 1.3f) + 0.04f * (Mathf.PerlinNoise(t * 3f, seed) - 0.5f);
            if (flameLight != null) flameLight.intensity = baseIntensity * flicker;
            if (glow != null) glow.color = new Color(FlameColor.r, FlameColor.g, FlameColor.b, 0.3f * flicker);
        }
    }

    // 깃발 기둥: 사슬에 매달린 깃발 천만 따로 떼어(village_prop_banner_cloth) 사슬 윗부분을 축으로 바람에 살짝 흔들리게 한다.
    public class HangingBanner : MonoBehaviour
    {
        private static readonly Vector2 HangPixel = new Vector2(368f, 372f); // 기둥 그림(613x2118) 속 사슬이 팔에 걸린 자리
        private const float ClothPivotX = 172f;                              // 천 그림(367x965) 속 같은 자리의 x(맨 위)

        private Transform pivot;
        private float seed;

        public static HangingBanner Spawn(Transform parent, Vector3 position, float height, int sortingOrder)
        {
            var sr = VillageProps.SpawnSprite(parent, "village_prop_banner", position, height, sortingOrder);
            if (sr == null) return null;
            var banner = sr.gameObject.AddComponent<HangingBanner>();
            banner.Build(sr, sortingOrder);
            return banner;
        }

        private void Build(SpriteRenderer sr, int sortingOrder)
        {
            seed = Random.Range(0f, 100f);
            var clothSprite = Resources.Load<Sprite>(VillageProps.Folder + "village_prop_banner_cloth");
            if (clothSprite == null) return;

            pivot = new GameObject("BannerPivot").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = VillageProps.PixelToLocal(sr.sprite, HangPixel.x, HangPixel.y);

            var cloth = new GameObject("BannerCloth", typeof(SpriteRenderer));
            cloth.transform.SetParent(pivot, false);
            // 천 그림 피벗(아래 가운데)이 축(맨 위의 ClothPivotX)에서 얼마나 떨어져 있는지.
            Vector2 size = clothSprite.rect.size;
            cloth.transform.localPosition = new Vector3((size.x / 2f - ClothPivotX) / clothSprite.pixelsPerUnit, -size.y / clothSprite.pixelsPerUnit, 0f);
            var clothRenderer = cloth.GetComponent<SpriteRenderer>();
            clothRenderer.sprite = clothSprite;
            clothRenderer.sortingOrder = sortingOrder;
        }

        private void Update()
        {
            if (pivot == null) return;
            float t = Time.time + seed;
            float angle = 2.2f * Mathf.Sin(t * 1.1f) + 0.8f * Mathf.Sin(t * 2.7f + 0.6f);
            pivot.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}

using UnityEngine;
using Aethoria.Bootstrap;
using Aethoria.Fx;

namespace Aethoria.Stages
{
    // 마을 광장의 천사 석상 분수. 원화의 물줄기는 초록 배경 위에 반투명하게 그려져 있어서 초록을 지우면 같이 사라지므로,
    // 석상(정지 그림)과 물(물결 무늬가 아래로 흘러내리는 8프레임)을 따로 만들어 겹친다.
    // 물이 떨어지는 자리마다 물방울이 튀고, 물 빛이 은은하게 주변을 비춘다.
    public class FountainProp : MonoBehaviour
    {
        private const string Folder = "Backgrounds/Parallax/Village/Props/";
        private const int WaterFrameCount = 8;
        private const float WaterFps = 14f;
        private static readonly Color DropletA = new Color(0.95f, 0.95f, 1f);
        private static readonly Color DropletB = new Color(0.75f, 0.7f, 1f);

        // 물이 떨어지는 자리(석상 그림 995x1445 속 픽셀, 위에서부터). 항아리 물줄기가 위 수반에, 양옆 폭포가 아래 수반에 닿는 곳.
        private static readonly Vector2[] SplashPixels =
        {
            new Vector2(633f, 829f),
            new Vector2(212f, 1110f), new Vector2(290f, 1092f),
            new Vector2(727f, 1092f), new Vector2(778f, 1128f),
        };

        private SpriteRenderer water;
        private Sprite[] waterFrames;
        private Vector3[] splashLocal;
        private float frameTimer;
        private int frame;
        private float splashTimer;

        // position: 분수 밑면 가운데. height: 화면에 보일 분수 높이(칸).
        public static FountainProp Spawn(Transform parent, Vector3 position, float height, int sortingOrder)
        {
            var baseSprite = Resources.Load<Sprite>(Folder + "village_fountain");
            if (baseSprite == null) return null;

            var go = new GameObject("Fountain", typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = position;
            float scale = height / baseSprite.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = baseSprite;
            sr.sortingOrder = sortingOrder;

            var fountain = go.AddComponent<FountainProp>();
            fountain.Build(baseSprite, sortingOrder);
            return fountain;
        }

        private void Build(Sprite baseSprite, int sortingOrder)
        {
            waterFrames = new Sprite[WaterFrameCount];
            for (int i = 0; i < WaterFrameCount; i++) waterFrames[i] = Resources.Load<Sprite>(Folder + "village_fountain_water_" + i);

            var waterGO = new GameObject("Water", typeof(SpriteRenderer));
            waterGO.transform.SetParent(transform, false);
            water = waterGO.GetComponent<SpriteRenderer>();
            water.sprite = waterFrames[0];
            water.sortingOrder = sortingOrder + 1;

            // 그림 픽셀 → 분수 로컬 좌표(피벗은 아래 가운데).
            float ppu = baseSprite.pixelsPerUnit;
            Vector2 size = baseSprite.rect.size;
            splashLocal = new Vector3[SplashPixels.Length];
            for (int i = 0; i < SplashPixels.Length; i++)
                splashLocal[i] = new Vector3((SplashPixels[i].x - size.x / 2f) / ppu, (size.y - SplashPixels[i].y) / ppu, 0f);

            SceneLighting.AddPointLight(transform, new Vector3(0f, size.y * 0.3f / ppu, 0f), new Color(0.7f, 0.75f, 1f), 3.2f / transform.localScale.x, 0.45f);
        }

        private void Update()
        {
            frameTimer += Time.deltaTime;
            if (frameTimer >= 1f / WaterFps)
            {
                frameTimer -= 1f / WaterFps;
                frame = (frame + 1) % WaterFrameCount;
                if (waterFrames[frame] != null) water.sprite = waterFrames[frame];
            }

            splashTimer -= Time.deltaTime;
            if (splashTimer <= 0f)
            {
                splashTimer = Random.Range(0.06f, 0.14f);
                Vector3 at = transform.TransformPoint(splashLocal[Random.Range(0, splashLocal.Length)]);
                FxUtil.Burst(at, new FxUtil.BurstSpec
                {
                    count = Random.Range(2, 4), lifeMin = 0.25f, lifeMax = 0.45f, speedMin = 0.5f, speedMax = 1.2f,
                    sizeMin = 0.04f, sizeMax = 0.07f, sizeEnd = 0.4f, gravity = 3.5f, drag = 0.5f,
                    arcStart = 55f, arc = 70f, radius = 0.05f,
                    colorA = DropletA, colorB = DropletB, texture = FxUtil.SoftCircle, sortingOrder = water.sortingOrder + 1,
                });
            }
        }
    }
}

using UnityEngine;
using Aethoria.Stages;

namespace Aethoria.Bootstrap
{
    // 지역별 시차 배경 층 구성(ParallaxBackground). 그림은 Resources/Backgrounds/Parallax/지역/에 있고,
    // ParallaxLayerPostprocessor가 1칸 = 135px, 아래 가운데 피벗으로 가져온다.
    // 높이(BottomAboveFloor)는 카메라가 기본 높이(땅에 서 있을 때)일 때 기준이다. 그때 화면은 바닥 아래 1.6칸 ~ 위 6.4칸을 보여 준다.
    public static partial class GameBootstrap
    {
        // FitCamera와 같은 값: 화면 크기(orthoSize 4)의 0.6만큼 바닥 위에 카메라가 선다.
        private const float CameraRestAboveFloor = 4f * 0.6f;
        // 시차 배경은 하늘을 위로 길게 늘려 두므로, 카메라가 이만큼 올라가도 빈 곳이 보이지 않는다.
        private const float ParallaxBackgroundTop = 30f;

        private const string ForestParallaxFolder = "Backgrounds/Parallax/Forest/";

        // 어둠의 숲: 하늘(달·먼 산) → 먼 침엽수 숲 → 가까운 고목 숲 → 흙길 띠.
        // (화면 가장자리에 붙어 따라다니는 앞 장식 층은 캐릭터를 따라 움직이는 게 어색해서 뺐다.)
        private static ParallaxBackground.Layer[] ForestParallaxLayers() => new[]
        {
            // 하늘(2400x1600 → 17.8x11.9칸): 거의 고정. 좌우가 이어지는 그림이 아니라 넓은 화면에서는 반전해 이어 붙인다.
            new ParallaxBackground.Layer
            {
                Path = ForestParallaxFolder + "forest_sky", ScrollX = 0.1f, ScrollY = 0.05f, BottomAboveFloor = -1.8f,
                SortingOrder = -15, Mirror = true, ExtendTop = true, ExtendBottom = true,
            },
            // 먼 숲(2048x1100): 나무 꼭대기가 바닥 위 약 3.8칸. 밑은 흙길 띠 뒤에 숨는다.
            new ParallaxBackground.Layer
            {
                Path = ForestParallaxFolder + "forest_far", ScrollX = 0.3f, ScrollY = 0.3f, BottomAboveFloor = -0.8f,
                SortingOrder = -14, ExtendBottom = true,
            },
            // 먼 숲과 가까운 숲 사이를 천천히 흐르는 안개(2048x795, 띠 가운데가 밑변 위 약 2.9칸 → 바닥 위 약 1.5칸).
            new ParallaxBackground.Layer
            {
                Path = ForestParallaxFolder + "forest_fog", ScrollX = 0.45f, ScrollY = 0.45f, BottomAboveFloor = -1.45f,
                SortingOrder = -13, Tint = new Color(1f, 1f, 1f, 0.55f), Drift = 0.15f,
            },
            // 가까운 숲(2048x1569 → 15.2x11.6칸): 굵은 고목과 덩굴. 발판·캐릭터가 돋보이게 조금 어둡게 누른다.
            // 세로는 카메라를 많이 따라가게(ScrollY 0.35) 둔다 — 숲 2번 꼭대기(바닥 위 11)까지 올라가도 그림 위끝(나무가 잘린 선)이
            // 화면 안으로 내려오지 않는다. 위끝 = 바닥 + 10.8 + 0.65·(카메라 상승) ≥ 화면 위끝 = 바닥 + 6.4 + 상승 → 상승 12.5칸까지 안전.
            new ParallaxBackground.Layer
            {
                Path = ForestParallaxFolder + "forest_near", ScrollX = 0.6f, ScrollY = 0.35f, BottomAboveFloor = -0.8f,
                SortingOrder = -12, ExtendBottom = true, Tint = new Color(0.68f, 0.66f, 0.75f),
            },
            // 흙길 띠(2048x458): 밝은 돌길 윗면이 위에서 122~160px라 그 가운데(148px)를 밟는 선으로 삼는다 — 윗면 맨 위
            // 가장자리(124px)에 세우면 발이 돌길 뒤쪽 끝에 걸려 떠 보였다. 밑변은 바닥 아래 310px(2.3칸). 땅이라 세상에 붙어 있다.
            new ParallaxBackground.Layer
            {
                Path = ForestParallaxFolder + "forest_ground", ScrollX = 1f, ScrollY = 1f, BottomAboveFloor = -310f / 135f,
                SortingOrder = -9, ExtendBottom = true,
            },
            // 땅 가까이 깔리는 옅은 안개(고목·가까운 숲 앞, 흙길 뒤). 반대 방향으로 조금 흘러 겹쳐 보이게.
            new ParallaxBackground.Layer
            {
                Path = ForestParallaxFolder + "forest_fog", ScrollX = 0.8f, ScrollY = 0.8f, BottomAboveFloor = -2.5f,
                SortingOrder = -10, Tint = new Color(0.9f, 0.85f, 1f, 0.32f), Drift = -0.1f,
            },
        };

        private const string PlainsParallaxFolder = "Backgrounds/Parallax/Plains/";

        // 광활한 평야: 뭉게구름 낮하늘(먼 첨탑 성) → 폭포 절벽과 유적 → 고목과 고딕 폐허 아치 → 들꽃 흙길.
        // 평야는 발판으로 바닥 위 약 5칸까지 오르므로, 가까운 층은 숲처럼 세로로 카메라를 더 따라가게(0.6) 둔다.
        private static ParallaxBackground.Layer[] PlainsParallaxLayers() => new[]
        {
            // 하늘(2400x1600, 0.9배): 해가 왼쪽 위, 먼 첨탑 성이 가운데 오른쪽 산 위.
            new ParallaxBackground.Layer
            {
                Path = PlainsParallaxFolder + "plains_sky", Scale = 0.9f, ScrollX = 0.1f, ScrollY = 0.05f, BottomAboveFloor = -1.6f,
                SortingOrder = -15, Mirror = true, ExtendTop = true, ExtendBottom = true,
            },
            // 폭포 절벽과 고대 유적(2048x701, 0.85배).
            new ParallaxBackground.Layer
            {
                Path = PlainsParallaxFolder + "plains_far", Scale = 0.85f, ScrollX = 0.3f, ScrollY = 0.3f, BottomAboveFloor = -0.4f,
                SortingOrder = -14, ExtendBottom = true,
            },
            // 고목과 고딕 폐허 아치(2048x1478, 0.62배 → 높이 약 6.8칸). 발판(-5)이 돋보이게 아주 살짝 누른다.
            new ParallaxBackground.Layer
            {
                Path = PlainsParallaxFolder + "plains_near", Scale = 0.62f, ScrollX = 0.6f, ScrollY = 0.6f, BottomAboveFloor = -0.7f,
                SortingOrder = -12, Tint = new Color(0.92f, 0.94f, 0.92f),
            },
            // 들꽃 흙길 띠(2048x656): 평평한 돌이 박힌 흙길 윗면이 위에서 약 130px.
            new ParallaxBackground.Layer
            {
                Path = PlainsParallaxFolder + "plains_ground", ScrollX = 1f, ScrollY = 1f, BottomAboveFloor = -(656f - 130f) / 135f,
                SortingOrder = -9, ExtendBottom = true,
            },
        };

        private const string HillParallaxFolder = "Backgrounds/Parallax/Hill/";

        // 절망의 언덕: 피빛 달 아래 성채가 보이는 붉은 하늘 → 붉은 폭포 절벽 → 핏빛 안개 → 고딕 폐허와 수정 → 낮게 깔린 안개 → 용암 균열 바닥.
        private static ParallaxBackground.Layer[] HillParallaxLayers() => new[]
        {
            new ParallaxBackground.Layer
            {
                Path = HillParallaxFolder + "hill_sky", Scale = 0.9f, ScrollX = 0.1f, ScrollY = 0.05f, BottomAboveFloor = -1.8f,
                SortingOrder = -15, Mirror = true, ExtendTop = true, ExtendBottom = true,
            },
            new ParallaxBackground.Layer
            {
                Path = HillParallaxFolder + "hill_far", Scale = 0.85f, ScrollX = 0.3f, ScrollY = 0.3f, BottomAboveFloor = -0.5f,
                SortingOrder = -14, ExtendBottom = true,
            },
            new ParallaxBackground.Layer
            {
                Path = HillParallaxFolder + "hill_fog", ScrollX = 0.45f, ScrollY = 0.45f, BottomAboveFloor = -1.2f,
                SortingOrder = -13, Tint = new Color(1f, 1f, 1f, 0.5f), Drift = 0.2f,
            },
            new ParallaxBackground.Layer
            {
                Path = HillParallaxFolder + "hill_near", Scale = 0.62f, ScrollX = 0.6f, ScrollY = 0.6f, BottomAboveFloor = -0.7f,
                SortingOrder = -12, Tint = new Color(0.78f, 0.72f, 0.75f),
            },
            new ParallaxBackground.Layer
            {
                Path = HillParallaxFolder + "hill_fog", ScrollX = 0.8f, ScrollY = 0.8f, BottomAboveFloor = -2.4f,
                SortingOrder = -10, Tint = new Color(1f, 0.85f, 0.85f, 0.3f), Drift = -0.12f,
            },
            // 용암 균열 바닥 띠(2048x264): 갈라진 검은 돌길 윗면이 위에서 약 58px.
            new ParallaxBackground.Layer
            {
                Path = HillParallaxFolder + "hill_ground", ScrollX = 1f, ScrollY = 1f, BottomAboveFloor = -(264f - 58f) / 135f,
                SortingOrder = -9, ExtendBottom = true,
            },
        };

        private const string VillageParallaxFolder = "Backgrounds/Parallax/Village/";

        // 마을: 달과 산 위 고딕 성이 뜬 밤하늘 → 먼 마을 지붕선 → 등나무꽃 핀 고딕 집 거리 → 돌바닥.
        // 마을은 발판이 없어 카메라가 위로 올라가지 않으므로, 화면 세로 8칸 안에 달·집·바닥이 다 들어오게 크기를 줄여 맞췄다.
        private static ParallaxBackground.Layer[] VillageParallaxLayers() => new[]
        {
            // 하늘(2400x1600, 0.9배 → 16x10.7칸): 화면 폭(약 14.2칸)보다 넓어야 반전 복사본(달이 두 개)이 안 보인다.
            // 달이 화면 위쪽(바닥 위 약 5.5칸)에 오게 내렸다. 산 위 성은 집들 뒤에 가려 틈으로만 보인다.
            new ParallaxBackground.Layer
            {
                Path = VillageParallaxFolder + "village_sky", Scale = 0.9f, ScrollX = 0.1f, ScrollY = 0.05f, BottomAboveFloor = -2.3f,
                SortingOrder = -15, Mirror = true, ExtendTop = true, ExtendBottom = true,
            },
            // 먼 마을 지붕선(2048x1213, 0.85배): 집 사이 틈으로 보이는 첨탑과 창문 불빛.
            new ParallaxBackground.Layer
            {
                Path = VillageParallaxFolder + "village_far", Scale = 0.85f, ScrollX = 0.3f, ScrollY = 0.3f, BottomAboveFloor = -0.6f,
                SortingOrder = -14, ExtendBottom = true,
            },
            // 고딕 집 거리(2048x1312, 0.62배 → 집 높이 약 6칸, 문 높이가 캐릭터 키쯤). 양 끝이 비어 있어 그대로 이어 붙인다.
            new ParallaxBackground.Layer
            {
                Path = VillageParallaxFolder + "village_near", Scale = 0.62f, ScrollX = 0.6f, ScrollY = 0.6f, BottomAboveFloor = -0.5f,
                SortingOrder = -12, Tint = new Color(0.8f, 0.78f, 0.85f),
            },
            // 돌바닥 띠(2048x362): 돌바닥 윗면이 위에서 50~118px라 그 가운데(85px)를 밟는 선으로 삼는다.
            new ParallaxBackground.Layer
            {
                Path = VillageParallaxFolder + "village_ground", ScrollX = 1f, ScrollY = 1f, BottomAboveFloor = -(362f - 85f) / 135f,
                SortingOrder = -9, ExtendBottom = true,
            },
        };

        private const string RuinsParallaxFolder = "Backgrounds/Parallax/Ruins/";

        // 잊혀진 폐허: 사신상이 선 황금빛 노을 하늘 → 모래 언덕 너머 해골 장미창 성당 → 모래에 묻힌 사암 열주 → 사암 성소 바닥.
        // 원화(Sprite/Map Sprite/05_Ruins)의 초록 배경을 지우고 위아래 빈 줄을 잘라 넣었다. 양 끝이 이어지지 않는 그림이라
        // 하나 건너 좌우 반전해 이어 붙인다.
        private static ParallaxBackground.Layer[] RuinsParallaxLayers() => new[]
        {
            // 하늘(1536x1024 원화를 2400x1600으로 키움, 0.9배): 지평선의 해와 사신상 실루엣이 먼 모래 언덕 바로 위에 오게.
            new ParallaxBackground.Layer
            {
                Path = RuinsParallaxFolder + "ruins_sky", Scale = 0.9f, ScrollX = 0.1f, ScrollY = 0.05f, BottomAboveFloor = 0.5f,
                SortingOrder = -15, Mirror = true, ExtendTop = true, ExtendBottom = true,
            },
            // 모래 언덕과 해골 장미창 성당(1711x869): 밑까지 모래로 꽉 차 있어 아래로 늘려 깐다.
            new ParallaxBackground.Layer
            {
                Path = RuinsParallaxFolder + "ruins_far", ScrollX = 0.3f, ScrollY = 0.3f, BottomAboveFloor = -0.6f,
                SortingOrder = -14, Mirror = true, ExtendBottom = true, Tint = new Color(0.95f, 0.92f, 0.9f),
            },
            // 모래에 묻힌 사암 열주(1517x898, 0.8배 → 높이 약 5.3칸): 해골·낫이 새겨진 기둥, 찢어진 붉은 깃발, 흘러내리는 모래.
            new ParallaxBackground.Layer
            {
                Path = RuinsParallaxFolder + "ruins_near", Scale = 0.8f, ScrollX = 0.6f, ScrollY = 0.6f, BottomAboveFloor = -0.4f,
                SortingOrder = -12, Mirror = true, Tint = new Color(0.8f, 0.74f, 0.72f), // 캐릭터·몬스터가 돋보이게 누른다
            },
            // 사암 성소 바닥(2172x387): 모래 덮인 돌판 윗면(위에서 약 30px)을 밟는 선으로 삼고, 그 아래 해골 부조 기초벽이 그림 밑까지 꽉 차 있다.
            new ParallaxBackground.Layer
            {
                Path = RuinsParallaxFolder + "ruins_ground", ScrollX = 1f, ScrollY = 1f, BottomAboveFloor = -(387f - 30f) / 135f,
                SortingOrder = -9, Mirror = true, ExtendBottom = true,
            },
        };

        private const string EnsroomParallaxFolder = "Backgrounds/Parallax/Ensroom/";

        // 엔스룸 마을: 하늘섬의 하얀 성이 뜬 낮하늘 → 폭포 절벽 위 성채 도시 → 하얀 대리석·금장식 거리 → 대리석 광장 바닥.
        // 원화(Sprite/Map Sprite/07_Ensroom)의 초록 배경을 지우고 위아래 빈 줄을 잘라 넣었다. 먼 도시·거리·바닥은 양 끝이
        // 이어지지 않는 그림이라 하나 건너 좌우 반전해 이어 붙인다(이음매가 거울처럼 맞물린다).
        private static ParallaxBackground.Layer[] EnsroomParallaxLayers() => new[]
        {
            // 하늘(1536x1024 원화를 2400x1600으로 키움, 0.9배 → 16x10.7칸): 하늘섬 성이 거리 지붕 위(바닥 위 약 5칸)에 보이게 올렸다.
            new ParallaxBackground.Layer
            {
                Path = EnsroomParallaxFolder + "ensroom_sky", Scale = 0.9f, ScrollX = 0.1f, ScrollY = 0.05f, BottomAboveFloor = 2f,
                SortingOrder = -15, Mirror = true, ExtendTop = true, ExtendBottom = true,
            },
            // 폭포 절벽 위 성채 도시(2172x485, 1.2배): 거리의 낮은 난간·아치 사이로 보인다. 밑은 거리 뒤에 숨는다.
            new ParallaxBackground.Layer
            {
                Path = EnsroomParallaxFolder + "ensroom_far", Scale = 1.2f, ScrollX = 0.3f, ScrollY = 0.3f, BottomAboveFloor = -0.2f,
                SortingOrder = -14, Mirror = true, ExtendBottom = true, Tint = new Color(0.94f, 0.94f, 0.97f),
            },
            // 하얀 대리석 거리(2172x683, 1배 → 높이 약 5칸, 문 높이가 캐릭터 키의 절반쯤 — 한 걸음 뒤 거리라 작게).
            new ParallaxBackground.Layer
            {
                Path = EnsroomParallaxFolder + "ensroom_near", ScrollX = 0.6f, ScrollY = 0.6f, BottomAboveFloor = -0.3f,
                SortingOrder = -12, Mirror = true, Tint = new Color(0.84f, 0.83f, 0.9f), // 캐릭터가 돋보이게 누른다
            },
            // 대리석 광장 바닥 띠(2172x383): 타일 윗면이 위에서 5~92px, 그 아래 금장식 난간 앞면(~175px), 그 밑은 덧댄 불투명한
            // 라벤더빛 돌 받침(원화 밑이 투명이라 늘려 깔 색이 없어 하늘색이 비쳤다). 윗면 앞쪽(60px)을 밟는 선으로 삼는다.
            new ParallaxBackground.Layer
            {
                Path = EnsroomParallaxFolder + "ensroom_ground", ScrollX = 1f, ScrollY = 1f, BottomAboveFloor = -(383f - 60f) / 135f,
                SortingOrder = -9, Mirror = true, ExtendBottom = true,
            },
        };

        private const string SeaParallaxFolder = "Backgrounds/Parallax/Sea/";
        // 바다 바닥 띠(2172x234): 돌길 윗면이 위에서 0~45px라 앞쪽(30px)을 밟는 선으로 삼는다. 붕괴 맵은 이 띠를 직접 깐다.
        private const string SeaGroundPath = SeaParallaxFolder + "sea_ground";
        private const float SeaGroundHeightPx = 234f;
        private const float SeaGroundWalkPx = 30f;

        // 푸른 바다: 붉은 달이 걸린 수평선과 물속 유적이 비치는 바다 → 떠 있는 섬·무너진 다리·물 위 신전 → 물보라 → 산호와 수정이 자란
        // 물 속 아치·기둥 → 낮게 깔린 물보라 → 산호 핀 고대 석조 둑길 바닥. 원화(Sprite/Map Sprite/06_BlueSea/Parallax)의 초록/검정 배경을 지웠다.
        // 먼 층일수록 수면이 높이 보이도록(하늘 수평선 ≈ 원경 수면 바닥 위 약 1.6칸, 근경 수면 약 0.75칸) 맞췄다.
        // includeGround: 붕괴 맵은 가운데가 끊긴 바닥 띠를 직접 깔기 때문에 바닥 층을 뺀다.
        private static ParallaxBackground.Layer[] SeaParallaxLayers(bool includeGround = true)
        {
            var layers = new System.Collections.Generic.List<ParallaxBackground.Layer>
            {
                // 하늘과 바다(1536x1024, 1.4배 → 15.9x10.6칸): 수평선이 그림 밑에서 289px. 아래는 물속이 비치는 얕은 바다라 늘려 깐다.
                new ParallaxBackground.Layer
                {
                    Path = SeaParallaxFolder + "sea_sky", Scale = 1.4f, ScrollX = 0.1f, ScrollY = 0.05f, BottomAboveFloor = -1.4f,
                    SortingOrder = -15, Mirror = true, ExtendTop = true, ExtendBottom = true,
                },
                // 물 위 유적(2172x465, 0.9배): 수면이 그림 밑에서 91px. 밑은 바다라 늘려 깐다.
                new ParallaxBackground.Layer
                {
                    Path = SeaParallaxFolder + "sea_far", Scale = 0.9f, ScrollX = 0.3f, ScrollY = 0.3f, BottomAboveFloor = 1.0f,
                    SortingOrder = -14, Mirror = true, ExtendBottom = true,
                },
                new ParallaxBackground.Layer
                {
                    Path = SeaParallaxFolder + "sea_fog", Scale = 0.8f, ScrollX = 0.45f, ScrollY = 0.45f, BottomAboveFloor = -0.6f,
                    SortingOrder = -13, Tint = new Color(1f, 1f, 1f, 0.3f), Drift = 0.2f,
                },
                // 물 속 아치와 기둥(2172x497, 0.85배 → 높이 약 3.1칸): 수면이 그림 밑에서 99px. 캐릭터가 돋보이게 살짝 누른다.
                new ParallaxBackground.Layer
                {
                    Path = SeaParallaxFolder + "sea_near", Scale = 0.85f, ScrollX = 0.6f, ScrollY = 0.6f, BottomAboveFloor = 0.15f,
                    SortingOrder = -12, Mirror = true, ExtendBottom = true, Tint = new Color(0.86f, 0.9f, 0.92f),
                },
                new ParallaxBackground.Layer
                {
                    Path = SeaParallaxFolder + "sea_fog", Scale = 0.7f, ScrollX = 0.8f, ScrollY = 0.8f, BottomAboveFloor = -1.6f,
                    SortingOrder = -10, Tint = new Color(0.9f, 1f, 1f, 0.22f), Drift = -0.15f,
                },
            };
            if (includeGround)
            {
                layers.Add(new ParallaxBackground.Layer
                {
                    Path = SeaGroundPath, ScrollX = 1f, ScrollY = 1f, BottomAboveFloor = -(SeaGroundHeightPx - SeaGroundWalkPx) / 135f,
                    SortingOrder = -9, Mirror = true, ExtendBottom = true,
                });
            }
            return layers.ToArray();
        }

        private const string HeavenParallaxFolder = "Backgrounds/Parallax/Heaven/";

        // 천상의 신전: 마법진이 뜬 구름 위 하늘 → 떠 있는 섬과 신전으로 이어지는 하얀 계단 → 흐르는 구름 → 꽃 덮인
        // 대리석 회랑 → 낮게 깔린 구름 → 보랏빛 깃발 대리석 다리 바닥. 원화(Sprite/Map Sprite/08_Heaven/Parallax)의 초록/검정 배경을 지웠다.
        private static ParallaxBackground.Layer[] HeavenTempleParallaxLayers() => new[]
        {
            // 하늘(2172x724, 1.1배 → 17.7x5.9칸): 마법진이 바닥 위 약 3칸, 맨 위는 거의 단색 파랑이라 위로 늘린다.
            new ParallaxBackground.Layer
            {
                Path = HeavenParallaxFolder + "heaven_sky", Scale = 1.1f, ScrollX = 0.1f, ScrollY = 0.05f, BottomAboveFloor = -0.8f,
                SortingOrder = -15, Mirror = true, ExtendTop = true, ExtendBottom = true,
            },
            // 떠 있는 섬과 하얀 계단 끝 신전(1711x885, 0.9배 → 높이 약 5.9칸). 밑은 구름이 물결 모양으로 끊겨 회랑·바닥 뒤에 숨긴다.
            new ParallaxBackground.Layer
            {
                Path = HeavenParallaxFolder + "heaven_far", Scale = 0.9f, ScrollX = 0.3f, ScrollY = 0.25f, BottomAboveFloor = -1.3f,
                SortingOrder = -14, Mirror = true, Tint = new Color(0.95f, 0.95f, 1f),
            },
            new ParallaxBackground.Layer
            {
                Path = HeavenParallaxFolder + "heaven_fog", ScrollX = 0.4f, ScrollY = 0.35f, BottomAboveFloor = 0.2f,
                SortingOrder = -13, Tint = new Color(1f, 1f, 1f, 0.45f), Drift = 0.15f,
            },
            // 꽃 덮인 대리석 회랑(2172x717, 0.85배 → 높이 약 4.5칸). 밑은 구름으로 꽉 차 있어 아래로 늘린다.
            new ParallaxBackground.Layer
            {
                Path = HeavenParallaxFolder + "heaven_near", Scale = 0.85f, ScrollX = 0.6f, ScrollY = 0.45f, BottomAboveFloor = -0.8f,
                SortingOrder = -12, Mirror = true, ExtendBottom = true, Tint = new Color(0.86f, 0.85f, 0.92f), // 발판·캐릭터가 돋보이게 누른다
            },
            new ParallaxBackground.Layer
            {
                Path = HeavenParallaxFolder + "heaven_fog", ScrollX = 0.8f, ScrollY = 0.8f, BottomAboveFloor = -2.2f,
                SortingOrder = -10, Tint = new Color(0.95f, 0.92f, 1f, 0.35f), Drift = -0.1f,
            },
            // 대리석 다리 바닥 띠(2172x340): 대리석 윗면이 위에서 0~60px라 앞쪽(45px)을 밟는 선으로 삼는다. 그 아래 깃발 걸린
            // 고딕 아치 기초벽, 밑은 구름이라 아래로 늘린다.
            new ParallaxBackground.Layer
            {
                Path = HeavenParallaxFolder + "heaven_ground", ScrollX = 1f, ScrollY = 1f, BottomAboveFloor = -(340f - 45f) / 135f,
                SortingOrder = -9, Mirror = true, ExtendBottom = true,
            },
        };

        // 천사 석상 분수: 연습장(x -13~-6)과 노아 상점(x -2.4~3.6) 사이 빈터. 높이 약 4.3칸(캐릭터의 약 3배).
        // 건물(-5)보다 뒤(-7, 물 -6), 돌바닥(-9)·고딕 집 거리(-12)보다 앞. 밑동을 돌바닥에 살짝 묻는다.
        private const float VillageFountainX = -4.2f;
        private const float VillageFountainHeight = 4.3f;

        private static void SpawnVillageFountain(Transform parent, int floorY)
        {
            FountainProp.Spawn(parent, new Vector3(VillageFountainX, floorY - 0.12f, 0f), VillageFountainHeight, -7);
            // 소원 동전: 분수 앞에서 [Z]로 1G를 던진다(1% 확률로 10G가 돌아온다).
            FountainWish.Spawn(parent, new Vector3(VillageFountainX, floorY - 0.12f, 0f), player);
            SpawnVillageDecor(parent, floorY);
        }

        // 가로등(높이 3.6칸)과 깃발 기둥(4칸). 건물 사이 빈자리에 둔다 — 연습장 x -13~-6, 분수 -5.7~-2.7, 노아 상점 -2.4~3.6,
        // 엘리 대장간 5.3~12.3, 마을 출구 포탈 그림이 x 약 14.6에 선다.
        private static readonly float[] VillageLampXs = { -14.2f, 4.45f };
        private static readonly float[] VillageBannerXs = { -15.4f, 12.9f };

        private static void SpawnVillageDecor(Transform parent, int floorY)
        {
            foreach (float x in VillageLampXs) StreetLamp.Spawn(parent, new Vector3(x, floorY - 0.1f, 0f), 3.6f, -6);
            foreach (float x in VillageBannerXs) HangingBanner.Spawn(parent, new Vector3(x, floorY - 0.1f, 0f), 4f, -6);
        }

        // 배경 소품: 맵의 정해진 자리에 세우는 그림(세상에 붙어 있어 땅과 같이 움직인다). 가까운 숲(-12) 앞, 땅 안개(-10)·흙길(-9) 뒤.
        // sink만큼 밑동을 땅에 묻어 흙길 띠의 풀이 뿌리를 덮게 한다.
        private static void SpawnBackdropProp(Transform parent, string path, float x, int floorY, float scale, Color tint, float sink = 0.3f, int sortingOrder = -11)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null) return;
            var go = new GameObject("Backdrop_" + System.IO.Path.GetFileName(path), typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(x, floorY - sink, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.color = tint;
        }

        // 수정이 박힌 거대한 고목(887x1706 → 6.6x12.6칸). 밝은 갈색이라 밤 숲에 맞게 눌러 준다.
        private static readonly Color ForestBigTreeTint = new Color(0.58f, 0.52f, 0.7f);

        // 어둠의 숲 맵마다 배경 소품 배치.
        //   1 숲길: 구름다리와 내리막 사이 뒤편에 고목 하나(랜드마크).
        //   2 거목 오르기: 가운데 바위섬 탑 바로 뒤에 크게 — 거목을 타고 오르는 느낌.
        //   보스 맵: 왼쪽 뒤편에 고목 하나.
        private static void SpawnForestBackdrop(Transform parent, int floorY, int stageIndex)
        {
            const string bigTree = ForestParallaxFolder + "Props/forest_prop_bigtree";
            switch (stageIndex)
            {
                case 1: SpawnBackdropProp(parent, bigTree, 1.0f, floorY, 0.62f, ForestBigTreeTint); break;
                case 2: SpawnBackdropProp(parent, bigTree, 1.0f, floorY, 0.95f, ForestBigTreeTint); break;
                default: SpawnBackdropProp(parent, bigTree, -6f, floorY, 0.6f, ForestBigTreeTint); break;
            }
        }

        // BuildMap(배경 없이) 다음, FitCamera 전에 부른다 — 카메라가 올라갈 수 있는 높이를 시차 배경 기준으로 잡아 준다.
        private static void SpawnParallax(ParallaxBackground.Layer[] layers, int xMin, int xMax, int floorY)
        {
            var go = new GameObject("ParallaxBackground");
            go.transform.SetParent(stageRoot.transform);
            go.AddComponent<ParallaxBackground>().Initialize(layers, floorY, floorY + CameraRestAboveFloor, (xMin + xMax + 1) / 2f);
            backgroundTopAboveFloor = ParallaxBackgroundTop;
        }
    }
}

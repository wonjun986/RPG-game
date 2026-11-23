using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Data;
using Aethoria.Monsters;
using Aethoria.Quests;
using Aethoria.Save;
using Aethoria.Stages;
using Aethoria.UI;

namespace Aethoria.Bootstrap
{
    // 사냥터 맵: 광활한 평야, 잊혀진 폐허, 푸른 바다(추격전/아쿠아 2차전 포함).
    public static partial class GameBootstrap
    {
        // 마을 중간쯤에 노아의 상점과 노아를 놓는다. 상점은 배경보다 앞, 캐릭터보다는 뒤에 그려진다.
        // 광활한 평야: 넓은 들판(40유닛)에 슬라임이 흩어져 있고, 기둥/아치/떠 있는 발판을 밟고 올라갈 수 있다.
        // 배경(YardMap) 속 흙길이 그림 위에서 약 82% 높이라 그 줄을 바닥으로 삼는다.
        private const float PlainsGroundFraction = 0.82f;

        // plainsIndex: 1~3. 3번째 맵 끝에서만 필드 보스(슬라임)가 나온다(어둠의 숲이 BossStage에서만
        // 보스를 추가하는 것과 같은 방식). 세 맵 모두 같은 지형을 재사용하고, 슬라임은 1~2번째 맵에만
        // 들어올 때마다 무작위 위치(MonsterSpawner)에 나온다.
        private static void BuildPlains(int plainsIndex, bool enteringForward)
        {
            // 배경은 한 장짜리 그림(Backgrounds/plains) 대신 시차 배경(하늘·폭포 절벽·폐허 정원·들꽃 흙길)을 깐다.
            BuildMap(stageRoot.transform, PlainsWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                null, PlainsGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;
            SpawnParallax(PlainsParallaxLayers(), xMin, xMax, yMin);

            player.SetCombatLocked(false);
            float playerSpawnX = enteringForward ? xMin + 1.5f : xMax - 1.5f;
            PlacePlayerAtSpawn(playerSpawnX, yMin);
            FitCamera(xMin, xMax, yMin);

            SpawnPlainsPlatforms(stageRoot.transform, yMin);
            SpawnPlainsSecrets(stageRoot.transform, yMin, plainsIndex);

            bool isFirstPlains = plainsIndex <= 1;
            bool isLastPlains = plainsIndex >= TotalPlainsStages;

            // 보스 맵(마지막 평야)에는 보스 슬라임만 나오고 일반 슬라임은 나오지 않는다.
            if (isLastPlains)
            {
                SpawnBossSlime(stageRoot.transform, yMin);
            }
            else
            {
                SpawnSlimes(stageRoot.transform, yMin, MonsterSpawner.PickSpawnXs(SlimesPerStage, xMin, xMax, playerSpawnX));
            }

            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true, framed: PlainsPortalStyle);
            if (isFirstPlains) backPortal.SetCustomTrigger(() => ConfirmReturnToVillage(backPortal));
            else if (isLastPlains) backPortal.SetCustomTrigger(() => GoToRegionRoom(TreasureChests.Plains, "arena", "right"));
            else backPortal.SetCustomTrigger(EnterPreviousFieldStage);

            if (!isLastPlains)
            {
                var forwardPortal = SpawnPortal(stageRoot.transform, xMax - 0.5f, yMin, backward: false, framed: PlainsPortalStyle);
                forwardPortal.SetCustomTrigger(EnterNextFieldStage);
            }

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(stageRoot.transform);
            stageManagerGO.AddComponent<StageManager>().Initialize(player);

            Debug.Log($"Aethoria: 광활한 평야 {plainsIndex}/{TotalPlainsStages} 진입");
        }

        // 평야 지형: 이끼 낀 고대 유적 타일셋(Sprite/Map Sprite/04_Plains/Platforms)에서 잘라낸 조각들을 왼쪽(출발)에서 오른쪽으로 이어지는 코스로 배치한다.
        // 한 번 점프가 약 1.6, 2단 점프가 약 3.2유닛이라, 인접한 발판끼리는 높이 차가 이 안에 들도록 했다.
        // 겹쳐 선 발판끼리는 위 발판 아랫면과 아래 발판 윗면 사이에 캐릭터 키(1.5) 이상 공간을 남긴다.
        // surfaceFromTopPx: 그림 맨 위에서 밟는 면(풀밭/돌길 윗줄)까지의 픽셀. -1이면 밟을 수 없는 장식.
        private static void SpawnPlainsPlatforms(Transform parent, int floorY)
        {
            // 1구역 (출발): 작은 기둥 두 개와 낮은 단, 작은 떠 있는 발판으로 몸풀기
            SpawnGroundProp(parent, "pillar_5", -18.6f, floorY, 14);
            SpawnGroundProp(parent, "pillar_6", -16.0f, floorY, 22);
            SpawnGroundProp(parent, "ground_3", -12.4f, floorY, 18);
            SpawnPlatform(parent, "platform_10", -14.2f, floorY + 2.5f, 31);
            SpawnPlatform(parent, "platform_8", -10.4f, floorY + 2.6f, 47);

            // 2구역: 높은 공중 돌길(ground_2), 그 아래 낮은 발판과 이중 아치(장식)
            SpawnPlatform(parent, "ground_2", -4.3f, floorY + 4.3f, 58);
            SpawnPlatform(parent, "platform_3", -7.5f, floorY + 1.4f, 33);
            SpawnGroundProp(parent, "arch_1", -3.2f, floorY, -1);

            // 3구역: 큰 기둥 → 덩굴 발판 → 윗면이 평평한 아치
            SpawnPlatform(parent, "platform_9", 1.8f, floorY + 2.9f, 35);
            SpawnGroundProp(parent, "pillar_1", 3.6f, floorY, 16);
            SpawnPlatform(parent, "platform_12", 6.2f, floorY + 4.0f, 29);
            SpawnGroundProp(parent, "arch_2", 9.0f, floorY, 36);

            // 4구역: 가장 긴 공중 다리(platform_7), 아래에는 부서진 기둥들
            SpawnGroundProp(parent, "pillar_2", 11.2f, floorY, 17);
            SpawnPlatform(parent, "platform_7", 15.4f, floorY + 4.3f, 48); // 발판을 3/4로 줄여 기둥 꼭대기가 낮아져서 4.6 → 4.3(2단 점프 안)
            SpawnGroundProp(parent, "pillar_3", 13.5f, floorY, -1);
            SpawnGroundProp(parent, "pillar_4", 17.8f, floorY, 17);
        }

        // 잊혀진 폐허: 1번째 맵은 폐허 입구(Forgotten Ruins_Entrance), 2~3번째 맵은 평평한 돌길이 이어지는
        // 폐허 마당(Forgotten Ruins_yard). 마당 그림은 돌길 윗면이 약 81% 높이라 그 줄을 바닥으로 삼는다.
        // 입구 그림은 왼쪽 높은 돌단 → 계단 → 가운데 돌다리 → 계단 → 오른쪽 아래 돌길로 내려가는 구조라,
        // 가장 낮은 오른쪽 돌길(그림의 약 83.4% 높이)을 바닥으로 삼고 그 위로 길을 따라 경사 지형을 깐다.
        // 콜로서스가 들어올 때마다 무작위 위치에 나오고, 보스는 아직 없다.
        private const float RuinsEntranceGroundFraction = 785f / 941f;
        private const float RuinsYardGroundFraction = 0.81f;
        // 맵 폭(40)에 맞춰 늘리면 배율이 약 2.39라 계단/성문이 캐릭터에 비해 거대해 보였다. 1.4로 고정하고 모자란 폭은 좌우 반전한
        // 복사본으로 채운다(천상의 계단과 같은 방식). 1유닛 = 그림 71.4px. 이 배율에서도 바닥 위 약 10.7유닛, 아래 약 2.2유닛을
        // 덮어서 카메라(입구에서 3유닛 올라가도)에 빈 곳이 생기지 않는다.
        private const float RuinsBackgroundScale = 1.4f;

        // 입구 지형: (월드 x, 바닥 위 높이). 배경(1672x941)을 배율 1.4로 맵 가운데(x 0)에 깔아 1유닛 = 71.4px이므로
        // x = (그림 x - 836) / 71.4, 높이 = (785 - 그림 y) / 71.4. 그림 왼쪽 끝(x -11.7) 너머는 좌우 반전 복사본이라
        // 높은 돌단이 이어지다가 반전된 계단으로 다시 내려간다. 계단은 걸어서 오르내릴 수 있게 완만한 경사로 이었다.
        // 맵 왼쪽 끝(포탈 자리)은 반전 복사본 속 계단이 내려가는 곳이라, 높은 돌단을 끝까지 평평하게 잇고
        // 그 아래를 돌담 띠(RuinsGroundStrip)로 덮어 포탈이 계단 위에 떠 보이지 않게 한다.
        //
        // 시차 배경으로 바꾼 뒤로는 입구의 높은 돌단·계단을 사암 계단 그림(RuinsStairsProp, 2172x603)으로 세운다.
        // 계단 그림을 1유닛 = 195px로 x -7.12(그림 왼쪽 끝)에 두면, 그림 속 밟는 선(돌단 윗면 y 80 → 첫 계단 →
        // 가운데 참 y 378 → 둘째 계단 → 바닥 y 585)이 아래 지형 점과 겹친다. 그 왼쪽(맵 왼쪽 끝까지)의 높은 돌단은
        // 사암 바닥 그림을 같은 높이로 올려 세운 돌단 벽(SpawnRuinsPlateau)이다.
        private static readonly Vector2[] RuinsEntranceTerrain =
        {
            // 충돌선은 점이 있는 곳까지만 생기므로 맵 왼쪽 벽(-20) 너머까지 점을 둬야 한다 — 없으면 포탈 자리에서 바닥까지 떨어진다.
            new Vector2(-21f, 2.59f),
            new Vector2(-5.68f, 2.59f),  // 높은 돌단 끝(계단 그림 x 280)
            new Vector2(-3.07f, 1.06f),  // 첫 계단을 내려와 가운데 참(그림 x 790, y 378)
            new Vector2(-1.02f, 1.06f),  // 참 끝(그림 x 1190)
            new Vector2(2.11f, 0f),      // 둘째 계단을 내려와 바닥(그림 x 1800, y 585)
        };

        private const string RuinsStairsProp = "Backgrounds/Parallax/Ruins/Props/ruins_stairs";
        private const float RuinsStairsPixelsPerUnit = 195f;
        private const float RuinsStairsLeftX = -7.12f;
        private const float RuinsStairsBottomPx = 18f; // 그림 밑 18px(모래)은 바닥 아래로 묻힌다

        // 입구 사암 계단: 캐릭터보다 뒤, 바닥 띠(-9)보다 앞. 그림 피벗(아래 가운데)과 시차 배경 1칸 = 135px 기준을 195px로 다시 잰다.
        private static void SpawnRuinsStairs(Transform parent, int floorY)
        {
            var sprite = Resources.Load<Sprite>(RuinsStairsProp);
            if (sprite == null) return;
            float scale = sprite.pixelsPerUnit / RuinsStairsPixelsPerUnit;
            float width = sprite.rect.width / RuinsStairsPixelsPerUnit;
            var go = new GameObject("RuinsStairs", typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(RuinsStairsLeftX + width / 2f, floorY - RuinsStairsBottomPx / RuinsStairsPixelsPerUnit, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -7;
        }

        // 계단 왼쪽의 높은 돌단 벽: 사암 바닥 그림(윗면이 위에서 30px)을 윗면이 돌단 높이에 오게 올려 세운다. 계단 그림 뒤(-8).
        private static void SpawnRuinsPlateau(Transform parent, int floorY, float x0, float x1, float topHeight)
        {
            var sprite = Resources.Load<Sprite>(RuinsParallaxFolder + "ruins_ground");
            if (sprite == null) return;
            float tileWidth = sprite.bounds.size.x;
            float surfaceFromTop = 30f / sprite.pixelsPerUnit;
            float bottomY = floorY + topHeight + surfaceFromTop - sprite.bounds.size.y;
            int i = 0;
            for (float x = x1; x > x0; x -= tileWidth, i++)
            {
                var go = new GameObject("RuinsPlateau", typeof(SpriteRenderer));
                go.transform.SetParent(parent);
                go.transform.position = new Vector3(x - tileWidth / 2f, bottomY, 0f);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.flipX = i % 2 == 1; // 바닥 띠처럼 하나 건너 뒤집어 이음매를 맞춘다
                sr.sortingOrder = -8;
            }
        }

        // 마당의 흘러내리는 모래: 부서진 돌 받침(8프레임, 돌은 고정·모래 줄기만 움직임). 가장 길게 쏟아질 때 줄기 끝이 그림 맨 아래에
        // 닿으므로, 그림 밑을 바닥에 맞춰 두면 모래가 땅까지 이어진다(받침 밑면은 바닥 위 약 1.1유닛). 캐릭터보다 뒤(-6).
        private const string RuinsSandFallFolder = "Backgrounds/Parallax/Ruins/Props/SandFall";
        private const float RuinsSandFallHeight = 2.6f;   // 프레임(264x401) 높이(유닛) — 받침 폭 약 1.7
        private static readonly float[][] RuinsSandFallXs =
        {
            new[] { -13.4f, 7.7f },   // 마당 1
            new[] { -13.0f, 1.4f },   // 마당 2(보스 맵): 모래룡 자리(x 6~19)는 피한다
        };

        private static void SpawnRuinsSandFalls(Transform parent, int floorY, int ruinsIndex)
        {
            int set = ruinsIndex - 2;
            if (set < 0 || set >= RuinsSandFallXs.Length) return;
            float scale = RuinsSandFallHeight / (401f / 135f);
            foreach (float x in RuinsSandFallXs[set])
            {
                // 피벗이 그림 아래 가운데 — 바닥 띠 윗면에 살짝 묻는다.
                var fx = FlipbookSpriteEffect.Create("RuinsSandFall", RuinsSandFallFolder,
                    new Vector2(x, floorY - 0.05f), 0.1f, loop: true, sortingOrder: -6);
                if (fx == null) continue;
                fx.transform.SetParent(parent, true);
                fx.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        // 폐허 마당 앞쪽 돌담(풀 덮인 윗면) 띠(Backgrounds/ruins_ground_strip, 1672x201 — 윗변 22px은 풀이 삐져나와 점점 투명).
        // 배경과 같은 배율로, 돌담 윗면이 topY에 오게 x0~x1 구간에 깐다. 이어 붙는 안쪽 끝은 투명하게 풀린 쪽을 쓴다
        // (fadeOnLeft: 띠가 x0에서 오른쪽으로 뻗고 왼쪽 끝이 풀림 / false면 x1에서 왼쪽으로 뻗고 오른쪽 끝이 풀림).
        private const float RuinsGroundStripLipPx = 22f;

        private static void SpawnRuinsGroundStrip(Transform parent, float x0, float x1, float topY, bool fadeOnLeft)
        {
            var texture = Resources.Load<Texture2D>("Backgrounds/ruins_ground_strip");
            if (texture == null || x1 <= x0) return;
            float pxPerUnit = 100f / RuinsBackgroundScale;
            int widthPx = Mathf.Min(texture.width, Mathf.CeilToInt((x1 - x0) * pxPerUnit));
            int startPx = fadeOnLeft ? 0 : texture.width - widthPx;
            var sprite = Sprite.Create(texture, new Rect(startPx, 0, widthPx, texture.height),
                new Vector2(fadeOnLeft ? 0f : 1f, 1f), pxPerUnit);

            var go = new GameObject("RuinsGroundStrip", typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(fadeOnLeft ? x0 : x1, topY + RuinsGroundStripLipPx / pxPerUnit, 0f);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -8; // 배경(-10)보다 앞, 발판/포탈(-5)보다 뒤
        }

        // 배경 그림 속 돌단처럼 그림은 이미 있고 밟는 면만 필요한 곳에 까는 보이지 않는 한쪽 방향 발판(윗면 = topY).
        private static void SpawnInvisibleLedge(Transform parent, float x0, float x1, float topY)
        {
            const float thickness = 0.2f;
            var go = new GameObject("Ledge", typeof(BoxCollider2D));
            go.transform.SetParent(parent);
            go.transform.position = new Vector3((x0 + x1) / 2f, topY - thickness / 2f, 0f);

            var collider = go.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(x1 - x0, thickness);
            collider.usedByEffector = true;

            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
        }

        // 입구 맵에서는 높은 돌단 위에서도 화면이 잘리지 않게 카메라가 이만큼까지 따라 올라간다.
        private const float RuinsEntranceCameraRise = 3f;

        // x 위치의 지형 높이(바닥 기준). 지형이 없거나 지형 점 범위 밖(오른쪽 평지)이면 0.
        private static float TerrainHeightAt(Vector2[] terrain, float x)
        {
            if (terrain == null || terrain.Length == 0) return 0f;
            if (x <= terrain[0].x) return terrain[0].y;
            for (int i = 1; i < terrain.Length; i++)
            {
                if (x <= terrain[i].x)
                {
                    float t = Mathf.InverseLerp(terrain[i - 1].x, terrain[i].x, x);
                    return Mathf.Lerp(terrain[i - 1].y, terrain[i].y, t);
                }
            }
            return 0f;
        }

        // 지형 윗면을 따라 이어진 선 콜라이더. 맨 왼쪽은 왼쪽 벽에, 맨 오른쪽은 바닥에 닿아 있어 아래로 빠질 틈이 없다.
        private static void BuildTerrain(Transform parent, int floorY, Vector2[] terrain)
        {
            var go = new GameObject("Terrain", typeof(EdgeCollider2D));
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(0f, floorY, 0f);

            var edge = go.GetComponent<EdgeCollider2D>();
            edge.points = terrain;
            edge.edgeRadius = 0.05f;
        }

        private static void BuildRuins(int ruinsIndex, bool enteringForward)
        {
            bool isFirstRuins = ruinsIndex <= 1;
            bool isLastRuins = ruinsIndex >= TotalRuinsStages;

            // 배경은 한 장짜리 그림(ruins_entrance/ruins_yard) 대신 시차 배경(노을 하늘·모래 언덕 성당·사암 열주·성소 바닥)을 깐다.
            BuildMap(stageRoot.transform, RuinsWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                null, RuinsYardGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;
            SpawnParallax(RuinsParallaxLayers(), xMin, xMax, yMin);

            var terrain = isFirstRuins ? RuinsEntranceTerrain : null;
            if (terrain != null)
            {
                BuildTerrain(stageRoot.transform, yMin, terrain);
                SpawnRuinsStairs(stageRoot.transform, yMin);
                SpawnRuinsPlateau(stageRoot.transform, yMin, xMin - 3f, RuinsStairsLeftX + 0.6f, 2.59f);
            }
            else
            {
                SpawnRuinsSandFalls(stageRoot.transform, yMin, ruinsIndex);
            }

            player.SetCombatLocked(false);
            float playerSpawnX = enteringForward ? xMin + 1.5f : xMax - 1.5f;
            PlacePlayerAtSpawn(playerSpawnX, yMin + TerrainHeightAt(terrain, playerSpawnX));
            FitCamera(xMin, xMax, yMin, terrain != null ? RuinsEntranceCameraRise : 0f);

            SpawnRuinsPlatforms(stageRoot.transform, yMin, ruinsIndex);
            SpawnRuinsSecrets(stageRoot.transform, yMin, ruinsIndex);
            // 보스 맵(마지막 폐허)에는 황금 모래룡만 나오고 콜로서스는 나오지 않는다(평야의 보스 슬라임과 같은 방식).
            SandDragonIntroCutscene sandDragonIntro = null;
            if (isLastRuins)
            {
                sandDragonIntro = SpawnSandDragon(stageRoot.transform, yMin, xMax);
            }
            else
            {
                SpawnColossi(stageRoot.transform, x => yMin + TerrainHeightAt(terrain, x),
                    MonsterSpawner.PickSpawnXs(ColossiPerStage, xMin, xMax, playerSpawnX, minSpacing: 3.5f));
            }

            float backPortalX = xMin + 0.5f;
            var backPortal = SpawnPortal(stageRoot.transform, backPortalX, yMin + TerrainHeightAt(terrain, backPortalX), backward: true, framed: RuinsPortalStyle);
            if (isFirstRuins) backPortal.SetCustomTrigger(() => ConfirmReturnToVillage(backPortal));
            else if (isLastRuins) backPortal.SetCustomTrigger(() => GoToRegionRoom(TreasureChests.Ruins, "arena", "right"));
            else backPortal.SetCustomTrigger(EnterPreviousFieldStage);
            // 보스 맵의 출구는 모래룡이 등장 연출에서 꼬리로 부순다.
            if (sandDragonIntro != null) sandDragonIntro.SetExitPortal(backPortal);

            if (!isLastRuins)
            {
                var forwardPortal = SpawnPortal(stageRoot.transform, xMax - 0.5f, yMin, backward: false, framed: RuinsPortalStyle);
                forwardPortal.SetCustomTrigger(EnterNextFieldStage);
            }

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(stageRoot.transform);
            stageManagerGO.AddComponent<StageManager>().Initialize(player);

            Debug.Log($"Aethoria: 잊혀진 폐허 {ruinsIndex}/{TotalRuinsStages} 진입");
        }

        // 폐허 발판: Forgotten Ruins_Platform에서 잘라낸 떠 있는 돌섬들. 맵마다 다른 코스로 배치한다.
        // 평야와 같은 규칙 — 인접 발판 높이 차는 2단 점프(약 3.2) 안, 바닥에서 바로 오르는 첫 발판은 3 이하.
        // surfaceFromTopPx는 그림 맨 위에서 돌판 윗면(이끼 낀 밟는 면)까지의 픽셀.
        private static void SpawnRuinsPlatforms(Transform parent, int floorY, int ruinsIndex)
        {
            switch (ruinsIndex)
            {
                case 1:
                    // 입구: 길은 높은 돌단에서 계단 두 번을 거쳐 내려간다(RuinsEntranceTerrain). 발판은 그 길 위로
                    // 돌단 위 돌섬 → 계단 위 돌섬들 → 긴 공중 다리로 이어지는 윗길을 만들고, 오른쪽 평지로 내려온다.
                    // (배경 배율을 1.4로 줄이며 지형이 낮아진 만큼, 지형 위 높이는 그대로 두고 앞 세 개를 내렸다.)
                    SpawnPlatform(parent, "ruin_rock_3", -15.5f, floorY + 4.56f, 28, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_6", -6.8f, floorY + 5.29f, 25, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_5", -2.5f, floorY + 3.6f, 29, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_bridge_long", 5.6f, floorY + 5.6f, 110, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_7", 13.2f, floorY + 3.0f, 27, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_spire", 16.3f, floorY + 1.8f, 133, RuinsPropFolder);
                    // 더 촘촘하게: 돌단 위 디딤돌과 맨 윗길, 돌다리 아래 징검돌, 오른쪽 평지 위 섬.
                    SpawnPlatform(parent, "ruin_slab_4", -11.5f, floorY + 4.9f, 15, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_4", -9.0f, floorY + 6.9f, 36, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_8", -4.3f, floorY + 7.3f, 59, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_1", 0.6f, floorY + 2.6f, 20, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_6", 2.4f, floorY + 7.6f, 25, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_2", 9.6f, floorY + 4.2f, 58, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_mound_3", 11.0f, floorY + 1.8f, 34, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_5", 15.0f, floorY + 5.2f, 29, RuinsPropFolder);
                    break;
                case 2:
                    // 마당 1: 작은 돌섬 계단으로 높은 다리까지 올라갔다가, 낮은 기둥 섬을 거쳐 다시 오르내린다.
                    SpawnPlatform(parent, "ruin_rock_4", -15.5f, floorY + 2.4f, 36, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_slab_4", -12.2f, floorY + 3.8f, 15, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_bridge", -6.6f, floorY + 5.0f, 40, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_1", -1.2f, floorY + 3.6f, 20, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_pillar_2", 3.4f, floorY + 1.4f, 149, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_slab_3", 8.3f, floorY + 3.0f, 6, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_8", 12.5f, floorY + 4.4f, 59, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_mound_3", 15.6f, floorY + 2.6f, 34, RuinsPropFolder);
                    // 더 촘촘하게: 낮은 징검돌과, 다리/돌섬 위로 이어지는 윗길.
                    SpawnPlatform(parent, "ruin_rock_3", -9.4f, floorY + 1.9f, 28, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_6", -10.0f, floorY + 6.8f, 25, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_7", -3.7f, floorY + 6.9f, 27, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_5", 1.4f, floorY + 5.9f, 29, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_3", 5.9f, floorY + 4.6f, 28, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_7", 10.5f, floorY + 6.3f, 27, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_5", 14.4f, floorY + 6.6f, 29, RuinsPropFolder);
                    break;
                default:
                    // 마당 2(모래룡 보스 맵): 탑이 솟은 성문 섬과 밧줄 다리 섬이 번갈아 나오는 높낮이 큰 코스.
                    // 커다란 모래룡과 땅에서 싸우도록 공중 발판은 줄였다(윗길 디딤돌과 모래룡 자리의 기둥 섬은 뺐다).
                    // 대신 보스 구역(x 6~19)에는 모래룡(몸통 높이 약 4.4)을 넘어 반대편으로 건너갈 수 있게, 양쪽 디딤돌과
                    // 모래룡 머리 위를 가로지르는 긴 공중 다리(높이 5.7, 폭 약 4)를 깐다. 인접 발판 높이 차는 2단 점프(약 3.2) 안.
                    SpawnPlatform(parent, "ruin_pillar_1", -15.2f, floorY + 1.3f, 152, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_mound_1", -10.6f, floorY + 2.8f, 69, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_2", -6.4f, floorY + 4.3f, 58, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_gate_high", -1.6f, floorY + 2.0f, 222, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rope", 4.2f, floorY + 3.4f, 95, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_mound_2", 8.6f, floorY + 4.8f, 32, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_slab_1", 12.6f, floorY + 3.2f, 7, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_1", 6.2f, floorY + 2.4f, 20, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_bridge_long", 13.6f, floorY + 5.7f, 110, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_2", 17.6f, floorY + 3.8f, 58, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_slab_4", 19.0f, floorY + 1.9f, 15, RuinsPropFolder);
                    break;
            }
        }

        // 푸른 바다: 바다 위 고대 석조 둑길. 세 맵 모두 같은 시차 배경(SeaParallaxLayers)이고 발판 배치만 다르다.
        // 마지막 맵은 바닥 띠를 직접 깔고 가운데 구간을 그 띠를 잘라 만든 조각으로 덮어 둔다 — 그 조각이 무너진다(SeaCollapseCutscene).
        // 구멍 왼쪽에서만 싸우도록 몬스터는 이 x까지만 나온다.
        private const int BrokenSeaSpawnMaxX = -4;
        private const string BlueSeaPropFolder = "Art/Props/Sea/";
        private const int SeaSpiritsPerStage = 3;

        private static void BuildBlueSea(int seaIndex, bool enteringForward)
        {
            bool isFirstSea = seaIndex <= 1;
            bool isLastSea = seaIndex >= TotalBlueSeaStages;

            BuildMap(stageRoot.transform, BlueSeaWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                null, 0.85f);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;
            SpawnParallax(SeaParallaxLayers(includeGround: !isLastSea), xMin, xMax, yMin);

            player.SetCombatLocked(false);
            float playerSpawnX = enteringForward ? xMin + 1.5f : xMax - 1.5f;
            PlacePlayerAtSpawn(playerSpawnX, yMin);
            FitCamera(xMin, xMax, yMin);

            List<Transform> collapsePieces = null;
            if (isLastSea) collapsePieces = SpawnBrokenSeaGround(stageRoot.transform, xMin, xMax, yMin);
            SpawnBlueSeaPlatforms(stageRoot.transform, yMin, seaIndex);
            SpawnSeaSecrets(stageRoot.transform, yMin, seaIndex);
            SpawnSeaSpirits(stageRoot.transform, yMin,
                MonsterSpawner.PickSpawnXs(SeaSpiritsPerStage, xMin, isLastSea ? BrokenSeaSpawnMaxX : xMax, playerSpawnX, minSpacing: 4f));

            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true, framed: SeaPortalStyle);
            if (isFirstSea) backPortal.SetCustomTrigger(() => ConfirmReturnToVillage(backPortal));
            else if (isLastSea) backPortal.SetCustomTrigger(() => GoToRegionRoom(TreasureChests.Sea, "arena", "right"));
            else backPortal.SetCustomTrigger(EnterPreviousFieldStage);

            if (!isLastSea)
            {
                var forwardPortal = SpawnPortal(stageRoot.transform, xMax - 0.5f, yMin, backward: false, framed: SeaPortalStyle);
                forwardPortal.SetCustomTrigger(EnterNextFieldStage);
            }
            else
            {
                var floor = stageRoot.transform.Find("MapBounds/Wall_Bottom").GetComponent<Collider2D>();
                var collapseGO = new GameObject("SeaCollapse");
                collapseGO.transform.SetParent(stageRoot.transform);
                collapseGO.AddComponent<SeaCollapseCutscene>().Initialize(player, floor, collapsePieces, BrokenSeaPieceColumns,
                    (BrokenSeaGapLeft + BrokenSeaGapRight) / 2f, BrokenSeaTriggerX, BrokenSeaBlockerX, yMin,
                    (BrokenSeaWaterRowPx - SeaGroundWalkPx) / 135f, EnterSeaChase);
            }

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(stageRoot.transform);
            stageManagerGO.AddComponent<StageManager>().Initialize(player);

            Debug.Log($"Aethoria: 푸른 바다 {seaIndex}/{TotalBlueSeaStages} 진입");
        }

        private static void EnterSeaChase()
        {
            currentStage = SeaChaseStage;
            BuildStage(enteringForward: true);
        }

        private static void BuildSeaChase()
        {
            mapXMin = -1; mapXMax = 0; mapYMin = SeaChaseFloorY; mapYMax = SeaChaseFloorY;
            backgroundTopAboveFloor = 0f;

            player.SetCombatLocked(true);
            PlacePlayerAtSpawn(2f, SeaChaseFloorY + 2f);
            FitCamera(mapXMin, mapXMax, SeaChaseFloorY);

            var chaseGO = new GameObject("SeaChase");
            chaseGO.transform.SetParent(stageRoot.transform);
            chaseGO.AddComponent<SeaChaseController>().Initialize(player, npcDialogueUI, seaChaseIntroSeen,
                () => seaChaseIntroSeen = true, EnterSeaArena);

            Debug.Log("Aethoria: 푸른 바다 추격전 진입");
        }

        private static void EnterSeaArena()
        {
            currentStage = SeaArenaStage;
            BuildStage(enteringForward: true);
        }

        // 아쿠아 2차전 투기장: 무너진 해저 신전 유적(1672x941) — 석상과 삼지창 깃발이 선 신전 앞 돌바닥 광장.
        // 광장 윗면이 그림 위에서 712~795px다. 785px로 두니 캐릭터가 돌바닥에 파묻혀 보여서 조금 더 안쪽(765px)을 바닥으로 삼는다.
        // 양옆에 낮은 돌판 발판을 두어 바닥을 훑는 드릴을 피할 수 있게 한다.
        private const float SeaArenaGroundFraction = 765f / 941f;

        private static void BuildSeaArena()
        {
            BuildMap(stageRoot.transform, SeaArenaWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/sea_arena", SeaArenaGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(false);
            PlacePlayerAtSpawn(xMin + 2.5f, yMin);
            FitCamera(xMin, xMax, yMin);
            // 높이 뛰어도 카메라가 따라 올라가지 않는다. 좌우로는 넓어진 투기장을 따라간다.
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.Follow(player.transform, xMin, xMax, yMin + 4f * 0.6f, yMin, 0f);

            SpawnPlatform(stageRoot.transform, "sea_slab_wide", xMin + 2.6f, yMin + 2.3f, 58, BlueSeaPropFolder);
            SpawnPlatform(stageRoot.transform, "sea_slab_wide", xMax - 1.6f, yMin + 2.3f, 58, BlueSeaPropFolder);
            // 넓어진 가운데에도 발판을 하나 더 둬서, 드릴을 위로 피하거나 아쿠아를 넘어갈 디딤돌로 쓴다.
            SpawnPlatform(stageRoot.transform, "sea_slab_wide", (xMin + xMax + 1) / 2f, yMin + 2.6f, 58, BlueSeaPropFolder);
            SpawnAqua(stageRoot.transform, xMin, xMax, yMin);

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(stageRoot.transform);
            stageManagerGO.AddComponent<StageManager>().Initialize(player);

            Debug.Log("Aethoria: 아쿠아 2차전 진입");
        }

        private static void SpawnAqua(Transform parent, int xMin, int xMax, int floorY)
        {
            var data = AquaData();
            var go = new GameObject("Boss_Aqua");
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(xMax + 4f, floorY, 0f);
            go.transform.localScale = new Vector3(AquaAI.VisualScale, AquaAI.VisualScale, 1f);
            go.AddComponent<SpriteRenderer>();
            // 떠 있는 몸이라 플레이어와 밀치지 않게 트리거로 두고, 접촉 피해는 AquaAI가 직접 잰다.
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = AquaAI.BodyColliderSize;
            collider.offset = AquaAI.BodyColliderOffset;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            var monster = go.AddComponent<Monster>();
            monster.AssignData(data);
            go.AddComponent<AquaAI>().Initialize(player, npcDialogueUI, xMin, xMax + 1f, floorY, aquaArenaIntroSeen,
                introShown: () => aquaArenaIntroSeen = true,
                reveal: () => bossHealthUI.Bind(monster, data.monsterName),
                finished: () => ReturnToVillage(spawnAtNoa: true));
        }

        // 바다 발판: 해저 유적 플랫폼과 산호 에셋 시트에서 잘라낸 떠 있는 돌섬/유적 다리. 폐허와 같은 규칙 —
        // 인접 발판 높이 차는 2단 점프(약 3.2) 안, 바닥에서 바로 오르는 첫 발판은 3 이하. 아치/열주는 바닥에 세운 낮은 단.
        // surfaceFromTopPx는 그림 맨 위에서 밟는 면(산호 아래 돌판 윗면)까지의 픽셀.
        private static void SpawnBlueSeaPlatforms(Transform parent, int floorY, int seaIndex)
        {
            switch (seaIndex)
            {
                case 1:
                    // 바다 1: 작은 돌섬에서 시작해 긴 돌판 → 기둥 섬 → 가장 높은 수로교를 넘고, 오른쪽으로 갈수록 낮아진다.
                    SpawnPlatform(parent, "sea_rock_small", -15.5f, floorY + 2.4f, 38, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_slab_long", -11.0f, floorY + 4.4f, 69, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_column_1", -6.8f, floorY + 3.2f, 70, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_aqueduct", -1.8f, floorY + 5.2f, 85, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_rock_tiny", 3.4f, floorY + 3.8f, 46, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_slab_wide", 7.8f, floorY + 2.6f, 58, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_column_2", 12.0f, floorY + 4.2f, 40, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_bridge_pillar", 16.0f, floorY + 2.8f, 93, BlueSeaPropFolder);
                    break;
                case 2:
                    // 바다 2: 가운데 바닥에 무너진 열주가 서 있고, 그 양옆으로 높은 돌판 두 개가 솟은 산 모양 코스.
                    SpawnPlatform(parent, "sea_slab_wide", -16.0f, floorY + 2.2f, 58, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_rock_spike", -12.2f, floorY + 3.8f, 78, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_slab_double", -7.6f, floorY + 5.2f, 77, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_rock_small", -3.2f, floorY + 3.6f, 38, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_colonnade", 0.8f, floorY + 0.9f, 32, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_slab_long", 4.8f, floorY + 3.2f, 69, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_column_1", 9.0f, floorY + 4.8f, 70, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_aqueduct", 13.4f, floorY + 3.4f, 85, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_rock_tiny", 17.2f, floorY + 2.0f, 46, BlueSeaPropFolder);
                    break;
                case 3:
                    // 바다 3(붕괴 맵): 몬스터와 싸우는 구멍 왼쪽(x -4까지)에만 낮은 코스. 구멍 너머 오른쪽은 막혀 있어
                    // 건너가지 못하지만, 둑길이 이어져 보이도록 발판 두 개를 세워 둔다.
                    SpawnPlatform(parent, "sea_rock_small", -16.0f, floorY + 2.4f, 38, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_slab_long", -11.5f, floorY + 4.2f, 69, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_column_1", -7.0f, floorY + 3.0f, 70, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_bridge_pillar", 8.0f, floorY + 2.8f, 93, BlueSeaPropFolder);
                    SpawnPlatform(parent, "sea_rock_tiny", 13.0f, floorY + 3.6f, 46, BlueSeaPropFolder);
                    break;
            }
        }

        // 붕괴 맵 바닥: 바닥 띠(sea_ground, 2172x234 · 1칸 = 135px)를 맵 왼쪽 끝부터 하나 건너 좌우 반전해 이어 깔되, 가운데 구멍 구간
        // [BrokenSeaGapLeft, BrokenSeaGapRight]은 비워 두고 그 자리를 같은 그림을 잘라 만든 조각(가로 3칸 × 윗단/아랫단)으로 덮는다.
        // 조각들은 SeaCollapseCutscene이 넘겨받아 떨어뜨린다. 반환값은 윗단 조각 먼저, 그다음 아랫단.
        private const float BrokenSeaGapLeft = -2.4f;
        private const float BrokenSeaGapRight = 1.8f;
        private const float BrokenSeaTriggerX = -0.9f;   // 구멍 위로 이만큼 들어서면 무너진다
        private const float BrokenSeaBlockerX = 0.9f;    // 구멍을 넘어가지 못하게 막는 자리
        private const int BrokenSeaPieceColumns = 3;
        private const float BrokenSeaUpperRowPx = 70f;   // 윗단 조각 = 돌길 윗면과 그 아래 벽 윗부분
        // 바닥 띠의 물거품 줄(위에서 186px, 바닥 아래 약 1.16칸). 그 아래 물결 띠는 무너지지 않고 남아 구멍 속 수면이 되고,
        // 붕괴 연출은 이 높이에서 물보라를 터뜨린다.
        private const float BrokenSeaWaterRowPx = 186f;
        // 무너진 뒤 구멍 안쪽에 보이는 뒷벽(같은 띠를 어둡게 눌러 조각 뒤에 깐다). 없으면 뒤쪽 근경 층의 바다가 바닥보다 높이 비쳐
        // 물에 빠지기 전부터 물속처럼 보였다.
        private static readonly Color BrokenSeaInnerWallTint = new Color(0.28f, 0.36f, 0.42f);

        private static List<Transform> SpawnBrokenSeaGround(Transform parent, int xMin, int xMax, int floorY)
        {
            var pieces = new List<Transform>();
            var sprite = Resources.Load<Sprite>(SeaGroundPath);
            if (sprite == null) return pieces;

            const float ppu = 135f;
            var texture = sprite.texture;
            var rect = sprite.rect;
            float tileWidth = rect.width / ppu;
            float bottomY = floorY - (SeaGroundHeightPx - SeaGroundWalkPx) / ppu;
            float mapRight = xMax + 1;

            // 한 타일 안의 월드 구간 [x0, x1]을 텍스처의 세로 구간 [rowFromTop0, rowFromTop1](위에서 px)만큼 잘라 낸 스프라이트를 만든다.
            GameObject Slice(string name, float tileLeft, bool flip, float x0, float x1, float rowTop0, float rowTop1, Vector2 pivot,
                int sortingOrder = -9)
            {
                float p0 = (x0 - tileLeft) * ppu, p1 = (x1 - tileLeft) * ppu;
                float texX0 = flip ? rect.width - p1 : p0;
                float width = p1 - p0;
                var sub = new Rect(rect.x + texX0, rect.y + (rect.height - rowTop1), width, rowTop1 - rowTop0);
                var go = new GameObject(name, typeof(SpriteRenderer));
                go.transform.SetParent(parent);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = Sprite.Create(texture, sub, pivot, ppu, 0, SpriteMeshType.FullRect);
                sr.flipX = flip;
                sr.sortingOrder = sortingOrder;
                float worldBottom = bottomY + (rect.height - rowTop1) / ppu;
                go.transform.position = new Vector3(x0 + width / ppu * pivot.x, worldBottom + (rowTop1 - rowTop0) / ppu * pivot.y, 0f);
                return go;
            }

            int index = 0;
            for (float left = xMin; left < mapRight; left += tileWidth, index++)
            {
                bool flip = index % 2 == 1;
                float right = left + tileWidth;
                // 구멍 왼쪽·오른쪽 부분만 바닥으로 깐다.
                if (left < BrokenSeaGapLeft) Slice("SeaGround", left, flip, left, Mathf.Min(right, BrokenSeaGapLeft), 0f, rect.height, new Vector2(0.5f, 0f));
                if (right > BrokenSeaGapRight) Slice("SeaGround", left, flip, Mathf.Max(left, BrokenSeaGapRight), right, 0f, rect.height, new Vector2(0.5f, 0f));
            }

            // 구멍 자리: 맨 아래 물결 띠는 그대로 남기고(수면), 그 위는 어둡게 누른 뒷벽을 조각 뒤에 깐다.
            {
                int tile = Mathf.FloorToInt((BrokenSeaGapLeft - xMin) / tileWidth);
                float tileLeft = xMin + tile * tileWidth;
                bool flip = tile % 2 == 1;
                Slice("SeaGapWater", tileLeft, flip, BrokenSeaGapLeft, BrokenSeaGapRight, BrokenSeaWaterRowPx, rect.height, new Vector2(0.5f, 0f));
                var wall = Slice("SeaGapInnerWall", tileLeft, flip, BrokenSeaGapLeft, BrokenSeaGapRight, SeaGroundWalkPx, BrokenSeaWaterRowPx,
                    new Vector2(0.5f, 0f), sortingOrder: -10);
                wall.GetComponent<SpriteRenderer>().color = BrokenSeaInnerWallTint;
            }

            // 구멍을 덮는 조각: 그 자리 타일에서 잘라 낸다(무너질 때 돌도록 가운데 기준). 물결 띠 위까지만.
            float columnWidth = (BrokenSeaGapRight - BrokenSeaGapLeft) / BrokenSeaPieceColumns;
            for (int row = 0; row < 2; row++)
            {
                float top0 = row == 0 ? 0f : BrokenSeaUpperRowPx;
                float top1 = row == 0 ? BrokenSeaUpperRowPx : BrokenSeaWaterRowPx;
                for (int c = 0; c < BrokenSeaPieceColumns; c++)
                {
                    float x0 = BrokenSeaGapLeft + c * columnWidth, x1 = x0 + columnWidth;
                    int tile = Mathf.FloorToInt((x0 - xMin) / tileWidth);
                    float tileLeft = xMin + tile * tileWidth;
                    var piece = Slice("CollapsePiece", tileLeft, tile % 2 == 1, x0, x1, top0, top1, new Vector2(0.5f, 0.5f));
                    pieces.Add(piece.transform);
                }
            }
            return pieces;
        }
    }
}

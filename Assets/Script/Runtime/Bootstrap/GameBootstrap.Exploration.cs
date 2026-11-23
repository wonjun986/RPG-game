using UnityEngine;
using Aethoria.Data;
using Aethoria.Stages;

namespace Aethoria.Bootstrap
{
    // 탐험 요소: 사냥터 곳곳의 보물상자(TreasureChests), 가까이 가야 보이는 숨겨진 포탈, 그 너머 비밀의 장소.
    //  - 보물상자는 지역마다 일반 맵 두 곳의 높은 발판 위에 하나씩 숨어 있다.
    //  - 숨겨진 포탈은 지역마다 한 곳. 들어가면 몬스터 없는 작은 비밀의 장소(큰 보물상자 + 체력/마나 회복)가 나오고,
    //    돌아오는 포탈을 타면 숨겨진 포탈이 있던 그 자리로 돌아온다(맵은 새로 지어져 몬스터도 다시 나온다).
    public static partial class GameBootstrap
    {
        private const int SecretStage = -19;
        private const int SecretRoomWidth = 20;

        // 비밀의 장소에 들어간 지역/돌아갈 맵/돌아갈 자리.
        private static string secretRegion;
        private static int secretOriginStage;
        private static Vector2 secretReturnPoint;
        // 다음 PlacePlayerAtSpawn 한 번만 이 자리에 세운다(비밀의 장소에서 돌아올 때).
        private static Vector2? pendingSpawnPoint;

        // surfaceY: 상자를 올려놓을 발판 윗면의 월드 높이.
        private static void SpawnTreasureChest(Transform parent, string id, float x, float surfaceY)
        {
            TreasureChest.Spawn(parent, id, new Vector3(x, surfaceY, 0f), player);
            NoteMinimapChest(id); // 던전 지도에 이 방의 상자로 표시
        }

        // 숨겨진 포탈. 일반 포탈 그림을 작게 줄이고 보랏빛으로 물들여, 가까이 가야만 드러나게 한다.
        private static void SpawnHiddenPortal(Transform parent, string region, float x, float surfaceY)
        {
            var portal = SpawnPortal(parent, x, surfaceY, backward: true);
            // 맵 끝 포탈처럼 안쪽으로 밀어 그릴 필요가 없다 — 발판 한가운데에 선다.
            var visual = portal.transform.Find("PortalVisual");
            if (visual != null) visual.localPosition = new Vector3(0f, visual.localPosition.y, 0f);
            const float hiddenScale = 0.55f;
            portal.transform.localScale = new Vector3(hiddenScale, hiddenScale, 1f);
            // 그림 밑동이 발판 윗면에 닿게 줄어든 만큼 내린다.
            portal.transform.position = new Vector3(x, surfaceY + PortalHeightAboveFloor * hiddenScale, 0f);
            portal.name = "HiddenPortal_" + region;

            int origin = currentStage;
            var returnPoint = new Vector2(x, surfaceY);
            portal.SetCustomTrigger(() => EnterSecretRoom(region, origin, returnPoint));
            portal.gameObject.AddComponent<HiddenPortalReveal>().Initialize(player.transform, new Color(0.95f, 0.8f, 1f));
        }

        private static void EnterSecretRoom(string region, int originStage, Vector2 returnPoint)
        {
            secretRegion = region;
            secretOriginStage = originStage;
            secretReturnPoint = returnPoint;
            currentStage = SecretStage;
            BuildStage(enteringForward: true);
        }

        private static void LeaveSecretRoom()
        {
            currentStage = secretOriginStage;
            pendingSpawnPoint = secretReturnPoint;
            BuildStage(enteringForward: false);
        }

        // 비밀의 장소: 그 지역 배경을 어둡게 눌러 깐 한 화면짜리 방. 가운데 큰 보물상자가 빛나고, 들어오면 체력/마나가 다 찬다.
        private static void BuildSecretRoom()
        {
            string background;
            float groundFraction;
            float backgroundScale = 0f;
            switch (secretRegion)
            {
                case TreasureChests.Plains:
                    background = null; groundFraction = PlainsGroundFraction; break; // 시차 배경
                case TreasureChests.Ruins:
                    background = null; groundFraction = RuinsYardGroundFraction; break; // 시차 배경
                case TreasureChests.Sea:
                    background = null; groundFraction = 0.85f; break; // 시차 배경
                default:
                    background = null; groundFraction = BackgroundGroundFraction; break; // 숲은 시차 배경
            }

            BuildMap(stageRoot.transform, SecretRoomWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                background, groundFraction, backgroundScale);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;
            if (background == null)
                SpawnParallax(secretRegion == TreasureChests.Plains ? PlainsParallaxLayers()
                    : secretRegion == TreasureChests.Ruins ? RuinsParallaxLayers()
                    : secretRegion == TreasureChests.Sea ? SeaParallaxLayers() : ForestParallaxLayers(), xMin, xMax, yMin);
            SpawnSecretRoomShade(stageRoot.transform, (xMin + xMax + 1) / 2f, yMin);

            player.SetCombatLocked(true);
            PlacePlayerAtSpawn(xMin + 1.5f, yMin);
            FitCamera(xMin, xMax, yMin);

            player.ClearMaxHpPenalty();
            player.Heal(player.MaxHp);
            player.RestoreMana(player.MaxMana);

            float chestX = (xMin + xMax + 1) / 2f + 1f;
            SpawnTreasureChest(stageRoot.transform, secretRegion + "_secret", chestX, yMin);
            SceneLighting.AddPointLight(stageRoot.transform, new Vector3(chestX, yMin + 1f, 0f), new Color(1f, 0.85f, 0.55f), 5f, 0.9f);

            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true);
            backPortal.SetCustomTrigger(LeaveSecretRoom);

            SpawnSecretRoomTitle(new Vector3(chestX, yMin + 4.2f, 0f));
            Debug.Log($"Aethoria: 비밀의 장소({TreasureChests.RegionName(secretRegion)}) 진입");
        }

        private static Sprite shadeSprite;

        // 배경 위에 반투명 어둠을 한 장 덮어 "숨은 곳" 분위기를 낸다(배경 -10과 발판 -5 사이).
        private static void SpawnSecretRoomShade(Transform parent, float centerX, int floorY)
        {
            if (shadeSprite == null)
                shadeSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            var go = new GameObject("SecretShade", typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(centerX, floorY + 6f, 0f);
            go.transform.localScale = new Vector3(60f, 40f, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = shadeSprite;
            sr.color = new Color(0.04f, 0.02f, 0.08f, 0.45f);
            sr.sortingOrder = -9;
        }

        private static void SpawnSecretRoomTitle(Vector3 position)
        {
            var go = new GameObject("SecretRoomTitle");
            go.transform.SetParent(stageRoot.transform);
            go.transform.position = position;
            var text = go.AddComponent<TextMesh>();
            text.text = "— 비밀의 장소 —\n<size=30>" + TreasureChests.RegionName(secretRegion) + "</size>";
            text.richText = true;
            text.characterSize = 0.08f;
            text.fontSize = 48;
            text.alignment = TextAlignment.Center;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(0.9f, 0.8f, 1f);
            go.GetComponent<MeshRenderer>().sortingOrder = 60;
            go.AddComponent<FloatingTextFade>();
        }

        // ---- 지역별 보물상자/숨겨진 포탈 자리 (모두 맵 발판 위, 2단 점프로 닿는 곳) ----

        // 어둠의 숲 1·2(SpawnForestPlatforms의 맨 위 발판).
        private static void SpawnForestSecrets(Transform parent, int floorY, int stageIndex)
        {
            if (stageIndex == 1)
            {
                SpawnTreasureChest(parent, "forest_1", 15.4f, floorY + 8.0f);
            }
            else if (stageIndex == 2)
            {
                SpawnTreasureChest(parent, "forest_2", 6.8f, floorY + 11.0f);
                SpawnHiddenPortal(parent, TreasureChests.Forest, -3.7f, floorY + 10.0f);
            }
        }

        // 광활한 평야 1·2(세 맵이 같은 지형이라 맵마다 다른 발판을 쓴다).
        private static void SpawnPlainsSecrets(Transform parent, int floorY, int plainsIndex)
        {
            if (plainsIndex == 1)
            {
                SpawnTreasureChest(parent, "plains_1", 6.2f, floorY + 4.0f);
            }
            else if (plainsIndex == 2)
            {
                SpawnTreasureChest(parent, "plains_2", -4.3f, floorY + 4.3f);
                SpawnHiddenPortal(parent, TreasureChests.Plains, 16.6f, floorY + 4.3f);
            }
        }

        // 잊혀진 폐허 1·2.
        private static void SpawnRuinsSecrets(Transform parent, int floorY, int ruinsIndex)
        {
            if (ruinsIndex == 1)
            {
                SpawnTreasureChest(parent, "ruins_1", 2.4f, floorY + 7.6f);
                SpawnHiddenPortal(parent, TreasureChests.Ruins, -4.3f, floorY + 7.3f);
            }
            else if (ruinsIndex == 2)
            {
                SpawnTreasureChest(parent, "ruins_2", 14.4f, floorY + 6.6f);
            }
        }

        // 푸른 바다 1·2(3번째는 무너지는 다리 맵이라 두지 않는다).
        private static void SpawnSeaSecrets(Transform parent, int floorY, int seaIndex)
        {
            if (seaIndex == 1)
            {
                SpawnTreasureChest(parent, "sea_1", -1.8f, floorY + 5.2f);
            }
            else if (seaIndex == 2)
            {
                SpawnTreasureChest(parent, "sea_2", -7.6f, floorY + 5.2f);
                SpawnHiddenPortal(parent, TreasureChests.Sea, 9.0f, floorY + 4.8f);
            }
        }
    }
}

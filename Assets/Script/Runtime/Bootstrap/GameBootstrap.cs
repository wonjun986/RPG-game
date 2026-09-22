using UnityEngine;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Data;
using Aethoria.Monsters;
using Aethoria.Stages;
using Aethoria.UI;

namespace Aethoria.Bootstrap
{
    // Play 모드로 들어가면 씬이 비어있어도 맵/플레이어/몬스터를 코드에서 바로 구성한다.
    // 에디터에서 미리 데이터 에셋이나 프리팹을 만들어 둘 필요가 없다.
    public static class GameBootstrap
    {
        private const int MapWidth = 20;
        private const int MapHeight = 14;
        private const float WallThickness = 0.5f;
        private const int VillageStage = 0; // 게임 시작 시 진입하는 안전 지역. 몬스터 없이 포탈로 1스테이지에 들어간다.
        // 던전 맵(20유닛)과 폭이 같으면 화면 폭에 거의 꽉 차서 카메라가 거의 못 움직인다(가장자리 근처에서만 살짝).
        // 마을은 좌우로 길게 걷는 구간이라 일부러 훨씬 넓게 잡아서 카메라가 계속 따라오는 느낌이 나게 한다.
        private const int VillageWidth = 32;
        private const int TotalStages = 3;
        private const int BossStage = 3;

        // 플레이어/카메라/HUD는 스테이지가 바뀌어도 유지되고, 맵/몬스터/포탈만 stageRoot 아래에
        // 묶어서 통째로 지웠다가 다시 짓는다.
        private static Character player;
        private static GameObject stageRoot;
        private static GameObject hudRoot;
        private static int currentStage;
        private static int mapXMin, mapXMax, mapYMin, mapYMax;
        private static StageClearUI stageClearUI;
        private static BossHealthUI bossHealthUI;
        private static NpcDialogueUI npcDialogueUI;

        // 궁극기의 칼날폭풍처럼 맵 전역에 이펙트를 뿌리는 연출이 현재 맵의 실제 크기를 알아야 할 때 쓴다.
        public static Rect CurrentMapBounds => new Rect(mapXMin, mapYMin, mapXMax - mapXMin + 1, mapYMax - mapYMin + 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            if (Object.FindFirstObjectByType<StageManager>() != null) return;

            ShowTitleScreen();
        }

        private static void ShowTitleScreen()
        {
            var titleGO = new GameObject("TitleScreen");
            titleGO.AddComponent<TitleScreenUI>().Initialize(StartNewGame);
        }

        private static void StartNewGame()
        {
            player = SpawnPlayer();
            BuildHud(player);
            currentStage = VillageStage;
            BuildStage();
        }

        // 게임오버 화면의 RETRY: 현재 판을 통째로 정리하고 스테이지 1부터 새로 시작한다.
        public static void RestartGame()
        {
            TeardownRun();
            StartNewGame();
        }

        // 게임오버 화면의 TITLE: 현재 판을 통째로 정리하고 타이틀 화면으로 돌아간다.
        public static void ReturnToTitle()
        {
            TeardownRun();
            ShowTitleScreen();
        }

        // 플레이어/맵/HUD를 전부 지우고 시간 배속도 원래대로 되돌린다.
        private static void TeardownRun()
        {
            if (stageRoot != null) Object.Destroy(stageRoot);
            if (player != null) Object.Destroy(player.gameObject);
            if (hudRoot != null) Object.Destroy(hudRoot);
            stageRoot = null;
            player = null;
            hudRoot = null;
            stageClearUI = null;
            bossHealthUI = null;
            npcDialogueUI = null;
            Time.timeScale = 1f;
        }

        // 포탈을 타고 다음 스테이지로 넘어갈 때 호출된다. 기존 맵/몬스터/포탈을 지우고 새로 짓는다.
        public static void EnterNextStage()
        {
            if (currentStage >= TotalStages) return;

            currentStage++;
            BuildStage();
        }

        // 맵 왼쪽 끝 포탈을 타고 이전 스테이지(마을 포함)로 돌아갈 때 호출된다.
        // 몬스터를 잡은 기록 등은 남지 않고, 그 스테이지를 처음 들어갈 때처럼 새로 짓는다.
        public static void EnterPreviousStage()
        {
            if (currentStage <= VillageStage) return;

            currentStage--;
            BuildStage();
        }

        private static void BuildStage()
        {
            if (stageRoot != null) Object.Destroy(stageRoot);
            stageRoot = new GameObject("StageRoot");

            if (currentStage == VillageStage)
            {
                BuildVillage();
                return;
            }

            BuildMap(stageRoot.transform, MapWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/map", BackgroundGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(false);
            PlacePlayerAtSpawn(xMin, yMin);
            FitCamera(xMin, xMax, yMin);
            SpawnMonsters(stageRoot.transform, yMin);

            if (currentStage == BossStage)
            {
                SpawnBoss(stageRoot.transform, yMin);
            }

            // 왼쪽 끝은 항상 이전 스테이지(마을 포함)로 돌아가는 포탈, 오른쪽 끝은 마지막 스테이지가
            // 아닐 때만 다음 스테이지로 가는 포탈.
            SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true);
            if (currentStage < TotalStages)
            {
                SpawnPortal(stageRoot.transform, xMax - 0.5f, yMin, backward: false);
            }

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(stageRoot.transform);
            var stageManager = stageManagerGO.AddComponent<StageManager>();
            stageManager.Initialize(player);

            if (currentStage == TotalStages)
            {
                stageManager.OnCleared += stageClearUI.Show;
            }

            Debug.Log($"Aethoria: 맵 {currentStage}/{TotalStages} 진입");
        }

        // 마을: 몬스터도 StageManager도 없는 안전 지역. 포탈까지 걸어가면 1스테이지로 들어간다.
        // 캐릭터가 떠 보인다는 피드백에 따라 바닥 기준점을 그림 더 아래쪽(가장 앞쪽 보도블록)으로 내림.
        private const float VillageGroundFraction = 0.87f;

        private static void BuildVillage()
        {
            BuildMap(stageRoot.transform, VillageWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/village", VillageGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(true);
            PlacePlayerAtSpawn(xMin, yMin);
            FitCamera(xMin, xMax, yMin);
            SpawnNoaShop(stageRoot.transform, yMin);
            SpawnPortal(stageRoot.transform, xMax - 0.5f, yMin, backward: false);

            Debug.Log("Aethoria: 마을 진입");
        }

        // 마을 중간쯤에 노아의 상점과 노아를 놓는다. 상점은 배경보다 앞, 캐릭터보다는 뒤에 그려진다.
        // 원래 자리(-3)는 배경 그림상 분수/운하 근처라 부자연스러워서, 노점상들이 있는 쪽(왼쪽)으로 옮김.
        private const float NoaShopX = -7f;
        private const float NoaShopWidth = 6f;

        private static void SpawnNoaShop(Transform parent, int floorY)
        {
            var shopSprite = Resources.Load<Sprite>("Backgrounds/NoaShop");
            if (shopSprite != null)
            {
                var shopGO = new GameObject("NoaShop", typeof(SpriteRenderer));
                shopGO.transform.SetParent(parent);
                shopGO.transform.position = new Vector3(NoaShopX, floorY, 0f);

                var shopRenderer = shopGO.GetComponent<SpriteRenderer>();
                shopRenderer.sprite = shopSprite;
                shopRenderer.sortingOrder = -5;

                float scale = NoaShopWidth / shopSprite.bounds.size.x;
                shopGO.transform.localScale = new Vector3(scale, scale, 1f);
            }

            var noaGO = new GameObject("Noa", typeof(SpriteRenderer));
            noaGO.transform.SetParent(parent);
            noaGO.transform.position = new Vector3(NoaShopX + 1.8f, floorY, 0f);
            noaGO.AddComponent<NoaNpc>().Initialize(player.transform, npcDialogueUI);
        }

        private static void PlacePlayerAtSpawn(int xMin, int floorY)
        {
            player.transform.position = new Vector3(xMin + 1.5f, floorY, 0f);
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private static void BuildMap(Transform parent, int width, int height, out int xMin, out int xMax, out int yMin, out int yMax,
            string backgroundResourcePath, float groundFraction)
        {
            xMin = -width / 2;
            xMax = xMin + width - 1;
            yMin = -height / 2;
            yMax = yMin + height - 1;

            BuildMapBackground(parent, xMin, xMax, yMin, backgroundResourcePath, groundFraction);
            BuildBoundaryWalls(parent, xMin, xMax, yMin, yMax);
        }

        // map.png 원화 속에서 실제로 밟고 서 있는 돌바닥은 이미지 맨 아래가 아니라 위에서부터
        // 약 80% 지점에 있고, 그 아래는 반사되는 물웅덩이(파란 배경)라 예전 방식(이미지를 맵 높이에
        // 맞춰 그냥 늘리기)으로는 캐릭터 발밑에 그 물웅덩이가 걸려 붕 떠 보였다.
        private const float BackgroundGroundFraction = 0.8f;

        // 카메라(orthoSize 5)가 바닥 위로 넉넉히 8~9유닛까지 올려다봐도 배경이 다 채워지도록 요구하는
        // 최소 높이. village.png처럼 가로로 아주 긴(파노라마) 배경은 맵 폭에 맞춰 늘리면 세로가
        // 모자라 화면 위쪽에 빈 공간이 생기므로, 폭 기준 배율과 이 높이 기준 배율 중 더 큰 쪽을 쓴다.
        private const float MinBackgroundHeightAboveGround = 9f;

        // 배경 원화를 맵 폭(xMin~xMax+1)에 맞춰 가로세로 비율 그대로 깔되, 세로 커버리지가 모자라면
        // 대신 세로 기준으로 확대한다. groundFraction 지점이 정확히 바닥 높이(yMin)에 오도록 맞춘다.
        private static void BuildMapBackground(Transform parent, int xMin, int xMax, int yMin,
            string backgroundResourcePath, float groundFraction)
        {
            var sprite = Resources.Load<Sprite>(backgroundResourcePath);
            if (sprite == null) return;

            float width = xMax - xMin + 1;
            float centerX = (xMin + xMax + 1) / 2f;

            var go = new GameObject("MapBackground", typeof(SpriteRenderer));
            go.transform.SetParent(parent);

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -10;

            Vector2 nativeSize = sprite.bounds.size;
            float scaleByWidth = width / nativeSize.x;
            float scaleByHeight = MinBackgroundHeightAboveGround / (nativeSize.y * groundFraction);
            float scale = Mathf.Max(scaleByWidth, scaleByHeight);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            float scaledHeight = nativeSize.y * scale;
            float centerY = yMin + scaledHeight * (groundFraction - 0.5f);
            go.transform.position = new Vector3(centerX, centerY, 0f);
        }

        private static void BuildBoundaryWalls(Transform parent, int xMin, int xMax, int yMin, int yMax)
        {
            var bounds = new GameObject("MapBounds");
            bounds.transform.SetParent(parent);

            float worldMinX = xMin;
            float worldMaxX = xMax + 1;
            float worldMinY = yMin;
            float worldMaxY = yMax + 1;
            float width = worldMaxX - worldMinX;
            float height = worldMaxY - worldMinY;
            float centerX = (worldMinX + worldMaxX) / 2f;
            float centerY = (worldMinY + worldMaxY) / 2f;

            CreateWall(bounds.transform, "Wall_Bottom", new Vector2(centerX, worldMinY - WallThickness / 2f), new Vector2(width + WallThickness * 2f, WallThickness));
            CreateWall(bounds.transform, "Wall_Top", new Vector2(centerX, worldMaxY + WallThickness / 2f), new Vector2(width + WallThickness * 2f, WallThickness));
            CreateWall(bounds.transform, "Wall_Left", new Vector2(worldMinX - WallThickness / 2f, centerY), new Vector2(WallThickness, height));
            CreateWall(bounds.transform, "Wall_Right", new Vector2(worldMaxX + WallThickness / 2f, centerY), new Vector2(WallThickness, height));
        }

        private static void CreateWall(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var wall = new GameObject(name, typeof(BoxCollider2D));
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            wall.GetComponent<BoxCollider2D>().size = size;
        }

        private static void FitCamera(int xMin, int xMax, int floorY)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null) return;

            // 캐릭터/몬스터/이펙트 등 모든 스프라이트가 각자 다른 PPU로 그려지는데, 그걸 하나하나
            // 손보는 대신 카메라를 줌인해서 화면에 보이는 모든 것을 한 번에 비례대로 키운다.
            const float orthoSize = 4f;
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = orthoSize;

            // 배경 스프라이트가 화면을 다 못 덮는 가장자리(특히 바닥 아래쪽)에서 Unity 기본 파란 배경이
            // 그대로 비쳐 보이는 것을 막기 위해, 배경 그림과 어울리는 어두운 색으로 클리어 컬러를 맞춘다.
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.03f, 0.02f, 0.05f);

            float cameraY = floorY + orthoSize * 0.6f;
            var follow = mainCamera.GetComponent<CameraFollow>();
            if (follow == null) follow = mainCamera.gameObject.AddComponent<CameraFollow>();
            follow.Follow(player.transform, xMin, xMax, cameraY);
        }

        private static void BuildHud(Character player)
        {
            var canvasGO = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hudRoot = canvasGO;
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            canvasGO.AddComponent<PlayerHUD>().Initialize(player);
            canvasGO.AddComponent<GameOverUI>().Initialize(player);

            stageClearUI = canvasGO.AddComponent<StageClearUI>();
            stageClearUI.Initialize();

            bossHealthUI = canvasGO.AddComponent<BossHealthUI>();
            bossHealthUI.Initialize();

            npcDialogueUI = canvasGO.AddComponent<NpcDialogueUI>();
            npcDialogueUI.Initialize();
        }

        private static Character SpawnPlayer()
        {
            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.characterName = "소울이터";
            data.minLevel = 1;
            data.maxLevel = 30;
            data.baseStats = new StatBlock { attack = 60, magic = 70, hp = 150, agility = 30, defense = 60, mana = 120 };
            data.growthPerLevel = new StatBlock { attack = 2, magic = 2, hp = 5, agility = 0, defense = 2, mana = 2 };

            var go = new GameObject("PlayerCharacter");
            go.AddComponent<Rigidbody2D>();

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.6f, 0.5f);
            collider.offset = new Vector2(0f, 0.25f);

            go.AddComponent<CharacterMovement2D>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.AddComponent<SpriteRenderer>();
            visual.AddComponent<CharacterPlaceholderVisual>();

            go.AddComponent<WalkBobVisual>();
            go.AddComponent<WalkSpriteAnimator>();
            go.AddComponent<JumpSpriteAnimator>();
            var character = go.AddComponent<Character>();
            character.AssignData(data);
            go.AddComponent<CharacterAttack2D>();
            go.AddComponent<CharacterSkillDash>();
            go.AddComponent<CharacterSkillX>();
            go.AddComponent<CharacterSkillQ>();
            go.AddComponent<CharacterSkillA>();
            go.AddComponent<CharacterSkillS>();
            go.AddComponent<CharacterSkillD>();
            go.AddComponent<CharacterSkillW>();
            go.AddComponent<CharacterSkillE>();
            go.AddComponent<CharacterSkillR>();

            return character;
        }

        private static void SpawnMonsters(Transform parent, int floorY)
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "기사";
            data.level = 1;
            data.maxHp = 80f; // 플레이어 기본 공격(약 60 데미지) 한 방에 죽지 않고 2대는 맞아야 죽을 정도로
            data.attack = 2f;
            data.defense = 0f;
            data.expReward = 20; // 보스 전 일반 스테이지가 줄어든 만큼(4→2) 마리당 경험치를 2배로 올려 보정

            float[] xOffsets = { 3f, -3f, 6f };
            foreach (var x in xOffsets)
            {
                var go = new GameObject("Knight");
                go.transform.position = new Vector3(x, floorY, 0f);
                go.transform.SetParent(parent);
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.85f, 0.3f, 0.3f), 24, 24);

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.6f, 1.3f);
                collider.offset = new Vector2(0f, 0.65f);

                go.AddComponent<Rigidbody2D>();
                go.AddComponent<MonsterWalkSpriteAnimator>();
                go.AddComponent<Monster>().AssignData(data);
                go.AddComponent<MonsterAI>();
                go.AddComponent<MonsterHealthBar>();
            }
        }

        private static void SpawnBoss(Transform parent, int floorY)
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "보스 기사";
            data.level = 10;
            data.maxHp = 2500f;
            data.attack = 70f; // 플레이어 방어력(60) 기준 콤보 전체가 체력의 20~27% 정도 나가는 수준
            data.defense = 20f;
            data.expReward = 600;

            var go = new GameObject("Boss_Knight");
            go.transform.position = new Vector3(5f, floorY, 0f);
            go.transform.SetParent(parent);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.5f, 0.1f, 0.5f), 48, 72, PlaceholderShape.Humanoid);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.3f, 1.8f);
            collider.offset = new Vector2(0f, 0.9f);

            go.AddComponent<Rigidbody2D>();
            go.AddComponent<MonsterWalkSpriteAnimator>().Configure("Art/Monsters/BossKnight/Walk");
            go.AddComponent<MonsterAttackSpriteAnimator>();
            var monster = go.AddComponent<Monster>();
            monster.AssignData(data);
            go.AddComponent<BossAI>();
            go.AddComponent<MonsterHealthBar>().Configure(newWidth: 1.4f, newVerticalOffset: 0.15f);

            bossHealthUI.Bind(monster, data.monsterName);
        }

        private const float PortalTriggerRadius = 2f;

        // 지정한 x 위치에 포탈을 놓는다. 화면에는 보이지 않고, 플레이어가 일정 거리 안에 들어오면
        // 자동으로 다음(또는 backward=true면 이전) 스테이지로 넘어간다.
        private static void SpawnPortal(Transform parent, float x, int floorY, bool backward)
        {
            var go = new GameObject(backward ? "Portal_Back" : "Portal_Forward");
            go.transform.position = new Vector3(x, floorY + 0.7f, 0f);
            go.transform.SetParent(parent);

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = PortalTriggerRadius;

            go.AddComponent<Portal>().Configure(backward);
        }
    }
}

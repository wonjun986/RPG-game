using UnityEngine;
using UnityEngine.Tilemaps;
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
        private const int TotalStages = 5;
        private const int BossStage = 5;

        // 플레이어/카메라/HUD는 스테이지가 바뀌어도 유지되고, 맵/몬스터/포탈만 stageRoot 아래에
        // 묶어서 통째로 지웠다가 다시 짓는다.
        private static Character player;
        private static GameObject stageRoot;
        private static int currentStage;
        private static int mapXMin, mapXMax, mapYMin, mapYMax;
        private static StageClearUI stageClearUI;

        // 궁극기의 칼날폭풍처럼 맵 전역에 이펙트를 뿌리는 연출이 현재 맵의 실제 크기를 알아야 할 때 쓴다.
        public static Rect CurrentMapBounds => new Rect(mapXMin, mapYMin, mapXMax - mapXMin + 1, mapYMax - mapYMin + 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            if (Object.FindFirstObjectByType<StageManager>() != null) return;

            player = SpawnPlayer();
            BuildHud(player);
            currentStage = 1;
            BuildStage();
        }

        // 포탈을 타고 다음 스테이지로 넘어갈 때 호출된다. 기존 맵/몬스터/포탈을 지우고 새로 짓는다.
        public static void EnterNextStage()
        {
            if (currentStage >= TotalStages) return;

            currentStage++;
            BuildStage();
        }

        private static void BuildStage()
        {
            if (stageRoot != null) Object.Destroy(stageRoot);
            stageRoot = new GameObject("StageRoot");

            BuildMap(stageRoot.transform, out int xMin, out int xMax, out int yMin, out int yMax);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            PlacePlayerAtSpawn(xMin, yMin);
            FitCamera(xMin, xMax, yMin);
            SpawnMonsters(stageRoot.transform, yMin);

            if (currentStage == BossStage)
            {
                SpawnBoss(stageRoot.transform, yMin);
            }

            if (currentStage < TotalStages)
            {
                SpawnPortal(stageRoot.transform, xMax, yMin);
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

        private static void PlacePlayerAtSpawn(int xMin, int floorY)
        {
            player.transform.position = new Vector3(xMin + 1.5f, floorY, 0f);
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private static void BuildMap(Transform parent, out int xMin, out int xMax, out int yMin, out int yMax)
        {
            var groundTile = CreateGroundTile();

            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.SetParent(parent);
            var groundGO = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer));
            groundGO.transform.SetParent(grid.transform, false);
            var tilemap = groundGO.GetComponent<Tilemap>();

            xMin = -MapWidth / 2;
            xMax = xMin + MapWidth - 1;
            yMin = -MapHeight / 2;
            yMax = yMin + MapHeight - 1;

            for (int x = xMin; x <= xMax; x++)
            {
                for (int y = yMin; y <= yMax; y++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
                }
            }

            BuildBoundaryWalls(parent, xMin, xMax, yMin, yMax);
        }

        private static Tile CreateGroundTile()
        {
            const int size = 32;
            var texture = new Texture2D(size, size) { filterMode = FilterMode.Point };
            var groundColor = new Color(0.35f, 0.55f, 0.3f);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = groundColor;
            texture.SetPixels(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = Color.white;
            return tile;
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

            const float orthoSize = 5f;
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = orthoSize;

            float cameraY = floorY + orthoSize * 0.6f;
            var follow = mainCamera.GetComponent<CameraFollow>();
            if (follow == null) follow = mainCamera.gameObject.AddComponent<CameraFollow>();
            follow.Follow(player.transform, xMin, xMax, cameraY);
        }

        private static void BuildHud(Character player)
        {
            var canvasGO = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            canvasGO.AddComponent<PlayerHUD>().Initialize(player);
            canvasGO.AddComponent<GameOverUI>().Initialize(player);

            stageClearUI = canvasGO.AddComponent<StageClearUI>();
            stageClearUI.Initialize();
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
            data.monsterName = "슬라임";
            data.level = 1;
            data.maxHp = 80f; // 플레이어 기본 공격(약 60 데미지) 한 방에 죽지 않고 2대는 맞아야 죽을 정도로
            data.attack = 2f;
            data.defense = 0f;
            data.expReward = 10;

            float[] xOffsets = { 3f, -3f, 6f };
            foreach (var x in xOffsets)
            {
                var go = new GameObject("Slime");
                go.transform.position = new Vector3(x, floorY, 0f);
                go.transform.SetParent(parent);
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.85f, 0.3f, 0.3f), 24, 24);

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.6f, 0.6f);
                collider.offset = new Vector2(0f, 0.3f);

                go.AddComponent<Rigidbody2D>();
                go.AddComponent<Monster>().AssignData(data);
                go.AddComponent<MonsterAI>();
                go.AddComponent<MonsterHealthBar>();
            }
        }

        private static void SpawnBoss(Transform parent, int floorY)
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "가디언";
            data.level = 10;
            data.maxHp = 2500f;
            data.attack = 70f; // 플레이어 방어력(60) 기준 콤보 전체가 체력의 20~27% 정도 나가는 수준
            data.defense = 20f;
            data.expReward = 500;

            var go = new GameObject("Boss_Guardian");
            go.transform.position = new Vector3(5f, floorY, 0f);
            go.transform.SetParent(parent);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.5f, 0.1f, 0.5f), 48, 72, PlaceholderShape.Humanoid);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.2f, 1.6f);
            collider.offset = new Vector2(0f, 0.8f);

            go.AddComponent<Rigidbody2D>();
            go.AddComponent<Monster>().AssignData(data);
            go.AddComponent<BossAI>();
            go.AddComponent<MonsterHealthBar>().Configure(newWidth: 1.4f, newVerticalOffset: 0.15f);
        }

        // 맵 오른쪽 끝에 포탈을 놓는다. 플레이어가 닿으면 새 스테이지로 넘어간다.
        private static void SpawnPortal(Transform parent, int xMax, int floorY)
        {
            var go = new GameObject("Portal");
            go.transform.position = new Vector3(xMax - 0.5f, floorY, 0f);
            go.transform.SetParent(parent);

            go.AddComponent<SpriteRenderer>();
            go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.3f, 0.85f, 0.9f), 28, 44, PlaceholderShape.Blob);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.9f, 1.4f);
            collider.offset = new Vector2(0f, 0.7f);

            go.AddComponent<Portal>();
        }
    }
}

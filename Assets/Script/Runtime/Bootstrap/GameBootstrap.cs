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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            if (Object.FindFirstObjectByType<StageManager>() != null) return;

            BuildMap(out int xMin, out int xMax, out int yMin, out int yMax);
            FitCamera(yMin);
            var player = SpawnPlayer(yMin);
            SpawnMonsters(yMin);
            BuildHud(player);

            new GameObject("StageManager").AddComponent<StageManager>();
        }

        private static void BuildMap(out int xMin, out int xMax, out int yMin, out int yMax)
        {
            var groundTile = CreateGroundTile();

            var grid = new GameObject("Grid", typeof(Grid));
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

            BuildBoundaryWalls(xMin, xMax, yMin, yMax);
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

        private static void BuildBoundaryWalls(int xMin, int xMax, int yMin, int yMax)
        {
            var bounds = new GameObject("MapBounds");

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

        private static void FitCamera(int floorY)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null) return;

            const float orthoSize = 5f;
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = orthoSize;
            mainCamera.transform.position = new Vector3(0f, floorY + orthoSize * 0.6f, mainCamera.transform.position.z);
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
        }

        private static Character SpawnPlayer(int floorY)
        {
            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.characterName = "소울이터";
            data.minLevel = 1;
            data.maxLevel = 50;
            data.baseStats = new StatBlock { attack = 60, magic = 70, hp = 150, agility = 30, defense = 60, mana = 120 };
            data.growthPerLevel = new StatBlock { attack = 2, magic = 2, hp = 5, agility = 0, defense = 2, mana = 2 };

            var go = new GameObject("PlayerCharacter");
            go.transform.position = new Vector3(0f, floorY, 0f);
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
            go.AddComponent<CharacterSkillQ>();
            go.AddComponent<CharacterSkillA>();

            return character;
        }

        private static void SpawnMonsters(int floorY)
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "슬라임";
            data.level = 1;
            data.maxHp = 10f;
            data.attack = 2f;
            data.defense = 0f;
            data.expReward = 10;

            float[] xOffsets = { 3f, -3f, 6f };
            foreach (var x in xOffsets)
            {
                var go = new GameObject("Slime");
                go.transform.position = new Vector3(x, floorY, 0f);
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.85f, 0.3f, 0.3f), 24, 24);

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.6f, 0.6f);
                collider.offset = new Vector2(0f, 0.3f);

                go.AddComponent<Monster>().AssignData(data);
            }
        }
    }
}

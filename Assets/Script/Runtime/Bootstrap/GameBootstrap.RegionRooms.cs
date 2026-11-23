using System.Collections.Generic;
using UnityEngine;
using Aethoria.Data;
using Aethoria.Monsters;
using Aethoria.Stages;
using Aethoria.UI;

namespace Aethoria.Bootstrap
{
    // 광활한 평야·잊혀진 폐허·푸른 바다의 방 구조(어둠의 숲 방 시스템을 넓힌 것). 예전 1·2번 맵 자리를 방 7개가 대신하고,
    // 마지막 보스 맵(3번)은 그대로 전멸방 오른쪽에 붙는다. 열쇠는 알리아스(어둠의 숲)에만 있다 — 여기서는 전멸방이 보스 길을 막는다.
    //
    // 평야                                    [종 퍼즐방] 시간 안에 종 3개
    //        [비밀방] ↑ 덩굴 바위(부숨)                ↑
    //   ▶[입구] ── [들판] ──────────────── [바람의 탑(높은 방)] ── [전멸방] ── [보스: 슬라임]
    //               ↓ 떨어지기                 ↑ 바닥 구멍
    //            [지하 동굴] ── 정예 거대 독 슬라임 · 숨겨진 포탈
    //
    // 폐허                 [하늘 다리] ── 금 간 벽(부숨) ── [비밀방]
    //                          ↑ 숨겨진 포탈
    //   ▶[입구] ── [대회랑(높은 방)] ── [전멸방] ── [보스: 황금 모래룡]
    //      ↓            ↑ 높은 턱(룬 방에서만 들어옴: 지름길)
    //   [지하 묘실] ── [룬 퍼즐방] 벽화 순서대로 룬 기둥 4개
    //    정예 고대 수호 거상
    //
    // 바다                            [밀물 퍼즐방] 시간 안에 조개 4개 · 숨겨진 포탈
    //                                      ↑
    //   ▶[입구] ── [둑길] ──────────── [전멸방] ── [보스: 붕괴 → 아쿠아]
    //               ↓ 떨어지기
    //   [비밀방] ── 산호 벽(부숨) ── [해저 동굴] 정예 심연의 정령
    //
    // 진행 상태(부순 벽, 퍼즐, 정예, 전멸방)는 WorldFlags로 저장된다. 지금 지역의 방 안에서 currentStage는 그 지역 1번 맵 번호다.
    // 벽·천장·문·스위치는 아직 임시(색 사각형)다.
    public static partial class GameBootstrap
    {
        private const string RegionBossRoom = "boss";

        private static string regionId;                 // 지금 방 구조를 쓰는 지역(TreasureChests.Plains 등)
        private static string regionRoom = RoomEntrance;
        private static string regionEntry;
        private static bool regionRoomTransition;

        private sealed class RegionRoomCtx
        {
            public string Region;
            public Transform Root;
            public int FloorY;
            public float Left, Right;
            public int Height;
            public Color Tint = Color.white;        // 어두운 방이면 장치 그림도 같이 어둡게 물들인다
        }

        private static string RegionForStage(int stage)
        {
            if (stage == PlainsStage1 || stage == PlainsStage2) return TreasureChests.Plains;
            if (stage == RuinsStage1 || stage == RuinsStage1 - 1) return TreasureChests.Ruins;
            if (stage == BlueSeaStage1 || stage == BlueSeaStage1 - 1) return TreasureChests.Sea;
            return null;
        }

        private static int RegionStage1(string region) => region == TreasureChests.Plains ? PlainsStage1
            : region == TreasureChests.Ruins ? RuinsStage1 : BlueSeaStage1;

        private static int RegionBossStage(string region) => region == TreasureChests.Plains ? PlainsStage3
            : region == TreasureChests.Ruins ? RuinsStage3 : BlueSeaStage3;

        private static void GoToRegionRoom(string region, string room, string entryExit)
            => ScreenFade.Run(() =>
            {
                regionId = region;
                if (room == RegionBossRoom)
                {
                    currentStage = RegionBossStage(region);
                    BuildStage(enteringForward: true);
                    return;
                }
                regionRoom = room;
                regionEntry = entryExit;
                regionRoomTransition = true;
                currentStage = RegionStage1(region);
                BuildStage(enteringForward: true);
            });

        // ---------------------------------------------------------------- 방 짓기

        private static void BuildRegionRoom()
        {
            string region = RegionForStage(currentStage);
            // 월드맵·관리자 이동처럼 밖에서 들어오면 입구부터. 비밀의 장소에서 돌아올 때는 있던 방 그대로.
            if ((!regionRoomTransition && !pendingSpawnPoint.HasValue) || regionId != region)
            {
                regionRoom = RoomEntrance;
                regionEntry = null;
            }
            regionRoomTransition = false;
            regionId = region;
            currentStage = RegionStage1(region);

            if (!RegionRoomSize(region, regionRoom, out int width, out int height, out bool dark))
            {
                regionRoom = RoomEntrance;
                RegionRoomSize(region, regionRoom, out width, out height, out dark);
            }
            var exits = new List<ForestExit>();
            DefineRegionExits(region, regionRoom, exits);

            BuildMap(stageRoot.transform, width, height, out int xMin, out int xMax, out int yMin, out int yMax, null, BackgroundGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;
            var layers = region == TreasureChests.Plains ? PlainsParallaxLayers()
                : region == TreasureChests.Ruins ? RuinsParallaxLayers() : SeaParallaxLayers();
            SpawnParallax(TintLayers(layers, dark ? new Color(0.5f, 0.5f, 0.64f) : Color.white), xMin, xMax, yMin);

            var root = stageRoot.transform;
            // 지하·실내 방은 배경 위에 반투명 어둠을 한 겹 더 덮는다(비밀의 장소와 같은 그늘).
            if (dark) SpawnSecretRoomShade(root, (xMin + xMax + 1) / 2f, yMin);
            var c = new RegionRoomCtx { Region = region, Root = root, FloorY = yMin, Left = xMin, Right = xMax + 1, Height = height,
                Tint = dark ? new Color(0.62f, 0.6f, 0.7f) : Color.white };
            foreach (var exit in exits)
                if (exit.Side == ExitSide.Bottom && (exit.Open == null || exit.Open())) OpenFloorGap(root, exit.Pos, yMin, forestArt: false);

            player.SetCombatLocked(false);
            Vector2 spawn = RoomSpawnPoint(exits, regionEntry, c.Left, c.Right, yMin, height);
            PlacePlayerAtSpawn(spawn.x, spawn.y);
            FitCamera(xMin - ForestCameraMargin, xMax + ForestCameraMargin, yMin);

            foreach (var exit in exits)
                SpawnRoomExit(root, exit, c.Left, c.Right, yMin, height, e => GoToRegionRoom(region, e.TargetRoom, e.TargetExit));
            BuildRegionEdges(c, exits, dark);

            if (region == TreasureChests.Plains) BuildPlainsRoom(c, regionRoom);
            else if (region == TreasureChests.Ruins) BuildRuinsRoom(c, regionRoom);
            else BuildSeaRoom(c, regionRoom);

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(root);
            stageManagerGO.AddComponent<StageManager>().Initialize(player);

            Debug.Log($"Aethoria: {TreasureChests.RegionName(region)} 방 {regionRoom} 진입 (출구 {regionEntry ?? "-"})");
        }

        private static bool RegionRoomSize(string region, string room, out int width, out int height, out bool dark)
        {
            dark = false;
            width = 40; height = 14;
            switch (region + "/" + room)
            {
                case "plains/entrance": return true;
                case "plains/field": width = 44; return true;
                case "plains/secret": width = 16; height = 10; return true;
                case "plains/hollow": dark = true; return true;
                case "plains/tower": width = 28; height = 22; return true;
                case "plains/bells": width = 26; height = 12; return true;
                case "plains/arena": width = 30; return true;

                case "ruins/entrance": return true;
                case "ruins/hall": height = 22; return true;
                case "ruins/sky": width = 36; height = 12; return true;
                case "ruins/secret": width = 16; height = 10; dark = true; return true;
                case "ruins/crypt": dark = true; return true;
                case "ruins/runes": width = 36; height = 16; dark = true; return true;
                case "ruins/arena": width = 30; return true;

                case "sea/entrance": return true;
                case "sea/cause": width = 44; return true;
                case "sea/tide": width = 26; height = 18; return true;
                case "sea/grotto": height = 16; dark = true; return true;
                case "sea/secret": width = 16; height = 10; dark = true; return true;
                case "sea/arena": width = 30; return true;
            }
            return false;
        }

        private static void DefineRegionExits(string region, string room, List<ForestExit> exits)
        {
            void Add(string id, ExitSide side, float pos, string target, string targetExit, System.Func<bool> open = null)
                => exits.Add(new ForestExit { Id = id, Side = side, Pos = pos, TargetRoom = target, TargetExit = targetExit, Open = open });

            switch (region + "/" + room)
            {
                // ---- 평야
                case "plains/entrance":
                    Add("right", ExitSide.Right, 0f, "field", "left");
                    break;
                case "plains/field":
                    Add("left", ExitSide.Left, 0f, RoomEntrance, "right");
                    Add("right", ExitSide.Right, 0f, "tower", "left");
                    Add("top", ExitSide.Top, -12f, "secret", "bottom", () => WorldFlags.Has(FlagPlainsVine));
                    Add("bottom", ExitSide.Bottom, 8f, "hollow", "top_l");
                    break;
                case "plains/secret":
                    Add("bottom", ExitSide.Bottom, 0f, "field", "top");
                    break;
                case "plains/hollow":
                    Add("top_l", ExitSide.Top, -12f, "field", "bottom");
                    Add("top_r", ExitSide.Top, 14f, "tower", "bottom");
                    break;
                case "plains/tower":
                    Add("left", ExitSide.Left, 0f, "field", "right");
                    Add("right", ExitSide.Right, 0f, "arena", "left");
                    Add("bottom", ExitSide.Bottom, -8f, "hollow", "top_r");
                    Add("top", ExitSide.Top, 8f, "bells", "bottom");
                    break;
                case "plains/bells":
                    Add("bottom", ExitSide.Bottom, -4f, "tower", "top");
                    break;
                case "plains/arena":
                    Add("left", ExitSide.Left, 0f, "tower", "right");
                    Add("right", ExitSide.Right, 0f, RegionBossRoom, null, () => WorldFlags.Has(FlagPlainsArena));
                    break;

                // ---- 폐허
                case "ruins/entrance":
                    Add("right", ExitSide.Right, 0f, "hall", "left");
                    Add("bottom", ExitSide.Bottom, 10f, "crypt", "top");
                    break;
                case "ruins/hall":
                    Add("left", ExitSide.Left, 0f, RoomEntrance, "right");
                    Add("left_high", ExitSide.Left, 10f, "runes", "right");
                    Add("right", ExitSide.Right, 0f, "arena", "left");
                    Add("top", ExitSide.Top, 12f, "sky", "bottom");
                    break;
                case "ruins/sky":
                    Add("bottom", ExitSide.Bottom, -10f, "hall", "top");
                    Add("right", ExitSide.Right, 0f, "secret", "left", () => WorldFlags.Has(FlagRuinsWall));
                    break;
                case "ruins/secret":
                    Add("left", ExitSide.Left, 0f, "sky", "right");
                    break;
                case "ruins/crypt":
                    Add("top", ExitSide.Top, -12f, RoomEntrance, "bottom");
                    Add("right", ExitSide.Right, 0f, "runes", "left");
                    break;
                case "ruins/runes":
                    Add("left", ExitSide.Left, 0f, "crypt", "right");
                    Add("right", ExitSide.Right, 0f, "hall", "left_high", () => WorldFlags.Has(FlagRuinsRunes));
                    break;
                case "ruins/arena":
                    Add("left", ExitSide.Left, 0f, "hall", "right");
                    Add("right", ExitSide.Right, 0f, RegionBossRoom, null, () => WorldFlags.Has(FlagRuinsArena));
                    break;

                // ---- 바다
                case "sea/entrance":
                    Add("right", ExitSide.Right, 0f, "cause", "left");
                    break;
                case "sea/cause":
                    Add("left", ExitSide.Left, 0f, RoomEntrance, "right");
                    Add("right", ExitSide.Right, 0f, "arena", "left");
                    Add("top", ExitSide.Top, 12f, "tide", "bottom");
                    Add("bottom", ExitSide.Bottom, -6f, "grotto", "top");
                    break;
                case "sea/tide":
                    Add("bottom", ExitSide.Bottom, -8f, "cause", "top");
                    break;
                case "sea/grotto":
                    Add("top", ExitSide.Top, 6f, "cause", "bottom");
                    Add("left", ExitSide.Left, 0f, "secret", "right", () => WorldFlags.Has(FlagSeaCoral));
                    break;
                case "sea/secret":
                    Add("right", ExitSide.Right, 0f, "grotto", "left");
                    break;
                case "sea/arena":
                    Add("left", ExitSide.Left, 0f, "cause", "right");
                    Add("right", ExitSide.Right, 0f, RegionBossRoom, null, () => WorldFlags.Has(FlagSeaArena));
                    break;
            }
        }

        // ---------------------------------------------------------------- 깃발

        private const string FlagPlainsVine = "plains_vine_broken";
        private const string FlagPlainsElite = "plains_elite_dead";
        private const string FlagPlainsBells = "plains_bells_solved";
        private const string FlagPlainsArena = "plains_arena_cleared";
        private const string FlagRuinsWall = "ruins_wall_broken";
        private const string FlagRuinsElite = "ruins_elite_dead";
        private const string FlagRuinsRunes = "ruins_runes_solved";
        private const string FlagRuinsArena = "ruins_arena_cleared";
        private const string FlagSeaCoral = "sea_coral_broken";
        private const string FlagSeaElite = "sea_elite_dead";
        private const string FlagSeaTide = "sea_tide_solved";
        private const string FlagSeaArena = "sea_arena_cleared";

        // ---------------------------------------------------------------- 발판

        // 그림 맨 위에서 밟는 면까지의 픽셀(기존 맵 배치에서 쓰던 값).
        private static readonly Dictionary<string, int> RegionSurfacePx = new()
        {
            { "platform_10", 31 }, { "platform_8", 47 }, { "ground_2", 58 }, { "platform_3", 33 }, { "platform_9", 35 },
            { "platform_12", 29 }, { "platform_7", 48 },
            { "ruin_rock_1", 20 }, { "ruin_rock_2", 58 }, { "ruin_slab_1", 7 }, { "ruin_slab_3", 6 }, { "ruin_bridge", 40 },
            { "ruin_bridge_long", 110 }, { "ruin_mound_1", 69 }, { "ruin_rope", 95 },
            { "sea_slab_long", 69 }, { "sea_aqueduct", 85 }, { "sea_slab_wide", 58 }, { "sea_slab_double", 77 },
            { "sea_bridge_pillar", 93 }, { "sea_rock_small", 38 },
        };

        private enum Plat { Small, Mid, Wide }

        // 지역마다 작은(약 2칸)·중간·넓은(4칸 이상) 발판. 바다에는 넓은 그림이 없어 수로교 두 개를 붙인다.
        private static void RP(RegionRoomCtx c, Plat kind, float x, float h)
        {
            string folder = c.Region == TreasureChests.Plains ? PlainsPropFolder : c.Region == TreasureChests.Ruins ? RuinsPropFolder : BlueSeaPropFolder;
            string name;
            if (c.Region == TreasureChests.Plains) name = kind == Plat.Small ? "platform_3" : kind == Plat.Mid ? "platform_12" : "platform_7";
            else if (c.Region == TreasureChests.Ruins) name = kind == Plat.Small ? "ruin_slab_1" : kind == Plat.Mid ? "ruin_bridge" : "ruin_bridge_long";
            else
            {
                if (kind == Plat.Wide)
                {
                    SpawnPlatform(c.Root, "sea_aqueduct", x - 1.05f, c.FloorY + h, RegionSurfacePx["sea_aqueduct"], folder);
                    SpawnPlatform(c.Root, "sea_aqueduct", x + 1.05f, c.FloorY + h, RegionSurfacePx["sea_aqueduct"], folder);
                    return;
                }
                name = kind == Plat.Small ? "sea_slab_wide" : "sea_slab_long";
            }
            SpawnPlatform(c.Root, name, x, c.FloorY + h, RegionSurfacePx[name], folder);
        }

        // 위로 오르는 사다리 발판: x0·x1을 번갈아 가며 dh씩 올라간다(작은·중간 발판 번갈아).
        private static void Ladder(RegionRoomCtx c, float x0, float x1, float h0, float dh, int count)
        {
            for (int i = 0; i < count; i++) RP(c, (i & 1) == 0 ? Plat.Mid : Plat.Small, (i & 1) == 0 ? x0 : x1, h0 + dh * i);
        }

        // ---------------------------------------------------------------- 몬스터

        private static void RegionMobs(string region, Transform parent, int floorY, IEnumerable<float> xs)
        {
            if (region == TreasureChests.Plains) SpawnSlimes(parent, floorY, xs);
            else if (region == TreasureChests.Ruins) SpawnColossi(parent, _ => floorY, xs);
            else SpawnSeaSpirits(parent, floorY, xs);
        }

        // 정예: 지역 기본 몬스터를 크고 붉게, 세 배 넘게 질기게. 잡으면 그 자리에 보물상자가 나온다(잡은 뒤에 오면 상자만 있다).
        private static void SpawnRegionElite(RegionRoomCtx c, float x, string flag, string chestId, string eliteName)
        {
            if (WorldFlags.Has(flag))
            {
                SpawnTreasureChest(c.Root, chestId, x, c.FloorY);
                return;
            }
            var holder = new GameObject("Elite").transform;
            holder.SetParent(c.Root);
            RegionMobs(c.Region, holder, c.FloorY, new[] { x });
            var monster = holder.GetComponentInChildren<Monster>();
            if (monster == null) return;
            var data = monster.Data;
            data.monsterName = eliteName;
            data.level += 3;
            data.maxHp *= 3.5f;
            data.attack *= 1.5f;
            data.defense += 2f;
            data.expReward *= 4;
            data.goldReward *= 4;
            monster.RestoreFullHp();
            monster.transform.localScale = new Vector3(monster.transform.localScale.x * 1.35f, monster.transform.localScale.y * 1.35f, 1f);
            var sr = monster.GetComponent<SpriteRenderer>();
            if (sr != null) monster.gameObject.AddComponent<EliteTint>().Initialize(sr);
            RoomArt.Dot(monster.transform, "RedAura", (Vector2)monster.transform.position + Vector2.up * 0.9f, 2.4f, new Color(1f, 0.15f, 0.15f, 0.25f), -6);

            var root = c.Root;
            int floorY = c.FloorY;
            monster.OnDied += m =>
            {
                WorldFlags.Set(flag);
                ChoicePopup.Toast($"{eliteName}을(를) 쓰러뜨렸다! 무언가 떨어졌다.", true, 2f);
                if (root != null) SpawnTreasureChest(root, chestId, m.transform.position.x, floorY);
            };
        }

        // 전멸방: 양쪽 끝 문(임시 철창)과 웨이브. 다 잡으면 가운데에 보상 상자.
        private static void SpawnRegionArena(RegionRoomCtx c, string flag, string chestId, params float[][] waves)
        {
            float centerX = (c.Left + c.Right) / 2f;
            if (WorldFlags.Has(flag))
            {
                SpawnTreasureChest(c.Root, chestId, centerX, c.FloorY);
                return;
            }
            var gates = new List<Transform>
            {
                ArenaGate(c, c.Left + 0.45f),
                ArenaGate(c, c.Right - 0.45f),
            };
            var spawners = new List<System.Action<Transform>>();
            string region = c.Region;
            int floorY = c.FloorY;
            foreach (var wave in waves)
            {
                var xs = wave;
                spawners.Add(parent => RegionMobs(region, parent, floorY, xs));
            }
            var go = new GameObject("ArenaLock");
            go.transform.SetParent(c.Root);
            var root = c.Root;
            go.AddComponent<ArenaLock>().Initialize(player, flag, c.Left + 3.5f, c.Right - 3.5f, gates, spawners,
                () => { if (root != null) SpawnTreasureChest(root, chestId, centerX, floorY); });
        }

        private static Transform ArenaGate(RegionRoomCtx c, float x)
        {
            const float h = 6f;
            var go = new GameObject("ArenaGate", typeof(BoxCollider2D));
            go.transform.SetParent(c.Root);
            go.transform.position = new Vector3(x, c.FloorY + h / 2f, 0f);
            go.GetComponent<BoxCollider2D>().size = new Vector2(0.8f, h);
            if (RoomArt.Sized(go.transform, "Gate", RoomDevicePath + "arena_gate_" + c.Region, new Vector2(x, c.FloorY), 0f, h, c.Tint, -3) != null)
                return go.transform;
            // 지역 문 그림이 아직 없으면 숲 철창문을 지역 색(평야 이끼빛·폐허 청동빛·바다 청록빛)으로 물들여 임시로 쓴다.
            Color regionTint = c.Region == TreasureChests.Ruins ? new Color(1f, 0.74f, 0.45f)
                : c.Region == TreasureChests.Sea ? new Color(0.6f, 0.9f, 0.95f) : new Color(0.85f, 0.95f, 0.72f);
            if (RoomArt.Sized(go.transform, "Gate", RoomDevicePath + "arena_gate", new Vector2(x, c.FloorY), 1.5f, h, regionTint * c.Tint, -3) != null)
                return go.transform;
            RoomArt.Rect(go.transform, "Frame", new Vector2(x, c.FloorY + h / 2f), new Vector2(0.9f, h), new Color(0.08f, 0.07f, 0.1f, 0.6f), -4);
            for (int i = 0; i < 3; i++)
                RoomArt.Rect(go.transform, "Bar" + i, new Vector2(x - 0.28f + i * 0.28f, c.FloorY + h / 2f), new Vector2(0.11f, h), new Color(0.42f, 0.4f, 0.46f), -3);
            for (int i = 0; i < 3; i++)
                RoomArt.Rect(go.transform, "Band" + i, new Vector2(x, c.FloorY + 0.8f + i * 2.2f), new Vector2(0.9f, 0.14f), new Color(0.3f, 0.28f, 0.33f), -3);
            return go.transform;
        }

        // 부서지는 벽/바위: 공격하면 무너지고 깃발이 켜져 그 너머 길이 열린다.
        private const string RoomDevicePath = "Art/Props/RoomDevices/";

        // 부서지는 벽/바위: 공격하면 무너지고 깃발이 켜져 그 너머 길이 열린다. art: RoomDevices 그림 이름(가로로 긴 바위면 폭, 세로 벽이면 높이에 맞춘다).
        private static void SpawnRegionBreakable(RegionRoomCtx c, Vector2 center, Vector2 size, Color color, string flag, string message, string art = null)
        {
            if (WorldFlags.Has(flag)) return;
            var go = new GameObject("Breakable", typeof(BoxCollider2D));
            go.transform.SetParent(c.Root);
            go.transform.position = center;
            var box = go.GetComponent<BoxCollider2D>();
            box.size = size;
            SpriteRenderer sr = null;
            if (art != null)
            {
                bool wide = size.x > size.y;
                sr = RoomArt.Sized(go.transform, "Body", RoomDevicePath + art, Vector2.zero, wide ? size.x + 0.2f : 0f, wide ? 0f : size.y + 0.2f, c.Tint, -5);
                if (sr != null) sr.transform.position = new Vector3(center.x, center.y - sr.bounds.size.y / 2f, 0f);
            }
            if (sr == null)
            {
                sr = RoomArt.Rect(go.transform, "Body", center, size, color, -5);
                float crack = Mathf.Min(size.x, size.y);
                var dark = new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f);
                RoomArt.Rect(go.transform, "Crack1", center + new Vector2(0.1f, 0.15f), new Vector2(crack * 0.14f, crack * 0.85f), dark, -4);
                RoomArt.Rect(go.transform, "Crack2", center + new Vector2(-0.15f, -0.2f), new Vector2(crack * 0.6f, crack * 0.12f), dark, -4);
            }
            var barrier = go.AddComponent<BreakableBarrier>();
            barrier.Initialize(player, flag, sr, box, center, new Vector2(size.x / 2f + 1.6f, size.y / 2f + 1.6f), message);
            barrier.Debris = DebrisSprites(c.Region == TreasureChests.Sea ? "debris_coral_" : "debris_stone_");
        }

        private static Sprite[] DebrisSprites(string prefix)
        {
            var list = new List<Sprite>();
            for (int i = 1; i <= 4; i++)
            {
                var s = Resources.Load<Sprite>(RoomDevicePath + prefix + i);
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        // 철창(보상 앞을 막는 세로 창살, 방 높이만큼). 퍼즐이 풀리면 지운다.
        private static GameObject SpawnCage(RegionRoomCtx c, float x)
        {
            var cage = new GameObject("Cage", typeof(BoxCollider2D));
            cage.transform.SetParent(c.Root);
            cage.transform.position = new Vector3(x, c.FloorY + c.Height / 2f, 0f);
            cage.GetComponent<BoxCollider2D>().size = new Vector2(0.6f, c.Height);
            if (Resources.Load<Sprite>(RoomDevicePath + "cage_bars") != null)
            {
                // 창살 그림(3칸 높이)을 바닥부터 천장까지 쌓는다.
                int n = Mathf.Max(1, Mathf.CeilToInt(c.Height / 3f));
                float h = (float)c.Height / n;
                for (int i = 0; i < n; i++)
                    RoomArt.Sized(cage.transform, "Bars" + i, RoomDevicePath + "cage_bars", new Vector2(x, c.FloorY + i * h - 0.02f), 0.7f, h + 0.04f, c.Tint, -3);
                return cage;
            }
            for (int i = 0; i < 3; i++)
                RoomArt.Rect(cage.transform, "Bar" + i, new Vector2(x - 0.3f + i * 0.3f, c.FloorY + c.Height / 2f), new Vector2(0.1f, c.Height),
                    new Color(0.32f, 0.32f, 0.38f), -3);
            return cage;
        }

        // 시간 제한 스위치 퍼즐. points: 각 스위치의 (x, 바닥 위 높이). 풀리면 철창이 사라진다.
        // offArt·onArt: RoomDevices의 꺼진/켜진 그림(있으면 색 대신 그림을 바꾼다).
        private static void SpawnTimedSwitches(RegionRoomCtx c, string label, Vector2[] points, Color on, Color off, float seconds,
            string flag, float cageX, string chestId, string solvedText, string offArt = null, string onArt = null)
        {
            var renderers = new List<SpriteRenderer>();
            Sprite offSprite = offArt != null ? Resources.Load<Sprite>(RoomDevicePath + offArt) : null;
            Sprite onSprite = onArt != null ? Resources.Load<Sprite>(RoomDevicePath + onArt) : null;
            foreach (var p in points)
            {
                var basePos = new Vector2(p.x, c.FloorY + p.y);
                if (offSprite != null && onSprite != null)
                {
                    renderers.Add(RoomArt.Sized(c.Root, label, RoomDevicePath + offArt, basePos - Vector2.up * 0.05f, 0f, 0f, Color.white, -4));
                    continue;
                }
                RoomArt.Rect(c.Root, label + "Stand", basePos + Vector2.up * 0.12f, new Vector2(0.9f, 0.24f), new Color(0.22f, 0.22f, 0.26f), -4);
                renderers.Add(RoomArt.Rect(c.Root, label, basePos + Vector2.up * 0.75f, new Vector2(0.7f, 0.95f), off, -4));
            }
            GameObject cage = WorldFlags.Has(flag) ? null : SpawnCage(c, cageX);
            SpawnTreasureChest(c.Root, chestId, cageX + 1.8f, c.FloorY);
            var go = new GameObject(label + "Puzzle");
            go.transform.SetParent(c.Root);
            var puzzle = go.AddComponent<TimedSwitchPuzzle>();
            puzzle.Initialize(player, renderers, on, off, seconds, flag,
                () => { if (cage != null) Object.Destroy(cage); }, label, solvedText);
            if (offSprite != null && onSprite != null) puzzle.SetSprites(onSprite, offSprite);
        }

        private static void ReturnToVillagePortal(RegionRoomCtx c)
        {
            var style = c.Region == TreasureChests.Plains ? PlainsPortalStyle : c.Region == TreasureChests.Ruins ? RuinsPortalStyle : SeaPortalStyle;
            var back = SpawnPortal(c.Root, c.Left + 0.5f, c.FloorY, backward: true, framed: style);
            back.SetCustomTrigger(() => ConfirmReturnToVillage(back));
        }

        // ---------------------------------------------------------------- 임시 벽·천장

        private static void BuildRegionEdges(RegionRoomCtx c, List<ForestExit> exits, bool dark)
        {
            // 지역 테두리 그림이 들어왔으면 그림으로(GameBootstrap.RegionEdges), 아직이면 아래 색 사각형으로.
            if (HasRegionEdgeArt(c.Region))
            {
                BuildRegionEdgeArt(c, exits, dark);
                return;
            }
            Color wall, face;
            if (c.Region == TreasureChests.Plains) { wall = new Color(0.27f, 0.33f, 0.22f); face = new Color(0.45f, 0.58f, 0.32f); }
            else if (c.Region == TreasureChests.Ruins) { wall = new Color(0.45f, 0.34f, 0.24f); face = new Color(0.72f, 0.57f, 0.38f); }
            else { wall = new Color(0.14f, 0.22f, 0.28f); face = new Color(0.3f, 0.52f, 0.56f); }
            if (dark)
            {
                wall = new Color(wall.r * 0.65f, wall.g * 0.65f, wall.b * 0.7f);
                face = new Color(face.r * 0.7f, face.g * 0.7f, face.b * 0.75f);
            }
            var parent = new GameObject("RoomEdges").transform;
            parent.SetParent(c.Root);
            const float thick = 3.5f, above = 10f, below = 3f, faceW = 0.14f;
            float ceil = c.FloorY + c.Height;

            void Block(float x0, float x1, float y0, float y1, Color col, int order)
            {
                if (x1 - x0 < 0.01f || y1 - y0 < 0.01f) return;
                RoomArt.Rect(parent, "Edge", new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f), new Vector2(x1 - x0, y1 - y0), col, order);
            }

            // 천장(위 출구 자리는 비운다)
            var gaps = new List<float>();
            foreach (var e in exits) if (e.Side == ExitSide.Top) gaps.Add(e.Pos);
            gaps.Sort();
            float run = c.Left - thick;
            foreach (float g in gaps)
            {
                Block(run, g - TopGapHalf, ceil, ceil + above, wall, -8);
                Block(run, g - TopGapHalf, ceil - faceW, ceil, face, -7);
                Block(g - TopGapHalf - faceW, g - TopGapHalf, ceil, ceil + above, face, -7);
                Block(g + TopGapHalf, g + TopGapHalf + faceW, ceil, ceil + above, face, -7);
                run = g + TopGapHalf;
            }
            Block(run, c.Right + thick, ceil, ceil + above, wall, -8);
            Block(run, c.Right + thick, ceil - faceW, ceil, face, -7);

            // 좌우 벽(옆 출구 자리는 비운다)
            foreach (var side in new[] { ExitSide.Left, ExitSide.Right })
            {
                bool isLeft = side == ExitSide.Left;
                float x0 = isLeft ? c.Left - thick : c.Right, x1 = isLeft ? c.Left : c.Right + thick;
                float fx0 = isLeft ? c.Left - faceW : c.Right, fx1 = fx0 + faceW;
                var holes = new List<Vector2>();
                foreach (var e in exits) if (e.Side == side) holes.Add(new Vector2(c.FloorY + e.Pos, c.FloorY + e.Pos + ArchOpening));
                holes.Sort((a, b) => a.x.CompareTo(b.x));
                float y = c.FloorY - below;
                foreach (var h in holes)
                {
                    Block(x0, x1, y, h.x, wall, -8);
                    Block(fx0, fx1, y, h.x, face, -7);
                    if (h.x > c.FloorY + 0.3f) Block(x0, x1, h.x - faceW, h.x, face, -7);  // 높은 턱 윗면
                    Block(x0, x1, h.y, h.y + faceW, face, -7);                               // 통로 위 상인방
                    // 통로 안쪽은 어둑하게
                    Block(x0, x1, h.x, h.y, new Color(0.02f, 0.02f, 0.04f, 0.55f), -9);
                    y = h.y;
                }
                Block(x0, x1, y, ceil + above, wall, -8);
                Block(fx0, fx1, y, ceil, face, -7);
            }

            // 바닥 구멍: 어두운 구덩이와 가장자리
            foreach (var e in exits)
            {
                if (e.Side != ExitSide.Bottom || (e.Open != null && !e.Open())) continue;
                Block(e.Pos - FloorGapHalf, e.Pos + FloorGapHalf, c.FloorY - 6f, c.FloorY + 0.02f, new Color(0.02f, 0.015f, 0.03f), -7);
                Block(e.Pos - FloorGapHalf - faceW, e.Pos - FloorGapHalf, c.FloorY - 3f, c.FloorY, face, -7);
                Block(e.Pos + FloorGapHalf, e.Pos + FloorGapHalf + faceW, c.FloorY - 3f, c.FloorY, face, -7);
            }
        }

        // ================================================================ 광활한 평야

        private static void BuildPlainsRoom(RegionRoomCtx c, string room)
        {
            var root = c.Root;
            int f = c.FloorY;
            switch (room)
            {
                case RoomEntrance:
                    ReturnToVillagePortal(c);
                    SpawnGroundProp(root, "pillar_5", -14.5f, f, 14);
                    SpawnGroundProp(root, "arch_2", -9.5f, f, 36);
                    RP(c, Plat.Mid, -5.5f, 3.4f);
                    RP(c, Plat.Small, -1.6f, 5.4f);
                    RP(c, Plat.Wide, 6.5f, 3.6f);
                    SpawnGroundProp(root, "pillar_1", 13f, f, 16);
                    RegionMobs(c.Region, root, f, new[] { -3f, 8f, 15f });
                    break;
                case "field":
                    // 왼쪽 위로 오르는 길 → 덩굴 감긴 바위(부숨) → 천장 구멍(비밀방)
                    Ladder(c, -8.6f, -12.4f, 2.4f, 2.4f, 4);
                    SpawnRegionBreakable(c, new Vector2(-12f, f + 12f), new Vector2(3.4f, 0.7f), new Color(0.34f, 0.42f, 0.24f),
                        FlagPlainsVine, "덩굴 감긴 바위가 부서졌다! 위로 길이 열렸다.", "plains_vine_boulder");
                    RP(c, Plat.Mid, -2.5f, 2.6f);
                    RP(c, Plat.Wide, 15f, 3.8f);
                    SpawnGroundProp(root, "pillar_2", 4f, f, 17);
                    RegionMobs(c.Region, root, f, new[] { -4f, 1.5f, 14f, 19f });
                    break;
                case "secret":
                    SpawnTreasureChest(root, "plains_1", -4.5f, f);
                    RP(c, Plat.Small, 4.5f, 2.2f);
                    break;
                case "hollow":
                    // 양쪽 끝에 위로 오르는 길(왼쪽 = 들판으로, 오른쪽 = 바람의 탑으로). 가운데 높은 단에 숨겨진 포탈.
                    Ladder(c, -8.6f, -12.4f, 2.4f, 2.4f, 4);
                    Ladder(c, 10.6f, 14.4f, 2.4f, 2.4f, 4);
                    RP(c, Plat.Small, -3.6f, 3.0f);
                    RP(c, Plat.Wide, 2f, 5.5f);
                    SpawnHiddenPortal(root, TreasureChests.Plains, 4f, f + 5.5f);
                    SpawnRegionElite(c, 0f, FlagPlainsElite, "plains_3", "거대 독 슬라임");
                    break;
                case "tower":
                    // 높은 방: 지그재그로 꼭대기까지 오르면 종 퍼즐방, 오른쪽은 전멸방, 바닥 구멍은 지하 동굴.
                    Ladder(c, -3f, 0.6f, 2.4f, 2.3f, 6);
                    RP(c, Plat.Mid, 4.4f, 16.2f);
                    RP(c, Plat.Wide, 8.4f, 18.4f);
                    RegionMobs(c.Region, root, f, new[] { 3f, 9f });
                    break;
                case "bells":
                    // 시간 안에 돌 종 세 개를 모두 쳐야 한다: 바닥 → 중간 발판 → 높은 발판.
                    RP(c, Plat.Mid, 0f, 3.0f);
                    RP(c, Plat.Small, 4f, 5.4f);
                    SpawnTimedSwitches(c, "돌 종", new[] { new Vector2(-10f, 0f), new Vector2(0f, 3.0f), new Vector2(4f, 5.4f) },
                        new Color(1f, 0.9f, 0.45f), new Color(0.45f, 0.45f, 0.4f), 6f, FlagPlainsBells, 9f, "plains_2",
                        "세 종이 함께 울린다... 철창이 열렸다!", "plains_bell_off", "plains_bell_on");
                    break;
                case "arena":
                    RP(c, Plat.Mid, -7f, 2.4f);
                    RP(c, Plat.Mid, 7f, 2.4f);
                    RP(c, Plat.Small, 0f, 4.6f);
                    SpawnRegionArena(c, FlagPlainsArena, "plains_4", new[] { -6f, 0f, 6f }, new[] { -9f, -3f, 3f, 9f });
                    break;
            }
        }

        // ================================================================ 잊혀진 폐허

        private static void BuildRuinsRoom(RegionRoomCtx c, string room)
        {
            var root = c.Root;
            int f = c.FloorY;
            switch (room)
            {
                case RoomEntrance:
                    ReturnToVillagePortal(c);
                    RP(c, Plat.Mid, -8f, 2.4f);
                    RP(c, Plat.Wide, -2.5f, 4.4f);
                    RP(c, Plat.Small, 3.6f, 2.6f);
                    RP(c, Plat.Small, 15f, 3f);
                    RegionMobs(c.Region, root, f, new[] { -5f, 3f });
                    break;
                case "hall":
                    // 높은 대회랑: 오른쪽 사다리로 꼭대기(하늘 다리)까지. 왼쪽 높은 턱은 룬 방에서 넘어오는 지름길 끝.
                    RP(c, Plat.Wide, -17.6f, 10f);
                    Ladder(c, 8.6f, 12.2f, 2.4f, 2.4f, 7);
                    RP(c, Plat.Wide, 12f, 19f);
                    RP(c, Plat.Mid, -8f, 3f);
                    RP(c, Plat.Wide, -2f, 5.2f);
                    RegionMobs(c.Region, root, f, new[] { -8f, 2f });
                    break;
                case "sky":
                    // 하늘 다리: 오른쪽 끝 금 간 벽 너머 비밀방. 높은 돌판 위에 숨겨진 포탈.
                    RP(c, Plat.Small, -6f, 2.4f);
                    RP(c, Plat.Wide, -1f, 4.6f);
                    RP(c, Plat.Small, 4.4f, 6.6f);
                    RP(c, Plat.Wide, 10f, 4.2f);
                    SpawnHiddenPortal(root, TreasureChests.Ruins, 4.4f, f + 6.6f);
                    SpawnRegionBreakable(c, new Vector2(c.Right - 0.7f, f + 2.1f), new Vector2(0.8f, 4.2f), new Color(0.62f, 0.48f, 0.32f),
                        FlagRuinsWall, "금 간 사암 벽이 무너졌다! 숨겨진 방이다.", "ruins_cracked_wall");
                    RegionMobs(c.Region, root, f, new[] { 2f, 11f });
                    break;
                case "secret":
                    SpawnTreasureChest(root, "ruins_1", 4.5f, f);
                    RP(c, Plat.Small, -3f, 2.2f);
                    break;
                case "crypt":
                    Ladder(c, -8.6f, -12.4f, 2.4f, 2.4f, 4);
                    RP(c, Plat.Mid, 9f, 2.8f);
                    SpawnRegionElite(c, 4f, FlagRuinsElite, "ruins_3", "고대 수호 거상");
                    RegionMobs(c.Region, root, f, new[] { 14f });
                    break;
                case "runes":
                    BuildRunePuzzle(c);
                    break;
                case "arena":
                    RP(c, Plat.Mid, -7f, 2.6f);
                    RP(c, Plat.Mid, 7f, 2.6f);
                    RP(c, Plat.Small, 0f, 4.8f);
                    SpawnRegionArena(c, FlagRuinsArena, "ruins_4", new[] { -5f, 5f }, new[] { -8f, 0f, 8f });
                    break;
            }
        }

        // 룬 퍼즐: 벽화에 그려진 순서대로 룬 기둥 4개를 치면 오른쪽 봉인 문이 열린다(대회랑 높은 턱으로 이어지는 지름길 + 보물).
        private static readonly Color[] RuneColors =
        {
            new Color(1f, 0.7f, 0.3f), new Color(0.35f, 0.9f, 1f), new Color(0.8f, 0.5f, 1f), new Color(0.5f, 1f, 0.55f),
        };
        private static readonly int[] RuneOrder = { 1, 3, 0, 2 };   // 하늘색 → 초록 → 주황 → 보라

        private static void BuildRunePuzzle(RegionRoomCtx c)
        {
            var root = c.Root;
            int f = c.FloorY;
            string glyph = RoomDevicePath + "ruins_rune_glyph";
            // 벽화: 둥근 홈 4개에 치는 순서대로 색 룬이 박혀 있다(그림 속 홈 자리: 폭의 23·41·58·76%, 높이의 44%).
            var mural = RoomArt.Sized(root, "Mural", RoomDevicePath + "ruins_mural", new Vector2(-1.5f, f + 5.9f), 6.4f, 0f, c.Tint, -11);
            if (mural != null)
            {
                float mw = mural.bounds.size.x, mh = mural.bounds.size.y, left = -1.5f - mw / 2f;
                float[] slots = { 0.232f, 0.407f, 0.582f, 0.757f };
                for (int i = 0; i < RuneOrder.Length; i++)
                    RoomArt.Picture(root, "MuralRune" + i, glyph, new Vector2(left + mw * slots[i], f + 5.9f + mh * 0.44f - 0.25f), 0.5f, RuneColors[RuneOrder[i]], -10);
            }
            else
            {
                RoomArt.Rect(root, "Mural", new Vector2(-1.5f, f + 7.4f), new Vector2(7f, 2.2f), new Color(0.2f, 0.16f, 0.12f, 0.95f), -11);
                for (int i = 0; i < RuneOrder.Length; i++)
                    RoomArt.Dot(root, "MuralRune" + i, new Vector2(-4.2f + i * 1.8f, f + 7.4f), 0.9f, RuneColors[RuneOrder[i]], -10);
            }

            var runes = new List<SpriteRenderer>();
            float[] xs = { -12f, -5f, 2f, 9f };
            for (int i = 0; i < xs.Length; i++)
            {
                // 룬 기둥 그림의 둥근 홈(높이의 58%)에 색 룬을 끼운다.
                if (RoomArt.Sized(root, "RunePillar" + i, RoomDevicePath + "ruins_rune_pillar", new Vector2(xs[i], f), 0f, 2.2f, c.Tint, -5) != null)
                    runes.Add(RoomArt.Picture(root, "Rune" + i, glyph, new Vector2(xs[i], f + 2.2f * 0.58f - 0.3f), 0.6f, RuneColors[i], -4));
                else
                {
                    RoomArt.Rect(root, "RunePillar" + i, new Vector2(xs[i], f + 1.1f), new Vector2(0.9f, 2.2f), new Color(0.38f, 0.3f, 0.24f), -5);
                    runes.Add(RoomArt.Dot(root, "Rune" + i, new Vector2(xs[i], f + 1.5f), 0.8f, RuneColors[i], -4));
                }
            }

            // 봉인 문 너머에 보물, 그 뒤가 대회랑으로 가는 출구.
            float gateX = c.Right - 4.2f;
            GameObject gate = null;
            if (!WorldFlags.Has(FlagRuinsRunes))
            {
                gate = new GameObject("RuneGate", typeof(BoxCollider2D));
                gate.transform.SetParent(root);
                gate.transform.position = new Vector3(gateX, f + c.Height / 2f, 0f);
                gate.GetComponent<BoxCollider2D>().size = new Vector2(1f, c.Height);
                // 봉인 문 그림(6.5칸): 둥근 봉인 4개(아래부터 높이의 22·39·57·73%)에 벽화 순서의 색 룬.
                var door = RoomArt.Sized(gate.transform, "Door", RoomDevicePath + "ruins_seal_door", new Vector2(gateX, f), 0f, 6.5f, c.Tint, -4);
                if (door != null)
                {
                    float[] seals = { 0.221f, 0.394f, 0.567f, 0.726f };
                    for (int i = 0; i < 4; i++)
                        RoomArt.Picture(gate.transform, "Seal" + i, glyph, new Vector2(gateX, f + 6.5f * seals[i] - 0.25f), 0.5f, RuneColors[RuneOrder[i]], -3);
                }
                else
                {
                    RoomArt.Rect(gate.transform, "Door", new Vector2(gateX, f + c.Height / 2f), new Vector2(1.1f, c.Height), new Color(0.24f, 0.18f, 0.14f), -4);
                    for (int i = 0; i < 4; i++)
                        RoomArt.Dot(gate.transform, "Seal" + i, new Vector2(gateX, f + 1.2f + i * 1.2f), 0.45f, RuneColors[RuneOrder[i]], -3);
                }
            }
            SpawnTreasureChest(root, "ruins_2", c.Right - 1.4f, f);

            var puzzle = new GameObject("RunePuzzle");
            puzzle.transform.SetParent(root);
            puzzle.AddComponent<CrystalPuzzle>().Initialize(player, runes, RuneColors, RuneOrder, () =>
            {
                if (gate != null) Object.Destroy(gate);
            }, FlagRuinsRunes, "룬이 차례로 빛난다... 봉인 문이 열렸다!", "룬의 빛이 꺼졌다. 벽화의 순서를 다시 보자.");
        }

        // ================================================================ 푸른 바다

        private static void BuildSeaRoom(RegionRoomCtx c, string room)
        {
            var root = c.Root;
            int f = c.FloorY;
            switch (room)
            {
                case RoomEntrance:
                    ReturnToVillagePortal(c);
                    RP(c, Plat.Mid, -8f, 2.4f);
                    RP(c, Plat.Small, -4f, 4.6f);
                    RP(c, Plat.Mid, 4f, 3f);
                    RP(c, Plat.Small, 9f, 5f);
                    RegionMobs(c.Region, root, f, new[] { -2f, 8f, 14f });
                    break;
                case "cause":
                    // 둑길: 오른쪽 사다리로 밀물 퍼즐방, 왼쪽 바닥 구멍으로 해저 동굴.
                    Ladder(c, 8.6f, 12.4f, 2.4f, 2.4f, 4);
                    RP(c, Plat.Mid, -14f, 2.6f);
                    RP(c, Plat.Small, -10.5f, 4.6f);
                    RegionMobs(c.Region, root, f, new[] { -16f, 0f, 18f });
                    break;
                case "tide":
                    // 시간 안에 빛나는 조개 네 개를 모두 쳐야 한다(바닥 양끝과 높은 발판들). 꼭대기 옆 돌판에 숨겨진 포탈.
                    Ladder(c, -4f, -0.5f, 2.4f, 2.4f, 4);
                    RP(c, Plat.Mid, 3.5f, 12f);
                    RP(c, Plat.Small, -3.8f, 12f);
                    SpawnHiddenPortal(root, TreasureChests.Sea, -3.8f, f + 12f);
                    SpawnTimedSwitches(c, "빛나는 조개",
                        new[] { new Vector2(-11f, 0f), new Vector2(-0.5f, 4.8f), new Vector2(3.5f, 12f), new Vector2(6.5f, 0f) },
                        new Color(0.5f, 1f, 0.95f), new Color(0.3f, 0.38f, 0.45f), 9f, FlagSeaTide, 9.5f, "sea_2",
                        "조개들이 함께 빛난다... 철창이 열렸다!", "sea_shell_off", "sea_shell_on");
                    break;
                case "grotto":
                    // 해저 동굴: 정예, 왼쪽 산호 벽(부숨) 너머 비밀방, 오른쪽 사다리로 둑길에 돌아간다.
                    Ladder(c, 4.6f, 8.4f, 2.4f, 2.4f, 5);
                    SpawnRegionBreakable(c, new Vector2(c.Left + 0.7f, f + 2.1f), new Vector2(0.8f, 4.2f), new Color(0.75f, 0.38f, 0.45f),
                        FlagSeaCoral, "산호 벽이 부서졌다! 숨겨진 방이다.", "sea_coral_wall");
                    SpawnRegionElite(c, -5f, FlagSeaElite, "sea_3", "심연의 정령");
                    RegionMobs(c.Region, root, f, new[] { 13f, 17f });
                    break;
                case "secret":
                    SpawnTreasureChest(root, "sea_1", -4.5f, f);
                    RP(c, Plat.Small, 3f, 2.2f);
                    break;
                case "arena":
                    RP(c, Plat.Mid, -7f, 2.4f);
                    RP(c, Plat.Mid, 7f, 2.4f);
                    RP(c, Plat.Small, 0f, 4.6f);
                    SpawnRegionArena(c, FlagSeaArena, "sea_4", new[] { -6f, 0f, 6f }, new[] { -9f, -3f, 3f, 9f });
                    break;
            }
        }
    }
}

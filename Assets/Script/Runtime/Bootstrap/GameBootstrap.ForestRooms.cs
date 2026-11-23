using System.Collections.Generic;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.Data;
using Aethoria.Monsters;
using Aethoria.Stages;
using Aethoria.UI;

namespace Aethoria.Bootstrap
{
    // 어둠의 숲(비선형 맵 시범): 방 9개가 왼쪽·오른쪽·위·아래로 이어진 그물 구조(초안: 맵 다이어그램/어둠의 숲 맵 다이어그램.drawio).
    //
    //              [S1 작은 비밀방]                         [S2 열쇠방] 열쇠①
    //                 ↑ 썩은 나뭇가지(공격해서 부숨)            ↑ 어려운 발판(2단 점프+대시)
    //   ▶ [입구] ─────── [A 진행방] ───────────── [B 높은 진행방] ──봉인 문── [보스방 알리아스]
    //        ↓ 떨어지기      ↑ 덮개(D에서 올라오면 열려 지름길이 됨)        열쇠 3개
    //     [C 진행방] ───── [D 진행방] ── 대시로 건너는 높은 턱 ── [S3 퍼즐방] 열쇠③
    //       정예 기사 열쇠②
    //
    // 방 사이 이동은 포탈이 아니라 화면 가장자리(RoomExit)로 걸어 나가거나, 천장 구멍으로 뛰어오르거나, 바닥 구멍으로 떨어진다.
    // 들어온 출구의 반대편 짝 출구 앞에서 시작한다. 진행 상태(열쇠, 덮개, 부순 가지, 퍼즐, 정예 처치, 문)는 WorldFlags로 저장된다.
    // currentStage는 숲 방 안에서 계속 1이다(숲 음악·분위기·퀘스트가 1을 보고 있다). 지금 어느 방인지는 forestRoom이 들고 있다.
    // 그림은 아직 임시(색 사각형·기존 숲 발판·천상 수정 그림)다.
    public static partial class GameBootstrap
    {
        private const string RoomEntrance = "entrance", RoomS1 = "s1", RoomA = "a", RoomB = "b", RoomS2 = "s2",
            RoomC = "c", RoomD = "d", RoomS3 = "s3", RoomBoss = "boss";

        private const string FlagBranch = "forest_branch_broken";
        private const string FlagHatch = "forest_hatch_open";
        private const string FlagElite = "forest_elite_dead";
        private const string FlagKey1 = KeyPickup.KeyPrefix + "1";
        private const string FlagKey2 = KeyPickup.KeyPrefix + "2";
        private const string FlagKey3 = KeyPickup.KeyPrefix + "3";

        private const string CrystalArt = "Backgrounds/Parallax/Heaven/Props/heaven_obelisk_crystal";
        private const string ForestDevicePath = "Art/Props/ForestDevices/";
        // 보스방 앞 봉인 문: 보랏빛 룬 장식 3개가 박힌 쇠창살 문(전멸방 철창문 그림 — 어둠의 숲 분위기라 숲 봉인 문으로 쓴다).
        private const string SealGateArt = "Art/Props/RoomDevices/arena_gate";

        private static string forestRoom = RoomEntrance;
        private static string forestEntry;              // 이 방에 들어온 출구 id(없으면 기본 시작점)
        private static bool forestRoomTransition;      // 방 사이를 넘어가는 중(아니면 입구부터 새로 시작)

        private enum ExitSide { Left, Right, Top, Bottom }

        private sealed class ForestExit
        {
            public string Id;
            public ExitSide Side;
            public float Pos;           // 위·아래 출구는 x, 왼쪽·오른쪽 출구는 바닥 위 턱 높이
            public string TargetRoom;
            public string TargetExit;
            public System.Func<bool> Open;
        }

        // 숲 발판 그림마다 밟는 면(그림 맨 위에서의 픽셀). 기존 숲 맵 배치에서 쓰던 값.
        private static readonly Dictionary<string, int> ForestSurfacePx = new()
        {
            { "forest_bridge", 98 }, { "forest_platform_1", 77 }, { "forest_platform_2", 43 }, { "forest_platform_3", 48 },
            { "forest_platform_4", 70 }, { "forest_platform_5", 53 }, { "forest_platform_6", 44 }, { "forest_platform_7", 40 },
            { "forest_platform_9", 36 }, { "forest_platform_wide", 80 },
        };

        // 방을 넘어갈 때는 화면이 잠깐 어두워졌다가(ScreenFade) 새 방이 밝아진다.
        private static void GoToForestRoom(string room, string entryExit)
            => ScreenFade.Run(() => GoToForestRoomNow(room, entryExit));

        private static void GoToForestRoomNow(string room, string entryExit)
        {
            if (room == RoomBoss)
            {
                currentStage = BossStage;
                BuildStage(enteringForward: true);
                return;
            }
            if (forestRoom == RoomD && room == RoomA && !WorldFlags.Has(FlagHatch))
            {
                WorldFlags.Set(FlagHatch);
                ChoicePopup.Toast("덮개를 밀어 올렸다! 지름길이 열렸다.", true, 2f);
            }
            forestRoom = room;
            forestEntry = entryExit;
            forestRoomTransition = true;
            currentStage = 1;
            BuildStage(enteringForward: true);
        }

        private static void BuildForestRoom()
        {
            // 월드맵·관리자 이동처럼 밖에서 들어오면 입구부터. 비밀의 장소에서 돌아올 때(pendingSpawnPoint)는 있던 방 그대로.
            if (!forestRoomTransition && !pendingSpawnPoint.HasValue)
            {
                forestRoom = RoomEntrance;
                forestEntry = null;
            }
            forestRoomTransition = false;

            var exits = new List<ForestExit>();
            int width, height;
            switch (forestRoom)
            {
                case RoomS1: width = 16; height = 10; break;
                case RoomA: width = 44; height = 14; break;
                case RoomB: width = 30; height = 24; break;
                case RoomS2: width = 20; height = 12; break;
                case RoomC: width = 40; height = 14; break;
                case RoomD: width = 44; height = 18; break;
                case RoomS3: width = 36; height = 16; break;
                default: forestRoom = RoomEntrance; width = 40; height = 14; break;
            }
            DefineForestExits(forestRoom, exits);

            BuildMap(stageRoot.transform, width, height, out int xMin, out int xMax, out int yMin, out int yMax, null, BackgroundGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;
            bool underground = forestRoom == RoomC || forestRoom == RoomD || forestRoom == RoomS3;
            SpawnParallax(TintLayers(ForestParallaxLayers(), underground ? new Color(0.55f, 0.55f, 0.72f) : Color.white), xMin, xMax, yMin);

            float left = xMin, right = xMax + 1;
            var root = stageRoot.transform;
            foreach (var exit in exits)
            {
                if (exit.Side == ExitSide.Bottom && (exit.Open == null || exit.Open())) OpenFloorGap(root, exit.Pos, yMin);
            }

            player.SetCombatLocked(false);
            Vector2 spawn = RoomSpawnPoint(exits, forestEntry, left, right, yMin, height);
            PlacePlayerAtSpawn(spawn.x, spawn.y);
            // 방 밖에 선 흙벽이 보이도록 카메라를 좌우로 조금 더 보낸다.
            FitCamera(xMin - ForestCameraMargin, xMax + ForestCameraMargin, yMin);

            foreach (var exit in exits) SpawnForestExit(root, exit, left, right, yMin, height);
            BuildForestEdges(root, exits, left, right, yMin, height);
            BuildForestRoomContent(root, forestRoom, yMin, left, right, height);

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(root);
            stageManagerGO.AddComponent<StageManager>().Initialize(player);

            Debug.Log($"Aethoria: 어둠의 숲 방 {forestRoom} 진입 (출구 {forestEntry ?? "-"})");
        }

        // 방마다 출구. 짝 출구 id는 상대 방 쪽 id다.
        private static void DefineForestExits(string room, List<ForestExit> exits)
        {
            void Add(string id, ExitSide side, float pos, string target, string targetExit, System.Func<bool> open = null)
                => exits.Add(new ForestExit { Id = id, Side = side, Pos = pos, TargetRoom = target, TargetExit = targetExit, Open = open });

            switch (room)
            {
                case RoomEntrance:
                    Add("right", ExitSide.Right, 0f, RoomA, "left");
                    Add("top", ExitSide.Top, -12f, RoomS1, "bottom", () => WorldFlags.Has(FlagBranch));
                    Add("bottom", ExitSide.Bottom, 8f, RoomC, "top");
                    break;
                case RoomS1:
                    Add("bottom", ExitSide.Bottom, 0f, RoomEntrance, "top");
                    break;
                case RoomA:
                    Add("left", ExitSide.Left, 0f, RoomEntrance, "right");
                    Add("right", ExitSide.Right, 0f, RoomB, "left");
                    Add("bottom", ExitSide.Bottom, 6f, RoomD, "top", () => WorldFlags.Has(FlagHatch));
                    break;
                case RoomB:
                    Add("left", ExitSide.Left, 0f, RoomA, "right");
                    Add("right", ExitSide.Right, 0f, RoomBoss, null, () => WorldFlags.Has(KeyGate.OpenFlag));
                    Add("top", ExitSide.Top, 8f, RoomS2, "bottom");
                    break;
                case RoomS2:
                    Add("bottom", ExitSide.Bottom, 5f, RoomB, "top");
                    break;
                case RoomC:
                    Add("top", ExitSide.Top, 8f, RoomEntrance, "bottom");
                    Add("right", ExitSide.Right, 0f, RoomD, "left");
                    break;
                case RoomD:
                    Add("left", ExitSide.Left, 0f, RoomC, "right");
                    Add("top", ExitSide.Top, 6f, RoomA, "bottom");
                    Add("right", ExitSide.Right, 6f, RoomS3, "left");
                    break;
                case RoomS3:
                    Add("left", ExitSide.Left, 0f, RoomD, "right");
                    break;
            }
        }

        // 들어온 출구(entryId) 앞의 시작점. 없으면 방 왼쪽.
        private static Vector2 RoomSpawnPoint(List<ForestExit> exits, string entryId, float left, float right, int floorY, int height)
        {
            if (pendingSpawnPoint.HasValue) return pendingSpawnPoint.Value; // 비밀의 장소에서 돌아옴(PlacePlayerAtSpawn이 다시 처리한다)
            ForestExit entry = null;
            foreach (var e in exits) if (e.Id == entryId) entry = e;
            if (entry == null) return new Vector2(left + 4f, floorY);
            return entry.Side switch
            {
                ExitSide.Left => new Vector2(left + 2f, floorY + entry.Pos + 0.1f),
                ExitSide.Right => new Vector2(right - 2f, floorY + entry.Pos + 0.1f),
                ExitSide.Top => new Vector2(entry.Pos, floorY + height - 3.4f),     // 위에서 떨어져 들어온다
                _ => new Vector2(entry.Pos + 2.6f, floorY + 0.1f),                  // 아래 구멍에서 올라와 그 옆에 선다
            };
        }

        private static void SpawnForestExit(Transform root, ForestExit exit, float left, float right, int floorY, int height)
            => SpawnRoomExit(root, exit, left, right, floorY, height, e => GoToForestRoom(e.TargetRoom, e.TargetExit));

        private static void SpawnRoomExit(Transform root, ForestExit exit, float left, float right, int floorY, int height,
            System.Action<ForestExit> go)
        {
            Rect area = exit.Side switch
            {
                ExitSide.Left => Rect.MinMaxRect(left - 1f, floorY + exit.Pos - 0.2f, left + 0.35f, floorY + exit.Pos + 3.2f),
                ExitSide.Right => Rect.MinMaxRect(right - 0.35f, floorY + exit.Pos - 0.2f, right + 1f, floorY + exit.Pos + 3.2f),
                ExitSide.Top => Rect.MinMaxRect(exit.Pos - 1.3f, floorY + height - 1.3f, exit.Pos + 1.3f, floorY + height + 1f),
                _ => Rect.MinMaxRect(exit.Pos - 1.1f, floorY - 4f, exit.Pos + 1.1f, floorY - 0.9f),
            };
            var exitGO = new GameObject("RoomExit_" + exit.Id);
            exitGO.transform.SetParent(root);
            var target = exit;
            exitGO.AddComponent<RoomExit>().Initialize(player, area, () => go(target), exit.Open);
        }

        // 바닥 구멍: 바닥 벽을 두 동강 내고, 그 자리에 절벽 틈을 그린다(FloorGapArt).
        // forestArt가 false면 충돌만 뚫는다(그림은 부르는 쪽이 따로 그린다).
        private static void OpenFloorGap(Transform root, float x, int floorY, bool forestArt = true)
        {
            const float half = FloorGapHalf;
            var floor = root.Find("MapBounds/Wall_Bottom");
            if (floor != null)
            {
                var box = floor.GetComponent<BoxCollider2D>();
                float wallLeft = floor.position.x - box.size.x / 2f, wallRight = floor.position.x + box.size.x / 2f;
                float y = floor.position.y, h = box.size.y;
                Object.Destroy(floor.gameObject);
                MakeWall(root, "Wall_Bottom_L", wallLeft, x - half, y, h);
                MakeWall(root, "Wall_Bottom_R", x + half, wallRight, y, h);
            }
            if (forestArt) FloorGapArt(root, x, floorY);
        }

        private static void MakeWall(Transform root, string name, float x0, float x1, float centerY, float height)
        {
            if (x1 - x0 < 0.05f) return;
            var go = new GameObject(name, typeof(BoxCollider2D));
            go.transform.SetParent(root);
            go.transform.position = new Vector3((x0 + x1) / 2f, centerY, 0f);
            go.GetComponent<BoxCollider2D>().size = new Vector2(x1 - x0, height);
        }

        private static ParallaxBackground.Layer[] TintLayers(ParallaxBackground.Layer[] layers, Color tint)
        {
            foreach (var l in layers) l.Tint = new Color(l.Tint.r * tint.r, l.Tint.g * tint.g, l.Tint.b * tint.b, l.Tint.a);
            return layers;
        }

        private static void FP(Transform root, string name, float x, int floorY, float height)
            => SpawnPlatform(root, name, x, floorY + height, ForestSurfacePx[name], ForestPropFolder);

        // ---------------------------------------------------------------- 방 내용

        private static void BuildForestRoomContent(Transform root, string room, int floorY, float left, float right, int height)
        {
            switch (room)
            {
                case RoomEntrance:
                {
                    // 왼쪽 끝은 마을로 나가는 포탈(한 번 묻는다).
                    var back = SpawnPortal(root, left + 0.5f, floorY, backward: true, framed: ForestPortalStyle);
                    back.SetCustomTrigger(() => ConfirmReturnToVillage(back));
                    // 가운데 낮은 발판과 다리
                    FP(root, "forest_platform_4", -2f, floorY, 2.2f);
                    FP(root, "forest_bridge", 2.2f, floorY, 4.2f);
                    // 왼쪽 위로 오르는 길 → 썩은 나뭇가지 → 천장 구멍(S1)
                    FP(root, "forest_platform_4", -8.6f, floorY, 2.4f);
                    FP(root, "forest_platform_6", -12.4f, floorY, 4.8f);
                    FP(root, "forest_platform_3", -9.4f, floorY, 7.2f);
                    FP(root, "forest_platform_2", -12f, floorY, 9.6f);
                    if (!WorldFlags.Has(FlagBranch)) SpawnBranch(root, new Vector2(-12f, floorY + 12f));
                    SpawnKnights(root, floorY, new[] { 0.5f, 14f });
                    break;
                }
                case RoomS1:
                    SpawnTreasureChest(root, "forest_1", -4.5f, floorY);
                    FP(root, "forest_platform_6", 4.5f, floorY, 2.2f);
                    break;
                case RoomA:
                    FP(root, "forest_platform_4", -14f, floorY, 2.3f);
                    FP(root, "forest_platform_wide", -8f, floorY, 4.4f);
                    FP(root, "forest_platform_3", -1.5f, floorY, 3.0f);
                    FP(root, "forest_bridge", 12f, floorY, 3.6f);
                    FP(root, "forest_platform_6", 17f, floorY, 2.2f);
                    if (!WorldFlags.Has(FlagHatch)
                        && RoomArt.Sized(root, "HatchClosed", ForestDevicePath + "forest_hatch", new Vector2(6f, floorY - 0.3f), 2.9f, 0f, Color.white, -8) == null)
                    {
                        // 닫힌 덮개: D에서 올라오면 열린다(아래에서만 열 수 있는 지름길).
                        RoomArt.Rect(root, "HatchClosed", new Vector2(6f, floorY - 0.02f), new Vector2(2.7f, 0.28f), new Color(0.32f, 0.2f, 0.12f), -8);
                        RoomArt.Rect(root, "HatchBand", new Vector2(6f, floorY - 0.02f), new Vector2(0.18f, 0.32f), new Color(0.15f, 0.15f, 0.18f), -7);
                    }
                    SpawnKnights(root, floorY, new[] { -10f, 1.5f, 15f });
                    break;
                case RoomB:
                {
                    // 위로 오르는 어려운 발판(2단 점프 + 대시) → 천장 구멍(S2)
                    FP(root, "forest_platform_4", -10f, floorY, 2.4f);
                    FP(root, "forest_platform_6", -6.5f, floorY, 4.6f);
                    FP(root, "forest_platform_3", -10f, floorY, 7.0f);
                    FP(root, "forest_platform_6", -6f, floorY, 9.4f);
                    FP(root, "forest_platform_6", -1.5f, floorY, 11.4f);   // 가장 먼 틈(약 3.3칸): 대시를 쓰면 편하다
                    FP(root, "forest_platform_6", 2.5f, floorY, 13.6f);
                    FP(root, "forest_platform_6", 6.2f, floorY, 15.9f);
                    FP(root, "forest_platform_6", 10.4f, floorY, 18f);
                    FP(root, "forest_platform_4", 8f, floorY, 20.3f);
                    SpawnKeyGate(root, right - 1.4f, floorY);
                    SpawnKnights(root, floorY, new[] { -3f, 5f });
                    break;
                }
                case RoomS2:
                    FP(root, "forest_platform_1", -4.5f, floorY, 1.2f);     // 제단
                    if (!WorldFlags.Has(FlagKey1)) SpawnKey(root, new Vector2(-4.5f, floorY + 2.4f), FlagKey1);
                    SpawnTreasureChest(root, "forest_2", -8.4f, floorY);
                    SpawnKnights(root, floorY, new[] { 1.5f });
                    break;
                case RoomC:
                    // 입구로 다시 올라가는 길(천장 구멍 x 8)
                    FP(root, "forest_platform_4", 4.6f, floorY, 2.4f);
                    FP(root, "forest_platform_6", 8.2f, floorY, 4.8f);
                    FP(root, "forest_platform_3", 5.2f, floorY, 7.2f);
                    FP(root, "forest_platform_2", 8.8f, floorY, 9.6f);
                    if (!WorldFlags.Has(FlagElite)) SpawnEliteKnight(root, floorY, -8f);
                    else if (!WorldFlags.Has(FlagKey2)) SpawnKey(root, new Vector2(-8f, floorY + 1.2f), FlagKey2); // 잡고 열쇠를 안 주웠으면 그 자리에
                    SpawnKnights(root, floorY, new[] { -16f });
                    break;
                case RoomD:
                    // 위로 올라가 A의 덮개를 여는 길(천장 구멍 x 6)
                    FP(root, "forest_platform_4", 2f, floorY, 2.4f);
                    FP(root, "forest_platform_6", 5.6f, floorY, 4.8f);
                    FP(root, "forest_platform_wide", 1f, floorY, 7.2f);
                    FP(root, "forest_platform_2", 6.2f, floorY, 9.6f);
                    FP(root, "forest_platform_3", 2.6f, floorY, 12f);
                    FP(root, "forest_platform_6", 6f, floorY, 14.4f);
                    // 오른쪽 높은 턱(S3로 가는 길): 앞 발판에서 대시로 건너야 닿는다.
                    FP(root, "forest_platform_4", 10f, floorY, 2.6f);
                    FP(root, "forest_platform_6", 12f, floorY, 4.9f);
                    FP(root, "forest_platform_wide", right - 2.4f, floorY, 6f);
                    // 왼쪽 위 숨겨진 포탈(비밀의 장소)
                    FP(root, "forest_platform_4", -17f, floorY, 2.4f);
                    FP(root, "forest_platform_2", -13.6f, floorY, 4.8f);
                    SpawnHiddenPortal(root, TreasureChests.Forest, -13.6f, floorY + 4.8f);
                    SpawnKnights(root, floorY, new[] { -9f, -1f, 13f });
                    break;
                case RoomS3:
                    BuildCrystalPuzzle(root, floorY, right);
                    break;
            }
        }

        // 썩은 나뭇가지: 천장 구멍 바로 아래를 막는 단단한 가지. 공격하면 부서진다.
        private static void SpawnBranch(Transform root, Vector2 center)
        {
            var go = new GameObject("RottenBranch", typeof(BoxCollider2D));
            go.transform.SetParent(root);
            go.transform.position = center;
            var box = go.GetComponent<BoxCollider2D>();
            box.size = new Vector2(3.6f, 0.6f);
            var sr = RoomArt.Sized(go.transform, "Wood", ForestDevicePath + "forest_rotten_branch", Vector2.zero, 3.9f, 0f, Color.white, -5);
            if (sr != null) sr.transform.position = new Vector3(center.x, center.y - sr.bounds.size.y / 2f, 0f);
            else
            {
                sr = RoomArt.Rect(go.transform, "Wood", center, new Vector2(3.6f, 0.55f), new Color(0.3f, 0.2f, 0.14f), -5);
                RoomArt.Rect(go.transform, "Crack", center + new Vector2(0.4f, 0f), new Vector2(0.12f, 0.6f), new Color(0.12f, 0.08f, 0.06f), -4);
            }
            go.AddComponent<BreakableBarrier>().Initialize(player, FlagBranch, sr, box, center, new Vector2(2.2f, 1.9f));
        }

        private static void SpawnKey(Transform root, Vector2 position, string flag)
        {
            var go = new GameObject("SealKey");
            go.transform.SetParent(root);
            go.transform.position = position;
            var glow = RoomArt.Dot(go.transform, "Glow", position + Vector2.up * 0.4f, 1.6f, new Color(1f, 0.85f, 0.4f, 0.4f), 3);
            if (RoomArt.Sized(go.transform, "Key", ForestDevicePath + "forest_seal_key", position - Vector2.up * 0.2f, 0f, 1.0f, Color.white, 4) == null)
                RoomArt.Picture(go.transform, "Crystal", CrystalArt, position, 0.9f, new Color(1f, 0.82f, 0.35f), 4);
            go.AddComponent<KeyPickup>().Initialize(player, flag, glow);
        }

        private static void SpawnKeyGate(Transform root, float x, int floorY)
        {
            var go = new GameObject("SealedGate", typeof(BoxCollider2D));
            go.transform.SetParent(root);
            go.transform.position = new Vector3(x, floorY + 2.6f, 0f);
            go.GetComponent<BoxCollider2D>().size = new Vector2(1f, 5.2f);
            var sockets = new List<SpriteRenderer>();
            // 봉인된 철창문(5.4칸, 룬 장식 3개가 박힌 쇠창살): 룬 장식 자리(아래부터 높이의 26·50·74%)가 열쇠 자리다.
            if (RoomArt.Sized(go.transform, "Door", SealGateArt, new Vector2(x, floorY), 0f, 5.4f, Color.white, -4) != null)
            {
                float[] runes = { 0.256f, 0.499f, 0.738f };
                for (int i = 0; i < KeyPickup.KeysNeeded && i < runes.Length; i++)
                    sockets.Add(RoomArt.Dot(go.transform, "Socket" + i, new Vector2(x, floorY + 5.4f * runes[i]), 0.62f, Color.gray, -3));
            }
            else
            {
                RoomArt.Rect(go.transform, "Door", new Vector2(x, floorY + 2.6f), new Vector2(1.1f, 5.2f), new Color(0.12f, 0.1f, 0.16f), -4);
                RoomArt.Rect(go.transform, "Trim", new Vector2(x, floorY + 2.6f), new Vector2(1.3f, 5.4f), new Color(0.35f, 0.22f, 0.3f), -5);
                for (int i = 0; i < KeyPickup.KeysNeeded; i++)
                    sockets.Add(RoomArt.Dot(go.transform, "Socket" + i, new Vector2(x, floorY + 1.4f + i * 1.1f), 0.5f, Color.gray, -3));
            }
            go.AddComponent<KeyGate>().Initialize(player, go.GetComponent<BoxCollider2D>(), sockets,
                Resources.Load<Sprite>(ForestDevicePath + "forest_seal_key"));
        }

        // 정예 기사: 기사보다 크고 붉으며 세 배 질기다. 잡으면 그 자리에 봉인의 열쇠 ②를 떨어뜨린다.
        private static void SpawnEliteKnight(Transform root, int floorY, float x)
        {
            var data = KnightData();
            data.monsterName = "붉은 기운의 기사";
            data.level = 4;
            data.maxHp *= 3.5f;
            data.attack *= 1.6f;
            data.defense += 2f;
            data.expReward *= 4;
            data.goldReward *= 4;

            var go = new GameObject("EliteKnight");
            go.transform.position = new Vector3(x, floorY, 0f);
            go.transform.SetParent(root);
            go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.85f, 0.3f, 0.3f), 24, 24);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.6f, 1.3f);
            collider.offset = new Vector2(0f, 0.65f);
            go.AddComponent<Rigidbody2D>();
            go.AddComponent<MonsterWalkSpriteAnimator>();
            go.AddComponent<MonsterAttackSpriteAnimator>().Configure("Art/Monsters/Knight/Attack", 0.6f);
            var monster = go.AddComponent<Monster>();
            monster.AssignData(data);
            go.AddComponent<MonsterAI>();
            go.AddComponent<MonsterHealthBar>();
            go.AddComponent<EliteTint>().Initialize(sr);
            RoomArt.Dot(go.transform, "RedAura", new Vector2(x, floorY + 0.9f), 2.6f, new Color(1f, 0.15f, 0.15f, 0.25f), -6);

            var parent = root;
            monster.OnDied += m =>
            {
                WorldFlags.Set(FlagElite);
                if (!WorldFlags.Has(FlagKey2) && parent != null)
                    SpawnKey(parent, new Vector2(m.transform.position.x, floorY + 1.2f), FlagKey2);
            };
        }

        // 수정 스위치 퍼즐(S3): 벽화(뒤쪽 작은 수정 4개)에 그려진 순서대로 바닥 수정 4개를 치면 오른쪽 끝 철창이 열린다.
        private static readonly Color[] CrystalColors =
        {
            new Color(1f, 0.35f, 0.35f), new Color(0.4f, 0.65f, 1f), new Color(0.45f, 1f, 0.5f), new Color(1f, 0.88f, 0.35f),
        };
        private static readonly int[] CrystalOrder = { 2, 0, 3, 1 };   // 초록 → 빨강 → 노랑 → 파랑

        private static void BuildCrystalPuzzle(Transform root, int floorY, float right)
        {
            // 벽화: 뒤쪽 돌판 위에 순서대로 놓인 작은 수정들(왼쪽부터 치는 순서)
            // 석벽 부조 그림(폭 6.4칸, 키 약 3.1칸)이 있으면 그 둥근 홈 4개(가운데에서 ±0.57·±1.76칸, 바닥에서 1.33칸)에 수정을 끼운다.
            if (RoomArt.Sized(root, "Mural", ForestDevicePath + "forest_mural", new Vector2(-1f, floorY + 5.05f), 6.4f, 0f, Color.white, -11) != null)
            {
                float[] socketX = { -1.76f, -0.57f, 0.57f, 1.76f };
                for (int i = 0; i < CrystalOrder.Length; i++)
                    RoomArt.Picture(root, "MuralCrystal" + i, CrystalArt, new Vector2(-1f + socketX[i], floorY + 5.05f + 1.33f - 0.32f), 0.64f, CrystalColors[CrystalOrder[i]], -10);
            }
            else
            {
                RoomArt.Rect(root, "Mural", new Vector2(-1f, floorY + 6.6f), new Vector2(6.4f, 2.2f), new Color(0.16f, 0.14f, 0.2f, 0.95f), -11);
                for (int i = 0; i < CrystalOrder.Length; i++)
                    RoomArt.Picture(root, "MuralCrystal" + i, CrystalArt, new Vector2(-3.4f + i * 1.6f, floorY + 6f), 1.1f, CrystalColors[CrystalOrder[i]], -10);
            }

            var crystals = new List<SpriteRenderer>();
            float[] xs = { -12f, -5f, 2f, 9f };
            for (int i = 0; i < xs.Length; i++)
            {
                // 수정 스위치 그림(받침대째 바닥에 선다. 흰 수정 — 색은 퍼즐이 입힌다). 없으면 받침돌 위에 천상 수정 그림.
                var crystal = RoomArt.Sized(root, "Crystal" + i, ForestDevicePath + "forest_crystal_switch", new Vector2(xs[i], floorY - 0.05f), 0f, 1.8f, CrystalColors[i], -4);
                if (crystal == null)
                {
                    FP(root, "forest_platform_7", xs[i], floorY, 0.5f); // 받침돌
                    crystal = RoomArt.Picture(root, "Crystal" + i, CrystalArt, new Vector2(xs[i], floorY + 0.5f), 1.6f, CrystalColors[i], -4);
                }
                crystals.Add(crystal);
            }

            // 철창 너머 열쇠 ③
            float cageX = right - 3f;
            var cage = new GameObject("Cage", typeof(BoxCollider2D));
            cage.transform.SetParent(root);
            cage.transform.position = new Vector3(cageX - 1.6f, floorY + 2f, 0f);
            var cageBox = cage.GetComponent<BoxCollider2D>();
            cageBox.size = new Vector2(0.4f, 4f);
            // 가시덩굴 철창 그림(키 4칸)이 있으면 그걸 세우고, 없으면 막대 3개로 그린다.
            if (RoomArt.Sized(cage.transform, "Bars", ForestDevicePath + "forest_cage_bars", new Vector2(cageX - 1.6f, floorY), 0f, 4f, Color.white, -3) == null)
                for (int i = 0; i < 3; i++)
                    RoomArt.Rect(cage.transform, "Bar" + i, new Vector2(cageX - 1.9f + i * 0.3f, floorY + 2f), new Vector2(0.1f, 4f), new Color(0.3f, 0.3f, 0.36f), -3);
            bool solved = WorldFlags.Has(CrystalPuzzle.SolvedFlag);
            if (solved) Object.Destroy(cage);
            if (!WorldFlags.Has(FlagKey3)) SpawnKey(root, new Vector2(cageX, floorY + 1.2f), FlagKey3);

            var puzzleGO = new GameObject("CrystalPuzzle");
            puzzleGO.transform.SetParent(root);
            puzzleGO.AddComponent<CrystalPuzzle>().Initialize(player, crystals, CrystalColors, CrystalOrder, () =>
            {
                if (cage != null) Object.Destroy(cage);
            });
        }
    }
}

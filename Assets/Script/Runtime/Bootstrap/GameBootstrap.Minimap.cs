using System.Collections.Generic;
using UnityEngine;
using Aethoria.Data;
using Aethoria.Stages;
using Aethoria.UI;

namespace Aethoria.Bootstrap
{
    // 던전 지도(MinimapUI)에 넘길 방 배치. 방 크기(칸)를 대략 가로 10칸·세로 7칸 = 1로 줄여 이웃 방과 맞닿게 손으로 놓았다
    // (GameBootstrap.ForestRooms / RegionRooms 머리 주석의 그림과 같은 모양). y는 위로 갈수록 크다.
    // 들어가 본 방만 지도에 그린다(WorldFlags "map_지역_방"으로 저장). 아직 안 가 본 방은 모양도 위치도 보이지 않고,
    // 가 본 방 가장자리에 "이쪽으로 길이 더 있다"는 작은 금빛 표시만 붙는다(열린 출구가 안 가 본 방으로 이어질 때). 보스방도 가 봐야 보인다.
    // 들어가 본 방에는 열쇠·상자·정예·퍼즐·전멸방 아이콘이 붙고, 끝낸 것은 흐려진다.
    // 상자는 방을 지을 때 놓인 것을 "mapchest_지역_방|상자id" 깃발로 기억한다(다음 실행에도 지도에 남게).
    // 지역에 새로 들어서면 지역 이름을, 처음 가 보는 방에 들어가면 방 이름을 화면 위쪽에 잠깐 띄운다.
    public static partial class GameBootstrap
    {
        private const string MapVisitedPrefix = "map_";
        private const string MapChestPrefix = "mapchest_";

        private static MinimapUI minimapUI;
        private static string minimapLastRegion;

        private static readonly Dictionary<string, (string id, string name, Rect rect)[]> MinimapLayouts = new()
        {
            {
                TreasureChests.Forest, new[]
                {
                    (RoomEntrance, "숲 입구", new Rect(0f, 0f, 4f, 2f)),
                    (RoomA, "어둠의 오솔길", new Rect(4f, 0f, 4.4f, 2f)),
                    (RoomB, "높은 나무길", new Rect(8.4f, 0f, 3f, 3.4f)),
                    (RoomBoss, "알리아스의 제단", new Rect(11.4f, 0f, 2f, 2f)),
                    (RoomS1, "썩은 가지 너머", new Rect(0.4f, 2f, 1.6f, 1.4f)),
                    (RoomS2, "우듬지 열쇠방", new Rect(9.2f, 3.4f, 2f, 1.7f)),
                    (RoomC, "기사의 굴", new Rect(0f, -2f, 4f, 2f)),
                    (RoomD, "뿌리 동굴", new Rect(4f, -2.6f, 4.4f, 2.6f)),
                    (RoomS3, "봉인 퍼즐방", new Rect(8.4f, -2.3f, 3.6f, 2.3f)),
                }
            },
            {
                TreasureChests.Plains, new[]
                {
                    (RoomEntrance, "평야 입구", new Rect(0f, 0f, 4f, 2f)),
                    ("field", "바람 부는 들판", new Rect(4f, 0f, 4.4f, 2f)),
                    ("secret", "덩굴 바위 너머", new Rect(4.6f, 2f, 1.6f, 1.4f)),
                    ("tower", "바람의 탑", new Rect(8.4f, 0f, 2.8f, 3.1f)),
                    ("bells", "종 퍼즐방", new Rect(8.6f, 3.1f, 2.6f, 1.7f)),
                    ("arena", "시련의 들판", new Rect(11.2f, 0f, 3f, 2f)),
                    (RegionBossRoom, "슬라임 둥지", new Rect(14.2f, 0f, 2f, 2f)),
                    ("hollow", "지하 동굴", new Rect(6.2f, -2f, 4f, 2f)),
                }
            },
            {
                TreasureChests.Ruins, new[]
                {
                    (RoomEntrance, "폐허 입구", new Rect(0f, 0f, 4f, 2f)),
                    ("hall", "대회랑", new Rect(4f, 0f, 4f, 3.1f)),
                    ("sky", "하늘 다리", new Rect(4.4f, 3.1f, 3.6f, 1.7f)),
                    ("secret", "금 간 벽 너머", new Rect(8f, 3.1f, 1.6f, 1.4f)),
                    ("arena", "시련의 회랑", new Rect(8f, 0f, 3f, 2f)),
                    (RegionBossRoom, "모래룡의 사막", new Rect(11f, 0f, 2f, 2f)),
                    ("crypt", "지하 묘실", new Rect(0f, -2f, 4f, 2f)),
                    ("runes", "룬 퍼즐방", new Rect(4f, -2.3f, 3.6f, 2.3f)),
                }
            },
            {
                TreasureChests.Sea, new[]
                {
                    (RoomEntrance, "해변 입구", new Rect(0f, 0f, 4f, 2f)),
                    ("cause", "파도 둑길", new Rect(4f, 0f, 4.4f, 2f)),
                    ("tide", "밀물 퍼즐방", new Rect(6.4f, 2f, 2.6f, 2.6f)),
                    ("arena", "시련의 해안", new Rect(8.4f, 0f, 3f, 2f)),
                    (RegionBossRoom, "아쿠아의 바다", new Rect(11.4f, 0f, 2f, 2f)),
                    ("grotto", "해저 동굴", new Rect(3.6f, -2.3f, 4f, 2.3f)),
                    ("secret", "산호 벽 너머", new Rect(2f, -2.3f, 1.6f, 1.4f)),
                }
            },
        };

        // 방마다 붙는 아이콘(상자 말고): 종류와 "끝났는지"를 알려 주는 깃발.
        private static readonly Dictionary<string, (MinimapUI.IconKind kind, string doneFlag)[]> MinimapRoomMarks = new()
        {
            { "forest/" + RoomS2, new[] { (MinimapUI.IconKind.Key, FlagKey1) } },
            { "forest/" + RoomC, new[] { (MinimapUI.IconKind.Elite, FlagElite), (MinimapUI.IconKind.Key, FlagKey2) } },
            { "forest/" + RoomS3, new[] { (MinimapUI.IconKind.Puzzle, FlagKey3), (MinimapUI.IconKind.Key, FlagKey3) } },
            { "plains/hollow", new[] { (MinimapUI.IconKind.Elite, FlagPlainsElite) } },
            { "plains/bells", new[] { (MinimapUI.IconKind.Puzzle, FlagPlainsBells) } },
            { "plains/arena", new[] { (MinimapUI.IconKind.Arena, FlagPlainsArena) } },
            { "ruins/crypt", new[] { (MinimapUI.IconKind.Elite, FlagRuinsElite) } },
            { "ruins/runes", new[] { (MinimapUI.IconKind.Puzzle, FlagRuinsRunes) } },
            { "ruins/arena", new[] { (MinimapUI.IconKind.Arena, FlagRuinsArena) } },
            { "sea/grotto", new[] { (MinimapUI.IconKind.Elite, FlagSeaElite) } },
            { "sea/tide", new[] { (MinimapUI.IconKind.Puzzle, FlagSeaTide) } },
            { "sea/arena", new[] { (MinimapUI.IconKind.Arena, FlagSeaArena) } },
        };

        private static void InitMinimap(MinimapUI ui)
        {
            minimapUI = ui;
            ui.StateVersion = () => WorldFlags.Version * 997 + TreasureChests.OpenedTotal;
            ui.RequestRefresh = () => RefreshMinimap(announce: false);
        }

        // 스테이지를 다 지은 뒤 부른다. 지금 서 있는 곳이 방 구조 지역이면 지도를 띄우고, 아니면 숨긴다.
        // announce: 지역에 새로 들어섰거나 처음 가 보는 방이면 이름을 띄운다.
        private static void RefreshMinimap(bool announce = true)
        {
            if (minimapUI == null) return;
            if (!CurrentMinimapRoom(out string region, out string room) || !MinimapLayouts.TryGetValue(region, out var layout))
            {
                minimapUI.Hide();
                // 비밀의 장소(숨겨진 포탈)에 다녀오는 건 지역을 떠난 게 아니다.
                if (announce && currentStage != SecretStage) minimapLastRegion = null;
                return;
            }

            bool firstVisit = !WorldFlags.Has(MapVisitedKey(region, room));
            WorldFlags.Set(MapVisitedKey(region, room));

            var rects = new Dictionary<string, Rect>();
            foreach (var (id, _, rect) in layout) rects[id] = rect;
            var exits = new List<ForestExit>();

            string roomName = null;
            var rooms = new List<MinimapUI.Room>();
            foreach (var (id, name, rect) in layout)
            {
                if (id == room) roomName = name;
                if (!WorldFlags.Has(MapVisitedKey(region, id))) continue;   // 안 가 본 방은 가린다

                // 열린 출구 중 안 가 본 방으로 이어지는 쪽에 길 표시.
                var stubs = new List<MinimapUI.ExitStub>();
                exits.Clear();
                if (region == TreasureChests.Forest) DefineForestExits(id, exits);
                else DefineRegionExits(region, id, exits);
                foreach (var exit in exits)
                {
                    if (exit.Open != null && !exit.Open()) continue;
                    if (exit.TargetRoom == null || WorldFlags.Has(MapVisitedKey(region, exit.TargetRoom))) continue;
                    if (!rects.TryGetValue(exit.TargetRoom, out var target)) continue;
                    stubs.Add(ExitStubBetween(rect, target, exit.Side));
                }

                rooms.Add(new MinimapUI.Room
                {
                    Id = id, Name = name, Rect = rect, Current = id == room, Visited = true, IsBoss = id == RegionBossRoom,
                    Icons = MinimapIcons(region, id), Exits = stubs,
                });
            }

            minimapUI.Show(TreasureChests.RegionName(region), MinimapStatus(region), rooms, () =>
            {
                if ((Object)player == null) return new Vector2(0.5f, 0f);
                Vector3 p = player.transform.position;
                float w = Mathf.Max(1f, mapXMax + 1 - mapXMin);
                float h = Mathf.Max(1f, mapYMax - mapYMin);
                return new Vector2((p.x - mapXMin) / w, (p.y - mapYMin) / h);
            });

            if (!announce) return;
            // 보스방은 보스 등장 연출(이름 자막)이 따로 있어서 띄우지 않는다.
            if (region != minimapLastRegion && room != RegionBossRoom)
                minimapUI.ShowAreaTitle(TreasureChests.RegionName(region), roomName, large: true);
            else if (firstVisit && room != RegionBossRoom && roomName != null)
                minimapUI.ShowAreaTitle(roomName, null, large: false);
            minimapLastRegion = region;
        }

        // 방 가장자리 위, 이웃 방과 맞닿은 구간의 가운데에 길 표시를 둔다.
        private static MinimapUI.ExitStub ExitStubBetween(Rect from, Rect to, ExitSide side)
        {
            float midY = (Mathf.Max(from.yMin, to.yMin) + Mathf.Min(from.yMax, to.yMax)) * 0.5f;
            float midX = (Mathf.Max(from.xMin, to.xMin) + Mathf.Min(from.xMax, to.xMax)) * 0.5f;
            midY = Mathf.Clamp(midY, from.yMin + 0.2f, from.yMax - 0.2f);
            midX = Mathf.Clamp(midX, from.xMin + 0.2f, from.xMax - 0.2f);
            return side switch
            {
                ExitSide.Left => new MinimapUI.ExitStub { Point = new Vector2(from.xMin, midY), Direction = Vector2.left },
                ExitSide.Right => new MinimapUI.ExitStub { Point = new Vector2(from.xMax, midY), Direction = Vector2.right },
                ExitSide.Top => new MinimapUI.ExitStub { Point = new Vector2(midX, from.yMax), Direction = Vector2.up },
                _ => new MinimapUI.ExitStub { Point = new Vector2(midX, from.yMin), Direction = Vector2.down },
            };
        }

        private static List<MinimapUI.Icon> MinimapIcons(string region, string room)
        {
            var icons = new List<MinimapUI.Icon>();
            if (MinimapRoomMarks.TryGetValue(region + "/" + room, out var marks))
                foreach (var (kind, flag) in marks) icons.Add(new MinimapUI.Icon { Kind = kind, Done = WorldFlags.Has(flag) });
            string chestPrefix = MapChestPrefix + region + "_" + room + "|";
            foreach (var flag in WorldFlags.WithPrefix(chestPrefix))
                icons.Add(new MinimapUI.Icon { Kind = MinimapUI.IconKind.Chest, Done = TreasureChests.IsOpened(flag.Substring(chestPrefix.Length)) });
            return icons;
        }

        // 작은 지도 제목 옆 한 줄: 숲은 열쇠 수, 모든 지역은 연 보물상자 수.
        private static string MinimapStatus(string region)
        {
            string chests = $"보물 {TreasureChests.OpenedCount(region)}/{TreasureChests.Total(region)}";
            if (region != TreasureChests.Forest) return chests;
            int keys = WorldFlags.Count(KeyPickup.KeyPrefix);
            string keyText = WorldFlags.Has(KeyGate.OpenFlag) ? "봉인 해제" : $"열쇠 {keys}/3";
            return $"{keyText}  ·  {chests}";
        }

        // 방을 지을 때 상자가 놓이면 그 방의 상자로 기억한다.
        private static void NoteMinimapChest(string chestId)
        {
            if (!CurrentMinimapRoom(out string region, out string room)) return;
            WorldFlags.Set(MapChestPrefix + region + "_" + room + "|" + chestId);
        }

        private static string MapVisitedKey(string region, string room) => MapVisitedPrefix + region + "_" + room;

        private static bool CurrentMinimapRoom(out string region, out string room)
        {
            region = null;
            room = null;
            if (currentStage == 1 || currentStage == 2) { region = TreasureChests.Forest; room = forestRoom; }
            else if (currentStage == BossStage) { region = TreasureChests.Forest; room = RoomBoss; }
            else if (RegionForStage(currentStage) != null) { region = regionId; room = regionRoom; }
            else if (currentStage == PlainsStage3) { region = TreasureChests.Plains; room = RegionBossRoom; }
            else if (currentStage == RuinsStage3) { region = TreasureChests.Ruins; room = RegionBossRoom; }
            else if (currentStage == BlueSeaStage3 || currentStage == SeaChaseStage || currentStage == SeaArenaStage)
            { region = TreasureChests.Sea; room = RegionBossRoom; }
            return region != null;
        }
    }
}

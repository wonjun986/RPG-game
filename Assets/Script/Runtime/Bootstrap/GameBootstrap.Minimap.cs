using System.Collections.Generic;
using UnityEngine;
using Aethoria.Data;
using Aethoria.UI;

namespace Aethoria.Bootstrap
{
    // 던전 지도(MinimapUI)에 넘길 방 배치. 방 크기(칸)를 대략 가로 10칸·세로 7칸 = 1로 줄여 이웃 방과 맞닿게 손으로 놓았다
    // (GameBootstrap.ForestRooms / RegionRooms 머리 주석의 그림과 같은 모양). y는 위로 갈수록 크다.
    // 들어가 본 방은 WorldFlags("map_지역_방")로 저장되어 밝게, 들어가 본 방과 열린 출구로 이어진 방은 어둡게 테두리만 보인다.
    // 보스방은 처음부터 보인다.
    public static partial class GameBootstrap
    {
        private const string MapVisitedPrefix = "map_";

        private static MinimapUI minimapUI;

        private static readonly Dictionary<string, (string id, Rect rect)[]> MinimapLayouts = new()
        {
            {
                TreasureChests.Forest, new[]
                {
                    (RoomEntrance, new Rect(0f, 0f, 4f, 2f)),
                    (RoomA, new Rect(4f, 0f, 4.4f, 2f)),
                    (RoomB, new Rect(8.4f, 0f, 3f, 3.4f)),
                    (RoomBoss, new Rect(11.4f, 0f, 2f, 2f)),
                    (RoomS1, new Rect(0.4f, 2f, 1.6f, 1.4f)),
                    (RoomS2, new Rect(9.2f, 3.4f, 2f, 1.7f)),
                    (RoomC, new Rect(0f, -2f, 4f, 2f)),
                    (RoomD, new Rect(4f, -2.6f, 4.4f, 2.6f)),
                    (RoomS3, new Rect(8.4f, -2.3f, 3.6f, 2.3f)),
                }
            },
            {
                TreasureChests.Plains, new[]
                {
                    (RoomEntrance, new Rect(0f, 0f, 4f, 2f)),
                    ("field", new Rect(4f, 0f, 4.4f, 2f)),
                    ("secret", new Rect(4.6f, 2f, 1.6f, 1.4f)),
                    ("tower", new Rect(8.4f, 0f, 2.8f, 3.1f)),
                    ("bells", new Rect(8.6f, 3.1f, 2.6f, 1.7f)),
                    ("arena", new Rect(11.2f, 0f, 3f, 2f)),
                    (RegionBossRoom, new Rect(14.2f, 0f, 2f, 2f)),
                    ("hollow", new Rect(6.2f, -2f, 4f, 2f)),
                }
            },
            {
                TreasureChests.Ruins, new[]
                {
                    (RoomEntrance, new Rect(0f, 0f, 4f, 2f)),
                    ("hall", new Rect(4f, 0f, 4f, 3.1f)),
                    ("sky", new Rect(4.4f, 3.1f, 3.6f, 1.7f)),
                    ("secret", new Rect(8f, 3.1f, 1.6f, 1.4f)),
                    ("arena", new Rect(8f, 0f, 3f, 2f)),
                    (RegionBossRoom, new Rect(11f, 0f, 2f, 2f)),
                    ("crypt", new Rect(0f, -2f, 4f, 2f)),
                    ("runes", new Rect(4f, -2.3f, 3.6f, 2.3f)),
                }
            },
            {
                TreasureChests.Sea, new[]
                {
                    (RoomEntrance, new Rect(0f, 0f, 4f, 2f)),
                    ("cause", new Rect(4f, 0f, 4.4f, 2f)),
                    ("tide", new Rect(6.4f, 2f, 2.6f, 2.6f)),
                    ("arena", new Rect(8.4f, 0f, 3f, 2f)),
                    (RegionBossRoom, new Rect(11.4f, 0f, 2f, 2f)),
                    ("grotto", new Rect(3.6f, -2.3f, 4f, 2.3f)),
                    ("secret", new Rect(2f, -2.3f, 1.6f, 1.4f)),
                }
            },
        };

        // 스테이지를 다 지은 뒤 부른다. 지금 서 있는 곳이 방 구조 지역이면 지도를 띄우고, 아니면 숨긴다.
        private static void RefreshMinimap()
        {
            if (minimapUI == null) return;
            if (!CurrentMinimapRoom(out string region, out string room) || !MinimapLayouts.TryGetValue(region, out var layout))
            {
                minimapUI.Hide();
                return;
            }

            WorldFlags.Set(MapVisitedKey(region, room));

            // 들어가 본 방에서 열린 출구로 이어진 방은 "알려진 방"으로 보여준다.
            var known = new HashSet<string>();
            var exits = new List<ForestExit>();
            foreach (var (id, _) in layout)
            {
                if (!WorldFlags.Has(MapVisitedKey(region, id))) continue;
                exits.Clear();
                if (region == TreasureChests.Forest) DefineForestExits(id, exits);
                else DefineRegionExits(region, id, exits);
                foreach (var exit in exits)
                    if (exit.Open == null || exit.Open()) known.Add(exit.TargetRoom);
            }

            var rooms = new List<MinimapUI.Room>();
            foreach (var (id, rect) in layout)
            {
                bool visited = WorldFlags.Has(MapVisitedKey(region, id));
                bool isBoss = id == RegionBossRoom;
                if (!visited && !known.Contains(id) && !isBoss) continue;
                rooms.Add(new MinimapUI.Room { Id = id, Rect = rect, Current = id == room, Visited = visited, IsBoss = isBoss });
            }

            minimapUI.Show(TreasureChests.RegionName(region), rooms, () =>
            {
                if (player == null) return new Vector2(0.5f, 0f);
                Vector3 p = player.transform.position;
                float w = Mathf.Max(1f, mapXMax + 1 - mapXMin);
                float h = Mathf.Max(1f, mapYMax - mapYMin);
                return new Vector2((p.x - mapXMin) / w, (p.y - mapYMin) / h);
            });
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

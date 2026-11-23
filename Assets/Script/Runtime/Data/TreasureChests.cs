using System.Collections.Generic;

namespace Aethoria.Data
{
    // 사냥터 곳곳에 숨겨 둔 보물상자 목록과 연 기록. 상자마다 한 번만 열리고, 연 기록은 세이브에 남는다.
    // 지역마다 일반 방 곳곳(비밀방·퍼즐방·정예·전멸방 보상)과 숨겨진 포탈 너머 비밀의 장소 한 곳에 있다.
    // 상자를 맵에 놓는 건 GameBootstrap(Exploration)이 맡고, 여기는 보상과 연 기록만 갖는다 —
    // 그래서 월드맵이 맵을 짓지 않고도 지역별 "보물 n/m"을 보여 줄 수 있다.
    public static class TreasureChests
    {
        public const string Forest = "forest";
        public const string Plains = "plains";
        public const string Ruins = "ruins";
        public const string Sea = "sea";

        public sealed class Entry
        {
            public string Id;
            public string Region;
            public int Gold;
            public int EnhanceStones;
            public int Potions;
            public bool Secret; // 비밀의 장소 상자(보상이 크다)
        }

        // 보상은 지역이 깊을수록 크게. 비밀의 장소 상자는 일반 상자의 약 2.5배.
        private static readonly Entry[] All =
        {
            new Entry { Id = "forest_1", Region = Forest, Gold = 300, EnhanceStones = 1 },
            new Entry { Id = "forest_2", Region = Forest, Gold = 300, Potions = 2 },
            new Entry { Id = "forest_secret", Region = Forest, Gold = 800, EnhanceStones = 3, Potions = 2, Secret = true },
            new Entry { Id = "plains_1", Region = Plains, Gold = 400, Potions = 2 },
            new Entry { Id = "plains_2", Region = Plains, Gold = 400, EnhanceStones = 1 },
            new Entry { Id = "plains_3", Region = Plains, Gold = 600, EnhanceStones = 2 },      // 정예(거대 독 슬라임)
            new Entry { Id = "plains_4", Region = Plains, Gold = 500, Potions = 3 },            // 전멸방
            new Entry { Id = "plains_secret", Region = Plains, Gold = 1000, EnhanceStones = 3, Potions = 2, Secret = true },
            new Entry { Id = "ruins_1", Region = Ruins, Gold = 700, EnhanceStones = 2 },
            new Entry { Id = "ruins_2", Region = Ruins, Gold = 700, Potions = 3 },
            new Entry { Id = "ruins_3", Region = Ruins, Gold = 1000, EnhanceStones = 3 },       // 정예(고대 수호 거상)
            new Entry { Id = "ruins_4", Region = Ruins, Gold = 900, Potions = 3 },             // 전멸방
            new Entry { Id = "ruins_secret", Region = Ruins, Gold = 1500, EnhanceStones = 4, Potions = 2, Secret = true },
            new Entry { Id = "sea_1", Region = Sea, Gold = 1000, EnhanceStones = 2 },
            new Entry { Id = "sea_2", Region = Sea, Gold = 1000, Potions = 3 },
            new Entry { Id = "sea_3", Region = Sea, Gold = 1400, EnhanceStones = 3 },          // 정예(심연의 정령)
            new Entry { Id = "sea_4", Region = Sea, Gold = 1200, Potions = 4 },               // 전멸방
            new Entry { Id = "sea_secret", Region = Sea, Gold = 2000, EnhanceStones = 5, Potions = 3, Secret = true },
        };

        private static readonly HashSet<string> opened = new();

        public static Entry Find(string id)
        {
            foreach (var entry in All)
            {
                if (entry.Id == id) return entry;
            }
            return null;
        }

        public static bool IsOpened(string id) => opened.Contains(id);

        public static void MarkOpened(string id)
        {
            if (Find(id) != null) opened.Add(id);
        }

        public static int Total(string region)
        {
            int n = 0;
            foreach (var entry in All) if (entry.Region == region) n++;
            return n;
        }

        public static int OpenedCount(string region)
        {
            int n = 0;
            foreach (var entry in All) if (entry.Region == region && opened.Contains(entry.Id)) n++;
            return n;
        }

        public static string RegionName(string region) => region switch
        {
            Forest => "어둠의 숲",
            Plains => "광활한 평야",
            Ruins => "잊혀진 폐허",
            Sea => "푸른 바다",
            _ => region,
        };

        // 새 게임이면 null → 아무것도 연 적 없음.
        public static void Restore(List<string> saved)
        {
            opened.Clear();
            if (saved == null) return;
            foreach (var id in saved)
            {
                if (Find(id) != null) opened.Add(id);
            }
        }

        public static List<string> ToSave() => new List<string>(opened);
    }
}

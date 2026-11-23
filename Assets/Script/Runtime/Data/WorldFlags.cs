using System.Collections.Generic;

namespace Aethoria.Data
{
    // 맵 진행 상태 깃발(한 번 켜면 계속 켜져 있는 것들): 주운 열쇠, 열어 둔 지름길 덮개, 부순 벽, 푼 퍼즐, 잡은 정예 몬스터 등.
    // 세이브(SaveData.worldFlags)에 함께 저장된다. 새 게임이면 아무것도 켜지지 않은 상태로 시작한다.
    public static class WorldFlags
    {
        private static readonly HashSet<string> flags = new();

        public static bool Has(string id) => flags.Contains(id);

        // 깃발이 새로 켜지거나 세이브를 불러올 때마다 올라간다(던전 지도가 다시 그릴 때를 알아챈다).
        public static int Version { get; private set; }

        public static void Set(string id)
        {
            if (flags.Add(id)) Version++;
        }

        // 접두사로 시작하는 깃발 개수(예: "forest_key_"로 주운 열쇠 수 세기).
        public static int Count(string prefix)
        {
            int n = 0;
            foreach (var f in flags) if (f.StartsWith(prefix)) n++;
            return n;
        }

        // 접두사로 시작하는 깃발들(예: 던전 지도가 방마다 본 상자 기록을 읽을 때).
        public static List<string> WithPrefix(string prefix)
        {
            var list = new List<string>();
            foreach (var f in flags) if (f.StartsWith(prefix)) list.Add(f);
            return list;
        }

        public static void Restore(List<string> saved)
        {
            flags.Clear();
            Version++;
            if (saved == null) return;
            foreach (var id in saved) flags.Add(id);
        }

        public static List<string> ToSave() => new List<string>(flags);
    }
}

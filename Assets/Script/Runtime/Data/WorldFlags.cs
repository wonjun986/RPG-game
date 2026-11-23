using System.Collections.Generic;

namespace Aethoria.Data
{
    // 맵 진행 상태 깃발(한 번 켜면 계속 켜져 있는 것들): 주운 열쇠, 열어 둔 지름길 덮개, 부순 벽, 푼 퍼즐, 잡은 정예 몬스터 등.
    // 세이브(SaveData.worldFlags)에 함께 저장된다. 새 게임이면 아무것도 켜지지 않은 상태로 시작한다.
    public static class WorldFlags
    {
        private static readonly HashSet<string> flags = new();

        public static bool Has(string id) => flags.Contains(id);

        public static void Set(string id) => flags.Add(id);

        // 접두사로 시작하는 깃발 개수(예: "forest_key_"로 주운 열쇠 수 세기).
        public static int Count(string prefix)
        {
            int n = 0;
            foreach (var f in flags) if (f.StartsWith(prefix)) n++;
            return n;
        }

        public static void Restore(List<string> saved)
        {
            flags.Clear();
            if (saved == null) return;
            foreach (var id in saved) flags.Add(id);
        }

        public static List<string> ToSave() => new List<string>(flags);
    }
}

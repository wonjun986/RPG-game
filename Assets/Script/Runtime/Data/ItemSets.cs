using System.Collections.Generic;

namespace Aethoria.Data
{
    // 세트 장비 정의. 같은 setId를 가진 장비를 여러 부위에 함께 끼면 개수에 따라 보너스 능력치가 붙는다.
    // 보너스는 Inventory.ApplyEquipmentStats에서 장비 능력치 합에 더해지고, 상세 화면의 세트 효과 칸에 설명이 뜬다.
    // 새 세트는 All에 항목을 추가하고, 해당 장비들의 ItemData.setId에 같은 id를 적으면 된다.
    public sealed class ItemSet
    {
        public string id;
        public string name;
        // 몇 개를 끼면 받는 보너스인지. 단계는 누적된다(3세트를 끼면 2세트 보너스도 같이 받는다).
        public (int pieces, ItemStats bonus)[] tiers;

        // 상세 화면 세트 효과 칸에 들어가는 설명(예: "공허의 고딕 세트  2: 스킬 피해 +5%  3: 치명타 확률 +5%").
        public string Describe()
        {
            var parts = new List<string>();
            foreach (var (pieces, bonus) in tiers) parts.Add($"{pieces}세트: {DescribeStats(bonus)}");
            return $"{name} 세트\n" + string.Join("  /  ", parts);
        }

        private static string DescribeStats(ItemStats s)
        {
            var parts = new List<string>();
            if (s.attack != 0f) parts.Add($"공격력 +{s.attack:0.#}");
            if (s.critChance != 0f) parts.Add($"치명타 확률 +{s.critChance:0.#}%");
            if (s.skillDamage != 0f) parts.Add($"스킬 피해 +{s.skillDamage:0.#}%");
            if (s.critDamage != 0f) parts.Add($"치명타 피해 +{s.critDamage:0.#}%");
            return string.Join(", ", parts);
        }
    }

    public static class ItemSets
    {
        // 노아의 상점에서 모을 수 있는 세트: 공허의 자색 크리스털 갑주 + 보라빛 고딕 갑주 치마 + 그림자 걸음 부츠.
        public static readonly ItemSet VoidGothic = new ItemSet
        {
            id = "void_gothic",
            name = "공허의 고딕",
            tiers = new[]
            {
                (2, new ItemStats { skillDamage = 8f }),
                (3, new ItemStats { critChance = 5f, critDamage = 15f }),
            },
        };

        // 보스 드랍으로만 모이는 세트: 악마의 목걸이(알리아스) + 황금 비늘 장화(황금 모래룡) + 심해 여왕의 반지(아쿠아).
        public static readonly ItemSet AbyssLord = new ItemSet
        {
            id = "abyss_lord",
            name = "심연의 군주",
            tiers = new[]
            {
                (2, new ItemStats { attack = 2f }),
                (3, new ItemStats { skillDamage = 10f, critDamage = 20f }),
            },
        };

        private static readonly ItemSet[] All = { VoidGothic, AbyssLord };

        public static ItemSet Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var set in All)
            {
                if (set.id == id) return set;
            }
            return null;
        }

        // 장착 중인 장비 목록을 받아 세트 보너스 합을 돌려준다.
        public static ItemStats BonusFor(IEnumerable<ItemData> equippedItems)
        {
            var counts = new Dictionary<string, int>();
            foreach (var item in equippedItems)
            {
                if (item == null || string.IsNullOrEmpty(item.setId)) continue;
                counts.TryGetValue(item.setId, out int c);
                counts[item.setId] = c + 1;
            }

            var total = new ItemStats();
            foreach (var pair in counts)
            {
                var set = Find(pair.Key);
                if (set == null) continue;
                foreach (var (pieces, bonus) in set.tiers)
                {
                    if (pair.Value >= pieces) total += bonus;
                }
            }
            return total;
        }
    }
}

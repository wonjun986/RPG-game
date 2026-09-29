namespace Aethoria.Data
{
    // 게임에 등장하는 아이템 목록. 새 아이템은 여기에 항목만 추가하면 된다.
    public static class ItemDatabase
    {
        public static readonly ItemData DevilNecklace = new ItemData
        {
            id = "devil_necklace",
            itemName = "악마의 목걸이",
            iconPath = "UI/Items/DevilNecklace",
            category = ItemCategory.Necklace,
            description = "어둠의 기운이 서린 목걸이.",
        };

        // 기본 지급 무기. 새 캐릭터는 항상 이걸 들고 시작한다(GameBootstrap에서 자동 지급/장착).
        public static readonly ItemData ReaperScythe = new ItemData
        {
            id = "reaper_scythe",
            itemName = "사신의 낫",
            iconPath = "UI/Items/DevilScythe",
            category = ItemCategory.Weapon,
            description = "소울이터의 기본 무기.",
        };

        // 노아의 상점 - 무기 탭. 아직 전용 아이콘 아트가 없어서 iconPath를 비워 두면 ShopUI가
        // 아이콘 대신 이름을 글자로 보여준다.
        public static readonly ItemData WornScythe = new ItemData
        {
            id = "worn_scythe",
            itemName = "낡은 낫",
            category = ItemCategory.Weapon,
            description = "오래 써서 날이 무딘 낫.",
            forSale = true,
            shopPrice = 0,
        };

        public static readonly ItemData SteelScythe = new ItemData
        {
            id = "steel_scythe",
            itemName = "강철 낫",
            category = ItemCategory.Weapon,
            description = "단단한 강철로 벼린 낫.",
            forSale = true,
            shopPrice = 300,
        };

        public static readonly ItemData SharpScythe = new ItemData
        {
            id = "sharp_scythe",
            itemName = "예리한 낫",
            category = ItemCategory.Weapon,
            description = "날카롭게 벼려져 베기가 좋은 낫.",
            forSale = true,
            shopPrice = 1000,
        };

        private static readonly ItemData[] All = { DevilNecklace, ReaperScythe, WornScythe, SteelScythe, SharpScythe };

        public static System.Collections.Generic.IReadOnlyList<ItemData> AllItems => All;

        public static ItemData Find(string id)
        {
            foreach (var item in All)
            {
                if (item.id == id) return item;
            }
            return null;
        }
    }
}

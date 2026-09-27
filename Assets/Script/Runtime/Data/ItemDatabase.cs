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

        private static readonly ItemData[] All = { DevilNecklace, ReaperScythe };

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

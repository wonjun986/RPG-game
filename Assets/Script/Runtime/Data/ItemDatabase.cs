namespace Aethoria.Data
{
    // 게임에 등장하는 아이템 목록. 새 아이템은 여기에 항목만 추가하면 된다.
    // 능력치 기준: 캐릭터 기본 공격력 60(레벨당 +2), 치명타 확률 기본 10%, 치명타 피해 기본 150%.
    public static class ItemDatabase
    {
        public static readonly ItemData DevilNecklace = new ItemData
        {
            id = "devil_necklace",
            itemName = "악마의 목걸이",
            iconPath = "UI/Items/DevilNecklace",
            category = ItemCategory.Necklace,
            rarity = ItemRarity.Epic,
            flavorText = "어둠의 기운이 서린 목걸이.",
            description = "보스 기사가 목에 걸고 있던 목걸이.\n걸고 있으면 귓가에 낮은 속삭임이 들려온다.",
            baseStats = new ItemStats { skillDamage = 10f, critDamage = 15f },
            bonusStats = new ItemStats { critChance = 3f },
        };

        // 기본 지급 무기. 새 캐릭터의 가방에 항상 들어 있다(GameBootstrap에서 자동 지급, 장착은 플레이어가 직접).
        public static readonly ItemData ReaperScythe = new ItemData
        {
            id = "reaper_scythe",
            itemName = "사신의 낫",
            iconPath = "UI/Items/DevilScythe",
            category = ItemCategory.Weapon,
            rarity = ItemRarity.Common,
            flavorText = "소울이터의 기본 무기.",
            description = "소울이터가 처음부터 손에 쥐고 있던 낫.\n오래된 만큼 손에 잘 익었다.",
            baseStats = new ItemStats { attack = 10f },
        };

        // 노아의 상점 - 무기 탭. 아직 전용 아이콘 아트가 없어서 iconPath를 비워 두면 ShopUI가
        // 아이콘 대신 이름을 글자로 보여준다.
        public static readonly ItemData WornScythe = new ItemData
        {
            id = "worn_scythe",
            itemName = "낡은 낫",
            category = ItemCategory.Weapon,
            rarity = ItemRarity.Common,
            flavorText = "오래 써서 날이 무딘 낫.",
            description = "노아의 상점 구석에 굴러다니던 낫.\n그래도 없는 것보다는 낫다.",
            baseStats = new ItemStats { attack = 5f },
            forSale = true,
            shopPrice = 0,
        };

        public static readonly ItemData SteelScythe = new ItemData
        {
            id = "steel_scythe",
            itemName = "강철 낫",
            category = ItemCategory.Weapon,
            rarity = ItemRarity.Uncommon,
            flavorText = "단단한 강철로 벼린 낫.",
            description = "마을 대장간에서 강철을 두드려 만든 낫.\n쉽게 이가 빠지지 않는다.",
            baseStats = new ItemStats { attack = 18f, critChance = 3f },
            forSale = true,
            shopPrice = 300,
        };

        public static readonly ItemData SharpScythe = new ItemData
        {
            id = "sharp_scythe",
            itemName = "예리한 낫",
            category = ItemCategory.Weapon,
            rarity = ItemRarity.Rare,
            flavorText = "날카롭게 벼려져 베기가 좋은 낫.",
            description = "숙련된 장인이 날을 몇 번이고 다시 세운 낫.\n스치기만 해도 깊게 베인다.",
            baseStats = new ItemStats { attack = 28f, critChance = 8f, critDamage = 20f },
            bonusStats = new ItemStats { attack = 5f },
            forSale = true,
            shopPrice = 1000,
        };

        // 노아의 상점 - 장비 탭.
        public static readonly ItemData VoidCrystalArmor = new ItemData
        {
            id = "void_crystal_armor",
            itemName = "공허의 자색 크리스털 갑주",
            iconPath = "UI/Items/VoidCrystalArmor",
            category = ItemCategory.Top,
            rarity = ItemRarity.Rare,
            flavorText = "공허의 결정이 박힌 갑주.",
            description = "자색 크리스털이 박힌 찢어진 망토 갑주.\n결정이 빛날 때마다 사슬에 힘이 실린다.",
            baseStats = new ItemStats { critChance = 4f, skillDamage = 8f },
            bonusStats = new ItemStats { critDamage = 10f },
            forSale = true,
            shopPrice = 800,
        };

        public static readonly ItemData GothicArmorSkirt = new ItemData
        {
            id = "gothic_armor_skirt",
            itemName = "보라빛 고딕 갑주 치마",
            iconPath = "UI/Items/GothicArmorSkirt",
            category = ItemCategory.Bottom,
            rarity = ItemRarity.Rare,
            flavorText = "보라빛 천과 가시 장식의 갑주 치마.",
            description = "겹겹이 찢긴 보라빛 천 사이로 가시 장식이 매달린 치마.\n움직일 때마다 사슬이 낮게 울린다.",
            baseStats = new ItemStats { skillDamage = 6f, critDamage = 15f },
            bonusStats = new ItemStats { critChance = 2f },
            forSale = true,
            shopPrice = 700,
        };

        // 엘리의 장비 강화 재료. 몬스터가 가끔 떨어뜨리고, 보스는 여러 개를 준다(GameBootstrap.HandleMonsterDiedForDrops).
        // 아이콘은 대장간 간판에 매달린 보석을 잘라 만든 임시 그림이다.
        public static readonly ItemData EnhanceStone = new ItemData
        {
            id = "enhance_stone",
            itemName = "강화석",
            iconPath = "UI/Items/EnhanceStone",
            category = ItemCategory.Materials,
            rarity = ItemRarity.Uncommon,
            flavorText = "장비를 단련하는 데 쓰는 보랏빛 광석.",
            description = "어둠의 기운이 응축된 광석.\n대장장이 엘리에게 가져가면 장비를 강화해 준다.",
        };

        private static readonly ItemData[] All =
        {
            DevilNecklace, ReaperScythe, WornScythe, SteelScythe, SharpScythe, VoidCrystalArmor, GothicArmorSkirt,
            EnhanceStone,
        };

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

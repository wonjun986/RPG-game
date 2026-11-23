namespace Aethoria.Data
{
    // 게임에 등장하는 아이템 목록. 새 아이템은 여기에 항목만 추가하면 된다.
    // 능력치 기준: 캐릭터 기본 공격력 15(레벨당 +0.5), 치명타 확률 기본 10%, 치명타 피해 기본 150%.
    // 공격력 1이 기본 데미지 0.7에 해당한다(DamageCalculation.md: (공격력 - 방어력) x 0.7 + 5).
    //
    // 밸런스 기준(아이템 점수). 중반(공격력 ~30, 치명타 ~25%/180%, 피해의 6할이 스킬)의 DPS 기여도로 환산:
    //   공격력 1 = 5점, 치명타 확률 1% = 1.2점, 스킬 피해 1% = 1점, 치명타 피해 1% = 0.4점 (기본+추가 옵션 합).
    // 목표 점수: 일반 8 / 고급 13 / 희귀 19 / 영웅 26 / 전설 34에 부위 배율을 곱한다
    //   (무기 x2.5, 상의·하의·목걸이 x1, 신발·반지 x0.85).
    // 같은 등급이면 보스 드랍이 상점품보다 약하지 않게 두고, 상점가는 대략 점수 x 25~50골드(무기는 주력 성장이라 싸게).
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
            description = "알리아스가 목에 걸고 있던 목걸이.\n걸고 있으면 귓가에 낮은 속삭임이 들려온다.",
            baseStats = new ItemStats { skillDamage = 12f, critDamage = 20f },
            bonusStats = new ItemStats { critChance = 5f },
            setId = "abyss_lord",
        };

        // 기본 지급 무기. 새 캐릭터의 가방에 항상 들어 있다(GameBootstrap에서 자동 지급, 장착은 플레이어가 직접).
        public static readonly ItemData ReaperScythe = new ItemData
        {
            id = "reaper_scythe",
            itemName = "사신의 낫",
            iconPath = "UI/Items/DevilScythe",
            category = ItemCategory.Weapon,
            rarity = ItemRarity.Common,
            flavorText = "네크로시아의 기본 무기.",
            description = "네크로시아가 처음부터 손에 쥐고 있던 낫.\n오래된 만큼 손에 잘 익었다.",
            baseStats = new ItemStats { attack = 3f },
        };

        // 노아의 상점 - 무기 탭.
        public static readonly ItemData WornScythe = new ItemData
        {
            id = "worn_scythe",
            iconPath = "UI/Items/WornScythe",
            itemName = "낡은 낫",
            category = ItemCategory.Weapon,
            rarity = ItemRarity.Common,
            flavorText = "오래 써서 날이 무딘 낫.",
            description = "노아의 상점 구석에 굴러다니던 낫.\n날은 무뎌도 기술을 실어 휘두르기엔 손에 잘 붙는다.",
            baseStats = new ItemStats { attack = 2f, skillDamage = 5f },
            forSale = true,
            shopPrice = 0,
        };

        public static readonly ItemData SteelScythe = new ItemData
        {
            id = "steel_scythe",
            iconPath = "UI/Items/SteelScythe",
            itemName = "강철 낫",
            category = ItemCategory.Weapon,
            rarity = ItemRarity.Uncommon,
            flavorText = "단단한 강철로 벼린 낫.",
            description = "마을 대장간에서 강철을 두드려 만든 낫.\n쉽게 이가 빠지지 않는다.",
            baseStats = new ItemStats { attack = 5f, critChance = 4f, critDamage = 5f },
            forSale = true,
            shopPrice = 300,
        };

        public static readonly ItemData SharpScythe = new ItemData
        {
            id = "sharp_scythe",
            iconPath = "UI/Items/SharpScythe",
            itemName = "예리한 낫",
            category = ItemCategory.Weapon,
            rarity = ItemRarity.Rare,
            flavorText = "날카롭게 벼려져 베기가 좋은 낫.",
            description = "숙련된 장인이 날을 몇 번이고 다시 세운 낫.\n스치기만 해도 깊게 베인다.",
            baseStats = new ItemStats { attack = 6f, critChance = 6f, critDamage = 15f },
            bonusStats = new ItemStats { attack = 1f },
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
            baseStats = new ItemStats { critChance = 4f, skillDamage = 10f },
            bonusStats = new ItemStats { critDamage = 10f },
            setId = "void_gothic",
            forSale = true,
            shopPrice = 900,
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
            baseStats = new ItemStats { skillDamage = 8f, critDamage = 20f },
            bonusStats = new ItemStats { critChance = 3f },
            setId = "void_gothic",
            forSale = true,
            shopPrice = 900,
        };

        // ---- 신발/반지 (노아의 상점) ----
        public static readonly ItemData LeatherBoots = new ItemData
        {
            id = "leather_boots",
            itemName = "낡은 가죽 장화",
            iconPath = "UI/Items/LeatherBoots",
            category = ItemCategory.Shoes,
            rarity = ItemRarity.Common,
            flavorText = "발에 잘 맞는 평범한 가죽 장화.",
            description = "노아가 창고에서 꺼내 온 가죽 장화.\n오래 걸어도 발이 아프지 않다.",
            baseStats = new ItemStats { attack = 1f, critChance = 2f },
            forSale = true,
            shopPrice = 200,
        };

        public static readonly ItemData ShadowStepBoots = new ItemData
        {
            id = "shadow_step_boots",
            itemName = "그림자 걸음 부츠",
            iconPath = "UI/Items/ShadowStepBoots",
            category = ItemCategory.Shoes,
            rarity = ItemRarity.Rare,
            flavorText = "발소리가 그림자에 삼켜지는 부츠.",
            description = "보랏빛 가죽에 가시 장식을 두른 부츠.\n신으면 걸음마다 그림자가 발끝을 따라붙는다.",
            baseStats = new ItemStats { critChance = 6f, critDamage = 15f },
            bonusStats = new ItemStats { skillDamage = 3f },
            setId = "void_gothic",
            forSale = true,
            shopPrice = 800,
        };

        public static readonly ItemData SilverRing = new ItemData
        {
            id = "silver_ring",
            itemName = "은빛 반지",
            iconPath = "UI/Items/SilverRing",
            category = ItemCategory.Ring,
            rarity = ItemRarity.Uncommon,
            flavorText = "달빛을 머금은 은반지.",
            description = "마을 장인이 은을 두드려 만든 반지.\n끼고 있으면 손끝이 조금 더 날카로워진다.",
            baseStats = new ItemStats { critDamage = 15f },
            bonusStats = new ItemStats { critChance = 4f },
            forSale = true,
            shopPrice = 350,
        };

        public static readonly ItemData BloodOathRing = new ItemData
        {
            id = "blood_oath_ring",
            itemName = "핏빛 서약의 반지",
            iconPath = "UI/Items/BloodOathRing",
            category = ItemCategory.Ring,
            rarity = ItemRarity.Rare,
            flavorText = "붉은 보석이 맥박처럼 깜빡이는 반지.",
            description = "피로 맺은 서약이 새겨졌다는 반지.\n기술을 쓸 때마다 보석이 뜨겁게 달아오른다.",
            baseStats = new ItemStats { skillDamage = 10f, critChance = 5f },
            forSale = true,
            shopPrice = 800,
        };

        // ---- 보스 전용 드랍 (GameBootstrap.HandleMonsterDiedForDrops) ----
        public static readonly ItemData SlimeKingRing = new ItemData
        {
            id = "slime_king_ring",
            itemName = "슬라임 왕의 반지",
            iconPath = "UI/Items/SlimeKingRing",
            category = ItemCategory.Ring,
            rarity = ItemRarity.Rare,
            flavorText = "말랑한 젤리 속에 왕관 보석이 박힌 반지.",
            description = "평야의 왕 보스 슬라임의 몸속에서 나온 반지.\n손가락에 끼면 묘하게 끈적하지만 힘이 솟는다.",
            baseStats = new ItemStats { attack = 2f, critChance = 5f },
        };

        public static readonly ItemData GoldenScaleBoots = new ItemData
        {
            id = "golden_scale_boots",
            itemName = "황금 비늘 장화",
            iconPath = "UI/Items/GoldenScaleBoots",
            category = ItemCategory.Shoes,
            rarity = ItemRarity.Epic,
            flavorText = "수호룡의 비늘로 감싼 장화.",
            description = "황금 모래룡이 쓰러지며 남긴 비늘로 지은 장화.\n발을 디딜 때마다 모래가 금빛으로 흩날린다.",
            baseStats = new ItemStats { attack = 2f, critDamage = 20f },
            bonusStats = new ItemStats { critChance = 3f },
            setId = "abyss_lord",
        };

        public static readonly ItemData AbyssQueenRing = new ItemData
        {
            id = "abyss_queen_ring",
            itemName = "심해 여왕의 반지",
            iconPath = "UI/Items/AbyssQueenRing",
            category = ItemCategory.Ring,
            rarity = ItemRarity.Epic,
            flavorText = "깊은 바다의 빛이 갇힌 반지.",
            description = "물의 여왕 아쿠아가 끼고 있던 반지.\n귀에 대면 아득한 파도 소리가 들린다.",
            baseStats = new ItemStats { skillDamage = 12f, critChance = 4f },
            bonusStats = new ItemStats { critDamage = 15f },
            setId = "abyss_lord",
        };

        public static readonly ItemData CrimsonMoonScythe = new ItemData
        {
            id = "crimson_moon_scythe",
            itemName = "진홍의 달 낫",
            iconPath = "UI/Items/CrimsonMoonScythe",
            category = ItemCategory.Weapon,
            rarity = ItemRarity.Legendary,
            flavorText = "붉은 달의 마지막 빛을 벼려 낸 낫.",
            description = "루나벨이 쓰러진 자리에 남은 핏빛 결정으로 다시 벼린 낫.\n네크로시아의 손에 쥐어지자 비로소 붉은빛이 잦아들었다.",
            baseStats = new ItemStats { attack = 12f, critChance = 10f, critDamage = 30f },
            bonusStats = new ItemStats { skillDamage = 10f },
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

        // 보스(기사/슬라임)를 잡을 때마다 몇 개씩 얻는 희귀 재료(GameBootstrap.HandleMonsterDiedForDrops).
        // 아직 쓰는 곳은 없다. 아이콘은 Sprite/Items/Crystal District.png를 300x300으로 줄인 것이다.
        public static readonly ItemData CrystalOrb = new ItemData
        {
            id = "crystal_orb",
            itemName = "수정구",
            iconPath = "UI/Items/CrystalOrb",
            category = ItemCategory.Materials,
            rarity = ItemRarity.Rare,
            flavorText = "황금 테두리에 감싸인 채 빛나는 수정구.",
            description = "강한 적이 쓰러지며 남긴 기운이 굳어 생긴 수정구.\n깎인 면마다 보랏빛 광채가 일렁인다.",
        };

        // 회복 소모품. 필드에서 F로 마시면 체력을 회복한다(CharacterPotionUse).
        // 새 캐릭터는 몇 개 들고 시작하고(GameBootstrap), 노아의 상점 기타 탭에서도 산다.
        public static readonly ItemData Potion = new ItemData
        {
            id = "potion",
            itemName = "회복 포션",
            iconPath = "UI/Items/Potion",
            category = ItemCategory.Misc,
            rarity = ItemRarity.Common,
            flavorText = "보랏빛 약물이 찰랑이는 회복 포션.",
            description = "F 키로 마시면 체력을 40 회복한다.",
            healAmount = 40f,
            forSale = true,
            shopPrice = 50,
        };

        private static readonly ItemData[] All =
        {
            DevilNecklace, ReaperScythe, WornScythe, SteelScythe, SharpScythe, VoidCrystalArmor, GothicArmorSkirt,
            EnhanceStone, CrystalOrb, Potion,
            LeatherBoots, ShadowStepBoots, SilverRing, BloodOathRing,
            SlimeKingRing, GoldenScaleBoots, AbyssQueenRing, CrimsonMoonScythe,
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

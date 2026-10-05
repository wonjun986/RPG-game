using System;

namespace Aethoria.Data
{
    // 인벤토리 화면(UI/Item_Screen)의 장비 칸(무기/상의/하의/신발/목걸이/반지) + 탭(재료/기타)과 1:1로 맞춘다.
    public enum ItemCategory { Weapon, Top, Bottom, Shoes, Necklace, Ring, Materials, Misc }

    // 아이템 등급. 상세 화면의 별 개수(1~5)와 "전설 무기" 같은 등급 표기에 쓴다.
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

    // 아이템 하나의 정의. id로 저장 파일과 인벤토리를 매칭하므로 한 번 붙인 id는 바꾸지 않는다.
    [Serializable]
    public class ItemData
    {
        public string id;
        public string itemName;
        public string iconPath;
        public ItemCategory category;
        public ItemRarity rarity;
        // 상세 화면 이름 바로 아래 한 줄 소개.
        public string flavorText;
        // 상세 화면 맨 아래 "아이템 설명" 칸에 들어가는 긴 설명.
        public string description;
        // 장착하면 적용되는 기본 능력치와 추가 옵션. 둘 다 합쳐서 캐릭터에 적용된다.
        public ItemStats baseStats;
        public ItemStats bonusStats;
        // 세트 효과 설명. 비워 두면 "세트 효과 없음"으로 표시된다(세트 효과 자체는 아직 미구현).
        public string setEffect;
        // true인 아이템만 노아의 상점에 뜬다. 보스 드랍 전용 등은 false로 둔다.
        public bool forSale;
        public int shopPrice; // 0이면 무료.

        public ItemStats TotalStats => baseStats + bonusStats;

        // 강화 단계를 반영한 능력치. 강화는 기본 능력치만 올리고 추가 옵션은 그대로 둔다(EnhanceRules 참고).
        public ItemStats BaseStatsAt(int enhanceLevel) => baseStats.Scaled(EnhanceRules.StatMultiplier(enhanceLevel));
        public ItemStats TotalStatsAt(int enhanceLevel) => BaseStatsAt(enhanceLevel) + bonusStats;

        // "사신의 낫 +3"처럼 강화 단계를 붙인 이름(0강이면 이름만).
        public string DisplayName(int enhanceLevel) => enhanceLevel > 0 ? $"{itemName} +{enhanceLevel}" : itemName;

        public bool IsEquipment => category <= ItemCategory.Ring;
        public int StarCount => (int)rarity + 1;

        // "전설 무기"처럼 등급 + 종류로 된 표기.
        public string GradeLabel => RarityLabel(rarity) + " " + CategoryLabel(category);

        public static string RarityLabel(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => "일반",
            ItemRarity.Uncommon => "고급",
            ItemRarity.Rare => "희귀",
            ItemRarity.Epic => "영웅",
            _ => "전설",
        };

        public static string CategoryLabel(ItemCategory category) => category switch
        {
            ItemCategory.Weapon => "무기",
            ItemCategory.Top => "상의",
            ItemCategory.Bottom => "하의",
            ItemCategory.Shoes => "신발",
            ItemCategory.Necklace => "목걸이",
            ItemCategory.Ring => "반지",
            ItemCategory.Materials => "재료",
            _ => "기타",
        };
    }
}

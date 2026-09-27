using System;

namespace Aethoria.Data
{
    // 인벤토리 화면(UI/Item_Screen)의 장비 칸(무기/상의/하의/신발/목걸이/반지) + 탭(재료/기타)과 1:1로 맞춘다.
    public enum ItemCategory { Weapon, Top, Bottom, Shoes, Necklace, Ring, Materials, Misc }

    // 아이템 하나의 정의. id로 저장 파일과 인벤토리를 매칭하므로 한 번 붙인 id는 바꾸지 않는다.
    [Serializable]
    public class ItemData
    {
        public string id;
        public string itemName;
        public string iconPath;
        public ItemCategory category;
        public string description;
    }
}

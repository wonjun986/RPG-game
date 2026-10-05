using System;

namespace Aethoria.Data
{
    // 장비 하나가 주는 능력치. 아이템 상세 화면(UI/Item_Detail)의 네 줄(공격력/치명타 확률/
    // 스킬데미지 증가/치명타 피해량 증가)과 1:1로 맞춘다. 0이면 화면에 "-"로 표시된다.
    [Serializable]
    public struct ItemStats
    {
        public float attack;       // 공격력(고정값)
        public float critChance;   // 치명타 확률(%)
        public float skillDamage;  // 스킬 피해 증가(%)
        public float critDamage;   // 치명타 피해량 증가(%)

        // 모든 능력치에 같은 배율을 곱한다(강화 단계 보정용). 소수 첫째 자리까지만 남긴다.
        public ItemStats Scaled(float multiplier)
        {
            return new ItemStats
            {
                attack = Round1(attack * multiplier),
                critChance = Round1(critChance * multiplier),
                skillDamage = Round1(skillDamage * multiplier),
                critDamage = Round1(critDamage * multiplier),
            };
        }

        private static float Round1(float value) => (float)System.Math.Round(value, 1);

        public static ItemStats operator +(ItemStats a, ItemStats b)
        {
            return new ItemStats
            {
                attack = a.attack + b.attack,
                critChance = a.critChance + b.critChance,
                skillDamage = a.skillDamage + b.skillDamage,
                critDamage = a.critDamage + b.critDamage,
            };
        }
    }
}

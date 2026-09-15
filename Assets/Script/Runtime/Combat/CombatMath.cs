using UnityEngine;

namespace Aethoria.Combat
{
    public static class CombatMath
    {
        public static float RollVariance(float min = 0.9f, float max = 1.1f)
        {
            return Random.Range(min, max);
        }

        // 기본 공격 피해 = (공격력 - 방어력) x 랜덤(0.9~1.1)
        public static float PhysicalDamage(float attackerAttack, float defenderDefense)
        {
            return Mathf.Max(1f, attackerAttack - defenderDefense) * RollVariance();
        }

        // 마법 공격 피해 = (마력 - 방어력 x 0.5) x 랜덤(0.9~1.1)
        public static float MagicalDamage(float attackerMagic, float defenderDefense)
        {
            return Mathf.Max(1f, attackerMagic - defenderDefense * 0.5f) * RollVariance();
        }
    }
}

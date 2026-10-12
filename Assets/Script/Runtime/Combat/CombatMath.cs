using UnityEngine;

namespace Aethoria.Combat
{
    // 데미지 계산식(DamageCalculation.md). 비율식 기반:
    //   최종 = 기본 데미지 x 스킬 배수 x 리듬 판정 배수 x 크리티컬 배수 + 랜덤(-1~+1)
    // 기본 데미지만 여기서 구하고, 배수들은 부르는 쪽(Character.DealDamage, RhythmBattleUI)이 곱한 뒤 Finish로 마무리한다.
    // 약점(속성) 배수는 아직 속성 시스템이 없어 적용하지 않는다(항상 x1.0).
    public static class CombatMath
    {
        public static float RollVariance(float min = 0.9f, float max = 1.1f)
        {
            return Random.Range(min, max);
        }

        // 1단계 기본 데미지 = (공격력 - 방어력) x 0.7 + 5, 반올림.
        // 공격력이 방어력 이하(음수)면 5로 친다.
        public static float BaseDamage(float attackerAttack, float defenderDefense)
        {
            return Mathf.Round(Mathf.Max(0f, attackerAttack - defenderDefense) * 0.7f + 5f);
        }

        // 배수를 다 곱한 데미지에 랜덤(-1~+1)을 더하고 반올림한다. 최소 1.
        public static float Finish(float damage)
        {
            return Mathf.Max(1f, Mathf.Round(damage + Random.Range(-1, 2)));
        }

        // 배수 없는(x1.0) 일반 타격 한 번. 몬스터 공격은 리듬 판정과 무관하게 항상 GOOD(x1.0)이다.
        public static float PhysicalDamage(float attackerAttack, float defenderDefense)
        {
            return Finish(BaseDamage(attackerAttack, defenderDefense));
        }

        // 마법 공격 피해 = (마력 - 방어력 x 0.5) x 랜덤(0.9~1.1)
        public static float MagicalDamage(float attackerMagic, float defenderDefense)
        {
            return Mathf.Max(1f, attackerMagic - defenderDefense * 0.5f) * RollVariance();
        }
    }
}

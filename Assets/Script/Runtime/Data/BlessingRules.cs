using UnityEngine;

namespace Aethoria.Data
{
    // 이리아의 축복. 장비 강화와 별개로 캐릭터 자체에 붙는 전역 보너스 — 기도할 때마다 단계가 하나
    // 오르고 공격력/방어력/치명타 확률/치명타 피해량/스킬 피해 증가가 같이 조금씩 오른다. 장비 강화와
    // 달리 성공 확률이나 강화석 같은 재료가 없다(사제의 축복이라 실패가 없다는 설정) — 골드만 내면 바로 오른다.
    public static class BlessingRules
    {
        public const int MaxLevel = 20;

        private const float AttackPerLevel = 2f;
        private const float DefensePerLevel = 1f;
        private const float CritChancePerLevel = 0.5f;
        private const float CritDamagePerLevel = 2f;
        private const float SkillDamagePerLevel = 1.5f;

        public static float AttackBonus(int level) => AttackPerLevel * Mathf.Clamp(level, 0, MaxLevel);
        public static float DefenseBonus(int level) => DefensePerLevel * Mathf.Clamp(level, 0, MaxLevel);
        public static float CritChanceBonus(int level) => CritChancePerLevel * Mathf.Clamp(level, 0, MaxLevel);
        public static float CritDamageBonus(int level) => CritDamagePerLevel * Mathf.Clamp(level, 0, MaxLevel);
        public static float SkillDamageBonus(int level) => SkillDamagePerLevel * Mathf.Clamp(level, 0, MaxLevel);

        // 골드 비용: 단계가 오를수록 점점 비싸진다(+1 = 200골드, +20 = 4000골드).
        public static int GoldCost(int currentLevel) => 200 * (currentLevel + 1);
    }
}

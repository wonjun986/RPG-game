using UnityEngine;

namespace Aethoria.Data
{
    // 엘리의 장비 강화 규칙. 강화 단계마다 기본 능력치가 15%씩 오른다(+10이면 2.5배).
    // 실패해도 단계는 내려가지 않고, 재료(강화석/골드)만 사라진다.
    public static class EnhanceRules
    {
        public const int MaxLevel = 10;
        private const float StatBonusPerLevel = 0.15f;

        // 현재 단계 → 다음 단계로 올릴 때의 성공 확률.
        private static readonly float[] SuccessRates = { 1f, 0.9f, 0.8f, 0.7f, 0.6f, 0.5f, 0.4f, 0.3f, 0.25f, 0.2f };

        public static float StatMultiplier(int level) => 1f + StatBonusPerLevel * Mathf.Clamp(level, 0, MaxLevel);

        public static float SuccessRate(int currentLevel) =>
            currentLevel >= 0 && currentLevel < SuccessRates.Length ? SuccessRates[currentLevel] : 0f;

        // 필요한 강화석: +1~2는 1개, +3~4는 2개 … +9~10은 5개.
        public static int StoneCost(int currentLevel) => 1 + currentLevel / 2;

        // 필요한 골드: 단계가 오를수록, 등급이 높을수록 비싸다(일반 +1 = 100골드, 전설 +10 = 3000골드).
        public static int GoldCost(ItemData item, int currentLevel)
        {
            float rarityMultiplier = 1f + 0.5f * (int)item.rarity;
            return Mathf.RoundToInt(100f * (currentLevel + 1) * rarityMultiplier / 10f) * 10;
        }
    }
}

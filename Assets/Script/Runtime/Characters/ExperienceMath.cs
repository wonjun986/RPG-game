using UnityEngine;

namespace Aethoria.Characters
{
    // ExperienceFormulas.md에 정리된 경험치 공식을 그대로 옮긴 것. 레벨업에 필요한 경험치,
    // 몬스터 처치 경험치(레벨차 보정 포함), 퀘스트 보상 경험치(레벨 배율) 세 가지를 담당한다.
    public static class ExperienceMath
    {
        // 다음 레벨까지 필요한 경험치. 지수적으로 늘어나 초반은 빠르고 후반은 느려진다.
        public static int RequiredExpForLevel(int level)
        {
            float required = 100f * level * (1f + level * 0.1f);
            return Mathf.Max(1, Mathf.RoundToInt(required));
        }

        // 몬스터 처치 경험치. 플레이어보다 훨씬 약한 몬스터를 잡으면 거의 안 주고, 훨씬 강한 몬스터를
        // 잡으면 더 준다.
        public static int GetMonsterExp(int playerLevel, int monsterLevel, int baseExp)
        {
            float modifier = GetLevelDiffModifier(playerLevel - monsterLevel);
            return Mathf.Max(1, Mathf.RoundToInt(baseExp * modifier));
        }

        private static float GetLevelDiffModifier(int levelDiff)
        {
            if (levelDiff <= -10) return 1.5f; // 몬스터가 훨씬 강함
            if (levelDiff <= -5) return 1.2f;  // 몬스터가 더 강함
            if (levelDiff <= 0) return 1.0f;   // 적정 레벨
            if (levelDiff <= 5) return 0.8f;   // 몬스터가 약함
            if (levelDiff <= 10) return 0.5f;  // 몬스터가 훨씬 약함
            return 0.1f;                       // 몬스터가 매우 약함(사냥터 벗어남)
        }

        // 퀘스트 보상 경험치. 플레이어 레벨이 높을수록 기본 보상에 배율이 붙는다.
        public static int GetQuestExp(int playerLevel, int baseReward)
        {
            float multiplier = 1f + playerLevel * 0.05f;
            return Mathf.Max(baseReward, Mathf.RoundToInt(baseReward * multiplier));
        }
    }
}

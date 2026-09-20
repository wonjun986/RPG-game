using UnityEngine;

namespace Aethoria.Data
{
    [CreateAssetMenu(fileName = "NewCharacterData", menuName = "Aethoria/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Header("Identity")]
        public int characterId;
        public string characterName;
        [TextArea] public string description;

        [Header("Level Range")]
        public int minLevel = 1;
        public int maxLevel = 30;

        [Header("Base Stats (Lv 1)")]
        public StatBlock baseStats;

        [Header("Growth Per Level")]
        public StatBlock growthPerLevel;

        [Header("Unlock")]
        public bool isUnlockedByDefault = true;
        // 레벨 조건 외에 퀘스트 완료 등 추가 조건은 퀘스트 시스템 구현 후 별도로 연결한다.
        public int unlockRequiredLevel;

        public StatBlock GetStatsAtLevel(int level)
        {
            int clampedLevel = Mathf.Clamp(level, minLevel, maxLevel);
            int levelsGained = clampedLevel - minLevel;
            return baseStats + growthPerLevel * levelsGained;
        }
    }
}

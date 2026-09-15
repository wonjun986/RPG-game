using UnityEngine;

namespace Aethoria.Data
{
    [CreateAssetMenu(fileName = "NewMonsterData", menuName = "Aethoria/Monster Data")]
    public class MonsterData : ScriptableObject
    {
        public string monsterName;
        public int level = 1;
        public float maxHp = 10f;
        public float attack = 2f;
        public float defense = 0f;
        public int expReward = 10;
    }
}

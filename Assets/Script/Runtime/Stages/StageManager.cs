using UnityEngine;
using Aethoria.Monsters;

namespace Aethoria.Stages
{
    public class StageManager : MonoBehaviour
    {
        private int remainingMonsters;
        private bool cleared;

        private void Start()
        {
            var monsters = FindObjectsByType<Monster>(FindObjectsSortMode.None);
            remainingMonsters = monsters.Length;

            foreach (var monster in monsters)
            {
                monster.OnDied += HandleMonsterDied;
            }

            Debug.Log($"Aethoria: 스테이지 시작 - 몬스터 {remainingMonsters}마리");
        }

        private void HandleMonsterDied(Monster monster)
        {
            monster.OnDied -= HandleMonsterDied;
            remainingMonsters--;

            if (remainingMonsters <= 0 && !cleared)
            {
                cleared = true;
                Debug.Log("Aethoria: 스테이지 클리어!");
            }
        }
    }
}

using System;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.Monsters;

namespace Aethoria.Stages
{
    public class StageManager : MonoBehaviour
    {
        private Character player;
        private int remainingMonsters;
        private bool cleared;

        // 이 스테이지의 몬스터를 모두 잡았을 때 발생한다. GameBootstrap이 마지막 스테이지에서만
        // 여기에 구독해 게임 클리어 UI를 띄운다.
        public event Action OnCleared;

        public void Initialize(Character playerCharacter)
        {
            player = playerCharacter;
        }

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

            if (player != null && !player.IsDead)
            {
                player.AddExp(monster.Data != null ? monster.Data.expReward : 0);
            }

            if (remainingMonsters <= 0 && !cleared)
            {
                cleared = true;
                Debug.Log("Aethoria: 스테이지 클리어!");
                OnCleared?.Invoke();
            }
        }
    }
}

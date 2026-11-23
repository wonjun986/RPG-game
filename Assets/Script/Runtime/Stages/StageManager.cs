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

        // 이 스테이지의 몬스터를 모두 잡았을 때 발생한다. (보스 처치 후 마을 귀환은 이 이벤트가 아니라
        // GameBootstrap에서 보스 몬스터의 OnDied에 직접 연결한다.)
        public event Action OnCleared;

        public void Initialize(Character playerCharacter)
        {
            player = playerCharacter;
        }

        private bool started;

        // 방이 지어진 뒤에 따로 나온 몬스터(전멸방 웨이브 등)도 경험치·골드를 주게 등록한다. Start 전이면 Start가 알아서 센다.
        public void Track(Monster monster)
        {
            if (!started || monster == null) return;
            remainingMonsters++;
            cleared = false;
            monster.OnDied += HandleMonsterDied;
        }

        private void Start()
        {
            started = true;
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
                if (monster.Data != null)
                {
                    int exp = ExperienceMath.GetMonsterExp(player.Level, monster.Data.level, monster.Data.expReward);
                    player.AddExp(exp);
                    player.AddGold(monster.Data.goldReward);
                    var monsterCollider = monster.GetComponent<Collider2D>();
                    float floorY = monsterCollider != null ? monsterCollider.bounds.min.y : monster.transform.position.y;
                    Aethoria.Fx.LootFx.SpawnGold(monster.transform.position + Vector3.up * 0.5f, floorY, monster.Data.goldReward);
                }
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

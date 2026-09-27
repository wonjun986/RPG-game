using System;
using UnityEngine;
using Aethoria.Combat;
using Aethoria.Data;

namespace Aethoria.Monsters
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class Monster : MonoBehaviour
    {
        [SerializeField] private MonsterData data;

        private float currentHp;

        public MonsterData Data => data;
        public float CurrentHp => currentHp;
        public float MaxHp => data != null ? data.maxHp : 0f;
        public float Attack => data != null ? data.attack : 0f;
        public float Defense => data != null ? data.defense : 0f;
        public bool IsDead => currentHp <= 0f;

        // 사슬 갈고리 등에 꽂혀 있는 동안 켜진다. AI는 이 동안 이동/새 공격 시작을 멈춘다
        // (이미 시작된 공격 모션까지 끊지는 않는다 - 피격 경직과 같은 방식).
        public bool IsImmobilized { get; private set; }
        public void SetImmobilized(bool immobilized) => IsImmobilized = immobilized;

        public event Action<Monster> OnDied;
        // 어느 몬스터든 죽으면 발생한다. 퀘스트처럼 스테이지와 상관없이 처치 수를 세야 하는 쪽에서 구독한다.
        public static event Action<Monster> AnyDied;
        public event Action<Monster, float> OnDamaged;

        private void Awake()
        {
            if (data == null)
            {
                Debug.LogWarning($"{name}: MonsterData가 할당되지 않았습니다.", this);
            }

            currentHp = MaxHp;
        }

        public void AssignData(MonsterData newData)
        {
            data = newData;
            currentHp = MaxHp;
        }

        public void TakeDamage(float finalDamage)
        {
            if (IsDead || finalDamage <= 0f) return;

            currentHp = Mathf.Max(0f, currentHp - finalDamage);
            OnDamaged?.Invoke(this, finalDamage);
            DamagePopup.Create(transform.position, finalDamage, Color.white);

            if (currentHp <= 0f)
            {
                OnDied?.Invoke(this);
                AnyDied?.Invoke(this);
                Destroy(gameObject);
            }
        }

        // 연습장 허수아비처럼 절대 쓰러지지 않아야 하는 대상이 맞을 때마다 체력을 되돌린다.
        // OnDamaged 안에서 부르면 TakeDamage의 사망 판정보다 먼저 체력이 차므로 한 방에 0이 되어도 쓰러지지 않는다.
        public void RestoreFullHp()
        {
            currentHp = MaxHp;
        }

        [ContextMenu("Test/Take 5 Damage")]
        private void DebugTakeDamage() => TakeDamage(5f);
    }
}

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

        public event Action<Monster> OnDied;
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
                Destroy(gameObject);
            }
        }

        [ContextMenu("Test/Take 5 Damage")]
        private void DebugTakeDamage() => TakeDamage(5f);
    }
}

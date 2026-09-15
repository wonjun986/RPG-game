using System;
using UnityEngine;
using Aethoria.Data;

namespace Aethoria.Characters
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class Character : MonoBehaviour
    {
        [SerializeField] private CharacterData data;
        [SerializeField] private int level = 1;

        private StatBlock currentStats;
        private float currentHp;
        private float currentMana;

        public CharacterData Data => data;
        public int Level => level;
        public StatBlock Stats => currentStats;
        public float CurrentHp => currentHp;
        public float MaxHp => currentStats.hp;
        public float CurrentMana => currentMana;
        public float MaxMana => currentStats.mana;
        public bool IsDead => currentHp <= 0f;

        public event Action<Character> OnDied;
        public event Action<Character, float> OnDamaged;
        public event Action<Character> OnLevelUp;

        protected virtual void Awake()
        {
            InitializeStats();
        }

        public void AssignData(CharacterData newData)
        {
            data = newData;
            InitializeStats();
        }

        public void InitializeStats()
        {
            if (data == null)
            {
                Debug.LogWarning($"{name}: CharacterData가 할당되지 않았습니다.", this);
                return;
            }

            currentStats = data.GetStatsAtLevel(level);
            currentHp = currentStats.hp;
            currentMana = currentStats.mana;
        }

        public void SetLevel(int newLevel)
        {
            if (data == null) return;

            float hpRatio = MaxHp > 0f ? currentHp / MaxHp : 1f;
            float mpRatio = MaxMana > 0f ? currentMana / MaxMana : 1f;

            level = Mathf.Clamp(newLevel, data.minLevel, data.maxLevel);
            currentStats = data.GetStatsAtLevel(level);
            currentHp = currentStats.hp * hpRatio;
            currentMana = currentStats.mana * mpRatio;

            OnLevelUp?.Invoke(this);
        }

        public void LevelUp()
        {
            SetLevel(level + 1);
        }

        public void TakeDamage(float finalDamage)
        {
            if (IsDead || finalDamage <= 0f) return;

            currentHp = Mathf.Max(0f, currentHp - finalDamage);
            OnDamaged?.Invoke(this, finalDamage);

            if (currentHp <= 0f)
            {
                OnDied?.Invoke(this);
            }
        }

        public void Heal(float amount)
        {
            currentHp = Mathf.Min(MaxHp, currentHp + amount);
        }

        public bool TrySpendMana(float amount)
        {
            if (currentMana < amount) return false;
            currentMana -= amount;
            return true;
        }

        public void RestoreMana(float amount)
        {
            currentMana = Mathf.Min(MaxMana, currentMana + amount);
        }

        [ContextMenu("Test/Take 10 Damage")]
        private void DebugTakeDamage() => TakeDamage(10f);

        [ContextMenu("Test/Level Up")]
        private void DebugLevelUp() => LevelUp();
    }
}

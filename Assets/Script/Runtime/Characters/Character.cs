using System;
using UnityEngine;
using Aethoria.Combat;
using Aethoria.Data;

namespace Aethoria.Characters
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class Character : MonoBehaviour
    {
        [SerializeField] private CharacterData data;
        [SerializeField] private int level = 1;
        [SerializeField] private float manaRegenPerSecond = 5f;

        private StatBlock currentStats;
        private float currentHp;
        private float currentMana;
        private int currentExp;
        private bool isInvincible;
        private bool isCombatLocked;

        public CharacterData Data => data;
        public int Level => level;
        public StatBlock Stats => currentStats;
        public float CurrentHp => currentHp;
        public float MaxHp => currentStats.hp;
        public float CurrentMana => currentMana;
        public float MaxMana => currentStats.mana;
        public bool IsDead => currentHp <= 0f;
        public bool IsInvincible => isInvincible;
        public bool IsCombatLocked => isCombatLocked;
        public int CurrentExp => currentExp;
        public int ExpToNextLevel => level * 100; // 레벨이 오를수록 다음 레벨까지 필요한 경험치도 늘어난다

        public event Action<Character> OnDied;
        public event Action<Character, float> OnDamaged;
        public event Action<Character> OnLevelUp;

        protected virtual void Awake()
        {
            InitializeStats();
        }

        protected virtual void Update()
        {
            if (IsDead || manaRegenPerSecond <= 0f) return;
            RestoreMana(manaRegenPerSecond * Time.deltaTime);
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

        // 몬스터를 잡는 등으로 경험치를 얻는다. 최대 레벨이 아니면 문턱을 넘을 때마다 자동으로 레벨업한다.
        public void AddExp(int amount)
        {
            if (amount <= 0 || data == null || level >= data.maxLevel) return;

            currentExp += amount;
            while (level < data.maxLevel && currentExp >= ExpToNextLevel)
            {
                currentExp -= ExpToNextLevel;
                LevelUp();
            }
        }

        // 궁극기 시전 등으로 무적 상태일 때는 피해를 전혀 받지 않는다.
        public void SetInvincible(bool invincible)
        {
            isInvincible = invincible;
        }

        // 마을처럼 몬스터가 없는 안전 지역에서는 공격 키(Z)가 NPC 대화 키와 겹치므로 공격을 막는다.
        public void SetCombatLocked(bool locked)
        {
            isCombatLocked = locked;
        }

        public void TakeDamage(float finalDamage)
        {
            if (IsDead || isInvincible || finalDamage <= 0f) return;

            currentHp = Mathf.Max(0f, currentHp - finalDamage);
            OnDamaged?.Invoke(this, finalDamage);
            DamagePopup.Create(transform.position, finalDamage, Color.red);

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

        [ContextMenu("Test/Add 50 Exp")]
        private void DebugAddExp() => AddExp(50);
    }
}

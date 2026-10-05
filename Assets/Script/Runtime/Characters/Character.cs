using System;
using System.Collections;
using UnityEngine;
using Aethoria.Combat;
using Aethoria.Data;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CharacterMovement2D))]
    public class Character : MonoBehaviour
    {
        [SerializeField] private CharacterData data;
        [SerializeField] private int level = 1;
        [SerializeField] private float manaRegenPerSecond = 5f;

        public const float BaseCritChancePercent = 0f;
        public const float BaseCritDamagePercent = 150f;

        [Header("피격 반응")]
        [SerializeField] private float knockbackForce = 6f;
        [SerializeField] private float knockbackDuration = 0.15f;
        [SerializeField] private float postHitInvincibleDuration = 0.5f;

        private Rigidbody2D body;
        private CharacterMovement2D movement;
        private Coroutine knockbackRoutine;

        private StatBlock currentStats;
        // 장착 중인 장비들의 능력치 합. Inventory가 장착이 바뀔 때마다 다시 계산해서 넣어준다.
        private ItemStats equipmentStats;
        private float currentHp;
        private float currentMana;
        private int currentExp;
        private int gold;
        private bool isInvincible;
        private float postHitInvincibleRemaining;
        private bool isCombatLocked;

        public CharacterData Data => data;
        public int Level => level;
        // 레벨 능력치 + 장비 공격력. 스킬/공격 피해 계산은 모두 이 값을 쓴다.
        public StatBlock Stats
        {
            get
            {
                var stats = currentStats;
                stats.attack += equipmentStats.attack;
                return stats;
            }
        }
        public ItemStats EquipmentStats => equipmentStats;
        // 치명타 확률(%), 치명타 피해(기본 150%), 스킬 피해 증가(%). 모두 장비로만 올라간다.
        public float CritChancePercent => BaseCritChancePercent + equipmentStats.critChance;
        public float CritDamagePercent => BaseCritDamagePercent + equipmentStats.critDamage;
        public float SkillDamagePercent => equipmentStats.skillDamage;
        public float CurrentHp => currentHp;
        public float MaxHp => currentStats.hp;
        public float CurrentMana => currentMana;
        public float MaxMana => currentStats.mana;
        public bool IsDead => currentHp <= 0f;
        // 스킬로 거는 무적(SetInvincible)과 피격 직후 잠깐의 무적(postHitInvincibleRemaining) 둘 중
        // 하나라도 걸려 있으면 무적이다. 서로 독립된 타이머라 한쪽이 끝나도 다른 쪽을 끄지 않는다.
        public bool IsInvincible => isInvincible || postHitInvincibleRemaining > 0f;
        public bool IsCombatLocked => isCombatLocked;
        public int CurrentExp => currentExp;
        public int ExpToNextLevel => ExperienceMath.RequiredExpForLevel(level);
        public int Gold => gold;
        public float PostHitInvincibleDuration => postHitInvincibleDuration;

        public event Action<Character> OnDied;
        public event Action<Character, float> OnDamaged;
        public event Action<Character> OnLevelUp;
        public event Action<int> OnGoldChanged;

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            movement = GetComponent<CharacterMovement2D>();
            InitializeStats();
        }

        protected virtual void Update()
        {
            if (postHitInvincibleRemaining > 0f)
            {
                postHitInvincibleRemaining = Mathf.Max(0f, postHitInvincibleRemaining - Time.deltaTime);
            }

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

        // 저장 파일에서 이어하기: 레벨/경험치/골드를 되돌리고 체력/마나는 가득 채운다.
        // 레벨업 연출이 아니므로 OnLevelUp은 발생시키지 않는다.
        public void RestoreProgress(int savedLevel, int savedExp, int savedGold)
        {
            if (data == null) return;

            level = Mathf.Clamp(savedLevel, data.minLevel, data.maxLevel);
            currentStats = data.GetStatsAtLevel(level);
            currentHp = currentStats.hp;
            currentMana = currentStats.mana;
            currentExp = Mathf.Max(0, savedExp);
            gold = Mathf.Max(0, savedGold);
            OnGoldChanged?.Invoke(gold);
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

        // 몬스터를 잡는 등으로 골드를 얻는다.
        public void AddGold(int amount)
        {
            if (amount <= 0) return;

            gold += amount;
            OnGoldChanged?.Invoke(gold);
        }

        public void SetEquipmentStats(ItemStats stats)
        {
            equipmentStats = stats;
        }

        // 플레이어가 몬스터를 때릴 때의 공통 피해 처리. 공격력 x 배율로 기본 피해를 굴리고,
        // 스킬이면 스킬 피해 증가를 곱한 뒤, 치명타 확률에 걸리면 치명타 피해 배율을 곱한다.
        public float DealDamage(Monster target, float attackMultiplier, bool isSkill)
        {
            if (target == null || target.IsDead) return 0f;

            float damage = CombatMath.PhysicalDamage(Stats.attack * attackMultiplier, target.Defense);
            if (isSkill) damage *= 1f + SkillDamagePercent / 100f;

            bool isCritical = UnityEngine.Random.value * 100f < CritChancePercent;
            if (isCritical) damage *= CritDamagePercent / 100f;

            target.TakeDamage(damage, isCritical);
            return damage;
        }

        // 상점 구매 등으로 골드를 쓴다. 보유량이 모자라면 아무 것도 하지 않고 false를 반환한다.
        public bool TrySpendGold(int amount)
        {
            if (amount <= 0 || gold < amount) return false;

            gold -= amount;
            OnGoldChanged?.Invoke(gold);
            return true;
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

        // sourcePosition: 공격이 날아온 위치(몬스터/투사체 위치 등). 넉백 방향을 거기서 멀어지는
        // 쪽으로 잡는 데 쓴다. 모르면 null로 두면 바라보는 반대 방향으로 밀려난다.
        public void TakeDamage(float finalDamage, Vector2? sourcePosition = null)
        {
            if (IsDead || IsInvincible || finalDamage <= 0f) return;

            currentHp = Mathf.Max(0f, currentHp - finalDamage);
            OnDamaged?.Invoke(this, finalDamage);
            DamagePopup.Create(transform.position, finalDamage, Color.red);

            if (currentHp <= 0f)
            {
                OnDied?.Invoke(this);
                return;
            }

            postHitInvincibleRemaining = postHitInvincibleDuration;
            ApplyKnockback(sourcePosition);
        }

        private void ApplyKnockback(Vector2? sourcePosition)
        {
            if (body == null || movement == null) return;

            Vector2 direction = sourcePosition.HasValue
                ? (Vector2)transform.position - sourcePosition.Value
                : -movement.FacingDirection;
            if (direction.sqrMagnitude < 0.0001f) direction = -movement.FacingDirection;
            direction.Normalize();

            if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
            knockbackRoutine = StartCoroutine(KnockbackRoutine(direction));
        }

        // 잠깐 입력을 잠그고 직접 속도를 줘서 뒤로 튕겨 나가게 한다(이동 입력이 같은 프레임에
        // 덮어쓰지 못하도록 CharacterMovement2D.SetInputLocked를 대시/스킬과 같은 방식으로 재사용).
        private IEnumerator KnockbackRoutine(Vector2 direction)
        {
            movement.SetInputLocked(true);
            body.linearVelocity = new Vector2(direction.x * knockbackForce, Mathf.Max(body.linearVelocity.y, knockbackForce * 0.35f));

            yield return new WaitForSeconds(knockbackDuration);

            movement.SetInputLocked(false);
            knockbackRoutine = null;
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

        [ContextMenu("Test/Add 100 Gold")]
        private void DebugAddGold() => AddGold(100);
    }
}

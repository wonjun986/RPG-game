using System;
using System.Collections;
using UnityEngine;
using Aethoria.Combat;
using Aethoria.Data;
using Aethoria.Monsters;
using Aethoria.Bootstrap;

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

        public const float BaseCritChancePercent = 10f;
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
        // 이리아의 축복 특성별 단계(TalentId 순서). 레벨업으로 얻은 축복 점수를 찍어 올린다.
        private readonly int[] talentRanks = new int[BlessingRules.TalentCount];
        // 불사의 가호(수호 핵심 특성)를 다시 쓸 수 있는 시각(Time.unscaledTime).
        private float undyingReadyAt;
        private float currentHp;
        private float currentMana;
        private int currentExp;
        private int gold;
        private bool isInvincible;
        private float postHitInvincibleRemaining;
        private bool isCombatLocked;

        public CharacterData Data => data;
        public int Level => level;
        // ---- 이리아의 축복(특성 트리) ----
        public int TalentRank(TalentId id) => talentRanks[(int)id];
        public bool HasTalent(TalentId id) => talentRanks[(int)id] > 0;
        public int TalentPointsSpent
        {
            get
            {
                int sum = 0;
                foreach (int r in talentRanks) sum += r;
                return sum;
            }
        }
        public int TalentPointsSpentIn(TalentBranch branch)
        {
            int sum = 0;
            foreach (var node in BlessingRules.Nodes) if (node.Branch == branch) sum += talentRanks[(int)node.Id];
            return sum;
        }
        public int TalentPointsTotal => BlessingRules.PointsForLevel(level);
        public int TalentPointsAvailable => Mathf.Max(0, TalentPointsTotal - TalentPointsSpent);
        // 스탯 특성의 지금 수치(찍은 단계 x 단계당 수치).
        private float TalentValue(TalentId id) => BlessingRules.Node(id).PerRank * talentRanks[(int)id];
        // 예전 축복(골드로 기도) 세이브를 불러와 돌려준 골드. 축복 화면이 한 번 안내한 뒤 0으로 지운다.
        public int BlessingRefundNotice { get; set; }

        // 이 특성을 지금 한 단계 더 찍을 수 있는지(점수/단계 상한/단계 해금/갈림길). 안 되면 reason에 이유.
        public bool CanRaiseTalent(TalentId id, out string reason)
        {
            var node = BlessingRules.Node(id);
            int rank = talentRanks[(int)id];
            if (rank >= node.MaxRank) { reason = "이미 최대 단계예요"; return false; }
            if (node.Rival.HasValue && talentRanks[(int)node.Rival.Value] > 0)
            {
                reason = $"갈림길에서 '{BlessingRules.Node(node.Rival.Value).Name}'을(를) 골랐어요";
                return false;
            }
            int need = BlessingRules.TierRequirement[node.Tier];
            int spent = TalentPointsSpentIn(node.Branch);
            if (spent < need)
            {
                reason = $"{BlessingRules.BranchNames[(int)node.Branch]}에 {need}점을 찍으면 열려요 (지금 {spent}점)";
                return false;
            }
            if (TalentPointsAvailable <= 0) { reason = "축복 점수가 없어요 — 레벨업하면 1점씩 생겨요"; return false; }
            reason = null;
            return true;
        }

        public bool RaiseTalent(TalentId id)
        {
            if (!CanRaiseTalent(id, out _)) return false;
            float hpRatio = MaxHp > 0f ? currentHp / MaxHp : 1f;
            talentRanks[(int)id]++;
            if (id == TalentId.Vitality) currentHp = MaxHp * hpRatio; // 최대 체력이 늘면 비율을 지켜 같이 늘린다
            return true;
        }

        // 찍은 특성을 전부 되돌린다(골드는 부르는 쪽이 낸다).
        public void ResetTalents()
        {
            float hpRatio = MaxHp > 0f ? currentHp / MaxHp : 1f;
            System.Array.Clear(talentRanks, 0, talentRanks.Length);
            currentHp = MaxHp * hpRatio;
        }

        // 저장 파일에서 특성 단계를 되돌린다("특성이름:단계" 목록). 단계 상한·갈림길·단계 해금·점수 합을 다시 검사해서
        // 어긋나면(레벨보다 많이 찍혔거나 규칙이 바뀌었으면) 전부 비운다 — 점수는 레벨에서 다시 나오므로 잃는 건 없다.
        public void RestoreTalents(System.Collections.Generic.IReadOnlyList<string> saved)
        {
            System.Array.Clear(talentRanks, 0, talentRanks.Length);
            if (saved != null)
            {
                foreach (var entry in saved)
                {
                    int colon = entry != null ? entry.IndexOf(':') : -1;
                    if (colon <= 0 || !System.Enum.TryParse(entry.Substring(0, colon), out TalentId id)) continue;
                    if (!int.TryParse(entry.Substring(colon + 1), out int rank)) continue;
                    talentRanks[(int)id] = Mathf.Clamp(rank, 0, BlessingRules.Node(id).MaxRank);
                }
            }
            bool valid = TalentPointsSpent <= TalentPointsTotal;
            foreach (var node in BlessingRules.Nodes)
            {
                if (talentRanks[(int)node.Id] == 0) continue;
                if (node.Rival.HasValue && talentRanks[(int)node.Rival.Value] > 0) valid = false;
                if (TalentPointsBelowTier(node) < BlessingRules.TierRequirement[node.Tier]) valid = false;
            }
            if (!valid) System.Array.Clear(talentRanks, 0, talentRanks.Length);
            currentHp = Mathf.Min(currentHp, MaxHp);
        }

        // 단계 해금 검사용: 그 갈래에서 이 특성보다 낮은 단계에 찍은 점수.
        private int TalentPointsBelowTier(TalentNode target)
        {
            int sum = 0;
            foreach (var node in BlessingRules.Nodes)
                if (node.Branch == target.Branch && node.Tier < target.Tier) sum += talentRanks[(int)node.Id];
            return sum;
        }

        public System.Collections.Generic.List<string> TalentsToSave()
        {
            var list = new System.Collections.Generic.List<string>();
            for (int i = 0; i < talentRanks.Length; i++)
                if (talentRanks[i] > 0) list.Add(((TalentId)i) + ":" + talentRanks[i]);
            return list;
        }

        // 리듬 전투·포션이 묻는 특성 수치들.
        public float TalentPerfectMultiplier(float baseValue) => HasTalent(TalentId.Perfectionist) ? BlessingRules.PerfectionistMultiplier : baseValue;
        public float TalentComboCap(float baseValue) => HasTalent(TalentId.Tempo) ? BlessingRules.TempoComboCap : baseValue;
        public float TalentGaugeMultiplier => 1f + TalentValue(TalentId.Pulse) / 100f;
        public float TalentPotionMultiplier => HasTalent(TalentId.HolyPotion) ? 1f + BlessingRules.HolyPotionBonus : 1f;

        // 레벨 능력치 + 장비 공격력 + 축복 특성 공격력/방어력. 스킬/공격 피해 계산은 모두 이 값을 쓴다.
        public StatBlock Stats
        {
            get
            {
                var stats = currentStats;
                stats.attack += equipmentStats.attack + TalentValue(TalentId.SharpScythe);
                stats.defense += TalentValue(TalentId.IronSkin);
                return stats;
            }
        }
        public ItemStats EquipmentStats => equipmentStats;
        // 치명타 확률(기본 10%), 치명타 피해(기본 150%), 스킬 피해 증가(%). 기본값 위로는 장비와 축복으로 올라간다.
        public float CritChancePercent => BaseCritChancePercent + equipmentStats.critChance + TalentValue(TalentId.ReaperEye);
        public float CritDamagePercent => BaseCritDamagePercent + equipmentStats.critDamage + TalentValue(TalentId.DeepWound);
        public float SkillDamagePercent => equipmentStats.skillDamage + TalentValue(TalentId.Resonance);
        public float CurrentHp => currentHp;
        public float MaxHp => currentStats.hp * (1f + TalentValue(TalentId.Vitality) / 100f) * (1f - MaxHpPenalty);
        // 전투 중에만 쌓이는 최대 체력 감소 비율(루나벨의 응시). 저장되지 않고 ClearMaxHpPenalty로 풀린다.
        public float MaxHpPenalty { get; private set; }
        public float CurrentMana => currentMana;
        public float MaxMana => currentStats.mana;
        public bool IsDead => currentHp <= 0f;
        // 스킬로 거는 무적(SetInvincible)과 피격 직후 잠깐의 무적(postHitInvincibleRemaining) 둘 중
        // 하나라도 걸려 있으면 무적이다. 서로 독립된 타이머라 한쪽이 끝나도 다른 쪽을 끄지 않는다.
        public bool IsInvincible => isInvincible || postHitInvincibleRemaining > 0f;
        // 관리자 모드(AdminUI)의 무적. 스킬 무적/피격 무적과 따로 놀아서 그쪽 타이머가 끝나도 풀리지 않는다.
        public bool GodMode { get; set; }
        // 릴리의 리듬 대결(미니게임) 동안 켜진다. 쓰러질 만큼 맞아도 체력 1로 버텨서 진짜로 쓰러지지 않는다
        // (체력 1이 되면 대결에서 진 것으로 친다 — RhythmBattleUI).
        public bool SurviveLethalHit { get; set; }
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
            Aethoria.Fx.GroundShadow.Attach(gameObject, 1.6f); // 발밑 그림자(몸 판정이 좁아 넓게)
        }

        protected virtual void Update()
        {
            if (postHitInvincibleRemaining > 0f)
            {
                // 리듬전투 등 Time.timeScale=0로 멈춰 있는 동안에도 줄어들어야 한다 — scaled deltaTime을
                // 쓰면 멈춰 있는 동안 영원히 무적 상태로 남는다(한 번 맞은 뒤로 다신 피해를 안 받음).
                postHitInvincibleRemaining = Mathf.Max(0f, postHitInvincibleRemaining - Time.unscaledDeltaTime);
            }

            UpdateBleed();

            if (IsDead || manaRegenPerSecond <= 0f) return;
            RestoreMana(manaRegenPerSecond * (1f + TalentValue(TalentId.SoulFlow) / 100f) * Time.deltaTime);
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
            currentHp = MaxHp;
            currentMana = currentStats.mana;
        }

        public void SetLevel(int newLevel)
        {
            if (data == null) return;

            float hpRatio = MaxHp > 0f ? currentHp / MaxHp : 1f;
            float mpRatio = MaxMana > 0f ? currentMana / MaxMana : 1f;

            level = Mathf.Clamp(newLevel, data.minLevel, data.maxLevel);
            currentStats = data.GetStatsAtLevel(level);
            currentHp = MaxHp * hpRatio;
            currentMana = currentStats.mana * mpRatio;

            OnLevelUp?.Invoke(this);
            SfxPlayer.Play(SfxPlayer.LevelUp);
        }

        // 저장 파일에서 이어하기: 레벨/경험치/골드를 되돌리고 체력/마나는 가득 채운다.
        // 레벨업 연출이 아니므로 OnLevelUp은 발생시키지 않는다.
        public void RestoreProgress(int savedLevel, int savedExp, int savedGold)
        {
            if (data == null) return;

            level = Mathf.Clamp(savedLevel, data.minLevel, data.maxLevel);
            currentStats = data.GetStatsAtLevel(level);
            currentHp = MaxHp;
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

        // 플레이어가 몬스터를 때릴 때의 공통 피해 처리(DamageCalculation.md).
        // 기본 데미지((공격력 - 방어력) x 0.7 + 5)에 배율(리듬 판정 x 스킬 배수)을 곱하고,
        // 스킬이면 장비의 스킬 피해 증가를, 치명타 확률에 걸리면 치명타 피해 배율을 곱한 뒤 랜덤 -1~+1을 더한다.
        public float DealDamage(Monster target, float attackMultiplier, bool isSkill)
        {
            if (target == null || target.IsDead) return 0f;

            float damage = CombatMath.BaseDamage(Stats.attack, target.Defense) * attackMultiplier;
            if (isSkill) damage *= 1f + SkillDamagePercent / 100f;

            // 축복 특성: 처형인(체력 절반 이하) / 첫 일격(체력 가득) / 사신의 심판(체력 25% 이하면 확정 치명타).
            float hpRatio = target.MaxHp > 0f ? target.CurrentHp / target.MaxHp : 1f;
            if (HasTalent(TalentId.Executioner) && hpRatio <= BlessingRules.ExecutionerHpRatio) damage *= 1f + BlessingRules.ExecutionerBonus;
            if (HasTalent(TalentId.FirstStrike) && hpRatio >= 0.999f) damage *= 1f + BlessingRules.FirstStrikeBonus;

            bool isCritical = UnityEngine.Random.value * 100f < CritChancePercent
                || (HasTalent(TalentId.Execution) && hpRatio <= BlessingRules.ExecutionHpRatio);
            if (isCritical) damage *= CritDamagePercent / 100f;
            damage = CombatMath.Finish(damage);

            target.TakeDamage(damage, isCritical, isSkill);
            // 축복 특성 피의 갈증: 준 피해의 일부만큼 체력 회복.
            if (HasTalent(TalentId.Bloodthirst) && !IsDead) Heal(damage * BlessingRules.BloodthirstRatio);
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
        // hitReaction=false면 피격 직후 무적/넉백을 걸지도, 그 무적에 막히지도 않는다 — 리듬전투처럼
        // 0.5초 안에 노드가 여러 개 연달아 오는 곳에서 연속 피격이 무적에 묻히지 않게 할 때 쓴다.
        // (스킬로 거는 무적은 그대로 존중한다.)
        public void TakeDamage(float finalDamage, Vector2? sourcePosition = null, bool hitReaction = true)
        {
            bool blocked = hitReaction ? IsInvincible : isInvincible;
            if (IsDead || blocked || GodMode || finalDamage <= 0f) return;

            // 축복 특성 굳건함: 받는 피해 감소.
            finalDamage *= 1f - TalentValue(TalentId.Steadfast) / 100f;
            // 축복 특성 불사의 가호: 쓰러질 피해면 체력 1로 버틴다(쿨타임마다 한 번).
            // 확정 즉사 기믹(알리아스 처형처럼 최대 체력의 몇 배를 한 번에 주는 피해)은 막지 못한다.
            bool undying = HasTalent(TalentId.Undying) && !SurviveLethalHit && currentHp - finalDamage <= 0f
                && finalDamage < MaxHp * 5f
                && currentHp > 1f && Time.unscaledTime >= undyingReadyAt;

            currentHp = Mathf.Max(SurviveLethalHit || undying ? Mathf.Min(1f, currentHp) : 0f, currentHp - finalDamage);
            if (undying)
            {
                undyingReadyAt = Time.unscaledTime + BlessingRules.UndyingCooldown;
                Aethoria.UI.ChoicePopup.Toast("불사의 가호! 쓰러지지 않고 버텼다", true, 1.6f);
            }
            OnDamaged?.Invoke(this, finalDamage);
            DamagePopup.Create(transform.position, finalDamage, Color.red);
            SfxPlayer.Play(SfxPlayer.NecrosiaHurt);

            if (currentHp <= 0f)
            {
                OnDied?.Invoke(this);
                return;
            }

            if (!hitReaction) return;
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

        // 릴리의 리듬 대결이 끝나면 대결 전 체력으로 되돌린다(대결 중의 피해/회복은 남기지 않는다).
        public void RestoreHp(float hp)
        {
            if (IsDead) return;
            currentHp = Mathf.Clamp(hp, 1f, MaxHp);
        }

        // 줄어든 최대치를 넘는 체력은 잘려 나가지만, 이것만으로 쓰러지지는 않는다.
        public void ReduceMaxHp(float ratio, float maxPenalty)
        {
            MaxHpPenalty = Mathf.Min(maxPenalty, MaxHpPenalty + ratio);
            currentHp = Mathf.Min(currentHp, MaxHp);
        }

        public void ClearMaxHpPenalty() => MaxHpPenalty = 0f;

        // 루나벨의 '주시' 디버프: 이 시각(Time.time)까지 아이템(포션)을 쓸 수 없다.
        public float ItemLockedUntil { get; set; }
        public bool ItemsLocked => Time.time < ItemLockedUntil;
        public float ItemLockRemaining => Mathf.Max(0f, ItemLockedUntil - Time.time);

        private const float BleedTickInterval = 1f;
        private static readonly Color BleedPopupColor = new Color(0.75f, 0.1f, 0.15f);
        private float bleedRemaining;
        private float bleedRatioPerTick;
        private float bleedTickTimer;

        // 출혈: 다시 걸리면 겹치지 않고 남은 시간만 새로 채운다. ratioPerSecond는 최대 체력 대비 초당 피해 비율.
        public void ApplyBleed(float ratioPerSecond, float duration)
        {
            if (bleedRemaining <= 0f) bleedTickTimer = BleedTickInterval;
            bleedRatioPerTick = ratioPerSecond * BleedTickInterval;
            bleedRemaining = duration;
        }

        public void ClearBleed() => bleedRemaining = 0f;

        // 피격 포즈/넉백 없이 체력만 줄인다(OnDamaged를 내지 않아서 출혈이 출혈을 다시 걸지 않는다). 연출용 무적 중에는 멈춘다.
        private void UpdateBleed()
        {
            if (bleedRemaining <= 0f || IsDead) return;

            bleedRemaining -= Time.deltaTime;
            bleedTickTimer -= Time.deltaTime;
            if (bleedTickTimer > 0f) return;

            bleedTickTimer += BleedTickInterval;
            if (isInvincible || GodMode) return;

            float damage = MaxHp * bleedRatioPerTick;
            currentHp = Mathf.Max(SurviveLethalHit ? Mathf.Min(1f, currentHp) : 0f, currentHp - damage);
            DamagePopup.Create(transform.position, damage, BleedPopupColor, 0.8f);
            if (currentHp <= 0f) OnDied?.Invoke(this);
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

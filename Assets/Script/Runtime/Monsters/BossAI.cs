using System.Collections;
using UnityEngine;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 보스 몬스터의 이동/공격 AI. 체력·방어력 등은 기존 Monster/MonsterData가 그대로 담당하고,
    // 여기서는 "플레이어를 쫓아가다가 사거리에 들어오면 콤보 패턴을 시전한다"는 행동과
    // 경직/슈퍼아머 상태를 담당한다.
    // 근접 패턴 두 가지는 각각 3연타 콤보로 구성해, 적은 패턴 수로도 위협적으로 느껴지게 한다.
    // 플레이어가 멀리 떨어져 있으면 점프 내려찍기로 플레이어 자리까지 한 번에 날아오고,
    // 중거리에서는 검을 휘둘러 땅을 타고 미끄러지는 검기(BossSwordWave)를 날린다.
    [RequireComponent(typeof(Monster))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BossAI : MonoBehaviour
    {
        [Header("이동/감지")]
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float attackRange = 1.4f;
        [SerializeField] private float gravityScale = 4f;

        [Header("패턴 A: 돌진 3연타 (근접, 조금씩 전진하며 베기)")]
        [SerializeField] private int dashComboHits = 3;
        [SerializeField] private float dashHitInterval = 0.35f;
        [SerializeField] private float dashStepDistance = 0.4f;
        [SerializeField] private float dashHitRadius = 1f;
        [SerializeField] private float dashDamageMultiplier = 1f;

        [Header("패턴 B: 휩쓸기 3연타 (넓은 범위, 마지막 타가 더 강함)")]
        [SerializeField] private int sweepComboHits = 3;
        [SerializeField] private float sweepHitInterval = 0.4f;
        [SerializeField] private float sweepRadius = 2f;
        [SerializeField] private float sweepDamageMultiplier = 0.9f;
        [SerializeField] private float sweepFinisherMultiplier = 1.3f;

        [Header("패턴 C: 점프 내려찍기 (멀리 있으면 플레이어 자리로 날아와 넓은 충격파)")]
        [SerializeField] private float jumpMinRange = 4.5f; // 이보다 멀리 떨어져 있으면 점프로 날아온다
        [SerializeField] private float jumpMaxDistance = 16f; // 한 번 점프로 이동하는 최대 거리(사실상 맵 어디든)
        [SerializeField] private float jumpDuration = 1.4f; // 8프레임 모션 전체 길이
        [SerializeField] private float jumpSlamRadius = 2.2f;
        [SerializeField] private float jumpDamageMultiplier = 1.6f;
        [SerializeField] private float jumpCooldown = 5f;

        [Header("패턴 D: 검기 (중거리에서 땅을 타고 미끄러지는 칼날)")]
        [SerializeField] private float waveMaxRange = 10f;
        [SerializeField] private float waveCooldown = 4.5f;
        [SerializeField] private float waveSwingDuration = 0.8f; // 기본 공격 모션 한 번
        [SerializeField, Range(0f, 1f)] private float waveReleaseTiming = 0.55f;
        [SerializeField] private float waveSpeed = 9f;
        [SerializeField] private float waveTravelRange = 12f;
        [SerializeField] private float waveScale = 1.8f; // 맞는 범위도 그림 크기에 맞춰 함께 커진다
        [SerializeField] private float waveDamageMultiplier = 1.2f;
        [SerializeField] private float waveForward = 0.9f; // 몸 중심에서 검기가 솟는 곳까지 앞쪽 거리
        [SerializeField] private float aboveThreshold = 1.8f; // 플레이어 발이 바닥보다 이만큼 높으면 대각선 검기로 노린다
        [SerializeField] private float aimedWaveHeight = 1.2f; // 대각선 검기가 출발하는 높이(검을 휘두르는 가슴께)

        [Header("분노 (체력 절반 이하)")]
        [SerializeField, Range(0f, 1f)] private float enrageHealthFraction = 0.5f;
        [SerializeField] private float enrageWaveDamageMultiplier = 0.9f; // 공격마다 양옆으로 함께 날아가는 검기
        [SerializeField] private float plantDuration = 1.2f;      // 땅에 검을 꽂고 무릎 꿇기(Plant 8프레임)
        [SerializeField] private float raiseDuration = 0.8f;      // 검을 하늘로 치켜들기(SpecialRaise 4프레임)
        [SerializeField] private float raiseHold = 0.6f;          // 치켜든 채로 검게 물드는 순간 잠깐 멈춤
        [SerializeField] private Color enragedColor = new Color(0.2f, 0.17f, 0.24f); // 분노하면 온몸이 검게 물든다
        [SerializeField] private float enragedSpeedScale = 1.3f;   // 분노 후 이동 속도
        [SerializeField] private float enragedCooldownScale = 0.65f; // 분노 후 패턴 사이 간격/쿨타임
        // 눈에서 흩날리는 붉은 실크(Effects/AliasEyeSilk). 피벗이 실크 왼쪽 끝(CharacterArtPostprocessor)이라 그 끝을 눈에 붙인다.
        // 걷기 그림(전투 중 가장 오래 보이는 모습) 속 투구 눈 자리: 피벗 기준 앞으로 약 0.13, 위로 2.13(192px = 1칸).
        // 실크가 투구에 묻혀 보여 눈 자리보다 조금 위(2.3)에 붙인다.
        // 왼쪽을 볼 때 화면 오른쪽 눈(뒤쪽 눈)에서 실크가 시작되도록 앞쪽 눈보다 조금 뒤로 잡았다.
        [SerializeField] private Vector2 eyeOffset = new Vector2(0.13f, 2.3f);
        [SerializeField] private float silkFrameDuration = 0.1f;

        [Header("피격")]
        // 맞아도 경직되지 않는다(경직 전용 모션이 없어 대기 모습으로 굳어 보였다). 대신 붉게 번쩍이기만 한다.
        [SerializeField] private bool staggerOnHit = false;
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.45f, 0.45f);
        [SerializeField] private float hitFlashDuration = 0.1f;

        [SerializeField] private float patternCooldown = 1.2f;

        [Header("경직 / 슈퍼아머")]
        [SerializeField] private float staggerFlinchDuration = 0.35f; // 스킬 시전 중 맞으면 경직되는 시간
        [SerializeField] private float superArmorInterval = 10f; // 피격 여부와 상관없이 이 주기마다 슈퍼아머 발동
        [SerializeField] private float superArmorDuration = 3f;
        [SerializeField] private Color superArmorColor = new Color(1f, 0.85f, 0.2f);

        private Monster monster;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private MonsterAttackSpriteAnimator attackAnimator;
        private Character target;
        private Color normalColor;

        private bool isAttacking;
        private bool nextIsDash = true;

        // 필살기(릴리의 리듬 대결 미니게임에서 적 턴에 발동): 체력이 각 문턱(50%, 20%) 이하로 처음 내려간 뒤의 적 턴에 한 번씩 쓴다.
        // 검을 들어올리고(SpecialRaise) → 기운을 모으다가(SpecialCharge 반복) → 기본 공격 모션과 함께 검기(Effects/BossSwordWave)를 날린다.
        private static readonly float[] SpecialMoveHealthFractions = { 0.5f, 0.2f };
        public const string SpecialRaisePath = "Art/Monsters/BossKnight/SpecialRaise";
        public const string SpecialChargePath = "Art/Monsters/BossKnight/SpecialCharge";
        public const string SwordWavePath = "Art/Effects/BossSwordWave";
        // 지금까지 쓴 필살기 횟수(0부터). 두 번째 필살기는 더 어려운 채보로 나온다.
        public int SpecialMovesUsed { get; private set; }
        public bool ShouldUseSpecialMove => !monster.IsDead && SpecialMovesUsed < CrossedSpecialThresholds();

        // 한 방에 두 문턱을 함께 넘었으면 필살기는 한 번만 쓰고 지나간다(연달아 두 번 쓰지 않게).
        public void MarkSpecialMoveUsed() => SpecialMovesUsed = CrossedSpecialThresholds();

        private int CrossedSpecialThresholds()
        {
            int crossed = 0;
            foreach (float fraction in SpecialMoveHealthFractions)
            {
                if (monster.CurrentHp <= monster.MaxHp * fraction) crossed++;
            }
            return crossed;
        }
        // 보스방 등장 연출(BossIntroCutscene) 동안은 아무 행동도 하지 않고 제자리에서 대기 모션만 한다.
        private bool isHoldingForIntro;
        private bool isStaggered;
        private bool isSuperArmor;
        private float jumpCooldownTimer;
        private float waveCooldownTimer;
        private Coroutine hitFlashRoutine;
        // 분노 전환: 체력이 절반 아래로 처음 떨어지면 하던 패턴을 마친 뒤 땅에 검을 꽂고 대사를 하고, 검을 치켜들며 분노한다.
        private bool enrageQueued;
        private bool enraged;
        private SpriteRenderer eyeSilk;
        private Sprite[] silkFrames;
        private float silkTimer;
        private int silkIndex;
        private const string PlantClip = "Plant";
        private const string PlantHoldClip = "PlantHold";
        private const string RaiseClip = "Raise";
        public const string PlantPath = "Art/Monsters/BossKnight/Plant";
        public const string EyeSilkPath = "Art/Effects/AliasEyeSilk";
        // 검을 꽂은 채 하는 대사. 보스 대화창을 가진 등장 연출(BossIntroCutscene)이 채워 넣는다(없으면 대사 없이 넘어간다).
        public System.Func<IEnumerator> EnrageTalk { get; set; }
        // 분노(2페이즈)했거나 분노 전환을 기다리는 중인가. 관리자 모드가 2페이즈로 바로 넘길 때 쓴다.
        public bool IsPhaseTwo => enraged || enrageQueued;
        public event System.Action OnEnraged;
        private Coroutine patternRoutine;
        private Coroutine staggerRoutine;
        private Coroutine superArmorCycleRoutine;

        private void Awake()
        {
            monster = GetComponent<Monster>();
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            attackAnimator = GetComponent<MonsterAttackSpriteAnimator>();
            body.gravityScale = gravityScale;
            body.freezeRotation = true;
            normalColor = spriteRenderer.color;
            jumpCooldownTimer = jumpCooldown * 0.5f; // 보스전 시작하자마자 뛰어들지 않도록 약간 늦춘다
            if (attackAnimator != null)
            {
                var plant = LoadSorted(PlantPath);
                if (plant.Length > 0)
                {
                    attackAnimator.AddClip(PlantClip, plant);
                    attackAnimator.AddClip(PlantHoldClip, new[] { plant[plant.Length - 1] });
                }
                attackAnimator.AddClip(RaiseClip, SpecialRaisePath);
            }
            waveCooldownTimer = waveCooldown * 0.5f;

            target = Object.FindFirstObjectByType<Character>();
            monster.OnDamaged += HandleDamaged;
            monster.OnDied += HandleDied;

            if (staggerOnHit) superArmorCycleRoutine = StartCoroutine(SuperArmorCycleRoutine());
        }

        // 플레이어의 W(악마의 손)처럼 연타로 경직을 노리는 스킬이 맞는 동안 계속 호출해서
        // 슈퍼아머 주기를 처음부터 다시 세게 만든다. 이미 슈퍼아머 상태였다면 즉시 풀어준다.
        // 이 스킬이 끝나고 호출이 멈추면, 그 시점부터 다시 정상적으로 10초 주기가 흐른다.
        public void ResetSuperArmorTimer()
        {
            if (monster.IsDead || !staggerOnHit) return;

            if (superArmorCycleRoutine != null) StopCoroutine(superArmorCycleRoutine);

            if (isSuperArmor)
            {
                isSuperArmor = false;
                spriteRenderer.color = normalColor;
            }

            superArmorCycleRoutine = StartCoroutine(SuperArmorCycleRoutine());
        }

        private void OnDestroy()
        {
            if (monster != null)
            {
                monster.OnDamaged -= HandleDamaged;
                monster.OnDied -= HandleDied;
            }
        }

        // 등장 연출이 시작될 때 호출한다. 슈퍼아머 주기도 전투가 시작될 때까지 멈춰 둔다(대화 중에 몸이 번쩍이지 않게).
        public void HoldForIntro()
        {
            isHoldingForIntro = true;
            if (superArmorCycleRoutine != null)
            {
                StopCoroutine(superArmorCycleRoutine);
                superArmorCycleRoutine = null;
            }
        }

        // 등장 대사가 끝나면 호출한다. 맵 반대편에 있는 플레이어에게 거리 제한 없이 점프 내려찍기로 뛰어든 뒤
        // 평소 AI로 돌아간다. 플레이어가 서 있던 자리에 내려찍지만 피해는 주지 않는다(연출에서 플레이어가 대시로 피한다).
        // onLanded는 착지하는 순간 호출된다.
        public void BeginFightWithJump(float duration, System.Action onLanded)
        {
            isHoldingForIntro = false;
            if (staggerOnHit && superArmorCycleRoutine == null) superArmorCycleRoutine = StartCoroutine(SuperArmorCycleRoutine());
            if (!CanJumpAttack)
            {
                onLanded?.Invoke();
                return;
            }
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            patternRoutine = StartCoroutine(RunJumpPattern(float.MaxValue, duration, onLanded, introJump: true));
        }

        private static Sprite[] LoadSorted(string path)
        {
            var loaded = Resources.LoadAll<Sprite>(path);
            System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
            return loaded;
        }

        private void Update()
        {
            UpdateEyeSilk();
            if (jumpCooldownTimer > 0f && !isAttacking) jumpCooldownTimer -= Time.deltaTime;
            if (waveCooldownTimer > 0f && !isAttacking) waveCooldownTimer -= Time.deltaTime;
        }

        private void FixedUpdate()
        {
            if (isHoldingForIntro || monster.IsMinigameOpponent || monster.IsDead || isStaggered || monster.IsImmobilized || target == null || target.IsDead)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            // 패턴이 이미 진행 중이면 사거리를 다시 확인하지 않는다. 즉, 한 번 시작된 공격은
            // 플레이어가 도중에 사거리 밖으로 나가도 끝까지(RunNextPattern이 끝날 때까지) 재생된다.
            // (맞아서 경직되는 것과는 별개 - 피격 중단은 HandleDamaged에서만 일어난다.)
            if (isAttacking)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            if (enrageQueued && !enraged)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                patternRoutine = StartCoroutine(EnrageRoutine());
                return;
            }

            float distance = target.transform.position.x - transform.position.x;
            // 플레이어가 높은 발판 위(칼이 닿지 않는 곳)에 있으면, 근접 거리여도 대각선 검기로 노린다.
            if (PlayerIsAbove() && waveCooldownTimer <= 0f && attackAnimator != null && attackAnimator.HasFrames)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                patternRoutine = StartCoroutine(RunSwordWavePattern());
            }
            else if (Mathf.Abs(distance) <= attackRange)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                patternRoutine = StartCoroutine(RunNextPattern());
            }
            else if (Mathf.Abs(distance) >= jumpMinRange && jumpCooldownTimer <= 0f && CanJumpAttack)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                patternRoutine = StartCoroutine(RunJumpPattern(jumpMaxDistance, jumpDuration, null));
            }
            else if (Mathf.Abs(distance) <= waveMaxRange && waveCooldownTimer <= 0f && attackAnimator != null && attackAnimator.HasFrames)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                patternRoutine = StartCoroutine(RunSwordWavePattern());
            }
            else
            {
                float direction = Mathf.Sign(distance);
                body.linearVelocity = new Vector2(direction * moveSpeed, body.linearVelocity.y);
                spriteRenderer.flipX = direction < 0f;
            }
        }

        // 맞으면 경직한다 (패턴 시전 중이었다면 그 패턴도 즉시 끊긴다).
        // W처럼 짧은 간격으로 계속 맞으면, 매 타격마다 경직이 새로 갱신되어 계속 경직 상태로 유지된다.
        // 슈퍼아머 상태일 때는 경직 없이 데미지만 받는다.
        private void HandleDamaged(Monster hitMonster, float damage)
        {
            if (monster.IsDead) return;
            if (!enraged && !enrageQueued && !monster.IsMinigameOpponent && monster.CurrentHp <= monster.MaxHp * enrageHealthFraction)
            {
                enrageQueued = true;
                monster.SurviveLethalHit = true; // 분노 전환 전에 쓰러지지 않게
            }
            if (!staggerOnHit)
            {
                if (hitFlashRoutine != null) StopCoroutine(hitFlashRoutine);
                hitFlashRoutine = StartCoroutine(HitFlashRoutine());
                return;
            }
            if (isSuperArmor) return;

            if (isAttacking)
            {
                InterruptPattern();
            }
            else
            {
                RefreshStagger();
            }
        }

        private IEnumerator HitFlashRoutine()
        {
            spriteRenderer.color = hitFlashColor;
            yield return new WaitForSeconds(hitFlashDuration);
            spriteRenderer.color = normalColor;
            hitFlashRoutine = null;
        }

        private void InterruptPattern()
        {
            if (patternRoutine != null)
            {
                StopCoroutine(patternRoutine);
                patternRoutine = null;
            }
            isAttacking = false;
            if (attackAnimator != null) attackAnimator.Stop();
            RefreshStagger();
        }

        private void RefreshStagger()
        {
            if (staggerRoutine != null) StopCoroutine(staggerRoutine);
            staggerRoutine = StartCoroutine(StaggerRoutine());
        }

        private IEnumerator StaggerRoutine()
        {
            isStaggered = true;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

            yield return new WaitForSeconds(staggerFlinchDuration);

            isStaggered = false;
            staggerRoutine = null;
        }

        // 맞았는지 여부와 상관없이, 일정 주기(superArmorInterval)마다 계속 슈퍼아머를 부여한다.
        private IEnumerator SuperArmorCycleRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(superArmorInterval);

                isSuperArmor = true;
                isStaggered = false; // 경직 중이었다면 즉시 풀고 슈퍼아머로 전환
                spriteRenderer.color = superArmorColor;

                yield return new WaitForSeconds(superArmorDuration);

                isSuperArmor = false;
                spriteRenderer.color = normalColor;
            }
        }

        // 두 패턴을 번갈아 시전한다 (패턴 수는 적지만, 매번 3연타 콤보라 체감 공격 횟수는 많다).
        private IEnumerator RunNextPattern()
        {
            isAttacking = true;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

            bool useDash = nextIsDash;
            nextIsDash = !nextIsDash;

            if (attackAnimator != null)
            {
                float patternDuration = useDash
                    ? dashComboHits * dashHitInterval
                    : sweepComboHits * sweepHitInterval;
                attackAnimator.Play(patternDuration);
            }

            yield return useDash ? DashComboRoutine() : SweepComboRoutine();
            yield return new WaitForSeconds(patternCooldown);

            isAttacking = false;
            patternRoutine = null;
        }

        private IEnumerator DashComboRoutine()
        {
            for (int i = 0; i < dashComboHits; i++)
            {
                if (monster.IsDead) yield break;

                Vector2 direction = FacingToTarget();
                spriteRenderer.flipX = direction.x < 0f;
                body.position += direction * dashStepDistance;

                DealDamageAt((Vector2)transform.position + direction * 0.6f, dashHitRadius, dashDamageMultiplier);
                if (i == dashComboHits - 1) FireEnragedWaves();

                yield return new WaitForSeconds(dashHitInterval);
            }
        }

        private IEnumerator SweepComboRoutine()
        {
            for (int i = 0; i < sweepComboHits; i++)
            {
                if (monster.IsDead) yield break;

                bool isFinisher = i == sweepComboHits - 1;
                float multiplier = isFinisher ? sweepFinisherMultiplier : sweepDamageMultiplier;
                DealDamageAt(transform.position, sweepRadius, multiplier);
                if (isFinisher) FireEnragedWaves();

                yield return new WaitForSeconds(sweepHitInterval);
            }
        }

        // 검기: 플레이어 쪽을 보고 검을 크게 한 번 휘두르면(기본 공격 모션) 앞쪽 바닥에서 보랏빛 칼날이 솟아 땅을 타고 날아간다.
        private IEnumerator RunSwordWavePattern()
        {
            isAttacking = true;
            waveCooldownTimer = waveCooldown;
            Vector2 direction = FacingToTarget();
            spriteRenderer.flipX = direction.x < 0f;
            attackAnimator.Play(waveSwingDuration);

            yield return new WaitForSeconds(waveSwingDuration * waveReleaseTiming);
            if (monster.IsDead) yield break;
            var bodyCollider = GetComponent<Collider2D>();
            float groundY = bodyCollider != null ? bodyCollider.bounds.min.y : transform.position.y;
            if (PlayerIsAbove()) LaunchAimedWave(waveDamageMultiplier);
            else BossSwordWave.Launch(new Vector2(transform.position.x + direction.x * waveForward, groundY), direction.x,
                monster.Attack * waveDamageMultiplier, waveSpeed, waveTravelRange, waveScale);
            // 분노 중이면 등 뒤로도 한 줄기 더 날린다(앞쪽은 이미 날아가고 있다).
            if (IsEnraged) LaunchWave(-direction.x, enrageWaveDamageMultiplier);

            yield return new WaitForSeconds(waveSwingDuration * (1f - waveReleaseTiming));
            yield return new WaitForSeconds(patternCooldown);
            isAttacking = false;
            patternRoutine = null;
        }

        // 분노한 뒤로는 모든 공격의 결정타(콤보 마지막 타/점프 착지/검기)에 양옆으로 검기가 함께 날아간다.
        private bool IsEnraged => enraged && !monster.IsDead;

        // 분노 전환: 플레이어 조작을 잠깐 막고 → 땅에 검을 꽂고 무릎 꿇은 채(Plant) 대사 → 검을 하늘로 치켜드는 순간(SpecialRaise)
        // 온몸이 검게 물들고 눈에서 붉은 실크가 흩날리며 화면이 흔들린다 → 빨라진 패턴으로 다시 싸운다.
        private IEnumerator EnrageRoutine()
        {
            isAttacking = true;
            var playerMovement = target != null ? target.GetComponent<CharacterMovement2D>() : null;
            if (playerMovement != null) playerMovement.SetCutsceneLocked(true);
            spriteRenderer.flipX = FacingToTarget().x < 0f;

            if (attackAnimator.HasClip(PlantClip))
            {
                attackAnimator.PlayClip(PlantClip, plantDuration);
                yield return new WaitForSeconds(plantDuration);
                attackAnimator.PlayClipLoop(PlantHoldClip, 1f);
            }

            if (EnrageTalk != null) yield return EnrageTalk();
            else yield return new WaitForSeconds(0.8f);

            if (attackAnimator.HasClip(RaiseClip))
            {
                attackAnimator.PlayClip(RaiseClip, raiseDuration);
                yield return new WaitForSeconds(raiseDuration * 0.75f);
            }

            // 검을 치켜든 순간: 하얗게 번쩍였다가 검게 물들고, 눈에 붉은 실크가 피어난다.
            enraged = true;
            enrageQueued = false;
            normalColor = enragedColor;
            ShowEyeSilk();
            var cam = Camera.main;
            var cameraFollow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            if (cameraFollow != null) cameraFollow.Shake(0.5f, 0.35f);
            for (float t = 0f; t < raiseHold; t += Time.deltaTime)
            {
                spriteRenderer.color = Color.Lerp(Color.white, enragedColor, t / (raiseHold * 0.6f));
                yield return null;
            }
            spriteRenderer.color = enragedColor;
            attackAnimator.Stop();

            moveSpeed *= enragedSpeedScale;
            patternCooldown *= enragedCooldownScale;
            jumpCooldown *= enragedCooldownScale;
            waveCooldown *= enragedCooldownScale;
            jumpCooldownTimer = 0.6f;
            waveCooldownTimer = 0.3f;
            monster.SurviveLethalHit = false;
            if (playerMovement != null) playerMovement.SetCutsceneLocked(false);
            OnEnraged?.Invoke();

            isAttacking = false;
            patternRoutine = null;
        }

        // 쓰러지면 눈의 붉은 실크를 걷어 낸다(처치 연출 동안 남아 있지 않게).
        private void HandleDied(Monster deadMonster)
        {
            if (eyeSilk != null) Destroy(eyeSilk.gameObject);
            eyeSilk = null;
        }

        private void ShowEyeSilk()
        {
            silkFrames = LoadSorted(EyeSilkPath);
            if (silkFrames.Length == 0) return;
            var go = new GameObject("EyeSilk");
            go.transform.SetParent(transform, false);
            eyeSilk = go.AddComponent<SpriteRenderer>();
            eyeSilk.sprite = silkFrames[0];
            eyeSilk.sortingOrder = spriteRenderer.sortingOrder + 1;
            UpdateEyeSilk();
        }

        // 실크는 눈에서 등 뒤로 흩날린다: 오른쪽을 보면 눈 자리에서 왼쪽으로, 왼쪽을 보면 뒤집어서 오른쪽으로.
        // 걷기/공격 모션마다 머리 높이가 조금씩 달라도 크게 어긋나지 않게 대기 모습 기준 눈 자리에 붙이고 살짝 일렁이게 한다.
        private void UpdateEyeSilk()
        {
            if (eyeSilk == null) return;
            bool facingLeft = spriteRenderer.flipX;
            eyeSilk.enabled = spriteRenderer.enabled && !monster.IsDead;
            // 실크는 왼쪽 끝(피벗)에서 오른쪽으로 그려져 있다. 왼쪽을 볼 때는 그대로 두면 등 뒤(오른쪽)로 흩날리고,
            // 오른쪽을 볼 때는 피벗을 축으로 뒤집어 왼쪽(등 뒤)으로 흩날리게 한다(flipX는 그림 가운데 기준이라 배율을 쓴다).
            float front = facingLeft ? -1f : 1f;
            eyeSilk.transform.localScale = new Vector3(-front, 1f, 1f);
            float bob = Mathf.Sin(Time.time * 6f) * 0.03f;
            eyeSilk.transform.localPosition = new Vector3(front * eyeOffset.x, eyeOffset.y + bob, 0f);

            silkTimer += Time.deltaTime;
            if (silkTimer < silkFrameDuration) return;
            silkTimer -= silkFrameDuration;
            silkIndex = (silkIndex + 1) % silkFrames.Length;
            eyeSilk.sprite = silkFrames[silkIndex];
        }

        private void FireEnragedWaves()
        {
            if (!IsEnraged) return;
            LaunchWave(-1f, enrageWaveDamageMultiplier);
            LaunchWave(1f, enrageWaveDamageMultiplier);
            // 높은 발판 위로 피해 있으면 그쪽으로도 한 줄기.
            if (PlayerIsAbove()) LaunchAimedWave(enrageWaveDamageMultiplier);
        }

        // 플레이어가 보스 칼이 닿지 않는 높이(발판 위)에 있는가.
        private bool PlayerIsAbove()
        {
            if (target == null || target.IsDead) return false;
            var bodyCollider = GetComponent<Collider2D>();
            float groundY = bodyCollider != null ? bodyCollider.bounds.min.y : transform.position.y;
            return target.transform.position.y - groundY >= aboveThreshold;
        }

        // 대각선 검기: 검을 휘두른 높이(가슴께)에서 플레이어 몸 가운데를 향해 곧게 날린다.
        private void LaunchAimedWave(float damageMultiplier)
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(FacingToTarget().x * waveForward, aimedWaveHeight);
            Vector2 aim = (Vector2)target.transform.position + Vector2.up * 0.75f - origin;
            BossSwordWave.LaunchAimed(origin, aim, monster.Attack * damageMultiplier, waveSpeed, waveTravelRange, waveScale * 0.85f);
        }

        private void LaunchWave(float direction, float damageMultiplier)
        {
            var bodyCollider = GetComponent<Collider2D>();
            float groundY = bodyCollider != null ? bodyCollider.bounds.min.y : transform.position.y;
            BossSwordWave.Launch(new Vector2(transform.position.x + direction * waveForward, groundY), direction,
                monster.Attack * damageMultiplier, waveSpeed, waveTravelRange, waveScale);
        }

        private bool CanJumpAttack => attackAnimator != null && attackAnimator.HasClip(JumpAttackClip);

        public const string JumpAttackClip = "JumpAttack";

        // 점프 공격 8프레임 기준 타이밍: 1 도약 준비 → 2~5 공중(이 동안 착지 지점까지 이동)
        // → 6 내려찍기 충격(이때 피해) → 7~8 착지 후 자세 회복.
        public const float JumpTakeoffFraction = 1f / 8f;
        public const float JumpImpactFraction = 5f / 8f;

        // introJump면 플레이어가 서 있던 자리로 날아들되 피해는 주지 않고, 착지하자마자 전투가 시작된다.
        // (등장 연출이 공중에 있는 동안 플레이어를 대시로 보스 밑을 빠져나가게 해서 피하는 장면을 만든다.)
        private IEnumerator RunJumpPattern(float maxDistance, float duration, System.Action onLanded, bool introJump = false)
        {
            isAttacking = true;
            jumpCooldownTimer = jumpCooldown;

            Vector2 direction = FacingToTarget();
            spriteRenderer.flipX = direction.x < 0f;

            // 도약하는 순간의 플레이어 위치로 착지 지점을 정한다(공중에서 따라가지 않음 → 피할 여지가 있다).
            float startX = body.position.x;
            float targetX = target != null ? target.transform.position.x : startX;
            float landingX = Mathf.Clamp(targetX, startX - maxDistance, startX + maxDistance);
            Rect bounds = GameBootstrap.CurrentMapBounds;
            landingX = Mathf.Clamp(landingX, bounds.xMin + 1f, bounds.xMax - 1f);

            attackAnimator.PlayClip(JumpAttackClip, duration);

            yield return new WaitForSeconds(duration * JumpTakeoffFraction);

            float airTime = duration * (JumpImpactFraction - JumpTakeoffFraction);
            float elapsed = 0f;
            while (elapsed < airTime)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / airTime);
                body.position = new Vector2(Mathf.Lerp(startX, landingX, t), body.position.y);
            }

            if (monster.IsDead) yield break;

            if (introJump)
            {
                // 착지 모션(7~8프레임)과 패턴 쿨다운을 기다리지 않고, 닿자마자 전투를 시작한다.
                attackAnimator.Stop();
                isAttacking = false;
                patternRoutine = null;
                onLanded?.Invoke();
                yield break;
            }

            DealDamageAt(transform.position, jumpSlamRadius, jumpDamageMultiplier);
            FireEnragedWaves();

            yield return new WaitForSeconds(duration * (1f - JumpImpactFraction));
            yield return new WaitForSeconds(patternCooldown);

            isAttacking = false;
            patternRoutine = null;
        }

        private Vector2 FacingToTarget()
        {
            if (target == null) return spriteRenderer.flipX ? Vector2.left : Vector2.right;
            float dx = target.transform.position.x - transform.position.x;
            return dx < 0f ? Vector2.left : Vector2.right;
        }

        private void DealDamageAt(Vector2 origin, float radius, float multiplier)
        {
            var hits = Physics2D.OverlapCircleAll(origin, radius);
            foreach (var hit in hits)
            {
                var character = hit.GetComponent<Character>();
                if (character == null || character.IsDead) continue;

                float damage = CombatMath.Finish(CombatMath.BaseDamage(monster.Attack, character.Stats.defense) * multiplier);
                character.TakeDamage(damage, origin);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, sweepRadius);
            Gizmos.color = new Color(0.6f, 0.2f, 1f);
            Gizmos.DrawWireSphere(transform.position, jumpMinRange);
        }
    }
}

using System.Collections;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.UI;

namespace Aethoria.Monsters
{
    // 절망의 언덕의 최종보스 루나벨. 걷지 않고 언덕 끝에 서서 플레이어 쪽을 바라본다.
    // 플레이어가 중거리에 있으면 주기적으로 손을 뻗어(Attack 8프레임) 피의 손(BloodHandProjectile)을 날리고,
    // 몸에 닿으면 리듬전투로 넘긴다. 등장 대사(LunabelIntroCutscene) 동안은 아무것도 하지 않는다.
    [RequireComponent(typeof(Monster))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class FinalBossAI : MonoBehaviour
    {
        [SerializeField] private float gravityScale = 4f;
        [SerializeField] private float contactRange = 1.6f;

        [Header("피의 손")]
        [SerializeField] private float castRange = 10f;
        [SerializeField] private float castCooldown = 4f;
        [SerializeField] private float castAnimDuration = 1.0f; // 손 뻗기 8프레임 전체 길이
        [SerializeField, Range(0f, 1f)] private float castFireTiming = 4f / 8f; // 5번째 프레임(손끝에서 마법이 터짐)
        [SerializeField] private float handDamageMultiplier = 1.4f;
        [SerializeField] private float handSpeed = 8f;
        [SerializeField] private float handTravelRange = 14f;
        [SerializeField] private float handHitRadius = 0.7f;
        [SerializeField] private float handMuzzleForward = 1.0f; // 몸 중심에서 뻗은 손끝까지 앞쪽 거리
        [SerializeField] private float handMuzzleHeight = 0.8f;  // 피의 손 그림 아래쪽이 놓이는 높이

        private Monster monster;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private MonsterAttackSpriteAnimator attackAnimator;
        private Character target;
        private RhythmBattleUI rhythmBattleUI;

        // 2페이즈: 1페이즈 체력을 다 깎은 뒤의 리듬전투 적 턴에 루나벨이 변신한다(RhythmBattleUI, Phase2 9프레임).
        // 1페이즈 동안은 쓰러질 피해를 받아도 체력 1로 버티고(SurviveLethalHit), 그렇게 체력이 바닥나면 변신한다.
        // 변신하면 체력을 모두 채우고, 그 뒤로는 공중에 뜬 2페이즈 대기 모션(Phase2Idle 5프레임)으로 서 있다. 한 번 변신하면
        // 흡혈로 체력이 다시 차도 2페이즈가 유지되고, 필드에서도 피의 손을 더 자주 날린다.
        private const float PhaseTwoHealthThreshold = 1f;
        public const string PhaseTwoPath = "Art/Monsters/FinalBoss/Phase2";
        public const string PhaseTwoIdlePath = "Art/Monsters/FinalBoss/Phase2Idle";
        public const string IdlePath = "Art/Monsters/FinalBoss/Idle";
        // 첫 조우 연출(LunabelIntroCutscene) 동안 의자에 앉아 있는 모습(4프레임 반복).
        public const string SeatedPath = "Art/Monsters/FinalBoss/Seated";
        // 2페이즈 기본 공격(손끝에서 구체를 쏘는 6프레임). 변신하면 필드/리듬전투 모두 이 모션으로 피의 손을 날린다.
        public const string PhaseTwoAttackPath = "Art/Monsters/FinalBoss/Phase2Attack";
        // 1페이즈 혈류 흡수(8프레임). 흡수해 체력을 채우면 피가 들끓는 상태(BloodCharged)가 되고, 그 뒤로 루나벨의
        // 노드를 일정 개수(BloodMisses가 문턱에 닿을 때까지) 놓치면 피의 회오리(Lubel_Skill1 → Effects/LunabelBloodVortex)가 터진다.
        public const string AbsorbPath = "Art/Monsters/FinalBoss/Absorb";
        public const string BloodVortexPath = "Art/Effects/LunabelBloodVortex";
        public bool BloodCharged { get; set; }
        public int BloodMisses { get; set; }
        public const string HitPath = "Art/Monsters/FinalBoss/Hit";
        [SerializeField] private float phaseTwoCooldownScale = 0.6f;
        public bool PhaseTwoStarted { get; private set; }

        // 변신 연출이 끝나는 순간 리듬전투가 부른다: 체력을 모두 채우고 대기 모습을 변신한 모습으로 바꾼다.
        public void EnterPhaseTwo(Sprite[] phaseTwoIdleFrames)
        {
            PhaseTwoStarted = true;
            monster.SurviveLethalHit = false;
            monster.Heal(monster.MaxHp);
            var walk = GetComponent<MonsterWalkSpriteAnimator>();
            if (walk != null && phaseTwoIdleFrames != null && phaseTwoIdleFrames.Length > 0)
                walk.OverrideFrames(phaseTwoIdleFrames, phaseTwoIdleFrames);
            BloodCharged = false;
            BloodMisses = 0;
            if (attackAnimator != null && Resources.LoadAll<Sprite>(PhaseTwoAttackPath).Length > 0)
                attackAnimator.Configure(PhaseTwoAttackPath);
        }
        // 변신 전에는 한 방에 쓰러질 피해를 받아도 체력 1로 버티고, 그렇게 체력이 바닥난 다음 적 턴에 곧바로 변신한다.
        public bool ShouldStartPhaseTwo => !PhaseTwoStarted && !monster.IsDead
            && monster.CurrentHp <= PhaseTwoHealthThreshold;

        // 2페이즈 필살기(리듬전투의 적 턴에서 발동): 2페이즈 체력이 각 문턱(60%, 25%) 이하로 처음 내려간 뒤의 적 턴에 한 번씩 쓴다.
        // 하늘로 손을 뻗고(Special 8프레임 한 번) → 핏빛 구체를 모으다가(Charge 8프레임 반복) → 구체(Effects/BloodOrb)를 던진다.
        private static readonly float[] SpecialMoveHealthFractions = { 0.6f, 0.25f };
        public const string SpecialPath = "Art/Monsters/FinalBoss/Special";
        public const string SpecialChargePath = "Art/Monsters/FinalBoss/Charge";
        public const string BloodOrbPath = "Art/Effects/BloodOrb";
        // 노드를 다 못 치면: 구체 던지기(Throw 5프레임) → 플레이어 발밑에서 피의 기둥이 솟구쳐(BloodPillarRise) 회오리친다(BloodPillarTornado).
        public const string ThrowPath = "Art/Monsters/FinalBoss/Throw";
        public const string BloodPillarRisePath = "Art/Effects/BloodPillarRise";
        public const string BloodPillarTornadoPath = "Art/Effects/BloodPillarTornado";
        // 지금까지 쓴 필살기 횟수(0부터). 두 번째 필살기는 더 어려운 채보로 나온다.
        public int SpecialMovesUsed { get; private set; }
        public bool ShouldUseSpecialMove => PhaseTwoStarted && !monster.IsDead && SpecialMovesUsed < CrossedSpecialThresholds();

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

        private bool isHoldingForIntro;
        private bool isCasting;
        private float castTimer;

        private void Awake()
        {
            monster = GetComponent<Monster>();
            monster.SurviveLethalHit = true;
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            attackAnimator = GetComponent<MonsterAttackSpriteAnimator>();
            body.gravityScale = gravityScale;
            body.freezeRotation = true;
            castTimer = castCooldown * 0.5f; // 마주치자마자 바로 쏘지 않도록 조금 늦춘다

            target = Object.FindFirstObjectByType<Character>();
            rhythmBattleUI = Object.FindFirstObjectByType<RhythmBattleUI>();
        }

        // 등장 대사가 흐르는 동안 공격/접촉 전투를 멈춘다. 대사가 끝나면 EndIntro로 풀어 준다.
        public void HoldForIntro() => isHoldingForIntro = true;
        public void EndIntro() => isHoldingForIntro = false;

        private void Update()
        {
            if (monster.IsDead || target == null || target.IsDead) return;

            Vector2 offset = target.transform.position - transform.position;
            if (!isCasting) spriteRenderer.flipX = offset.x < 0f;
            if (isHoldingForIntro) return;

            if (castTimer > 0f && !isCasting) castTimer -= Time.deltaTime;

            if (rhythmBattleUI != null && Mathf.Abs(offset.x) <= contactRange && offset.y <= 2f && offset.y >= -0.5f)
            {
                rhythmBattleUI.TryBegin(monster, target);
                return;
            }

            if (!isCasting && castTimer <= 0f && Mathf.Abs(offset.x) <= castRange && !monster.IsImmobilized
                && attackAnimator != null && attackAnimator.HasFrames
                && (rhythmBattleUI == null || !rhythmBattleUI.IsOpen))
            {
                StartCoroutine(CastRoutine());
            }
        }

        private void FixedUpdate()
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }

        private IEnumerator CastRoutine()
        {
            isCasting = true;
            castTimer = castCooldown * (PhaseTwoStarted ? phaseTwoCooldownScale : 1f);

            float facing = spriteRenderer.flipX ? -1f : 1f;
            attackAnimator.Play(castAnimDuration);

            yield return new WaitForSeconds(castAnimDuration * castFireTiming);
            if (!monster.IsDead)
            {
                Vector2 muzzle = (Vector2)transform.position + new Vector2(facing * handMuzzleForward, handMuzzleHeight);
                BloodHandProjectile.Launch(muzzle, new Vector2(facing, 0f), monster.Attack * handDamageMultiplier,
                    handSpeed, handTravelRange, handHitRadius);
            }

            yield return new WaitForSeconds(castAnimDuration * (1f - castFireTiming));
            isCasting = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, contactRange);
            Gizmos.color = new Color(0.8f, 0.1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, castRange);
        }
    }
}

using System.Collections;
using UnityEngine;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 절망의 언덕의 최종보스 루나벨. 걷지 않고 언덕 끝에 서서 플레이어 쪽을 바라본다.
    // 플레이어가 중거리에 있으면 주기적으로 손을 뻗어(Attack 8프레임) 피의 손(BloodHandProjectile)을 날리고,
    // 몸에 닿아 있으면 접촉 피해를 준다. 등장 대사(LunabelIntroCutscene) 동안은 아무것도 하지 않는다.
    // 1페이즈 체력을 다 깎으면 그 자리에서 변신(Phase2 9프레임)해 체력을 모두 채우고 2페이즈로 싸운다.
    // 그 밖의 패턴: 피의 기둥 추격(플레이어 발밑에 기둥이 세 번 연달아 솟는다), 피의 소용돌이(가까이 붙으면 혈류를 흡수해
    // 몸 주위로 소용돌이를 터뜨리고 맞힌 만큼 회복), 2페이즈 전용 피의 회오리(땅을 타고 플레이어 쪽으로 밀려오는 회오리).
    // 걸어 다니지 않는 대신, 플레이어가 멀리 떨어지면 붉은 차원문으로 빨려 들어가 플레이어 근처의 차원문에서 다시 나타난다.
    [RequireComponent(typeof(Monster))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class FinalBossAI : MonoBehaviour
    {
        [SerializeField] private float gravityScale = 4f;
        [SerializeField] private float contactRange = 1.0f; // 몸통(폭 1.3)에 실제로 닿을 만큼만(예전 1.6은 떨어져 있어도 맞았다)
        [SerializeField] private float contactInterval = 1f;
        [SerializeField] private float contactDamageMultiplier = 1f;
        [SerializeField] private float transformDuration = 1.35f; // 변신 9프레임 전체 길이
        [SerializeField] private float transformHold = 0.4f;      // 변신을 마친 모습으로 잠깐 멈춰 있는 시간

        [Header("피의 손")]
        [SerializeField] private float castRange = 10f;
        [SerializeField] private float castCooldown = 4f;
        [SerializeField] private float castAnimDuration = 1.0f; // 손 뻗기 8프레임 전체 길이
        [SerializeField, Range(0f, 1f)] private float castFireTiming = 4f / 8f; // 5번째 프레임(손끝에서 마법이 터짐)
        // 루나벨이 쏘는 공격은 모두 피격 무적 없이 닿아 있는 동안 짧은 간격으로 계속 맞는 연타다.
        // 한 번의 피해는 예전 한 방 피해를 연타 수로 나눈 만큼(총 피해는 비슷하게).
        [SerializeField] private float handDamageMultiplier = 0.5f;   // 몸을 꿰뚫고 지나가며 handHitInterval마다(대략 3타)
        [SerializeField] private float handHitInterval = 0.08f;
        [SerializeField] private float handSpeed = 8f;
        [SerializeField] private float handTravelRange = 14f;
        [SerializeField] private float handHitRadius = 0.5f;
        [SerializeField] private float handMuzzleForward = 1.0f; // 몸 중심에서 뻗은 손끝까지 앞쪽 거리
        [SerializeField] private float handMuzzleHeight = 0.8f;  // 피의 손 그림 아래쪽이 놓이는 높이

        [Header("필살기 (2페이즈)")]
        [SerializeField] private float specialRaiseDuration = 1.0f;  // 하늘로 손 뻗기(Special 8프레임 한 번)
        [SerializeField] private float specialChargeDuration = 1.6f; // 구체 모으기(Charge 8프레임 반복)
        [SerializeField] private float specialChargeCycle = 0.8f;
        [SerializeField] private float specialThrowDuration = 0.6f;  // 던지기(Throw 5프레임)
        [SerializeField, Range(0f, 1f)] private float specialReleaseTiming = 3f / 5f; // 4번째 프레임(팔을 앞으로 뻗음)에서 놓는다
        [SerializeField] private float specialOrbScale = 0.32f;      // 예전 핏빛 구체(BloodOrb) 배율 — 새 구체 그림이 없을 때만
        [SerializeField] private float specialGiantOrbScale = 0.4f;  // 진홍빛 에너지 구체(LunabelGiantOrb) 배율: 구슬 지름 약 0.9유닛
        [SerializeField] private float specialOrbFlightTime = 0.25f;
        [SerializeField] private float specialLandForward = 2.2f;    // 구체가 떨어지는 곳(몸 앞쪽 거리)
        [SerializeField] private int specialBurstCount = 6;          // 앞으로 번져 가는 폭발 수
        [SerializeField] private float specialBurstSpacing = 1.4f;
        [SerializeField] private float specialBurstInterval = 0.12f;
        [SerializeField] private float specialBurstWidth = 1.0f;
        [SerializeField] private float specialBurstHeight = 2.2f;
        [SerializeField] private float specialDamageMultiplier = 0.5f; // 기둥 하나가 솟는 동안 specialHitInterval마다
        [SerializeField] private float specialHitInterval = 0.08f;
        [SerializeField] private float specialBurstHitTime = 0.36f;    // 기둥 하나가 맞히는 시간(솟는 6프레임)

        [Header("2페이즈 개막: 공중에서 거대한 구체 → 플레이어를 빨아들임 → 맵 가운데 거대한 핏기둥")]
        [SerializeField] private float openingAirHeight = 3.5f;      // 맵 가운데 이만큼 위 공중으로 순간이동한다
        [SerializeField] private float openingOrbScale = 0.9f;       // 다 모았을 때 거대한 에너지 구체(LunabelGiantOrb, 구슬 지름 2.2) 배율
        [SerializeField] private float openingOrbFrameTime = 0.08f;  // 에너지 구체 6프레임이 도는 빠르기
        // 2페이즈 전투 중에도 간간히 개막 패턴(공중으로 올라가 거대한 구체 → 플레이어를 빨아들임 → 거대한 핏기둥)을 다시 쓴다.
        // 이때는 광기 어린 웃음 대사 없이 구체만 모은다.
        [SerializeField] private float giantOrbCooldown = 26f;
        [SerializeField] private float giantOrbFirstDelay = 20f;     // 변신 개막이 끝난 뒤 처음 다시 쓰기까지
        [SerializeField] private float openingChargeDuration = 3f;   // 구체를 모으는 동안 플레이어가 가운데로 빨려 든다
        [SerializeField] private float openingOrbDropTime = 0.35f;   // 구체가 바로 아래 맵 가운데 바닥으로 내리꽂힌다
        [SerializeField] private float openingWarnTime = 0.5f;       // 떨어진 자리 바닥이 핏빛으로 들끓는 예고
        [SerializeField] private float openingPillarScale = 3f;      // 피의 기둥 그림(약 2.2칸 폭)을 이만큼 키운다
        [SerializeField] private float openingPillarHitWidth = 4.2f;
        [SerializeField] private float openingTornadoTime = 2f;      // 다 솟은 뒤 회오리치는 시간(그동안 계속 할퀸다)
        [SerializeField] private float openingHitInterval = 0.125f;  // 기둥 안에 있으면 이 간격으로 연타
        [SerializeField] private float openingDamageMultiplier = 0.35f; // 연타 한 번의 피해(초당 8번 들어가므로 작게)
        // 빨아들이는 힘(가로 속도). 걷기(4)보다 빨라 걸어서는 못 빠져나가고, 대시(1.5초마다 3칸, 대시 중엔 끌리지 않음)를
        // 쉬지 않고 써야 1.5초에 약 1칸씩 겨우 빠져나간다: 3 + (4 - 5.5) x 1.3 ≈ 1.05.
        [SerializeField] private float pullStartSpeed = 2f;
        [SerializeField] private float pullMaxSpeed = 5.5f;
        [SerializeField] private float pullRampTime = 1.5f;
        [SerializeField] private float openingCameraSize = 5.2f;     // 공중의 루나벨과 플레이어가 한 화면에 들어오게 넓힌다(기본 4)
        // 공중에서 변신을 마친 순간의 핏빛 충격파(모래룡 포효와 같은 연출).
        [SerializeField] private float awakeningShockwaveSize = 13f; // 고리 지름(유닛) — 넓힌 화면을 가로지를 만큼
        [SerializeField] private float awakeningShakeDuration = 1.4f;
        [SerializeField] private float awakeningShakeMagnitude = 0.5f;

        [Header("피의 기둥 추격")]
        [SerializeField] private float chaseRange = 12f;
        [SerializeField] private float chaseCooldown = 7f;
        [SerializeField] private float chaseFirstDelay = 3f;
        [SerializeField] private int chaseCount = 3;              // 플레이어 발밑에 연달아 솟는 기둥 수
        [SerializeField] private float chaseInterval = 0.55f;     // 기둥 사이 간격(매번 그때 플레이어 자리를 다시 노린다)
        [SerializeField] private float chaseWarnTime = 0.5f;      // 바닥이 핏빛으로 일렁이는 예고 시간
        [SerializeField] private float chaseHitWidth = 1.0f;
        [SerializeField] private float chaseHitHeight = 2.4f;
        [SerializeField] private float chaseDamageMultiplier = 0.35f; // 기둥이 솟는 프레임마다 한 번씩(4타)

        [Header("피의 소용돌이 (근접)")]
        [SerializeField] private float vortexTriggerRange = 3f;   // 플레이어가 이만큼 붙어 있으면 쓴다
        [SerializeField] private float vortexCooldown = 6f;
        [SerializeField] private float vortexAbsorbDuration = 1.1f; // 혈류 흡수(Absorb 8프레임)
        [SerializeField] private float vortexHeight = 3.4f;        // 양옆으로 날아가는 소용돌이 키
        [SerializeField] private float vortexHitWidth = 1.2f;
        [SerializeField] private float vortexSpeed = 5f;
        [SerializeField] private float vortexLifetime = 2.6f;
        [SerializeField] private float vortexHitInterval = 0.15f;
        [SerializeField] private float vortexDamageMultiplier = 0.36f;
        [SerializeField] private float vortexSpawnOffset = 0.8f;   // 몸 양옆 이만큼에서 솟아 바깥으로 날아간다

        [Header("피의 폭풍 (2페이즈, 혈류 흡수 대신)")]
        // 2페이즈에서는 1페이즈 모습으로 그려진 혈류 흡수를 쓰지 않는다. 대신 다른 공격과 상관없이 주기적으로
        // 맵 아무 곳에서나(플레이어 바로 위는 빼고) 피의 회오리가 솟아 무작위 방향으로 밀려간다.
        [SerializeField] private float bloodStormInterval = 3f;
        [SerializeField] private float bloodStormIntervalJitter = 0.6f;
        [SerializeField] private float bloodStormPlayerClearance = 2.2f;
        [SerializeField] private float bloodStormSpeed = 2.2f;
        [SerializeField] private float bloodStormLifetime = 3.5f;
        [SerializeField] private float bloodStormHitInterval = 0.15f;
        [SerializeField] private float bloodStormDamageMultiplier = 0.3f;

        [Header("차원문 순간이동")]
        [SerializeField] private float teleportFarRange = 6.5f;   // 플레이어가 이보다 멀면 차원문을 타고 다가간다
        [SerializeField] private float teleportCooldown = 7f;
        [SerializeField] private float teleportFirstDelay = 4f;
        [SerializeField] private float teleportEnterDuration = 0.9f;    // 차원문에 빨려 드는 6프레임(나올 때는 거꾸로)
        [SerializeField] private float teleportCollapseDuration = 0.4f; // 차원문이 오그라들어 사라지는 4프레임(열릴 때는 거꾸로)
        [SerializeField] private float teleportGap = 0.3f;              // 사라진 뒤 다시 열리기까지
        [SerializeField] private float teleportNearDistance = 3.2f;     // 플레이어 옆 이만큼 떨어진 곳에 나타난다
        // 빨려 든 마지막 장(차원문만 남은 그림)에서 차원문 한가운데는 발 기준점보다 이만큼 앞/위에 있다(182px = 1칸).
        [SerializeField] private Vector2 portalCenterOffset = new Vector2(11f / 182f, 247f / 182f);
        [SerializeField] private float portalEffectScale = 0.84f;       // 붕괴 그림의 차원문이 빨려 든 그림의 차원문 크기와 맞도록
        // 2페이즈 순간이동(PortalEnter 8프레임 → 붕괴하는 붉은 소용돌이 4프레임). 소용돌이 크기는 그림을 자를 때 맞춰 두어 배율은 1이다.
        [SerializeField] private float teleportEnterDurationPhaseTwo = 1.1f;
        // 빨려 든 마지막 장의 소용돌이 한가운데(바라보는 쪽 기준 앞/위). 그림을 뒤집어 틀기 때문에 원화 기준과 좌우가 반대다.
        [SerializeField] private Vector2 portalCenterOffsetPhaseTwo = new Vector2(0.13f, 1.98f);
        [SerializeField] private float portalEffectScalePhaseTwo = 1f;
        // 차원문 그림을 지금 대기 모습 키에 맞추는 배율. 루나벨 시트를 모두 같은 키로 환산해 두어서(CharacterArtPostprocessor)
        // 지금은 둘 다 1이다. 나중에 2페이즈만 크게 그리고 싶으면 여기서 맞춘다.
        [SerializeField] private float portalClipScalePhaseOne = 1f;
        [SerializeField] private float portalClipScalePhaseTwo = 1f;

        [Header("피의 회오리 (2페이즈)")]
        [SerializeField] private float tornadoCooldown = 9f;
        [SerializeField] private float tornadoSpeed = 3.5f;
        [SerializeField] private float tornadoLifetime = 4f;
        [SerializeField] private float tornadoHeight = 3.6f;
        [SerializeField] private float tornadoHitWidth = 1.2f;
        [SerializeField] private float tornadoHitInterval = 0.15f;
        [SerializeField] private float tornadoDamageMultiplier = 0.3f;

        [Header("피의 가시 난사")]
        [SerializeField] private float thornRange = 9f;            // 플레이어가 이 안에 있으면 쓴다(가시가 뻗어 나가는 거리도 이만큼)
        [SerializeField] private float thornCooldown = 8f;
        [SerializeField] private float thornFirstDelay = 5f;
        [SerializeField] private int thornVolleys = 3;              // 손을 뻗을 때마다 한 줄씩, 연달아 몇 줄
        [SerializeField] private float thornVolleyInterval = 0.2f;  // 한 줄을 쏜 뒤 다음 손짓까지 쉬는 시간
        [SerializeField] private float thornStartForward = 1.0f;    // 몸 앞 이만큼에서 첫 가시가 솟는다
        [SerializeField] private float thornSpacing = 0.9f;
        [SerializeField] private float thornStepInterval = 0.035f;  // 가시가 앞으로 번져 나가는 빠르기
        [SerializeField] private Vector2 thornScaleRange = new Vector2(0.45f, 0.7f); // 가시 그림(키 약 2.9) 배율, 무작위
        // 가장 큰 가시(thornScaleRange.y) 기준 맞는 범위. 작은 가시는 크기에 비례해 줄어든다.
        [SerializeField] private float thornHitWidth = 0.8f;
        [SerializeField] private float thornHitHeight = 1.6f;
        [SerializeField] private float thornHitInterval = 0.1f;
        [SerializeField] private float thornDamageMultiplier = 0.3f;

        private Monster monster;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private MonsterAttackSpriteAnimator attackAnimator;
        private Character target;
        private float contactTimer;
        private bool isTransforming;
        private Coroutine castRoutine;
        private const string PhaseTwoClip = "Phase2";
        private const string AbsorbClip = "Absorb";
        private const string PortalEnterClip = "PortalEnter";
        private const string PortalExitClip = "PortalExit";
        public const string PortalEnterPath = "Art/Monsters/FinalBoss/Phase1/PortalEnter";
        public const string PortalCollapsePath = "Art/Effects/LunabelPortalCollapse";
        // 2페이즈 순간이동: 붉은 소용돌이에 잠식되며 빨려 들고(8프레임, 1페이즈처럼 대기와 반대쪽을 봐서 뒤집어 튼다) → 소용돌이가 닫힌다(4프레임).
        private const string PortalEnterClipPhaseTwo = "PortalEnter2";
        private const string PortalExitClipPhaseTwo = "PortalExit2";
        public const string PortalEnterPhaseTwoPath = "Art/Monsters/FinalBoss/Phase2/PortalEnter";
        public const string PortalCollapsePhaseTwoPath = "Art/Effects/LunabelPortalCollapsePhase2";
        private float teleportTimer;
        private bool isTeleporting;
        private float chaseTimer;
        private float vortexTimer;
        private float tornadoTimer;
        private float giantOrbTimer;
        private float bloodStormTimer;
        public const string GiantOrbPath = "Art/Effects/LunabelGiantOrb";
        // 에너지 구체 그림(배율 1)에서 가운데 구슬 반지름. 구슬 아래끝이 머리 위에 오도록 띄울 때 쓴다.
        private const float GiantOrbCoreRadius = 1.1f;
        private float thornTimer;
        // 피의 결정체 분출(4프레임): 바닥이 갈라지며 핏빛 결정 가시가 솟는다. 첫 조우 연출과 피의 가시 난사가 쓴다.
        public const string BloodThornPath = "Art/Effects/BloodThorn";
        public const float ThornFrameDuration = 0.05f;
        private const float ThornHold = 0.25f;
        private const float ThornFade = 0.2f;
        private readonly System.Collections.Generic.List<GameObject> patternEffects = new System.Collections.Generic.List<GameObject>();
        private const string SpecialClip = "Special";
        private const string ChargeClip = "Charge";
        private const string ThrowClip = "Throw";
        private GameObject specialOrb;

        // 2페이즈: 1페이즈 체력을 다 깎으면 루나벨이 변신한다(Phase2 9프레임). 필드에서는 그 자리에서 바로,
        // 릴리의 리듬 대결(미니게임)에서는 그다음 적 턴에 변신한다(RhythmBattleUI).
        // 1페이즈 동안은 쓰러질 피해를 받아도 체력 1로 버티고(SurviveLethalHit), 그렇게 체력이 바닥나면 변신한다.
        // 변신하면 체력을 모두 채우고, 그 뒤로는 공중에 뜬 2페이즈 대기 모션(Phase2Idle 5프레임)으로 서 있다. 한 번 변신하면
        // 흡혈로 체력이 다시 차도 2페이즈가 유지되고, 필드에서도 피의 손을 더 자주 날린다.
        private const float PhaseTwoHealthThreshold = 1f;
        public const string PhaseTwoPath = "Art/Monsters/FinalBoss/Phase2/Transform";
        public const string PhaseTwoIdlePath = "Art/Monsters/FinalBoss/Phase2/Idle";
        public const string IdlePath = "Art/Monsters/FinalBoss/Phase1/Idle";
        // 첫 조우 연출(LunabelIntroCutscene) 동안 의자에 앉아 있는 모습(4프레임 반복).
        public const string SeatedPath = "Art/Monsters/FinalBoss/Phase1/Seated";
        // 2페이즈 기본 공격(붉은 마법을 두른 손을 앞으로 뻗는 8프레임). 변신하면 필드/리듬전투 모두 이 모션으로 피의 손을 날린다.
        public const string PhaseTwoAttackPath = "Art/Monsters/FinalBoss/Phase2/Attack";
        // 1페이즈 혈류 흡수(8프레임). 흡수해 체력을 채우면 피가 들끓는 상태(BloodCharged)가 되고, 그 뒤로 루나벨의
        // 노드를 일정 개수(BloodMisses가 문턱에 닿을 때까지) 놓치면 피의 회오리(Lubel_Skill1 → Effects/LunabelBloodVortex)가 터진다.
        public const string AbsorbPath = "Art/Monsters/FinalBoss/Phase1/Absorb";
        public const string BloodVortexPath = "Art/Effects/LunabelBloodVortex";
        public bool BloodCharged { get; set; }
        // 변신 직전 대사. 대화창을 가진 등장 연출(LunabelIntroCutscene)이 채워 넣는다(없으면 대사 없이 변신한다).
        public System.Func<IEnumerator> PhaseTwoTalk { get; set; }
        // 2페이즈 개막에서 피를 모으는 동안 화면 가운데에 띄우는 광기 어린 웃음(저절로 사라진다). 이게 끝나야 구체를 던진다.
        public System.Func<IEnumerator> ChargeLaugh { get; set; }
        public int BloodMisses { get; set; }
        public const string HitPath = "Art/Monsters/FinalBoss/Phase1/Hit";
        [SerializeField] private float phaseTwoCooldownScale = 0.6f;
        public bool PhaseTwoStarted { get; private set; }

        // 변신 연출이 끝나는 순간 리듬전투가 부른다: 체력을 모두 채우고 대기 모습을 변신한 모습으로 바꾼다.
        public void EnterPhaseTwo(Sprite[] phaseTwoIdleFrames)
        {
            PhaseTwoStarted = true;
            giantOrbTimer = giantOrbFirstDelay;
            bloodStormTimer = bloodStormInterval;
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

        // 2페이즈 필살기: 2페이즈 체력이 각 문턱(60%, 25%) 이하로 처음 내려가면 한 번씩 쓴다(리듬 대결에서는 그다음 적 턴에).
        // 하늘로 손을 뻗고(Special 8프레임 한 번) → 핏빛 구체를 모으다가(Charge 8프레임 반복) → 구체(Effects/BloodOrb)를 던진다.
        // 필드에서는 구체가 앞쪽 땅에 떨어져 터지고, 피의 기둥(BloodPillarRise)이 앞으로 줄지어 솟구친다(SpecialRoutine).
        private static readonly float[] SpecialMoveHealthFractions = { 0.6f, 0.25f };
        public const string SpecialPath = "Art/Monsters/FinalBoss/Phase2/Special";
        public const string SpecialChargePath = "Art/Monsters/FinalBoss/Phase2/Charge";
        public const string BloodOrbPath = "Art/Effects/BloodOrb";
        // 노드를 다 못 치면: 구체 던지기(Throw 5프레임) → 플레이어 발밑에서 피의 기둥이 솟구쳐(BloodPillarRise) 회오리친다(BloodPillarTornado).
        public const string ThrowPath = "Art/Monsters/FinalBoss/Phase2/Throw";
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
            // 쓰러지는 순간 맵에 남은 피의 회오리/가시 같은 패턴 이펙트를 걷어 낸다(패배 연출 중에 맞지 않게).
            monster.OnDied += _ =>
            {
                foreach (var effect in patternEffects) if (effect != null) Destroy(effect);
                patternEffects.Clear();
            };
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            attackAnimator = GetComponent<MonsterAttackSpriteAnimator>();
            body.gravityScale = gravityScale;
            body.freezeRotation = true;
            castTimer = castCooldown * 0.5f; // 마주치자마자 바로 쏘지 않도록 조금 늦춘다
            chaseTimer = chaseFirstDelay;
            tornadoTimer = tornadoCooldown * 0.4f;
            teleportTimer = teleportFirstDelay;
            thornTimer = thornFirstDelay;

            target = Object.FindFirstObjectByType<Character>();
            if (attackAnimator != null)
            {
                attackAnimator.AddClip(PhaseTwoClip, PhaseTwoPath);
                attackAnimator.AddClip(SpecialClip, SpecialPath);
                attackAnimator.AddClip(ChargeClip, SpecialChargePath);
                attackAnimator.AddClip(ThrowClip, ThrowPath);
                attackAnimator.AddClip(AbsorbClip, AbsorbPath);
                var enter = LoadSorted(PortalEnterPath);
                attackAnimator.AddClip(PortalEnterClip, enter);
                var exit = (Sprite[])enter.Clone();
                System.Array.Reverse(exit);
                attackAnimator.AddClip(PortalExitClip, exit);
                var enterTwo = LoadSorted(PortalEnterPhaseTwoPath);
                if (enterTwo.Length > 0)
                {
                    attackAnimator.AddClip(PortalEnterClipPhaseTwo, enterTwo);
                    var exitTwo = (Sprite[])enterTwo.Clone();
                    System.Array.Reverse(exitTwo);
                    attackAnimator.AddClip(PortalExitClipPhaseTwo, exitTwo);
                }
            }
        }

        // 등장 대사가 흐르는 동안 공격/접촉 전투를 멈춘다. 대사가 끝나면 EndIntro로 풀어 준다.
        public void HoldForIntro() => isHoldingForIntro = true;
        public void EndIntro() => isHoldingForIntro = false;

        private void Update()
        {
            if (monster.IsMinigameOpponent || monster.IsDead || target == null || target.IsDead) return;

            Vector2 offset = target.transform.position - transform.position;
            if (!isCasting && !isTransforming) spriteRenderer.flipX = offset.x < 0f;
            if (isHoldingForIntro || isTransforming || isTeleporting) return;

            if (ShouldStartPhaseTwo)
            {
                StartCoroutine(TransformRoutine());
                return;
            }

            if (castTimer > 0f && !isCasting) castTimer -= Time.deltaTime;
            if (contactTimer > 0f) contactTimer -= Time.deltaTime;
            if (!isCasting)
            {
                if (chaseTimer > 0f) chaseTimer -= Time.deltaTime;
                if (vortexTimer > 0f) vortexTimer -= Time.deltaTime;
                if (tornadoTimer > 0f) tornadoTimer -= Time.deltaTime;
                if (teleportTimer > 0f) teleportTimer -= Time.deltaTime;
                if (thornTimer > 0f) thornTimer -= Time.deltaTime;
                if (giantOrbTimer > 0f) giantOrbTimer -= Time.deltaTime;
            }
            UpdateBloodStorm();

            if (contactTimer <= 0f && Mathf.Abs(offset.x) <= contactRange && offset.y <= 2f && offset.y >= -0.5f)
            {
                contactTimer = contactInterval;
                target.TakeDamage(CombatMath.PhysicalDamage(monster.Attack * contactDamageMultiplier, target.Stats.defense), transform.position);
            }

            if (!isCasting && ShouldUseSpecialMove && attackAnimator != null)
            {
                castRoutine = StartCoroutine(SpecialRoutine());
                return;
            }

            if (isCasting || monster.IsImmobilized || attackAnimator == null || !attackAnimator.HasFrames) return;
            float distance = Mathf.Abs(offset.x);

            if (PhaseTwoStarted && giantOrbTimer <= 0f && attackAnimator.HasClip(SpecialClip))
            {
                castRoutine = StartCoroutine(GiantOrbRoutine());
                return;
            }
            // 혈류 흡수는 1페이즈 모습으로 그려져 있어 1페이즈에서만 쓴다(2페이즈는 피의 폭풍이 대신한다).
            if (!PhaseTwoStarted && distance <= vortexTriggerRange && vortexTimer <= 0f && attackAnimator.HasClip(AbsorbClip))
            {
                castRoutine = StartCoroutine(VortexRoutine());
                return;
            }
            if (distance >= teleportFarRange && teleportTimer <= 0f && attackAnimator.HasClip(PortalEnterClip))
            {
                castRoutine = StartCoroutine(TeleportRoutine());
                return;
            }
            if (PhaseTwoStarted && tornadoTimer <= 0f && distance <= castRange)
            {
                castRoutine = StartCoroutine(TornadoRoutine());
                return;
            }
            if (thornTimer <= 0f && distance <= thornRange)
            {
                castRoutine = StartCoroutine(ThornBarrageRoutine());
                return;
            }
            if (chaseTimer <= 0f && distance <= chaseRange)
            {
                castRoutine = StartCoroutine(PillarChaseRoutine());
                return;
            }
            if (castTimer <= 0f && distance <= castRange)
            {
                castRoutine = StartCoroutine(CastRoutine());
            }
        }

        // 패턴이 연달아 붙지 않게, 하나를 쓰고 나면 피의 손도 잠깐 쉰다.
        private void EndPattern()
        {
            castTimer = Mathf.Max(castTimer, 1.2f);
            isCasting = false;
            castRoutine = null;
        }

        private float Cooldown(float baseCooldown) => baseCooldown * (PhaseTwoStarted ? phaseTwoCooldownScale + 0.2f : 1f);

        // 피의 기둥 추격: 손을 뻗어(기본 공격 모션) 땅에 피를 흘려 보내면, 플레이어 발밑이 핏빛으로 일렁인 뒤 기둥이 솟는다.
        // 매번 그 순간 플레이어 자리를 다시 노려 chaseCount번 연달아 솟으니, 멈춰 있지 말고 계속 움직여야 피할 수 있다.
        private IEnumerator PillarChaseRoutine()
        {
            isCasting = true;
            chaseTimer = Cooldown(chaseCooldown);
            attackAnimator.Play(castAnimDuration);
            yield return new WaitForSeconds(castAnimDuration * castFireTiming);

            float groundY = GroundY();
            for (int i = 0; i < chaseCount && !monster.IsDead && target != null && !target.IsDead; i++)
            {
                StartCoroutine(ChasePillar(new Vector2(target.transform.position.x, groundY)));
                yield return new WaitForSeconds(chaseInterval);
            }
            EndPattern();
        }

        private IEnumerator ChasePillar(Vector2 bottomCenter)
        {
            var frames = LoadSorted(BloodPillarRisePath);
            if (frames.Length == 0) yield break;
            var go = new GameObject("LunabelChasePillar");
            go.transform.position = bottomCenter;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 3;
            patternEffects.Add(go);

            // 예고: 기둥 첫 장(바닥에 핏물이 번지는 그림)을 흐리게 떨며 보여 준다.
            renderer.sprite = frames[0];
            for (float t = 0f; t < chaseWarnTime; t += Time.deltaTime)
            {
                if (go == null) yield break;
                renderer.color = new Color(1f, 1f, 1f, 0.35f + 0.25f * Mathf.Sin(t * 30f));
                yield return null;
            }
            renderer.color = Color.white;

            for (int i = 0; i < frames.Length; i++)
            {
                if (go == null) yield break;
                renderer.sprite = frames[i];
                // 기둥이 솟기 시작하는 세 번째 장부터 끝까지, 장마다 한 번씩 무적 없이 연타로 맞는다.
                if (i >= 2 && !monster.IsDead) HitPlayerInBox(bottomCenter, chaseHitWidth, chaseHitHeight, chaseDamageMultiplier, hitReaction: false);
                yield return new WaitForSeconds(0.06f);
            }
            yield return FadeAndDestroy(go, renderer, 0.25f);
        }

        // 피의 소용돌이: 가까이 붙은 플레이어를 떼어 낸다. 혈류를 흡수하는 모션(Absorb) 끝에 몸 양옆에서 피의 소용돌이가 하나씩
        // 솟아 좌우 바깥으로 날아간다. 지나가는 자리를 계속 할퀴니 대시로 뚫거나 이단 점프로 넘는다.
        private IEnumerator VortexRoutine()
        {
            isCasting = true;
            vortexTimer = Cooldown(vortexCooldown);
            attackAnimator.PlayClip(AbsorbClip, vortexAbsorbDuration);
            yield return new WaitForSeconds(vortexAbsorbDuration * 0.75f);
            if (monster.IsDead) { EndPattern(); yield break; }

            float groundY = GroundY();
            foreach (float side in new[] { -1f, 1f })
            {
                SandWhirlwind.Spawn(new Vector2(transform.position.x + side * vortexSpawnOffset, groundY), side, monster.Attack,
                    vortexDamageMultiplier, vortexSpeed, vortexLifetime, vortexHitInterval, vortexHitWidth, vortexHeight, vortexHeight,
                    BloodVortexPath, hitReaction: false);
            }
            yield return new WaitForSeconds(vortexAbsorbDuration * 0.25f + 0.3f);
            EndPattern();
        }

        // 2페이즈 피의 회오리: 손끝의 구체를 땅에 쏘아(2페이즈 기본 공격 모션) 핏빛 회오리를 일으키면, 회오리가 땅을 타고
        // 플레이어 쪽으로 밀려오며 지나가는 자리를 계속 할퀸다. 키가 낮지 않아 대시로 뚫거나 점프 두 번으로 넘어야 한다.
        private IEnumerator TornadoRoutine()
        {
            isCasting = true;
            tornadoTimer = tornadoCooldown;
            float facing = spriteRenderer.flipX ? -1f : 1f;
            attackAnimator.Play(castAnimDuration);
            yield return new WaitForSeconds(castAnimDuration * castFireTiming);
            if (!monster.IsDead)
            {
                Vector2 at = new Vector2(transform.position.x + facing * handMuzzleForward * 1.5f, GroundY());
                SandWhirlwind.Spawn(at, facing, monster.Attack, tornadoDamageMultiplier, tornadoSpeed, tornadoLifetime,
                    tornadoHitInterval, tornadoHitWidth, tornadoHeight, tornadoHeight, BloodPillarTornadoPath, hitReaction: false);
            }
            yield return new WaitForSeconds(castAnimDuration * (1f - castFireTiming));
            EndPattern();
        }

        // 피의 가시 난사: 손을 앞으로 뻗을 때마다(기본 공격 모션) 몸 앞에서부터 핏빛 결정 가시가 땅을 타고 한 줄로 빠르게
        // 뻗어 나간다. thornVolleys번 연달아 쏘고, 줄마다 가시 자리가 조금씩 어긋나며 크기도 제각각이다.
        // 솟은 가시에 닿아 있으면 무적 없이 연타로 맞는다. 줄 사이 틈에 이단 점프로 넘거나 대시 무적으로 뚫는다.
        private IEnumerator ThornBarrageRoutine()
        {
            isCasting = true;
            thornTimer = Cooldown(thornCooldown);
            var frames = LoadSorted(BloodThornPath);
            if (frames.Length == 0) { EndPattern(); yield break; }

            Rect bounds = GameBootstrap.CurrentMapBounds;
            for (int volley = 0; volley < thornVolleys && !monster.IsDead; volley++)
            {
                float facing = spriteRenderer.flipX ? -1f : 1f;
                attackAnimator.Play(castAnimDuration);
                yield return new WaitForSeconds(castAnimDuration * castFireTiming);
                if (monster.IsDead) break;

                float groundY = GroundY();
                float x = transform.position.x + facing * (thornStartForward + (volley % 2) * thornSpacing * 0.5f);
                float end = transform.position.x + facing * thornRange;
                // 한 줄을 다 쏘는 동안 다음 손짓을 기다리지 않고, 가시는 따로 뻗어 나간다.
                StartCoroutine(ThornLine(frames, x, end, facing, groundY, bounds));
                if (volley < thornVolleys - 1) yield return new WaitForSeconds(thornVolleyInterval);
            }
            yield return new WaitForSeconds(castAnimDuration * (1f - castFireTiming));
            EndPattern();
        }

        private IEnumerator ThornLine(Sprite[] frames, float x, float end, float facing, float groundY, Rect bounds)
        {
            while ((end - x) * facing >= 0f && !monster.IsDead)
            {
                if (x < bounds.xMin + 0.3f || x > bounds.xMax - 0.3f) yield break;
                float jitter = Random.Range(-0.2f, 0.2f);
                float scale = Random.Range(thornScaleRange.x, thornScaleRange.y);
                StartCoroutine(BloodThorn(frames, new Vector2(x + jitter, groundY), scale, facing, dealsDamage: true));
                x += facing * thornSpacing;
                yield return new WaitForSeconds(thornStepInterval);
            }
        }

        // 핏빛 결정 가시 하나: 바닥이 갈라지며 솟고(4프레임) 잠깐 머물렀다 흐려진다. dealsDamage면 두 번째 장부터
        // 흐려지기 전까지 가시 안에 있으면 thornHitInterval마다 무적 없이 맞는다. 첫 조우 연출도 피해 없이 이걸 빌려 쓴다.
        public IEnumerator BloodThorn(Sprite[] frames, Vector2 bottomCenter, float scale, float facing, bool dealsDamage,
            Transform parent = null, int sortingOrder = 3)
        {
            var go = new GameObject("LunabelBloodThorn");
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = bottomCenter;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.flipX = facing < 0f;
            if (dealsDamage) patternEffects.Add(go);

            float riseTime = frames.Length * ThornFrameDuration;
            float nextHit = ThornFrameDuration; // 두 번째 장(가시가 막 솟음)부터
            for (float t = 0f; t < riseTime + ThornHold; t += Time.deltaTime)
            {
                if (go == null) yield break;
                renderer.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t / ThornFrameDuration))];
                if (dealsDamage && t >= nextHit && !monster.IsDead)
                {
                    nextHit += thornHitInterval;
                    float growth = Mathf.Clamp01((t + ThornFrameDuration) / riseTime);
                    HitPlayerInBox(bottomCenter, thornHitWidth * scale / thornScaleRange.y, thornHitHeight * growth * scale / thornScaleRange.y,
                        thornDamageMultiplier, hitReaction: false);
                }
                yield return null;
            }
            for (float t = 0f; t < ThornFade; t += Time.deltaTime)
            {
                if (go == null) yield break;
                renderer.color = new Color(1f, 1f, 1f, 1f - t / ThornFade);
                yield return null;
            }
            patternEffects.Remove(go);
            if (go != null) Destroy(go);
        }

        // 첫 조우 연출 마지막: 의자에서 일어난 루나벨이 차원문을 타고 플레이어 바로 앞(거리 distance)으로 넘어온다.
        public IEnumerator IntroTeleportInFrontOf(Transform player, float distance)
        {
            Rect bounds = GameBootstrap.CurrentMapBounds;
            float side = transform.position.x >= player.position.x ? 1f : -1f;
            float x = Mathf.Clamp(player.position.x + side * distance, bounds.xMin + 1.5f, bounds.xMax - 1.5f);
            // 순간이동하는 동안 Update가 바라보는 방향을 바꾸지 않게 시전 중으로 둔다.
            isCasting = true;
            isTeleporting = true;
            yield return PortalHopTo(new Vector3(x, transform.position.y, transform.position.z), keepAirborne: false);
            isTeleporting = false;
            isCasting = false;
            spriteRenderer.flipX = player.position.x < transform.position.x;
        }

        // 차원문 순간이동: 손을 뻗어 연 붉은 차원문에 빨려 들고(PortalEnter 6프레임) → 남은 차원문이 오그라들어 사라진다(붕괴 4프레임).
        // 잠깐 뒤 플레이어 옆(앞이나 뒤, 맵 안쪽)에 차원문이 거꾸로 열리고 → 그 안에서 걸어 나온다(PortalEnter를 거꾸로).
        // 사라져 있는 동안은 몸통 판정과 체력바를 끄고 맞지 않는다.
        private IEnumerator TeleportRoutine()
        {
            isCasting = true;
            isTeleporting = true;
            teleportTimer = Cooldown(teleportCooldown);
            var bodyCollider = GetComponent<Collider2D>();
            float clipScale = PhaseTwoStarted ? portalClipScalePhaseTwo : portalClipScalePhaseOne;
            SetHealthBarVisible(false);
            transform.localScale = new Vector3(clipScale, clipScale, 1f);

            FacePlayer();
            yield return PlayPortalClip(PortalEnterClip);

            // 사라진다: 몸은 숨기고 그 자리 차원문이 오그라든다.
            SetVisible(false);
            if (bodyCollider != null) bodyCollider.enabled = false;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            StartCoroutine(PlayPortalCollapse(PortalCenter(), reverse: false));
            yield return new WaitForSeconds(teleportCollapseDuration + teleportGap);

            // 플레이어 옆 맵 안쪽으로 옮기고, 그곳 차원문이 열린다.
            if (target != null && !monster.IsDead)
            {
                transform.position = new Vector3(PickTeleportX(), transform.position.y, transform.position.z);
                // 도착한 자리에서 플레이어 쪽을 보고 차원문을 연다(나오기 직전에 한 번 더 확인한다).
                FacePlayer();
            }
            StartCoroutine(PlayPortalCollapse(PortalCenter(), reverse: true));
            yield return new WaitForSeconds(teleportCollapseDuration);

            // 차원문 안에서 걸어 나온다. 차원문이 열리는 사이 플레이어가 반대편으로 옮겨 갔으면 그쪽을 보고 나온다.
            SetVisible(true);
            SetHealthBarVisible(false);
            FacePlayer();
            yield return PlayPortalClip(PortalExitClip);
            transform.localScale = Vector3.one;
            SetHealthBarVisible(true);

            if (bodyCollider != null) bodyCollider.enabled = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            isTeleporting = false;
            EndPattern();
        }

        // 플레이어 앞/뒤 중 맵 안에 들어가는 쪽, 둘 다 되면 무작위. 맵 끝에 몰려 있으면 들어가는 만큼만 붙는다.
        private float PickTeleportX()
        {
            Rect bounds = GameBootstrap.CurrentMapBounds;
            float minX = bounds.xMin + 1.5f, maxX = bounds.xMax - 1.5f;
            float px = target.transform.position.x;
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = px + side * teleportNearDistance;
            if (x < minX || x > maxX) x = px - side * teleportNearDistance;
            return Mathf.Clamp(x, minX, maxX);
        }

        // 차원문 그림(PortalEnter)은 다른 모션과 달리 루나벨이 왼쪽을 보고 그려져 있어서, 그대로 틀면 순간이동하는 순간
        // 몸이 홱 돌아섰다. 차원문 모션을 트는 동안만 좌우를 뒤집어 대기 때와 같은 방향을 보게 하고, 끝나면 되돌린다.
        // 2페이즈에서는 2페이즈 전용 그림으로 바꿔 튼다. 1·2페이즈 차원문 그림 모두 대기와 반대쪽을 보고 그려져 있어서
        // 트는 동안만 좌우를 뒤집는다.
        private IEnumerator PlayPortalClip(string clipName)
        {
            bool phaseTwo = UsePhaseTwoPortal;
            string clip = !phaseTwo ? clipName : clipName == PortalExitClip ? PortalExitClipPhaseTwo : PortalEnterClipPhaseTwo;
            float duration = phaseTwo ? teleportEnterDurationPhaseTwo : teleportEnterDuration;
            bool facingLeft = spriteRenderer.flipX;
            spriteRenderer.flipX = !facingLeft;
            attackAnimator.PlayClip(clip, duration);
            yield return new WaitForSeconds(duration);
            spriteRenderer.flipX = facingLeft;
        }

        // 순간이동은 들어갈 때와 나올 때 모두 그 순간 플레이어가 있는 쪽을 본다.
        private void FacePlayer()
        {
            if (target != null) spriteRenderer.flipX = target.transform.position.x < transform.position.x;
        }

        private bool UsePhaseTwoPortal => PhaseTwoStarted && attackAnimator != null && attackAnimator.HasClip(PortalEnterClipPhaseTwo);

        private Vector2 PortalCenter()
        {
            float facing = spriteRenderer.flipX ? -1f : 1f;
            Vector2 offset = UsePhaseTwoPortal ? portalCenterOffsetPhaseTwo : portalCenterOffset;
            return (Vector2)transform.position + new Vector2(facing * offset.x, offset.y) * transform.localScale.y;
        }

        private void SetVisible(bool visible)
        {
            spriteRenderer.enabled = visible;
            SetHealthBarVisible(visible);
        }

        private void SetHealthBarVisible(bool visible)
        {
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("HealthBar")) child.gameObject.SetActive(visible);
            }
        }

        // 붕괴하는 차원문(4프레임, 점점 오그라든다). reverse면 거꾸로 — 빛점에서 차원문이 활짝 열린다.
        private IEnumerator PlayPortalCollapse(Vector2 center, bool reverse)
        {
            bool phaseTwo = UsePhaseTwoPortal;
            var frames = LoadSorted(phaseTwo ? PortalCollapsePhaseTwoPath : PortalCollapsePath);
            if (frames.Length == 0) yield break;
            if (reverse) System.Array.Reverse(frames);
            var go = new GameObject("LunabelPortal");
            go.transform.position = center;
            float size = (phaseTwo ? portalEffectScalePhaseTwo : portalEffectScale) * transform.localScale.y;
            // 2페이즈 소용돌이는 화면에 그려진 빨려 든 마지막 장과 같은 쪽으로 뒤집는다(차원문 그림은 바라보는 방향과
            // 반대로 뒤집어 틀므로 !flipX). 소용돌이 모양이 그대로 이어진다.
            bool flip = phaseTwo && !spriteRenderer.flipX;
            go.transform.localScale = new Vector3(size, size, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            renderer.flipX = flip;
            patternEffects.Add(go);
            float frameTime = teleportCollapseDuration / frames.Length;
            foreach (var frame in frames)
            {
                if (go == null) yield break;
                renderer.sprite = frame;
                yield return new WaitForSeconds(frameTime);
            }
            patternEffects.Remove(go);
            if (go != null) Destroy(go);
        }

        // 바닥(bottomCenter)부터 위로 width x height 상자 안에 플레이어가 있으면 피해를 준다. 맞혔으면 true.
        // hitReaction이 false면 맞아도 넉백/피격 무적이 없다(짧은 간격으로 연타하는 공격용).
        private bool HitPlayerInBox(Vector2 bottomCenter, float width, float height, float damageMultiplier, bool hitReaction = true)
        {
            if (target == null || target.IsDead) return false;
            Vector2 feet = target.transform.position;
            if (Mathf.Abs(feet.x - bottomCenter.x) > width * 0.5f) return false;
            if (feet.y > bottomCenter.y + height || feet.y + 1.5f < bottomCenter.y) return false;
            target.TakeDamage(CombatMath.PhysicalDamage(monster.Attack * damageMultiplier, target.Stats.defense), bottomCenter, hitReaction);
            return true;
        }

        private IEnumerator FadeAndDestroy(GameObject go, SpriteRenderer renderer, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (go == null) yield break;
                renderer.color = new Color(1f, 1f, 1f, 1f - t / duration);
                yield return null;
            }
            patternEffects.Remove(go);
            if (go != null) Destroy(go);
        }

        private void LateUpdate()
        {
            UpdateOpeningFocus();
        }

        private void FixedUpdate()
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }

        // 필드 변신: 시전 중이던 피의 손을 끊고, 변신 전 대사 → 차원문을 타고 맵 가운데 위 공중으로 올라가 →
        // 공중에서 변신 모션을 한 번 재생한 뒤 2페이즈로 들어가고, 그 자리에서 곧바로 거대한 구체를 모아 던진다.
        // 변신하는 동안에도 1페이즈의 SurviveLethalHit가 켜져 있어 체력 1 아래로는 떨어지지 않는다.
        private IEnumerator TransformRoutine()
        {
            isTransforming = true;
            if (castRoutine != null) StopCoroutine(castRoutine);
            castRoutine = null;
            isCasting = false;
            if (specialOrb != null) Destroy(specialOrb);
            if (attackAnimator != null) attackAnimator.Stop();

            // 변신 전 대사: 조작을 잠깐 막고 대사를 주고받은 뒤 변신한다.
            var playerMovement = target != null ? target.GetComponent<CharacterMovement2D>() : null;
            if (PhaseTwoTalk != null)
            {
                if (playerMovement != null) playerMovement.SetCutsceneLocked(true);
                yield return PhaseTwoTalk();
            }

            // 차원문을 타고 맵 가운데 위 공중으로 올라간다. 카메라를 넓혀 공중의 루나벨과 플레이어가 한 화면에 들어오게 하고
            // (개막 패턴이 끝나면 되돌린다), 그 자리에서 변신한다.
            float groundY = GroundY();
            float floorPosY = transform.position.y;
            float centerX = GameBootstrap.CurrentMapBounds.center.x;
            bool airborne = attackAnimator != null && target != null;
            if (airborne)
            {
                yield return PortalHopTo(new Vector3(centerX, floorPosY + openingAirHeight, transform.position.z), keepAirborne: true);
                BeginOpeningCamera();
            }

            Sprite[] transformFrames = LoadSorted(PhaseTwoPath);
            if (attackAnimator != null && attackAnimator.HasClip(PhaseTwoClip))
            {
                attackAnimator.PlayClip(PhaseTwoClip, transformDuration);
                yield return new WaitForSeconds(transformDuration);
                // 변신을 마친 순간 몸에서 핏빛 충격파가 연달아 퍼지며 화면이 크게 흔들리고 붉게 번쩍인다(모래룡 포효와 같은 연출).
                if (airborne) AwakeningShockwave();
                yield return new WaitForSeconds(transformHold);
            }

            // 2페이즈 대기 모션이 없으면 변신 마지막 프레임(뿔과 날개가 돋은 모습)으로 서 있는다.
            Sprite[] phaseTwoIdle = LoadSorted(PhaseTwoIdlePath);
            if (phaseTwoIdle.Length == 0 && transformFrames.Length > 0) phaseTwoIdle = new[] { transformFrames[transformFrames.Length - 1] };
            if (attackAnimator != null) attackAnimator.Stop();
            EnterPhaseTwo(phaseTwoIdle);
            if (playerMovement != null) playerMovement.SetCutsceneLocked(false);

            // 변신하자마자 공중에서 거대한 구체를 모아 맵 가운데로 던져 거대한 핏기둥을 터뜨린다(조작은 풀려 있어 피할 수 있다).
            if (airborne) yield return PhaseTwoOpeningRoutine(groundY, floorPosY, centerX);
            castTimer = castCooldown * phaseTwoCooldownScale * 0.5f;
            isTransforming = false;
        }

        // 2페이즈 전투 중 간간히 다시 쓰는 개막 패턴: 차원문을 타고 맵 가운데 위 공중으로 올라가 → 거대한 에너지 구체를 모으며
        // 플레이어를 빨아들이고 → 내리꽂아 거대한 핏기둥 → 차원문을 타고 내려온다. 변신 직후와 달리 웃음 대사는 없다.
        private bool inGiantOrb;
        // 바닥 높이. 거대한 구체 패턴 중에는 루나벨이 공중에 떠 있어 GroundY()가 바닥을 가리키지 않으므로, 처음 자리에서 재 둔다.
        private float groundLevel;

        private void Start()
        {
            groundLevel = GroundY();
        }

        private IEnumerator GiantOrbRoutine()
        {
            isCasting = true;
            inGiantOrb = true;
            giantOrbTimer = giantOrbCooldown;
            float groundY = GroundY();
            float floorPosY = transform.position.y;
            float centerX = GameBootstrap.CurrentMapBounds.center.x;

            yield return PortalHopTo(new Vector3(centerX, floorPosY + openingAirHeight, transform.position.z), keepAirborne: true);
            if (!monster.IsDead)
            {
                BeginOpeningCamera();
                yield return PhaseTwoOpeningRoutine(groundY, floorPosY, centerX, laugh: false);
            }
            inGiantOrb = false;
            EndPattern();
        }

        // 피의 폭풍(2페이즈): 다른 공격과 상관없이 bloodStormInterval(±jitter)마다 맵 안 아무 곳에서나 피의 회오리가 솟아
        // 무작위 방향으로 밀려간다. 플레이어 바로 위에서는 솟지 않아 솟는 동안(맞지 않는 0.4초) 보고 피할 수 있다.
        // 변신 중이나 거대한 구체 패턴 중에는 쉰다(빨아들이는 힘과 겹치면 피할 길이 없다).
        private void UpdateBloodStorm()
        {
            if (!PhaseTwoStarted || isTransforming || inGiantOrb) return;
            bloodStormTimer -= Time.deltaTime;
            if (bloodStormTimer > 0f) return;
            bloodStormTimer = bloodStormInterval + Random.Range(-bloodStormIntervalJitter, bloodStormIntervalJitter);

            Rect bounds = GameBootstrap.CurrentMapBounds;
            float minX = bounds.xMin + 1f, maxX = bounds.xMax - 1f;
            if (maxX <= minX) return;
            float playerX = target.transform.position.x;
            float x = Random.Range(minX, maxX);
            for (int i = 0; i < 6 && Mathf.Abs(x - playerX) < bloodStormPlayerClearance; i++) x = Random.Range(minX, maxX);
            if (Mathf.Abs(x - playerX) < bloodStormPlayerClearance) return;

            float direction = Random.value < 0.5f ? -1f : 1f;
            var whirl = SandWhirlwind.Spawn(new Vector2(x, groundLevel), direction, monster.Attack, bloodStormDamageMultiplier,
                bloodStormSpeed, bloodStormLifetime, bloodStormHitInterval, vortexHitWidth, vortexHeight, vortexHeight,
                BloodVortexPath, hitReaction: false);
            // 루나벨이 쓰러지면 남은 회오리도 함께 걷히도록 패턴 이펙트로 묶어 둔다.
            patternEffects.RemoveAll(e => e == null);
            if (whirl != null) patternEffects.Add(whirl.gameObject);
        }

        private void AwakeningShockwave()
        {
            var cam = Camera.main;
            var cameraFollow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            if (cameraFollow != null) cameraFollow.Shake(awakeningShakeDuration, awakeningShakeMagnitude);
            ShockwaveBurst.Play(this, spriteRenderer.bounds.center, transform.parent, new ShockwaveBurst.Settings
            {
                rings = 5,
                interval = 0.13f,
                duration = 0.8f,
                maxSize = awakeningShockwaveSize,
                squash = 0.85f,
                ringColor = new Color(1f, 0.25f, 0.3f, 0.9f),
                flashColor = new Color(1f, 0.2f, 0.25f, 0.4f),
                flashDuration = 0.4f,
                sortingOrder = 55,
            });
        }

        // 2페이즈 개막(TransformRoutine이 공중으로 올라가 변신한 직후 부른다): 하늘로 손을 뻗고(Special) 머리 위로 거대한
        // 핏빛 에너지 구체를 모은다(Charge). 모으는 동안 플레이어를 맵 가운데로 점점 세게 빨아들인다(대시를 계속 써야 겨우 빠져나간다).
        // 다 모으면 구체를 바로 아래로 내리꽂고(Throw), 맵 가운데 바닥이 들끓은 뒤 화면을 메우는 거대한 피의 기둥이 솟아
        // 회오리친다 — 기둥 안에 있으면 0.125초마다 연타로 맞는다. 기둥이 걷히면 다시 차원문을 타고 플레이어 근처 땅으로 내려온다.
        // groundY/floorPosY는 올라가기 전 땅 높이(발판 아래 바닥)와 발 위치, centerX는 맵 가운데.
        // laugh: 변신 직후 첫 개막에서만 광기 어린 웃음 대사를 띄운다(전투 중 다시 쓸 때는 대사 없이).
        private IEnumerator PhaseTwoOpeningRoutine(float groundY, float floorPosY, float centerX, bool laugh = true)
        {
            if (monster.IsDead) { EndOpeningCamera(); yield break; }

            pullActive = true;
            StartCoroutine(PullPlayerRoutine(centerX));

            attackAnimator.PlayClip(SpecialClip, specialRaiseDuration);
            yield return new WaitForSeconds(specialRaiseDuration);

            attackAnimator.PlayClipLoop(ChargeClip, specialChargeCycle);
            // 피를 모으기 시작하면 화면 가운데에 광기 어린 웃음이 떠오른다. 웃음이 사라질 때까지 구체를 키운 뒤 던진다.
            bool laughDone = !laugh || ChargeLaugh == null;
            if (!laughDone) StartCoroutine(RunThen(ChargeLaugh(), () => laughDone = true));
            // 머리 위에서 진홍빛 에너지 구체(6프레임 반복)가 점점 커진다. 그림이 없으면 예전 핏빛 구체 한 장을 쓴다.
            var orbFrames = LoadSorted(GiantOrbPath);
            bool animatedOrb = orbFrames.Length > 0;
            if (!animatedOrb)
            {
                var fallback = Resources.Load<Sprite>(BloodOrbPath + "/orb_1");
                if (fallback != null) orbFrames = new[] { fallback };
            }
            SpriteRenderer orbRenderer = null;
            if (orbFrames.Length > 0)
            {
                specialOrb = new GameObject("LunabelGiantOrb");
                orbRenderer = specialOrb.AddComponent<SpriteRenderer>();
                orbRenderer.sprite = orbFrames[0];
                orbRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            }
            var cam = Camera.main;
            var cameraFollow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            if (cameraFollow != null) cameraFollow.Shake(openingChargeDuration, 0.08f);
            float orbAge = 0f;
            for (float t = 0f; t < openingChargeDuration || !laughDone; t += Time.deltaTime)
            {
                if (monster.IsDead) { StopOpeningPull(); EndOpeningCamera(); yield break; }
                orbAge += Time.deltaTime;
                if (specialOrb != null)
                {
                    float grow = Mathf.SmoothStep(0.15f, 1f, Mathf.Min(1f, t / openingChargeDuration));
                    float pulse = 1f + 0.08f * Mathf.Sin(t * 16f);
                    float scale = openingOrbScale * grow * pulse;
                    specialOrb.transform.localScale = Vector3.one * scale;
                    orbRenderer.sprite = orbFrames[(int)(orbAge / openingOrbFrameTime) % orbFrames.Length];
                    Bounds bounds = spriteRenderer.bounds;
                    // 새 구체는 가운데가 기준점이라 구슬 아래끝이 머리 위에 닿게 반지름만큼 띄운다(예전 구체는 아래 가운데 기준).
                    float lift = animatedOrb ? GiantOrbCoreRadius * scale : 0f;
                    specialOrb.transform.position = new Vector2(bounds.center.x, bounds.max.y + lift);
                }
                yield return null;
            }

            attackAnimator.PlayClip(ThrowClip, specialThrowDuration);
            yield return new WaitForSeconds(specialThrowDuration * specialReleaseTiming);

            Vector2 center = new Vector2(centerX, groundY);
            if (specialOrb != null)
            {
                Vector2 from = specialOrb.transform.position;
                for (float t = 0f; t < openingOrbDropTime; t += Time.deltaTime)
                {
                    orbAge += Time.deltaTime;
                    if (orbRenderer != null) orbRenderer.sprite = orbFrames[(int)(orbAge / openingOrbFrameTime) % orbFrames.Length];
                    specialOrb.transform.position = Vector2.Lerp(from, center, t / openingOrbDropTime);
                    yield return null;
                }
                Destroy(specialOrb);
                specialOrb = null;
            }
            attackAnimator.PlayClipLoop(ChargeClip, specialChargeCycle);
            yield return GiantBloodPillar(center, spriteRenderer.flipX ? -1f : 1f);
            StopOpeningPull();
            attackAnimator.Stop();
            if (monster.IsDead || target == null) { EndOpeningCamera(); yield break; }

            yield return PortalHopTo(new Vector3(PickTeleportX(), floorPosY, transform.position.z), keepAirborne: false);
            EndOpeningCamera();
        }

        private static IEnumerator RunThen(IEnumerator routine, System.Action done)
        {
            yield return routine;
            done();
        }

        private Transform openingFocus;
        private CameraFollow openingCamera;

        private void BeginOpeningCamera()
        {
            var cam = Camera.main;
            openingCamera = cam != null ? cam.GetComponent<CameraFollow>() : null;
            if (openingCamera == null || target == null) return;
            openingFocus = new GameObject("LunabelOpeningFocus").transform;
            UpdateOpeningFocus();
            openingCamera.SetTarget(openingFocus, 0.5f);
            openingCamera.ZoomTo(openingCameraSize, 0.8f);
        }

        // 두 사람 사이 가로 한가운데, 높이는 플레이어 발(바닥 기준을 그대로 두어야 카메라가 땅 밑을 비추지 않는다).
        private void UpdateOpeningFocus()
        {
            if (openingFocus == null || target == null) return;
            openingFocus.position = new Vector3((target.transform.position.x + transform.position.x) * 0.5f, target.transform.position.y, 0f);
        }

        private void EndOpeningCamera()
        {
            if (openingCamera != null)
            {
                if (target != null) openingCamera.SetTarget(target.transform, 0.5f);
                openingCamera.ResetZoom(0.8f);
                StartCoroutine(ResetCameraSmoothTimeLater(openingCamera));
            }
            if (openingFocus != null) Destroy(openingFocus.gameObject);
            openingFocus = null;
            openingCamera = null;
        }

        private static IEnumerator ResetCameraSmoothTimeLater(CameraFollow cameraFollow)
        {
            yield return new WaitForSeconds(1f);
            if (cameraFollow != null) cameraFollow.ResetSmoothTime();
        }

        // 차원문을 타고 destination(발 위치)으로 옮긴다: 빨려 듦 → 차원문이 오그라듦 → 도착지에 차원문이 열림 → 걸어 나옴.
        // keepAirborne이면 도착한 뒤에도 중력 없이 그 자리(공중)에 떠 있다.
        private IEnumerator PortalHopTo(Vector3 destination, bool keepAirborne)
        {
            var bodyCollider = GetComponent<Collider2D>();
            SetHealthBarVisible(false);
            FacePlayer();
            if (attackAnimator.HasClip(PortalEnterClip)) yield return PlayPortalClip(PortalEnterClip);

            SetVisible(false);
            if (bodyCollider != null) bodyCollider.enabled = false;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            StartCoroutine(PlayPortalCollapse(PortalCenter(), reverse: false));
            yield return new WaitForSeconds(teleportCollapseDuration + teleportGap);

            transform.position = destination;
            FacePlayer();
            StartCoroutine(PlayPortalCollapse(PortalCenter(), reverse: true));
            yield return new WaitForSeconds(teleportCollapseDuration);

            SetVisible(true);
            SetHealthBarVisible(false);
            FacePlayer();
            if (attackAnimator.HasClip(PortalExitClip)) yield return PlayPortalClip(PortalExitClip);
            SetHealthBarVisible(true);
            if (bodyCollider != null) bodyCollider.enabled = true;
            if (!keepAirborne) body.bodyType = RigidbodyType2D.Dynamic;
        }

        private bool pullActive;

        // 맵 가운데(centerX)로 플레이어를 끌어당긴다. 처음엔 약하다가 pullRampTime 동안 pullMaxSpeed까지 세진다.
        private IEnumerator PullPlayerRoutine(float centerX)
        {
            var movement = target != null ? target.GetComponent<CharacterMovement2D>() : null;
            if (movement == null) yield break;
            for (float t = 0f; pullActive && !monster.IsDead && !target.IsDead; t += Time.deltaTime)
            {
                float strength = Mathf.Lerp(pullStartSpeed, pullMaxSpeed, t / pullRampTime);
                float dx = centerX - target.transform.position.x;
                movement.ExternalPullX = Mathf.Abs(dx) < 0.15f ? 0f : Mathf.Sign(dx) * strength;
                yield return null;
            }
            movement.ExternalPullX = 0f;
        }

        private void StopOpeningPull()
        {
            pullActive = false;
            var movement = target != null ? target.GetComponent<CharacterMovement2D>() : null;
            if (movement != null) movement.ExternalPullX = 0f;
        }

        private IEnumerator GiantBloodPillar(Vector2 bottomCenter, float facing)
        {
            var rise = LoadSorted(BloodPillarRisePath);
            var tornado = LoadSorted(BloodPillarTornadoPath);
            if (rise.Length == 0) yield break;
            var go = new GameObject("LunabelGiantPillar");
            go.transform.position = bottomCenter;
            go.transform.localScale = new Vector3(openingPillarScale, openingPillarScale, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 5;
            renderer.flipX = facing < 0f;
            patternEffects.Add(go);
            float hitHeight = rise[rise.Length - 1].bounds.size.y * openingPillarScale;

            // 예고: 바닥에 핏물이 번져 들끓는다.
            renderer.sprite = rise[0];
            for (float t = 0f; t < openingWarnTime; t += Time.deltaTime)
            {
                if (go == null) yield break;
                renderer.color = new Color(1f, 1f, 1f, 0.4f + 0.3f * Mathf.Sin(t * 30f));
                yield return null;
            }
            renderer.color = Color.white;

            var cam = Camera.main;
            var cameraFollow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            if (cameraFollow != null) cameraFollow.Shake(openingTornadoTime + 0.4f, 0.4f);

            // 솟구치는 동안과 회오리치는 동안 내내, 기둥 안에 있으면 openingHitInterval마다 연타(넉백/피격 무적 없이)로 맞는다.
            var loop = tornado.Length > 0 ? tornado : new[] { rise[rise.Length - 1] };
            float riseTime = rise.Length * 0.06f;
            float total = riseTime + openingTornadoTime;
            float nextHit = 0.12f; // 세 번째 장(기둥이 솟기 시작)부터 맞는다
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                if (go == null) yield break;
                renderer.sprite = t < riseTime ? rise[Mathf.Min(rise.Length - 1, Mathf.FloorToInt(t / 0.06f))]
                    : loop[Mathf.FloorToInt((t - riseTime) / 0.07f) % loop.Length];
                while (t >= nextHit)
                {
                    HitPlayerInBox(bottomCenter, openingPillarHitWidth, hitHeight, openingDamageMultiplier, hitReaction: false);
                    nextHit += openingHitInterval;
                }
                yield return null;
            }
            yield return FadeAndDestroy(go, renderer, 0.4f);
        }

        private static Sprite[] LoadSorted(string path)
        {
            var loaded = Resources.LoadAll<Sprite>(path);
            System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
            return loaded;
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
                    handSpeed, handTravelRange, handHitRadius, handHitInterval);
            }

            yield return new WaitForSeconds(castAnimDuration * (1f - castFireTiming));
            isCasting = false;
        }

        // 필드 필살기: 손을 하늘로 뻗어 핏빛 구체를 모은 뒤 앞쪽 땅으로 던진다. 구체가 땅에 닿으면 그 자리부터 앞으로
        // 피의 기둥이 차례로 솟구치고, 솟는 기둥 안에 있으면 무적 없이 연타로 맞는다. 대시 무적으로 뚫거나
        // 루나벨 뒤쪽으로 돌아가면 피할 수 있다. 시전 중에는 경직되지 않는다.
        private IEnumerator SpecialRoutine()
        {
            isCasting = true;
            MarkSpecialMoveUsed();
            float facing = spriteRenderer.flipX ? -1f : 1f;

            attackAnimator.PlayClip(SpecialClip, specialRaiseDuration);
            yield return new WaitForSeconds(specialRaiseDuration);

            // 머리 위로 든 두 손 사이에서 진홍빛 에너지 구체(개막과 같은 6프레임 반복)가 점점 커진다.
            attackAnimator.PlayClipLoop(ChargeClip, specialChargeCycle);
            var orbFrames = LoadSorted(GiantOrbPath);
            bool animatedOrb = orbFrames.Length > 0;
            if (!animatedOrb)
            {
                var fallback = Resources.Load<Sprite>(BloodOrbPath + "/orb_1");
                if (fallback != null) orbFrames = new[] { fallback };
            }
            float orbScale = animatedOrb ? specialGiantOrbScale : specialOrbScale;
            SpriteRenderer orbRenderer = null;
            if (orbFrames.Length > 0)
            {
                specialOrb = new GameObject("LunabelSpecialOrb");
                orbRenderer = specialOrb.AddComponent<SpriteRenderer>();
                orbRenderer.sprite = orbFrames[0];
                orbRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            }
            float orbAge = 0f;
            for (float t = 0f; t < specialChargeDuration; t += Time.deltaTime)
            {
                if (monster.IsDead) { EndSpecial(); yield break; }
                orbAge += Time.deltaTime;
                if (specialOrb != null)
                {
                    float grow = Mathf.SmoothStep(0.15f, 1f, t / specialChargeDuration);
                    float pulse = 1f + 0.06f * Mathf.Sin(t * 18f);
                    specialOrb.transform.localScale = Vector3.one * (orbScale * grow * pulse);
                    orbRenderer.sprite = orbFrames[(int)(orbAge / openingOrbFrameTime) % orbFrames.Length];
                    specialOrb.transform.position = OrbHoldPosition(orbFrames[0], animatedOrb, orbScale);
                }
                yield return null;
            }

            attackAnimator.PlayClip(ThrowClip, specialThrowDuration);
            yield return new WaitForSeconds(specialThrowDuration * specialReleaseTiming);
            if (monster.IsDead) { EndSpecial(); yield break; }

            // 앞으로 뻗은 손에서 앞쪽 땅으로 구체가 내리꽂힌다.
            float groundY = GroundY();
            Vector2 landing = new Vector2(transform.position.x + facing * specialLandForward, groundY);
            if (specialOrb != null)
            {
                Vector2 from = specialOrb.transform.position;
                for (float t = 0f; t < specialOrbFlightTime; t += Time.deltaTime)
                {
                    orbAge += Time.deltaTime;
                    if (orbRenderer != null) orbRenderer.sprite = orbFrames[(int)(orbAge / openingOrbFrameTime) % orbFrames.Length];
                    specialOrb.transform.position = Vector2.Lerp(from, landing, t / specialOrbFlightTime);
                    yield return null;
                }
                Destroy(specialOrb);
                specialOrb = null;
            }

            // 떨어진 자리부터 앞으로 피의 기둥이 번져 간다. 기둥마다 솟는 동안 그 안에 있으면 무적 없이 연타로 맞는다.
            for (int i = 0; i < specialBurstCount; i++)
            {
                if (monster.IsDead) break;
                Vector2 burst = new Vector2(landing.x + facing * specialBurstSpacing * i, groundY);
                var pillar = FlipbookSpriteEffect.Create("LunabelSpecialBurst", BloodPillarRisePath, burst, 0.06f, loop: false, sortingOrder: 3);
                pillar.GetComponent<SpriteRenderer>().flipX = facing < 0f;
                StartCoroutine(SpecialBurstHits(burst));
                yield return new WaitForSeconds(specialBurstInterval);
            }

            castTimer = castCooldown * phaseTwoCooldownScale;
            EndSpecial();
        }

        private IEnumerator SpecialBurstHits(Vector2 burst)
        {
            for (float t = 0f; t < specialBurstHitTime && !monster.IsDead; t += specialHitInterval)
            {
                HitPlayerInBox(burst, specialBurstWidth, specialBurstHeight, specialDamageMultiplier, hitReaction: false);
                yield return new WaitForSeconds(specialHitInterval);
            }
        }

        private void EndSpecial()
        {
            if (specialOrb != null) Destroy(specialOrb);
            specialOrb = null;
            attackAnimator.Stop();
            isCasting = false;
            castRoutine = null;
        }

        // 기 모으기 그림(Charge)에서 머리 위로 든 두 손이 맞닿은 곳은 그림 위끝에서 키의 약 15% 아래, 가로 가운데다.
        // 새 에너지 구체는 가운데가 기준점이라 구슬 한가운데를 거기에 맞춘다(예전 구체는 아래 가운데 기준이라 반만큼 내렸다 —
        // 그러면 구슬이 손보다 한참 아래 머리 쪽에 걸려 어색했다).
        private Vector2 OrbHoldPosition(Sprite orbSprite, bool centerPivot, float scale)
        {
            Bounds bounds = spriteRenderer.bounds;
            float handsY = bounds.max.y - bounds.size.y * 0.15f;
            if (centerPivot) return new Vector2(bounds.center.x, handsY);
            float orbHalf = orbSprite.bounds.size.y * scale * 0.5f;
            return new Vector2(bounds.center.x, handsY - orbHalf);
        }

        private float GroundY()
        {
            var collider = GetComponent<Collider2D>();
            return collider != null ? collider.bounds.min.y : transform.position.y;
        }

        private void OnDestroy()
        {
            if (specialOrb != null) Destroy(specialOrb);
            foreach (var effect in patternEffects) if (effect != null) Destroy(effect);
            if (pullActive) StopOpeningPull();
            if (openingFocus != null) EndOpeningCamera();
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

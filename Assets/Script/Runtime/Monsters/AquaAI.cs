using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Combat;
using Aethoria.Stages;
using Aethoria.UI;

namespace Aethoria.Monsters
{
    // 물의 여왕 아쿠아 2차전(바닷속 고정 투기장). 바다 추격전(SeaChaseController)에서 이기면 곧바로 이어진다.
    // 네크로시아는 해저 바닥에 서서 평소처럼 싸우고, 아쿠아를 Z/스킬로 직접 때린다. 아쿠아는 바닥 조금 위에 떠서
    // 네크로시아와 거리를 두고 맴돌다가 세 패턴을 돌아가며 쓴다.
    //   물의 드릴: 변신하는 동안 바닥 높이 경고선 → 투기장을 가로질러 뚫고 지나간 뒤, 화면 위쪽 양옆에서 번갈아
    //             네크로시아를 겨냥해 대각선으로 다시 날아온다(점프/발판/대시로 피한다).
    //   물의 창 난사: 창을 내지르며 물의 창 다섯 줄기를 부채꼴로 쏜다. 여러 번 연달아.
    //   푸른 번개: 세로 경고 기둥 뒤에 위에서 번개가 내리꽂힌다. 한 줄기는 늘 네크로시아 자리를 노린다.
    //   추격 번개: 네크로시아를 쫓아가며 그 자리에 번개가 연달아 다섯 번 떨어진다(멈추지 말고 계속 움직여 피한다).
    //   물의 소용돌이 흡입: 손을 내밀어 눕힌 회오리 깔때기로 끌어당긴다(대시 불가). 붙잡히면 둥근 소용돌이에 갇혀 연타 피해.
    // 드릴 → 추격 번개 → 흡입 → 물의 창 → 푸른 번개 순서로 돌아간다.
    // 체력이 절반 아래로 떨어지면 몸빛이 짙어지고 쉬는 틈이 짧아지며, 드릴 재돌진·창·번개 횟수가 늘어난다.
    // 몸에 닿아도 피해를 입는다. 쓰러지면 대사를 나눈 뒤 마을(노아 앞)로 돌아간다.
    [RequireComponent(typeof(Monster))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class AquaAI : MonoBehaviour
    {
        private const string IdlePath = "Art/Monsters/Aqua/Idle";
        private const string ThrustPath = "Art/Monsters/Aqua/Thrust";
        private const string DrillTransformPath = "Art/Monsters/Aqua/DrillTransform";
        private const string DrillPath = "Art/Monsters/Aqua/Drill";
        private const string BoltPath = "Art/Monsters/Aqua/Lightning";
        private const string LancePath = "Art/Effects/SlimeWaterBall";
        private const string PullPath = "Art/Monsters/Aqua/Pull";
        private const string StreamPath = "Art/Effects/AquaTornado";
        private const string TrapPath = "Art/Effects/AquaVortex";
        // 흡입 모션(8장)은 앞 네 장이 손을 내미는 동작, 뒤 네 장이 손끝에 소용돌이가 휘도는 동작이다.
        private const int PullLoopFirstFrame = 4;
        // 흡입 모션에서 내민 손끝(소용돌이 가운데)은 그림 기준점(아래 가운데)보다 앞쪽 1.31, 위 2.15유닛에 있다(443px 칸의 (393, 161)).
        private static readonly Vector2 HandOffset = new Vector2(1.31f, 2.15f);

        // 그림(916x564 칸, 240px = 1칸, 피벗 아래 가운데)은 소용돌이 몸통이 칸 한가운데에 오게 잘려 있다.
        // 오브젝트째 키워서 키 약 3.4유닛(알리아스보다 조금 크게)으로 쓴다.
        public const float VisualScale = 1.45f;
        private static readonly Vector2 CenterOffset = new Vector2(0f, 564f / 240f * VisualScale / 2f);
        // 실제 몸통(소용돌이 폭, 몸 키)만 맞는 범위. 캔버스는 뻗은 창까지 담아 넓다.
        private static readonly Vector2 BodyHitSize = new Vector2(2.2f, 2.9f);
        public static Vector2 BodyColliderSize => BodyHitSize / VisualScale;
        public static Vector2 BodyColliderOffset => CenterOffset / VisualScale;
        private const float HoverLift = 0.2f;   // 캔버스 아래끝이 바닥보다 이만큼 위(몸통 아래가 Z 공격 높이 안에 들어온다)
        private const float FrameRate = 8f;
        private const int DrillLoopFirstFrame = 3;   // 회전 시트 앞 세 장은 작게 시작하는 장면이라 뒤쪽만 반복한다

        [Header("이동")]
        [SerializeField] private float moveSpeed = 2.6f;
        [SerializeField] private float keepDistance = 4f;
        [SerializeField] private float edgeMargin = 1.6f;
        [SerializeField] private float bobAmplitude = 0.12f;
        [SerializeField] private float restPhaseOne = 1.5f;
        [SerializeField] private float restPhaseTwo = 0.9f;
        [SerializeField, Range(0f, 1f)] private float phaseTwoFraction = 0.5f;

        [Header("몸통 접촉")]
        [SerializeField] private float contactInterval = 1f;
        [SerializeField] private float contactDamageMultiplier = 0.8f;

        [Header("물의 드릴")]
        [SerializeField] private float drillChargeTime = 1.1f;
        [SerializeField] private float drillDuration = 0.75f;
        [SerializeField] private float drillLaneHeight = 1.8f;
        [SerializeField] private float drillLaneLift = 1.0f;          // 경고선 가운데가 바닥보다 이만큼 위(서 있으면 맞고, 발판 위는 안전)
        [SerializeField] private float drillTravelMargin = 4f;        // 드릴 전체가 화면 밖으로 나갈 때까지 달린다
        [SerializeField] private int drillPassesPhaseOne = 2;
        [SerializeField] private int drillPassesPhaseTwo = 3;
        [SerializeField] private float drillDiagonalWarnTime = 0.6f;
        [SerializeField] private float drillDiagonalDuration = 0.55f;
        [SerializeField] private float drillDiagonalStartMargin = 3.5f;
        [SerializeField] private float drillDiagonalStartHeight = 5.5f; // 바닥 위 이 높이(화면 위쪽)에서 출발한다
        [SerializeField] private float drillReturnTime = 0.8f;
        [SerializeField] private float drillDamageMultiplier = 1.2f;
        // 드릴이 기울어져도 맞도록 드릴 축을 따라가는 캡슐로 판정한다(추격전 값을 이 크기에 맞춰 줄인 것).
        private const float DrillCoreBack = 1.55f;
        private const float DrillCoreFront = 2.05f;
        private const float DrillCoreRadius = 0.77f;
        private const float DrillTipLift = 0.12f;

        [Header("물의 창 난사")]
        [SerializeField] private int lanceVolleysPhaseOne = 2;
        [SerializeField] private int lanceVolleysPhaseTwo = 3;
        [SerializeField] private int lancesPerVolley = 5;
        [SerializeField] private float lanceSpreadDegrees = 22f;     // 이웃한 창 사이 각도
        [SerializeField] private float lanceWindup = 0.55f;
        [SerializeField] private float lanceVolleyGap = 0.45f;
        [SerializeField] private float lanceSpeed = 8.5f;
        [SerializeField] private float lanceLength = 1.7f;
        [SerializeField] private float lanceHitRadius = 0.35f;
        [SerializeField] private float lanceDamageMultiplier = 0.8f;

        [Header("푸른 번개")]
        [SerializeField] private int boltWavesPhaseOne = 3;
        [SerializeField] private int boltWavesPhaseTwo = 4;
        [SerializeField] private int boltsMin = 3;
        [SerializeField] private int boltsMax = 5;
        [SerializeField] private float boltWarnTimePhaseOne = 0.85f;
        [SerializeField] private float boltWarnTimePhaseTwo = 0.7f;
        [SerializeField] private float boltWaveGap = 0.25f;
        [SerializeField] private float boltDuration = 0.45f;
        [SerializeField] private float boltColumnWidth = 1.3f;
        [SerializeField] private float boltTopAboveFloor = 6.8f;     // 구름이 화면 위끝(바닥 위 약 6.4)을 살짝 넘는다
        [SerializeField] private float boltDamageMultiplier = 1f;
        private const float BoltHitStart = 1f / 6f;
        private const float BoltHitEnd = 5f / 6f;

        [Header("물의 소용돌이 흡입")]
        // 손을 내밀어 회오리 깔때기(세로 회오리를 눕힌 것)로 네크로시아를 끌어당긴다. 대시로는 빠져나갈 수 없고, 처음엔 걸어서
        // 버틸 수 있지만 점점 세진다. 흡입이 끝날 때까지 반대쪽으로 걸어 버티면 피할 수 있다(멀리 있을수록 유리하다).
        // 아쿠아 앞까지 끌려오면 둥근 소용돌이에 갇혀 연타 피해를 입고, 마지막에 소용돌이가 터지며 튕겨 나간다.
        [SerializeField] private float suctionWindup = 0.7f;
        [SerializeField] private float suctionDurationPhaseOne = 2.6f;
        [SerializeField] private float suctionDurationPhaseTwo = 3.2f;
        [SerializeField] private float pullStartSpeed = 2.2f;     // 걷기 속도(4)보다 약해서 처음엔 걸어서 버틸 수 있다
        [SerializeField] private float pullMaxSpeed = 5.2f;       // 끝무렵엔 걷기보다 세서 끌려온다
        [SerializeField] private float pullRampTime = 2f;
        [SerializeField] private float streamLength = 6.5f;       // 깔때기가 손끝에서 뻗는 길이
        [SerializeField] private float streamWidth = 2.4f;        // 깔때기 입구 쪽 굵기
        [SerializeField] private float captureReach = 1.4f;       // 몸 가운데에서 앞쪽으로 이만큼까지 끌려오면 붙잡힌다
        [SerializeField] private float trapDuration = 1.6f;
        [SerializeField] private int trapHits = 8;
        [SerializeField] private float trapHitDamageMultiplier = 0.22f;
        [SerializeField] private float trapFinisherMultiplier = 0.6f;
        [SerializeField] private float trapSize = 2.4f;
        [SerializeField] private float trapSpinSpeed = 360f;      // 둥근 소용돌이가 도는 속도(도/초)

        [Header("추격 번개")]
        // 네크로시아가 서 있는 자리에 경고 기둥이 뜨고 잠시 뒤 번개가 떨어지기를 연달아 되풀이한다(멈춰 있으면 맞는다).
        [SerializeField] private int chaseStrikes = 5;
        [SerializeField] private float chaseWarnTime = 0.5f;
        [SerializeField] private float chaseIntervalPhaseOne = 0.38f;
        [SerializeField] private float chaseIntervalPhaseTwo = 0.3f;

        private static readonly Color DrillWarnColor = new Color(1f, 0.25f, 0.2f);
        private static readonly Color BoltWarnColor = new Color(0.45f, 0.75f, 1f);
        private static readonly Color CastColor = new Color(0.7f, 0.85f, 1f);
        private static readonly Color PhaseTwoColor = new Color(0.72f, 0.82f, 1f);
        private static readonly Color HurtColor = new Color(1f, 0.55f, 0.55f);
        private const float HurtFlashTime = 0.12f;

        private const string NextHint = "   <color=#B98CE8>[Z]</color>";
        private const string SkipHint = "   <color=#B98CE8>[Esc] 스킵</color>";

        private static readonly (bool aqua, string line)[] IntroLines =
        {
            (true, "바위 뒤에 숨는 꾀는 여기까지란다. 이곳은 내 왕좌가 잠든 바다의 심장."),
            (false, "...발 디딜 바닥이 있군. 그거면 충분해."),
            (true, "그 낫이 파도보다 빠른지 보자꾸나, 작은 사신."),
        };

        private static readonly (bool aqua, string line)[] DefeatLines =
        {
            (true, "...파도가 갈라졌어. 내 바다에서 내가 무릎을 꿇다니."),
            (false, "붉은 달에 물든 건 정령들만이 아니었군. 당신의 바다도 아파하고 있었어."),
            (true, "...그래, 이 물결은 내 것이 아니었는지도 모르지. 가렴, 작은 사신. 이번엔 정말로 길을 열어 주마."),
        };

        private enum Pattern { Drill, Lance, Lightning, ChaseLightning, Suction }
        private static readonly Pattern[] PatternOrder = { Pattern.Drill, Pattern.ChaseLightning, Pattern.Suction, Pattern.Lance, Pattern.Lightning };

        private Monster monster;
        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;
        private Character target;
        private Collider2D targetCollider;
        private CharacterMovement2D targetMovement;
        private NpcDialogueUI dialogueUI;
        private bool introSkippable;
        private Action onIntroShown;
        private Action onReveal;
        private Action onFinished;
        private float arenaMinX;
        private float arenaMaxX;
        private float floorY;

        private Sprite[] idleFrames;
        private Sprite[] thrustFrames;
        private Sprite[] drillTransformFrames;
        private Sprite[] drillLoopFrames;
        private Sprite[] boltFrames;
        private Sprite[] lanceFrames;
        private Sprite[] pullStartFrames;
        private Sprite[] pullLoopFrames;
        private Sprite[] streamFrames;
        private Sprite[] trapFrames;
        private Rigidbody2D targetBody;
        private CharacterSkillDash targetDash;
        // 흡입/가두기가 플레이어에게 걸어 둔 상태. 패턴이 중간에 끊겨도(아쿠아가 쓰러짐 등) ReleasePlayer로 되돌린다.
        private bool pulling;
        private bool trapping;
        private float savedGravity;
        private Sprite[] poseFrames;
        private float poseStart;
        private float poseDuration;
        private bool poseLoops;
        private float animationTime;

        private bool fighting;
        private bool drilling;
        private bool casting;
        private bool phaseTwo;
        private bool talking;
        private float contactTimer;
        private float hurtUntil;
        private float bobTime;
        private Transform effectsRoot;
        private Image fade;

        private class Lance
        {
            public Transform Root;
            public SpriteRenderer Renderer;
            public Vector2 Direction;
            public float Traveled;
        }
        private readonly List<Lance> lances = new List<Lance>();

        private float HoverCenterY => floorY + HoverLift + CenterOffset.y;
        private float MaxTravel => (arenaMaxX - arenaMinX) + drillTravelMargin * 2f;

        public void Initialize(Character player, NpcDialogueUI dialogue, float minX, float maxX, float groundY,
            bool skippable, Action introShown, Action reveal, Action finished)
        {
            target = player;
            targetCollider = player.GetComponent<Collider2D>();
            targetMovement = player.GetComponent<CharacterMovement2D>();
            targetBody = player.GetComponent<Rigidbody2D>();
            targetDash = player.GetComponent<CharacterSkillDash>();
            dialogueUI = dialogue;
            arenaMinX = minX;
            arenaMaxX = maxX;
            floorY = groundY;
            introSkippable = skippable;
            onIntroShown = introShown;
            onReveal = reveal;
            onFinished = finished;
        }

        private void Awake()
        {
            monster = GetComponent<Monster>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            spriteRenderer.sortingOrder = 5;
            monster.DestroyOnDeath = false;
            monster.OnDied += HandleDied;
            monster.OnDamaged += (_, __) => hurtUntil = Time.time + HurtFlashTime;

            idleFrames = LoadSorted(IdlePath);
            thrustFrames = LoadSorted(ThrustPath);
            drillTransformFrames = LoadSorted(DrillTransformPath);
            var drill = LoadSorted(DrillPath);
            drillLoopFrames = new Sprite[Mathf.Max(0, drill.Length - DrillLoopFirstFrame)];
            if (drillLoopFrames.Length > 0) Array.Copy(drill, DrillLoopFirstFrame, drillLoopFrames, 0, drillLoopFrames.Length);
            boltFrames = LoadSorted(BoltPath);
            lanceFrames = LoadSorted(LancePath);
            var pull = LoadSorted(PullPath);
            int split = Mathf.Min(PullLoopFirstFrame, pull.Length);
            pullStartFrames = new Sprite[split];
            Array.Copy(pull, 0, pullStartFrames, 0, split);
            pullLoopFrames = new Sprite[pull.Length - split];
            Array.Copy(pull, split, pullLoopFrames, 0, pullLoopFrames.Length);
            streamFrames = LoadSorted(StreamPath);
            trapFrames = LoadSorted(TrapPath);
            if (idleFrames.Length > 0) spriteRenderer.sprite = idleFrames[0];
        }

        private static Sprite[] LoadSorted(string path)
        {
            var frames = Resources.LoadAll<Sprite>(path) ?? new Sprite[0];
            Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            return frames;
        }

        private IEnumerator Start()
        {
            var holder = new GameObject("AquaEffects");
            holder.transform.SetParent(transform.parent, false);
            effectsRoot = holder.transform;
            fade = SeaChaseController.CreateFade(effectsRoot, SeaChaseController.DeepWaterColor);
            SetFadeAlpha(1f);

            yield return null;
            yield return Intro();
            onReveal?.Invoke();
            fighting = true;

            int next = 0;
            while (!monster.IsDead)
            {
                yield return Rest(phaseTwo ? restPhaseTwo : restPhaseOne);
                if (monster.IsDead) yield break;
                if (!phaseTwo && monster.CurrentHp <= monster.MaxHp * phaseTwoFraction) yield return EnterPhaseTwo();

                switch (PatternOrder[next])
                {
                    case Pattern.Drill: yield return DrillRoutine(); break;
                    case Pattern.Lance: yield return LanceRoutine(); break;
                    case Pattern.ChaseLightning: yield return ChaseLightningRoutine(); break;
                    case Pattern.Suction: yield return SuctionRoutine(); break;
                    default: yield return LightningRoutine(); break;
                }
                next = (next + 1) % PatternOrder.Length;
            }
        }

        private void Update()
        {
            animationTime += Time.deltaTime;
            AnimatePose();
            if (body != null) body.linearVelocity = Vector2.zero; // 스킬 넉백 등이 떠 있는 몸을 밀어내지 않게
            spriteRenderer.color = Time.time < hurtUntil ? HurtColor
                : casting ? Color.Lerp(BaseColor, CastColor, Mathf.PingPong(Time.time * 6f, 1f))
                : BaseColor;
            UpdateLances();
            TryContactDamage();
        }

        private Color BaseColor => phaseTwo ? PhaseTwoColor : Color.white;

        // ── 포즈 ─────────────────────────────────────────────

        private void PlayPose(Sprite[] frames, float duration, bool loops)
        {
            poseFrames = frames;
            poseStart = animationTime;
            poseDuration = duration;
            poseLoops = loops;
        }

        private void AnimatePose()
        {
            if (poseFrames != null && poseFrames.Length > 0)
            {
                int count = poseFrames.Length;
                int index = Mathf.FloorToInt((animationTime - poseStart) / poseDuration * count);
                spriteRenderer.sprite = poseFrames[poseLoops ? index % count : Mathf.Clamp(index, 0, count - 1)];
                return;
            }
            if (idleFrames.Length > 0) spriteRenderer.sprite = idleFrames[Mathf.FloorToInt(animationTime * FrameRate) % idleFrames.Length];
        }

        // ── 위치 ─────────────────────────────────────────────

        private Vector2 Center => (Vector2)transform.position + (Vector2)(transform.rotation * (Vector3)CenterOffset);

        private void SetCenter(Vector2 center)
        {
            transform.position = (Vector3)center - transform.rotation * (Vector3)CenterOffset;
        }

        private void SetAngle(float degrees)
        {
            Vector2 center = Center;
            transform.rotation = Quaternion.Euler(0f, 0f, degrees);
            SetCenter(center);
        }

        private void FacePlayer()
        {
            if (target != null) spriteRenderer.flipX = target.transform.position.x < Center.x;
        }

        private Vector2 PlayerCenter => targetCollider != null ? (Vector2)targetCollider.bounds.center : (Vector2)target.transform.position + Vector2.up * 0.75f;

        // 네크로시아와 keepDistance만큼 떨어진 자리(지금 있는 쪽)로 떠다니며 기다린다.
        private IEnumerator Rest(float duration)
        {
            for (float t = 0f; t < duration && !monster.IsDead; t += Time.deltaTime)
            {
                if (!monster.IsImmobilized && target != null)
                {
                    float px = target.transform.position.x;
                    float side = Center.x >= px ? 1f : -1f;
                    float desiredX = Mathf.Clamp(px + side * keepDistance, arenaMinX + edgeMargin, arenaMaxX - edgeMargin);
                    // 벽에 몰리면 반대편으로 넘어간다.
                    if (Mathf.Abs(desiredX - px) < keepDistance * 0.6f)
                        desiredX = Mathf.Clamp(px - side * keepDistance, arenaMinX + edgeMargin, arenaMaxX - edgeMargin);
                    bobTime += Time.deltaTime;
                    var center = Center;
                    center.x = Mathf.MoveTowards(center.x, desiredX, moveSpeed * Time.deltaTime);
                    center.y = Mathf.MoveTowards(center.y, HoverCenterY + Mathf.Sin(bobTime * 2.2f) * bobAmplitude, moveSpeed * Time.deltaTime);
                    SetCenter(center);
                }
                FacePlayer();
                yield return null;
            }
        }

        // ── 등장 / 2페이즈 / 퇴장 ──────────────────────────────

        private IEnumerator Intro()
        {
            targetMovement?.SetCutsceneLocked(true);
            spriteRenderer.flipX = true;
            SetCenter(new Vector2(arenaMaxX + 3f, HoverCenterY));
            StartCoroutine(SeaChaseController.FadeTo(fade, 0f, 0.6f));

            var from = Center;
            var to = new Vector2(arenaMaxX - 3f, HoverCenterY);
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                SetCenter(Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            SetCenter(to);

            onIntroShown?.Invoke();
            yield return Talk(IntroLines, introSkippable);
            dialogueUI?.Hide();
            talking = false;
            targetMovement?.SetCutsceneLocked(false);
        }

        private IEnumerator EnterPhaseTwo()
        {
            phaseTwo = true;
            casting = true;
            Shake(0.6f, 0.3f);
            yield return new WaitForSeconds(0.7f);
            casting = false;
        }

        private void HandleDied(Monster _)
        {
            StopAllCoroutines();
            ReleasePlayer();
            ClearEffects();
            fighting = false;
            drilling = false;
            casting = false;
            StartCoroutine(DefeatRoutine());
        }

        private IEnumerator DefeatRoutine()
        {
            targetMovement?.SetCutsceneLocked(true);
            poseFrames = null;
            transform.rotation = Quaternion.identity;
            Shake(0.5f, 0.3f);

            // 화면 밖에서 쓰러졌을 수도 있으니 대화하는 동안 보이는 자리로 데려온다.
            Vector2 from = Center;
            var to = new Vector2(Mathf.Clamp(from.x, arenaMinX + 2.5f, arenaMaxX - 2.5f), HoverCenterY - 0.3f);
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.9f);
                SetCenter(Vector2.Lerp(from, to, k) + new Vector2(Mathf.Sin(t * 40f) * 0.12f * (1f - k), 0f));
                yield return null;
            }
            SetCenter(to);
            FacePlayer();

            yield return Talk(DefeatLines, false);
            dialogueUI?.Hide();
            talking = false;

            // 물거품처럼 옅어지며 떠오른다.
            Vector2 start = Center;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                SetCenter(start + Vector2.up * (t * 0.8f));
                var color = spriteRenderer.color;
                color.a = 1f - t / 1.2f;
                spriteRenderer.color = color;
                enabled = false; // Update가 색을 덮어쓰지 않게
                yield return null;
            }
            yield return SeaChaseController.FadeTo(fade, 1f, 0.8f);
            targetMovement?.SetCutsceneLocked(false);
            onFinished?.Invoke();
        }

        private IEnumerator Talk((bool aqua, string line)[] lines, bool skippable)
        {
            foreach (var (aqua, line) in lines)
            {
                talking = true;
                string text = line + (skippable ? SkipHint : "") + NextHint;
                if (aqua) dialogueUI?.ShowAqua(text);
                else dialogueUI?.ShowNecrosia(text);

                yield return null; // 대사가 바뀐 그 프레임의 입력으로 바로 넘어가지 않게
                while (true)
                {
                    var keyboard = Keyboard.current;
                    if (keyboard != null && skippable && keyboard.escapeKey.wasPressedThisFrame) yield break;
                    if (keyboard != null && (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                        || keyboard.numpadEnterKey.wasPressedThisFrame)) break;
                    yield return null;
                }
            }
        }

        private void OnDestroy()
        {
            // 대화 도중 맵이 사라지면(관리자 순간이동 등) 대화창이 남지 않게 닫는다.
            if (talking) dialogueUI?.Hide();
            ReleasePlayer();
        }

        // ── 물의 드릴 ─────────────────────────────────────────

        private IEnumerator DrillRoutine()
        {
            float laneY = floorY + drillLaneLift;
            float centerY = laneY - DrillTipLift;
            float px = target.transform.position.x;
            // 네크로시아가 있는 쪽으로 돌진한다.
            float dir = px >= Center.x ? 1f : -1f;
            AimDrill(new Vector2(dir, 0f));

            PlayPose(drillTransformFrames, drillChargeTime, loops: false);
            var lane = CreateWarning(new Vector2((arenaMinX + arenaMaxX) / 2f, laneY), new Vector2(arenaMaxX - arenaMinX, drillLaneHeight), 0f);
            Vector2 start = Center;
            for (float t = 0f; t < drillChargeTime; t += Time.deltaTime)
            {
                PulseWarning(lane, DrillWarnColor, t);
                SetCenter(new Vector2(start.x, Mathf.Lerp(start.y, centerY, t / drillChargeTime)));
                yield return null;
            }
            DestroyEffect(lane);

            drilling = true;
            PlayPose(drillLoopFrames, 0.3f, loops: true);
            float fromX = Center.x;
            float toX = dir > 0f ? arenaMaxX + drillTravelMargin : arenaMinX - drillTravelMargin;
            for (float t = 0f; t < drillDuration; t += Time.deltaTime)
            {
                SetCenter(new Vector2(Mathf.Lerp(fromX, toX, t / drillDuration), centerY));
                CheckDrillHit();
                yield return null;
            }

            int passes = phaseTwo ? drillPassesPhaseTwo : drillPassesPhaseOne;
            // 방금 나간 쪽에서 먼저 돌아온다.
            bool fromRight = dir > 0f;
            for (int i = 0; i < passes; i++)
            {
                yield return DiagonalPass(fromRight);
                fromRight = !fromRight;
            }

            // 마지막으로 나간 반대쪽(fromRight가 가리키는 쪽) 화면 밖에서 원래 모습으로 미끄러져 들어온다.
            drilling = false;
            poseFrames = null;
            transform.rotation = Quaternion.identity;
            float side = fromRight ? 1f : -1f;
            var enter = new Vector2(side > 0f ? arenaMaxX + 3f : arenaMinX - 3f, HoverCenterY);
            var rest = new Vector2(side > 0f ? arenaMaxX - edgeMargin - 1f : arenaMinX + edgeMargin + 1f, HoverCenterY);
            SetCenter(enter);
            FacePlayer();
            for (float t = 0f; t < drillReturnTime; t += Time.deltaTime)
            {
                SetCenter(Vector2.Lerp(enter, rest, Mathf.SmoothStep(0f, 1f, t / drillReturnTime)));
                yield return null;
            }
        }

        // 화면 위쪽 한옆 밖에서 지금 네크로시아가 있는 자리를 겨냥해 반대쪽 밖까지 가로지른다.
        private IEnumerator DiagonalPass(bool fromRight)
        {
            Vector2 aim = PlayerCenter;
            float side = fromRight ? 1f : -1f;
            float halfWidth = (arenaMaxX - arenaMinX) / 2f;
            float midX = (arenaMinX + arenaMaxX) / 2f;
            var start = new Vector2(midX + side * (halfWidth + drillDiagonalStartMargin), floorY + drillDiagonalStartHeight);
            Vector2 direction = (aim - start).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float length = Mathf.Min(MaxTravel, (halfWidth + drillDiagonalStartMargin) * 2f / Mathf.Max(0.2f, Mathf.Abs(direction.x)));

            transform.rotation = Quaternion.identity;
            SetCenter(start);
            AimDrill(direction);

            var lane = CreateWarning(aim, new Vector2(length, drillLaneHeight), angle);
            for (float t = 0f; t < drillDiagonalWarnTime; t += Time.deltaTime)
            {
                PulseWarning(lane, DrillWarnColor, t);
                yield return null;
            }
            DestroyEffect(lane);

            Vector2 end = start + direction * length;
            for (float t = 0f; t < drillDiagonalDuration; t += Time.deltaTime)
            {
                SetCenter(Vector2.Lerp(start, end, t / drillDiagonalDuration));
                CheckDrillHit();
                yield return null;
            }
        }

        // 드릴 끝이 direction을 향하게 한다. 그림은 오른쪽을 향해 있어서 왼쪽으로 갈 때는 좌우를 뒤집고 그만큼 각도를 돌린다.
        // (위아래 뒤집기는 그림 기준점인 아래 가운데를 축으로 뒤집혀 몸이 키만큼 아래로 내려가 그려지므로 쓰지 않는다.
        //  좌우 뒤집기는 축이 캔버스 가로 가운데라 몸 위치가 그대로다.)
        private void AimDrill(Vector2 direction)
        {
            bool left = direction.x < 0f;
            spriteRenderer.flipX = left;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            SetAngle(left ? angle - 180f : angle);
        }

        // 드릴 끝이 향하는 방향(좌우를 뒤집었으면 그림의 오른쪽 반대).
        private Vector2 DrillAxis => transform.rotation * (spriteRenderer.flipX ? Vector3.left : Vector3.right);

        private void CheckDrillHit()
        {
            if (target == null || target.IsDead) return;
            Vector2 center = Center;
            Vector2 axis = DrillAxis;
            Vector2 lift = transform.rotation * (Vector3.up * DrillTipLift);
            Vector2 back = center + lift - axis * DrillCoreBack;
            Vector2 point = PlayerCenter;
            float along = Mathf.Clamp(Vector2.Dot(point - back, axis), 0f, DrillCoreBack + DrillCoreFront);
            float reach = DrillCoreRadius + 0.4f;
            if ((point - (back + axis * along)).sqrMagnitude <= reach * reach) HitPlayer(drillDamageMultiplier, center);
        }

        // ── 물의 소용돌이 흡입 ─────────────────────────────────

        private Vector2 HandPoint => (Vector2)transform.position + new Vector2(spriteRenderer.flipX ? -HandOffset.x : HandOffset.x, HandOffset.y);

        private IEnumerator SuctionRoutine()
        {
            if (pullLoopFrames.Length == 0 || streamFrames.Length == 0 || target == null) yield break;
            FacePlayer();
            float dir = spriteRenderer.flipX ? -1f : 1f;
            transform.rotation = Quaternion.identity;

            // 손을 내미는 동안 앞쪽 바닥 위로 경고 띠를 띄운다.
            PlayPose(pullStartFrames, suctionWindup, loops: false);
            casting = true;
            float reach = Mathf.Abs((dir > 0f ? arenaMaxX : arenaMinX) - Center.x);
            var warn = CreateWarning(new Vector2(Center.x + dir * reach / 2f, floorY + 1.4f), new Vector2(reach, 2.8f), 0f);
            for (float t = 0f; t < suctionWindup; t += Time.deltaTime)
            {
                PulseWarning(warn, BoltWarnColor, t);
                yield return null;
            }
            DestroyEffect(warn);
            casting = false;

            PlayPose(pullLoopFrames, 0.5f, loops: true);
            var stream = CreateStream();
            pulling = true;
            if (targetDash != null) targetDash.Blocked = true;

            bool captured = false;
            float duration = phaseTwo ? suctionDurationPhaseTwo : suctionDurationPhaseOne;
            for (float t = 0f; t < duration && !monster.IsDead && !target.IsDead; t += Time.deltaTime)
            {
                AimStream(stream, t);
                float strength = Mathf.Lerp(pullStartSpeed, pullMaxSpeed, t / pullRampTime);
                float captureX = Center.x + dir * captureReach;
                float px = target.transform.position.x;
                if (targetMovement != null) targetMovement.ExternalPullX = -dir * strength;
                // 몸 앞(captureX)을 넘어 아쿠아 쪽으로 들어오면 붙잡힌다.
                if ((px - captureX) * dir <= 0f && Mathf.Abs(PlayerCenter.y - Center.y) < 3f)
                {
                    captured = true;
                    break;
                }
                yield return null;
            }

            if (targetMovement != null) targetMovement.ExternalPullX = 0f;
            pulling = false;
            DestroyEffect(stream);
            if (captured) yield return TrapRoutine(dir);
            if (targetDash != null) targetDash.Blocked = false;
            poseFrames = null;
        }

        // 세로 회오리를 눕혀 손끝에서 플레이어 쪽으로 뻗게 한다. 그림 아래(좁은 밑동, 피벗)가 손끝, 위(넓은 입구)가 플레이어 쪽이다.
        private SpriteRenderer CreateStream()
        {
            var go = new GameObject("AquaSuctionStream", typeof(SpriteRenderer));
            go.transform.SetParent(effectsRoot, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = streamFrames[0];
            renderer.sortingOrder = 4;
            Vector2 size = streamFrames[0].bounds.size;
            go.transform.localScale = new Vector3(streamWidth / size.x, streamLength / size.y, 1f);
            return renderer;
        }

        private void AimStream(SpriteRenderer stream, float time)
        {
            Vector2 hand = HandPoint;
            Vector2 aim = PlayerCenter - hand;
            float dir = spriteRenderer.flipX ? -1f : 1f;
            // 플레이어가 바로 아래에 붙어 있어도 깔때기가 뒤로 꺾이지 않게 앞쪽으로만 겨눈다.
            if (aim.x * dir < 0.5f) aim.x = dir * 0.5f;
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg - 90f;
            stream.transform.SetPositionAndRotation(hand, Quaternion.Euler(0f, 0f, angle));
            stream.sprite = streamFrames[Mathf.FloorToInt(time * 14f) % streamFrames.Length];
            var color = Color.white;
            color.a = Mathf.Clamp01(time / 0.25f) * 0.9f;   // 처음엔 서서히 뻗어 나온다
            stream.color = color;
        }

        // 손 앞에 둥근 소용돌이를 만들어 네크로시아를 가두고, 빙글빙글 돌리며 연타 피해를 준다. 마지막 한 방은 넉백과 함께 튕겨 낸다.
        private IEnumerator TrapRoutine(float dir)
        {
            trapping = true;
            if (targetMovement != null) targetMovement.SetCutsceneLocked(true);
            if (targetBody != null)
            {
                savedGravity = targetBody.gravityScale;
                targetBody.gravityScale = 0f;
                targetBody.linearVelocity = Vector2.zero;
            }

            float trapX = Mathf.Clamp(Center.x + dir * (captureReach + 0.6f), arenaMinX + trapSize / 2f, arenaMaxX - trapSize / 2f);
            var trapCenter = new Vector2(trapX, floorY + 1.3f);
            var go = new GameObject("AquaTrap", typeof(SpriteRenderer));
            go.transform.SetParent(effectsRoot, false);
            go.transform.position = trapCenter;
            var trap = go.GetComponent<SpriteRenderer>();
            trap.sprite = trapFrames.Length > 0 ? trapFrames[0] : null;
            trap.sortingOrder = 20;
            float scale = trapFrames.Length > 0 ? trapSize / trapFrames[0].bounds.size.x : 1f;
            Vector2 playerOffset = targetCollider != null ? (Vector2)(targetCollider.bounds.center - target.transform.position) : Vector2.up * 0.75f;
            Shake(0.3f, 0.2f);

            float hitInterval = trapDuration / trapHits;
            float nextHit = 0f;
            int hits = 0;
            for (float t = 0f; t < trapDuration && !monster.IsDead && !target.IsDead; t += Time.deltaTime)
            {
                // 감기며 커졌다가 살짝 일렁인다.
                float grow = Mathf.Min(1f, t / 0.2f);
                float pulse = 1f + Mathf.Sin(t * 18f) * 0.04f;
                go.transform.localScale = new Vector3(scale * grow * pulse, scale * grow * pulse, 1f);
                go.transform.Rotate(0f, 0f, -trapSpinSpeed * Time.deltaTime);
                if (trapFrames.Length > 0) trap.sprite = trapFrames[Mathf.FloorToInt(t * 12f) % trapFrames.Length];
                if (targetBody != null)
                {
                    targetBody.position = trapCenter - playerOffset;
                    targetBody.linearVelocity = Vector2.zero;
                }

                if (t >= nextHit && hits < trapHits - 1)
                {
                    hits++;
                    nextHit += hitInterval;
                    // 연타는 피격 무적에 막히지 않게 넉백 없이 넣는다.
                    if (!target.IsDead)
                        target.TakeDamage(CombatMath.Finish(CombatMath.BaseDamage(monster.Attack, target.Stats.defense) * trapHitDamageMultiplier), trapCenter, hitReaction: false);
                }
                yield return null;
            }

            ReleasePlayer();
            // 소용돌이가 터지며 튕겨 낸다(마지막 한 방은 넉백 + 피격 무적).
            HitPlayer(trapFinisherMultiplier, Center);
            Shake(0.3f, 0.25f);
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                go.transform.localScale = Vector3.one * scale * (1f + t * 2f);
                trap.color = new Color(1f, 1f, 1f, 1f - t / 0.25f);
                yield return null;
            }
            Destroy(go);
        }

        // 흡입/가두기로 바꿔 둔 플레이어 상태를 모두 되돌린다. 여러 번 불러도 괜찮다.
        private void ReleasePlayer()
        {
            if (pulling && targetMovement != null) targetMovement.ExternalPullX = 0f;
            pulling = false;
            if (targetDash != null) targetDash.Blocked = false;
            if (!trapping) return;
            trapping = false;
            if (targetBody != null) targetBody.gravityScale = savedGravity;
            if (targetMovement != null) targetMovement.SetCutsceneLocked(false);
        }

        // ── 물의 창 난사 ───────────────────────────────────────

        private IEnumerator LanceRoutine()
        {
            int volleys = phaseTwo ? lanceVolleysPhaseTwo : lanceVolleysPhaseOne;
            for (int v = 0; v < volleys && !monster.IsDead; v++)
            {
                FacePlayer();
                // 창을 뒤로 당겼다가(찌르기 앞쪽 절반) 내지르는 순간 쏜다.
                PlayPose(thrustFrames, lanceWindup * 2f, loops: false);
                casting = true;
                yield return new WaitForSeconds(lanceWindup);
                casting = false;
                FireVolley();
                yield return new WaitForSeconds(lanceVolleyGap);
                poseFrames = null;
            }
        }

        private void FireVolley()
        {
            if (lanceFrames.Length == 0 || target == null) return;
            Vector2 origin = Center + new Vector2(spriteRenderer.flipX ? -1f : 1f, 0.3f);
            Vector2 aim = PlayerCenter - origin;
            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            for (int i = 0; i < lancesPerVolley; i++)
            {
                float angle = baseAngle + (i - (lancesPerVolley - 1) / 2f) * lanceSpreadDegrees;
                SpawnLance(origin, angle);
            }
        }

        // 물의 창 그림(오른쪽을 향함, 피벗 아래 가운데)을 가운데 기준으로 돌릴 수 있게 빈 부모 밑에 그림 가운데를 맞춰 단다.
        private void SpawnLance(Vector2 origin, float angle)
        {
            var root = new GameObject("AquaLance").transform;
            root.SetParent(effectsRoot, false);
            root.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, angle));
            var art = new GameObject("Art", typeof(SpriteRenderer));
            art.transform.SetParent(root, false);
            var renderer = art.GetComponent<SpriteRenderer>();
            renderer.sprite = lanceFrames[0];
            renderer.sortingOrder = 6;
            float scale = lanceLength / lanceFrames[0].bounds.size.x;
            art.transform.localScale = new Vector3(scale, scale, 1f);
            art.transform.localPosition = -lanceFrames[0].bounds.center * scale;
            float rad = angle * Mathf.Deg2Rad;
            lances.Add(new Lance { Root = root, Renderer = renderer, Direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) });
        }

        private void UpdateLances()
        {
            for (int i = lances.Count - 1; i >= 0; i--)
            {
                var lance = lances[i];
                if (lance.Root == null) { lances.RemoveAt(i); continue; }
                float step = lanceSpeed * Time.deltaTime;
                lance.Root.position += (Vector3)(lance.Direction * step);
                lance.Traveled += step;
                lance.Renderer.sprite = lanceFrames[Mathf.FloorToInt(Time.time * 12f) % lanceFrames.Length];

                Vector2 head = (Vector2)lance.Root.position + lance.Direction * (lanceLength * 0.4f);
                bool hit = false;
                if (target != null && !target.IsDead && targetCollider != null)
                {
                    Vector2 closest = targetCollider.bounds.ClosestPoint(head);
                    hit = (closest - head).sqrMagnitude <= lanceHitRadius * lanceHitRadius;
                }
                bool gone = head.y < floorY - 0.2f || lance.Traveled > MaxTravel;
                if (hit) HitPlayer(lanceDamageMultiplier, head);
                if (hit || gone)
                {
                    Destroy(lance.Root.gameObject);
                    lances.RemoveAt(i);
                }
            }
        }

        // ── 푸른 번개 ─────────────────────────────────────────

        private IEnumerator LightningRoutine()
        {
            if (boltFrames.Length == 0) yield break;
            casting = true;
            float progress = 1f - monster.CurrentHp / monster.MaxHp;
            int count = Mathf.RoundToInt(Mathf.Lerp(boltsMin, boltsMax, progress));
            int waves = phaseTwo ? boltWavesPhaseTwo : boltWavesPhaseOne;
            float warnTime = phaseTwo ? boltWarnTimePhaseTwo : boltWarnTimePhaseOne;
            for (int wave = 0; wave < waves && !monster.IsDead; wave++)
            {
                FacePlayer();
                var columns = PickBoltColumns(count);
                var warnings = new List<SpriteRenderer>();
                foreach (float x in columns)
                    warnings.Add(CreateWarning(new Vector2(x, floorY + boltTopAboveFloor / 2f), new Vector2(boltColumnWidth, boltTopAboveFloor), 0f));
                for (float t = 0f; t < warnTime; t += Time.deltaTime)
                {
                    foreach (var w in warnings) PulseWarning(w, BoltWarnColor, t);
                    yield return null;
                }
                foreach (var w in warnings) DestroyEffect(w);

                var bolts = new List<SpriteRenderer>();
                foreach (float x in columns) bolts.Add(CreateBolt(x));
                Shake(0.25f, 0.2f);
                for (float t = 0f; t < boltDuration; t += Time.deltaTime)
                {
                    float k = t / boltDuration;
                    var frame = boltFrames[Mathf.Min(Mathf.FloorToInt(k * boltFrames.Length), boltFrames.Length - 1)];
                    foreach (var bolt in bolts) bolt.sprite = frame;
                    if (k >= BoltHitStart && k < BoltHitEnd) CheckBoltHits(columns);
                    yield return null;
                }
                foreach (var bolt in bolts) DestroyEffect(bolt);
                yield return new WaitForSeconds(boltWaveGap);
            }
            casting = false;
        }

        // 추격 번개: 일정한 간격으로 그 순간 네크로시아가 서 있는 자리에 경고 기둥을 띄우고, 각 기둥은 chaseWarnTime 뒤에 떨어진다.
        // 경고가 뜬 뒤 떨어지기 전에 다음 경고가 따라붙으므로, 계속 움직여야 피할 수 있다.
        private IEnumerator ChaseLightningRoutine()
        {
            if (boltFrames.Length == 0) yield break;
            casting = true;
            float interval = phaseTwo ? chaseIntervalPhaseTwo : chaseIntervalPhaseOne;
            for (int i = 0; i < chaseStrikes && !monster.IsDead; i++)
            {
                FacePlayer();
                float x = Mathf.Clamp(target.transform.position.x, arenaMinX + 0.8f, arenaMaxX - 0.8f);
                StartCoroutine(SingleStrike(x, chaseWarnTime));
                yield return new WaitForSeconds(interval);
            }
            // 마지막 번개가 다 떨어질 때까지 기다린다.
            yield return new WaitForSeconds(chaseWarnTime + boltDuration);
            casting = false;
        }

        private IEnumerator SingleStrike(float x, float warnTime)
        {
            var columns = new List<float> { x };
            var warning = CreateWarning(new Vector2(x, floorY + boltTopAboveFloor / 2f), new Vector2(boltColumnWidth, boltTopAboveFloor), 0f);
            for (float t = 0f; t < warnTime; t += Time.deltaTime)
            {
                PulseWarning(warning, BoltWarnColor, t * 1.6f);   // 짧은 경고라 더 빠르게 깜빡인다
                yield return null;
            }
            DestroyEffect(warning);

            var bolt = CreateBolt(x);
            Shake(0.15f, 0.15f);
            for (float t = 0f; t < boltDuration; t += Time.deltaTime)
            {
                float k = t / boltDuration;
                bolt.sprite = boltFrames[Mathf.Min(Mathf.FloorToInt(k * boltFrames.Length), boltFrames.Length - 1)];
                if (k >= BoltHitStart && k < BoltHitEnd) CheckBoltHits(columns);
                yield return null;
            }
            DestroyEffect(bolt);
        }

        private List<float> PickBoltColumns(int count)
        {
            float min = arenaMinX + 0.8f;
            float max = arenaMaxX - 0.8f;
            var columns = new List<float> { Mathf.Clamp(target.transform.position.x, min, max) };
            for (int tries = 0; columns.Count < count && tries < 40; tries++)
            {
                float x = UnityEngine.Random.Range(min, max);
                bool clear = true;
                foreach (float column in columns)
                {
                    if (Mathf.Abs(column - x) < boltColumnWidth * 1.6f) { clear = false; break; }
                }
                if (clear) columns.Add(x);
            }
            return columns;
        }

        // 번개 그림(피벗이 번개가 땅에 닿는 자리)을 바닥에 세우고, 구름이 화면 위끝을 살짝 넘도록 키운다.
        private SpriteRenderer CreateBolt(float x)
        {
            var go = new GameObject("AquaBolt", typeof(SpriteRenderer));
            go.transform.SetParent(effectsRoot, false);
            float scale = boltTopAboveFloor / boltFrames[0].bounds.max.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.transform.position = new Vector3(x, floorY, 0f);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = boltFrames[0];
            renderer.sortingOrder = 7;
            return renderer;
        }

        private void CheckBoltHits(List<float> columns)
        {
            if (target == null || target.IsDead || targetCollider == null) return;
            var bounds = targetCollider.bounds;
            foreach (float x in columns)
            {
                if (bounds.max.x < x - boltColumnWidth / 2f || bounds.min.x > x + boltColumnWidth / 2f) continue;
                HitPlayer(boltDamageMultiplier, new Vector2(x, bounds.center.y));
                return;
            }
        }

        // ── 공통 ─────────────────────────────────────────────

        // 몸에 닿아 있으면 contactInterval마다 피해. 드릴로 도는 동안은 드릴 판정이 대신한다.
        private void TryContactDamage()
        {
            if (contactTimer > 0f) contactTimer -= Time.deltaTime;
            if (!fighting || drilling || pulling || trapping || contactTimer > 0f || monster.IsDead || target == null || target.IsDead || targetCollider == null) return;
            var bodyRect = new Rect(Center - BodyHitSize / 2f, BodyHitSize);
            var bounds = targetCollider.bounds;
            var playerRect = new Rect(bounds.min, bounds.size);
            if (!bodyRect.Overlaps(playerRect)) return;
            contactTimer = contactInterval;
            HitPlayer(contactDamageMultiplier, Center);
        }

        private void HitPlayer(float multiplier, Vector2 from)
        {
            if (target == null || target.IsDead) return;
            float damage = CombatMath.Finish(CombatMath.BaseDamage(monster.Attack, target.Stats.defense) * multiplier);
            target.TakeDamage(damage, from);
        }

        private SpriteRenderer CreateWarning(Vector2 center, Vector2 size, float angle)
        {
            var go = new GameObject("AquaWarning", typeof(SpriteRenderer));
            go.transform.SetParent(effectsRoot, false);
            Vector2 pixel = UISprites.WhitePixel.bounds.size;
            go.transform.localScale = new Vector3(size.x / pixel.x, size.y / pixel.y, 1f);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = UISprites.WhitePixel;
            renderer.sortingOrder = 3;
            renderer.color = Color.clear;
            return renderer;
        }

        private static void PulseWarning(SpriteRenderer warning, Color baseColor, float time)
        {
            var color = baseColor;
            color.a = 0.12f + 0.25f * Mathf.PingPong(time * 6f, 1f);
            warning.color = color;
        }

        private static void DestroyEffect(Component effect)
        {
            if (effect != null) Destroy(effect.gameObject);
        }

        private void ClearEffects()
        {
            lances.Clear();
            if (effectsRoot == null) return;
            foreach (Transform child in effectsRoot)
            {
                if (fade != null && child == fade.canvas.transform) continue;
                Destroy(child.gameObject);
            }
        }

        private void SetFadeAlpha(float alpha)
        {
            var color = fade.color;
            color.a = alpha;
            fade.color = color;
        }

        private static void Shake(float duration, float magnitude)
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.Shake(duration, magnitude);
        }
    }
}

using System.Collections;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 잊혀진 폐허의 필드 보스 황금 모래룡. 마지막 폐허 맵 오른쪽 끝 모래 속에 똬리를 틀고 있다가, 플레이어가 감지 거리 안에
    // 들어오면 모래 물결을 타고 천천히 미끄러져 다가온다. 가까이 붙으면 대지 강타(Attack 6프레임)로 앞쪽 바닥을 내려찍고
    // (내려찍은 자리에서 꼬리가 땅을 뚫고 튀어나온다 — Effects/SandTailBurst),
    // 중거리에서는 가끔 입에 금빛 구슬을 모아(Charge 8프레임) 황금 브레스(Effects/SandDragonBeam)를 땅에 내리쏜다.
    // 플레이어가 발판 위 높은 곳에 오래 머물면 포효(Roar 6프레임)하며 거대한 모래 회오리 두 개로 플레이어를 앞뒤에서 조여 온다(SandWhirlwind).
    // 대지 강타가 닿지 않는 거리에서는 땅을 내려찍어 플레이어 발밑에서 꼬리를 솟구치게 한다(꼬리 대지 폭발 — 잠깐 모래가
    // 들썩이는 예고 뒤에 솟으니, 그 사이 자리를 피하면 된다).
    [RequireComponent(typeof(Monster))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class SandDragonAI : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float detectRange = 11f; // 이 거리 밖이면 제자리에서 꿈틀거리기만 한다
        [SerializeField] private float keepDistance = 3.0f;
        [SerializeField] private float gravityScale = 4f;

        [Header("대지 강타")]
        [SerializeField] private float slamRange = 4.2f;    // 이 거리 안이면 내려찍는다
        [SerializeField] private float slamCooldown = 2.5f;
        [SerializeField, Range(0f, 1f)] private float slamImpactTiming = 4f / 6f; // 5번째 프레임(앞발이 땅에 닿음)
        [SerializeField] private float slamRadius = 3.2f;
        [SerializeField] private float slamForward = 2.8f;  // 몸 중심에서 충격 지점까지 앞쪽 거리
        [SerializeField] private float slamDamageMultiplier = 1.3f;
        [SerializeField] private float tailScale = 1.4f;          // 꼬리 그림(200px = 1칸, 키 약 3.4) 배율
        [SerializeField] private float tailFrameDuration = 0.07f;
        [SerializeField] private float tailHold = 0.3f;           // 다 솟은 꼬리가 멈춰 있는 시간(그 뒤 거꾸로 감기며 들어간다)

        [Header("꼬리 대지 폭발 (중거리)")]
        [SerializeField] private float tailStrikeMaxRange = 10f;  // 대지 강타 거리 밖 ~ 이 거리 안이면 쓴다
        [SerializeField] private float tailStrikeCooldown = 5f;
        [SerializeField] private float tailStrikeFirstDelay = 2.5f;
        [SerializeField] private float tailWarnTime = 0.6f;       // 발밑 모래가 들썩이는 예고 시간(이 사이에 피한다)
        [SerializeField] private float tailHitWidth = 1.8f;
        [SerializeField] private float tailHitHeight = 4.5f;
        [SerializeField] private float tailStrikeDamageMultiplier = 1.2f;

        [Header("황금 브레스 (필살기)")]
        [SerializeField] private float beamMaxRange = 10f;       // 대지 강타 거리 밖 ~ 이 거리 안이면 쏜다
        [SerializeField] private float beamCooldown = 9f;
        [SerializeField] private float beamFirstDelay = 5f;      // 등장 직후 바로 쏘지 않게
        [SerializeField] private float beamChargeDuration = 0.96f; // 구슬 모으기(Charge 8프레임 한 번)
        [SerializeField] private float beamHoldDuration = 1.0f;  // 다 모은 구슬을 문 채 겨누는 시간(마지막 2프레임 반복)
        [SerializeField] private float beamAimLockLead = 0.45f;  // 쏘기 이만큼 전에 조준을 고정한다(피할 틈)
        [SerializeField] private float beamDuration = 1.2f;
        [SerializeField] private float beamSweep = 2.5f;         // 쏘는 동안 착탄점이 앞으로 밀려 나가는 거리
        [SerializeField] private float beamMinForward = 3.5f;    // 착탄점은 몸 앞쪽으로 최소 이만큼 떨어진 곳
        [SerializeField] private float beamThickness = 1.2f;     // 빔 그림 굵기 배율
        [SerializeField] private float beamHitHalfWidth = 0.55f;
        [SerializeField] private float beamImpactRadius = 1.4f;
        [SerializeField] private int beamHits = 4;
        [SerializeField] private float beamHitDamageMultiplier = 0.9f;

        [Header("포효 + 모래 회오리")]
        [SerializeField] private float highThreshold = 1.8f;     // 플레이어 발이 바닥보다 이만큼 높으면 '높은 곳'
        [SerializeField] private float highHoldTime = 2.5f;      // 높은 곳에 이만큼 계속 머물면 포효한다(점프 한 번으로는 안 나온다)
        [SerializeField] private float roarCooldown = 12f;
        [SerializeField] private float roarDuration = 1.3f;      // 포효 6프레임 전체 길이
        [SerializeField, Range(0f, 1f)] private float roarImpactTiming = 0.45f; // 입을 크게 벌리는 순간
        [SerializeField] private float roarHold = 0.5f;          // 포효 뒤 잠깐 멈춰 있는 시간
        [SerializeField] private float whirlFrontOffset = 3.5f;  // 몸 중심에서 첫 회오리가 솟는 곳(몸 바로 앞)까지 거리
        [SerializeField] private float whirlBeyondPlayer = 4f;   // 플레이어 너머 이만큼 떨어진 곳에서 두 번째 회오리가 솟는다
        [SerializeField] private float whirlSpeed = 2.5f;        // 두 회오리가 플레이어 쪽으로 밀려오는 속도
        [SerializeField] private float whirlLifetime = 5f;
        [SerializeField] private float whirlHitInterval = 0.5f;
        [SerializeField] private float whirlDamageMultiplier = 0.8f;
        [SerializeField] private float whirlHitWidth = 2.0f;      // 대시 한 번(3유닛)으로 맞닿은 데서 반대편까지 빠져나갈 수 있는 폭
        // 회오리 그림 키. 화면 위끝(바닥 위 약 6.4)보다 살짝 낮게 세워 깔때기 윗부분까지 다 보이게 한다.
        [SerializeField] private float whirlHeight = 6.0f;
        // 맞는 범위 키. 깔때기 위로 흩날리는 모래까지 쳐서 그림보다 조금 높게 — 땅에서는 점프로 못 넘고 대시로만 뚫을 수 있다.
        [SerializeField] private float whirlHitHeight = 7.0f;

        private Monster monster;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Character target;
        private MonsterAttackSpriteAnimator attackAnimator;
        private bool isAttacking;
        private float slamTimer;
        private float beamTimer;
        private GameObject beamObject;
        private readonly System.Collections.Generic.List<GameObject> tailObjects = new System.Collections.Generic.List<GameObject>();
        private float tailStrikeTimer;
        public const string TailBurstPath = "Art/Effects/SandTailBurst";
        private float highTimer;
        private float roarTimer;
        // 회오리가 솟을 수 있는 x 범위(맵 벽 안쪽). GameBootstrap이 모래룡을 만들 때 알려 준다.
        private float arenaMinX = float.NegativeInfinity;
        private float arenaMaxX = float.PositiveInfinity;

        public void SetArenaBounds(float minX, float maxX)
        {
            arenaMinX = minX;
            arenaMaxX = maxX;
        }

        private const string RoarClip = "Roar";
        public const string RoarPath = "Art/Monsters/SandDragon/Roar";
        // 포효로 입을 크게 벌리는 순간. 등장 연출(SandDragonIntroCutscene)이 받아서 화면 흔들림/번쩍임/충격파를 띄운다.
        public event System.Action OnRoarImpact;

        private const string ChargeClip = "Charge";
        private const string ChargeHoldClip = "ChargeHold";
        private const string ChargeFireClip = "ChargeFire";
        public const string ChargePath = "Art/Monsters/SandDragon/Charge";
        public const string BeamPath = "Art/Effects/SandDragonBeam";
        // 충전 프레임(150px = 1칸, 피벗 아래 가운데)에서 입에 문 구슬은 피벗보다 이만큼 앞/위에 있다(원화 px 133, 237).
        // 모래룡 오브젝트를 키워 두므로(GameBootstrap.SandDragonScale) 쓸 때 오브젝트 배율을 곱한다.
        private static readonly Vector2 ChargeOrbOffset = new Vector2(133f / 150f, 237f / 150f);
        // 빔 칸 그림(512x512, 240px = 1칸, 피벗 아래 가운데)의 출발점(오른쪽 위 별)과 충돌점(왼쪽 아래) — 칸 왼쪽 위 기준 px.
        private static readonly Vector2 BeamOriginPx = new Vector2(475f, 24f);
        private static readonly Vector2 BeamEndPx = new Vector2(21f, 451f);
        private const float BeamCellPx = 512f;
        private const float BeamPixelsPerUnit = 240f;
        private const float BeamFrameInterval = 0.06f;
        // 등장 연출(SandDragonIntroCutscene) 동안에는 움직이지도, 닿아도 전투를 열지도 않는다(연출이 직접 연다).
        private bool heldForIntro;

        public void HoldForIntro(bool hold)
        {
            heldForIntro = hold;
            body.linearVelocity = Vector2.zero;
            body.bodyType = hold ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        }

        private void Awake()
        {
            monster = GetComponent<Monster>();
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            body.gravityScale = gravityScale;
            body.freezeRotation = true;

            target = Object.FindFirstObjectByType<Character>();
            attackAnimator = GetComponent<MonsterAttackSpriteAnimator>();
            beamTimer = beamFirstDelay;
            tailStrikeTimer = tailStrikeFirstDelay;
            if (attackAnimator != null)
            {
                var charge = Resources.LoadAll<Sprite>(ChargePath);
                System.Array.Sort(charge, (a, b) => string.CompareOrdinal(a.name, b.name));
                if (charge.Length >= 2)
                {
                    attackAnimator.AddClip(ChargeClip, charge);
                    attackAnimator.AddClip(ChargeHoldClip, new[] { charge[charge.Length - 2], charge[charge.Length - 1] });
                    // 쏘는 동안에도 굳어 있지 않게 구슬을 문 마지막 세 장을 빠르게 돌려 몸이 들썩이게 한다.
                    int fireCount = Mathf.Min(3, charge.Length);
                    var fire = new Sprite[fireCount];
                    System.Array.Copy(charge, charge.Length - fireCount, fire, 0, fireCount);
                    attackAnimator.AddClip(ChargeFireClip, fire);
                }
                attackAnimator.AddClip(RoarClip, RoarPath);
            }
        }

        // 모래룡은 땅 위에서만 싸운다. 커다란 몸이 낮은 공중 발판(한쪽 방향 발판)에 걸려 올라타지 않도록 발판과는 부딪히지 않게 한다.
        private void Start()
        {
            var bodyCollider = GetComponent<Collider2D>();
            if (bodyCollider == null) return;
            foreach (var effector in Object.FindObjectsByType<PlatformEffector2D>())
            {
                foreach (var platform in effector.GetComponents<Collider2D>())
                {
                    if (platform.usedByEffector) Physics2D.IgnoreCollision(bodyCollider, platform, true);
                }
            }
        }

        private void Update()
        {
            if (slamTimer > 0f && !isAttacking) slamTimer -= Time.deltaTime;
            if (beamTimer > 0f && !isAttacking && !heldForIntro) beamTimer -= Time.deltaTime;
            if (tailStrikeTimer > 0f && !isAttacking && !heldForIntro) tailStrikeTimer -= Time.deltaTime;
            if (roarTimer > 0f && !isAttacking) roarTimer -= Time.deltaTime;

            // 플레이어가 높은 곳에 머문 시간. 내려오거나 멀어지면 처음부터 다시 잰다.
            bool high = !heldForIntro && !isAttacking && target != null && !target.IsDead
                && Mathf.Abs(target.transform.position.x - transform.position.x) <= detectRange
                && target.transform.position.y - GroundY() >= highThreshold;
            highTimer = high ? highTimer + Time.deltaTime : 0f;
        }

        private void OnDestroy()
        {
            if (beamObject != null) Destroy(beamObject);
            foreach (var tail in tailObjects) if (tail != null) Destroy(tail);
        }

        private void FixedUpdate()
        {
            if (heldForIntro || isAttacking || monster.IsMinigameOpponent || monster.IsDead || monster.IsImmobilized || target == null || target.IsDead)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            float dx = target.transform.position.x - transform.position.x;
            float distance = Mathf.Abs(dx);
            if (distance > detectRange)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            // 원화가 오른쪽을 보고 있어서, 플레이어가 왼쪽에 있으면 뒤집는다.
            spriteRenderer.flipX = dx < 0f;

            if (highTimer >= highHoldTime && roarTimer <= 0f && attackAnimator != null && attackAnimator.HasClip(RoarClip))
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                StartCoroutine(RoarRoutine());
                return;
            }

            if (distance <= slamRange && slamTimer <= 0f && attackAnimator != null && attackAnimator.HasFrames)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                StartCoroutine(SlamRoutine());
                return;
            }

            if (distance > slamRange && distance <= beamMaxRange && beamTimer <= 0f
                && attackAnimator != null && attackAnimator.HasClip(ChargeClip))
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                StartCoroutine(BeamRoutine());
                return;
            }

            if (distance > slamRange && distance <= tailStrikeMaxRange && tailStrikeTimer <= 0f
                && attackAnimator != null && attackAnimator.HasFrames)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                StartCoroutine(TailStrikeRoutine());
                return;
            }

            float move = distance > keepDistance ? Mathf.Sign(dx) * moveSpeed : 0f;
            body.linearVelocity = new Vector2(move, body.linearVelocity.y);
        }

        // 대지 강타: 앞발을 들었다가 내려찍는 순간 앞쪽 바닥 원형 범위에 피해를 준다.
        private IEnumerator SlamRoutine()
        {
            isAttacking = true;
            slamTimer = slamCooldown;
            float facing = spriteRenderer.flipX ? -1f : 1f;
            float duration = attackAnimator.DefaultDuration;
            attackAnimator.Play(duration);

            yield return new WaitForSeconds(duration * slamImpactTiming);
            if (!monster.IsDead)
            {
                StartCoroutine(TailBurst(new Vector2(transform.position.x + facing * slamForward, GroundY()), facing));
            }
            if (!monster.IsDead && target != null && !target.IsDead)
            {
                Vector2 impact = (Vector2)transform.position + new Vector2(facing * slamForward, 0f);
                Vector2 offset = (Vector2)target.transform.position - impact;
                if (offset.magnitude <= slamRadius)
                {
                    float damage = CombatMath.Finish(CombatMath.BaseDamage(monster.Attack, target.Stats.defense) * slamDamageMultiplier);
                    target.TakeDamage(damage, impact);
                }
            }

            yield return new WaitForSeconds(duration * (1f - slamImpactTiming));
            isAttacking = false;
        }

        // 꼬리 대지 폭발: 대지 강타 모션으로 땅을 내려찍는 순간 플레이어가 서 있던 자리를 정하고, 그 발밑 모래가 잠깐
        // 들썩인 뒤(tailWarnTime) 꼬리가 솟구친다. 솟는 순간 꼬리 기둥 안에 있으면 맞는다.
        private IEnumerator TailStrikeRoutine()
        {
            isAttacking = true;
            tailStrikeTimer = tailStrikeCooldown;
            slamTimer = Mathf.Max(slamTimer, 1f);
            float facing = spriteRenderer.flipX ? -1f : 1f;
            float duration = attackAnimator.DefaultDuration;
            attackAnimator.Play(duration);

            yield return new WaitForSeconds(duration * slamImpactTiming);
            if (!monster.IsDead && target != null)
            {
                float x = Mathf.Clamp(target.transform.position.x, arenaMinX + 0.8f, arenaMaxX - 0.8f);
                StartCoroutine(TailBurst(new Vector2(x, GroundY()), facing, tailWarnTime, dealsDamage: true));
            }

            yield return new WaitForSeconds(duration * (1f - slamImpactTiming));
            isAttacking = false;
        }

        // 꼬리가 땅을 뚫고 솟구쳤다가(4프레임) 잠깐 멈춘 뒤 거꾸로 감기며 땅속으로 들어간다.
        // warnTime > 0이면 먼저 첫 장(모래가 막 들썩이는 그림)을 흐리게 떨며 예고한다.
        // dealsDamage면 꼬리가 솟는 순간 꼬리 기둥 범위에 피해를 준다(대지 강타에 딸린 꼬리는 강타가 이미 때려서 끈다).
        // 꼬리 그림은 오른쪽으로 휘어 있어서, 왼쪽을 볼 때는 뒤집는다.
        private IEnumerator TailBurst(Vector2 bottomCenter, float facing, float warnTime = 0f, bool dealsDamage = false)
        {
            var frames = Resources.LoadAll<Sprite>(TailBurstPath);
            if (frames.Length == 0) yield break;
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));

            var go = new GameObject("SandDragonTail");
            go.transform.position = bottomCenter;
            go.transform.localScale = new Vector3(tailScale, tailScale, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            renderer.flipX = facing < 0f;
            tailObjects.Add(go);

            if (warnTime > 0f)
            {
                renderer.sprite = frames[0];
                for (float t = 0f; t < warnTime; t += Time.deltaTime)
                {
                    if (go == null) yield break;
                    float jitter = Mathf.Sin(t * 60f) * 0.04f;
                    go.transform.position = bottomCenter + new Vector2(jitter, 0f);
                    renderer.color = new Color(1f, 1f, 1f, 0.45f + 0.25f * Mathf.Sin(t * 25f));
                    yield return null;
                }
                go.transform.position = bottomCenter;
                renderer.color = Color.white;
            }

            for (int i = 0; i < frames.Length; i++)
            {
                if (go == null) yield break;
                renderer.sprite = frames[i];
                if (dealsDamage && i == 1) TailHit(bottomCenter);
                yield return new WaitForSeconds(tailFrameDuration);
            }
            yield return new WaitForSeconds(tailHold);
            for (int i = frames.Length - 2; i >= 0; i--)
            {
                renderer.sprite = frames[i];
                yield return new WaitForSeconds(tailFrameDuration);
            }
            tailObjects.Remove(go);
            if (go != null) Destroy(go);
        }

        private void TailHit(Vector2 bottomCenter)
        {
            if (monster.IsDead || target == null || target.IsDead) return;
            Vector2 center = bottomCenter + Vector2.up * (tailHitHeight * 0.5f);
            foreach (var hit in Physics2D.OverlapBoxAll(center, new Vector2(tailHitWidth, tailHitHeight), 0f))
            {
                if (hit.GetComponent<Character>() != target) continue;
                float damage = CombatMath.Finish(CombatMath.BaseDamage(monster.Attack, target.Stats.defense) * tailStrikeDamageMultiplier);
                target.TakeDamage(damage, bottomCenter);
                return;
            }
        }

        // 포효: 고개를 들어 크게 울부짖고(Roar 6프레임), 입을 크게 벌리는 순간 화면이 흔들리며 거대한 모래 회오리 두 개가 솟는다.
        // 하나는 모래룡 바로 앞에서 플레이어 쪽으로, 하나는 플레이어 너머에서 모래룡 쪽으로 밀려와 플레이어를 앞뒤에서 조인다.
        // 회오리는 맵 꼭대기까지 닿아서 발판 위로 피할 수 없고 대시로만 뚫고 지나갈 수 있다.
        private IEnumerator RoarRoutine()
        {
            isAttacking = true;
            roarTimer = roarCooldown;
            highTimer = 0f;

            attackAnimator.PlayClip(RoarClip, roarDuration);
            yield return new WaitForSeconds(roarDuration * roarImpactTiming);
            if (!monster.IsDead)
            {
                OnRoarImpact?.Invoke();
                float groundY = GroundY();
                float toPlayer = target != null && target.transform.position.x < transform.position.x ? -1f : 1f;
                float playerX = target != null ? target.transform.position.x : transform.position.x + toPlayer * whirlBeyondPlayer;
                float frontX = transform.position.x + toPlayer * whirlFrontOffset;
                float beyondX = Mathf.Clamp(playerX + toPlayer * whirlBeyondPlayer, arenaMinX + 1f, arenaMaxX - 1f);
                SpawnWhirl(frontX, groundY, toPlayer);
                SpawnWhirl(beyondX, groundY, -toPlayer);
            }

            yield return new WaitForSeconds(roarDuration * (1f - roarImpactTiming) + roarHold);
            isAttacking = false;
        }

        private void SpawnWhirl(float x, float groundY, float direction)
        {
            SandWhirlwind.Spawn(new Vector2(x, groundY), direction, monster.Attack, whirlDamageMultiplier, whirlSpeed, whirlLifetime,
                whirlHitInterval, whirlHitWidth, whirlHeight, whirlHitHeight);
        }

        // 황금 브레스: 입에 금빛 구슬을 모았다가(Charge 한 번 → 마지막 2프레임 반복) 플레이어가 서 있던 땅을 겨눠 빔을 내리쏜다.
        // 조준은 쏘기 직전(beamAimLockLead)에 고정되고, 쏘는 동안 착탄점이 앞으로 밀려 나간다. 빔 줄기나 착탄점에 닿으면
        // 여러 번 피해를 입는다. 대시 무적으로 뚫거나 모래룡 쪽으로 파고들면 피할 수 있다. 시전 중에는 경직되지 않는다.
        private IEnumerator BeamRoutine()
        {
            isAttacking = true;
            beamTimer = beamCooldown;
            slamTimer = Mathf.Max(slamTimer, 1f);
            float facing = spriteRenderer.flipX ? -1f : 1f;
            float groundY = GroundY();

            attackAnimator.PlayClip(ChargeClip, beamChargeDuration);
            yield return new WaitForSeconds(beamChargeDuration);
            attackAnimator.PlayClipLoop(ChargeHoldClip, 0.2f);

            float aimX = AimX(facing);
            for (float t = 0f; t < beamHoldDuration; t += Time.deltaTime)
            {
                if (monster.IsDead) { EndBeam(); yield break; }
                if (t < beamHoldDuration - beamAimLockLead) aimX = AimX(facing);
                yield return null;
            }

            attackAnimator.PlayClipLoop(ChargeFireClip, 0.24f);
            float scale = Mathf.Abs(transform.lossyScale.y);
            Vector2 origin = (Vector2)transform.position + new Vector2(facing * ChargeOrbOffset.x, ChargeOrbOffset.y) * scale;
            var frames = Resources.LoadAll<Sprite>(BeamPath);
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            SpriteRenderer beamRenderer = frames.Length > 0 ? CreateBeam(origin) : null;

            float hitInterval = beamDuration / Mathf.Max(1, beamHits);
            float nextHit = BeamFrameInterval * 2f; // 빔이 굵어지는 3번째 프레임부터 맞는다
            int dealt = 0;
            for (float t = 0f; t < beamDuration + nextHit; t += Time.deltaTime)
            {
                if (monster.IsDead) break;
                Vector2 impact = new Vector2(aimX + facing * beamSweep * Mathf.Clamp01(t / beamDuration), groundY);
                if (beamRenderer != null)
                {
                    int step = (int)(t / BeamFrameInterval);
                    int frame = step < frames.Length ? step : frames.Length - 3 + step % 3; // 다 굵어진 뒤엔 마지막 세 장을 번갈아
                    beamRenderer.sprite = frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
                    AimBeam(origin, impact);
                }

                if (dealt < beamHits && t >= nextHit)
                {
                    dealt++;
                    nextHit += hitInterval;
                    if (target != null && !target.IsDead && BeamTouches(origin, impact))
                    {
                        float damage = CombatMath.Finish(CombatMath.BaseDamage(monster.Attack, target.Stats.defense) * beamHitDamageMultiplier);
                        target.TakeDamage(damage, impact);
                    }
                }
                yield return null;
            }

            // 빔이 서서히 사라진다.
            for (float f = 0f; beamRenderer != null && f < 0.25f && !monster.IsDead; f += Time.deltaTime)
            {
                beamRenderer.color = new Color(1f, 1f, 1f, 1f - f / 0.25f);
                yield return null;
            }
            EndBeam();
        }

        private void EndBeam()
        {
            if (beamObject != null) Destroy(beamObject);
            beamObject = null;
            attackAnimator.Stop();
            isAttacking = false;
        }

        // 플레이어가 서 있는 x. 몸 바로 앞이나 뒤쪽이면 앞쪽 최소 거리로 밀어낸다.
        private float AimX(float facing)
        {
            float px = target != null ? target.transform.position.x : transform.position.x;
            float forward = Mathf.Max((px - transform.position.x) * facing, beamMinForward);
            return transform.position.x + facing * Mathf.Min(forward, beamMaxRange);
        }

        // 빔 그림을 그대로 쓰되, 그림 속 빔 줄기(출발점 → 충돌점)가 부모의 +x 축에 오도록 자식을 돌려 두고
        // 부모를 원하는 방향으로 돌린 뒤 길이 방향으로만 늘린다(굵기는 beamThickness 배).
        private SpriteRenderer CreateBeam(Vector2 origin)
        {
            beamObject = new GameObject("SandDragonBeam");
            beamObject.transform.position = origin;

            var art = new GameObject("Art");
            art.transform.SetParent(beamObject.transform, false);
            var renderer = art.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = spriteRenderer.sortingOrder + 1;

            Vector2 artDir = BeamArtDirection();
            float artAngle = Mathf.Atan2(artDir.y, artDir.x) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(0f, 0f, -artAngle);
            // 피벗(아래 가운데)에서 그림 속 출발점까지의 거리. 출발점이 부모 원점에 오게 자식을 옮긴다.
            Vector2 originFromPivot = new Vector2(BeamOriginPx.x - BeamCellPx * 0.5f, BeamCellPx - BeamOriginPx.y) / BeamPixelsPerUnit;
            art.transform.localRotation = rotation;
            art.transform.localPosition = -(rotation * originFromPivot);
            return renderer;
        }

        private void AimBeam(Vector2 origin, Vector2 impact)
        {
            Vector2 want = impact - origin;
            beamObject.transform.position = origin;
            beamObject.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(want.y, want.x) * Mathf.Rad2Deg);
            beamObject.transform.localScale = new Vector3(want.magnitude / BeamArtDirection().magnitude, beamThickness, 1f);
        }

        private static Vector2 BeamArtDirection() =>
            new Vector2(BeamEndPx.x - BeamOriginPx.x, -(BeamEndPx.y - BeamOriginPx.y)) / BeamPixelsPerUnit;

        // 플레이어 몸 중심이 빔 줄기 가까이 있거나 착탄점 폭발 안에 있으면 맞는다.
        private bool BeamTouches(Vector2 origin, Vector2 impact)
        {
            var targetCollider = target.GetComponent<Collider2D>();
            Vector2 p = targetCollider != null ? (Vector2)targetCollider.bounds.center : (Vector2)target.transform.position;
            if (Vector2.Distance(p, impact) <= beamImpactRadius) return true;
            Vector2 seg = impact - origin;
            float along = Mathf.Clamp01(Vector2.Dot(p - origin, seg) / seg.sqrMagnitude);
            return Vector2.Distance(p, origin + seg * along) <= beamHitHalfWidth;
        }

        private float GroundY()
        {
            var collider = GetComponent<Collider2D>();
            return collider != null ? collider.bounds.min.y : transform.position.y;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, slamRange);
        }
    }
}

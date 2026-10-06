using System.Collections;
using UnityEngine;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Combat;
using Aethoria.UI;

namespace Aethoria.Monsters
{
    // 보스 몬스터의 이동/공격 AI. 체력·방어력 등은 기존 Monster/MonsterData가 그대로 담당한다.
    // 근접 사거리(접촉)에 들어오면 더 이상 돌진/휩쓸기 콤보로 때리지 않고 리듬전투 화면으로 넘긴다.
    // 원거리 점프 내려찍기는 접촉이 아니라서 그대로 실시간으로 유지한다(거리를 좁히는 수단).
    // 플레이어가 조금 떨어져 있으면 가끔 점프 내려찍기로 거리를 한 번에 좁힌다.
    [RequireComponent(typeof(Monster))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BossAI : MonoBehaviour
    {
        [Header("이동/감지")]
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float attackRange = 1.4f;
        [SerializeField] private float gravityScale = 4f;
        // 플레이어가 이만큼 위에 있으면(점프로 넘어가는 중) 근접 접촉으로 치지 않는다 — 싸우기 싫으면 뛰어넘어 지나갈 수 있게.
        [SerializeField] private float jumpOverHeight = 2f;

        [Header("패턴: 점프 내려찍기 (중거리에서 플레이어 쪽으로 뛰어들어 넓은 충격파) - 유일하게 남은 원거리 실시간 패턴")]
        [SerializeField] private float jumpMaxRange = 6f; // 근접 사거리보다 멀고 이 거리 안이면 점프를 고려한다
        [SerializeField] private float jumpMaxDistance = 5f; // 한 번 점프로 이동하는 최대 거리
        [SerializeField] private float jumpDuration = 1.4f; // 8프레임 모션 전체 길이
        [SerializeField] private float jumpSlamRadius = 2.2f;
        [SerializeField] private float jumpDamageMultiplier = 1.6f;
        [SerializeField] private float jumpCooldown = 7f;

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
        private RhythmBattleUI rhythmBattleUI;
        private Color normalColor;

        private bool isAttacking;
        private bool isStaggered;
        private bool isSuperArmor;
        private float jumpCooldownTimer;
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

            target = Object.FindFirstObjectByType<Character>();
            rhythmBattleUI = Object.FindFirstObjectByType<RhythmBattleUI>();
            monster.OnDamaged += HandleDamaged;

            superArmorCycleRoutine = StartCoroutine(SuperArmorCycleRoutine());
        }

        // 플레이어의 W(악마의 손)처럼 연타로 경직을 노리는 스킬이 맞는 동안 계속 호출해서
        // 슈퍼아머 주기를 처음부터 다시 세게 만든다. 이미 슈퍼아머 상태였다면 즉시 풀어준다.
        // 이 스킬이 끝나고 호출이 멈추면, 그 시점부터 다시 정상적으로 10초 주기가 흐른다.
        public void ResetSuperArmorTimer()
        {
            if (monster.IsDead) return;

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
            if (monster != null) monster.OnDamaged -= HandleDamaged;
        }

        private void Update()
        {
            if (jumpCooldownTimer > 0f && !isAttacking) jumpCooldownTimer -= Time.deltaTime;
        }

        private void FixedUpdate()
        {
            if (monster.IsDead || isStaggered || monster.IsImmobilized || target == null || target.IsDead)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            // 점프 패턴이 이미 진행 중이면 사거리를 다시 확인하지 않는다. 즉, 한 번 시작된 점프는
            // 플레이어가 도중에 사거리 밖으로 나가도 끝까지(RunJumpPattern이 끝날 때까지) 재생된다.
            // (맞아서 경직되는 것과는 별개 - 피격 중단은 HandleDamaged에서만 일어난다.)
            if (isAttacking)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            float distance = target.transform.position.x - transform.position.x;
            bool playerJumpedOver = target.transform.position.y - transform.position.y > jumpOverHeight;

            if (!playerJumpedOver && Mathf.Abs(distance) <= attackRange)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                spriteRenderer.flipX = distance < 0f;
                if (rhythmBattleUI != null) rhythmBattleUI.TryBegin(monster, target);
            }
            else if (Mathf.Abs(distance) <= jumpMaxRange && jumpCooldownTimer <= 0f && CanJumpAttack)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                patternRoutine = StartCoroutine(RunJumpPattern());
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
            if (monster.IsDead || isSuperArmor) return;

            if (isAttacking)
            {
                InterruptPattern();
            }
            else
            {
                RefreshStagger();
            }
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

        private bool CanJumpAttack => attackAnimator != null && attackAnimator.HasClip(JumpAttackClip);

        public const string JumpAttackClip = "JumpAttack";

        // 점프 공격 8프레임 기준 타이밍: 1 도약 준비 → 2~5 공중(이 동안 착지 지점까지 이동)
        // → 6 내려찍기 충격(이때 피해) → 7~8 착지 후 자세 회복.
        private const float JumpTakeoffFraction = 1f / 8f;
        private const float JumpImpactFraction = 5f / 8f;

        private IEnumerator RunJumpPattern()
        {
            isAttacking = true;
            jumpCooldownTimer = jumpCooldown;

            Vector2 direction = FacingToTarget();
            spriteRenderer.flipX = direction.x < 0f;

            // 도약하는 순간의 플레이어 위치로 착지 지점을 정한다(공중에서 따라가지 않음 → 피할 여지가 있다).
            float startX = body.position.x;
            float targetX = target != null ? target.transform.position.x : startX;
            float landingX = Mathf.Clamp(targetX, startX - jumpMaxDistance, startX + jumpMaxDistance);
            Rect bounds = GameBootstrap.CurrentMapBounds;
            landingX = Mathf.Clamp(landingX, bounds.xMin + 1f, bounds.xMax - 1f);

            attackAnimator.PlayClip(JumpAttackClip, jumpDuration);

            yield return new WaitForSeconds(jumpDuration * JumpTakeoffFraction);

            float airTime = jumpDuration * (JumpImpactFraction - JumpTakeoffFraction);
            float elapsed = 0f;
            while (elapsed < airTime)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / airTime);
                body.position = new Vector2(Mathf.Lerp(startX, landingX, t), body.position.y);
            }

            if (monster.IsDead) yield break;
            DealDamageAt(transform.position, jumpSlamRadius, jumpDamageMultiplier);

            yield return new WaitForSeconds(jumpDuration * (1f - JumpImpactFraction));
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

                float damage = CombatMath.PhysicalDamage(monster.Attack * multiplier, character.Stats.defense);
                character.TakeDamage(damage, origin);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = new Color(0.6f, 0.2f, 1f);
            Gizmos.DrawWireSphere(transform.position, jumpMaxRange);
        }
    }
}

using System.Collections;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 보스 몬스터의 이동/공격 AI. 체력·방어력 등은 기존 Monster/MonsterData가 그대로 담당하고,
    // 여기서는 "플레이어를 쫓아가다가 사거리에 들어오면 콤보 패턴을 시전한다"는 행동과
    // 경직/슈퍼아머 상태를 담당한다.
    // 패턴은 두 가지뿐이지만 각각 3연타 콤보로 구성해, 적은 패턴 수로도 위협적으로 느껴지게 한다.
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

        [SerializeField] private float patternCooldown = 1.2f;

        [Header("경직 / 슈퍼아머")]
        [SerializeField] private float staggerFlinchDuration = 0.35f; // 스킬 시전 중 맞으면 경직되는 시간
        [SerializeField] private float superArmorInterval = 10f; // 피격 여부와 상관없이 이 주기마다 슈퍼아머 발동
        [SerializeField] private float superArmorDuration = 3f;
        [SerializeField] private Color superArmorColor = new Color(1f, 0.85f, 0.2f);

        private Monster monster;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Character target;
        private Color normalColor;

        private bool isAttacking;
        private bool isStaggered;
        private bool isSuperArmor;
        private bool nextIsDash = true;
        private Coroutine patternRoutine;
        private Coroutine staggerRoutine;
        private Coroutine superArmorCycleRoutine;

        private void Awake()
        {
            monster = GetComponent<Monster>();
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            body.gravityScale = gravityScale;
            body.freezeRotation = true;
            normalColor = spriteRenderer.color;

            target = Object.FindFirstObjectByType<Character>();
            monster.OnDamaged += HandleDamaged;

            superArmorCycleRoutine = StartCoroutine(SuperArmorCycleRoutine());
        }

        // 플레이어의 W(사슬 폭풍)처럼 지속적으로 경직을 노리는 스킬이 맞는 동안 계속 호출해서
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

        private void FixedUpdate()
        {
            if (monster.IsDead || isAttacking || isStaggered || target == null || target.IsDead)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            float distance = target.transform.position.x - transform.position.x;
            if (Mathf.Abs(distance) <= attackRange)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                patternRoutine = StartCoroutine(RunNextPattern());
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

                yield return new WaitForSeconds(sweepHitInterval);
            }
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
                character.TakeDamage(damage);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, sweepRadius);
        }
    }
}

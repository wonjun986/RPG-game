using System.Collections;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 일반 몬스터용 간단한 AI. 보스처럼 콤보 패턴은 없고,
    // 플레이어를 향해 다가가다가 사거리에 들어오면 일정 주기로 단발 공격만 한다.
    // 공격 모션이 있으면 휘두르는 동안 멈춰 서고, 칼이 실제로 닿는 프레임에 맞춰 피해를 준다.
    [RequireComponent(typeof(Monster))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class MonsterAI : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float attackRange = 0.9f;
        [SerializeField] private float attackInterval = 1.2f;
        [SerializeField] private float gravityScale = 4f;
        [SerializeField] private float attackDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float hitTiming = 0.5f; // 모션 중 피해가 들어가는 시점(비율). 6프레임 중 4번째 베기 프레임
        [SerializeField] private float hitRangeTolerance = 0.3f; // 휘두르는 사이 플레이어가 살짝 물러나도 맞는 여유 거리

        private Monster monster;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Character target;
        private float attackCooldown;
        private MonsterAttackSpriteAnimator attackAnimator;
        private bool isAttacking;

        private void Awake()
        {
            monster = GetComponent<Monster>();
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            body.gravityScale = gravityScale;
            body.freezeRotation = true;
            attackAnimator = GetComponent<MonsterAttackSpriteAnimator>();

            target = Object.FindFirstObjectByType<Character>();
        }

        private void Update()
        {
            if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;
        }

        private void FixedUpdate()
        {
            if (monster.IsDead || monster.IsImmobilized || target == null || target.IsDead)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            if (isAttacking)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            float distance = target.transform.position.x - transform.position.x;

            if (Mathf.Abs(distance) <= attackRange)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                spriteRenderer.flipX = distance < 0f;
                TryAttack();
            }
            else
            {
                float direction = Mathf.Sign(distance);
                body.linearVelocity = new Vector2(direction * moveSpeed, body.linearVelocity.y);
                spriteRenderer.flipX = direction < 0f;
            }
        }

        private void TryAttack()
        {
            if (attackCooldown > 0f) return;
            attackCooldown = attackInterval;

            if (attackAnimator == null || !attackAnimator.HasFrames)
            {
                DealDamage();
                return;
            }

            StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            isAttacking = true;
            attackAnimator.Play(attackDuration);

            yield return new WaitForSeconds(attackDuration * hitTiming);
            if (!monster.IsDead && target != null && !target.IsDead
                && Mathf.Abs(target.transform.position.x - transform.position.x) <= attackRange + hitRangeTolerance)
            {
                DealDamage();
            }

            yield return new WaitForSeconds(attackDuration * (1f - hitTiming));
            isAttacking = false;
        }

        private void DealDamage()
        {
            float damage = CombatMath.PhysicalDamage(monster.Attack, target.Stats.defense);
            target.TakeDamage(damage, transform.position);
        }
    }
}

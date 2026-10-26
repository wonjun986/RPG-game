using System.Collections;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 일반 몬스터용 간단한 AI. 보스처럼 콤보 패턴은 없고,
    // 플레이어를 향해 다가가다가 사거리에 들어오면 일정 주기로 단발 공격만 한다.
    // 공격 모션이 있으면 휘두르는 동안 멈춰 서고, 칼이 실제로 닿는 프레임에 맞춰 피해를 준다.
    // 공격 모션이 없는 몬스터(슬라임, 심해 정령 등)는 몸으로 부딪혀 바로 피해를 준다.
    [RequireComponent(typeof(Monster))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class MonsterAI : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float attackRange = 0.9f;
        [SerializeField] private float attackInterval = 1.2f;
        [SerializeField] private float gravityScale = 4f;
        [SerializeField, Range(0f, 1f)] private float hitTiming = 0.5f; // 모션 중 피해가 들어가는 시점(비율). 6프레임 중 4번째 베기 프레임
        [SerializeField] private float hitRangeTolerance = 0.3f; // 휘두르는 사이 플레이어가 살짝 물러나도 맞는 여유 거리
        // 플레이어가 이만큼 위에 있으면(점프로 넘어가는 중) 공격이 닿지 않는다.
        [SerializeField] private float jumpOverHeight = 2f;

        private Monster monster;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Character target;
        private float attackCooldown;
        private MonsterAttackSpriteAnimator attackAnimator;
        private bool isAttacking;
        private float contactRange;

        private void Awake()
        {
            monster = GetComponent<Monster>();
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            body.gravityScale = gravityScale;
            body.freezeRotation = true;
            attackAnimator = GetComponent<MonsterAttackSpriteAnimator>();

            target = Object.FindFirstObjectByType<Character>();

            // attackRange는 기사(폭 0.6) 기준 중심 거리라, 콜로서스처럼 몸이 넓은 몬스터는 플레이어와 옆으로 맞닿아도
            // 두 콜라이더가 서로 밀어내 중심 거리가 그보다 멀어서 공격이 닿지 않았다. 넓어진 몸 반폭만큼 늘려 준다.
            var box = GetComponent<BoxCollider2D>();
            float extraHalfWidth = box != null ? Mathf.Max(0f, box.size.x * 0.5f - 0.3f) : 0f;
            contactRange = attackRange + extraHalfWidth;
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

            if (Mathf.Abs(distance) <= contactRange)
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
                if (TargetInReach(0f)) DealDamage();
                return;
            }

            StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            isAttacking = true;
            // 모션 길이는 몬스터마다 Configure로 정해 둔 값(기사 0.6초, 콜로서스 0.9초)을 그대로 쓴다.
            float attackDuration = attackAnimator.DefaultDuration;
            attackAnimator.Play(attackDuration);

            yield return new WaitForSeconds(attackDuration * hitTiming);
            if (!monster.IsDead && target != null && !target.IsDead && TargetInReach(hitRangeTolerance))
            {
                DealDamage();
            }

            yield return new WaitForSeconds(attackDuration * (1f - hitTiming));
            isAttacking = false;
        }

        private bool TargetInReach(float tolerance)
        {
            Vector2 offset = target.transform.position - transform.position;
            return Mathf.Abs(offset.x) <= contactRange + tolerance && offset.y <= jumpOverHeight;
        }

        private void DealDamage()
        {
            float damage = CombatMath.PhysicalDamage(monster.Attack, target.Stats.defense);
            target.TakeDamage(damage, transform.position);
        }
    }
}

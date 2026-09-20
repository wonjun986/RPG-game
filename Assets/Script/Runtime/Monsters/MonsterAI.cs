using UnityEngine;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 일반 몬스터용 간단한 AI. 보스처럼 콤보 패턴은 없고,
    // 플레이어를 향해 다가가다가 사거리에 들어오면 일정 주기로 단발 공격만 한다.
    [RequireComponent(typeof(Monster))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class MonsterAI : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float attackRange = 0.9f;
        [SerializeField] private float attackInterval = 1.2f;
        [SerializeField] private float gravityScale = 4f;

        private Monster monster;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Character target;
        private float attackCooldown;

        private void Awake()
        {
            monster = GetComponent<Monster>();
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            body.gravityScale = gravityScale;
            body.freezeRotation = true;

            target = Object.FindFirstObjectByType<Character>();
        }

        private void Update()
        {
            if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;
        }

        private void FixedUpdate()
        {
            if (monster.IsDead || target == null || target.IsDead)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            float distance = target.transform.position.x - transform.position.x;

            if (Mathf.Abs(distance) <= attackRange)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
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

            float damage = CombatMath.PhysicalDamage(monster.Attack, target.Stats.defense);
            target.TakeDamage(damage);
        }
    }
}

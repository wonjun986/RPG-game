using System.Collections;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 광활한 평야의 필드 보스. 플레이어가 감지 거리 안에 들어오면 폴짝폴짝 쫓아오며,
    // 중거리에서는 주기적으로 입을 벌려 물대포를 쏘고, 붙어 있으면 몸통 접촉으로 피해를 준다.
    // 말랑한 슬라임이라 기사 보스와 달리 경직/슈퍼아머는 없다(맞아도 공격이 끊기지 않음).
    [RequireComponent(typeof(Monster))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BossSlimeAI : MonoBehaviour
    {
        [Header("이동/감지")]
        [SerializeField] private float moveSpeed = 1.8f;
        [SerializeField] private float detectRange = 12f; // 이 거리 밖이면 제자리에서 대기
        [SerializeField] private float keepDistance = 1.2f; // 이보다 가까우면 더 다가가지 않는다
        [SerializeField] private float gravityScale = 4f;

        [Header("물대포")]
        [SerializeField] private float cannonRange = 8f;
        [SerializeField] private float cannonCooldown = 4.5f;
        [SerializeField] private float cannonAnimDuration = 1.4f; // 공격 6프레임 전체 길이
        [SerializeField, Range(0f, 1f)] private float cannonFireTiming = 4f / 6f; // 5번째 프레임(물대포 발사)에 맞춤
        [SerializeField] private float cannonDamageMultiplier = 1.5f;
        [SerializeField] private float cannonSpeed = 9f;
        [SerializeField] private float cannonTravelRange = 12f;
        [SerializeField] private float cannonHitRadius = 0.8f;
        [SerializeField] private float cannonMuzzleForward = 1.2f; // 몸 중심에서 입까지 앞쪽 거리

        [Header("몸통 접촉")]
        [SerializeField] private float contactRange = 1.7f;
        [SerializeField] private float contactInterval = 1f;
        [SerializeField] private float contactDamageMultiplier = 1f;

        private Monster monster;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private MonsterAttackSpriteAnimator attackAnimator;
        private Character target;

        private bool isAttacking;
        private float cannonTimer;
        private float contactTimer;

        private void Awake()
        {
            monster = GetComponent<Monster>();
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            attackAnimator = GetComponent<MonsterAttackSpriteAnimator>();
            body.gravityScale = gravityScale;
            body.freezeRotation = true;

            target = Object.FindFirstObjectByType<Character>();
            cannonTimer = cannonCooldown * 0.5f; // 마주치자마자 바로 쏘지 않도록 조금 늦춘다
        }

        private void Update()
        {
            if (cannonTimer > 0f && !isAttacking) cannonTimer -= Time.deltaTime;
            if (contactTimer > 0f) contactTimer -= Time.deltaTime;

            if (monster.IsDead || target == null || target.IsDead) return;
            TryContactDamage();
        }

        private void FixedUpdate()
        {
            if (monster.IsDead || isAttacking || monster.IsImmobilized || target == null || target.IsDead)
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

            spriteRenderer.flipX = dx < 0f;

            if (distance <= cannonRange && cannonTimer <= 0f && attackAnimator != null && attackAnimator.HasFrames)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                StartCoroutine(CannonRoutine());
                return;
            }

            float move = distance > keepDistance ? Mathf.Sign(dx) * moveSpeed : 0f;
            body.linearVelocity = new Vector2(move, body.linearVelocity.y);
        }

        private IEnumerator CannonRoutine()
        {
            isAttacking = true;
            cannonTimer = cannonCooldown;

            float facing = spriteRenderer.flipX ? -1f : 1f;
            attackAnimator.Play(cannonAnimDuration);

            yield return new WaitForSeconds(cannonAnimDuration * cannonFireTiming);
            if (!monster.IsDead)
            {
                Vector2 muzzle = (Vector2)transform.position + new Vector2(facing * cannonMuzzleForward, 0f);
                SlimeWaterBall.Launch(muzzle, new Vector2(facing, 0f), monster.Attack * cannonDamageMultiplier,
                    cannonSpeed, cannonTravelRange, cannonHitRadius);
            }

            yield return new WaitForSeconds(cannonAnimDuration * (1f - cannonFireTiming));
            isAttacking = false;
        }

        // 몸에 닿아 있는 동안 contactInterval마다 피해. 물대포를 쏘는 중에도 몸은 여전히 위험하다.
        private void TryContactDamage()
        {
            if (contactTimer > 0f) return;

            Vector2 offset = target.transform.position - transform.position;
            if (Mathf.Abs(offset.x) > contactRange || offset.y > 2f || offset.y < -0.5f) return;

            contactTimer = contactInterval;
            target.TakeDamage(CombatMath.PhysicalDamage(monster.Attack * contactDamageMultiplier, target.Stats.defense));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRange);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, cannonRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, contactRange);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // Shift = 대시 공격. 몬스터가 커서 점프로 넘어가기 힘든 경우가 많아서, 대시 중에는
    // 몬스터와의 물리 충돌을 잠시 무시해 그대로 통과하며 경로상의 몬스터에게 피해를 준다
    // (벽/바닥과의 충돌은 그대로 유지되어 맵 밖으로 나가지는 않는다).
    // 대시 중에는 무적 상태가 되어 회피기로도 쓸 수 있다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class CharacterSkillDash : MonoBehaviour
    {
        [SerializeField] private float cooldown = 1.5f;
        [SerializeField] private float dashDistance = 3f;
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float hitRadius = 0.6f;
        [SerializeField] private float damageMultiplier = 1f;

        private Character character;
        private CharacterMovement2D movement;
        private Rigidbody2D body;
        private Collider2D bodyCollider;

        private float cooldownRemaining;
        private bool isDashing;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            if (movement.IsInputLocked) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame))
            {
                TryUseSkill();
            }
        }

        private void TryUseSkill()
        {
            if (isDashing || cooldownRemaining > 0f) return;

            cooldownRemaining = cooldown;
            StartCoroutine(DashRoutine());
        }

        private IEnumerator DashRoutine()
        {
            isDashing = true;
            movement.SetInputLocked(true);
            character.SetInvincible(true);

            Vector2 direction = movement.FacingDirection;
            Vector2 start = body.position;
            Vector2 end = start + direction * dashDistance;

            var monsterColliders = BeginIgnoringMonsterCollisions();
            var alreadyHit = new HashSet<Monster>();

            float elapsed = 0f;
            while (elapsed < dashDuration)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / dashDuration);
                body.MovePosition(Vector2.Lerp(start, end, t));

                DealDamageAlongPath(alreadyHit);
            }

            RestoreMonsterCollisions(monsterColliders);

            character.SetInvincible(false);
            movement.SetInputLocked(false);
            isDashing = false;
        }

        private void DealDamageAlongPath(HashSet<Monster> alreadyHit)
        {
            var hits = Physics2D.OverlapCircleAll(body.position, hitRadius);
            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead || !alreadyHit.Add(monster)) continue;

                float damage = CombatMath.PhysicalDamage(character.Stats.attack * damageMultiplier, monster.Defense);
                monster.TakeDamage(damage);
            }
        }

        private List<Collider2D> BeginIgnoringMonsterCollisions()
        {
            var monsters = Object.FindObjectsByType<Monster>(FindObjectsSortMode.None);
            var colliders = new List<Collider2D>(monsters.Length);

            foreach (var monster in monsters)
            {
                var col = monster.GetComponent<Collider2D>();
                if (col == null) continue;

                colliders.Add(col);
                Physics2D.IgnoreCollision(bodyCollider, col, true);
            }

            return colliders;
        }

        private void RestoreMonsterCollisions(List<Collider2D> colliders)
        {
            foreach (var col in colliders)
            {
                if (col != null) Physics2D.IgnoreCollision(bodyCollider, col, false);
            }
        }
    }
}

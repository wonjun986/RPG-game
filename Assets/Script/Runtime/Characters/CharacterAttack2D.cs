using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // Z = 기본 공격. 바라보는 방향 앞의 원형 범위 안에 있는 몬스터를 때린다.
    // 연타 방지를 위해 자체 쿨타임(기본 0.5초)을 둔다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(AttackSpriteAnimator))]
    public class CharacterAttack2D : MonoBehaviour
    {
        [SerializeField] private float attackRange = 1.4f;
        [SerializeField] private float attackRadius = 0.6f;
        [SerializeField] private float cooldown = 0.5f;
        [SerializeField] private float manaRestoreOnAttack = 5f;

        private Character character;
        private CharacterMovement2D movement;
        private AttackSpriteAnimator attackAnimator;

        private float cooldownRemaining;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            attackAnimator = GetComponent<AttackSpriteAnimator>();
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            if (movement.IsInputLocked) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.zKey.wasPressedThisFrame)
            {
                TryPerformBasicAttack();
            }
        }

        private void TryPerformBasicAttack()
        {
            if (cooldownRemaining > 0f) return;

            cooldownRemaining = cooldown;
            PerformBasicAttack();
        }

        private void PerformBasicAttack()
        {
            attackAnimator.Play(movement.FacingDirection);
            character.RestoreMana(manaRestoreOnAttack);

            Vector2 origin = (Vector2)transform.position + movement.FacingDirection * attackRange;
            var hits = Physics2D.OverlapCircleAll(origin, attackRadius);

            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;

                float damage = CombatMath.PhysicalDamage(character.Stats.attack, monster.Defense);
                monster.TakeDamage(damage);
            }
        }

        private void OnDrawGizmosSelected()
        {
            var facingSource = movement != null ? movement : GetComponent<CharacterMovement2D>();
            if (facingSource == null) return;

            Gizmos.color = Color.red;
            Vector2 origin = (Vector2)transform.position + facingSource.FacingDirection * attackRange;
            Gizmos.DrawWireSphere(origin, attackRadius);
        }
    }
}

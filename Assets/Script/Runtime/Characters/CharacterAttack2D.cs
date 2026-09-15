using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // Z = 기본 공격. 바라보는 방향 앞의 원형 범위 안에 있는 몬스터를 때린다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(ScytheVisual))]
    public class CharacterAttack2D : MonoBehaviour
    {
        [SerializeField] private float attackRange = 0.9f;
        [SerializeField] private float attackRadius = 0.6f;

        private Character character;
        private CharacterMovement2D movement;
        private ScytheVisual weaponVisual;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            weaponVisual = GetComponent<ScytheVisual>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.zKey.wasPressedThisFrame)
            {
                PerformBasicAttack();
            }
        }

        private void PerformBasicAttack()
        {
            weaponVisual.PlaySwing(movement.FacingDirection);

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

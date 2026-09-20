using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // X = 특수 공격. 사슬을 빠르게 회전시켜 전방 넓은 범위를 벤다. 쿨타임은 없지만 마나를 소모한다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    public class CharacterSkillX : MonoBehaviour
    {
        [SerializeField] private float manaCost = 0f;
        [SerializeField] private float attackRange = 1.1f;
        [SerializeField] private float attackRadius = 1.0f;
        [SerializeField] private float damageMultiplier = 1.6f;

        private Character character;
        private CharacterMovement2D movement;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
        }

        private void Update()
        {
            if (movement.IsInputLocked) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
            {
                TryUseSkill();
            }
        }

        private void TryUseSkill()
        {
            if (!character.TrySpendMana(manaCost)) return;
            PerformSpinAttack();
        }

        private void PerformSpinAttack()
        {
            Vector2 origin = (Vector2)transform.position + movement.FacingDirection * attackRange;
            var hits = Physics2D.OverlapCircleAll(origin, attackRadius);

            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;

                float damage = CombatMath.PhysicalDamage(character.Stats.attack * damageMultiplier, monster.Defense);
                monster.TakeDamage(damage);
            }
        }

        private void OnDrawGizmosSelected()
        {
            var facingSource = movement != null ? movement : GetComponent<CharacterMovement2D>();
            if (facingSource == null) return;

            Gizmos.color = new Color(0.6f, 0.2f, 0.8f);
            Vector2 origin = (Vector2)transform.position + facingSource.FacingDirection * attackRange;
            Gizmos.DrawWireSphere(origin, attackRadius);
        }
    }
}

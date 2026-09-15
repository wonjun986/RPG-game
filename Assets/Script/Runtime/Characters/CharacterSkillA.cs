using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // A = 낫 연속 베기. 짧은 쿨타임의 3연타 근접 콤보 (기획서: 마나20, 쿨타임2초, 히트당 공격력x1.0, 총 0.6초).
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(ScytheVisual))]
    public class CharacterSkillA : MonoBehaviour
    {
        [SerializeField] private float manaCost = 20f;
        [SerializeField] private float cooldown = 2f;
        [SerializeField] private float hitDamageMultiplier = 1.0f;
        [SerializeField] private int hitCount = 3;
        [SerializeField] private float totalDuration = 0.6f;
        [SerializeField] private float attackRange = 0.9f;
        [SerializeField] private float attackRadius = 0.6f;

        private Character character;
        private CharacterMovement2D movement;
        private ScytheVisual weaponVisual;

        private float cooldownRemaining;
        private bool isAttacking;

        public float CooldownRemaining => cooldownRemaining;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            weaponVisual = GetComponent<ScytheVisual>();
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.aKey.wasPressedThisFrame)
            {
                TryUseSkill();
            }
        }

        private void TryUseSkill()
        {
            if (isAttacking || cooldownRemaining > 0f) return;
            if (!character.TrySpendMana(manaCost)) return;

            cooldownRemaining = cooldown;
            StartCoroutine(ComboRoutine());
        }

        private IEnumerator ComboRoutine()
        {
            isAttacking = true;
            movement.SetInputLocked(true);

            Vector2 direction = movement.FacingDirection;
            float interval = totalDuration / hitCount;

            for (int i = 0; i < hitCount; i++)
            {
                weaponVisual.PlaySwing(direction);
                DealDamage(direction);
                yield return new WaitForSeconds(interval);
            }

            movement.SetInputLocked(false);
            isAttacking = false;
        }

        private void DealDamage(Vector2 direction)
        {
            Vector2 origin = (Vector2)transform.position + direction * attackRange;
            var hits = Physics2D.OverlapCircleAll(origin, attackRadius);

            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;

                float damage = CombatMath.PhysicalDamage(character.Stats.attack * hitDamageMultiplier, monster.Defense);
                monster.TakeDamage(damage);
            }
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // Q = 돌진 베기. 마나를 소모해 짧은 거리를 빠르게 돌진하며 앞의 몬스터를 크게 때린다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(ScytheVisual))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterSkillQ : MonoBehaviour
    {
        [SerializeField] private float manaCost = 60f;
        [SerializeField] private float cooldown = 10f;
        [SerializeField] private float damageMultiplier = 1.2f;
        [SerializeField] private float dashDistance = 2.2f;
        [SerializeField] private float dashDuration = 0.15f;
        [SerializeField] private float hitRadius = 0.7f;

        private Character character;
        private CharacterMovement2D movement;
        private Rigidbody2D body;
        private ScytheVisual weaponVisual;

        private float cooldownRemaining;

        public float CooldownRemaining => cooldownRemaining;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            body = GetComponent<Rigidbody2D>();
            weaponVisual = GetComponent<ScytheVisual>();
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            {
                TryUseSkill();
            }
        }

        private void TryUseSkill()
        {
            if (cooldownRemaining > 0f) return;
            if (!character.TrySpendMana(manaCost)) return;

            cooldownRemaining = cooldown;
            StartCoroutine(DashAttackRoutine());
        }

        private IEnumerator DashAttackRoutine()
        {
            Vector2 direction = movement.FacingDirection;
            Vector2 start = body.position;
            Vector2 end = start + direction * dashDistance;

            movement.SetInputLocked(true);
            weaponVisual.PlaySwing(direction);

            float elapsed = 0f;
            while (elapsed < dashDuration)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / dashDuration);
                body.MovePosition(Vector2.Lerp(start, end, t));
            }

            DealDamage(body.position, direction);
            movement.SetInputLocked(false);
        }

        private void DealDamage(Vector2 origin, Vector2 direction)
        {
            var hits = Physics2D.OverlapCircleAll(origin + direction * 0.3f, hitRadius);
            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;

                float damage = CombatMath.PhysicalDamage(character.Stats.attack * damageMultiplier, monster.Defense);
                monster.TakeDamage(damage);
            }
        }
    }
}

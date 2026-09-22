using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // E = 섬광 절단. 짧게 순간이동한 뒤 그 자리에서 낫으로 크게 벤다. 소울이터 최고의 단일 데미지 스킬.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterSkillE : MonoBehaviour
    {
        [SerializeField] private float manaCost = 65f;
        [SerializeField] private float cooldown = 15f;
        [SerializeField] private float teleportDistance = 1.5f;
        [SerializeField] private float recoveryDuration = 0.15f;
        [SerializeField] private float hitRadius = 1.0f;
        [SerializeField] private float damageMultiplier = 3.0f;
        [SerializeField] private string chainBurstEffectPath = "Art/Effects/ChainBurst";
        [SerializeField] private float chainBurstFrameDuration = 0.05f;

        private Character character;
        private CharacterMovement2D movement;
        private Rigidbody2D body;

        private float cooldownRemaining;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            body = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            if (movement.IsInputLocked) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                TryUseSkill();
            }
        }

        private void TryUseSkill()
        {
            if (character.IsCombatLocked) return;
            if (cooldownRemaining > 0f) return;
            if (!character.TrySpendMana(manaCost)) return;

            cooldownRemaining = cooldown;
            StartCoroutine(FlashStrikeRoutine());
        }

        private IEnumerator FlashStrikeRoutine()
        {
            movement.SetInputLocked(true);

            Vector2 direction = movement.FacingDirection;
            Vector2 destination = body.position + direction * teleportDistance;
            body.position = destination;

            FlipbookSpriteEffect.Create("ChainBurst", chainBurstEffectPath, destination, chainBurstFrameDuration, loop: false);
            DealDamage(destination, direction);

            yield return new WaitForSeconds(recoveryDuration);

            movement.SetInputLocked(false);
        }

        private void DealDamage(Vector2 origin, Vector2 direction)
        {
            var hits = Physics2D.OverlapCircleAll(origin + direction * 0.5f, hitRadius);
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

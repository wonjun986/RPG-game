using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // D = 낫 부메랑 스트라이크. 낫을 멀리 던졌다가 다시 손으로 회수한다.
    // 던질 때와 받을 때 각각 한 번씩 피해를 준다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    public class CharacterSkillD : MonoBehaviour
    {
        [SerializeField] private float manaCost = 30f;
        [SerializeField] private float cooldown = 7f;
        [SerializeField] private float throwRange = 4f;
        [SerializeField] private float throwDuration = 0.35f;
        [SerializeField] private float returnDuration = 0.35f;
        [SerializeField] private float hitRadius = 0.7f;
        [SerializeField] private float damageMultiplier = 1.5f;
        [SerializeField] private string scytheEffectPath = "Art/Effects/ScytheSpin";
        [SerializeField] private float spinFrameDuration = 0.05f;

        private Character character;
        private CharacterMovement2D movement;

        private float cooldownRemaining;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            if (movement.IsInputLocked) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.dKey.wasPressedThisFrame)
            {
                TryUseSkill();
            }
        }

        private void TryUseSkill()
        {
            if (cooldownRemaining > 0f) return;
            if (!character.TrySpendMana(manaCost)) return;

            cooldownRemaining = cooldown;
            StartCoroutine(BoomerangRoutine());
        }

        // 던진 직후에는 다른 행동도 가능하도록 이동 입력을 잠그지 않는다.
        private IEnumerator BoomerangRoutine()
        {
            Vector2 direction = movement.FacingDirection;
            Vector2 throwOrigin = transform.position;
            Vector2 farPoint = throwOrigin + direction * throwRange;

            var flyingScythe = FlipbookSpriteEffect.Create("BoomerangScythe", scytheEffectPath, throwOrigin, spinFrameDuration, loop: true);

            float elapsed = 0f;
            while (elapsed < throwDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / throwDuration);
                flyingScythe.transform.position = Vector2.Lerp(throwOrigin, farPoint, t);
                yield return null;
            }

            DealDamage(farPoint);

            elapsed = 0f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                Vector2 currentOrigin = transform.position;
                flyingScythe.transform.position = Vector2.Lerp(farPoint, currentOrigin, t);
                yield return null;
            }

            DealDamage(transform.position);
            Destroy(flyingScythe.gameObject);
        }

        private void DealDamage(Vector2 point)
        {
            var hits = Physics2D.OverlapCircleAll(point, hitRadius);
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

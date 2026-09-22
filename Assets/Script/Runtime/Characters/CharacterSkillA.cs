using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // A = 낫 회전 베기. 몸 주위 원형 범위를 즉시 크게 벤다.
    // 최대 3충전(charge) 방식: 충전이 남아있는 한 연달아 재시전할 수 있고,
    // 최대치보다 모자랄 때만 충전 하나당 cooldown 시간을 들여 서서히 회복한다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(ASkillSpriteAnimator))]
    public class CharacterSkillA : MonoBehaviour
    {
        [SerializeField] private float manaCost = 0f;
        [SerializeField] private int maxCharges = 3;
        [SerializeField] private float rechargeCooldown = 2f;
        [SerializeField] private float damageMultiplier = 1.0f;
        [SerializeField] private float radius = 1.5f;

        private Character character;
        private CharacterMovement2D movement;
        private ASkillSpriteAnimator skillAnimator;

        private int currentCharges;
        private float rechargeTimer;

        public int CurrentCharges => currentCharges;
        public int MaxCharges => maxCharges;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            skillAnimator = GetComponent<ASkillSpriteAnimator>();

            currentCharges = maxCharges;
        }

        private void Update()
        {
            if (currentCharges < maxCharges)
            {
                rechargeTimer -= Time.deltaTime;
                if (rechargeTimer <= 0f)
                {
                    currentCharges++;
                    rechargeTimer = currentCharges < maxCharges ? rechargeCooldown : 0f;
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.aKey.wasPressedThisFrame)
            {
                TryUseSkill();
            }
        }

        private void TryUseSkill()
        {
            if (character.IsCombatLocked) return;
            if (currentCharges <= 0) return;
            if (!character.TrySpendMana(manaCost)) return;

            if (currentCharges == maxCharges) rechargeTimer = rechargeCooldown;
            currentCharges--;

            PerformSlash();
        }

        private void PerformSlash()
        {
            skillAnimator.Play(movement.FacingDirection);

            Vector2 origin = transform.position;
            var hits = Physics2D.OverlapCircleAll(origin, radius);

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
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

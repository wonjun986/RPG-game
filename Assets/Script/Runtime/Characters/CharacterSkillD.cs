using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // D = 낫 부메랑 스트라이크. 낫을 멀리 던졌다가 다시 손으로 회수한다.
    // 날아가는 동안과 돌아오는 동안 경로에 겹치는 모든 적에게 피해를 준다(같은 다리에서는 적당 한 번씩만).
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
            var hitThisLeg = new HashSet<Monster>();

            float elapsed = 0f;
            while (elapsed < throwDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / throwDuration);
                flyingScythe.transform.position = Vector2.Lerp(throwOrigin, farPoint, t);
                DealDamageAlongPath(flyingScythe.transform.position, hitThisLeg);
                yield return null;
            }

            hitThisLeg.Clear(); // 돌아오는 다리에서는 같은 적이라도 다시 한 번 맞을 수 있게 초기화

            elapsed = 0f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                Vector2 currentOrigin = transform.position;
                flyingScythe.transform.position = Vector2.Lerp(farPoint, currentOrigin, t);
                DealDamageAlongPath(flyingScythe.transform.position, hitThisLeg);
                yield return null;
            }

            Destroy(flyingScythe.gameObject);
        }

        // 매 프레임 낫의 현재 위치를 기준으로 피해를 확인한다. alreadyHit에 있는 몬스터는
        // 같은 다리(던지기/회수) 안에서는 건너뛰어 프레임마다 중복 피해가 들어가지 않게 한다.
        private void DealDamageAlongPath(Vector2 point, HashSet<Monster> alreadyHit)
        {
            var hits = Physics2D.OverlapCircleAll(point, hitRadius);
            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;
                if (!alreadyHit.Add(monster)) continue;

                float damage = CombatMath.PhysicalDamage(character.Stats.attack * damageMultiplier, monster.Defense);
                monster.TakeDamage(damage);
            }
        }
    }
}

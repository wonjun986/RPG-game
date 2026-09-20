using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // Q = 어둠의 주먹. 전방으로 거대한 어둠의 주먹을 날려, 경로상의 몬스터를 모두 강타한다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    public class CharacterSkillQ : MonoBehaviour
    {
        [SerializeField] private float manaCost = 40f;
        [SerializeField] private float cooldown = 10f;
        [SerializeField] private float damageMultiplier = 1.2f;
        [SerializeField] private float travelDistance = 5f;
        [SerializeField] private float travelDuration = 0.3f;
        [SerializeField] private float hitRadius = 0.7f;
        [SerializeField] private string fistEffectPath = "Art/Effects/DarkFist";

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
            StartCoroutine(FistPunchRoutine());
        }

        // 캐릭터는 그 자리에 있고, 주먹만 날아가면서 맞은 몬스터를 전부(관통) 때린다.
        private IEnumerator FistPunchRoutine()
        {
            Vector2 direction = movement.FacingDirection;
            Vector2 start = (Vector2)transform.position + Vector2.up * 0.7f;
            Vector2 end = start + direction * travelDistance;

            var fist = FlipbookSpriteEffect.Create("DarkFist", fistEffectPath, start, travelDuration / 8f, loop: false);
            fist.GetComponent<SpriteRenderer>().flipX = direction.x < 0f;
            var alreadyHit = new HashSet<Monster>();

            float elapsed = 0f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                Vector2 position = Vector2.Lerp(start, end, t);
                fist.transform.position = position;

                DealDamage(position, alreadyHit);
                yield return null;
            }

            Destroy(fist.gameObject);
        }

        private void DealDamage(Vector2 point, HashSet<Monster> alreadyHit)
        {
            var hits = Physics2D.OverlapCircleAll(point, hitRadius);
            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead || !alreadyHit.Add(monster)) continue;

                float damage = CombatMath.PhysicalDamage(character.Stats.attack * damageMultiplier, monster.Defense);
                monster.TakeDamage(damage);
            }
        }
    }
}

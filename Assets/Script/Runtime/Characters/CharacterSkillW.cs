using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // W = 사슬 폭풍. 제자리에서 사슬을 휘둘러 주위 넓은 범위를 여러 번 연속으로 타격한다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(WSkillSpriteAnimator))]
    public class CharacterSkillW : MonoBehaviour
    {
        [SerializeField] private float initialManaCost = 20f;
        [SerializeField] private float manaDrainPerSecond = 5f;
        [SerializeField] private float maxHoldDuration = 5f;
        [SerializeField] private float cooldown = 12f;
        [SerializeField] private float radius = 2f;
        [SerializeField] private int hitCount = 4;
        [SerializeField] private float duration = 0.7f;
        [SerializeField] private float damageMultiplierPerHit = 0.9f;

        private Character character;
        private CharacterMovement2D movement;
        private WSkillSpriteAnimator skillAnimator;

        private float cooldownRemaining;
        private bool isBusy;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            skillAnimator = GetComponent<WSkillSpriteAnimator>();
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            if (movement.IsInputLocked) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.wKey.wasPressedThisFrame)
            {
                TryUseSkill();
            }
        }

        private void TryUseSkill()
        {
            if (character.IsCombatLocked) return;
            if (isBusy || cooldownRemaining > 0f) return;
            if (!character.TrySpendMana(initialManaCost)) return;

            StartCoroutine(StormRoutine());
        }

        // 처음 시전 시 initialManaCost를 소모하고, 이후 W를 누르고 있는 동안
        // 초당 manaDrainPerSecond씩 추가로 소모하며 최대 maxHoldDuration초까지 유지된다.
        // 키를 떼거나 마나가 바닥나거나 최대 유지 시간에 도달하면 채널링이 끝나고 쿨타임이 시작된다.
        // 애니메이션은 사이클 단위로 끊어 재생하지 않고 채널링 내내 한 번만 시작해 계속 이어서
        // 반복시킨다 (매 타격마다 다시 시작하면 뚝뚝 끊겨 부자연스럽다).
        private IEnumerator StormRoutine()
        {
            isBusy = true;
            movement.SetInputLocked(true);
            skillAnimator.PlayLooping(movement.FacingDirection);

            float interval = duration / hitCount;
            var keyboard = Keyboard.current;
            float elapsed = 0f;

            while (elapsed < maxHoldDuration)
            {
                DealDamage();
                yield return new WaitForSeconds(interval);
                elapsed += interval;

                bool stillHeld = keyboard != null && keyboard.wKey.isPressed;
                if (!stillHeld) break;
                if (!character.TrySpendMana(manaDrainPerSecond * interval)) break;
            }

            skillAnimator.Stop();
            cooldownRemaining = cooldown;
            movement.SetInputLocked(false);
            isBusy = false;
        }

        private void DealDamage()
        {
            Vector2 origin = transform.position;
            var hits = Physics2D.OverlapCircleAll(origin, radius);

            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;

                float damage = CombatMath.PhysicalDamage(character.Stats.attack * damageMultiplierPerHit, monster.Defense);
                monster.TakeDamage(damage);

                // W로 계속 두들기는 동안은 보스의 슈퍼아머 주기를 계속 처음부터 다시 세게 만들어서
                // 채널링이 끝날 때까지 경직 상태로 계속 맞게 한다.
                var bossAI = hit.GetComponent<BossAI>();
                if (bossAI != null) bossAI.ResetSuperArmorTimer();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

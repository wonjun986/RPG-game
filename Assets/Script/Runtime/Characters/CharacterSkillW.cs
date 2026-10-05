using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // W = 악마의 손. 바라보는 방향 앞에 포탈을 열어 악마의 손을 뻗어내, 일정 시간 동안 전방 범위의 적을 연타한다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(WSkillSpriteAnimator))]
    public class CharacterSkillW : MonoBehaviour, ISkillCooldownReset
    {
        [SerializeField] private float manaCost = 20f;
        [SerializeField] private float cooldown = 10f;

        [Header("연타")]
        // 손이 뻗어 있는 동안 hitInterval마다 한 번씩, 총 hitCount번 타격한다(지속 시간 = hitCount * hitInterval).
        [SerializeField] private int hitCount = 16;
        [SerializeField] private float hitInterval = 0.1f;
        [SerializeField] private float damageMultiplierPerHit = 0.25f;

        [Header("시전 타이밍")]
        // 시전 애니메이션에서 손앞에 포탈이 열리는 프레임(3번째)에 맞춰 악마의 손 이펙트를 띄운다.
        [SerializeField] private int handSpawnFrame = 2;

        [Header("악마의 손 이펙트")]
        [SerializeField] private string handEffectPath = "Art/Effects/DemonHand";
        [SerializeField] private float handFrameDuration = 0.07f;
        // 손이 가장 크게 뻗어 있는 4~6번째 프레임을 지속 시간 동안 반복한다.
        [SerializeField] private int handLoopStart = 3;
        [SerializeField] private int handLoopEnd = 5;
        // 이펙트의 포탈(왼쪽 끝) 위치. 발밑 기준, x는 바라보는 방향 쪽.
        [SerializeField] private Vector2 handOffset = new Vector2(0.9f, 0.85f);

        [Header("타격 범위 (발밑 기준, x는 바라보는 방향 쪽)")]
        [SerializeField] private Vector2 hitBoxCenter = new Vector2(1.9f, 0.9f);
        [SerializeField] private Vector2 hitBoxSize = new Vector2(3.0f, 1.8f);

        private Character character;
        private CharacterMovement2D movement;
        private WSkillSpriteAnimator skillAnimator;

        private float cooldownRemaining;
        private bool isBusy;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;

        public void ResetCooldown() => cooldownRemaining = 0f;

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
            if (!character.TrySpendMana(manaCost)) return;

            cooldownRemaining = cooldown;
            StartCoroutine(CastRoutine());
        }

        private IEnumerator CastRoutine()
        {
            isBusy = true;
            movement.SetInputLocked(true);

            Vector2 facing = movement.FacingDirection;
            float sign = facing.x < 0f ? -1f : 1f;
            skillAnimator.Play(facing);

            yield return new WaitForSeconds(skillAnimator.FrameDuration * handSpawnFrame);

            Vector2 feet = transform.position;
            var hand = DemonHandEffect.Create(handEffectPath, feet + new Vector2(sign * handOffset.x, handOffset.y), sign,
                handFrameDuration, handLoopStart, handLoopEnd, sortingOrder: 3);

            // 손이 다 뻗어나온 뒤부터 연타한다.
            yield return new WaitForSeconds(handFrameDuration * handLoopStart);

            for (int i = 0; i < hitCount; i++)
            {
                DealDamage(sign);
                yield return new WaitForSeconds(hitInterval);
            }

            if (hand != null) hand.Release();
            skillAnimator.Release();
            yield return new WaitForSeconds(skillAnimator.OutroDuration);

            movement.SetInputLocked(false);
            isBusy = false;
        }

        private void DealDamage(float sign)
        {
            Vector2 center = (Vector2)transform.position + new Vector2(sign * hitBoxCenter.x, hitBoxCenter.y);
            var hits = Physics2D.OverlapBoxAll(center, hitBoxSize, 0f);

            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;

                character.DealDamage(monster, damageMultiplierPerHit, isSkill: true);

                // 연타가 이어지는 동안은 보스의 슈퍼아머 주기를 계속 처음부터 다시 세게 만들어서
                // 끝날 때까지 경직 상태로 계속 맞게 한다.
                var bossAI = hit.GetComponent<BossAI>();
                if (bossAI != null) bossAI.ResetSuperArmorTimer();
            }
        }

        private void OnDrawGizmosSelected()
        {
            float sign = 1f;
            if (Application.isPlaying && movement != null && movement.FacingDirection.x < 0f) sign = -1f;
            Vector2 center = (Vector2)transform.position + new Vector2(sign * hitBoxCenter.x, hitBoxCenter.y);
            Gizmos.color = new Color(0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(center, hitBoxSize);
        }
    }
}

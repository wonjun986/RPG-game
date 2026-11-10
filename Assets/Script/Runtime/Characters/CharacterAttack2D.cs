using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Bootstrap;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // Z = 기본 공격. 낫이 휘둘러지는 호의 안쪽(몸 앞부터 칼날 끝까지, 발밑부터 머리 위까지) 상자 안에 있는 몬스터를 때린다.
    // 연타 방지를 위해 자체 쿨타임(기본 0.5초)을 둔다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(AttackSpriteAnimator))]
    public class CharacterAttack2D : MonoBehaviour
    {
        // 공격 시트(캔버스 키 364px → 1.5유닛, 발 기준점 x 264)에서 칼날 호가 몸 앞 약 1.1유닛, 발밑 0.1~1.3유닛 높이를 감싼다.
        // 손맛을 위해 칼날 끝보다 0.5유닛쯤 더 멀리까지 닿게 한다. 값은 발 기준점(바라보는 쪽이 +x)에서 잰 거리.
        [SerializeField] private float attackNear = 0.1f;
        [SerializeField] private float attackFar = 1.6f;
        [SerializeField] private float attackBottom = 0f;
        [SerializeField] private float attackTop = 1.4f;
        // 공격 모션(AttackSpriteAnimator, 0.35초) 길이와 맞춰 둔다. 모션이 끝나자마자 다음 공격이 나간다.
        [SerializeField] private float cooldown = 0.35f;
        [SerializeField] private float manaRestoreOnAttack = 5f;

        private Character character;
        private CharacterMovement2D movement;
        private AttackSpriteAnimator attackAnimator;

        private float cooldownRemaining;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            attackAnimator = GetComponent<AttackSpriteAnimator>();
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            if (movement.IsInputLocked || character.IsCombatLocked) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.zKey.wasPressedThisFrame)
            {
                TryPerformBasicAttack();
            }
        }

        private void TryPerformBasicAttack()
        {
            if (cooldownRemaining > 0f) return;
            if (!movement.IsGrounded) return; // 공중에서는 공격과 스킬을 쓸 수 없다

            cooldownRemaining = cooldown;
            PerformBasicAttack();
        }

        private void PerformBasicAttack()
        {
            attackAnimator.Play(movement.FacingDirection);
            SfxPlayer.Play(SfxPlayer.NecrosiaZAttack);
            character.RestoreMana(manaRestoreOnAttack);

            var hits = Physics2D.OverlapBoxAll(AttackBoxCenter(movement.FacingDirection), AttackBoxSize, 0f);

            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;

                character.DealDamage(monster, 1f, isSkill: false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            var facingSource = movement != null ? movement : GetComponent<CharacterMovement2D>();
            if (facingSource == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(AttackBoxCenter(facingSource.FacingDirection), AttackBoxSize);
        }

        private Vector2 AttackBoxSize => new Vector2(attackFar - attackNear, attackTop - attackBottom);

        private Vector2 AttackBoxCenter(Vector2 facing)
        {
            float side = facing.x < 0f ? -1f : 1f;
            return (Vector2)transform.position + new Vector2(side * (attackNear + attackFar) / 2f, (attackBottom + attackTop) / 2f);
        }
    }
}

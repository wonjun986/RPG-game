using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // Shift = 대시 공격. 몬스터가 커서 점프로 넘어가기 힘든 경우가 많아서, 대시 중에는
    // 몬스터와의 물리 충돌을 잠시 무시해 그대로 통과하며 경로상의 몬스터에게 피해를 준다
    // (벽/바닥과의 충돌은 그대로 유지되어 맵 밖으로 나가지는 않는다).
    // 대시 중에는 무적 상태가 되어 회피기로도 쓸 수 있다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class CharacterSkillDash : MonoBehaviour, ISkillCooldownReset
    {
        [SerializeField] private float cooldown = 1.5f;
        [SerializeField] private float dashDistance = 3f;
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float hitRadius = 0.6f;
        [SerializeField] private float damageMultiplier = 1f;

        [Header("대시 모션")]
        [SerializeField] private string dashFramesPath = "Art/Necrosia/Dash";
        // 대시 프레임은 낫을 크게 휘두르는 궤적까지 포함해 프레임 전체 높이 중 몸통이 차지하는
        // 비중이 걷기보다 작다(Idle이 낫을 머리 위로 든 것과 같은 이유). 기준 키(1.5)를 그대로 쓰면
        // 몸통이 걷기보다 작아 보여서, 대시 전용으로 더 큰 기준 키를 쓴다. Idle(1.8)보다도 체감상 더
        // 작아 보인다는 피드백에 따라 2.1로 키웠다.
        [SerializeField] private float dashTargetHeight = 2.1f;

        private Character character;
        private CharacterMovement2D movement;
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private SpriteRenderer visualRenderer;
        private WalkSpriteAnimator walkAnimator;
        private JumpSpriteAnimator jumpAnimator;
        private Sprite[] dashFrames;

        private float cooldownRemaining;
        private bool isDashing;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;

        public void ResetCooldown() => cooldownRemaining = 0f;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            walkAnimator = GetComponent<WalkSpriteAnimator>();
            jumpAnimator = GetComponent<JumpSpriteAnimator>();

            var visual = transform.Find("Visual");
            visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();

            dashFrames = LoadSortedFrames(dashFramesPath);
        }

        private static Sprite[] LoadSortedFrames(string path)
        {
            var frames = Resources.LoadAll<Sprite>(path);
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            return frames;
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            if (movement.IsInputLocked) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame))
            {
                TryUseSkill();
            }
        }

        private void TryUseSkill()
        {
            if (character.IsCombatLocked) return;
            if (isDashing || cooldownRemaining > 0f) return;

            cooldownRemaining = cooldown;
            StartCoroutine(DashRoutine());
        }

        private IEnumerator DashRoutine()
        {
            isDashing = true;
            movement.SetInputLocked(true);
            character.SetInvincible(true);

            Vector2 direction = movement.FacingDirection;
            Vector2 start = body.position;
            Vector2 end = start + direction * dashDistance;

            BeginDashPose(direction);

            var monsterColliders = BeginIgnoringMonsterCollisions();
            var alreadyHit = new HashSet<Monster>();

            float elapsed = 0f;
            while (elapsed < dashDuration)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / dashDuration);
                body.MovePosition(Vector2.Lerp(start, end, t));

                DealDamageAlongPath(alreadyHit);
                UpdateDashFrame(t);
            }

            RestoreMonsterCollisions(monsterColliders);
            EndDashPose();

            character.SetInvincible(false);
            movement.SetInputLocked(false);
            isDashing = false;
        }

        // 걷기/점프 애니메이터가 스프라이트를 덮어쓰지 않도록 잠시 끄고, 대시가 끝나면 돌려준다.
        private void BeginDashPose(Vector2 direction)
        {
            if (walkAnimator != null) walkAnimator.enabled = false;
            if (jumpAnimator != null) jumpAnimator.enabled = false;
            if (visualRenderer == null) return;

            visualRenderer.flipX = direction.x < 0f;
            if (dashFrames != null && dashFrames.Length > 0) SetDashFrame(dashFrames[0]);
        }

        private void UpdateDashFrame(float t)
        {
            if (visualRenderer == null || dashFrames == null || dashFrames.Length == 0) return;

            int frameIndex = Mathf.Clamp(Mathf.FloorToInt(t * dashFrames.Length), 0, dashFrames.Length - 1);
            SetDashFrame(dashFrames[frameIndex]);
        }

        // 대시 시트도 걷기처럼 프레임마다 캐릭터를 감싸는 크롭 크기가 제각각이라(특히 슬래시 이펙트가
        // 걸치는 프레임), 걷기와 같은 기준 키로 매 프레임 보정해야 대시 중 캐릭터 크기가 안 흔들린다.
        private void SetDashFrame(Sprite frame)
        {
            visualRenderer.sprite = frame;
            SkillFrameNormalizer.Apply(visualRenderer.transform, frame, dashTargetHeight);
        }

        private void EndDashPose()
        {
            if (walkAnimator != null) walkAnimator.enabled = true;
            if (jumpAnimator != null) jumpAnimator.enabled = true;
            // 점프 애니메이터는 스스로 스케일을 보정하지 않고 1배를 그대로 쓰므로, 대시가 남긴
            // 보정 스케일을 원래대로 되돌려 둔다(ESkill이 시작할 때 하는 것과 같은 이유).
            if (visualRenderer != null) SkillFrameNormalizer.Reset(visualRenderer.transform);
        }

        private void DealDamageAlongPath(HashSet<Monster> alreadyHit)
        {
            var hits = Physics2D.OverlapCircleAll(body.position, hitRadius);
            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead || !alreadyHit.Add(monster)) continue;

                character.DealDamage(monster, damageMultiplier, isSkill: true);
            }
        }

        private List<Collider2D> BeginIgnoringMonsterCollisions()
        {
            var monsters = Object.FindObjectsByType<Monster>(FindObjectsSortMode.None);
            var colliders = new List<Collider2D>(monsters.Length);

            foreach (var monster in monsters)
            {
                var col = monster.GetComponent<Collider2D>();
                if (col == null) continue;

                colliders.Add(col);
                Physics2D.IgnoreCollision(bodyCollider, col, true);
            }

            return colliders;
        }

        private void RestoreMonsterCollisions(List<Collider2D> colliders)
        {
            foreach (var col in colliders)
            {
                if (col != null) Physics2D.IgnoreCollision(bodyCollider, col, false);
            }
        }
    }
}

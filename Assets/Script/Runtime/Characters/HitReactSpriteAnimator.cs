using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // Character.OnDamaged를 구독해서 피격당할 때마다 짧게 스태거 포즈(Art/Necrosia/Hit)를 보여준다.
    // 재생 중엔 걷기/점프 애니메이터를 잠시 꺼 뒀다가 끝나면 돌려준다(대시/스킬과 같은 방식).
    // 죽어서 체력이 0이 된 타격은 재생하지 않는다(죽는 연출은 DeathReturnUI 쪽에서 따로 처리한다).
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    public class HitReactSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string hitFramesPath = "Art/Necrosia/Hit";
        [SerializeField] private float frameInterval = 0.08f;
        // Hit 프레임도 Idle처럼 낫을 들어 올린 포즈라 프레임 전체 높이 중 몸통 비중이 걷기보다 작다.
        [SerializeField] private float hitTargetHeight = 1.8f;
        [SerializeField] private float flickerInterval = 0.06f;

        private Character character;
        private CharacterMovement2D movement;
        private SpriteRenderer visualRenderer;
        private WalkSpriteAnimator walkAnimator;
        private JumpSpriteAnimator jumpAnimator;
        private Sprite[] hitFrames;
        private Coroutine poseRoutine;
        private Coroutine flickerRoutine;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            walkAnimator = GetComponent<WalkSpriteAnimator>();
            jumpAnimator = GetComponent<JumpSpriteAnimator>();

            var visual = transform.Find("Visual");
            visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();

            hitFrames = LoadSortedFrames(hitFramesPath);
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

        private void OnEnable()
        {
            if (character != null) character.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (character != null) character.OnDamaged -= HandleDamaged;
            if (visualRenderer != null) visualRenderer.enabled = true;
        }

        private void HandleDamaged(Character target, float amount)
        {
            if (character.IsDead) return;

            if (hitFrames != null && hitFrames.Length > 0)
            {
                if (poseRoutine != null) StopCoroutine(poseRoutine);
                poseRoutine = StartCoroutine(PlayHitReact());
            }

            // 깜빡임은 Character.PostHitInvincibleDuration(피격 직후 무적 시간)과 똑같이 맞춰서,
            // 화면에서 "지금 무적이다"를 그대로 보여준다.
            if (flickerRoutine != null) StopCoroutine(flickerRoutine);
            flickerRoutine = StartCoroutine(FlickerRoutine(character.PostHitInvincibleDuration));
        }

        private IEnumerator PlayHitReact()
        {
            if (walkAnimator != null) walkAnimator.enabled = false;
            if (jumpAnimator != null) jumpAnimator.enabled = false;
            if (visualRenderer != null) visualRenderer.flipX = movement.FacingDirection.x < 0f;

            foreach (var frame in hitFrames)
            {
                if (visualRenderer != null)
                {
                    visualRenderer.sprite = frame;
                    SkillFrameNormalizer.Apply(visualRenderer.transform, frame, hitTargetHeight);
                }
                yield return new WaitForSeconds(frameInterval);

                // 맞는 도중 스킬을 쓰는 등 다른 동작이 스프라이트를 가져갔으면 조용히 물러난다(복구는 그쪽이 한다).
                if (visualRenderer != null && visualRenderer.sprite != frame)
                {
                    poseRoutine = null;
                    yield break;
                }
            }

            if (visualRenderer != null) SkillFrameNormalizer.Reset(visualRenderer.transform);
            if (walkAnimator != null) walkAnimator.enabled = true;
            if (jumpAnimator != null) jumpAnimator.enabled = true;
            poseRoutine = null;
        }

        private IEnumerator FlickerRoutine(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (visualRenderer != null) visualRenderer.enabled = !visualRenderer.enabled;
                yield return new WaitForSeconds(flickerInterval);
                elapsed += flickerInterval;
            }

            if (visualRenderer != null) visualRenderer.enabled = true;
            flickerRoutine = null;
        }
    }
}

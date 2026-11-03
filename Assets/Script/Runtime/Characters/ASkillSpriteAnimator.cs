using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // Resources/Art/Necrosia/ASkill 아래의 사슬낫 공격(A) 프레임을 불러와, 요청 시 한 번만 재생한다.
    // 재생 중에는 WalkSpriteAnimator(걷기/대기 스프라이트)를 잠시 꺼서 서로 스프라이트를
    // 덮어쓰지 않게 하고, 끝나면 다시 켜서 제어권을 돌려준다.
    public class ASkillSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string resourcesPath = "Art/Necrosia/ASkill";
        [SerializeField] private float totalDuration = 0.5f;
        // A스킬 시트(보랏빛 사슬낫 공격, 12프레임)에서 서 있는 1번째 프레임의 머리 장식~부츠 키(원본 px).
        // 12장을 같은 캔버스에 발 위치를 맞춰 잘라 두었으므로 시트 전체에 같은 배율을 건다.
        [SerializeField] private float bodyPixelHeight = 239f;
        // 그 키가 대기 모션의 서 있는 키(머리 장식~부츠 약 1.53유닛)와 같아 보이게 한다.
        [SerializeField] private float bodyTargetHeight = 1.53f;

        private SpriteRenderer spriteRenderer;
        private WalkSpriteAnimator walkAnimator;
        private JumpSpriteAnimator jumpAnimator;
        private Sprite[] frames;
        private Coroutine playRoutine;

        public bool HasFrames => frames != null && frames.Length > 0;

        private void Awake()
        {
            walkAnimator = GetComponent<WalkSpriteAnimator>();
            jumpAnimator = GetComponent<JumpSpriteAnimator>();

            var visual = transform.Find("Visual");
            spriteRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();

            frames = Resources.LoadAll<Sprite>(resourcesPath);
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
        }

        public void Play(Vector2 facingDirection)
        {
            if (!HasFrames) return;
            // 공격 모션이 걸음째 미끄러져 보이지 않게 재생하는 동안 제자리에 세운다.
            GetComponent<CharacterMovement2D>()?.RootFor(totalDuration);
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayRoutine(facingDirection));
        }

        private IEnumerator PlayRoutine(Vector2 facingDirection)
        {
            if (walkAnimator != null) walkAnimator.enabled = false;
            if (jumpAnimator != null) jumpAnimator.enabled = false; // 공중에서도 점프 스프라이트가 덮어쓰지 않게
            spriteRenderer.flipX = facingDirection.x < 0f;

            float frameDuration = totalDuration / frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                spriteRenderer.sprite = frames[i];
                SkillFrameNormalizer.ApplyBody(spriteRenderer.transform, frames[i], bodyPixelHeight, bodyTargetHeight);
                yield return new WaitForSeconds(frameDuration);
                // 그새 다른 동작(스킬/피격 등)이 스프라이트를 가져갔으면 조용히 물러난다. 걷기/점프
                // 애니메이터 복구와 스케일 초기화는 마지막에 가져간 쪽이 끝날 때 한다(여기서 하면
                // 그쪽 재생 도중에 걷기 스프라이트가 덮어써서 이상하게 캔슬된 것처럼 보였다).
                if (spriteRenderer.sprite != frames[i])
                {
                    playRoutine = null;
                    yield break;
                }
            }

            SkillFrameNormalizer.Reset(spriteRenderer.transform);
            if (walkAnimator != null) walkAnimator.enabled = true;
            if (jumpAnimator != null) jumpAnimator.enabled = true;
            playRoutine = null;
        }
    }
}

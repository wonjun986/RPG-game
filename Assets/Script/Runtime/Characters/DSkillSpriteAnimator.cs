using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // Resources/Art/Necrosia/DSkill 아래의 낫 던지기(D) 프레임을 불러와, 요청 시 한 번만 재생한다.
    // 낫이 날아가는 동안 앞쪽 절반(던지기), 돌아오는 동안 뒤쪽 절반(팔을 들어 받아내기)이 재생되도록
    // CharacterSkillD가 던지기+회수 전체 시간을 넘겨준다.
    // 재생 중에는 WalkSpriteAnimator를 잠시 꺼서 서로 스프라이트를 덮어쓰지 않게 하고, 끝나면 다시 켠다.
    public class DSkillSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string resourcesPath = "Art/Necrosia/DSkill";
        // 프레임(고딕 마녀의 연속 투척 동작, 8프레임)은 한 줄 시트에서 이웃 프레임과 겹친 머리카락 사이로 가장 덜 걸치는 선을 따라 잘라,
        // 모두 같은 캔버스(303x474)에 두 발 한가운데·부츠 바닥을 맞춰 놓았다(기준점도 그 자리).
        // 머리 장식~부츠(416px)가 대기 모션의 서 있는 키(약 1.53유닛)와 같아 보이도록 캔버스 높이를 1.53 * 474 / 416 ≈ 1.74로 맞춘다.
        [SerializeField] private float targetHeight = 1.74f;

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

        public void Play(Vector2 facingDirection, float totalDuration)
        {
            if (!HasFrames) return;
            // 던지고 받는 모션이 걸음째 미끄러져 보이지 않게 재생하는 동안 제자리에 세운다.
            GetComponent<CharacterMovement2D>()?.RootFor(totalDuration);
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayRoutine(facingDirection, totalDuration));
        }

        private IEnumerator PlayRoutine(Vector2 facingDirection, float totalDuration)
        {
            if (walkAnimator != null) walkAnimator.enabled = false;
            if (jumpAnimator != null) jumpAnimator.enabled = false; // 공중에서도 점프 스프라이트가 덮어쓰지 않게
            spriteRenderer.flipX = facingDirection.x < 0f;

            float frameDuration = totalDuration / frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                spriteRenderer.sprite = frames[i];
                SkillFrameNormalizer.Apply(spriteRenderer.transform, frames[i], targetHeight);
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

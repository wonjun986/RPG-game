using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // Resources/Art/Necrosia/SSkill 아래의 사슬 갈고리(S) 프레임 12장(캐릭터 포함)을 재생한다.
    // 0~3: 사슬을 던져 뻗는 동작, 4~6: 뻗은 채로 버티는 동작(대기 중 반복 재생),
    // 7~11: 사슬을 다시 감아들이며 원래 자세로 돌아오는 동작.
    // 재생 중에는 WalkSpriteAnimator를 잠시 꺼서 스프라이트를 서로 덮어쓰지 않게 한다.
    public class ChainSkillSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string resourcesPath = "Art/Necrosia/SSkill";
        [SerializeField] private float holdFrameInterval = 0.08f;

        private SpriteRenderer spriteRenderer;
        private WalkSpriteAnimator walkAnimator;
        private Sprite[] frames;
        private Coroutine activeRoutine;

        private const int ExtendStart = 0;
        private const int ExtendEnd = 3;
        private const int HoldStart = 3;
        private const int HoldEnd = 6;
        private const int RetractStart = 7;
        private const int RetractEnd = 11;

        public bool HasFrames => frames != null && frames.Length >= 12;

        private void Awake()
        {
            walkAnimator = GetComponent<WalkSpriteAnimator>();

            var visual = transform.Find("Visual");
            spriteRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();

            frames = Resources.LoadAll<Sprite>(resourcesPath);
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
        }

        // 사슬을 던져 뻗는 동작을 재생하고 끝까지 기다린다 (호출부에서 yield return으로 대기).
        public IEnumerator PlayExtend(Vector2 facingDirection, float duration)
        {
            if (!HasFrames) yield break;

            if (walkAnimator != null) walkAnimator.enabled = false;
            spriteRenderer.flipX = facingDirection.x < 0f;

            yield return PlaySequence(ExtendStart, ExtendEnd, duration);
        }

        // 뻗은 자세를 유지하며 살짝 흔들리는 루프를 계속 재생한다 (StopHold로 멈출 때까지).
        public void StartHold(Vector2 facingDirection)
        {
            if (!HasFrames) return;
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            spriteRenderer.flipX = facingDirection.x < 0f;
            activeRoutine = StartCoroutine(HoldLoop());
        }

        public void StopHold()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }
        }

        // 사슬을 감아들이며 원래 자세로 돌아오는 동작을 재생하고, 끝나면 걷기/대기 애니메이터에게 제어권을 돌려준다.
        public IEnumerator PlayRetract(float duration)
        {
            if (!HasFrames)
            {
                if (walkAnimator != null) walkAnimator.enabled = true;
                yield break;
            }

            StopHold();
            yield return PlaySequence(RetractStart, RetractEnd, duration);

            SkillFrameNormalizer.Reset(spriteRenderer.transform);
            if (walkAnimator != null) walkAnimator.enabled = true;
        }

        private IEnumerator HoldLoop()
        {
            int index = HoldStart;
            int step = 1;
            while (true)
            {
                spriteRenderer.sprite = frames[index];
                SkillFrameNormalizer.Apply(spriteRenderer.transform, frames[index]);
                yield return new WaitForSeconds(holdFrameInterval);

                index += step;
                if (index >= HoldEnd || index <= HoldStart)
                {
                    step = -step;
                    index = Mathf.Clamp(index, HoldStart, HoldEnd);
                }
            }
        }

        private IEnumerator PlaySequence(int startIndex, int endIndex, float duration)
        {
            int count = endIndex - startIndex + 1;
            float frameDuration = duration / count;
            for (int i = startIndex; i <= endIndex; i++)
            {
                spriteRenderer.sprite = frames[i];
                SkillFrameNormalizer.Apply(spriteRenderer.transform, frames[i]);
                yield return new WaitForSeconds(frameDuration);
            }
        }
    }
}

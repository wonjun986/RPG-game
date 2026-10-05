using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // S = 사슬을 던져 뻗는 동작(SSkill 4프레임)을 재생하고, 다 뻗으면 마지막 프레임 자세로 버틴다.
    // Z = 당겨온 대상을 베어내는 마무리 동작(ZSkill 4프레임)을 한 번 재생한다.
    // 재생 중에는 WalkSpriteAnimator를 잠시 꺼서 스프라이트를 서로 덮어쓰지 않게 하고,
    // 끝나면 다시 켜서 제어권을 돌려준다.
    public class ChainSkillSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string throwResourcesPath = "Art/Necrosia/SSkill";
        [SerializeField] private string finisherResourcesPath = "Art/Necrosia/ZSkill";
        // ZSkill 프레임은 위로 휘두르는 낫까지 담느라 모두 724px 높이의 큰 캔버스인데, 그 안의 캐릭터는
        // SSkill보다 작게 그려져 있다(비슷한 런지 자세 기준 약 409px vs 502px). 프레임 높이를 기준 키(1.5)에
        // 맞추면 캐릭터가 절반 가까이 작아지므로, 캐릭터 크기가 SSkill과 같아지는 프레임 높이로 맞춘다.
        [SerializeField] private float finisherFrameHeight = 2.6f;

        private SpriteRenderer spriteRenderer;
        private WalkSpriteAnimator walkAnimator;
        private JumpSpriteAnimator jumpAnimator;
        private Sprite[] throwFrames;
        private Sprite[] finisherFrames;

        public bool HasFrames => throwFrames != null && throwFrames.Length > 0;

        private void Awake()
        {
            walkAnimator = GetComponent<WalkSpriteAnimator>();
            jumpAnimator = GetComponent<JumpSpriteAnimator>();

            var visual = transform.Find("Visual");
            spriteRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();

            throwFrames = LoadSortedFrames(throwResourcesPath);
            finisherFrames = LoadSortedFrames(finisherResourcesPath);
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

        // 사슬을 던져 뻗는 동작을 재생하고 끝까지 기다린다 (호출부에서 yield return으로 대기).
        public IEnumerator PlayExtend(Vector2 facingDirection, float duration)
        {
            if (!HasFrames) yield break;

            if (walkAnimator != null) walkAnimator.enabled = false;
            // 기본 공격 등 다른 동작이 점프 애니메이터를 꺼둔 채로 이 스킬에 넘겨줄 수 있어서,
            // 끝날 때 함께 다시 켜려면 여기서도 같이 끈다.
            if (jumpAnimator != null) jumpAnimator.enabled = false;
            spriteRenderer.flipX = facingDirection.x < 0f;

            yield return PlaySequence(throwFrames, 0, throwFrames.Length - 1, duration);
        }

        // 뻗은 자세(마지막 프레임)를 그대로 유지한다.
        public void StartHold(Vector2 facingDirection)
        {
            if (!HasFrames) return;
            spriteRenderer.flipX = facingDirection.x < 0f;
            Sprite lastFrame = throwFrames[throwFrames.Length - 1];
            spriteRenderer.sprite = lastFrame;
            SkillFrameNormalizer.Apply(spriteRenderer.transform, lastFrame);
        }

        // Z: 당겨온 대상을 베어내는 마무리 동작을 한 번 재생하고, 끝나면 마지막 프레임에서 멈춘다.
        public IEnumerator PlayFinisher(float duration)
        {
            if (finisherFrames == null || finisherFrames.Length == 0) yield break;
            yield return PlaySequence(finisherFrames, 0, finisherFrames.Length - 1, duration, finisherFrameHeight);
        }

        // 사슬을 다시 감아들이며 원래 자세로 돌아오는 동작(뻗는 동작의 역재생)을 재생하고,
        // 끝나면 걷기/대기 애니메이터에게 제어권을 돌려준다.
        public IEnumerator PlayRetract(float duration)
        {
            if (!HasFrames)
            {
                if (walkAnimator != null) walkAnimator.enabled = true;
                if (jumpAnimator != null) jumpAnimator.enabled = true;
                yield break;
            }

            yield return PlaySequence(throwFrames, throwFrames.Length - 1, 0, duration);

            SkillFrameNormalizer.Reset(spriteRenderer.transform);
            if (walkAnimator != null) walkAnimator.enabled = true;
            if (jumpAnimator != null) jumpAnimator.enabled = true;
        }

        private IEnumerator PlaySequence(Sprite[] frames, int startIndex, int endIndex, float duration,
            float targetHeight = SkillFrameNormalizer.ReferenceCharacterHeight)
        {
            int step = endIndex >= startIndex ? 1 : -1;
            int count = Mathf.Abs(endIndex - startIndex) + 1;
            float frameDuration = duration / count;

            int index = startIndex;
            for (int i = 0; i < count; i++)
            {
                spriteRenderer.sprite = frames[index];
                SkillFrameNormalizer.Apply(spriteRenderer.transform, frames[index], targetHeight);
                yield return new WaitForSeconds(frameDuration);
                index += step;
            }
        }
    }
}

using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // Resources/Art/Necrosia/WSkill 아래의 악마의 손 시전(W) 프레임을 불러온다.
    // 앞부분(포탈을 열고 손이 뻗어나옴)을 재생한 뒤, Release()가 호출될 때까지 손을 뻗은 프레임 구간을
    // 반복하고, 이후 남은 프레임(손을 거둬들임)을 재생하고 끝난다.
    // 재생 중에는 WalkSpriteAnimator(걷기/대기 스프라이트)를 잠시 꺼서 서로 스프라이트를
    // 덮어쓰지 않게 하고, 끝나면 다시 켜서 제어권을 돌려준다.
    public class WSkillSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string resourcesPath = "Art/Necrosia/WSkill";
        // 프레임은 모두 부츠 위치를 기준으로 맞춘 같은 캔버스(383px)로 잘려 있고, 그중 캐릭터 키(머리~발끝)는
        // 약 349px이다. 캐릭터가 기준 키(1.5)로 보이도록 캔버스 전체 높이를 1.5 * 383 / 349 ≈ 1.65로 맞춘다.
        [SerializeField] private float targetHeight = 1.65f;
        [SerializeField] private float frameDuration = 0.08f;
        // 손이 가장 크게 뻗어 있는 6~8번째 프레임을 지속 시간 동안 반복한다.
        [SerializeField] private int loopStart = 5;
        [SerializeField] private int loopEnd = 7;

        private SpriteRenderer spriteRenderer;
        private WalkSpriteAnimator walkAnimator;
        private JumpSpriteAnimator jumpAnimator;
        private Sprite[] frames;
        private Coroutine playRoutine;
        private bool released;

        public bool HasFrames => frames != null && frames.Length > 0;
        public float FrameDuration => frameDuration;
        // 반복 구간 이후(손을 거둬들이는) 프레임들의 재생 시간.
        public float OutroDuration => HasFrames ? Mathf.Max(0, frames.Length - 1 - loopEnd) * frameDuration : 0f;

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
                loopStart = Mathf.Clamp(loopStart, 0, frames.Length - 1);
                loopEnd = Mathf.Clamp(loopEnd, loopStart, frames.Length - 1);
            }
        }

        public void Play(Vector2 facingDirection)
        {
            if (!HasFrames) return;
            if (playRoutine != null) StopCoroutine(playRoutine);
            released = false;
            playRoutine = StartCoroutine(PlayRoutine(facingDirection));
        }

        public void Release() => released = true;

        private IEnumerator PlayRoutine(Vector2 facingDirection)
        {
            if (walkAnimator != null) walkAnimator.enabled = false;
            if (jumpAnimator != null) jumpAnimator.enabled = false;
            spriteRenderer.flipX = facingDirection.x < 0f;

            for (int i = 0; i < loopStart; i++)
            {
                yield return ShowFrame(i);
                if (WasPreempted(i)) yield break;
            }

            int loopIndex = loopStart;
            while (!released)
            {
                yield return ShowFrame(loopIndex);
                if (WasPreempted(loopIndex)) yield break;
                loopIndex = loopIndex >= loopEnd ? loopStart : loopIndex + 1;
            }

            for (int i = loopEnd + 1; i < frames.Length; i++)
            {
                yield return ShowFrame(i);
                if (WasPreempted(i)) yield break;
            }

            SkillFrameNormalizer.Reset(spriteRenderer.transform);
            if (walkAnimator != null) walkAnimator.enabled = true;
            if (jumpAnimator != null) jumpAnimator.enabled = true;
            playRoutine = null;
        }

        // 그새 다른 동작(피격 등)이 스프라이트를 가져갔으면 조용히 물러난다. 걷기/점프 애니메이터 복구와
        // 스케일 초기화는 마지막에 가져간 쪽이 끝날 때 한다.
        private bool WasPreempted(int shownIndex)
        {
            if (spriteRenderer.sprite == frames[shownIndex]) return false;
            playRoutine = null;
            return true;
        }

        private WaitForSeconds ShowFrame(int index)
        {
            spriteRenderer.sprite = frames[index];
            SkillFrameNormalizer.Apply(spriteRenderer.transform, frames[index], targetHeight);
            return new WaitForSeconds(frameDuration);
        }
    }
}

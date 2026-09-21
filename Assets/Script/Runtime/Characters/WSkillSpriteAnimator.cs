using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // Resources/Art/Necrosia/WSkill 아래의 사슬 폭풍(W) 프레임을 불러온다.
    // 재생 중에는 WalkSpriteAnimator(걷기/대기 스프라이트)를 잠시 꺼서 서로 스프라이트를
    // 덮어쓰지 않게 하고, 끝나면 다시 켜서 제어권을 돌려준다.
    public class WSkillSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string resourcesPath = "Art/Necrosia/WSkill";
        [SerializeField] private float totalDuration = 0.7f;

        private SpriteRenderer spriteRenderer;
        private WalkSpriteAnimator walkAnimator;
        private Sprite[] frames;
        private Coroutine playRoutine;

        public bool HasFrames => frames != null && frames.Length > 0;

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

        // 한 번만 재생하고 끝나면 걷기 애니메이터에게 제어권을 돌려준다.
        public void Play(Vector2 facingDirection)
        {
            if (!HasFrames) return;
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayOnceRoutine(facingDirection));
        }

        // W를 누르고 있는 동안 매 사이클마다 처음부터 다시 재생하면 프레임이 뚝뚝 끊겨
        // 부자연스러워 보인다. 대신 Stop()이 호출될 때까지 프레임을 계속 이어서 반복 재생한다.
        public void PlayLooping(Vector2 facingDirection)
        {
            if (!HasFrames) return;
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayLoopingRoutine(facingDirection));
        }

        public void Stop()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }
            SkillFrameNormalizer.Reset(spriteRenderer.transform);
            if (walkAnimator != null) walkAnimator.enabled = true;
        }

        private IEnumerator PlayOnceRoutine(Vector2 facingDirection)
        {
            if (walkAnimator != null) walkAnimator.enabled = false;
            spriteRenderer.flipX = facingDirection.x < 0f;

            float frameDuration = totalDuration / frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                spriteRenderer.sprite = frames[i];
                SkillFrameNormalizer.Apply(spriteRenderer.transform, frames[i]);
                yield return new WaitForSeconds(frameDuration);
            }

            SkillFrameNormalizer.Reset(spriteRenderer.transform);
            if (walkAnimator != null) walkAnimator.enabled = true;
            playRoutine = null;
        }

        private IEnumerator PlayLoopingRoutine(Vector2 facingDirection)
        {
            if (walkAnimator != null) walkAnimator.enabled = false;
            spriteRenderer.flipX = facingDirection.x < 0f;

            float frameDuration = totalDuration / frames.Length;
            int i = 0;
            while (true)
            {
                spriteRenderer.sprite = frames[i];
                SkillFrameNormalizer.Apply(spriteRenderer.transform, frames[i]);
                i = (i + 1) % frames.Length;
                yield return new WaitForSeconds(frameDuration);
            }
        }
    }
}

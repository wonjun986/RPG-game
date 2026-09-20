using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // Resources/Art/Necrosia/ASkill 아래의 낫 회전 베기(A) 프레임을 불러와, 요청 시 한 번만 재생한다.
    // 재생 중에는 WalkSpriteAnimator(걷기/대기 스프라이트)를 잠시 꺼서 서로 스프라이트를
    // 덮어쓰지 않게 하고, 끝나면 다시 켜서 제어권을 돌려준다.
    public class ASkillSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string resourcesPath = "Art/Necrosia/ASkill";
        [SerializeField] private float totalDuration = 0.5f;

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

        public void Play(Vector2 facingDirection)
        {
            if (!HasFrames) return;
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayRoutine(facingDirection));
        }

        private IEnumerator PlayRoutine(Vector2 facingDirection)
        {
            if (walkAnimator != null) walkAnimator.enabled = false;
            spriteRenderer.flipX = facingDirection.x < 0f;

            float frameDuration = totalDuration / frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                spriteRenderer.sprite = frames[i];
                yield return new WaitForSeconds(frameDuration);
            }

            if (walkAnimator != null) walkAnimator.enabled = true;
            playRoutine = null;
        }
    }
}

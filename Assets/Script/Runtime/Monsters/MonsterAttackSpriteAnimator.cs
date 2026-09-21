using System.Collections;
using UnityEngine;

namespace Aethoria.Monsters
{
    // Resources/Art/Monsters/BossKnight/Attack 아래의 공격 프레임을, 패턴이 시전되는 동안 한 번 재생한다.
    // 재생 중에는 MonsterWalkSpriteAnimator를 꺼서 스프라이트를 서로 덮어쓰지 않게 하고,
    // 끝나거나 중간에 경직 등으로 끊기면 다시 켜서 제어권을 돌려준다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class MonsterAttackSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string resourcesPath = "Art/Monsters/BossKnight/Attack";
        [SerializeField] private float defaultDuration = 1.2f;

        private SpriteRenderer spriteRenderer;
        private MonsterWalkSpriteAnimator walkAnimator;
        private Sprite[] frames;
        private Coroutine playRoutine;

        public bool HasFrames => frames != null && frames.Length > 0;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            walkAnimator = GetComponent<MonsterWalkSpriteAnimator>();

            frames = Resources.LoadAll<Sprite>(resourcesPath);
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
        }

        public void Play(float duration = -1f)
        {
            if (!HasFrames) return;
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayRoutine(duration > 0f ? duration : defaultDuration));
        }

        // 경직 등으로 패턴이 중간에 끊겼을 때 호출해서, 즉시 걷기 애니메이터에 제어권을 돌려준다.
        public void Stop()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }
            if (walkAnimator != null) walkAnimator.enabled = true;
        }

        private IEnumerator PlayRoutine(float duration)
        {
            if (walkAnimator != null) walkAnimator.enabled = false;

            float frameDuration = duration / frames.Length;
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

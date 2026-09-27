using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aethoria.Monsters
{
    // Resources/Art/Monsters/BossKnight/Attack 아래의 공격 프레임을, 패턴이 시전되는 동안 한 번 재생한다.
    // 재생 중에는 MonsterWalkSpriteAnimator를 꺼서 스프라이트를 서로 덮어쓰지 않게 하고,
    // 끝나거나 중간에 경직 등으로 끊기면 다시 켜서 제어권을 돌려준다.
    // 보스의 점프 내려찍기처럼 패턴마다 모션이 다르면 AddClip으로 이름 붙인 프레임 묶음을 더 등록해서 쓴다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class MonsterAttackSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string resourcesPath = "Art/Monsters/BossKnight/Attack";
        [SerializeField] private float defaultDuration = 1.2f;

        private SpriteRenderer spriteRenderer;
        private MonsterWalkSpriteAnimator walkAnimator;
        private Sprite[] frames;
        private readonly Dictionary<string, Sprite[]> clips = new Dictionary<string, Sprite[]>();
        private Coroutine playRoutine;

        public bool HasFrames => frames != null && frames.Length > 0;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            walkAnimator = GetComponent<MonsterWalkSpriteAnimator>();
            LoadFrames();
        }

        // 일반 기사처럼 보스와 프레임 경로가 다를 때, AddComponent 직후 호출해서 덮어쓴다.
        public void Configure(string newResourcesPath, float newDefaultDuration = -1f)
        {
            resourcesPath = newResourcesPath;
            if (newDefaultDuration > 0f) defaultDuration = newDefaultDuration;
            LoadFrames();
        }

        private void LoadFrames()
        {
            frames = LoadSorted(resourcesPath);
        }

        private static Sprite[] LoadSorted(string path)
        {
            var loaded = Resources.LoadAll<Sprite>(path);
            if (loaded != null && loaded.Length > 0)
            {
                System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            return loaded;
        }

        public void AddClip(string clipName, string clipResourcesPath)
        {
            clips[clipName] = LoadSorted(clipResourcesPath);
        }

        public bool HasClip(string clipName)
        {
            return clips.TryGetValue(clipName, out var clip) && clip != null && clip.Length > 0;
        }

        public void Play(float duration = -1f)
        {
            if (!HasFrames) return;
            StartPlaying(frames, duration > 0f ? duration : defaultDuration);
        }

        public void PlayClip(string clipName, float duration)
        {
            if (!HasClip(clipName)) return;
            StartPlaying(clips[clipName], duration);
        }

        private void StartPlaying(Sprite[] clip, float duration)
        {
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayRoutine(clip, duration));
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

        private IEnumerator PlayRoutine(Sprite[] clip, float duration)
        {
            if (walkAnimator != null) walkAnimator.enabled = false;

            float frameDuration = duration / clip.Length;
            for (int i = 0; i < clip.Length; i++)
            {
                spriteRenderer.sprite = clip[i];
                yield return new WaitForSeconds(frameDuration);
            }

            if (walkAnimator != null) walkAnimator.enabled = true;
            playRoutine = null;
        }
    }
}

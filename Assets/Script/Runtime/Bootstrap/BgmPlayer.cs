using UnityEngine;

namespace Aethoria.Bootstrap
{
    // 배경 음악. GameBootstrap이 맵을 지을 때마다 그 맵의 곡을 고른다(곡이 없는 맵이면 끈다).
    // 맵에 들어오면 곡을 처음부터 틀고, 곡이 끝나면 처음이 아니라 loopStart초부터 다시 튼다
    // (도입부는 들어왔을 때 한 번만 듣고, 싸움이 길어지면 본 곡만 되풀이된다). 볼륨은 AudioListener(전체 볼륨)를 따른다.
    public class BgmPlayer : MonoBehaviour
    {
        private static BgmPlayer instance;

        private AudioSource source;
        private float loopStart;
        private float lastTime;

        // restartIfPlaying=false면 같은 곡이 이미 나오고 있을 때 처음으로 되감지 않고 그대로 둔다.
        public static void Play(string resourcePath, float loopStartSeconds, bool restartIfPlaying = true)
        {
            var clip = Resources.Load<AudioClip>(resourcePath);
            if (!restartIfPlaying && instance != null && instance.source.clip == clip && clip != null) return;
            if (clip == null)
            {
                Debug.LogWarning($"Aethoria: 배경 음악을 찾을 수 없음 ({resourcePath})");
                Stop();
                return;
            }
            var bgm = GetInstance();
            bgm.source.Stop();
            bgm.source.clip = clip;
            bgm.loopStart = Mathf.Clamp(loopStartSeconds, 0f, clip.length - 1f);
            bgm.source.time = 0f;
            bgm.lastTime = 0f;
            bgm.source.Play();
        }

        public static void Stop()
        {
            if (instance == null) return;
            instance.source.Stop();
            instance.source.clip = null;
        }

        private static BgmPlayer GetInstance()
        {
            if (instance != null) return instance;
            var go = new GameObject("BgmPlayer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<BgmPlayer>();
            instance.source = go.AddComponent<AudioSource>();
            instance.source.playOnAwake = false;
            instance.source.loop = false;
            instance.source.spatialBlend = 0f;
            return instance;
        }

        private void Update()
        {
            var clip = source.clip;
            if (clip == null) return;
            if (source.isPlaying)
            {
                lastTime = source.time;
                return;
            }
            // 곡 끝까지 다 들었을 때만 이어 튼다(창이 포커스를 잃어 잠깐 멈춘 것과 구분한다).
            if (lastTime >= clip.length - 0.5f)
            {
                source.time = loopStart;
                lastTime = loopStart;
                source.Play();
            }
        }
    }
}

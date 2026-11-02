using System.Collections.Generic;
using UnityEngine;

namespace Aethoria.Bootstrap
{
    // 효과음(Resources/Audio/Sfx). 한 AudioSource로 PlayOneShot해서 여러 소리가 겹쳐도 서로 끊지 않는다.
    // 위치와 상관없이 같은 크기로 들린다(2D). 볼륨은 BgmPlayer처럼 AudioListener(전체 볼륨)를 따른다.
    public class SfxPlayer : MonoBehaviour
    {
        public const string NecrosiaZAttack = "Audio/Sfx/necrosia_z_attack";
        public const string NecrosiaDeath = "Audio/Sfx/necrosia_death";
        public const string AliasAttack = "Audio/Sfx/alias_attack";
        public const string AliasFinaleStart = "Audio/Sfx/alias_finale_start";
        public const string SandDragonRoar = "Audio/Sfx/sand_dragon_roar";

        private static SfxPlayer instance;
        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        private AudioSource source;

        public static void Play(string resourcePath, float volume = 1f)
        {
            if (!cache.TryGetValue(resourcePath, out var clip))
            {
                clip = Resources.Load<AudioClip>(resourcePath);
                if (clip == null) Debug.LogWarning($"Aethoria: 효과음을 찾을 수 없음 ({resourcePath})");
                cache[resourcePath] = clip;
            }
            if (clip == null) return;
            GetInstance().source.PlayOneShot(clip, volume);
        }

        private static SfxPlayer GetInstance()
        {
            if (instance != null) return instance;
            var go = new GameObject("SfxPlayer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SfxPlayer>();
            instance.source = go.AddComponent<AudioSource>();
            instance.source.playOnAwake = false;
            instance.source.spatialBlend = 0f;
            return instance;
        }
    }
}

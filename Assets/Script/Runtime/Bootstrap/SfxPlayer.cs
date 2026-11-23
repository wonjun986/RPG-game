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

        // 아직 파일이 없는 효과음들. Assets/Resources/Audio/Sfx/에 같은 이름(.wav/.ogg)으로 넣으면 바로 나온다.
        // 파일이 없는 동안은 경로마다 경고를 한 번만 남기고 조용히 넘어간다.
        public const string NecrosiaJump = "Audio/Sfx/necrosia_jump";
        public const string NecrosiaDash = "Audio/Sfx/necrosia_dash";
        public const string NecrosiaHurt = "Audio/Sfx/necrosia_hurt";
        public const string SkillA = "Audio/Sfx/skill_a";   // 낫 회전 베기
        public const string SkillS = "Audio/Sfx/skill_s";   // 어둠의 주먹
        public const string SkillD = "Audio/Sfx/skill_d";   // 낫 부메랑
        public const string SkillW = "Audio/Sfx/skill_w";   // 악마의 손
        public const string SkillE = "Audio/Sfx/skill_e";   // 사슬 폭풍
        public const string SkillR = "Audio/Sfx/skill_r";   // 죽음의 소용돌이
        public const string MonsterHit = "Audio/Sfx/monster_hit";
        public const string LevelUp = "Audio/Sfx/level_up";
        public const string PotionDrink = "Audio/Sfx/potion_drink";
        public const string ItemPickup = "Audio/Sfx/item_pickup";
        public const string QuestComplete = "Audio/Sfx/quest_complete";
        public const string PortalEnter = "Audio/Sfx/portal_enter";
        public const string RhythmHit = "Audio/Sfx/rhythm_hit";
        public const string RhythmMiss = "Audio/Sfx/rhythm_miss";

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
            GetInstance().source.PlayOneShot(clip, volume * Aethoria.Save.GameSettings.SfxVolume);
        }

        // 파일이 있으면 틀고 true, 없으면 경고 없이 false(코드 합성 소리로 대신할 때 쓴다).
        public static bool TryPlay(string resourcePath, float volume = 1f)
        {
            if (!cache.TryGetValue(resourcePath, out var clip))
            {
                clip = Resources.Load<AudioClip>(resourcePath);
                cache[resourcePath] = clip;
            }
            if (clip == null) return false;
            GetInstance().source.PlayOneShot(clip, volume * Aethoria.Save.GameSettings.SfxVolume);
            return true;
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

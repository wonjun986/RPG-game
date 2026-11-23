using System.Collections.Generic;
using UnityEngine;

namespace Aethoria.Fx
{
    public enum Surface { Grass, Stone, Sand, Wet, Wood }

    public enum Ambience { None, Forest, Wind, Desert, Sea, Cave, Heaven, Crimson }

    public enum HitSound { Normal, Critical, Kill }

    // 소리 파일 없이 코드로 합성한 발소리와 맵 배경 앰비언스. 전체 볼륨(AudioListener.volume)을 그대로 따른다.
    public class ProceduralAudio : MonoBehaviour
    {
        private const int Rate = 22050;
        private const float FootstepVolume = 0.32f;
        private const float AmbienceVolume = 0.22f;

        private static ProceduralAudio instance;
        private readonly Dictionary<Surface, AudioClip[]> footsteps = new Dictionary<Surface, AudioClip[]>();
        private readonly Dictionary<Ambience, AudioClip> ambienceClips = new Dictionary<Ambience, AudioClip>();
        private const float HitVolume = 0.55f;
        // 광역기로 여러 마리를 한꺼번에 때려도 소리가 겹겹이 쌓여 터지지 않게, 이 간격 안의 일반 타격음은 건너뛴다.
        private const float HitSoundGap = 0.035f;

        private readonly Dictionary<HitSound, AudioClip[]> hitClips = new Dictionary<HitSound, AudioClip[]>();
        private AudioSource hitShot; // 발소리와 음 높이가 섞이지 않게 따로 둔다
        private float lastHitSoundTime = -10f;
        private AudioSource oneShot;
        private AudioSource ambienceA, ambienceB;
        private bool aIsActive = true;
        private Ambience currentAmbience = Ambience.None;
        private float fade = 1f;

        private static ProceduralAudio Instance
        {
            get
            {
                if (instance != null) return instance;
                // 씬과 함께 사라지게 둔다(타이틀로 돌아가면 맵 환경음이 남지 않는다).
                var go = new GameObject("ProceduralAudio");
                instance = go.AddComponent<ProceduralAudio>();
                return instance;
            }
        }

        private void Awake()
        {
            oneShot = gameObject.AddComponent<AudioSource>();
            oneShot.playOnAwake = false;
            hitShot = gameObject.AddComponent<AudioSource>();
            hitShot.playOnAwake = false;
            ambienceA = MakeLoopSource();
            ambienceB = MakeLoopSource();
        }

        private AudioSource MakeLoopSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.volume = 0f;
            return s;
        }

        public static void PlayFootstep(Surface surface)
        {
            var self = Instance;
            if (!self.footsteps.TryGetValue(surface, out var clips))
            {
                clips = new AudioClip[3];
                for (int i = 0; i < clips.Length; i++) clips[i] = MakeFootstep(surface, i * 7919 + (int)surface * 31);
                self.footsteps[surface] = clips;
            }
            self.oneShot.pitch = Random.Range(0.9f, 1.1f);
            self.oneShot.PlayOneShot(clips[Random.Range(0, clips.Length)], FootstepVolume * Aethoria.Save.GameSettings.SfxVolume);
        }

        // 몬스터 타격음. 종류마다 3가지 변형을 만들어 두고 골라 쓰며, 칠 때마다 음 높이를 살짝 바꿔 기계적으로 들리지 않게 한다.
        public static void PlayHit(HitSound kind)
        {
            var self = Instance;
            float now = Time.unscaledTime;
            if (kind == HitSound.Normal && now - self.lastHitSoundTime < HitSoundGap) return;
            self.lastHitSoundTime = now;

            if (!self.hitClips.TryGetValue(kind, out var clips))
            {
                clips = new AudioClip[3];
                for (int i = 0; i < clips.Length; i++) clips[i] = MakeHit(kind, i * 4271 + (int)kind * 97 + 13);
                self.hitClips[kind] = clips;
            }
            self.hitShot.pitch = Random.Range(0.92f, 1.08f);
            float volume = kind == HitSound.Kill ? HitVolume * 1.2f : HitVolume;
            self.hitShot.PlayOneShot(clips[Random.Range(0, clips.Length)], volume * Aethoria.Save.GameSettings.SfxVolume);
        }

        public static void SetAmbience(Ambience ambience)
        {
            var self = Instance;
            if (self.currentAmbience == ambience) return;
            self.currentAmbience = ambience;
            var next = self.aIsActive ? self.ambienceB : self.ambienceA;
            self.aIsActive = !self.aIsActive;
            if (ambience == Ambience.None)
            {
                next.Stop();
                next.clip = null;
            }
            else
            {
                if (!self.ambienceClips.TryGetValue(ambience, out var clip))
                {
                    clip = MakeAmbience(ambience);
                    self.ambienceClips[ambience] = clip;
                }
                next.clip = clip;
                next.time = Random.Range(0f, clip.length);
                next.Play();
            }
            self.fade = 0f;
        }

        private void Update()
        {
            var active = aIsActive ? ambienceA : ambienceB;
            var old = aIsActive ? ambienceB : ambienceA;
            // 맵 앰비언스는 배경음 볼륨을 따른다(설정을 바꾸면 바로 반영되도록 다 바뀐 뒤에도 매 프레임 맞춘다).
            float ambience = AmbienceVolume * Aethoria.Save.GameSettings.BgmVolume;
            if (fade >= 1f) { active.volume = ambience; return; }
            fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / 1.5f);
            active.volume = fade * ambience;
            old.volume = (1f - fade) * old.volume;
            if (fade >= 1f && old.isPlaying) { old.Stop(); old.volume = 0f; }
        }

        // ---- 합성 ----

        private static AudioClip MakeFootstep(Surface surface, int seed)
        {
            var rng = new System.Random(seed);
            float length = surface == Surface.Wet ? 0.22f : surface == Surface.Sand ? 0.16f : 0.11f;
            int n = (int)(Rate * length);
            var data = new float[n];
            float lp = 0f, lp2 = 0f;
            // 재질마다 잡음을 얼마나 거르는지(작을수록 먹먹)와 저음 쿵 소리 세기가 다르다.
            float cutoff = surface switch { Surface.Grass => 0.18f, Surface.Stone => 0.55f, Surface.Sand => 0.3f, Surface.Wet => 0.35f, _ => 0.4f };
            float thump = surface switch { Surface.Stone => 0.55f, Surface.Wood => 0.7f, Surface.Grass => 0.25f, _ => 0.15f };
            float thumpHz = surface == Surface.Wood ? 160f : 95f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * (surface == Surface.Wet ? 18f : 38f)) * Mathf.Clamp01(t * 600f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * cutoff;
                lp2 += (lp - lp2) * cutoff;
                float body = Mathf.Sin(2f * Mathf.PI * thumpHz * t) * Mathf.Exp(-t * 60f) * thump;
                float sample = (lp2 * 1.6f + body) * env;
                if (surface == Surface.Wet) sample += Mathf.Sin(2f * Mathf.PI * (900f - t * 2500f) * t) * Mathf.Exp(-t * 25f) * 0.15f;
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }
            var clip = AudioClip.Create("Footstep_" + surface, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 타격음: 짧게 걸러 낸 잡음(베이는 소리) + 음이 떨어지는 저음(퍽 하는 몸통 소리).
        //  - Critical: 더 밝은 잡음에 쇳소리 울림을 얹는다.
        //  - Kill: 더 낮고 길게 울리는 쿵 소리.
        private static AudioClip MakeHit(HitSound kind, int seed)
        {
            var rng = new System.Random(seed);
            float jitter = (float)rng.NextDouble();
            float length = kind == HitSound.Kill ? 0.32f : kind == HitSound.Critical ? 0.2f : 0.13f;
            float cutoff = kind == HitSound.Critical ? 0.7f : kind == HitSound.Kill ? 0.3f : 0.5f;
            float noiseDecay = kind == HitSound.Kill ? 20f : 45f;
            float bodyStart = (kind == HitSound.Kill ? 120f : 185f) + jitter * 25f;
            float bodyFall = kind == HitSound.Kill ? 220f : 650f;
            float bodyDecay = kind == HitSound.Kill ? 13f : 32f;
            float bodyGain = kind == HitSound.Kill ? 1f : 0.8f;

            int n = (int)(Rate * length);
            var data = new float[n];
            float lp = 0f, lp2 = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float attack = Mathf.Clamp01(t * 900f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * cutoff;
                lp2 += (lp - lp2) * cutoff;
                float slash = lp2 * Mathf.Exp(-t * noiseDecay) * 1.3f;

                float freq = Mathf.Max(45f, bodyStart - bodyFall * t);
                phase += 2f * Mathf.PI * freq / Rate;
                float body = Mathf.Sin(phase) * Mathf.Exp(-t * bodyDecay) * bodyGain;

                float sample = (slash + body) * attack;
                if (kind == HitSound.Critical)
                {
                    float ring = Mathf.Sin(2f * Mathf.PI * (1350f + jitter * 150f) * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 2080f * t);
                    sample += ring * Mathf.Exp(-t * 24f) * 0.22f * attack;
                }
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }
            var clip = AudioClip.Create("Hit_" + kind, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeAmbience(Ambience ambience)
        {
            const float length = 12f;
            int n = (int)(Rate * length);
            var data = new float[n];
            var rng = new System.Random((int)ambience * 1013 + 7);
            float brown = 0f, lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float phase = t / length * Mathf.PI * 2f; // 루프 끝과 처음이 이어지도록 주기를 길이에 맞춘다
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                brown = Mathf.Clamp(brown + white * 0.02f, -1f, 1f) * 0.998f;
                lp += (white - lp) * 0.08f;
                float s = 0f;
                switch (ambience)
                {
                    case Ambience.Wind:
                    case Ambience.Desert:
                    case Ambience.Heaven:
                    {
                        float gust = 0.55f + 0.45f * Mathf.Sin(phase * 2f) * Mathf.Sin(phase * 3f + 1f);
                        s = brown * 1.6f * gust;
                        if (ambience == Ambience.Desert) s += lp * 0.25f * gust;  // 모래가 쓸리는 쉭 소리
                        if (ambience == Ambience.Heaven) s *= 0.6f;
                        break;
                    }
                    case Ambience.Crimson:
                        s = brown * 1.2f * (0.6f + 0.4f * Mathf.Sin(phase * 2f));
                        s += Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.05f * (0.5f + 0.5f * Mathf.Sin(phase));
                        break;
                    case Ambience.Sea:
                    {
                        // 6초마다(12초에 2번) 밀려왔다 빠지는 파도.
                        float wave = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(phase * 2f - Mathf.PI / 2f), 2f);
                        s = (lp * 0.9f + brown * 0.8f) * (0.25f + 0.75f * wave);
                        break;
                    }
                    case Ambience.Forest:
                        s = brown * 0.7f * (0.7f + 0.3f * Mathf.Sin(phase * 3f));
                        break;
                    case Ambience.Cave:
                        s = brown * 0.5f;
                        break;
                }
                data[i] = s;
            }

            // 새소리(숲)·물방울(동굴/바다)·반짝임(천상)을 군데군데 얹는다.
            if (ambience == Ambience.Forest) AddEvents(data, rng, 7, AddChirp);
            if (ambience == Ambience.Cave || ambience == Ambience.Sea) AddEvents(data, rng, ambience == Ambience.Cave ? 9 : 4, AddDrip);
            if (ambience == Ambience.Heaven) AddEvents(data, rng, 6, AddChime);

            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create("Ambience_" + ambience, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void AddEvents(float[] data, System.Random rng, int count, System.Action<float[], int, System.Random> add)
        {
            int margin = Rate; // 루프 이음매에서 끊기지 않게 끝 1초는 비운다
            for (int k = 0; k < count; k++) add(data, rng.Next(0, data.Length - margin), rng);
        }

        private static void AddChirp(float[] data, int start, System.Random rng)
        {
            int notes = 2 + rng.Next(3);
            float baseHz = 2600f + (float)rng.NextDouble() * 1400f;
            int offset = start;
            for (int k = 0; k < notes; k++)
            {
                int len = (int)(Rate * 0.07f);
                for (int i = 0; i < len && offset + i < data.Length; i++)
                {
                    float t = (float)i / Rate;
                    float hz = baseHz + 1800f * (t / 0.07f);
                    float env = Mathf.Sin(Mathf.PI * i / len);
                    data[offset + i] += Mathf.Sin(2f * Mathf.PI * hz * t) * env * 0.12f;
                }
                offset += (int)(Rate * 0.1f);
            }
        }

        private static void AddDrip(float[] data, int start, System.Random rng)
        {
            float hz = 1200f + (float)rng.NextDouble() * 900f;
            int len = (int)(Rate * 0.18f);
            for (int i = 0; i < len && start + i < data.Length; i++)
            {
                float t = (float)i / Rate;
                data[start + i] += Mathf.Sin(2f * Mathf.PI * (hz + 1500f * t) * t) * Mathf.Exp(-t * 30f) * 0.25f;
            }
        }

        private static void AddChime(float[] data, int start, System.Random rng)
        {
            float[] scale = { 1046.5f, 1174.7f, 1318.5f, 1568f, 1760f };
            float hz = scale[rng.Next(scale.Length)];
            int len = (int)(Rate * 1.2f);
            for (int i = 0; i < len && start + i < data.Length; i++)
            {
                float t = (float)i / Rate;
                data[start + i] += (Mathf.Sin(2f * Mathf.PI * hz * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * hz * t)) * Mathf.Exp(-t * 4f) * 0.06f;
            }
        }
    }
}

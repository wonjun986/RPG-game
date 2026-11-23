using UnityEngine;

namespace Aethoria.UI
{
    // 메뉴 효과음(소리 파일 없이 합성): 커서 이동 · 확인 · 취소 · 창 열기 · 창 닫기 · 안 됨.
    // Audio/Sfx/ui_move 같은 파일을 넣으면 그 파일을 먼저 쓴다. 효과음 볼륨(GameSettings.SfxVolume)을 따른다.
    // 한 프레임에 여러 곳에서 불려도(예: 창을 열면서 첫 칸을 고름) 가장 중요한 소리 하나만 난다.
    public static class UiSfx
    {
        public enum Kind { Move = 0, Confirm = 1, Cancel = 2, Open = 3, Close = 4, Error = 5, Anvil = 6, EnhanceSuccess = 7, EnhanceFail = 8 }

        private static readonly string[] FileNames = { "ui_move", "ui_confirm", "ui_cancel", "ui_open", "ui_close", "ui_error", "enhance_hammer", "enhance_success", "enhance_fail" };
        // 같은 프레임에 겹치면 숫자가 큰 쪽이 이긴다.
        private static readonly int[] Priority = { 0, 3, 3, 2, 2, 4, 5, 6, 6 };
        private static readonly float[] Volume = { 0.35f, 0.55f, 0.5f, 0.5f, 0.45f, 0.5f, 0.7f, 0.7f, 0.6f };

        private static AudioClip[] clips;
        private static AudioSource source;
        private static int lastFrame = -1;
        private static int lastPriority = -1;
        private static float lastMoveTime;

        public static void Move() => Play(Kind.Move);
        public static void Confirm() => Play(Kind.Confirm);
        public static void Cancel() => Play(Kind.Cancel);
        public static void Open() => Play(Kind.Open);
        public static void Close() => Play(Kind.Close);
        public static void Error() => Play(Kind.Error);
        // 강화창: 망치질 · 강화 성공 · 강화 실패.
        public static void Anvil() => Play(Kind.Anvil);
        public static void EnhanceSuccess() => Play(Kind.EnhanceSuccess);
        public static void EnhanceFail() => Play(Kind.EnhanceFail);

        public static void Play(Kind kind)
        {
            int k = (int)kind;
            if (Time.frameCount == lastFrame && Priority[k] <= lastPriority) return;
            // 키를 꾹 눌러 커서가 연달아 움직일 때 소리가 뭉개지지 않게.
            if (kind == Kind.Move && Time.unscaledTime - lastMoveTime < 0.045f) return;
            if (kind == Kind.Move) lastMoveTime = Time.unscaledTime;
            lastFrame = Time.frameCount;
            lastPriority = Priority[k];

            EnsureSource();
            if (clips == null) clips = new AudioClip[FileNames.Length];
            if (clips[k] == null)
            {
                clips[k] = Resources.Load<AudioClip>("Audio/Sfx/" + FileNames[k]);
                if (clips[k] == null) clips[k] = Synthesize(kind);
            }
            source.pitch = kind == Kind.Move ? Random.Range(0.97f, 1.03f) : 1f;
            source.PlayOneShot(clips[k], Volume[k] * Aethoria.Save.GameSettings.SfxVolume);
        }

        private static void EnsureSource()
        {
            if (source != null) return;
            var go = new GameObject("UiSfx");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
        }

        // ---------------------------------------------------------------- 합성
        private const int Rate = 44100;

        private static AudioClip Synthesize(Kind kind)
        {
            float length = kind switch { Kind.Move => 0.06f, Kind.Confirm => 0.22f, Kind.Cancel => 0.16f, Kind.Open => 0.28f, Kind.Close => 0.18f, Kind.Anvil => 0.5f, Kind.EnhanceSuccess => 0.9f, Kind.EnhanceFail => 0.7f, _ => 0.2f };
            int n = (int)(Rate * length);
            var data = new float[n];
            var rng = new System.Random(1234 + (int)kind);
            float phase = 0f, phase2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float k = t / length;
                float s = 0f;
                switch (kind)
                {
                    case Kind.Move:
                        // 짧고 맑은 "틱": 높은 사인 + 아주 짧은 잡음.
                        phase += 2f * Mathf.PI * 1850f / Rate;
                        s = Mathf.Sin(phase) * Mathf.Exp(-t * 70f) * 0.6f
                            + (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 400f) * 0.25f;
                        break;
                    case Kind.Confirm:
                        // 두 음이 이어지는 맑은 "띠링"(5도 위로).
                        float f = t < 0.06f ? 880f : 1320f;
                        phase += 2f * Mathf.PI * f / Rate;
                        phase2 += 2f * Mathf.PI * f * 2.01f / Rate;
                        float env = t < 0.06f ? Mathf.Exp(-t * 30f) : Mathf.Exp(-(t - 0.06f) * 16f);
                        s = (Mathf.Sin(phase) * 0.55f + Mathf.Sin(phase2) * 0.15f) * env;
                        break;
                    case Kind.Cancel:
                        // 아래로 떨어지는 부드러운 음.
                        phase += 2f * Mathf.PI * Mathf.Lerp(700f, 380f, k) / Rate;
                        s = Mathf.Sin(phase) * Mathf.Exp(-t * 18f) * 0.55f;
                        break;
                    case Kind.Open:
                        // 위로 쓸려 올라가는 바람 + 은은한 종소리.
                        phase += 2f * Mathf.PI * Mathf.Lerp(300f, 900f, k * k) / Rate;
                        phase2 += 2f * Mathf.PI * 1560f / Rate;
                        float noise = (float)(rng.NextDouble() * 2 - 1);
                        s = Mathf.Sin(phase) * 0.25f * Mathf.Sin(Mathf.PI * k)
                            + noise * 0.12f * Mathf.Sin(Mathf.PI * k)
                            + Mathf.Sin(phase2) * 0.2f * Mathf.Exp(-Mathf.Max(0f, t - 0.08f) * 14f) * (t > 0.08f ? 1f : 0f);
                        break;
                    case Kind.Close:
                        // 아래로 쓸려 내려가는 바람.
                        phase += 2f * Mathf.PI * Mathf.Lerp(800f, 260f, k) / Rate;
                        float n2 = (float)(rng.NextDouble() * 2 - 1);
                        s = (Mathf.Sin(phase) * 0.25f + n2 * 0.1f) * Mathf.Sin(Mathf.PI * Mathf.Min(1f, k * 1.4f)) * (1f - k);
                        break;
                    case Kind.Anvil:
                    {
                        // 망치가 모루를 때리는 "깡": 짧은 딱 소리 + 배음이 아닌 금속 울림.
                        float ring = Mathf.Sin(2f * Mathf.PI * 1250f * t) * 0.35f * Mathf.Exp(-t * 9f)
                            + Mathf.Sin(2f * Mathf.PI * 2710f * t) * 0.22f * Mathf.Exp(-t * 13f)
                            + Mathf.Sin(2f * Mathf.PI * 4130f * t) * 0.12f * Mathf.Exp(-t * 18f);
                        float hit = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 140f) * 0.7f;
                        phase += 2f * Mathf.PI * Mathf.Lerp(180f, 90f, k) / Rate;
                        s = ring + hit + Mathf.Sin(phase) * 0.3f * Mathf.Exp(-t * 25f);
                        break;
                    }
                    case Kind.EnhanceSuccess:
                    {
                        // 위로 올라가는 반짝이는 세 음(도-미-솔-도) + 은은한 꼬리.
                        float[] notes = { 784f, 988f, 1175f, 1568f };
                        int idx = Mathf.Min(notes.Length - 1, (int)(t / 0.09f));
                        float local = t - idx * 0.09f;
                        phase += 2f * Mathf.PI * notes[idx] / Rate;
                        phase2 += 2f * Mathf.PI * notes[idx] * 2f / Rate;
                        float noteEnv = Mathf.Exp(-local * (idx == notes.Length - 1 ? 4f : 14f));
                        s = (Mathf.Sin(phase) * 0.45f + Mathf.Sin(phase2) * 0.15f) * noteEnv;
                        break;
                    }
                    case Kind.EnhanceFail:
                    {
                        // 힘 빠지듯 내려가는 낮은 음 + 바스러지는 잡음.
                        phase += 2f * Mathf.PI * Mathf.Lerp(330f, 140f, k) / Rate;
                        float crumble = (float)(rng.NextDouble() * 2 - 1) * 0.12f * Mathf.Exp(-t * 6f);
                        s = (Mathf.Sin(phase) * 0.4f + Mathf.Sin(phase * 0.5f) * 0.2f) * Mathf.Exp(-t * 3.5f) + crumble;
                        break;
                    }
                    default:
                        // 안 됨: 낮게 두 번 "붑붑".
                        phase += 2f * Mathf.PI * 150f / Rate;
                        float gate = (k < 0.4f || (k > 0.5f && k < 0.9f)) ? 1f : 0f;
                        s = Mathf.Sign(Mathf.Sin(phase)) * 0.18f * gate * (1f - k * 0.5f);
                        break;
                }
                // 시작·끝 클릭 방지.
                float edge = Mathf.Min(1f, i / 40f) * Mathf.Min(1f, (n - i) / 80f);
                data[i] = s * edge;
            }
            var clip = AudioClip.Create("UiSfx_" + kind, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

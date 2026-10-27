using UnityEngine;

namespace Aethoria.Save
{
    // 타이틀 화면 SETTINGS 창(SettingsUI)에서 바꾸는 환경 설정. 세이브 슬롯과 상관없이 PlayerPrefs에 한 벌만 저장하고,
    // 게임을 켜자마자(첫 씬을 불러오기 전) 불러와 바로 적용한다. 값을 바꾸는 즉시 적용·저장한다.
    public static class GameSettings
    {
        // 고를 수 있는 해상도(16:9). 모니터보다 큰 것은 목록에서 뺀다.
        private static readonly Vector2Int[] AllResolutions =
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
        };

        // 프레임 제한. 0 = 수직 동기화, -1 = 무제한, 그 밖에는 초당 프레임 수.
        public static readonly int[] FrameLimits = { 0, 30, 60, 120, 144, -1 };

        private const string KeyFullscreen = "settings.fullscreen";
        private const string KeyResolutionW = "settings.resolutionWidth";
        private const string KeyResolutionH = "settings.resolutionHeight";
        private const string KeyFrameLimit = "settings.frameLimit";
        private const string KeyVolume = "settings.masterVolume";
        private const string KeyScreenShake = "settings.screenShake";
        private const string KeyDamageNumbers = "settings.damageNumbers";

        public static bool Fullscreen { get; private set; } = true;
        public static Vector2Int Resolution { get; private set; } = new Vector2Int(1920, 1080);
        public static int FrameLimit { get; private set; }
        public static float MasterVolume { get; private set; } = 1f;
        // 끄면 CameraFollow.Shake가 아무것도 하지 않는다(보스 포효/착지 충격 등 모든 화면 흔들림).
        public static bool ScreenShake { get; private set; } = true;
        // 끄면 DamagePopup이 숫자를 띄우지 않는다(피해·치명타·회복 숫자 모두).
        public static bool DamageNumbers { get; private set; } = true;

        public static Vector2Int[] AvailableResolutions
        {
            get
            {
                var desktop = Screen.currentResolution;
                var list = new System.Collections.Generic.List<Vector2Int>();
                foreach (var r in AllResolutions)
                {
                    if (r.x <= desktop.width && r.y <= desktop.height) list.Add(r);
                }
                if (list.Count == 0) list.Add(AllResolutions[0]);
                return list.ToArray();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadAndApply()
        {
            Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
            var available = AvailableResolutions;
            var fallback = available[available.Length - 1];
            foreach (var r in available) if (r == new Vector2Int(1920, 1080)) fallback = r;
            Resolution = new Vector2Int(PlayerPrefs.GetInt(KeyResolutionW, fallback.x), PlayerPrefs.GetInt(KeyResolutionH, fallback.y));
            FrameLimit = PlayerPrefs.GetInt(KeyFrameLimit, 0);
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVolume, 1f));
            ScreenShake = PlayerPrefs.GetInt(KeyScreenShake, 1) == 1;
            DamageNumbers = PlayerPrefs.GetInt(KeyDamageNumbers, 1) == 1;
            ApplyDisplay();
            ApplyFrameLimit();
            AudioListener.volume = MasterVolume;
        }

        public static void SetFullscreen(bool value)
        {
            Fullscreen = value;
            PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0);
            ApplyDisplay();
            PlayerPrefs.Save();
        }

        public static void SetResolution(Vector2Int value)
        {
            Resolution = value;
            PlayerPrefs.SetInt(KeyResolutionW, value.x);
            PlayerPrefs.SetInt(KeyResolutionH, value.y);
            ApplyDisplay();
            PlayerPrefs.Save();
        }

        public static void SetFrameLimit(int value)
        {
            FrameLimit = value;
            PlayerPrefs.SetInt(KeyFrameLimit, value);
            ApplyFrameLimit();
            PlayerPrefs.Save();
        }

        public static void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyVolume, MasterVolume);
            AudioListener.volume = MasterVolume;
            PlayerPrefs.Save();
        }

        public static void SetScreenShake(bool value)
        {
            ScreenShake = value;
            PlayerPrefs.SetInt(KeyScreenShake, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SetDamageNumbers(bool value)
        {
            DamageNumbers = value;
            PlayerPrefs.SetInt(KeyDamageNumbers, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            var available = AvailableResolutions;
            var resolution = available[available.Length - 1];
            foreach (var r in available) if (r == new Vector2Int(1920, 1080)) resolution = r;
            SetFullscreen(true);
            SetResolution(resolution);
            SetFrameLimit(0);
            SetMasterVolume(1f);
            SetScreenShake(true);
            SetDamageNumbers(true);
        }

        // 전체 화면은 테두리 없는 전체 창(FullScreenWindow)으로 띄워 Alt+Tab이 빠르다. 에디터에서는 게임 뷰 크기를 바꾸지 않는다.
        private static void ApplyDisplay()
        {
            if (Application.isEditor) return;
            var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(Resolution.x, Resolution.y, mode);
        }

        private static void ApplyFrameLimit()
        {
            if (FrameLimit == 0)
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = -1;
            }
            else
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = FrameLimit;
            }
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.UI;

namespace Aethoria.Monsters
{
    // 루나벨 2페이즈의 타임어택. 멀리 도망쳐 스킬이 다 찰 때까지 시간을 끄는 플레이를 막으려고 화면에 제한 시간을 띄운다.
    // 시간이 다 되면 응시 패턴이 온다: 세상이 멈추고 화면이 암전 → 어둠 속에서 붉은 눈들이 떠서 네크로시아를 응시 → 최대 체력이 깎인다.
    // 깎인 최대 체력은 이 전투 동안 쌓이고(MaxPenalty까지), 시간은 다시 돌아간다. 쓰러지거나 전투가 끝나면 원래대로 돌아온다.
    // 첫 응시는 2페이즈 개막 패턴의 기둥이 사그라들자마자(루나벨이 던진 자세로 공중에 떠 있는 채로) 오고,
    // 어둠 속에서 대사로 규칙을 알려 준 뒤 시간이 돌기 시작한다.
    public class LunabelGazeCurse : MonoBehaviour
    {
        private const string FramesPath = "Backgrounds/LunabelGaze";

        private const string LunabelNextHint = "   <color=#E07A8A>[Z]</color>";
        private const string LunabelSkipHint = "   <color=#E07A8A>[Esc] 스킵</color>";
        private const string PlayerNextHint = "   <color=#B98CE8>[Z]</color>";
        private const string PlayerSkipHint = "   <color=#B98CE8>[Esc] 스킵</color>";

        private static readonly (bool player, string line)[] FirstGazeLines =
        {
            (false, "후후... 달아나면서 힘이 차오르길 기다릴 생각이었어? 그렇게 둘 줄 알고?"),
            (false, "보이지? 이 눈들은 전부 널 보고 있어. 시간이 흐를수록 네 피는 내 것이 돼."),
            (true, "...몸이 무거워졌다. 시간을 끌수록 불리해진다는 건가."),
            (false, "그래, 서두르는 게 좋을 거야. 네 심장이 다 마르기 전에."),
            (true, "그럼 그 전에 끝내면 될 일이다."),
        };

        private const float FirstLimit = 60f;
        private const float RepeatLimit = 45f;
        private const float WarningTime = 10f;      // 이 아래로 내려가면 시간 글자가 붉어진다
        // 첫 응시에서만 최대 체력이 줄고, 응시마다 '주시' 디버프로 한동안 아이템(포션)을 쓸 수 없다.
        private const float FirstGazePenalty = 0.15f;
        private const float ItemLockSeconds = 15f;

        // 응시 연출: 세상이 어두워짐 → 감긴 눈들이 스며 나옴 → 천천히 뜨다가 마지막에 번쩍 뜸(붉은 섬광·화면 울림)
        // → 심장이 두 번 뛰듯 두근거림 → 눈을 감으며 사라짐. 눈 그림 10장 사이는 겹쳐 그려(크로스페이드) 끊기지 않게 한다.
        private const float BlackoutTime = 0.55f;
        private const float EyesFadeTime = 0.45f;
        private const float EyesOpenTime = 1.9f;
        private const float SnapTime = 0.35f;        // 번쩍 뜨는 순간의 섬광·울림
        private const float StareTime = 1.3f;        // 두근거림(박동 두 번)
        private const float CloseTime = 0.45f;       // 눈을 감으며 흐려짐
        private const float ReleaseTime = 0.5f;
        private const float PushStart = 1.03f, PushEnd = 1.09f; // 응시하는 내내 아주 천천히 다가온다(가장자리도 가려 준다)

        private static readonly Color NormalTimeColor = Color.white;
        private static readonly Color WarningTimeColor = new Color(1f, 0.25f, 0.25f);

        private FinalBossAI boss;
        private Monster monster;
        private Character player;
        private Texture2D[] frames;
        private GameObject timerRoot;
        private Text timerText;
        private Text debuffText;
        private readonly System.Collections.Generic.List<WatchingEye> watchingEyes = new();
        private bool eyesClosed;
        private GameObject overlayRoot;
        private Image blackout;
        private RawImage eyes;
        private RawImage eyesNext;      // 다음 프레임(겹쳐 그려 부드럽게 넘어간다)
        private Image flash;            // 번쩍 뜨는 순간의 붉은 섬광
        private RawImage vignette;      // 가장자리를 붉게 조여 오는 그늘
        private RectTransform eyesGroup;
        // 새 응시 그림: 붉은 안개 배경 + 곳곳에 흩뿌린 작은 눈(차례로 하나씩 뜬다) + 가운데 큰 눈(마지막에 번쩍) + 가장자리에서 조여 오는 가시.
        private Texture2D[] smallFrames;
        private RawImage gazeBg;
        private RawImage thorns;
        private readonly System.Collections.Generic.List<(RawImage a, RawImage b, float start, float length)> eyeSlots = new();

        // 작은 눈 자리(화면 비율 x, y, 크기 배율, 기울기). 가운데 큰 눈 자리(가운데 약 40%)는 비운다.
        private static readonly Vector4[] SmallEyeLayout =
        {
            new Vector4(0.11f, 0.80f, 0.95f, -8f), new Vector4(0.29f, 0.90f, 0.70f, 6f), new Vector4(0.84f, 0.84f, 1.00f, 9f),
            new Vector4(0.66f, 0.91f, 0.65f, -5f), new Vector4(0.07f, 0.47f, 0.80f, 4f), new Vector4(0.19f, 0.20f, 1.05f, -6f),
            new Vector4(0.39f, 0.11f, 0.75f, 3f), new Vector4(0.62f, 0.12f, 0.85f, -9f), new Vector4(0.86f, 0.24f, 0.95f, 7f),
            new Vector4(0.94f, 0.56f, 0.72f, -4f), new Vector4(0.27f, 0.56f, 0.60f, 10f), new Vector4(0.74f, 0.60f, 0.62f, -10f),
        };
        // 작은 눈마다 뜨기 시작하는 때(전체 진행 0~1). 흩어진 순서로 하나둘 뜨게 섞어 둔다.
        private static readonly float[] SmallEyeStart = { 0.05f, 0.30f, 0.12f, 0.42f, 0.22f, 0.00f, 0.36f, 0.18f, 0.08f, 0.48f, 0.27f, 0.40f };
        private const float SmallEyeLength = 0.34f;
        private const float BigEyeStart = 0.5f;
        private const float SmallEyeWidth = 150f;   // 기준 해상도(1280x720) 위 폭
        private const float BigEyeWidth = 560f;

        private static Texture2D[] LoadFrames(string prefix)
        {
            var list = new System.Collections.Generic.List<Texture2D>();
            for (int i = 1; i <= 12; i++)
            {
                var tex = Resources.Load<Texture2D>($"{FramesPath}/{prefix}_{i}");
                if (tex == null) break;
                list.Add(tex);
            }
            return list.ToArray();
        }
        private Text message;
        private NpcDialogueUI dialogueUI;
        private bool skippable;
        private System.Action onTalkShown;
        private bool firstGazeDone;
        private float timeLeft = FirstLimit;
        private bool gazing;
        private bool talking;

        public void Initialize(FinalBossAI lunabel, Monster lunabelMonster, Character targetPlayer,
            NpcDialogueUI dialogue, bool talkSkippable, System.Action talkShown)
        {
            boss = lunabel;
            monster = lunabelMonster;
            player = targetPlayer;
            dialogueUI = dialogue;
            skippable = talkSkippable;
            onTalkShown = talkShown;

            // 가운데 큰 눈 6단계(eye_big_1~6)가 있어야 응시 연출을 쓴다. 흩뿌린 작은 눈(eye_small), 배경, 가시 테두리는 없으면 빼고 그린다.
            frames = LoadFrames("eye_big");
            smallFrames = LoadFrames("eye_small");

            BuildTimer();
            BuildOverlay();
            boss.OpeningGaze = () => frames.Length > 0 ? GazeRoutine() : null;
        }

        private static GameObject CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            return go;
        }

        private static Text CreateText(Transform parent, string name, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        // 보스 체력바 바로 아래에 남은 시간을 띄운다.
        private void BuildTimer()
        {
            timerRoot = CreateCanvas(transform, "LunabelGazeTimer", 1);
            timerText = CreateText(timerRoot.transform, "Time", 24);
            var rect = timerText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -92f);
            rect.sizeDelta = new Vector2(200f, 32f);

            // 주시 디버프 표시(남은 시간 아래).
            debuffText = CreateText(timerRoot.transform, "Debuff", 17);
            var drect = debuffText.rectTransform;
            drect.anchorMin = drect.anchorMax = new Vector2(0.5f, 1f);
            drect.pivot = new Vector2(0.5f, 1f);
            drect.anchoredPosition = new Vector2(0f, -122f);
            drect.sizeDelta = new Vector2(420f, 26f);
            debuffText.color = new Color(1f, 0.45f, 0.5f);
            timerRoot.SetActive(false);
        }

        // HUD 캔버스(순서 0)보다 뒤에 깔아서, 게임 화면은 가려도 체력바가 깎이는 모습은 보이게 한다.
        private void BuildOverlay()
        {
            overlayRoot = CreateCanvas(transform, "LunabelGazeOverlay", -1);

            var blackGO = new GameObject("Blackout", typeof(RectTransform), typeof(Image));
            blackGO.transform.SetParent(overlayRoot.transform, false);
            Stretch((RectTransform)blackGO.transform);
            blackout = blackGO.GetComponent<Image>();
            blackout.raycastTarget = false;
            blackout.color = new Color(0f, 0f, 0f, 0f);

            // 눈 두 장(지금 프레임 + 다음 프레임)을 한 틀에 넣어 함께 다가오고 함께 울리게 한다.
            var groupGO = new GameObject("EyesGroup", typeof(RectTransform));
            groupGO.transform.SetParent(overlayRoot.transform, false);
            eyesGroup = (RectTransform)groupGO.transform;
            Stretch(eyesGroup);
            gazeBg = MakeRaw(eyesGroup, "GazeBackground");
            gazeBg.texture = Resources.Load<Texture2D>(FramesPath + "/gaze_bg");
            if (smallFrames.Length > 0)
            {
                for (int i = 0; i < SmallEyeLayout.Length; i++)
                {
                    var l = SmallEyeLayout[i];
                    float w = SmallEyeWidth * l.z, h = w * smallFrames[0].height / smallFrames[0].width;
                    var a = MakeEyeImage(eyesGroup, "SmallEye" + i, new Vector2(l.x, l.y), new Vector2(w, h), l.w);
                    var b = MakeEyeImage(eyesGroup, "SmallEyeNext" + i, new Vector2(l.x, l.y), new Vector2(w, h), l.w);
                    eyeSlots.Add((a, b, SmallEyeStart[i % SmallEyeStart.Length], SmallEyeLength));
                }
            }
            if (frames.Length > 0)
            {
                float bh = BigEyeWidth * frames[0].height / frames[0].width;
                eyes = MakeEyeImage(eyesGroup, "BigEye", new Vector2(0.5f, 0.52f), new Vector2(BigEyeWidth, bh), 0f);
                eyesNext = MakeEyeImage(eyesGroup, "BigEyeNext", new Vector2(0.5f, 0.52f), new Vector2(BigEyeWidth, bh), 0f);
                eyeSlots.Add((eyes, eyesNext, BigEyeStart, 1f - BigEyeStart));
            }

            thorns = MakeRaw(overlayRoot.transform, "Thorns");
            thorns.texture = Resources.Load<Texture2D>(FramesPath + "/gaze_thorns");

            var flashGO = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            flashGO.transform.SetParent(overlayRoot.transform, false);
            Stretch((RectTransform)flashGO.transform);
            flash = flashGO.GetComponent<Image>();
            flash.raycastTarget = false;
            flash.color = new Color(1f, 0.1f, 0.12f, 0f);

            vignette = MakeRaw(overlayRoot.transform, "Vignette");
            vignette.texture = MakeVignette();

            message = CreateText(overlayRoot.transform, "Message", 34);
            Stretch(message.rectTransform);
            message.color = new Color(1f, 0.85f, 0.85f);

            overlayRoot.SetActive(false);
        }

        private static RawImage MakeEyeImage(Transform parent, string name, Vector2 anchor, Vector2 size, float rotation)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var raw = go.GetComponent<RawImage>();
            raw.raycastTarget = false;
            raw.color = new Color(1f, 1f, 1f, 0f);
            return raw;
        }

        private static RawImage MakeRaw(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var raw = go.GetComponent<RawImage>();
            raw.raycastTarget = false;
            raw.color = new Color(1f, 1f, 1f, 0f);
            return raw;
        }

        // 가운데는 투명하고 가장자리로 갈수록 검붉어지는 그늘(가로로 긴 타원).
        private static Texture2D MakeVignette()
        {
            const int W = 128, H = 72;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
                    float d = Mathf.Sqrt(u * u * 0.85f + v * v);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, d));
                    px[y * W + x] = new Color(0.25f, 0f, 0.02f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (gazing || boss == null) return;

            if (monster.IsDead && !eyesClosed) CloseWatchingEyes();
            bool fighting = boss.PhaseTwoStarted && !boss.IsTransforming && !monster.IsDead && player != null && !player.IsDead;
            timerRoot.SetActive(fighting && firstGazeDone);
            if (debuffText != null && player != null)
                debuffText.text = player.ItemsLocked ? $"주시 — 아이템 사용 불가  {Mathf.CeilToInt(player.ItemLockRemaining)}초" : string.Empty;
            if (!fighting) return;

            if (!firstGazeDone)
            {
                if (!boss.IsBusy && frames.Length > 0) StartCoroutine(GazeRoutine());
                return;
            }

            timeLeft = Mathf.Max(0f, timeLeft - Time.deltaTime);
            int seconds = Mathf.CeilToInt(timeLeft);
            timerText.text = $"{seconds / 60}:{seconds % 60:00}";
            timerText.color = timeLeft <= WarningTime ? WarningTimeColor : NormalTimeColor;

            // 공격 도중에 끊으면 패턴이 꼬이므로 시간이 다 되어도 지금 공격이 끝나기를 기다린다.
            if (timeLeft <= 0f && !boss.IsBusy && frames.Length > 0) StartCoroutine(GazeRoutine());
        }

        private IEnumerator GazeRoutine()
        {
            gazing = true;
            timerRoot.SetActive(false);
            Time.timeScale = 0f;
            overlayRoot.SetActive(true);
            message.text = string.Empty;
            message.color = new Color(1f, 0.85f, 0.85f, 0f);
            ShowEyes(0f, 0f);
            eyesGroup.localScale = Vector3.one * PushStart;
            eyesGroup.anchoredPosition = Vector2.zero;
            flash.color = new Color(1f, 0.1f, 0.12f, 0f);
            float total = EyesFadeTime + EyesOpenTime + SnapTime + StareTime;
            float elapsed = 0f;

            // 1) 세상이 어두워지고 가장자리부터 붉은 그늘이 조여 온다.
            for (float t = 0f; t < BlackoutTime; t += Time.unscaledDeltaTime)
            {
                float k = Smooth(t / BlackoutTime);
                blackout.color = new Color(0f, 0f, 0f, k);
                vignette.color = new Color(1f, 1f, 1f, 0.6f * k);
                yield return null;
            }
            blackout.color = Color.black;

            // 2) 감긴 눈들이 어둠 속에서 스며 나온다.
            for (float t = 0f; t < EyesFadeTime; t += Time.unscaledDeltaTime, elapsed += Time.unscaledDeltaTime)
            {
                ShowEyes(0f, Smooth(t / EyesFadeTime));
                Push(elapsed / total);
                yield return null;
            }

            // 3) 천천히 뜨다가 끝에서 빨라진다(ease-in). 프레임 사이는 겹쳐 그린다.
            for (float t = 0f; t < EyesOpenTime; t += Time.unscaledDeltaTime, elapsed += Time.unscaledDeltaTime)
            {
                float k = t / EyesOpenTime;
                ShowEyes(k * k * k, 1f);
                vignette.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.6f, 0.85f, k));
                Push(elapsed / total);
                yield return null;
            }
            ShowEyes(1f, 1f);

            // 4) 번쩍 — 붉은 섬광, 살짝 튀어나왔다 돌아오며 화면이 울린다(무작위가 아니라 잦아드는 흔들림).
            if (!firstGazeDone)
            {
                player.ReduceMaxHp(FirstGazePenalty, FirstGazePenalty);
                message.text = $"최대 체력이 {Mathf.RoundToInt(player.MaxHpPenalty * 100f)}% 감소했다\n<size=24>주시당했다 — {ItemLockSeconds:0}초 동안 아이템을 쓸 수 없다</size>";
            }
            else
            {
                message.text = $"주시당했다\n<size=24>{ItemLockSeconds:0}초 동안 아이템을 쓸 수 없다</size>";
            }
            for (float t = 0f; t < SnapTime; t += Time.unscaledDeltaTime, elapsed += Time.unscaledDeltaTime)
            {
                float k = t / SnapTime;
                flash.color = new Color(1f, 0.1f, 0.12f, 0.55f * (1f - k) * (1f - k));
                float punch = 0.05f * Mathf.Sin(k * Mathf.PI) * (1f - k);
                float shake = 10f * (1f - k) * (1f - k);
                eyesGroup.localScale = Vector3.one * (Mathf.Lerp(PushStart, PushEnd, elapsed / total) + punch);
                eyesGroup.anchoredPosition = new Vector2(Mathf.Sin(t * 70f) * shake, Mathf.Cos(t * 53f) * shake * 0.6f);
                message.color = new Color(1f, 0.85f, 0.85f, Mathf.Clamp01(k * 2f));
                yield return null;
            }
            flash.color = new Color(1f, 0.1f, 0.12f, 0f);
            eyesGroup.anchoredPosition = Vector2.zero;
            message.color = new Color(1f, 0.85f, 0.85f, 1f);

            // 5) 두근, 두근 — 박동마다 화면이 살짝 부풀고 그늘이 짙어진다.
            for (float t = 0f; t < StareTime; t += Time.unscaledDeltaTime, elapsed += Time.unscaledDeltaTime)
            {
                float beat = Heartbeat(t / StareTime);
                eyesGroup.localScale = Vector3.one * (Mathf.Lerp(PushStart, PushEnd, elapsed / total) + 0.025f * beat);
                vignette.color = new Color(1f, 1f, 1f, 0.85f + 0.15f * beat);
                yield return null;
            }

            if (!firstGazeDone)
            {
                message.text = string.Empty;
                yield return FirstGazeTalk();
            }

            // 6) 눈을 감으며(프레임을 거꾸로) 흐려지고, 세상이 다시 밝아진다.
            for (float t = 0f; t < CloseTime; t += Time.unscaledDeltaTime)
            {
                float k = t / CloseTime;
                ShowEyes(1f - Smooth(k), 1f - k * k);
                message.color = new Color(1f, 0.85f, 0.85f, 1f - k);
                yield return null;
            }
            ShowEyes(0f, 0f);
            message.text = string.Empty;
            for (float t = 0f; t < ReleaseTime; t += Time.unscaledDeltaTime)
            {
                float k = Smooth(t / ReleaseTime);
                blackout.color = new Color(0f, 0f, 0f, 1f - k);
                vignette.color = new Color(1f, 1f, 1f, 0.85f * (1f - k));
                yield return null;
            }
            blackout.color = new Color(0f, 0f, 0f, 0f);
            vignette.color = new Color(1f, 1f, 1f, 0f);
            eyesGroup.localScale = Vector3.one;

            overlayRoot.SetActive(false);
            Time.timeScale = 1f;
            player.ItemLockedUntil = Time.time + ItemLockSeconds;
            if (!firstGazeDone) SpawnWatchingEyes();
            timeLeft = firstGazeDone ? RepeatLimit : FirstLimit;
            firstGazeDone = true;
            gazing = false;
        }

        // open: 0(감김) ~ 1(다 뜸) — 전체 진행도. 작은 눈들은 저마다 정해진 때에 하나씩 뜨고, 가운데 큰 눈은 마지막에 뜬다.
        // 각 눈은 이웃한 두 프레임을 겹쳐 그려 끊기지 않게 한다. alpha: 배경·눈·가시 전체 투명도.
        private void ShowEyes(float open, float alpha)
        {
            if (gazeBg != null) gazeBg.color = new Color(1f, 1f, 1f, alpha);
            if (thorns != null)
            {
                thorns.color = new Color(1f, 1f, 1f, alpha);
                thorns.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.18f, 1f, Smooth(alpha)); // 가장자리에서 조여 든다
            }
            foreach (var (a, b, start, length) in eyeSlots)
            {
                var set = a == eyes ? frames : smallFrames;
                float local = Mathf.Clamp01((open - start) / length);
                local = local * local * (3f - 2f * local);
                float pos = local * (set.Length - 1);
                int i0 = Mathf.FloorToInt(pos), i1 = Mathf.Min(set.Length - 1, i0 + 1);
                a.texture = set[i0];
                b.texture = set[i1];
                // 아직 뜨기 전인 눈은 희미하게(감긴 채 어둠 속에) 보인다.
                float presence = alpha * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(local * 3f));
                a.color = new Color(1f, 1f, 1f, presence);
                b.color = new Color(1f, 1f, 1f, presence * (pos - i0));
            }
        }

        private void Push(float k) => eyesGroup.localScale = Vector3.one * Mathf.Lerp(PushStart, PushEnd, Mathf.Clamp01(k));

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        // 0~1 동안 심장이 두 번 뛰는 모양(두근-두근, 각 박동은 빠르게 올랐다 천천히 가라앉는다).
        private static float Heartbeat(float x)
        {
            float Pulse(float c) { float d = x - c; return d < 0f ? Mathf.Exp(-d * d * 900f) : Mathf.Exp(-d * 14f) * (d < 0.35f ? 1f : 0f); }
            return Mathf.Clamp01(Pulse(0.12f) + 0.75f * Pulse(0.32f));
        }

        // 세상이 멈춘 채로 대사를 넘긴다. 대화창은 HUD 캔버스에 있어서 눈 그림 위로 보인다.
        private IEnumerator FirstGazeTalk()
        {
            talking = true;
            onTalkShown?.Invoke();
            foreach (var (isPlayer, line) in FirstGazeLines)
            {
                if (isPlayer) dialogueUI?.ShowNecrosia(line + (skippable ? PlayerSkipHint : "") + PlayerNextHint);
                else dialogueUI?.ShowLunabel(line + (skippable ? LunabelSkipHint : "") + LunabelNextHint);

                yield return null; // 대사가 바뀐 그 프레임의 입력으로 바로 넘어가지 않게
                bool skip = false;
                while (!AdvancePressed())
                {
                    skip = SkipPressed();
                    if (skip) break;
                    yield return null;
                }
                if (skip) break;
            }
            dialogueUI?.Hide();
            talking = false;
        }

        private bool SkipPressed()
        {
            var keyboard = Keyboard.current;
            return skippable && keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        private static bool AdvancePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return NpcDialogueUI.AdvancePressed(keyboard, allowEnter: true); // 타이핑 마저 보여 주기/다음 장은 대화창이 먼저 쓴다
        }

        private static IEnumerator Fade(System.Action<float> apply, float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                apply(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            apply(to);
        }

        // 첫 응시가 끝나면 결전장 곳곳에 눈이 떠올라 네크로시아를 따라본다(맵이 지워질 때 함께 사라진다).
        private static readonly Vector2[] EyeSpots =
        {
            new Vector2(0.06f, 3.3f), new Vector2(0.18f, 5.7f), new Vector2(0.31f, 2.7f), new Vector2(0.44f, 4.9f),
            new Vector2(0.57f, 6.3f), new Vector2(0.70f, 3.1f), new Vector2(0.83f, 5.4f), new Vector2(0.95f, 4.0f),
        };
        private static readonly float[] EyeWidths = { 1.6f, 1.2f, 1.9f, 1.4f, 1.1f, 1.7f, 1.3f, 1.5f };

        private void SpawnWatchingEyes()
        {
            if (!WatchingEye.AssetsReady || player == null) return;
            Rect arena = Aethoria.Bootstrap.GameBootstrap.CurrentMapBounds;
            float x0 = arena.xMin + 1.5f, x1 = arena.xMax - 1.5f;
            for (int i = 0; i < EyeSpots.Length; i++)
            {
                var pos = new Vector3(Mathf.Lerp(x0, x1, EyeSpots[i].x), arena.yMin + EyeSpots[i].y, 0f);
                var eye = WatchingEye.Spawn(transform.parent, pos, EyeWidths[i], -11, player.transform, 0.15f + i * 0.22f);
                if (eye != null) watchingEyes.Add(eye);
            }
        }

        private void CloseWatchingEyes()
        {
            eyesClosed = true;
            foreach (var eye in watchingEyes) if (eye != null) eye.Close();
            watchingEyes.Clear();
        }

        private void OnDestroy()
        {
            if (player != null) player.ItemLockedUntil = 0f;
            // 응시 도중 맵이 사라지면 멈춘 세상을 풀어 준다.
            if (gazing) Time.timeScale = 1f;
            if (talking) dialogueUI?.Hide();
            if (player != null) player.ClearMaxHpPenalty();
        }
    }
}

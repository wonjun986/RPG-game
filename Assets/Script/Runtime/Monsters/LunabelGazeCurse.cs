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
        private const float PenaltyPerGaze = 0.15f;
        private const float MaxPenalty = 0.75f;

        private const float BlackoutTime = 0.4f;
        private const float EyesFadeTime = 0.3f;
        private const float EyesOpenTime = 1.6f;
        private const float StareTime = 1.1f;
        private const float ReleaseTime = 0.5f;
        private const float StareShake = 8f;        // 응시하는 동안 그림이 떨리는 폭(기준 해상도 픽셀)
        private const float StareOverscan = 1.04f;  // 떨려도 가장자리가 드러나지 않게 살짝 키운다

        private static readonly Color NormalTimeColor = Color.white;
        private static readonly Color WarningTimeColor = new Color(1f, 0.25f, 0.25f);

        private FinalBossAI boss;
        private Monster monster;
        private Character player;
        private Texture2D[] frames;
        private GameObject timerRoot;
        private Text timerText;
        private GameObject overlayRoot;
        private Image blackout;
        private RawImage eyes;
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

            frames = Resources.LoadAll<Texture2D>(FramesPath);
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));

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

            var eyesGO = new GameObject("Eyes", typeof(RectTransform), typeof(RawImage));
            eyesGO.transform.SetParent(overlayRoot.transform, false);
            Stretch((RectTransform)eyesGO.transform);
            eyes = eyesGO.GetComponent<RawImage>();
            eyes.raycastTarget = false;
            eyes.color = new Color(1f, 1f, 1f, 0f);

            message = CreateText(overlayRoot.transform, "Message", 34);
            Stretch(message.rectTransform);
            message.color = new Color(1f, 0.85f, 0.85f);

            overlayRoot.SetActive(false);
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

            bool fighting = boss.PhaseTwoStarted && !boss.IsTransforming && !monster.IsDead && player != null && !player.IsDead;
            timerRoot.SetActive(fighting && firstGazeDone);
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
            eyes.texture = frames[0];

            yield return Fade(a => blackout.color = new Color(0f, 0f, 0f, a), 0f, 1f, BlackoutTime);
            yield return Fade(a => eyes.color = new Color(1f, 1f, 1f, a), 0f, 1f, EyesFadeTime);

            for (float t = 0f; t < EyesOpenTime; t += Time.unscaledDeltaTime)
            {
                eyes.texture = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t / EyesOpenTime * frames.Length))];
                yield return null;
            }
            eyes.texture = frames[frames.Length - 1];

            player.ReduceMaxHp(PenaltyPerGaze, MaxPenalty);
            message.text = $"최대 체력이 {Mathf.RoundToInt(player.MaxHpPenalty * 100f)}% 감소했다";
            var eyesRect = eyes.rectTransform;
            eyesRect.localScale = Vector3.one * StareOverscan;
            for (float t = 0f; t < StareTime; t += Time.unscaledDeltaTime)
            {
                float strength = StareShake * (1f - t / StareTime);
                eyesRect.anchoredPosition = Random.insideUnitCircle * strength;
                yield return null;
            }
            eyesRect.anchoredPosition = Vector2.zero;
            eyesRect.localScale = Vector3.one;

            message.text = string.Empty;
            if (!firstGazeDone) yield return FirstGazeTalk();

            eyes.color = new Color(1f, 1f, 1f, 0f);
            yield return Fade(a => blackout.color = new Color(0f, 0f, 0f, a), 1f, 0f, ReleaseTime);

            overlayRoot.SetActive(false);
            Time.timeScale = 1f;
            timeLeft = firstGazeDone ? RepeatLimit : FirstLimit;
            firstGazeDone = true;
            gazing = false;
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
            return keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame;
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

        private void OnDestroy()
        {
            // 응시 도중 맵이 사라지면 멈춘 세상을 풀어 준다.
            if (gazing) Time.timeScale = 1f;
            if (talking) dialogueUI?.Hide();
            if (player != null) player.ClearMaxHpPenalty();
        }
    }
}

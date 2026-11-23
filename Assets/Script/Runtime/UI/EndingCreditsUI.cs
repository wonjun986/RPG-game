using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Bootstrap;

namespace Aethoria.UI
{
    // 루나벨을 처음 쓰러뜨렸을 때 처치 연출(LunabelDeathCutscene)이 끝난 뒤 한 번 나오는 엔딩.
    //   1) 에필로그: 장마다 일러스트(UI/Ending/...)를 깔고 문장을 한 글자씩 쓴다. 그림이 아직 없으면 검은 화면에 문장만.
    //   2) 크레딧: 검은 화면 아래에서 위로 글이 천천히 올라간다. Z/Enter를 누르고 있으면 빨리 올라간다.
    // Esc를 누르면 남은 엔딩을 모두 건너뛴다. 엔딩 곡(Audio/Bgm/ending)이 없으면 마을 곡을 대신 튼다.
    // 시간 정지와 상관없이 돌도록 전부 실시간으로 잰다.
    public class EndingCreditsUI : MonoBehaviour
    {
        public const string EndingBgmPath = "Audio/Bgm/ending";
        private const string FallbackBgmPath = "Audio/Bgm/village";

        private readonly struct EpiloguePage
        {
            public readonly string Illustration;
            public readonly string Text;
            public EpiloguePage(string illustration, string text) { Illustration = illustration; Text = text; }
        }

        // 일러스트 경로는 Resources 기준(확장자 없이). 그림을 넣기 전까지는 문장만 나온다.
        private static readonly EpiloguePage[] Epilogue =
        {
            new EpiloguePage("UI/Ending/ending_1", "붉은 달이 저물었다.\n오랜 밤 끝에, 하늘은 처음으로 푸른빛을 되찾았다."),
            new EpiloguePage("UI/Ending/ending_1", "피의 여왕이 남긴 핏빛 결정은 바람에 흩어졌고,\n그녀에게 묶여 있던 기사들과 정령들도 마침내 잠들었다."),
            new EpiloguePage("UI/Ending/ending_2", "마을 사람들은 거리로 나와 하늘을 올려다보았다.\n노아는 누구보다 먼저, 돌아오는 사신을 알아보았다."),
            new EpiloguePage("UI/Ending/ending_3", "천상의 신전에서는 이리아가 오래 미뤄 둔 기도를 올렸다.\n이번에는, 두려움이 아닌 감사의 기도를."),
            new EpiloguePage("UI/Ending/ending_4", "한때 꺾였던 낫은 다시 벼려졌다.\n그리고 사신 네크로시아는, 다시 이 땅의 밤을 지킨다."),
        };

        // 크레딧. "#"으로 시작하는 줄은 제목(크게, 보라색), 빈 줄은 간격이다. 이름은 직접 채워 넣는다.
        private static readonly string[] CreditLines =
        {
            "#AETHORIA",
            "",
            "",
            "#기획 · 개발",
            "(이름을 적어 주세요)",
            "",
            "#그래픽",
            "(이름을 적어 주세요)",
            "",
            "#음악 · 효과음",
            "(이름을 적어 주세요)",
            "",
            "#등장인물",
            "네크로시아",
            "노아 · 이시스 · 엘리 · 릴리",
            "이리아",
            "알리아스 · 아쿠아 · 루나벨",
            "",
            "",
            "#플레이해 주셔서 감사합니다",
        };

        private const int EpilogueFontSize = 28;
        private const int CreditFontSize = 26;
        private const int CreditTitleFontSize = 34;
        private const float CharsPerSecond = 26f;
        private const float FadeDuration = 0.6f;
        private const float CreditScrollSpeed = 60f;     // 캔버스 단위/초
        private const float CreditFastMultiplier = 4f;   // Z/Enter를 누르고 있을 때
        private const float CreditEndHold = 2.5f;        // 마지막 줄이 화면 가운데에 온 뒤 멈춰 있는 시간

        private static readonly Color TextColor = new Color(0.93f, 0.9f, 0.97f);
        private static readonly Color TitleColor = new Color(0.73f, 0.55f, 0.91f);

        private GameObject panel;
        private RawImage illustration;
        private AspectRatioFitter illustrationFitter;
        private Image band;
        private Text epilogueText;
        private Text hintText;
        private RectTransform creditsRoot;
        private Text creditsText;
        private Image fade;
        private Coroutine routine;
        private System.Action onFinished;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize()
        {
            BuildPanel();
        }

        public void Play(System.Action finished = null)
        {
            onFinished = finished;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling(); // 다른 HUD보다 위에
            BgmPlayer.Play(EndingBgmPath, 0f, fallbackPath: FallbackBgmPath);
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            SetFade(1f);
            creditsRoot.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.5f);

            bool skipped = false;
            for (int i = 0; i < Epilogue.Length && !skipped; i++)
            {
                bool sameScene = i > 0 && Epilogue[i].Illustration == Epilogue[i - 1].Illustration;
                if (!sameScene) ShowIllustration(Epilogue[i].Illustration);
                epilogueText.text = "";
                if (!sameScene) yield return Fade(1f, 0f);

                yield return TypeText(Epilogue[i].Text, () => skipped = true);
                if (skipped) break;

                yield return null; // 문장을 한 번에 띄운 입력으로 바로 넘어가지 않게
                while (!AdvancePressed())
                {
                    if (SkipPressed()) { skipped = true; break; }
                    yield return null;
                }
                if (skipped) break;

                bool nextSame = i + 1 < Epilogue.Length && Epilogue[i + 1].Illustration == Epilogue[i].Illustration;
                if (!nextSame) yield return Fade(0f, 1f);
            }

            if (!skipped) yield return RollCredits(() => skipped = true);

            yield return Fade(fade.color.a, 1f);
            Finish();
        }

        private IEnumerator RollCredits(System.Action onSkip)
        {
            illustration.enabled = false;
            band.enabled = false;
            epilogueText.text = "";
            hintText.text = "[Z] 빨리 넘기기   [Esc] 건너뛰기";
            creditsRoot.gameObject.SetActive(true);
            creditsText.text = BuildCreditsText();

            // 글 높이를 먼저 잰 뒤, 화면 아래에서 시작해 마지막 줄이 화면 가운데에 올 때까지 올린다.
            Canvas.ForceUpdateCanvases();
            float textHeight = creditsText.preferredHeight;
            float screenHeight = ((RectTransform)panel.transform).rect.height;
            creditsText.rectTransform.sizeDelta = new Vector2(creditsText.rectTransform.sizeDelta.x, textHeight);
            float y = -screenHeight * 0.5f - textHeight * 0.5f;
            float endY = textHeight * 0.5f - CreditTitleFontSize;
            creditsText.rectTransform.anchoredPosition = new Vector2(0f, y);

            yield return Fade(1f, 0f);
            while (y < endY)
            {
                if (SkipPressed()) { onSkip(); yield break; }
                float speed = CreditScrollSpeed * (AdvanceHeld() ? CreditFastMultiplier : 1f);
                y = Mathf.Min(endY, y + speed * Time.unscaledDeltaTime);
                creditsText.rectTransform.anchoredPosition = new Vector2(0f, y);
                yield return null;
            }

            for (float t = 0f; t < CreditEndHold; t += Time.unscaledDeltaTime)
            {
                if (SkipPressed() || AdvancePressed()) break;
                yield return null;
            }
        }

        private static string BuildCreditsText()
        {
            var sb = new System.Text.StringBuilder();
            string title = ColorUtility.ToHtmlStringRGB(TitleColor);
            foreach (var line in CreditLines)
            {
                if (line.StartsWith("#")) sb.Append($"<size={CreditTitleFontSize}><color=#{title}>{line.Substring(1)}</color></size>");
                else sb.Append(line);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private IEnumerator TypeText(string text, System.Action onSkip)
        {
            float shown = 0f;
            while (shown < text.Length)
            {
                yield return null;
                if (SkipPressed()) { onSkip(); yield break; }
                if (AdvancePressed()) break;
                shown += Time.unscaledDeltaTime * CharsPerSecond;
                epilogueText.text = text.Substring(0, Mathf.Min(text.Length, Mathf.FloorToInt(shown)));
            }
            epilogueText.text = text;
        }

        private void ShowIllustration(string path)
        {
            var texture = string.IsNullOrEmpty(path) ? null : Resources.Load<Texture2D>(path);
            illustration.texture = texture;
            illustration.enabled = texture != null;
            if (texture != null) illustrationFitter.aspectRatio = (float)texture.width / texture.height;
            // 그림이 있으면 아래쪽 반투명 띠에, 없으면 검은 화면 가운데에 문장을 둔다.
            band.enabled = texture != null;
            SetAnchors(band.rectTransform, 0f, texture != null ? 0f : 0.3f, 1f, texture != null ? 0.3f : 0.7f);
        }

        private IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < FadeDuration; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(from, to, t / FadeDuration));
                yield return null;
            }
            SetFade(to);
        }

        private void SetFade(float alpha) => fade.color = new Color(0f, 0f, 0f, alpha);

        private void Finish()
        {
            routine = null;
            panel.SetActive(false);
            illustration.texture = null;
            var finished = onFinished;
            onFinished = null;
            finished?.Invoke();
        }

        private void BuildPanel()
        {
            panel = new GameObject("EndingCredits", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            SetAnchors(rect, 0f, 0f, 1f, 1f);
            panel.GetComponent<Image>().color = Color.black;

            var illustGO = new GameObject("Illustration", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            var illustRect = (RectTransform)illustGO.transform;
            illustRect.SetParent(rect, false);
            SetAnchors(illustRect, 0f, 0f, 1f, 1f);
            illustration = illustGO.GetComponent<RawImage>();
            illustration.raycastTarget = false;
            illustrationFitter = illustGO.GetComponent<AspectRatioFitter>();
            illustrationFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            var bandGO = new GameObject("TextBand", typeof(RectTransform), typeof(Image));
            var bandRect = (RectTransform)bandGO.transform;
            bandRect.SetParent(rect, false);
            band = bandGO.GetComponent<Image>();
            band.color = new Color(0f, 0f, 0f, 0.72f);
            band.raycastTarget = false;
            SetAnchors(bandRect, 0f, 0.3f, 1f, 0.7f);

            epilogueText = BuildText(rect, "Epilogue", EpilogueFontSize, TextAnchor.MiddleCenter);
            // 문장은 띠 안에 둔다. 띠 Image만 꺼도(그림 없는 장) 글자는 그대로 보인다.
            epilogueText.transform.SetParent(bandRect, false);
            SetAnchors(epilogueText.rectTransform, 0.1f, 0.1f, 0.9f, 0.9f);
            epilogueText.lineSpacing = 1.25f;

            var creditsGO = new GameObject("Credits", typeof(RectTransform));
            creditsRoot = (RectTransform)creditsGO.transform;
            creditsRoot.SetParent(rect, false);
            SetAnchors(creditsRoot, 0f, 0f, 1f, 1f);
            creditsText = BuildText(creditsRoot, "CreditsText", CreditFontSize, TextAnchor.UpperCenter);
            var creditsRect = creditsText.rectTransform;
            creditsRect.anchorMin = creditsRect.anchorMax = new Vector2(0.5f, 0.5f);
            creditsRect.pivot = new Vector2(0.5f, 0.5f);
            creditsRect.sizeDelta = new Vector2(900f, 100f);
            creditsText.verticalOverflow = VerticalWrapMode.Overflow;
            creditsText.lineSpacing = 1.4f;
            creditsText.supportRichText = true;

            hintText = BuildText(rect, "Hint", 18, TextAnchor.LowerRight);
            SetAnchors(hintText.rectTransform, 0.5f, 0.02f, 0.98f, 0.08f);
            hintText.color = TitleColor;
            hintText.text = "[Z] 다음   [Esc] 건너뛰기";

            var fadeGO = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            var fadeRect = (RectTransform)fadeGO.transform;
            fadeRect.SetParent(rect, false);
            SetAnchors(fadeRect, 0f, 0f, 1f, 1f);
            fade = fadeGO.GetComponent<Image>();
            fade.raycastTarget = false;
            SetFade(1f);

            panel.SetActive(false);
        }

        private static Text BuildText(Transform parent, string name, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = TextColor;
            text.raycastTarget = false;
            return text;
        }

        private static void SetAnchors(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static bool AdvancePressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
        }

        private static bool AdvanceHeld()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.zKey.isPressed || keyboard.enterKey.isPressed);
        }

        private static bool SkipPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }
    }
}

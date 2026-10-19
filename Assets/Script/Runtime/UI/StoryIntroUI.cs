using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // 새 게임을 시작하면 숲 튜토리얼에 앞서 한 번 나오는 도입 스토리. 장마다 일러스트(UI/Story/...)를 띄우고
    // 문장을 한 글자씩 써 내려간다. 그림 모양에 따라 배치가 바뀐다:
    //   가로 그림 — 화면 가득 깔고 아래쪽 띠에 문장. 세로 그림(Tutorial_Scene1/2) — 왼쪽에 통째로 보여 주고 오른쪽에 문장.
    //   그림 없는 장 — 검은 화면 가운데에 문장만.
    // Z/Enter: 쓰는 중이면 문장을 한 번에 다 보여주고, 다 썼으면 다음 장. Esc: 남은 스토리를 전부 건너뛴다.
    // 그림이 바뀌는 장에서는 화면이 어두워졌다가 다시 밝아지고, 같은 그림이 이어지면 문장만 바뀐다.
    // 시간 정지와 무관하게 돌도록 전부 실시간으로 잰다.
    public class StoryIntroUI : MonoBehaviour
    {
        private readonly struct StoryPage
        {
            public readonly string Illustration;
            public readonly string Text;
            public StoryPage(string illustration, string text) { Illustration = illustration; Text = text; }
        }

        // 문장은 줄바꿈(\n)으로 나눠 쓴다. 일러스트 경로는 Resources 기준(확장자 없이), null이면 그림 없는 장.
        // scene_1 = 쓰러뜨린 적들 위 왕좌의 네크로시아(옛 영광), scene_2 = 루나벨 앞에 무릎 꿇은 네크로시아(패배),
        // scene_3 = 사슬에 묶인 수정 관 속에서 잠든 네크로시아(긴 잠).
        private const string GloryScene = "UI/Story/scene_1";
        private const string DefeatScene = "UI/Story/scene_2";
        private const string SleepScene = "UI/Story/scene_3";

        private static readonly StoryPage[] Pages =
        {
            new StoryPage(GloryScene, "먼 옛날, 이 땅의 밤을 지배하던 사신이 있었다.\n영혼을 거두는 낫의 주인, 네크로시아."),
            new StoryPage(GloryScene, "적이 휘두르는 모든 공격에는 리듬이 있었고,\n네크로시아는 그 리듬을 읽을 수 있었다.\n어떤 칼날도 그 박자를 벗어나지 못했다."),
            new StoryPage(DefeatScene, "그러나 붉은 달이 뜬 밤, 피의 여왕 루나벨이 나타났다.\n그리고 처음으로... 리듬이 무너졌다."),
            new StoryPage(SleepScene, "패배한 사신은 모든 것을 잃었다.\n힘도, 이름도, 영광도.\n그리고 깊은 어둠 속에서 긴 잠에 빠졌다."),
            new StoryPage(SleepScene, "그로부터, 오랜 세월이 흘렀다."),
            new StoryPage(null, "어둠의 숲 깊은 곳.\n잊혀진 사신이, 다시 눈을 뜬다."),
        };

        // 가로세로 비율이 이보다 작으면(세로로 길면) 왼쪽 그림 + 오른쪽 문장 배치를 쓴다.
        private const float PortraitAspectLimit = 1.2f;
        private const int DefaultFontSize = 28;
        private const int PortraitFontSize = 25;

        private const float CharsPerSecond = 28f;
        private const float FadeDuration = 0.45f;
        private const float FirstPageDelay = 0.4f;

        private GameObject panel;
        private RawImage illustration;
        private RectTransform illustrationArea;
        private AspectRatioFitter illustrationFitter;
        private RectTransform bandRect;
        private Image bandImage;
        private Text storyText;
        private Text hintText;
        private Image fade;
        private System.Action onFinished;
        private Coroutine routine;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize()
        {
            BuildPanel();
        }

        private void BuildPanel()
        {
            panel = new GameObject("StoryIntro", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            Stretch(rect);
            panel.GetComponent<Image>().color = Color.black;

            // 그림이 놓일 칸(배치마다 위치가 바뀐다). 그림은 이 칸 안에서 비율을 지키며 맞춰진다(AspectRatioFitter).
            var areaGO = new GameObject("IllustrationArea", typeof(RectTransform));
            illustrationArea = (RectTransform)areaGO.transform;
            illustrationArea.SetParent(rect, false);
            Stretch(illustrationArea);

            var illustGO = new GameObject("Illustration", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            var illustRect = (RectTransform)illustGO.transform;
            illustRect.SetParent(illustrationArea, false);
            Stretch(illustRect);
            illustration = illustGO.GetComponent<RawImage>();
            illustration.raycastTarget = false;
            illustrationFitter = illustGO.GetComponent<AspectRatioFitter>();
            illustrationFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            // 문장 띠: 단색 반투명 판. 위치는 배치마다 바뀐다(ApplyLayout).
            var bandGO = new GameObject("TextBand", typeof(RectTransform), typeof(Image));
            bandRect = (RectTransform)bandGO.transform;
            bandRect.SetParent(rect, false);
            bandRect.offsetMin = Vector2.zero;
            bandRect.offsetMax = Vector2.zero;
            bandImage = bandGO.GetComponent<Image>();
            bandImage.raycastTarget = false;

            storyText = BuildText(bandRect, "Text", new Vector2(0.1f, 0.18f), new Vector2(0.9f, 0.95f), DefaultFontSize, new Color(0.93f, 0.9f, 0.97f), TextAnchor.MiddleCenter);
            storyText.lineSpacing = 1.25f;
            hintText = BuildText(bandRect, "Hint", new Vector2(0.6f, 0f), new Vector2(0.98f, 0.2f), 18, new Color(0.73f, 0.55f, 0.91f), TextAnchor.MiddleRight);
            hintText.text = "[Z] 다음   [Esc] 건너뛰기";

            var fadeGO = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            var fadeRect = (RectTransform)fadeGO.transform;
            fadeRect.SetParent(rect, false);
            Stretch(fadeRect);
            fade = fadeGO.GetComponent<Image>();
            fade.color = Color.black;
            fade.raycastTarget = false;

            panel.SetActive(false);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text BuildText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        public void Play(System.Action finished = null)
        {
            onFinished = finished;
            panel.SetActive(true);
            // 다른 HUD 요소보다 위에 그려지게 맨 뒤 자식으로 옮긴다.
            panel.transform.SetAsLastSibling();
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            SetFade(1f);
            storyText.text = "";
            yield return new WaitForSecondsRealtime(FirstPageDelay);

            for (int i = 0; i < Pages.Length; i++)
            {
                // 같은 그림이 이어지는 장은 화면을 다시 어둡게 하지 않고 문장만 바꾼다.
                bool sameScene = i > 0 && Pages[i].Illustration == Pages[i - 1].Illustration;
                if (!sameScene) ShowIllustration(Pages[i].Illustration);
                storyText.text = "";
                if (!sameScene) yield return Fade(1f, 0f);
                if (SkipPressed()) break;

                bool skipped = false;
                yield return TypeText(Pages[i].Text, () => skipped = true);
                if (skipped) break;

                // 다 쓴 문장을 넘기는 입력. 문장을 한 번에 띄운 그 프레임의 입력으로 바로 넘어가지 않게 한 프레임 쉰다.
                yield return null;
                while (!AdvancePressed())
                {
                    if (SkipPressed())
                    {
                        skipped = true;
                        break;
                    }
                    yield return null;
                }
                if (skipped) break;

                bool nextSameScene = i + 1 < Pages.Length && Pages[i + 1].Illustration == Pages[i].Illustration;
                if (!nextSameScene) yield return Fade(0f, 1f);
            }

            Finish();
        }

        private IEnumerator TypeText(string text, System.Action onSkip)
        {
            float shown = 0f;
            while (shown < text.Length)
            {
                yield return null;
                if (SkipPressed())
                {
                    onSkip();
                    yield break;
                }
                if (AdvancePressed())
                {
                    break;
                }
                shown += Time.unscaledDeltaTime * CharsPerSecond;
                storyText.text = text.Substring(0, Mathf.Min(text.Length, Mathf.FloorToInt(shown)));
            }
            storyText.text = text;
        }

        private void ShowIllustration(string path)
        {
            var texture = string.IsNullOrEmpty(path) ? null : Resources.Load<Texture2D>(path);
            illustration.texture = texture;
            illustration.enabled = texture != null;
            if (texture == null)
            {
                ApplyLayout(Layout.TextOnly);
                return;
            }
            float aspect = (float)texture.width / texture.height;
            illustrationFitter.aspectRatio = aspect;
            ApplyLayout(aspect < PortraitAspectLimit ? Layout.Portrait : Layout.Wide);
        }

        private enum Layout { Wide, Portrait, TextOnly }

        private void ApplyLayout(Layout layout)
        {
            switch (layout)
            {
                case Layout.Wide:
                    // 화면 가득 + 아래쪽 띠.
                    SetAnchors(illustrationArea, 0f, 0f, 1f, 1f);
                    illustrationFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                    SetAnchors(bandRect, 0f, 0f, 1f, 0.3f);
                    bandImage.color = new Color(0f, 0f, 0f, 0.72f);
                    break;
                case Layout.Portrait:
                    // 왼쪽에 세로 그림을 통째로, 오른쪽에 문장.
                    SetAnchors(illustrationArea, 0.03f, 0.03f, 0.5f, 0.97f);
                    illustrationFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    SetAnchors(bandRect, 0.5f, 0.25f, 0.99f, 0.75f);
                    bandImage.color = new Color(0f, 0f, 0f, 0f);
                    SetAnchors(storyText.rectTransform, 0.02f, 0.18f, 0.98f, 0.95f);
                    storyText.fontSize = PortraitFontSize;
                    return;
                default:
                    // 검은 화면 가운데에 문장만.
                    SetAnchors(bandRect, 0.1f, 0.3f, 0.9f, 0.7f);
                    bandImage.color = new Color(0f, 0f, 0f, 0f);
                    break;
            }
            SetAnchors(storyText.rectTransform, 0.1f, 0.18f, 0.9f, 0.95f);
            storyText.fontSize = DefaultFontSize;
        }

        private static void SetAnchors(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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

        private void SetFade(float alpha)
        {
            fade.color = new Color(0f, 0f, 0f, alpha);
        }

        private void Finish()
        {
            routine = null;
            panel.SetActive(false);
            illustration.texture = null;
            var finished = onFinished;
            onFinished = null;
            finished?.Invoke();
        }

        private static bool AdvancePressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
        }

        private static bool SkipPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // 보스 등장 컷: 화면 위아래에서 검은 띠(레터박스)가 밀려 들어오고, 아래쪽에 보스 이름이 큼직하게 떠올랐다가 사라진다.
    // 보스 체력바가 연결되는 순간(BossHealthUI.Bind) 띄운다. 실제 시간으로 재므로 멈춘 화면에서도 그대로 재생된다.
    public class BossTitleCard : MonoBehaviour
    {
        private const float BarHeight = 0.11f;   // 화면 높이 대비
        private const float SlideTime = 0.35f;
        private const float HoldTime = 2.1f;

        private RectTransform top, bottom;
        private Text nameText, subText;
        private Image lineImage;
        private CanvasGroup textGroup;
        private Coroutine routine;
        private static BossTitleCard current;

        // 이름 자막이 떠 있는 동안 true. 보스 등장 대사는 이게 끝난 뒤에 시작한다(겹치지 않게).
        public static bool IsShowing => current != null && current.routine != null;

        // 이름 자막이 다 끝날 때까지 기다린다. skip이 true를 돌려주면(Esc 등) 기다리지 않고 바로 넘어간다.
        public static IEnumerator WaitUntilDone(System.Func<bool> skip = null)
        {
            while (IsShowing)
            {
                if (skip != null && skip()) yield break;
                yield return null;
            }
        }

        public static BossTitleCard Create(Transform canvas)
        {
            var go = new GameObject("BossTitleCard", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var card = go.AddComponent<BossTitleCard>();
            current = card;
            card.Build(rect);
            go.SetActive(false);
            return card;
        }

        public void Show(string bossName, string subtitle = "BOSS")
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            nameText.text = bossName;
            subText.text = subtitle;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            SetBars(0f);
            textGroup.alpha = 0f;
            float t = 0f;
            while (t < SlideTime) { t += Time.unscaledDeltaTime; SetBars(Ease(t / SlideTime)); yield return null; }
            SetBars(1f);

            t = 0f;
            var nameRect = nameText.rectTransform;
            Vector2 basePos = nameRect.anchoredPosition;
            while (t < HoldTime)
            {
                t += Time.unscaledDeltaTime;
                float fadeIn = Mathf.Clamp01(t / 0.4f);
                float fadeOut = Mathf.Clamp01((HoldTime - t) / 0.4f);
                textGroup.alpha = Mathf.Min(fadeIn, fadeOut);
                // 이름이 아래에서 살짝 올라오고, 밑줄이 가운데서 양옆으로 펼쳐진다.
                nameRect.anchoredPosition = basePos + new Vector2(0f, -12f * (1f - Ease(fadeIn)));
                lineImage.rectTransform.localScale = new Vector3(Ease(Mathf.Clamp01(t / 0.6f)), 1f, 1f);
                yield return null;
            }
            nameRect.anchoredPosition = basePos;
            textGroup.alpha = 0f;

            t = 0f;
            while (t < SlideTime) { t += Time.unscaledDeltaTime; SetBars(1f - Ease(t / SlideTime)); yield return null; }
            SetBars(0f);
            gameObject.SetActive(false);
            routine = null;
        }

        private static float Ease(float x) => 1f - (1f - Mathf.Clamp01(x)) * (1f - Mathf.Clamp01(x));

        private void SetBars(float k)
        {
            float h = BarHeight * k;
            top.anchorMin = new Vector2(0f, 1f - h);
            bottom.anchorMax = new Vector2(1f, h);
        }

        private void Build(RectTransform root)
        {
            top = MakeBar(root, "LetterboxTop", new Vector2(0f, 1f), new Vector2(1f, 1f));
            bottom = MakeBar(root, "LetterboxBottom", new Vector2(0f, 0f), new Vector2(1f, 0f));

            var textRoot = new GameObject("Title", typeof(RectTransform), typeof(CanvasGroup));
            var textRect = (RectTransform)textRoot.transform;
            textRect.SetParent(root, false);
            textRect.anchorMin = new Vector2(0f, 0.16f);
            textRect.anchorMax = new Vector2(1f, 0.36f);
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            textGroup = textRoot.GetComponent<CanvasGroup>();
            textGroup.blocksRaycasts = false;
            textGroup.interactable = false;

            subText = MakeText(textRect, "Sub", 22, new Color(1f, 0.45f, 0.4f), new Vector2(0f, 0.68f), new Vector2(1f, 1f));
            nameText = MakeText(textRect, "Name", 52, new Color(1f, 0.9f, 0.7f), new Vector2(0f, 0.12f), new Vector2(1f, 0.72f));
            nameText.fontStyle = FontStyle.Bold;
            var outline = nameText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            var line = new GameObject("Underline", typeof(RectTransform), typeof(Image));
            var lineRect = (RectTransform)line.transform;
            lineRect.SetParent(textRect, false);
            lineRect.anchorMin = new Vector2(0.3f, 0.08f);
            lineRect.anchorMax = new Vector2(0.7f, 0.08f);
            lineRect.sizeDelta = new Vector2(0f, 2f);
            lineImage = line.GetComponent<Image>();
            lineImage.color = new Color(1f, 0.75f, 0.45f, 0.85f);
            lineImage.raycastTarget = false;
        }

        private static RectTransform MakeBar(RectTransform root, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            return rect;
        }

        private static Text MakeText(RectTransform parent, string name, int size, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }
    }
}

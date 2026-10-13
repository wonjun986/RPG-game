using UnityEngine;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // NPC 근처에 가면 뜨는 짧은 대사창. 대화 프레임 이미지(예: UI/Noa_Talk) 위에 대사 텍스트를 얹어서 보여준다.
    public class NpcDialogueUI : MonoBehaviour
    {
        private const float ImageWidth = 2172f;
        private const float ImageHeight = 724f;
        private const float BoxWidth = 1000f;
        private const float BoxHeight = BoxWidth * ImageHeight / ImageWidth; // 대화 프레임 원본 비율에 맞춤
        public const float DefaultTextLeft = 0.32f;
        private const float PlainTextLeft = 0.07f;
        private const float DefaultTextTop = 0.53f;
        // 이름판 기본 좌표(엘리/노아 등 Elly_Talk 계열 프레임 기준, 그림 x 620~990, y 318~372).
        // 프레임마다 이름판 위치가 달라서(예: 이리아는 훨씬 위쪽) Show()에서 NPC별로 다시 줄 수 있다.
        private const float DefaultPlateX0 = 620f, DefaultPlateY0 = 318f, DefaultPlateX1 = 990f, DefaultPlateY1 = 372f;

        private GameObject box;
        private RawImage frameImage;
        private Image plainBox;
        private Text nameText;
        private RectTransform nameRect;
        private Text dialogueText;
        private RectTransform textRect;
        private string currentResourcePath;

        public void Initialize()
        {
            box = new GameObject("NpcDialogueBox", typeof(RectTransform));
            var rect = (RectTransform)box.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(BoxWidth, BoxHeight);
            rect.anchoredPosition = new Vector2(0f, 30f);

            var frameGO = new GameObject("Frame", typeof(RectTransform), typeof(RawImage));
            var frameRect = (RectTransform)frameGO.transform;
            frameRect.SetParent(rect, false);
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            frameImage = frameGO.GetComponent<RawImage>();

            // 전용 대화 프레임 그림이 아직 없는 NPC(예: 엘리)용 기본 대사 칸. 프레임 그림 속 대사 칸과 같은 자리에
            // 어두운 판을 깐다. 프레임 그림이 있으면 꺼 둔다.
            var plainGO = new GameObject("PlainBox", typeof(RectTransform), typeof(Image), typeof(Outline));
            var plainRect = (RectTransform)plainGO.transform;
            plainRect.SetParent(rect, false);
            plainRect.anchorMin = new Vector2(0.03f, 0.16f);
            plainRect.anchorMax = new Vector2(0.97f, 0.59f);
            plainRect.offsetMin = Vector2.zero;
            plainRect.offsetMax = Vector2.zero;
            plainBox = plainGO.GetComponent<Image>();
            plainBox.color = new Color(0.07f, 0.05f, 0.1f, 0.94f);
            var plainOutline = plainGO.GetComponent<Outline>();
            plainOutline.effectColor = new Color(0.62f, 0.48f, 0.3f, 0.9f);
            plainOutline.effectDistance = new Vector2(2f, -2f);
            plainGO.SetActive(false);

            // 대화 프레임 그림 속 비어있는 대사 영역(초상화 오른쪽의 어두운 직사각형)에 맞춘 좌표.
            var textGO = new GameObject("DialogueText", typeof(RectTransform), typeof(Text));
            textRect = (RectTransform)textGO.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = new Vector2(DefaultTextLeft, 0.22f);
            textRect.anchorMax = new Vector2(0.92f, 0.53f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            dialogueText = textGO.GetComponent<Text>();
            dialogueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dialogueText.fontSize = 22;
            dialogueText.color = Color.white;
            dialogueText.alignment = TextAnchor.MiddleLeft;
            dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            // 세로는 Overflow로 두면 대사가 길 때 칸 아래로 그냥 삐져나왔다. Best Fit으로 칸 높이에
            // 맞춰 글자 크기를 자동으로 줄이고, 그래도 넘치면(극단적으로 긴 대사) 잘라내 안전망을 둔다.
            dialogueText.verticalOverflow = VerticalWrapMode.Truncate;
            dialogueText.resizeTextForBestFit = true;
            dialogueText.resizeTextMinSize = 12;
            dialogueText.resizeTextMaxSize = 22;

            // 이름판이 있는 프레임(예: UI/Elly_Talk, 대사 칸 왼쪽 위의 작은 판)에 말하는 사람 이름을 쓴다.
            // 기본 좌표는 Elly_Talk 기준(그림 x 620~990, y 318~372)이고, Show()에서 프레임마다 다시 줄 수 있다.
            var nameGO = new GameObject("SpeakerName", typeof(RectTransform), typeof(Text));
            nameRect = (RectTransform)nameGO.transform;
            nameRect.SetParent(rect, false);
            nameRect.anchorMin = new Vector2(DefaultPlateX0 / ImageWidth, 1f - DefaultPlateY1 / ImageHeight);
            nameRect.anchorMax = new Vector2(DefaultPlateX1 / ImageWidth, 1f - DefaultPlateY0 / ImageHeight);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            nameText = nameGO.GetComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 20;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = new Color(0.91f, 0.72f, 0.42f);
            nameText.alignment = TextAnchor.MiddleCenter;
            nameGO.SetActive(false);

            box.SetActive(false);
        }

        // textLeft: 대사가 시작되는 가로 위치(프레임 폭 비율). 초상화가 대사 칸 쪽으로 더 튀어나온 프레임은 크게 준다.
        // resourcePath의 그림이 없으면 기본 대사 칸(어두운 판)에 왼쪽부터 대사를 쓴다.
        // speakerName: 이름판이 있는 프레임이면 그 안에 쓸 이름. 이름판과 겹치지 않게 대사 칸 위쪽을 조금 낮춘다.
        // namePlateX0~Y1: 그 이름판이 그림(2172x724) 속에서 차지하는 픽셀 사각형(왼쪽위 기준). 프레임마다
        // 위치가 달라서(이리아는 엘리보다 훨씬 위쪽) 안 주면 기본값(Elly_Talk 기준)을 쓴다.
        // textColor: 대사 글자색. 기본은 흰색(어두운 프레임용)이고, 이리아처럼 대사 칸 배경이 밝은
        // 프레임은 검은 계열로 따로 줘야 글자가 보인다.
        public void Show(string resourcePath, string line, float textLeft = DefaultTextLeft, string speakerName = null,
            float namePlateX0 = DefaultPlateX0, float namePlateY0 = DefaultPlateY0,
            float namePlateX1 = DefaultPlateX1, float namePlateY1 = DefaultPlateY1,
            Color? textColor = null)
        {
            if (currentResourcePath != resourcePath)
            {
                frameImage.texture = Resources.Load<Texture2D>(resourcePath);
                currentResourcePath = resourcePath;
            }
            bool hasFrame = frameImage.texture != null;
            frameImage.enabled = hasFrame;
            plainBox.gameObject.SetActive(!hasFrame);
            textRect.anchorMin = new Vector2(hasFrame ? textLeft : PlainTextLeft, textRect.anchorMin.y);

            bool hasName = hasFrame && !string.IsNullOrEmpty(speakerName);
            nameText.text = hasName ? speakerName : "";
            nameText.gameObject.SetActive(hasName);
            float plateTopAnchor = 1f - namePlateY1 / ImageHeight;
            nameRect.anchorMin = new Vector2(namePlateX0 / ImageWidth, plateTopAnchor);
            nameRect.anchorMax = new Vector2(namePlateX1 / ImageWidth, 1f - namePlateY0 / ImageHeight);
            textRect.anchorMax = new Vector2(textRect.anchorMax.x, hasName ? plateTopAnchor : DefaultTextTop);
            dialogueText.text = line;
            dialogueText.color = textColor ?? Color.white;
            box.SetActive(true);
        }

        public void Hide()
        {
            box.SetActive(false);
            currentResourcePath = null;
        }
    }
}

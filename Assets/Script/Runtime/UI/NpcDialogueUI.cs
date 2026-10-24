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
        public const float DefaultTextTop = 0.53f;
        public const float DefaultTextBottom = 0.22f;
        public const float DefaultTextRight = 0.92f;
        // 이름판 기본 좌표(엘리/노아 등 Elly_Talk 계열 프레임 기준, 그림 x 620~990, y 318~372).
        // 프레임마다 이름판 위치가 달라서(예: 이리아는 훨씬 위쪽) Show()에서 NPC별로 다시 줄 수 있다.
        private const float DefaultPlateX0 = 620f, DefaultPlateY0 = 318f, DefaultPlateX1 = 990f, DefaultPlateY1 = 372f;

        // 네크로시아 대화창(UI/Player_Talk). 원본(Sprite/UI/Player_Talk, 2171x724)은 캔버스를 꽉 채워서 루나벨 창보다 훨씬 커 보였다.
        // 루나벨 창처럼 2172x724 투명 캔버스 안에 0.8배로 줄여 (217, 170)에 넣어 두었다. 어두운 대사 칸 안쪽에서
        // 오른쪽 나침반 문양 앞까지(x 729~1537, y 462~602)에 글자를 쓴다. 이름판이 없는 그림이라 이름은 대사 첫 줄에 붙인다.
        private const string PlayerTalkFrame = "UI/Player_Talk";
        private const float PlayerTextLeft = 729f / ImageWidth;
        private const float PlayerTextRight = 1537f / ImageWidth;
        private const float PlayerTextTop = 1f - 462f / ImageHeight;
        private const float PlayerTextBottom = 1f - 602f / ImageHeight;
        private const string PlayerNameTag = "<b><color=#C9A6F0>네크로시아</color></b>\n";

        // 루나벨 대화창(UI/Lunabel_Talk, 원본 1848x851을 좌우 투명 여백으로 3:1에 맞춘 2172x724).
        // 이름판 안쪽은 x 810~1070, y 405~465이고, 어두운 대사 칸은 이름판 아래 y 465~640, x 800~1770이다.
        // 글자는 테두리에 붙지 않게 안쪽으로 조금 띄운다(x 820~1750, y 468~632).
        private const string LunabelTalkFrame = "UI/Lunabel_Talk";
        private const float LunabelTextLeft = 820f / ImageWidth;
        private const float LunabelTextRight = 1750f / ImageWidth;
        private const float LunabelTextBottom = 1f - 632f / ImageHeight;
        private const float LunabelPlateX0 = 810f, LunabelPlateY0 = 405f, LunabelPlateX1 = 1070f, LunabelPlateY1 = 465f;

        // 대화창 그림마다 캔버스(2172x724) 안에서 어두운 대사 칸이 차지하는 크기/자리가 달라서(노아·이리아처럼 그림을 꽉
        // 채운 것도, 네크로시아처럼 작게 그린 것도 있다) 그대로 그리면 말하는 사람이 바뀔 때마다 창 크기가 들쑥날쑥했다.
        // 그림별로 대사 칸의 왼쪽/오른쪽/아랫변(그림 픽셀)을 적어 두고, 창을 열 때 대사 칸이 기준 칸(네크로시아 창)과
        // 같은 폭·같은 자리에 오도록 창 전체의 크기와 위치를 맞춘다. 대사/이름판 좌표는 창 기준 비율이라 그대로 따라온다.
        private readonly struct FrameBox
        {
            public readonly float Left, Right, Bottom;
            public FrameBox(float left, float right, float bottom) { Left = left; Right = right; Bottom = bottom; }
        }

        private static readonly FrameBox ReferenceBox = new FrameBox(709f, 1839f, 629f); // UI/Player_Talk
        private static readonly System.Collections.Generic.Dictionary<string, FrameBox> FrameBoxes =
            new System.Collections.Generic.Dictionary<string, FrameBox>
            {
                { "UI/Player_Talk", ReferenceBox },
                { "UI/Lunabel_Talk", new FrameBox(636f, 1795f, 679f) },
                { "UI/Noa_Talk", new FrameBox(570f, 1991f, 594f) },
                { "UI/Elly_Talk", new FrameBox(670f, 2003f, 592f) },
                { "UI/Isis_Talk", new FrameBox(754f, 2035f, 632f) },
                { "UI/Iria_Talk", new FrameBox(498f, 2052f, 545f) },
                { "UI/BossKnight_Talk", new FrameBox(557f, 1940f, 677f) },
                { "UI/Lily_Talk", new FrameBox(815f, 2040f, 546f) },
            };
        private const float BoxBottomMargin = 30f;

        private GameObject box;
        private RectTransform boxRect;
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
            rect.anchoredPosition = new Vector2(0f, BoxBottomMargin);
            boxRect = rect;

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
            textRect.anchorMin = new Vector2(DefaultTextLeft, DefaultTextBottom);
            textRect.anchorMax = new Vector2(DefaultTextRight, DefaultTextTop);
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
        // namedTextTop: 이름판이 있을 때 대사 칸 위쪽 끝(프레임 비율). 안 주면 이름판 윗변에 맞춘다. 릴리처럼 이름판이
        // 대사 칸 위 테두리에 따로 붙어 있는 프레임은 대사가 테두리 장식까지 올라가지 않게 직접 준다.
        // textBottom/textRight: 대사 칸 아래쪽/오른쪽 끝(프레임 비율). 보스 기사처럼 대사 칸이 더 낮거나
        // 오른쪽에 문양이 그려진 프레임에서 따로 준다.
        public void Show(string resourcePath, string line, float textLeft = DefaultTextLeft, string speakerName = null,
            float namePlateX0 = DefaultPlateX0, float namePlateY0 = DefaultPlateY0,
            float namePlateX1 = DefaultPlateX1, float namePlateY1 = DefaultPlateY1,
            Color? textColor = null, float textBottom = DefaultTextBottom, float textRight = DefaultTextRight,
            float textTop = DefaultTextTop, float? namedTextTop = null)
        {
            if (currentResourcePath != resourcePath)
            {
                frameImage.texture = Resources.Load<Texture2D>(resourcePath);
                currentResourcePath = resourcePath;
            }
            bool hasFrame = frameImage.texture != null;
            frameImage.enabled = hasFrame;
            FitBoxToFrame(hasFrame ? resourcePath : null);
            plainBox.gameObject.SetActive(!hasFrame);
            textRect.anchorMin = new Vector2(hasFrame ? textLeft : PlainTextLeft, hasFrame ? textBottom : DefaultTextBottom);

            bool hasName = hasFrame && !string.IsNullOrEmpty(speakerName);
            nameText.text = hasName ? speakerName : "";
            nameText.gameObject.SetActive(hasName);
            float plateTopAnchor = 1f - namePlateY1 / ImageHeight;
            nameRect.anchorMin = new Vector2(namePlateX0 / ImageWidth, plateTopAnchor);
            nameRect.anchorMax = new Vector2(namePlateX1 / ImageWidth, 1f - namePlateY0 / ImageHeight);
            textRect.anchorMax = new Vector2(hasFrame ? textRight : DefaultTextRight, hasName ? namedTextTop ?? plateTopAnchor : hasFrame ? textTop : DefaultTextTop);
            dialogueText.text = line;
            dialogueText.color = textColor ?? Color.white;
            box.SetActive(true);
        }

        // 그림의 대사 칸이 기준 칸(ReferenceBox, 기본 크기 창 기준)과 같은 폭/왼쪽 끝/아랫변에 오도록 창 크기와 위치를 맞춘다.
        // 표에 없는 그림(또는 그림 없는 기본 판)은 기본 크기/자리 그대로 둔다.
        private void FitBoxToFrame(string resourcePath)
        {
            if (resourcePath == null || !FrameBoxes.TryGetValue(resourcePath, out var frameBox))
            {
                boxRect.sizeDelta = new Vector2(BoxWidth, BoxHeight);
                boxRect.anchoredPosition = new Vector2(0f, BoxBottomMargin);
                return;
            }

            float scale = (ReferenceBox.Right - ReferenceBox.Left) / (frameBox.Right - frameBox.Left);
            float width = BoxWidth * scale;
            float height = BoxHeight * scale;
            // 기준 창에서 대사 칸 왼쪽 끝/아랫변이 놓이는 화면 위치(피벗: 창 아래 가운데).
            float targetLeft = -BoxWidth / 2f + ReferenceBox.Left / ImageWidth * BoxWidth;
            float targetBottom = BoxBottomMargin + (1f - ReferenceBox.Bottom / ImageHeight) * BoxHeight;
            float x = targetLeft + width / 2f - frameBox.Left / ImageWidth * width;
            float y = targetBottom - (1f - frameBox.Bottom / ImageHeight) * height;
            boxRect.sizeDelta = new Vector2(width, height);
            boxRect.anchoredPosition = new Vector2(x, y);
        }

        // 네크로시아(플레이어) 대사. 이름을 첫 줄에 붙여 네크로시아 대화창에 띄운다.
        public void ShowNecrosia(string line)
        {
            Show(PlayerTalkFrame, PlayerNameTag + line, PlayerTextLeft,
                textBottom: PlayerTextBottom, textRight: PlayerTextRight, textTop: PlayerTextTop);
        }

        // 루나벨 대사. 이름은 이름판 안에 쓴다.
        public void ShowLunabel(string line)
        {
            Show(LunabelTalkFrame, line, LunabelTextLeft, "루나벨",
                LunabelPlateX0, LunabelPlateY0, LunabelPlateX1, LunabelPlateY1, null, LunabelTextBottom, LunabelTextRight);
        }

        // 같은 캔버스에서 나중에 켜진 창(리듬 전투 화면 등)보다 위에 대화창을 그린다.
        public void BringToFront()
        {
            box.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            box.SetActive(false);
            currentResourcePath = null;
        }
    }
}

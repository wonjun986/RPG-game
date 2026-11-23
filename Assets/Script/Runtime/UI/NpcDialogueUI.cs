using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // NPC 근처에 가면 뜨는 짧은 대사창. 대화 프레임 이미지(예: UI/Noa_Talk) 위에 대사 텍스트를 얹어서 보여준다.
    // 대사는 스토리처럼 한 글자씩 타이핑되고, 글자 크기를 고정한 채 대사 칸에 다 안 들어가면 여러 장으로 나눠
    // [Z]로 넘긴다. 대사를 넘기는 쪽은 Z를 직접 읽지 말고 AdvancePressed로 물어야 한다 — 타이핑 중이면 먼저
    // 그 장을 다 보여 주고, 남은 장이 있으면 다음 장으로 넘긴 뒤 false를 돌려준다(마지막 장을 다 본 뒤에만 true).
    public class NpcDialogueUI : MonoBehaviour
    {
        private const float CharsPerSecond = 32f;   // 스토리(28)보다 조금 빠르게
        private const int FontSize = 18;
        private const string ContinueHint = "  <color=#B98CE8>▼</color>";

        // 이름판이 없는 대화창은 이름을 대사 첫 줄에 붙인다. Show에서 이름을 따로 주지 않으면 그림에 맞는 이름을 쓴다.
        private static readonly Dictionary<string, string> FrameNameTags = new Dictionary<string, string>
        {
            { "UI/Talk/Noa_Talk", "<b><color=#E8C47A>노아</color></b>\n" },
            { "UI/Talk/Isis_Talk", "<b><color=#D8C6F5>이시스</color></b>\n" },
        };

        private static NpcDialogueUI instance;
        private static int consumedFrame = -1;
        // 새 대사를 띄운 프레임. 그 대사를 띄우게 한 Z를 같은 프레임의 다른 NPC(엘리·이시스처럼 매 프레임 Z를 묻는 쪽)가
        // 다시 물으면 타이핑이 곧바로 끝나 버려서(노아 대사가 한 번에 다 보였다), 그 프레임의 Z는 대사 넘기기에 쓰지 않는다.
        private int shownFrame = -1;
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
        private const string PlayerTalkFrame = "UI/Talk/Player_Talk";
        private const float PlayerTextLeft = 729f / ImageWidth;
        private const float PlayerTextRight = 1537f / ImageWidth;
        private const float PlayerTextTop = 1f - 462f / ImageHeight;
        private const float PlayerTextBottom = 1f - 602f / ImageHeight;
        private const string PlayerNameTag = "<b><color=#C9A6F0>네크로시아</color></b>\n";

        // 루나벨 대화창(UI/Lunabel_Talk, 원본 1848x851을 좌우 투명 여백으로 3:1에 맞춘 2172x724).
        // 이름판 안쪽은 x 810~1070, y 405~465이고, 어두운 대사 칸은 이름판 아래 y 465~640, x 800~1770이다.
        // 글자는 테두리에 붙지 않게 안쪽으로 조금 띄운다(x 820~1750, y 468~632).
        private const string LunabelTalkFrame = "UI/Talk/Lunabel_Talk";
        private const float LunabelTextLeft = 820f / ImageWidth;
        private const float LunabelTextRight = 1750f / ImageWidth;
        private const float LunabelTextBottom = 1f - 632f / ImageHeight;
        private const float LunabelPlateX0 = 810f, LunabelPlateY0 = 405f, LunabelPlateX1 = 1070f, LunabelPlateY1 = 465f;

        // 아쿠아 대화창(UI/Aqua_Talk, 2172x724). 어두운 대사 칸은 x 942~2020, y 342~585이고, 글자는 테두리에 붙지 않게
        // 안쪽(x 970~1980, y 370~560)에 쓴다. 이름판이 없는 그림이라 네크로시아 창처럼 이름은 대사 첫 줄에 붙인다.
        private const string AquaTalkFrame = "UI/Talk/Aqua_Talk";
        private const float AquaTextLeft = 970f / ImageWidth;
        private const float AquaTextRight = 1980f / ImageWidth;
        private const float AquaTextTop = 1f - 370f / ImageHeight;
        private const float AquaTextBottom = 1f - 560f / ImageHeight;
        private const string AquaNameTag = "<b><color=#8FD8FF>아쿠아</color></b>\n";

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
                { "UI/Talk/Player_Talk", ReferenceBox },
                { "UI/Talk/Lunabel_Talk", new FrameBox(636f, 1795f, 679f) },
                { "UI/Talk/Noa_Talk", new FrameBox(570f, 1991f, 594f) },
                { "UI/Talk/Elly_Talk", new FrameBox(670f, 2003f, 592f) },
                { "UI/Talk/Isis_Talk", new FrameBox(754f, 2035f, 632f) },
                { "UI/Talk/Iria_Talk", new FrameBox(498f, 2052f, 545f) },
                { "UI/Talk/BossKnight_Talk", new FrameBox(557f, 1940f, 677f) },
                { "UI/Talk/Lily_Talk", new FrameBox(815f, 2040f, 546f) },
                { "UI/Talk/Aqua_Talk", new FrameBox(942f, 2020f, 585f) },
            };
        // 그림마다 실제 어두운 대사 칸 안쪽(테두리·초상화·문양에 닿지 않는 자리)과 이름판 안쪽을 그림 픽셀(2172x724, 왼쪽 위 기준)로
        // 재 둔 표. 예전엔 부르는 쪽(NPC·컷신)마다 좌표를 따로 넘겼는데, 칸보다 조금씩 크게 잡힌 곳이 많아서 세 줄짜리 긴 대사가
        // 테두리 밖으로 나가거나(이리아·엘리·아쿠아) 이름이 이름판 위 테두리에 걸렸다(이리아). 표에 있는 그림은 넘겨받은 좌표 대신 이 값을 쓴다.
        private readonly struct FrameLayout
        {
            public readonly Rect Text;      // xMin, yMin(위), xMax, yMax(아래) — 그림 픽셀
            public readonly Rect? Plate;    // 이름판 안쪽. 없으면 넘겨받은 이름판 좌표를 쓴다
            public readonly Color? NameColor;
            public FrameLayout(float l, float t, float r, float b, Rect? plate = null, Color? nameColor = null)
            {
                Text = Rect.MinMaxRect(l, t, r, b);
                Plate = plate;
                NameColor = nameColor;
            }
        }

        private static Rect Px(float l, float t, float r, float b) => Rect.MinMaxRect(l, t, r, b);
        private static readonly Color DefaultNameColor = new Color(0.91f, 0.72f, 0.42f);

        private static readonly Dictionary<string, FrameLayout> FrameLayouts = new Dictionary<string, FrameLayout>
        {
            { "UI/Talk/Player_Talk", new FrameLayout(729f, 462f, 1537f, 600f) },
            { "UI/Talk/Noa_Talk", new FrameLayout(695f, 362f, 1700f, 560f) },
            { "UI/Talk/Elly_Talk", new FrameLayout(692f, 380f, 1930f, 546f, Px(617f, 315f, 987f, 368f)) },
            // 이리아 창은 이름판이 밝은 흰 판이라 금색 이름이 묻혔다. 대사 글자와 같은 짙은 갈색으로 쓴다.
            { "UI/Talk/Iria_Talk", new FrameLayout(682f, 324f, 1985f, 506f, Px(605f, 258f, 882f, 310f), new Color(0.35f, 0.22f, 0.1f)) },
            { "UI/Talk/Isis_Talk", new FrameLayout(848f, 362f, 1900f, 536f) },
            { "UI/Talk/Lily_Talk", new FrameLayout(845f, 380f, 1950f, 533f, Px(685f, 297f, 831f, 351f)) },
            { "UI/Talk/BossKnight_Talk", new FrameLayout(615f, 470f, 1740f, 628f, Px(625f, 411f, 932f, 467f)) },
            { "UI/Talk/Lunabel_Talk", new FrameLayout(822f, 478f, 1750f, 636f, Px(816f, 406f, 1062f, 462f)) },
            { "UI/Talk/Aqua_Talk", new FrameLayout(1002f, 372f, 1972f, 538f) },
        };

        private const float BoxBottomMargin = 30f;
        private const float NoaTextRight = 1700f / ImageWidth;

        private GameObject box;
        private RectTransform boxRect;
        private RawImage frameImage;
        private Image plainBox;
        private Text nameText;
        private RectTransform nameRect;
        private Text dialogueText;
        private RectTransform textRect;
        private string currentResourcePath;

        // 지금 띄운 대사(같은 대사를 매 프레임 다시 Show해도 타이핑을 처음부터 다시 하지 않게 비교한다).
        private string currentKey;
        private readonly List<string> pages = new List<string>();
        private int pageIndex;
        private bool typing;
        private float typedChars;
        private int pageVisibleChars;

        // 타이핑 중이거나 아직 남은 장이 있다(시간이 지나면 저절로 닫히는 대사는 이 동안 기다려 줘야 한다).
        public bool IsBusy => box != null && box.activeSelf && (typing || pageIndex < pages.Count - 1);

        // 대사를 넘기는 입력(Z, allowEnter면 Enter도). 대화창이 그 입력을 타이핑 마저 보여 주기/다음 장 넘기기에 썼으면
        // false다. 한 프레임에 여러 곳에서 물어도 한 번만 쓰인다.
        public static bool AdvancePressed(Keyboard keyboard, bool allowEnter = false)
        {
            if (keyboard == null) return false;
            bool pressed = keyboard.zKey.wasPressedThisFrame
                || (allowEnter && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame));
            if (!pressed) return false;
            if (consumedFrame == Time.frameCount) return false;
            if (instance != null && instance.box != null && instance.box.activeSelf && instance.shownFrame == Time.frameCount) return false;
            if (instance == null || instance.box == null || !instance.box.activeSelf) return true;

            if (instance.typing)
            {
                instance.FinishTyping();
                consumedFrame = Time.frameCount;
                return false;
            }
            if (instance.pageIndex < instance.pages.Count - 1)
            {
                instance.StartPage(instance.pageIndex + 1);
                consumedFrame = Time.frameCount;
                return false;
            }
            return true;
        }

        public void Initialize()
        {
            instance = this;
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
            dialogueText.fontSize = FontSize;
            dialogueText.color = Color.white;
            dialogueText.alignment = TextAnchor.MiddleLeft;
            dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            // 예전에는 Best Fit으로 글자를 줄여 칸에 우겨넣다가 그래도 넘치면 잘라냈다. 이제는 글자 크기를 고정하고
            // 칸에 다 안 들어가면 여러 장으로 나눈다(Paginate). Truncate는 혹시 모를 넘침에 대한 안전망이다.
            dialogueText.verticalOverflow = VerticalWrapMode.Truncate;
            dialogueText.resizeTextForBestFit = false;
            dialogueText.supportRichText = true;

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
            // 프레임 그림이 화면에서 작게 줄어들면(FitBoxToFrame) 이름판 칸 높이가 글자 한 줄보다 낮아지는데, 기본값(Truncate)이면
            // Unity가 넘치는 줄을 통째로 숨겨 이름이 아예 안 보였다. 칸을 넘쳐도 가운데 정렬로 그대로 그리게 한다.
            nameText.verticalOverflow = VerticalWrapMode.Overflow;
            nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
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
            float textTop = DefaultTextTop, float? namedTextTop = null, string namePrefix = null)
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

            // 칸을 재 둔 그림이면 넘겨받은 좌표 대신 표의 값을 쓴다.
            Color nameColor = DefaultNameColor;
            if (hasFrame && FrameLayouts.TryGetValue(resourcePath, out var layout))
            {
                textLeft = layout.Text.xMin / ImageWidth;
                textRight = layout.Text.xMax / ImageWidth;
                textTop = 1f - layout.Text.yMin / ImageHeight;
                textBottom = 1f - layout.Text.yMax / ImageHeight;
                namedTextTop = textTop;
                if (layout.Plate.HasValue)
                {
                    var plate = layout.Plate.Value;
                    namePlateX0 = plate.xMin; namePlateY0 = plate.yMin;
                    namePlateX1 = plate.xMax; namePlateY1 = plate.yMax;
                }
                if (layout.NameColor.HasValue) nameColor = layout.NameColor.Value;
                textRect.anchorMin = new Vector2(textLeft, textBottom);
            }
            nameText.color = nameColor;

            bool hasName = hasFrame && !string.IsNullOrEmpty(speakerName);
            nameText.text = hasName ? speakerName : "";
            nameText.gameObject.SetActive(hasName);
            float plateTopAnchor = 1f - namePlateY1 / ImageHeight;
            nameRect.anchorMin = new Vector2(namePlateX0 / ImageWidth, plateTopAnchor);
            nameRect.anchorMax = new Vector2(namePlateX1 / ImageWidth, 1f - namePlateY0 / ImageHeight);
            // 노아 창은 대사 칸 오른쪽에 나침반 문양(그림 x 약 1640~)이 있어 기본 오른쪽 끝(0.92)이면 글자가 문양에 겹쳤다.
            if (resourcePath == "UI/Talk/Noa_Talk" && Mathf.Approximately(textRight, DefaultTextRight)) textRight = NoaTextRight; // 표가 없을 때의 예전 처리
            textRect.anchorMax = new Vector2(hasFrame ? textRight : DefaultTextRight, hasName ? namedTextTop ?? plateTopAnchor : hasFrame ? textTop : DefaultTextTop);
            dialogueText.color = textColor ?? Color.white;

            // 이름판이 없는 그림이면 이름을 대사 첫 줄에 붙인다(장마다 다시 붙인다).
            if (namePrefix == null && !hasName && resourcePath != null) FrameNameTags.TryGetValue(resourcePath, out namePrefix);
            namePrefix ??= "";

            string key = resourcePath + "\u0001" + namePrefix + "\u0001" + line;
            bool wasOpen = box.activeSelf;
            box.SetActive(true);
            if (wasOpen && key == currentKey) return; // 같은 대사를 다시 띄운 것뿐이면 타이핑을 이어 간다
            currentKey = key;

            Canvas.ForceUpdateCanvases();
            Paginate(namePrefix, line ?? "");
            StartPage(0);
            shownFrame = Time.frameCount;
        }

        private void Update()
        {
            if (!typing || box == null || !box.activeSelf) return;
            typedChars += Time.unscaledDeltaTime * CharsPerSecond * Aethoria.Save.GameSettings.DialogueSpeedMultiplier;
            if (typedChars >= pageVisibleChars)
            {
                FinishTyping();
                return;
            }
            dialogueText.text = RevealPrefix(pages[pageIndex], Mathf.FloorToInt(typedChars));
        }

        private void StartPage(int index)
        {
            pageIndex = index;
            typedChars = 0f;
            pageVisibleChars = CountVisible(pages[index]);
            typing = pageVisibleChars > 0;
            dialogueText.text = typing ? RevealPrefix(pages[index], 0) : pages[index];
        }

        private void FinishTyping()
        {
            typing = false;
            dialogueText.text = pages[pageIndex];
        }

        // ---- 장 나누기 ----

        // 대사 칸(고정 글자 크기)에 들어가는 만큼씩 끊어 장을 만든다. 끊는 자리는 띄어쓰기/줄바꿈 뒤를 고르고(태그 안은 피한다),
        // 장 사이에 걸친 서식 태그(<b>, <color> 등)는 앞 장에서 닫고 다음 장에서 다시 연다. 마지막이 아닌 장 끝에는 ▼를 붙인다.
        // 대사 끝에 붙는 조작 안내("   [Esc] 스킵   [Z]" — 공백 + 색 태그 + [키]). 장을 나눌 때 이 안내만 따로 떨어져
        // 대사 없는 빈 장이 생기지 않게, 떼어 두었다가 마지막 장 끝에 다시 붙인다.
        private static readonly System.Text.RegularExpressions.Regex TrailingHints =
            new System.Text.RegularExpressions.Regex(@"(?:\s+<color=#[0-9A-Fa-f]{6,8}>\[[^<]*</color>)+\s*$");

        private void Paginate(string prefix, string body)
        {
            pages.Clear();
            string tail = "";
            var match = TrailingHints.Match(body);
            if (match.Success && match.Index > 0)
            {
                tail = match.Value;
                body = body.Substring(0, match.Index);
            }

            PaginateBody(prefix, body, tail);

            // 그래도 보이는 글자가 하나도 없는 장이 생기면(태그만 남은 장 등) 빼고, 안내는 마지막 장에 붙인다.
            for (int i = pages.Count - 1; i > 0; i--)
            {
                if (string.IsNullOrWhiteSpace(StripTags(pages[i]).Replace(ContinueHint, "")))
                    pages.RemoveAt(i);
            }
            if (pages.Count == 0) pages.Add(prefix + body);
            int last = pages.Count - 1;
            if (pages[last].EndsWith(ContinueHint)) pages[last] = pages[last].Substring(0, pages[last].Length - ContinueHint.Length);
            pages[last] += tail;
        }

        // boundary 바로 앞(공백·태그를 건너뛴 마지막 글자)이 문장 끝 부호이거나, boundary가 줄바꿈 자리인지.
        private static bool EndsSentence(string text, int boundary)
        {
            if (boundary < text.Length && text[boundary] == '\n') return true;
            int i = boundary - 1;
            while (i >= 0)
            {
                char c = text[i];
                if (c == ' ' || c == '\n') { i--; continue; }
                if (c == '>') { int open = text.LastIndexOf('<', i); if (open < 0) break; i = open - 1; continue; }
                return c == '.' || c == '?' || c == '!' || c == '…' || c == '~' || c == '"' || c == '」';
            }
            return false;
        }

        private static string StripTags(string text) => System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "");

        // tail(조작 안내)은 마지막 장에 붙을 자리까지 셈해서 나눈다. 장 목록에는 tail을 빼고 넣는다(Paginate가 붙인다).
        private void PaginateBody(string prefix, string body, string tail)
        {
            string remaining = body;
            int guard = 0;
            while (guard++ < 50)
            {
                if (Fits(prefix + remaining + tail) || remaining.Length == 0)
                {
                    pages.Add(prefix + remaining);
                    return;
                }

                // 들어가는 자리 중 문장이 끝나는 곳(. ? ! … 뒤, 줄바꿈)이 있으면 거기서 끊는다 — "내 검을 ▼ / 넘어 봐라"처럼
                // 문장 한가운데서 장이 넘어가면 대사가 끊긴 것처럼 보였다. 문장 끝이 없을 때만 마지막 띄어쓰기에서 끊는다.
                int cut = -1, sentenceCut = -1;
                foreach (int boundary in Boundaries(remaining))
                {
                    string part = remaining.Substring(0, boundary).TrimEnd();
                    if (!Fits(prefix + part + CloseTags(OpenTagsAt(remaining, boundary)) + ContinueHint)) break;
                    cut = boundary;
                    if (EndsSentence(remaining, boundary)) sentenceCut = boundary;
                }
                if (sentenceCut > 0) cut = sentenceCut;
                if (cut <= 0) cut = LongestFittingCut(prefix, remaining); // 띄어쓰기 없이 긴 덩어리면 글자 단위로
                if (cut <= 0 || cut >= remaining.Length)
                {
                    pages.Add(prefix + remaining);
                    return;
                }

                var open = OpenTagsAt(remaining, cut);
                pages.Add(prefix + remaining.Substring(0, cut).TrimEnd() + CloseTags(open) + ContinueHint);
                remaining = string.Concat(open) + remaining.Substring(cut).TrimStart(' ', '\n');
            }
            pages.Add(prefix + remaining);
        }

        private bool Fits(string text)
        {
            Vector2 size = textRect.rect.size;
            if (size.x <= 1f || size.y <= 1f) return true;
            var settings = dialogueText.GetGenerationSettings(new Vector2(size.x, 0f));
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.resizeTextForBestFit = false;
            float height = dialogueText.cachedTextGeneratorForLayout.GetPreferredHeight(text, settings) / dialogueText.pixelsPerUnit;
            return height <= size.y + 0.5f;
        }

        // 끊어도 되는 자리(띄어쓰기 바로 뒤, 줄바꿈 바로 앞). 태그 안은 건너뛴다.
        private static IEnumerable<int> Boundaries(string text)
        {
            bool inTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<') inTag = true;
                else if (c == '>') { inTag = false; continue; }
                if (inTag) continue;
                if (c == '\n' && i > 0) yield return i;
                else if (c == ' ' && i + 1 < text.Length) yield return i + 1;
            }
        }

        private int LongestFittingCut(string prefix, string text)
        {
            int best = -1;
            for (int i = 1; i < text.Length; i++)
            {
                // 태그 한가운데에서는 끊지 않는다(i 앞에서 마지막으로 연 '<'가 아직 '>'로 닫히지 않았으면 태그 안).
                if (text.LastIndexOf('<', i - 1) > text.LastIndexOf('>', i - 1)) continue;
                if (!Fits(prefix + text.Substring(0, i) + CloseTags(OpenTagsAt(text, i)) + ContinueHint)) break;
                best = i;
            }
            return best;
        }

        // text[0..index) 안에서 열린 채 남은 서식 태그들(여는 태그 원문, 연 순서대로).
        private static List<string> OpenTagsAt(string text, int index)
        {
            var stack = new List<string>();
            int i = 0;
            while (i < index)
            {
                if (text[i] == '<')
                {
                    int end = text.IndexOf('>', i);
                    if (end < 0 || end >= index) break;
                    string tag = text.Substring(i, end - i + 1);
                    if (tag.StartsWith("</")) { if (stack.Count > 0) stack.RemoveAt(stack.Count - 1); }
                    else stack.Add(tag);
                    i = end + 1;
                }
                else i++;
            }
            return stack;
        }

        private static string TagName(string openTag)
        {
            int end = openTag.IndexOfAny(new[] { '=', '>' }, 1);
            return openTag.Substring(1, end - 1);
        }

        private static string CloseTags(List<string> open)
        {
            var sb = new StringBuilder();
            for (int i = open.Count - 1; i >= 0; i--) sb.Append("</").Append(TagName(open[i])).Append('>');
            return sb.ToString();
        }

        // ---- 타이핑 ----

        private static int CountVisible(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '<')
                {
                    int end = text.IndexOf('>', i);
                    if (end > i) { i = end; continue; }
                }
                count++;
            }
            return count;
        }

        // 앞에서부터 visible 글자만 보이게 하고, 나머지는 투명하게 그대로 둔다(글자가 늘어날 때 줄 배치가 흔들리지 않게).
        // 숨긴 부분은 색 태그만 빼고(투명색을 덮어쓰지 않게) 굵기/크기 태그는 남겨 자리를 그대로 차지하게 한다.
        private static string RevealPrefix(string text, int visible)
        {
            int shown = 0;
            int i = 0;
            while (i < text.Length && shown < visible)
            {
                if (text[i] == '<')
                {
                    int end = text.IndexOf('>', i);
                    if (end > i) { i = end + 1; continue; }
                }
                shown++;
                i++;
            }
            // 바로 뒤에 붙은 태그(닫는 태그 등)는 보이는 쪽에 포함시킨다.
            while (i < text.Length && text[i] == '<')
            {
                int end = text.IndexOf('>', i);
                if (end < 0) break;
                i = end + 1;
            }

            var open = OpenTagsAt(text, i);
            var sb = new StringBuilder(text.Length + 64);
            sb.Append(text, 0, i).Append(CloseTags(open)).Append("<color=#00000000>");
            foreach (var tag in open) if (TagName(tag) != "color") sb.Append(tag);
            int j = i;
            while (j < text.Length)
            {
                if (text[j] == '<')
                {
                    int end = text.IndexOf('>', j);
                    if (end > j)
                    {
                        string tag = text.Substring(j, end - j + 1);
                        bool isColor = tag.StartsWith("<color") || tag == "</color>";
                        if (!isColor) sb.Append(tag);
                        j = end + 1;
                        continue;
                    }
                }
                sb.Append(text[j]);
                j++;
            }
            sb.Append("</color>");
            return sb.ToString();
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
            Show(PlayerTalkFrame, line, PlayerTextLeft,
                textBottom: PlayerTextBottom, textRight: PlayerTextRight, textTop: PlayerTextTop, namePrefix: PlayerNameTag);
        }

        // 물의 여왕 아쿠아 대사. 이름을 첫 줄에 붙여 아쿠아 대화창에 띄운다.
        public void ShowAqua(string line)
        {
            Show(AquaTalkFrame, line, AquaTextLeft,
                textBottom: AquaTextBottom, textRight: AquaTextRight, textTop: AquaTextTop, namePrefix: AquaNameTag);
        }

        // 루나벨 대사. 이름은 이름판 안에 쓴다.
        public void ShowLunabel(string line)
        {
            Show(LunabelTalkFrame, line, LunabelTextLeft, "루나벨",
                LunabelPlateX0, LunabelPlateY0, LunabelPlateX1, LunabelPlateY1, null, LunabelTextBottom, LunabelTextRight);
        }

        // 전투 도중 루나벨이 외치는 대사를 화면 한가운데에 띄운다(2페이즈 개막의 광기 어린 웃음).
        // 다음 Show/ShowLunabel 때는 FitBoxToFrame이 원래 자리(화면 아래)로 되돌린다.
        public void ShowLunabelCentered(string line)
        {
            ShowLunabel(line);
            var parent = boxRect.parent as RectTransform;
            if (parent == null) return;
            float centeredY = (parent.rect.height - boxRect.sizeDelta.y) * 0.5f;
            boxRect.anchoredPosition = new Vector2(boxRect.anchoredPosition.x, centeredY);
            BringToFront();
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
            currentKey = null;
            typing = false;
            pages.Clear();
            pageIndex = 0;
        }
    }
}

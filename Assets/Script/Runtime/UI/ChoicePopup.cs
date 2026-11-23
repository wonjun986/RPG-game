using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 게임 전체에서 함께 쓰는 선택 팝업("마을로 이동하시겠습니까?", 장착 확인, 상점 구매 확인 등)과 짧은 알림 띠.
    // 그림은 UI/Common/Popup(원본 Sprite/UI/Menus/Shop의 고딕 보라·금테 판/버튼/화살표/알림 띠). 글자는 코드로 쓴다.
    //  - 팝업: 화면을 어둡게 덮고 가운데에 판이 살짝 커지며 튕기듯 나타난다. 닫을 때는 작아지며 흐려진다.
    //    ←/→(또는 마우스 올리기)로 버튼 선택, Z/Enter = 고른 버튼, X/Esc = 취소. 수량 줄이 있으면 ↑/↓(또는 화살표 버튼)로 수량을 바꾼다.
    //  - 알림 띠: 화면 위쪽에 살짝 내려오며 나타났다가 잠시 뒤 흐려진다(입력을 막지 않는다).
    // 자체 캔버스(맨 위)에 그리므로 타이틀 화면에서도, 상점·장비창 위에서도 쓸 수 있다.
    // 다른 창의 Update는 ConsumesInput이 true인 동안 키 입력을 무시해야 한다(팝업을 닫은 그 프레임의 Z/X까지 포함).
    public class ChoicePopup : MonoBehaviour
    {
        public sealed class Request
        {
            public string Message;
            public string SubMessage;           // 메시지 아래 작은 글씨(선택)
            public Sprite Icon;                 // 메시지 왼쪽 아이콘(선택)
            public string ConfirmLabel = "예";
            public string CancelLabel = "아니오";
            public System.Action OnConfirm;
            public System.Action OnCancel;
            // 수량 선택: MaxQuantity가 2 이상이면 수량 줄을 띄운다. QuantityLabel로 "3개 · 300 G" 같은 글을 만들고,
            // 확인하면 OnConfirmQuantity(수량)가 불린다(OnConfirm보다 먼저).
            public int MaxQuantity;
            public System.Func<int, string> QuantityLabel;
            public System.Action<int> OnConfirmQuantity;
            // 떠 있는 동안 게임을 멈추고 이 캐릭터의 조작을 잠근다. 닫으면 열기 전 상태로 되돌린다.
            public bool PauseGame = true;
            public CharacterMovement2D LockMovement;
        }

        private const string ArtFolder = "UI/Common/Popup/";
        private static readonly Vector2 ReferenceResolution = new(1280f, 720f);
        private const float PanelWidth = 640f;
        private const float ButtonWidth = 236f;
        private const float OpenDuration = 0.24f;
        private const float CloseDuration = 0.13f;
        private const float DimAlpha = 0.6f;
        private static readonly Color LabelColor = new(0.86f, 0.82f, 0.92f);
        private static readonly Color SelectedLabelColor = new(1f, 0.95f, 0.8f);

        private static ChoicePopup instance;
        private static int closedFrame = -1;

        // 팝업이 떠 있거나 방금(이 프레임에) 닫혔다 — 다른 창은 이번 프레임 키 입력을 쓰지 않는다.
        public static bool ConsumesInput => IsOpen || Time.frameCount == closedFrame;
        public static bool IsOpen => instance != null && instance.request != null;

        private Font font;
        private CanvasGroup rootGroup;
        private Image dim;
        private RectTransform panel;
        private Text messageText;
        private Text subMessageText;
        private Image iconImage;
        private RectTransform messageBox;
        private GameObject quantityRow;
        private Text quantityText;
        private readonly ButtonView[] buttons = new ButtonView[2];

        private Request request;
        private int selected;               // 0 = 확인, 1 = 취소
        private int quantity = 1;
        private int openedFrame = -1;
        private float animStart;
        private bool closing;
        private float savedTimeScale = 1f;
        private bool savedInputLocked;

        // 알림 띠
        private RectTransform toast;
        private CanvasGroup toastGroup;
        private Text toastText;
        private float toastStart = -10f;
        private float toastHold;
        private const float ToastFadeIn = 0.18f;
        private const float ToastFadeOut = 0.3f;

        private sealed class ButtonView
        {
            public RectTransform Root;
            public Image Normal;
            public Image Selected;      // 고른 버튼 뒤에 번지는 보라 빛
            public Text Label;
            public float Hover = 1f;    // 지금 크기 배율(고르면 SelectedButtonScale로 부드럽게 커진다)
        }

        private static readonly Color SelectedGlowColor = new(0.68f, 0.38f, 1f, 0.55f);
        private static readonly Color UnselectedButtonTint = new(0.62f, 0.58f, 0.68f);
        private const float SelectedButtonScale = 1.05f;
        private static Sprite softGlow;

        // 가장자리가 흐린 둥근 빛(버튼 뒤 선택 표시). 가로로 늘려 쓴다.
        private static Sprite SoftGlow
        {
            get
            {
                if (softGlow != null) return softGlow;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var center = new Vector2((size - 1) / 2f, (size - 1) / 2f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), center) / (size / 2f);
                        float a = Mathf.Clamp01(1f - d);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                }
                texture.Apply();
                softGlow = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                return softGlow;
            }
        }

        // ---------------------------------------------------------------- 공개 함수

        public static void Show(Request newRequest)
        {
            if (newRequest == null) return;
            Ensure().Open(newRequest);
        }

        // 확인 버튼 하나뿐인 간단한 질문을 위한 줄임 함수.
        public static void Ask(string message, System.Action onConfirm, System.Action onCancel = null,
            string confirmLabel = "예", string cancelLabel = "아니오", CharacterMovement2D lockMovement = null, string subMessage = null)
        {
            Show(new Request
            {
                Message = message, SubMessage = subMessage, OnConfirm = onConfirm, OnCancel = onCancel,
                ConfirmLabel = confirmLabel, CancelLabel = cancelLabel, LockMovement = lockMovement,
            });
        }

        // 짧은 알림(구매 완료, 골드 부족 등). good이면 밝은 초록, 아니면 붉은 글씨.
        public static void Toast(string text, bool good = false, float hold = 1.3f)
        {
            Ensure().ShowToast(text, good ? new Color(0.65f, 1f, 0.7f) : new Color(1f, 0.55f, 0.55f), hold);
        }

        public static void Toast(string text, Color color, float hold = 1.3f) => Ensure().ShowToast(text, color, hold);

        // ---------------------------------------------------------------- 짓기

        private static ChoicePopup Ensure()
        {
            if (instance != null) return instance;
            TitleScreenUI.EnsureEventSystem();

            var go = new GameObject("ChoicePopupCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // 다른 모든 UI 위
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;

            instance = go.AddComponent<ChoicePopup>();
            instance.Build();
            return instance;
        }

        private void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = NewRect("PopupRoot", transform);
            Stretch(root);
            rootGroup = root.gameObject.AddComponent<CanvasGroup>();

            dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, DimAlpha);
            dim.raycastTarget = true; // 뒤쪽 UI 클릭을 막는다

            var panelSprite = Load("popup_panel");
            float panelHeight = panelSprite != null ? PanelWidth * panelSprite.rect.height / panelSprite.rect.width : 310f;
            panel = NewRect("Panel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelWidth, panelHeight);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = panelSprite;
            panelImage.color = panelSprite != null ? Color.white : new Color(0.06f, 0.04f, 0.09f, 0.97f);
            panelImage.raycastTarget = true;

            // 판 안쪽 글자 영역(판 그림의 테두리 안쪽).
            messageBox = NewRect("MessageBox", panel);
            iconImage = NewRect("Icon", panel).gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            messageText = MakeText(messageBox, "Message", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            subMessageText = MakeText(messageBox, "SubMessage", 15, new Color(0.75f, 0.68f, 0.85f), TextAnchor.UpperCenter, FontStyle.Normal);

            BuildQuantityRow();

            buttons[0] = BuildButton(panel, "ConfirmButton", 0);
            buttons[1] = BuildButton(panel, "CancelButton", 1);

            BuildToast();
            root.gameObject.SetActive(false);
        }

        private void BuildQuantityRow()
        {
            var row = NewRect("QuantityRow", panel);
            row.anchorMin = new Vector2(0.18f, 0.33f);
            row.anchorMax = new Vector2(0.82f, 0.47f);
            row.offsetMin = row.offsetMax = Vector2.zero;
            quantityRow = row.gameObject;

            quantityText = MakeText(row, "Quantity", 20, new Color(1f, 0.88f, 0.45f), TextAnchor.MiddleCenter, FontStyle.Bold);
            var textRect = quantityText.rectTransform;
            textRect.anchorMin = new Vector2(0.18f, 0f);
            textRect.anchorMax = new Vector2(0.82f, 1f);

            var arrowSprite = Load("popup_arrow");
            for (int i = 0; i < 2; i++)
            {
                int delta = i == 0 ? -1 : 1;
                var arrow = NewRect(i == 0 ? "LessButton" : "MoreButton", row);
                arrow.anchorMin = arrow.anchorMax = new Vector2(i == 0 ? 0.07f : 0.93f, 0.5f);
                arrow.sizeDelta = new Vector2(38f, 44f);
                if (i == 0) arrow.localScale = new Vector3(-1f, 1f, 1f); // 오른쪽 화살표 그림을 뒤집어 왼쪽으로
                var image = arrow.gameObject.AddComponent<Image>();
                image.sprite = arrowSprite;
                image.preserveAspect = true;
                var target = arrow.gameObject.AddComponent<PointerTarget>();
                target.OnDown = () => ChangeQuantity(delta);
            }
        }

        private ButtonView BuildButton(RectTransform parent, string name, int index)
        {
            var normalSprite = Load("popup_button");
            float height = normalSprite != null ? ButtonWidth * normalSprite.rect.height / normalSprite.rect.width : 56f;
            var anchor = new Vector2(index == 0 ? 0.3f : 0.7f, 0.19f);

            // 두 버튼 모두 같은 금테 버튼 그림을 쓴다(선택 그림은 비율이 달라 두 버튼 크기가 달라 보였다).
            // 고른 버튼은 뒤에 보라 빛이 번지고, 밝아지며 살짝 커진다. 빛은 버튼 뒤에 깔려야 하므로 버튼보다 먼저 만든다.
            var glowRect = NewRect(name + "Glow", parent);
            glowRect.anchorMin = glowRect.anchorMax = anchor;
            glowRect.sizeDelta = new Vector2(ButtonWidth * 1.25f, height * 1.9f);
            var glow = glowRect.gameObject.AddComponent<Image>();
            glow.sprite = SoftGlow;
            glow.color = SelectedGlowColor;
            glow.raycastTarget = false;

            var root = NewRect(name, parent);
            root.anchorMin = root.anchorMax = anchor;
            root.sizeDelta = new Vector2(ButtonWidth, height);

            var normal = root.gameObject.AddComponent<Image>();
            normal.sprite = normalSprite;
            normal.color = normalSprite != null ? Color.white : new Color(0.15f, 0.1f, 0.2f);
            var selectedImage = glow;

            var label = MakeText(root, "Label", 19, LabelColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.rectTransform.offsetMin = new Vector2(18f, 0f);
            label.rectTransform.offsetMax = new Vector2(-18f, 0f);

            var target = root.gameObject.AddComponent<PointerTarget>();
            target.OnEnter = () => SetSelected(index);
            target.OnDown = () => Activate(index);
            return new ButtonView { Root = root, Normal = normal, Selected = selectedImage, Label = label };
        }

        private void BuildToast()
        {
            var sprite = Load("popup_toast");
            toast = NewRect("Toast", transform);
            toast.anchorMin = toast.anchorMax = new Vector2(0.5f, 0.8f);
            float width = 560f;
            toast.sizeDelta = new Vector2(width, sprite != null ? width * sprite.rect.height / sprite.rect.width : 80f);
            var image = toast.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : new Color(0f, 0f, 0f, 0.8f);
            image.raycastTarget = false;
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastText = MakeText(toast, "Text", 19, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            toastText.rectTransform.offsetMin = new Vector2(60f, 0f);
            toastText.rectTransform.offsetMax = new Vector2(-60f, 0f);
            toast.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- 열고 닫기

        private void Open(Request newRequest)
        {
            bool wasOpen = request != null && !closing;
            if (!wasOpen)
            {
                // 이미 멈춰 있던 화면(상점·장비창) 위에서 열렸다면 닫을 때 그 상태 그대로 돌려준다.
                savedTimeScale = Time.timeScale;
                savedInputLocked = newRequest.LockMovement != null && newRequest.LockMovement.IsInputLocked;
            }

            request = newRequest;
            closing = false;
            selected = 0;
            quantity = 1;
            openedFrame = Time.frameCount;
            animStart = Time.unscaledTime;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            if (request.PauseGame) Time.timeScale = 0f;
            if (request.LockMovement != null) request.LockMovement.SetInputLocked(true);

            Layout();
            RefreshButtons();
            RefreshQuantity();
            rootGroup.gameObject.SetActive(true);
            rootGroup.blocksRaycasts = true;
            ApplyOpen(0f);
        }

        // 아이콘·부제·수량 줄이 있느냐에 따라 글자 영역을 나눈다.
        private void Layout()
        {
            bool hasQuantity = request.MaxQuantity >= 2;
            bool hasIcon = request.Icon != null;
            bool hasSub = !string.IsNullOrEmpty(request.SubMessage);

            float bottom = hasQuantity ? 0.49f : 0.34f;
            messageBox.anchorMin = new Vector2(hasIcon ? 0.3f : 0.1f, bottom);
            messageBox.anchorMax = new Vector2(0.9f, 0.82f);
            messageBox.offsetMin = messageBox.offsetMax = Vector2.zero;

            iconImage.enabled = hasIcon;
            iconImage.sprite = request.Icon;
            var iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0.1f, bottom + 0.02f);
            iconRect.anchorMax = new Vector2(0.27f, 0.8f);
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;

            messageText.text = request.Message ?? string.Empty;
            messageText.rectTransform.anchorMin = new Vector2(0f, hasSub ? 0.38f : 0f);
            subMessageText.gameObject.SetActive(hasSub);
            subMessageText.text = request.SubMessage ?? string.Empty;
            subMessageText.rectTransform.anchorMax = new Vector2(1f, 0.36f);

            quantityRow.SetActive(hasQuantity);
            buttons[0].Label.text = request.ConfirmLabel;
            buttons[1].Label.text = request.CancelLabel;
        }

        private void Close(bool confirmed)
        {
            if (request == null || closing) return;

            var finished = request;
            closing = true;
            closedFrame = Time.frameCount;
            animStart = Time.unscaledTime;
            rootGroup.blocksRaycasts = false;

            // 상태를 먼저 되돌린 뒤 콜백을 부른다(콜백이 맵을 새로 짓거나 다른 팝업을 열 수 있다).
            if (finished.PauseGame) Time.timeScale = savedTimeScale;
            if (finished.LockMovement != null) finished.LockMovement.SetInputLocked(savedInputLocked);

            if (confirmed)
            {
                if (finished.MaxQuantity >= 2) finished.OnConfirmQuantity?.Invoke(quantity);
                else finished.OnConfirmQuantity?.Invoke(1);
                finished.OnConfirm?.Invoke();
            }
            else
            {
                finished.OnCancel?.Invoke();
            }
        }

        private void FinishClose()
        {
            if (!closing) return; // 콜백이 그새 다른 팝업을 열었다
            closing = false;
            request = null;
            rootGroup.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- 조작

        private void SetSelected(int index)
        {
            if (request == null || closing || selected == index) return;
            selected = index;
            RefreshButtons();
        }

        private void Activate(int index)
        {
            if (request == null || closing) return;
            Close(index == 0);
        }

        private void ChangeQuantity(int delta)
        {
            if (request == null || closing || request.MaxQuantity < 2) return;
            quantity = Mathf.Clamp(quantity + delta, 1, request.MaxQuantity);
            RefreshQuantity();
        }

        private void RefreshButtons()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                bool on = i == selected;
                buttons[i].Selected.enabled = on;
                buttons[i].Normal.color = on ? Color.white : UnselectedButtonTint;
                buttons[i].Label.color = on ? SelectedLabelColor : LabelColor;
            }
        }

        private void RefreshQuantity()
        {
            if (request == null || request.MaxQuantity < 2) return;
            quantityText.text = request.QuantityLabel != null ? request.QuantityLabel(quantity) : $"{quantity}개";
        }

        private void Update()
        {
            UpdateToast();
            if (request == null) return;

            float t = Time.unscaledTime - animStart;
            if (closing)
            {
                ApplyClose(t);
                if (t >= CloseDuration) FinishClose();
                return;
            }
            ApplyOpen(t);

            // 팝업을 연 그 프레임의 키(창을 연 Z 등)로 바로 고르지 않게 한 프레임 거른다.
            if (Time.frameCount == openedFrame) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) SetSelected(0);
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) SetSelected(1);
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) ChangeQuantity(1);
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) ChangeQuantity(-1);

            if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                Activate(selected);
            else if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                Activate(1);
        }

        // 화면이 어두워지고, 판이 작게 시작해 살짝 넘쳤다가 제 크기로 자리 잡는다. 버튼은 조금 늦게 올라온다.
        private void ApplyOpen(float t)
        {
            float k = Mathf.Clamp01(t / OpenDuration);
            dim.color = new Color(0f, 0f, 0f, DimAlpha * EaseOutCubic(k));
            float pop = EaseOutBack(k);
            panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.82f, 1f, pop);
            panel.anchoredPosition = new Vector2(0f, -18f * (1f - EaseOutCubic(k)));
            rootGroup.alpha = Mathf.Clamp01(k * 2.2f);

            float b = Mathf.Clamp01((t - OpenDuration * 0.35f) / (OpenDuration * 0.65f));
            float buttonPop = Mathf.LerpUnclamped(0.85f, 1f, EaseOutBack(b));
            for (int i = 0; i < buttons.Length; i++)
            {
                // 고른 버튼은 살짝 크게(부드럽게 따라간다).
                float target = i == selected ? SelectedButtonScale : 1f;
                buttons[i].Hover = Mathf.MoveTowards(buttons[i].Hover, target, Time.unscaledDeltaTime * 0.8f);
                buttons[i].Root.localScale = Vector3.one * (buttonPop * buttons[i].Hover);
                buttons[i].Selected.rectTransform.localScale = buttons[i].Root.localScale;
            }
            // 고른 버튼 뒤의 빛은 은은하게 숨 쉬듯 밝아졌다 어두워진다.
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            buttons[selected].Selected.color = new Color(SelectedGlowColor.r, SelectedGlowColor.g, SelectedGlowColor.b, SelectedGlowColor.a * pulse);
        }

        private void ApplyClose(float t)
        {
            float k = Mathf.Clamp01(t / CloseDuration);
            float e = k * k;
            rootGroup.alpha = 1f - e;
            panel.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, e);
        }

        // ---------------------------------------------------------------- 알림 띠

        private void ShowToast(string text, Color color, float hold)
        {
            toastText.text = text;
            toastText.color = color;
            toastStart = Time.unscaledTime;
            toastHold = hold;
            toast.SetAsLastSibling();
            toast.gameObject.SetActive(true);
            UpdateToast();
        }

        private void UpdateToast()
        {
            if (!toast.gameObject.activeSelf) return;
            float t = Time.unscaledTime - toastStart;
            float total = ToastFadeIn + toastHold + ToastFadeOut;
            if (t >= total)
            {
                toast.gameObject.SetActive(false);
                return;
            }
            float alpha = t < ToastFadeIn ? t / ToastFadeIn
                : t > ToastFadeIn + toastHold ? 1f - (t - ToastFadeIn - toastHold) / ToastFadeOut : 1f;
            toastGroup.alpha = alpha;
            float drop = t < ToastFadeIn ? 1f - EaseOutCubic(t / ToastFadeIn) : 0f;
            toast.anchoredPosition = new Vector2(0f, 16f * drop);
        }

        // ---------------------------------------------------------------- 도우미

        private static Sprite Load(string name) => Resources.Load<Sprite>(ArtFolder + name);

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private Text MakeText(Transform parent, string name, int size, Color color, TextAnchor alignment, FontStyle style)
        {
            var rect = NewRect(name, parent);
            Stretch(rect);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static float EaseOutCubic(float x) { x = 1f - x; return 1f - x * x * x; }
        private static float EaseOutBack(float x)
        {
            const float c1 = 1.5f, c3 = c1 + 1f;
            float m = x - 1f;
            return 1f + c3 * m * m * m + c1 * m * m;
        }

        // InventoryUI.SlotInteraction과 같은 이유로 클릭 대신 누르는 순간(OnPointerDown)에 반응한다.
        private class PointerTarget : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
        {
            public System.Action OnEnter;
            public System.Action OnDown;
            public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke();
            public void OnPointerDown(PointerEventData eventData) => OnDown?.Invoke();
        }
    }
}

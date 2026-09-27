using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Aethoria.Save;

namespace Aethoria.UI
{
    // 게임 시작 시 표시되는 타이틀 화면(UI/Title.png). GAME START는 새 게임(빈 슬롯에 저장), CONTINUE는
    // 가장 최근에 저장된 슬롯에서 이어하기, EXIT는 게임 종료. 슬롯 5칸이 모두 차 있는데 GAME START를 누르면
    // 가장 오래된 슬롯을 덮어쓸지 한 번 더 묻는다.
    // 저장이 없으면 CONTINUE 글자를 어둡게 덮고, 눌러도 안내만 띄운다.
    // SETTINGS/EXTRAS는 아직 뒷받침하는 시스템이 없어 이미지에 장식으로만 그려져 있다(클릭 불가).
    public class TitleScreenUI : MonoBehaviour
    {
        private static readonly Vector2 ContinueMin = new Vector2(0.65f, 0.378f);
        private static readonly Vector2 ContinueMax = new Vector2(0.95f, 0.422f);

        private static readonly Color GlowColor = Color.white;
        private static readonly Vector2 GlowSize = new Vector2(268f, 34f); // 기준 해상도 1280x720. 원본(3:1)보다 납작하게 눌러 가는 줄로 보이게 한다
        private const float GlowAnchorX = (0.768f - 0.65f) / (0.95f - 0.65f); // 버튼 영역(0.65~0.95) 안에서 글자 중심 위치

        private GameObject confirmPanel;
        private Text alertText;
        private Button[] menuButtons;
        private RawImage[] menuGlows;
        private int selectedIndex;

        private SaveScreenUI loadScreen;

        // onContinue: 불러오기 화면에서 고른 슬롯 번호(1~5)를 넘겨받는다.
        public void Initialize(UnityAction onGameStart, UnityAction<int> onContinue)
        {
            EnsureEventSystem();

            var canvasGO = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            BuildBackground(canvasGO.transform, "UI/Title");

            bool hasSave = SaveSystem.HasSave;

            UnityAction startNew = () =>
            {
                Destroy(gameObject);
                onGameStart?.Invoke();
            };

            // 아래 앵커 좌표는 Title.png 안에 이미 그려진 "GAME START"/"CONTINUE"/"EXIT" 글자 위치에 맞춘 값이다.
            var gameStartButton = BuildButton(canvasGO.transform, "GameStartButton", new Vector2(0.65f, 0.433f), new Vector2(0.95f, 0.477f), () =>
            {
                // 빈 슬롯이 있으면 거기서 새로 시작하므로 기존 저장은 건드리지 않는다. 모든 슬롯이 차 있을 때만 묻는다.
                if (!SaveSystem.HasEmptySlot)
                {
                    confirmPanel.SetActive(true);
                    // 뒤쪽 메뉴가 방향키로 선택되지 않도록 선택을 비운다(보라색 표시는 마지막 항목에 남는다).
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                }
                else startNew();
            });
            var continueButton = BuildButton(canvasGO.transform, "ContinueButton", ContinueMin, ContinueMax, () =>
            {
                if (SaveSystem.HasSave) OpenLoadScreen();
                else ShowAlert("저장된 데이터가 없습니다.");
            });
            var exitButton = BuildButton(canvasGO.transform, "ExitButton", new Vector2(0.65f, 0.205f), new Vector2(0.95f, 0.255f), Application.Quit);

            SetupMenuSelection(new[] { gameStartButton, continueButton, exitButton }, hasSave ? 1 : 0);
            // 메뉴 글자만 따로 뽑은 레이어(UI/Title_MenuText.png)를 빛 위에 다시 덮어 글자가 가려지지 않게 한다.
            BuildBackground(canvasGO.transform, "UI/Title_MenuText").GetComponent<RawImage>().raycastTarget = false;

            if (!hasSave) BuildContinueDimmer(canvasGO.transform);
            BuildAlert(canvasGO.transform);
            BuildConfirmDialog(canvasGO.transform, startNew);

            // CONTINUE를 누르면 노아의 저장 화면과 같은 슬롯 창을 불러오기 모드로 띄운다.
            loadScreen = canvasGO.AddComponent<SaveScreenUI>();
            loadScreen.InitializeForLoad(slot =>
            {
                Destroy(gameObject);
                onContinue?.Invoke(slot);
            });
        }

        private void OpenLoadScreen()
        {
            // 불러오기 창이 떠 있는 동안 Enter가 뒤쪽 메뉴 버튼까지 누르지 않도록 선택을 비운다(보라색 표시는 남는다).
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            loadScreen.Open();
        }

        // 저장이 없을 때 CONTINUE 글자 위를 반투명하게 덮어 비활성처럼 보이게 한다(클릭은 버튼이 받는다).
        private static void BuildContinueDimmer(Transform parent)
        {
            var go = new GameObject("ContinueDimmer", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = ContinueMin;
            rect.anchorMax = ContinueMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = new Color(0.03f, 0.02f, 0.05f, 0.6f);
            image.raycastTarget = false;
        }

        private void BuildAlert(Transform parent)
        {
            var go = new GameObject("TitleAlert", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.6f, 0.13f);
            rect.anchorMax = new Vector2(1f, 0.19f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            alertText = go.GetComponent<Text>();
            alertText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            alertText.fontSize = 22;
            alertText.fontStyle = FontStyle.Bold;
            alertText.alignment = TextAnchor.MiddleCenter;
            alertText.color = new Color(1f, 0.5f, 0.5f);
            alertText.raycastTarget = false;
            alertText.text = string.Empty;
        }

        private void ShowAlert(string message)
        {
            alertText.text = message;
            CancelInvoke(nameof(ClearAlert));
            Invoke(nameof(ClearAlert), 2f);
        }

        private void ClearAlert()
        {
            alertText.text = string.Empty;
        }

        // 슬롯이 모두 찼는데 GAME START를 눌렀을 때: 새로 시작하면 마을에 들어가는 순간 가장 오래된 슬롯이 자동 저장으로 덮어써진다.
        private void BuildConfirmDialog(Transform parent, UnityAction startNew)
        {
            confirmPanel = new GameObject("NewGameConfirm", typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)confirmPanel.transform;
            dimRect.SetParent(parent, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            confirmPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var boxGO = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var boxRect = (RectTransform)boxGO.transform;
            boxRect.SetParent(dimRect, false);
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(620f, 240f);
            boxGO.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.09f, 0.97f);

            var messageGO = new GameObject("Message", typeof(RectTransform), typeof(Text));
            var messageRect = (RectTransform)messageGO.transform;
            messageRect.SetParent(boxRect, false);
            messageRect.anchorMin = new Vector2(0f, 0.45f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.offsetMin = new Vector2(20f, 0f);
            messageRect.offsetMax = new Vector2(-20f, 0f);
            var messageText = messageGO.GetComponent<Text>();
            messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            messageText.fontSize = 24;
            messageText.fontStyle = FontStyle.Bold;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.color = Color.white;
            messageText.text = $"저장 슬롯이 모두 찼습니다.\n새로 시작하면 가장 오래된 슬롯({SaveSystem.SlotForNewGame()}번)에 덮어써집니다.";

            WorldMapUI.BuildDialogButton(boxRect, "YesButton", "새로 시작", new Vector2(0.08f, 0.12f), new Vector2(0.47f, 0.4f), startNew);
            WorldMapUI.BuildDialogButton(boxRect, "NoButton", "취소", new Vector2(0.53f, 0.12f), new Vector2(0.92f, 0.4f), () => confirmPanel.SetActive(false));

            confirmPanel.SetActive(false);
        }

        private static GameObject BuildBackground(Transform parent, string resourcesPath)
        {
            var go = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(resourcesPath);
            return go;
        }

        // anchorMin/anchorMax: 화면 비율(0~1) 기준의 투명 버튼 영역. 배경 이미지 위에 그대로 겹친다.
        internal static Button BuildButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // 보이지 않지만 클릭/레이캐스트는 받는다
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            return button;
        }

        // 메뉴 선택: 방향키(위/아래)·마우스 호버로 항목을 고르고 Enter/클릭으로 실행한다.
        // 선택된 항목에는 보라색 빛이 맥동하며 남고, 빈 곳을 클릭해도 선택이 풀리지 않는다.
        private void SetupMenuSelection(Button[] buttons, int initialIndex)
        {
            menuButtons = buttons;
            menuGlows = new RawImage[buttons.Length];
            selectedIndex = initialIndex;

            var glowTexture = Resources.Load<Texture2D>("UI/Select_Effect");
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                button.transition = Selectable.Transition.None;

                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length];
                nav.selectOnDown = buttons[(i + 1) % buttons.Length];
                button.navigation = nav;

                button.gameObject.AddComponent<SelectOnHover>();

                // UI/Select_Effect.png(양 끝에 별빛이 있는 보라색 가로 줄)를 글자 뒤로 깐다.
                // Title.png의 메뉴 글자는 버튼 영역 가운데가 아니라 화면 x≈0.768에 있으므로 거기에 맞춘다.
                var glowGO = new GameObject("SelectGlow", typeof(RectTransform), typeof(RawImage));
                var glowRect = (RectTransform)glowGO.transform;
                glowRect.SetParent(button.transform, false);
                glowRect.anchorMin = glowRect.anchorMax = new Vector2(GlowAnchorX, 0.5f);
                glowRect.pivot = new Vector2(0.5f, 0.5f);
                glowRect.sizeDelta = GlowSize;
                var glow = glowGO.GetComponent<RawImage>();
                glow.texture = glowTexture;
                glow.color = GlowColor;
                glow.raycastTarget = false;
                glowGO.SetActive(false);
                menuGlows[i] = glow;
            }

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(buttons[selectedIndex].gameObject);
        }

        private void Update()
        {
            if (menuButtons == null) return;

            var eventSystem = EventSystem.current;
            bool dialogOpen = (confirmPanel != null && confirmPanel.activeSelf) || (loadScreen != null && loadScreen.IsOpen);
            if (eventSystem != null && !dialogOpen)
            {
                int index = System.Array.FindIndex(menuButtons, b => b.gameObject == eventSystem.currentSelectedGameObject);
                if (index >= 0) selectedIndex = index;
                else eventSystem.SetSelectedGameObject(menuButtons[selectedIndex].gameObject); // 선택이 풀렸으면 마지막 항목을 다시 선택
            }

            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            for (int i = 0; i < menuGlows.Length; i++)
            {
                bool selected = i == selectedIndex;
                if (menuGlows[i].gameObject.activeSelf != selected) menuGlows[i].gameObject.SetActive(selected);
                if (selected) menuGlows[i].color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, GlowColor.a * pulse);
            }
        }

        private class SelectOnHover : MonoBehaviour, IPointerEnterHandler
        {
            public void OnPointerEnter(PointerEventData eventData)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        internal static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}

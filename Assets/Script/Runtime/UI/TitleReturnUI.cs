using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Bootstrap;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 필드 Esc 일시정지 메뉴: [계속하기 / 설정 / 타이틀로]. 판·버튼은 공통 팝업 그림(UI/Common/Popup)을 쓴다.
    //  계속하기 = 닫기, 설정 = 설정 창(SettingsUI)을 띄우고 닫으면 이 메뉴로 돌아온다,
    //  타이틀로 = "타이틀 화면으로 돌아가시겠습니까?"(ChoicePopup)에 예를 고르면 자동 저장 후 타이틀로.
    // 조작: ↑↓(W/S) 고르기, Z/Enter 누르기, Esc/X 계속하기. 마우스는 올리면 고르고 누르면 실행.
    // 인벤토리/상점/대화/리듬 전투 등 다른 창이 열려 있으면(조작이 잠겨 있으면) 열지 않는다 — 그 창들은 Esc로
    // 닫히는데, 닫으려고 누른 Esc가 같은 프레임에 이 메뉴까지 열지 않도록 직전 프레임에도 잠겨 있지 않았을 때만 연다.
    public class TitleReturnUI : MonoBehaviour
    {
        private static readonly string[] ItemNames = { "계속하기", "설정", "타이틀로" };
        private const float PanelWidth = 600f;
        private const float ButtonWidth = 214f;
        private const float OpenDuration = 0.22f;
        private static readonly Color LabelColor = new(0.86f, 0.82f, 0.92f);
        private static readonly Color SelectedLabelColor = new(1f, 0.95f, 0.8f);
        private static readonly Color UnselectedTint = new(0.62f, 0.58f, 0.68f);
        private static readonly Color GlowColor = new(0.68f, 0.38f, 1f, 0.5f);

        private Character player;
        private CharacterMovement2D playerMovement;
        private bool wasBlockedLastFrame = true;

        private GameObject root;
        private CanvasGroup rootGroup;
        private Image dim;
        private RectTransform panel;
        private readonly Image[] buttonImages = new Image[3];
        private readonly Image[] glows = new Image[3];
        private readonly Text[] labels = new Text[3];
        private SettingsUI settings;
        private int selected;
        private int openedFrame = -1;
        private float openTime;
        private bool menuOpen;      // 메뉴(또는 그 위의 설정 창·확인 팝업)가 떠 있는 동안 true
        private float savedTimeScale = 1f;

        public bool IsOpen => menuOpen;

        public void Initialize(Character character, CharacterMovement2D movement)
        {
            player = character;
            playerMovement = movement;
            Build();
        }

        private void Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root = new GameObject("PauseMenu", typeof(RectTransform), typeof(Image));
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(transform, false);
            Stretch(rootRect);
            dim = root.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            rootGroup = root.AddComponent<CanvasGroup>();

            var panelSprite = Resources.Load<Sprite>("UI/Common/Popup/popup_panel");
            panel = NewRect("Panel", rootRect);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelWidth, panelSprite != null ? PanelWidth * panelSprite.rect.height / panelSprite.rect.width : 290f);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = panelSprite;
            panelImage.color = panelSprite != null ? Color.white : new Color(0.06f, 0.04f, 0.09f, 0.97f);

            var title = MakeText(panel, "Title", "일시정지", 22, new Color(0.85f, 0.72f, 1f), font);
            title.rectTransform.anchorMin = new Vector2(0f, 0.72f);
            title.rectTransform.anchorMax = new Vector2(1f, 0.86f);

            var buttonSprite = Resources.Load<Sprite>("UI/Common/Popup/popup_button");
            float buttonHeight = buttonSprite != null ? ButtonWidth * buttonSprite.rect.height / buttonSprite.rect.width : 48f;
            var glowSprite = MakeSoftGlow();
            for (int i = 0; i < ItemNames.Length; i++)
            {
                int index = i;
                var anchor = new Vector2(0.5f, 0.57f - i * 0.19f);
                var glowRect = NewRect(ItemNames[i] + "Glow", panel);
                glowRect.anchorMin = glowRect.anchorMax = anchor;
                glowRect.sizeDelta = new Vector2(ButtonWidth * 1.25f, buttonHeight * 1.9f);
                glows[i] = glowRect.gameObject.AddComponent<Image>();
                glows[i].sprite = glowSprite;
                glows[i].color = GlowColor;
                glows[i].raycastTarget = false;

                var rect = NewRect(ItemNames[i], panel);
                rect.anchorMin = rect.anchorMax = anchor;
                rect.sizeDelta = new Vector2(ButtonWidth, buttonHeight);
                buttonImages[i] = rect.gameObject.AddComponent<Image>();
                buttonImages[i].sprite = buttonSprite;
                labels[i] = MakeText(rect, "Label", ItemNames[i], 18, LabelColor, font);
                var pointer = rect.gameObject.AddComponent<Pointer>();
                pointer.enter = () => Select(index);
                pointer.down = () => { Select(index); Activate(index); };
            }

            settings = SettingsUI.Create(transform);
            root.SetActive(false);
        }

        // ---------------------------------------------------------------- 열고 닫기

        private void OpenMenu()
        {
            if (!menuOpen)
            {
                savedTimeScale = Time.timeScale;
                menuOpen = true;
                if (playerMovement != null) playerMovement.SetInputLocked(true);
                Time.timeScale = 0f;
            }
            TitleScreenUI.EnsureEventSystem();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            root.transform.SetAsLastSibling(); // 다른 HUD 위에 그린다
            root.SetActive(true);
            selected = 0;
            openedFrame = Time.frameCount;
            openTime = Time.unscaledTime;
            Refresh();
            Animate();
        }

        private void Resume()
        {
            root.SetActive(false);
            menuOpen = false;
            wasBlockedLastFrame = true; // 닫은 그 Esc로 바로 다시 열리지 않게
            Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void Activate(int index)
        {
            switch (index)
            {
                case 0:
                    Resume();
                    break;
                case 1:
                    root.SetActive(false);
                    settings.Open(() => OpenMenu());
                    break;
                case 2:
                    ChoicePopup.Ask("타이틀 화면으로 돌아가시겠습니까?", ConfirmTitle, null, subMessage: "지금까지의 진행은 자동 저장됩니다");
                    break;
            }
        }

        private void ConfirmTitle()
        {
            root.SetActive(false);
            menuOpen = false;
            GameBootstrap.SaveGame();
            GameBootstrap.ReturnToTitle(); // HUD(이 컴포넌트 포함)를 통째로 지우고 timeScale도 되돌린다
        }

        // ---------------------------------------------------------------- 매 프레임

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (menuOpen)
            {
                if (!root.activeSelf) return; // 설정 창이 떠 있다(닫히면 OpenMenu로 돌아온다)
                Animate();
                if (Time.frameCount == openedFrame || ChoicePopup.ConsumesInput || settings.ClosedThisFrame) return;

                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) Select((selected + 2) % 3);
                else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) Select((selected + 1) % 3);
                else if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Activate(selected);
                else if (keyboard.escapeKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame) Resume();
                return;
            }

            // 다른 곳의 팝업이 떠 있거나 방금 닫혔으면(닫은 그 Esc로 바로 열리지 않게) 다음 프레임까지 기다린다.
            if (ChoicePopup.ConsumesInput)
            {
                wasBlockedLastFrame = true;
                return;
            }

            bool blocked = player == null || player.IsDead
                || (playerMovement != null && playerMovement.IsInputLocked)
                || Time.timeScale == 0f;

            if (!blocked && !wasBlockedLastFrame && keyboard.escapeKey.wasPressedThisFrame)
            {
                OpenMenu();
                return;
            }
            wasBlockedLastFrame = blocked;
        }

        private void Select(int index)
        {
            if (selected == index) return;
            selected = index;
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = i == selected;
                glows[i].enabled = on;
                buttonImages[i].color = on ? Color.white : UnselectedTint;
                labels[i].color = on ? SelectedLabelColor : LabelColor;
            }
        }

        // 화면이 어두워지며 판이 작게 시작해 살짝 튕기듯 커진다. 고른 버튼 뒤 빛은 숨 쉬듯 일렁인다.
        private void Animate()
        {
            float k = Mathf.Clamp01((Time.unscaledTime - openTime) / OpenDuration);
            float m = k - 1f;
            float back = 1f + 2.5f * m * m * m + 1.5f * m * m;
            panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.82f, 1f, back);
            rootGroup.alpha = Mathf.Clamp01(k * 2.2f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            glows[selected].color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, GlowColor.a * pulse);
        }

        // ---------------------------------------------------------------- 도우미

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

        private static Text MakeText(Transform parent, string name, string content, int size, Color color, Font font)
        {
            var rect = NewRect(name, parent);
            Stretch(rect);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static Sprite MakeSoftGlow()
        {
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
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private class Pointer : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
        {
            public System.Action enter;
            public System.Action down;
            public void OnPointerEnter(PointerEventData eventData) => enter?.Invoke();
            public void OnPointerDown(PointerEventData eventData) => down?.Invoke();
        }
    }
}

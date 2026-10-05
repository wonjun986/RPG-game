using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Data;

namespace Aethoria.UI
{
    // 노아 대화에서 "[1] 상점"을 고르면 뜨는 구매 화면(UI/NoahsShopUI).
    // 조작: 방향키로 칸 선택(좌우로 끝까지 가면 탭도 넘어간다), Z/Enter 구매, X/Esc 닫기.
    public class ShopUI : MonoBehaviour
    {
        // UI/NoahsShopUI(1536x1024) 그림 속 실제 좌표를 픽셀로 재서 정규화한 값들.
        private const int GridColumns = 4;
        private const int GridRows = 3;
        private const float GridX0 = 678f / 1536f;
        private const float GridX1 = 1418f / 1536f;
        private const float GridTopFromBottom = 1f - 468f / 1024f;
        private const float GridBottomFromBottom = 1f - 937f / 1024f;

        // 무기/장비/장신구/기타 탭 중심 x(정규화), 탭 줄 y(정규화, 아래 기준).
        private static readonly float[] TabCenterX = { 766.5f / 1536f, 955.5f / 1536f, 1143.5f / 1536f, 1331.5f / 1536f };
        private const float TabRowYFromBottom = 1f - 550.5f / 1024f;
        private const float TabCursorYOffset = 0f;

        // 탭 하나가 어떤 카테고리들을 묶어서 보여주는지(InventoryUI의 5탭을 상점 아트의 4탭에 맞춰 합쳤다).
        private static readonly ItemCategory[][] TabFilters =
        {
            new[] { ItemCategory.Weapon },
            new[] { ItemCategory.Top, ItemCategory.Bottom, ItemCategory.Shoes },
            new[] { ItemCategory.Necklace, ItemCategory.Ring },
            new[] { ItemCategory.Materials, ItemCategory.Misc },
        };

        // 골드 뱃지 안의 "12,345"는 배경 그림에 미리 그려진 숫자라, 그 위에 어두운 판을 깔고
        // 실제 보유 골드를 새로 그려서 가린다(코인 아이콘 자리는 그대로 살려 둔다).
        private static readonly Color GoldMaskColor = new(0.04f, 0.025f, 0.06f, 1f);

        private Character player;
        private CharacterMovement2D playerMovement;
        private Inventory inventory;

        private GameObject panel;
        private Text goldText;
        private RectTransform tabCursor;
        private RawImage tabCursorImage;
        private RectTransform gridHighlight;
        private RawImage gridHighlightImage;
        private readonly Image[] gridIcons = new Image[GridColumns * GridRows];
        private readonly Text[] gridNameLabels = new Text[GridColumns * GridRows];
        private readonly Text[] gridPriceLabels = new Text[GridColumns * GridRows];

        private int currentTab;
        private List<ItemData> currentItems = new();
        private int selectedIndex = -1;
        private int openedFrame = -1;

        private GameObject messagePanel;
        private Text messageText;
        private float messageTimer;
        private const float MessageDuration = 1.4f;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize(Character character, CharacterMovement2D movement)
        {
            player = character;
            playerMovement = movement;
            inventory = character.GetComponent<Inventory>();
            BuildPanel();
        }

        private void BuildPanel()
        {
            TitleScreenUI.EnsureEventSystem();

            panel = new GameObject("ShopPanel", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/NoahsShopUI");

            BuildGoldDisplay(rect);
            BuildTabCursor(rect);
            BuildGridHighlight(rect); // 아이콘보다 먼저 만들어서 뒤에 깔리게 한다.
            BuildGrid(rect);
            BuildMessage(rect); // 맨 마지막에 만들어서 그리드 위에 뜨게 한다.

            panel.SetActive(false);
        }

        // 골드 부족 등 안내 문구. 그리드 한가운데에 잠깐 떴다가 사라진다.
        private void BuildMessage(Transform parent)
        {
            messagePanel = new GameObject("Message", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)messagePanel.transform;
            rect.SetParent(parent, false);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchorMin = rect.anchorMax = new Vector2((GridX0 + GridX1) / 2f, (GridTopFromBottom + GridBottomFromBottom) / 2f);
            rect.sizeDelta = new Vector2(460f, 70f);
            messagePanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            messageText = textGO.GetComponent<Text>();
            messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            messageText.fontSize = 22;
            messageText.fontStyle = FontStyle.Bold;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.color = new Color(1f, 0.4f, 0.4f);

            messagePanel.SetActive(false);
        }

        private void ShowMessage(string text)
        {
            messageText.text = text;
            messagePanel.SetActive(true);
            messageTimer = MessageDuration;
        }

        private void BuildGoldDisplay(Transform parent)
        {
            var maskGO = new GameObject("GoldMask", typeof(RectTransform), typeof(Image));
            var maskRect = (RectTransform)maskGO.transform;
            maskRect.SetParent(parent, false);
            maskRect.anchorMin = new Vector2(1f, 1f);
            maskRect.anchorMax = new Vector2(1f, 1f);
            maskRect.pivot = new Vector2(1f, 1f);
            maskRect.anchoredPosition = new Vector2(-115f, -191f);
            maskRect.sizeDelta = new Vector2(140f, 34f);
            maskGO.GetComponent<Image>().color = GoldMaskColor;

            var textGO = new GameObject("GoldText", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(parent, false);
            textRect.anchorMin = new Vector2(1f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(1f, 1f);
            textRect.anchoredPosition = new Vector2(-115f, -191f);
            textRect.sizeDelta = new Vector2(140f, 34f);

            goldText = textGO.GetComponent<Text>();
            goldText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            goldText.fontSize = 26;
            goldText.fontStyle = FontStyle.Bold;
            goldText.alignment = TextAnchor.MiddleCenter;
            goldText.color = new Color(1f, 0.85f, 0.3f);
        }

        private void BuildTabCursor(Transform parent)
        {
            var go = new GameObject("TabCursor", typeof(RectTransform), typeof(RawImage));
            tabCursor = (RectTransform)go.transform;
            tabCursor.SetParent(parent, false);
            tabCursor.pivot = new Vector2(0.5f, 0.5f);
            tabCursor.sizeDelta = new Vector2(150f, 50f);
            tabCursor.anchorMin = tabCursor.anchorMax = new Vector2(TabCenterX[0], TabRowYFromBottom);
            tabCursor.anchoredPosition = new Vector2(0f, TabCursorYOffset);

            tabCursorImage = go.GetComponent<RawImage>();
            tabCursorImage.texture = Resources.Load<Texture2D>("UI/Select_Effect");
            tabCursorImage.raycastTarget = false;
        }

        // 4x3 상품 칸. 좌표는 배경 그림을 픽셀 단위로 재서 계산했다.
        private void BuildGrid(Transform parent)
        {
            float colWidth = (GridX1 - GridX0) / GridColumns;
            float rowHeight = (GridTopFromBottom - GridBottomFromBottom) / GridRows;

            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridColumns; col++)
                {
                    int index = row * GridColumns + col;
                    float centerX = GridX0 + (col + 0.5f) * colWidth;
                    float centerY = GridTopFromBottom - (row + 0.5f) * rowHeight;

                    var go = new GameObject($"Slot_{row}_{col}", typeof(RectTransform), typeof(Image));
                    var rect = (RectTransform)go.transform;
                    rect.SetParent(parent, false);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchorMin = rect.anchorMax = new Vector2(centerX, centerY);
                    rect.anchoredPosition = new Vector2(0f, 12f);
                    rect.sizeDelta = new Vector2(80f, 80f);

                    var image = go.GetComponent<Image>();
                    image.preserveAspect = true;
                    image.enabled = false;
                    gridIcons[index] = image;

                    // 전용 아이콘 아트가 없는 아이템은 이 자리에 이름을 글자로 대신 보여준다.
                    var nameGO = new GameObject("Name", typeof(RectTransform), typeof(Text));
                    var nameRect = (RectTransform)nameGO.transform;
                    nameRect.SetParent(parent, false);
                    nameRect.pivot = new Vector2(0.5f, 0.5f);
                    nameRect.anchorMin = nameRect.anchorMax = new Vector2(centerX, centerY);
                    nameRect.anchoredPosition = new Vector2(0f, 12f);
                    nameRect.sizeDelta = new Vector2(colWidth * 1536f - 16f, 80f);
                    var nameText = nameGO.GetComponent<Text>();
                    nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    nameText.fontSize = 17;
                    nameText.fontStyle = FontStyle.Bold;
                    nameText.alignment = TextAnchor.MiddleCenter;
                    nameText.color = Color.white;
                    nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    nameText.enabled = false;
                    gridNameLabels[index] = nameText;

                    var priceGO = new GameObject("Price", typeof(RectTransform), typeof(Text));
                    var priceRect = (RectTransform)priceGO.transform;
                    priceRect.SetParent(parent, false);
                    priceRect.pivot = new Vector2(0.5f, 0.5f);
                    priceRect.anchorMin = priceRect.anchorMax = new Vector2(centerX, centerY);
                    priceRect.anchoredPosition = new Vector2(0f, -46f);
                    priceRect.sizeDelta = new Vector2(150f, 24f);
                    var priceText = priceGO.GetComponent<Text>();
                    priceText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    priceText.fontSize = 15;
                    priceText.fontStyle = FontStyle.Bold;
                    priceText.alignment = TextAnchor.MiddleCenter;
                    priceText.color = new Color(1f, 0.85f, 0.3f);
                    priceText.enabled = false;
                    gridPriceLabels[index] = priceText;
                }
            }
        }

        private void BuildGridHighlight(Transform parent)
        {
            var go = new GameObject("GridHighlight", typeof(RectTransform), typeof(RawImage));
            gridHighlight = (RectTransform)go.transform;
            gridHighlight.SetParent(parent, false);
            gridHighlight.pivot = new Vector2(0.5f, 0.5f);
            gridHighlight.anchoredPosition = Vector2.zero;
            gridHighlight.sizeDelta = new Vector2(120f, 142f); // ItemSelectEffect2 원본 비율(271x319)에 맞춤

            gridHighlightImage = go.GetComponent<RawImage>();
            gridHighlightImage.texture = Resources.Load<Texture2D>("UI/ItemSelectEffect2");
            gridHighlightImage.raycastTarget = false;
            gridHighlight.gameObject.SetActive(false);
        }

        public void Show()
        {
            currentTab = 0;
            openedFrame = Time.frameCount;
            messagePanel.SetActive(false);
            RefreshGold();
            RefreshTab();
            panel.SetActive(true);
            // InventoryUI와 같은 이유로, 나중에 추가된 HUD 요소가 위에 겹쳐 보이지 않도록 맨 앞으로 올린다.
            panel.transform.SetAsLastSibling();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void RefreshGold()
        {
            if (goldText != null && player != null) goldText.text = player.Gold.ToString("N0");
        }

        private void RefreshTab()
        {
            var categories = TabFilters[currentTab];
            currentItems = ItemDatabase.AllItems
                .Where(item => categories.Contains(item.category) && item.forSale)
                .OrderBy(item => item.shopPrice).ToList();

            for (int i = 0; i < gridIcons.Length; i++)
            {
                if (i < currentItems.Count)
                {
                    var item = currentItems[i];
                    // 아이콘 아트가 없으면(iconPath 비어있음) 이름을 글자로 대신 보여준다.
                    bool hasIcon = !string.IsNullOrEmpty(item.iconPath);
                    gridIcons[i].sprite = hasIcon ? Resources.Load<Sprite>(item.iconPath) : null;
                    gridIcons[i].enabled = hasIcon;
                    gridNameLabels[i].text = item.itemName;
                    gridNameLabels[i].enabled = !hasIcon;

                    bool owned = inventory.GetCount(item.id) > 0;
                    gridPriceLabels[i].text = owned ? "구매완료" : item.shopPrice > 0 ? item.shopPrice.ToString("N0") + " G" : "무료";
                    gridPriceLabels[i].color = owned ? Color.gray : new Color(1f, 0.85f, 0.3f);
                    gridPriceLabels[i].enabled = true;
                }
                else
                {
                    gridIcons[i].enabled = false;
                    gridNameLabels[i].enabled = false;
                    gridPriceLabels[i].enabled = false;
                }
            }

            tabCursor.anchorMin = tabCursor.anchorMax = new Vector2(TabCenterX[currentTab], TabRowYFromBottom);
            tabCursor.anchoredPosition = new Vector2(0f, TabCursorYOffset);

            selectedIndex = currentItems.Count > 0 ? 0 : -1;
            RefreshSelectionHighlight();
        }

        private void RefreshSelectionHighlight()
        {
            if (selectedIndex < 0)
            {
                gridHighlight.gameObject.SetActive(false);
                return;
            }

            gridHighlight.gameObject.SetActive(true);
            gridHighlight.anchorMin = gridHighlight.anchorMax = gridIcons[selectedIndex].rectTransform.anchorMin;
        }

        private void ChangeTab(int delta)
        {
            currentTab = (currentTab + delta + TabFilters.Length) % TabFilters.Length;
            RefreshTab();
        }

        // 좌우로 grid 끝을 넘어가면 Q/E 대신 이 방향키가 탭까지 넘겨준다(위아래는 같은 탭 안에서만 움직인다).
        private void MoveSelection(int dCol, int dRow)
        {
            if (selectedIndex < 0)
            {
                if (dCol != 0) ChangeTab(dCol);
                return;
            }

            int row = selectedIndex / GridColumns;
            int col = selectedIndex % GridColumns;
            int rawCol = col + dCol;

            if (rawCol < 0 || rawCol >= GridColumns)
            {
                ChangeTab(dCol);
                return;
            }

            int newRow = Mathf.Clamp(row + dRow, 0, GridRows - 1);
            int newIndex = newRow * GridColumns + rawCol;

            if (newIndex >= 0 && newIndex < currentItems.Count)
            {
                selectedIndex = newIndex;
                RefreshSelectionHighlight();
            }
        }

        private void UpdateGlowPulse()
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            tabCursorImage.color = new Color(1f, 1f, 1f, pulse);
            if (gridHighlight.gameObject.activeSelf)
            {
                gridHighlightImage.color = new Color(1f, 1f, 1f, pulse);
            }
        }

        private void TryBuySelected()
        {
            if (selectedIndex < 0 || selectedIndex >= currentItems.Count) return;
            if (player == null || inventory == null) return;

            var item = currentItems[selectedIndex];
            if (inventory.GetCount(item.id) > 0)
            {
                ShowMessage("이미 보유한 아이템입니다.");
                return;
            }

            // 무료 아이템(0골드)은 TrySpendGold(0)이 실패로 취급하므로 그냥 바로 지급한다.
            if (item.shopPrice > 0 && !player.TrySpendGold(item.shopPrice))
            {
                ShowMessage("골드가 부족합니다.");
                return;
            }

            inventory.AddItem(item);
            RefreshGold();
            gridPriceLabels[selectedIndex].text = "구매완료";
            gridPriceLabels[selectedIndex].color = Color.gray;
        }

        private void Update()
        {
            if (!IsOpen) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            UpdateGlowPulse();

            if (messagePanel.activeSelf)
            {
                messageTimer -= Time.unscaledDeltaTime;
                if (messageTimer <= 0f) messagePanel.SetActive(false);
            }

            // 상점을 연 그 프레임의 입력(Z 등)이 같은 프레임에 다시 소비되어 바로 닫히지 않게 한다.
            if (Time.frameCount == openedFrame) return;

            if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                Hide();
                return;
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame) MoveSelection(-1, 0);
            else if (keyboard.rightArrowKey.wasPressedThisFrame) MoveSelection(1, 0);
            else if (keyboard.upArrowKey.wasPressedThisFrame) MoveSelection(0, -1);
            else if (keyboard.downArrowKey.wasPressedThisFrame) MoveSelection(0, 1);

            if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                TryBuySelected();
            }
        }
    }
}

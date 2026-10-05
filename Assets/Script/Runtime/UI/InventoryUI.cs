using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Data;

namespace Aethoria.UI
{
    // I키로 여닫는 인벤토리 화면(UI/Item_Screen). 월드맵/저장 화면과 같은 방식으로 여는 동안
    // 시간이 멈추고 캐릭터 조작이 잠긴다.
    // 조작: Q/E 탭 전환, 방향키로 칸 선택, Z/Enter 장착·해제(확인 창에서 Z 예 / X 아니오), 장비 칸 클릭 = 해제, I/Esc 닫기.
    // 칸에 마우스를 올리거나 방향키로 칸을 옮기면 그 칸 바로 옆에 아이템 상세 정보가 뜬다.
    public class InventoryUI : MonoBehaviour
    {
        // UI/Item_Screen(1536x1024) 그림 속 실제 좌표를 픽셀로 재서 정규화한 값들.
        private const int GridColumns = 6;
        private const int GridRows = 5;
        private const float GridX0 = 740f / 1536f;
        private const float GridX1 = 1382f / 1536f;
        private const float GridTopFromBottom = 1f - 320f / 1024f;
        private const float GridBottomFromBottom = 1f - 875f / 1024f;

        // 칸 좌표는 배경 그림을 픽셀 단위로 재서 계산했는데, 실제로 보면 칸보다 아이콘이 살짝 위에
        // 걸려 보여서(측정 오차) 조금 내려서 보정한다.
        private const float IconYNudge = -10f;

        // 왼쪽 장비 칸 6개는 간격이 일정하지 않아서(칸마다 높이가 105~116px로 다름) 균등 분배하면
        // 아래로 갈수록 한 칸씩 밀렸다(하의가 신발 칸에 들어감). 칸마다 테두리 안쪽, 이름 글자 위
        // 빈 공간의 중심을 그림에서 직접 잰 값을 쓴다.
        private const float EquipColumnX = 607.5f / 1536f;
        private static readonly float[] EquipSlotCenterYPx = { 190f, 315f, 437f, 565f, 691f, 821f };
        private const float EquipIconSize = 50f;

        // 무기/옷/장신구/재료/기타 탭 중심 x(정규화), 탭 줄 y(정규화, 아래 기준).
        private static readonly float[] TabCenterX = { 809f / 1536f, 937f / 1536f, 1065f / 1536f, 1193f / 1536f, 1321f / 1536f };
        private const float TabRowYFromBottom = 1f - 295f / 1024f;

        // 탭 하나가 어떤 카테고리들을 묶어서 보여주는지. 장비 칸(무기/상의/하의/신발/목걸이/반지)과
        // 순서를 맞춘 배열이 EquipCategories.
        private static readonly ItemCategory[][] TabFilters =
        {
            new[] { ItemCategory.Weapon },
            new[] { ItemCategory.Top, ItemCategory.Bottom, ItemCategory.Shoes },
            new[] { ItemCategory.Necklace, ItemCategory.Ring },
            new[] { ItemCategory.Materials },
            new[] { ItemCategory.Misc },
        };

        private static readonly ItemCategory[] EquipCategories =
        {
            ItemCategory.Weapon, ItemCategory.Top, ItemCategory.Bottom,
            ItemCategory.Shoes, ItemCategory.Necklace, ItemCategory.Ring,
        };

        private Character player;
        private CharacterMovement2D playerMovement;
        private Inventory inventory;

        private const float TabCursorYOffset = 0f; // 탭 글자 밑이 아니라 글자 줄 높이에 바로 겹치게 둔다.

        private GameObject panel;
        private Text goldText;
        private RectTransform tabCursor;
        private RawImage tabCursorImage;
        private RectTransform gridHighlight;
        private RawImage gridHighlightImage;
        private readonly Image[] equipIcons = new Image[EquipCategories.Length];
        private readonly Image[] gridIcons = new Image[GridColumns * GridRows];
        private ItemDetailPanel detailPanel;

        // 아이템 상세 정보 창. 칸에 마우스를 올리면(키보드로 칸을 옮겨도) 그 칸 바로 오른쪽에 뜨고,
        // 오른쪽에 자리가 없으면 왼쪽에 뜬다. 너비는 캔버스 단위(기준 해상도 1280x720), 높이는 그림 비율대로
        // 정해진다(약 467).
        private const float DetailPanelWidth = 440f;
        private const float DetailPanelGap = 6f;
        private const float DetailPanelScreenMargin = 6f;

        // 장비를 누르면 바로 장착/해제하지 않고 "장착하시겠습니까?"/"해제하시겠습니까?" 확인 창을 띄운다.
        private GameObject confirmPanel;
        private Text confirmMessage;
        private ItemData pendingItem;
        private bool pendingUnequip;
        private bool IsConfirming => confirmPanel != null && confirmPanel.activeSelf;

        private int currentTab;
        private List<(ItemData item, int count)> currentItems = new();
        private int selectedIndex = -1;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize(Character character, CharacterMovement2D movement)
        {
            player = character;
            playerMovement = movement;
            inventory = character.GetComponent<Inventory>();
            BuildPanel();

            // 장착이 실제로 반영됐다는 걸 눈으로 바로 알 수 있게, 이미 장착 중인 걸 다시 눌러도
            // (아무 상태 변화가 없어도) 매번 장비 칸 아이콘이 한 번 통통 튄다.
            if (inventory != null)
            {
                inventory.OnEquipped += HandleEquipped;
                inventory.OnUnequipped += _ => RefreshEquipIcons();
            }
        }

        private Coroutine popRoutine;
        private RectTransform poppingIcon;

        private void HandleEquipped(ItemData item)
        {
            int index = System.Array.IndexOf(EquipCategories, item.category);
            if (index < 0) return;

            RefreshEquipIcons();

            // 튀어 오르던 도중에 다른 칸을 또 장착하면 멈춘 자리(확대된 크기)에 그대로 남아있었다
            // ("사신의 낫 크기가 안 맞는다"던 문제) — 멈추기 전에 원래 크기로 되돌려 둔다.
            if (popRoutine != null)
            {
                StopCoroutine(popRoutine);
                if (poppingIcon != null) poppingIcon.localScale = Vector3.one;
            }

            poppingIcon = equipIcons[index].rectTransform;
            popRoutine = StartCoroutine(PopEquipIcon(poppingIcon));
        }

        private System.Collections.IEnumerator PopEquipIcon(RectTransform iconRect)
        {
            const float duration = 0.18f;
            Vector3 baseScale = Vector3.one;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float bump = 1f + 0.35f * Mathf.Sin(t * Mathf.PI);
                iconRect.localScale = baseScale * bump;
                yield return null;
            }
            iconRect.localScale = baseScale;
            popRoutine = null;
            poppingIcon = null;
        }

        private void BuildPanel()
        {
            TitleScreenUI.EnsureEventSystem();

            panel = new GameObject("InventoryPanel", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/Item_Screen");

            BuildGoldDisplay(rect);
            BuildTabCursor(rect);
            BuildEquipSlots(rect);
            BuildGridHighlight(rect); // 아이콘보다 먼저 만들어서 뒤에 깔리게 한다.
            BuildGrid(rect);
            BuildDetailPanel(rect);
            BuildEquipConfirm(rect); // 가장 마지막에 만들어서 다른 모든 요소 위에 덮이게 한다.

            panel.SetActive(false);
        }

        // 화면 오른쪽 위, 장식 테두리 안쪽 빈 공간에 주머니 아이콘과 보유 골드를 보여준다.
        private void BuildGoldDisplay(Transform parent)
        {
            var iconGO = new GameObject("GoldIcon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)iconGO.transform;
            iconRect.SetParent(parent, false);
            iconRect.anchorMin = new Vector2(1f, 1f);
            iconRect.anchorMax = new Vector2(1f, 1f);
            iconRect.pivot = new Vector2(1f, 1f);
            iconRect.anchoredPosition = new Vector2(-70f, -70f);
            iconRect.sizeDelta = new Vector2(40f, 40f);
            var iconImage = iconGO.GetComponent<Image>();
            iconImage.sprite = Resources.Load<Sprite>("UI/Money");
            iconImage.preserveAspect = true;

            var textGO = new GameObject("GoldText", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(parent, false);
            textRect.anchorMin = new Vector2(1f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(1f, 1f);
            textRect.anchoredPosition = new Vector2(-118f, -76f);
            textRect.sizeDelta = new Vector2(160f, 30f);

            goldText = textGO.GetComponent<Text>();
            goldText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            goldText.fontSize = 24;
            goldText.fontStyle = FontStyle.Bold;
            goldText.alignment = TextAnchor.MiddleRight;
            goldText.color = new Color(1f, 0.85f, 0.3f);
        }

        // 지금 선택된 탭(무기/옷/장신구/재료/기타) 밑에 표시되는 빛나는 효과.
        private void BuildTabCursor(Transform parent)
        {
            var go = new GameObject("TabCursor", typeof(RectTransform), typeof(RawImage));
            tabCursor = (RectTransform)go.transform;
            tabCursor.SetParent(parent, false);
            tabCursor.pivot = new Vector2(0.5f, 0.5f);
            tabCursor.sizeDelta = new Vector2(140f, 18f);
            tabCursor.anchorMin = tabCursor.anchorMax = new Vector2(TabCenterX[0], TabRowYFromBottom);
            tabCursor.anchoredPosition = new Vector2(0f, TabCursorYOffset);

            tabCursorImage = go.GetComponent<RawImage>();
            tabCursorImage.texture = Resources.Load<Texture2D>("UI/Select_Effect");
            tabCursorImage.raycastTarget = false;
        }

        // 왼쪽 장비 칸(무기/상의/하의/신발/목걸이/반지) 6개. 장착된 아이템이 있으면 그 아이콘을
        // 배경에 이미 그려진 칸 그림 위에 덮어 보여주고, 없으면 꺼서 배경 그림이 그대로 보이게 한다.
        private void BuildEquipSlots(Transform parent)
        {
            int count = EquipCategories.Length;
            for (int i = 0; i < count; i++)
            {
                float y = 1f - EquipSlotCenterYPx[i] / 1024f;

                var go = new GameObject("Equip_" + EquipCategories[i], typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchorMin = rect.anchorMax = new Vector2(EquipColumnX, y);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.sizeDelta = new Vector2(EquipIconSize, EquipIconSize);

                var image = go.GetComponent<Image>();
                image.preserveAspect = false;
                image.enabled = false;
                equipIcons[i] = image;

                var interaction = go.AddComponent<EquipSlotInteraction>();
                interaction.owner = this;
                interaction.index = i;
            }
        }

        // 오른쪽 6x5 아이템 칸. 실제 좌표는 배경 그림을 픽셀 단위로 재서 계산했다.
        // 비어있는 칸은 Image를 꺼둬서(raycastTarget도 같이 꺼짐) 마우스 반응이 없게 한다.
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
                    rect.anchoredPosition = new Vector2(0f, IconYNudge);
                    rect.localScale = Vector3.one;
                    rect.sizeDelta = new Vector2(60f, 60f);

                    var image = go.GetComponent<Image>();
                    image.preserveAspect = false;
                    image.enabled = false;
                    gridIcons[index] = image;

                    var interaction = go.AddComponent<SlotInteraction>();
                    interaction.owner = this;
                    interaction.index = index;
                }
            }
        }

        private void BuildDetailPanel(RectTransform parent)
        {
            detailPanel = ItemDetailPanel.Create(parent, DetailPanelWidth);
            var detailRect = detailPanel.Rect;
            detailRect.anchorMin = detailRect.anchorMax = new Vector2(0.5f, 0.5f);
        }

        // 장착 확인 창. 뒤를 어둡게 덮어서(클릭도 막음) 확인 창에만 집중하게 한다.
        // Z/Enter 또는 [예] = 장착, X/Esc 또는 [아니오] = 취소.
        private void BuildEquipConfirm(Transform parent)
        {
            confirmPanel = new GameObject("EquipConfirmPanel", typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)confirmPanel.transform;
            dimRect.SetParent(parent, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImage = confirmPanel.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.55f);
            dimImage.raycastTarget = true;

            var boxGO = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var boxRect = (RectTransform)boxGO.transform;
            boxRect.SetParent(confirmPanel.transform, false);
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(520f, 210f);
            boxGO.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.09f, 0.97f);

            var messageGO = new GameObject("Message", typeof(RectTransform), typeof(Text));
            var messageRect = (RectTransform)messageGO.transform;
            messageRect.SetParent(boxRect, false);
            messageRect.anchorMin = new Vector2(0f, 0.48f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.offsetMin = new Vector2(16f, 0f);
            messageRect.offsetMax = new Vector2(-16f, 0f);
            confirmMessage = messageGO.GetComponent<Text>();
            confirmMessage.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            confirmMessage.fontSize = 26;
            confirmMessage.fontStyle = FontStyle.Bold;
            confirmMessage.alignment = TextAnchor.MiddleCenter;
            confirmMessage.color = Color.white;

            WorldMapUI.BuildDialogButton(boxRect, "YesButton", "예 (Z)", new Vector2(0.08f, 0.12f), new Vector2(0.47f, 0.42f), ConfirmEquipYes);
            WorldMapUI.BuildDialogButton(boxRect, "NoButton", "아니오 (X)", new Vector2(0.53f, 0.12f), new Vector2(0.92f, 0.42f), ConfirmEquipNo);

            confirmPanel.SetActive(false);
        }

        // unequip이 true면 해제 확인, 아니면 장착 확인.
        private void ShowEquipConfirm(ItemData item, bool unequip)
        {
            pendingItem = item;
            pendingUnequip = unequip;
            confirmMessage.text = item.DisplayName(inventory.GetEnhanceLevel(item)) + (unequip ? "\n해제하시겠습니까?" : "\n장착하시겠습니까?");
            detailPanel.Hide();
            confirmPanel.SetActive(true);
        }

        private void ConfirmEquipYes()
        {
            var item = pendingItem;
            bool unequip = pendingUnequip;
            CloseEquipConfirm();
            if (item == null) return;

            // 장착은 OnEquipped 구독(HandleEquipped)에서, 해제는 OnUnequipped 구독에서 장비 칸 아이콘을 갱신한다.
            if (unequip) inventory.Unequip(item.category);
            else inventory.Equip(item);
        }

        private void ConfirmEquipNo() => CloseEquipConfirm();

        private void CloseEquipConfirm()
        {
            pendingItem = null;
            pendingUnequip = false;
            confirmPanel.SetActive(false);
        }

        // slot 바로 오른쪽(자리가 없으면 왼쪽)에 item의 상세 정보 창을 띄운다. 창 위쪽은 칸 위쪽에 맞추되
        // 화면 밖으로 나가지 않게 위아래를 잘라 맞춘다.
        private void ShowDetailNextTo(RectTransform slot, ItemData item)
        {
            if (item == null)
            {
                detailPanel.Hide();
                return;
            }

            var parentRect = (RectTransform)panel.transform;
            var detailRect = detailPanel.Rect;
            Vector2 size = detailRect.sizeDelta;

            var corners = new Vector3[4];
            slot.GetWorldCorners(corners); // 0: 왼쪽 아래, 2: 오른쪽 위
            Vector2 slotMin = parentRect.InverseTransformPoint(corners[0]);
            Vector2 slotMax = parentRect.InverseTransformPoint(corners[2]);
            Rect bounds = parentRect.rect;

            bool placeRight = slotMax.x + DetailPanelGap + size.x <= bounds.xMax - DetailPanelScreenMargin;
            float x = placeRight ? slotMax.x + DetailPanelGap : slotMin.x - DetailPanelGap;
            detailRect.pivot = new Vector2(placeRight ? 0f : 1f, 1f);

            float top = Mathf.Min(slotMax.y, bounds.yMax - DetailPanelScreenMargin);
            top = Mathf.Max(top, bounds.yMin + DetailPanelScreenMargin + size.y);

            detailRect.anchoredPosition = new Vector2(x, top);
            detailPanel.Show(item, inventory != null ? inventory.GetEnhanceLevel(item) : 0);
        }

        // 지금 선택된 칸 뒤에 깔리는 빛나는 효과(마우스로 칸을 고르면 같이 움직인다).
        private void BuildGridHighlight(Transform parent)
        {
            var go = new GameObject("GridHighlight", typeof(RectTransform), typeof(RawImage));
            gridHighlight = (RectTransform)go.transform;
            gridHighlight.SetParent(parent, false);
            gridHighlight.pivot = new Vector2(0.5f, 0.5f);
            gridHighlight.anchoredPosition = Vector2.zero;
            gridHighlight.localScale = Vector3.one;
            gridHighlight.sizeDelta = new Vector2(100f, 14f);

            gridHighlightImage = go.GetComponent<RawImage>();
            gridHighlightImage.texture = Resources.Load<Texture2D>("UI/Select_Effect");
            gridHighlightImage.raycastTarget = false;
            gridHighlight.gameObject.SetActive(false);
        }

        // 마우스로 아이템 칸을 고를 수 있게 한다. 호버하면 선택(키보드 방향키와 같은 효과),
        // 누르면 바로 장착(Z/Enter와 같은 효과)한다. 뗄 때 같은 칸 위에 있어야만 발동하는
        // OnPointerClick 대신 OnPointerDown을 쓴다 — 클릭 도중 살짝 흔들리기만 해도 눌림이
        // 씹히던 문제(선택은 되는데 장착이 안 되는 것처럼 보임) 때문.
        // 마우스가 칸을 벗어나면 상세 정보 창을 닫는다.
        private class SlotInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
        {
            public InventoryUI owner;
            public int index;

            public void OnPointerEnter(PointerEventData eventData) => owner.HandleSlotHover(index);
            public void OnPointerExit(PointerEventData eventData) => owner.detailPanel.Hide();
            public void OnPointerDown(PointerEventData eventData) => owner.HandleSlotClick(index);
        }

        // 왼쪽 장비 칸에 마우스를 올리면 장착 중인 아이템의 상세 정보를 보여주고, 누르면 해제 확인 창을 띄운다
        // (빈 칸은 Image가 꺼져 있어 반응 없음).
        private class EquipSlotInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
        {
            public InventoryUI owner;
            public int index;

            public void OnPointerEnter(PointerEventData eventData) => owner.HandleEquipSlotHover(index);
            public void OnPointerExit(PointerEventData eventData) => owner.detailPanel.Hide();
            public void OnPointerDown(PointerEventData eventData) => owner.HandleEquipSlotClick(index);
        }

        private void HandleSlotHover(int index)
        {
            if (index < 0 || index >= currentItems.Count) return;

            selectedIndex = index;
            RefreshSelectionHighlight();
            ShowSelectedDetail();
        }

        private void HandleEquipSlotHover(int index)
        {
            if (inventory == null) return;
            ShowDetailNextTo(equipIcons[index].rectTransform, inventory.GetEquipped(EquipCategories[index]));
        }

        private void HandleEquipSlotClick(int index)
        {
            if (inventory == null) return;
            var equipped = inventory.GetEquipped(EquipCategories[index]);
            if (equipped != null) ShowEquipConfirm(equipped, unequip: true);
        }

        private void ShowSelectedDetail()
        {
            if (selectedIndex < 0 || selectedIndex >= currentItems.Count)
            {
                detailPanel.Hide();
                return;
            }
            ShowDetailNextTo(gridIcons[selectedIndex].rectTransform, currentItems[selectedIndex].item);
        }

        private void HandleSlotClick(int index)
        {
            HandleSlotHover(index);
            TryEquipSelected();
        }

        public void Show()
        {
            currentTab = 0;
            RefreshGold();
            RefreshEquipIcons();
            RefreshTab();
            panel.SetActive(true);
            // 나중에 추가된 HUD 요소(퀘스트 트래커, 토스트 등)가 형제 순서상 더 위라 이 화면 위에
            // 겹쳐 보였다 — 열 때마다 맨 앞으로 올려서 다른 UI를 완전히 가린다.
            panel.transform.SetAsLastSibling();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            CloseEquipConfirm();
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void RefreshGold()
        {
            if (goldText != null && player != null) goldText.text = player.Gold.ToString("N0");
        }

        private void RefreshEquipIcons()
        {
            if (inventory == null) return;

            for (int i = 0; i < EquipCategories.Length; i++)
            {
                var equipped = inventory.GetEquipped(EquipCategories[i]);
                var icon = equipIcons[i];
                if (equipped != null)
                {
                    icon.sprite = Resources.Load<Sprite>(equipped.iconPath);
                    icon.enabled = true;
                }
                else
                {
                    icon.enabled = false;
                }
            }
        }

        private void RefreshTab()
        {
            var categories = TabFilters[currentTab];
            currentItems = inventory == null
                ? new List<(ItemData, int)>()
                : inventory.All().Where(entry => categories.Contains(entry.item.category))
                    .OrderBy(entry => entry.item.id).ToList();

            for (int i = 0; i < gridIcons.Length; i++)
            {
                if (i < currentItems.Count)
                {
                    gridIcons[i].sprite = Resources.Load<Sprite>(currentItems[i].item.iconPath);
                    gridIcons[i].enabled = true;
                }
                else
                {
                    gridIcons[i].enabled = false;
                }
            }

            tabCursor.anchorMin = tabCursor.anchorMax = new Vector2(TabCenterX[currentTab], TabRowYFromBottom);
            tabCursor.anchoredPosition = new Vector2(0f, TabCursorYOffset);

            selectedIndex = currentItems.Count > 0 ? 0 : -1;
            RefreshSelectionHighlight();
            detailPanel.Hide(); // 탭을 바꾸면 마우스를 다시 올리기 전까지는 상세 정보를 띄우지 않는다.
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

        private void MoveSelection(int dCol, int dRow)
        {
            if (selectedIndex < 0 || currentItems.Count == 0) return;

            int row = selectedIndex / GridColumns;
            int col = selectedIndex % GridColumns;
            int newCol = Mathf.Clamp(col + dCol, 0, GridColumns - 1);
            int newRow = Mathf.Clamp(row + dRow, 0, GridRows - 1);
            int newIndex = newRow * GridColumns + newCol;

            if (newIndex >= 0 && newIndex < currentItems.Count)
            {
                selectedIndex = newIndex;
                RefreshSelectionHighlight();
                ShowSelectedDetail();
            }
        }

        // 인벤토리가 열려 있는 동안(Time.timeScale=0) 탭/칸 표시가 은은하게 맥동하도록 실시간으로 잰다.
        private void UpdateGlowPulse()
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            tabCursorImage.color = new Color(1f, 1f, 1f, pulse);
            if (gridHighlight.gameObject.activeSelf)
            {
                gridHighlightImage.color = new Color(1f, 1f, 1f, pulse);
            }
        }

        private void TryEquipSelected()
        {
            if (selectedIndex < 0 || selectedIndex >= currentItems.Count) return;

            var item = currentItems[selectedIndex].item;
            if (!EquipCategories.Contains(item.category)) return;

            // 이미 장착 중인 장비를 다시 누르면 해제할지 묻는다.
            ShowEquipConfirm(item, unequip: inventory.IsEquipped(item));
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (IsOpen)
            {
                UpdateGlowPulse();

                if (IsConfirming)
                {
                    if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    {
                        ConfirmEquipYes();
                    }
                    else if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                    {
                        ConfirmEquipNo();
                    }
                    return;
                }

                if (keyboard.iKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                {
                    Hide();
                    return;
                }

                if (keyboard.qKey.wasPressedThisFrame) ChangeTab(-1);
                else if (keyboard.eKey.wasPressedThisFrame) ChangeTab(1);

                if (keyboard.leftArrowKey.wasPressedThisFrame) MoveSelection(-1, 0);
                else if (keyboard.rightArrowKey.wasPressedThisFrame) MoveSelection(1, 0);
                else if (keyboard.upArrowKey.wasPressedThisFrame) MoveSelection(0, -1);
                else if (keyboard.downArrowKey.wasPressedThisFrame) MoveSelection(0, 1);

                if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    TryEquipSelected();
                }
                return;
            }

            // 대화창/월드맵/저장 화면 등 다른 곳에서 이미 조작을 잠가 둔 상태면 열지 않는다.
            if (player == null || player.IsDead) return;
            if (playerMovement != null && playerMovement.IsInputLocked) return;

            if (keyboard.iKey.wasPressedThisFrame)
            {
                Show();
            }
        }
    }
}

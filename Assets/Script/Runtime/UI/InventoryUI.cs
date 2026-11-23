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
    // I키로 여닫는 인벤토리 화면. 월드맵/저장 화면과 같은 방식으로 여는 동안 시간이 멈추고 캐릭터 조작이 잠긴다.
    // 조작: Q/E 탭 전환, 방향키로 칸 선택, Z/Enter 장착·해제(확인 창에서 Z 예 / X 아니오), 장비 칸 클릭 = 해제, I/Esc 닫기.
    // 칸에 마우스를 올리거나 방향키로 칸을 옮기면 그 칸 바로 옆에 아이템 상세 정보가 뜬다.
    //
    // 화면은 부품(UI/Menus/Inventory)을 코드로 배치해 만든다 — 바탕 판(inv_panel) 위에 왼쪽은 캐릭터 전신(inv_portrait)과
    // 양옆 장비 칸 3개씩, 오른쪽은 위 탭 5개(inv_tab/inv_tab_on), 가운데 같은 크기의 칸 6x5(inv_slot), 아래 골드·조작 안내.
    // 칸 이름·탭 이름은 그림에 박지 않고 게임 글꼴로 쓴다. 모든 위치는 바탕 판 그림(1672x941) 속 픽셀 좌표로 잡는다.
    public class InventoryUI : MonoBehaviour
    {
        private const string Folder = "UI/Menus/Inventory/";
        private const float PanelW = 1672f, PanelH = 941f;

        private const int GridColumns = 6;
        private const int GridRows = 5;
        // 가운데 칸 영역(x 592~1596, y 170~780)에 110px 칸을 8px 간격으로 6x5 — 가운데 정렬.
        private const float SlotPx = 110f, SlotGapPx = 8f;
        private const float GridLeftPx = 592f + (1004f - (6f * 110f + 5f * 8f)) / 2f;
        private const float GridTopPx = 170f + (610f - (5f * 110f + 4f * 8f)) / 2f;
        private const float IconInset = 0.14f;       // 칸 테두리 안쪽 여백(칸 크기 비율)

        // 왼쪽 영역(x 75~565, y 103~850): 캐릭터 전신 양옆에 장비 칸 3개씩.
        private const float EquipSlotPx = 96f;
        private static readonly Vector2[] EquipSlotCenterPx =
        {
            new Vector2(150f, 250f), new Vector2(150f, 445f), new Vector2(150f, 640f),   // 무기, 상의, 하의
            new Vector2(490f, 250f), new Vector2(490f, 445f), new Vector2(490f, 640f),   // 신발, 목걸이, 반지
        };
        private static readonly string[] EquipSlotNames = { "무기", "상의", "하의", "신발", "목걸이", "반지" };
        private static readonly string[] EquipEmblems = { "weapon", "top", "bottom", "shoes", "necklace", "ring" };

        // 위 탭 판(x 610~1570, y 110~165). 장비 칸 6개와 하나씩 짝이 맞게 부위별로 나눈 탭 6개 + 재료·기타를 합친 탭 1개.
        // 판 폭(960)에 맞춰 탭 하나 폭 128px(높이는 그림 비율대로 약 39px), 간격 135px.
        private static readonly string[] TabNames = { "무기", "상의", "하의", "신발", "목걸이", "반지", "기타" };
        private const float TabCenterYPx = 138f, TabWidthPx = 128f, TabStepPx = 135f, TabRowCenterXPx = 1090f;

        private static readonly Color LabelColor = new Color(0.86f, 0.8f, 0.95f);
        private static readonly Color TabTextOff = new Color(0.7f, 0.64f, 0.8f);
        private static readonly Color TabTextOn = new Color(1f, 0.95f, 1f);

        // 탭 하나가 보여 주는 카테고리. 앞 6개는 장비 칸(EquipCategories)과 같은 순서이고, 마지막 "기타"에 재료와 기타를 함께 넣는다.
        private static readonly ItemCategory[][] TabFilters =
        {
            new[] { ItemCategory.Weapon },
            new[] { ItemCategory.Top },
            new[] { ItemCategory.Bottom },
            new[] { ItemCategory.Shoes },
            new[] { ItemCategory.Necklace },
            new[] { ItemCategory.Ring },
            new[] { ItemCategory.Materials, ItemCategory.Misc },
        };

        private static readonly ItemCategory[] EquipCategories =
        {
            ItemCategory.Weapon, ItemCategory.Top, ItemCategory.Bottom,
            ItemCategory.Shoes, ItemCategory.Necklace, ItemCategory.Ring,
        };

        private Character player;
        private CharacterMovement2D playerMovement;
        private Inventory inventory;

        private GameObject panel;
        private PanelTransition transition; // 스르륵 올라와 펼쳐지는 여닫기 연출
        private Text goldText;
        private Text nameText;
        private readonly Image[] tabImages = new Image[TabNames.Length];
        private readonly Text[] tabLabels = new Text[TabNames.Length];
        private Sprite tabSprite, tabOnSprite;
        private RectTransform gridHighlight;   // 선택 커서(inv_cursor)
        private Image gridHighlightImage;
        private readonly RectTransform[] gridFrames = new RectTransform[GridColumns * GridRows];
        private readonly Image[] equipEmblems = new Image[EquipCategories.Length];
        private readonly Image[] equipIcons = new Image[EquipCategories.Length];
        private readonly Image[] gridIcons = new Image[GridColumns * GridRows];
        // 아이콘 그림이 아직 없는 아이템은 칸 위에 이름을 글자로 대신 띄운다.
        private readonly Text[] equipNameLabels = new Text[EquipCategories.Length];
        private readonly Text[] gridNameLabels = new Text[GridColumns * GridRows];
        private ItemDetailPanel detailPanel;

        // 아이템 상세 정보 창. 칸에 마우스를 올리면(키보드로 칸을 옮겨도) 그 칸 바로 오른쪽에 뜨고,
        // 오른쪽에 자리가 없으면 왼쪽에 뜬다. 너비는 캔버스 단위(기준 해상도 1280x720), 높이는 그림 비율대로
        // 정해진다(약 467).
        private const float DetailPanelWidth = 440f;
        private const float DetailPanelGap = 6f;
        private const float DetailPanelScreenMargin = 6f;

        // 장비를 누르면 바로 장착/해제하지 않고 "장착하시겠습니까?"/"해제하시겠습니까?" 확인 창(공통 선택 팝업)을 띄운다.
        private ItemData pendingItem;
        private bool pendingUnequip;

        private int currentTab;
        private List<(ItemData item, int count)> currentItems = new();
        private int selectedIndex = -1;

        // 닫히는 연출 중에는 화면엔 보여도 이미 닫힌 것으로 본다(입력·시간 정지가 바로 풀린다).
        public bool IsOpen => panel != null && panel.activeSelf && (transition == null || !transition.IsClosing);

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
            panel.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(Folder + "inv_panel");
            transition = panel.AddComponent<PanelTransition>();

            BuildPortrait(rect);
            BuildEquipSlots(rect);
            BuildTabs(rect);
            BuildGrid(rect);
            BuildGridHighlight(rect); // 칸 위에 덮이는 선택 커서
            BuildBottomBar(rect);
            BuildDetailPanel(rect);

            panel.SetActive(false);
        }

        // ---- 바탕 판 그림(1672x941) 속 픽셀 상자 → 앵커 ----

        private static RectTransform PlaceBox(Transform parent, string name, float x0, float y0, float x1, float y1, params System.Type[] components)
        {
            var types = new System.Type[components.Length + 1];
            types[0] = typeof(RectTransform);
            components.CopyTo(types, 1);
            var go = new GameObject(name, types);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0 / PanelW, 1f - y1 / PanelH);
            rect.anchorMax = new Vector2(x1 / PanelW, 1f - y0 / PanelH);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform PlaceCentered(Transform parent, string name, float cx, float cy, float w, float h, params System.Type[] components)
            => PlaceBox(parent, name, cx - w / 2f, cy - h / 2f, cx + w / 2f, cy + h / 2f, components);

        private static Image AddImage(RectTransform rect, Sprite sprite, bool preserveAspect = true, bool raycast = false)
        {
            var image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = raycast;
            return image;
        }

        private static Text AddText(RectTransform rect, int size, Color color, TextAnchor anchor, bool bold = true)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            return text;
        }

        private static Sprite Load(string name) => Resources.Load<Sprite>(Folder + name);

        // 왼쪽: 캐릭터 전신(장비 칸 뒤)과 맨 아래 이름·레벨.
        private void BuildPortrait(Transform parent)
        {
            var portrait = PlaceBox(parent, "Portrait", 150f, 118f, 490f, 790f, typeof(Image));
            AddImage(portrait, Load("inv_portrait"));
            var nameRect = PlaceBox(parent, "CharacterName", 90f, 796f, 550f, 842f);
            nameText = AddText(nameRect, 22, new Color(0.95f, 0.88f, 1f), TextAnchor.MiddleCenter);
        }

        // 장비 칸 6개(무기/상의/하의/신발/목걸이/반지). 빈 칸에는 흐린 문양, 장착하면 아이템 아이콘. 칸 아래에 이름.
        private void BuildEquipSlots(Transform parent)
        {
            var slotSprite = Load("inv_slot");
            for (int i = 0; i < EquipCategories.Length; i++)
            {
                Vector2 c = EquipSlotCenterPx[i];
                var frame = PlaceCentered(parent, "Equip_" + EquipCategories[i], c.x, c.y, EquipSlotPx, EquipSlotPx, typeof(Image));
                AddImage(frame, slotSprite, preserveAspect: false, raycast: true);

                var emblemRect = Inset(frame, "Emblem", 0.2f);
                equipEmblems[i] = AddImage(emblemRect, Load("inv_equip_" + EquipEmblems[i]));
                equipEmblems[i].color = new Color(1f, 1f, 1f, 0.22f);

                var iconRect = Inset(frame, "Icon", IconInset);
                var icon = AddImage(iconRect, null, preserveAspect: true);
                icon.enabled = false;
                equipIcons[i] = icon;
                equipNameLabels[i] = CreateIconFallbackLabel(iconRect);

                var label = PlaceCentered(parent, "EquipLabel_" + i, c.x, c.y + EquipSlotPx / 2f + 16f, 140f, 26f);
                AddText(label, 15, LabelColor, TextAnchor.MiddleCenter).text = EquipSlotNames[i];

                var interaction = frame.gameObject.AddComponent<EquipSlotInteraction>();
                interaction.owner = this;
                interaction.index = i;
            }
        }

        private static RectTransform Inset(RectTransform parent, string name, float inset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(inset, inset);
            rect.anchorMax = new Vector2(1f - inset, 1f - inset);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        // 위 탭 5개. 고른 탭은 밝은 판(inv_tab_on)으로 바꾸고, 클릭으로도 고를 수 있다.
        private void BuildTabs(Transform parent)
        {
            tabSprite = Load("inv_tab");
            tabOnSprite = Load("inv_tab_on");
            float aspect = tabSprite != null ? tabSprite.rect.width / tabSprite.rect.height : 3.2f;
            for (int i = 0; i < TabNames.Length; i++)
            {
                float cx = TabRowCenterXPx + (i - (TabNames.Length - 1) / 2f) * TabStepPx;
                var tab = PlaceCentered(parent, "Tab_" + i, cx, TabCenterYPx, TabWidthPx, TabWidthPx / aspect, typeof(Image), typeof(Button));
                tabImages[i] = AddImage(tab, tabSprite, preserveAspect: true, raycast: true);
                var button = tab.GetComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                int index = i;
                button.onClick.AddListener(() => { currentTab = index; RefreshTab(); });

                var labelRect = Inset(tab, "Label", 0f);
                tabLabels[i] = AddText(labelRect, 15, TabTextOff, TextAnchor.MiddleCenter);
                tabLabels[i].text = TabNames[i];
            }
        }

        // 가운데 6x5 아이템 칸. 칸 그림(inv_slot)은 늘 보이고, 아이템이 있으면 그 위에 아이콘을 얹는다.
        private void BuildGrid(Transform parent)
        {
            var slotSprite = Load("inv_slot");
            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridColumns; col++)
                {
                    int index = row * GridColumns + col;
                    float x0 = GridLeftPx + col * (SlotPx + SlotGapPx);
                    float y0 = GridTopPx + row * (SlotPx + SlotGapPx);
                    var frame = PlaceBox(parent, $"Slot_{row}_{col}", x0, y0, x0 + SlotPx, y0 + SlotPx, typeof(Image));
                    AddImage(frame, slotSprite, preserveAspect: false, raycast: true);
                    gridFrames[index] = frame;

                    var iconRect = Inset(frame, "Icon", IconInset);
                    var image = AddImage(iconRect, null, preserveAspect: true);
                    image.enabled = false;
                    gridIcons[index] = image;
                    gridNameLabels[index] = CreateIconFallbackLabel(iconRect);

                    var interaction = frame.gameObject.AddComponent<SlotInteraction>();
                    interaction.owner = this;
                    interaction.index = index;
                }
            }
        }

        // 아래 골드 판(x 610~1570, y 785~843): 왼쪽에 조작 안내, 오른쪽에 주머니 아이콘과 보유 골드.
        private void BuildBottomBar(Transform parent)
        {
            var hint = PlaceBox(parent, "Hint", 640f, 790f, 1250f, 840f);
            AddText(hint, 14, new Color(0.72f, 0.66f, 0.82f), TextAnchor.MiddleLeft, bold: false).text =
                "Q / E  탭 전환     방향키  선택     Z  장착·해제     I  닫기";

            var icon = PlaceCentered(parent, "GoldIcon", 1330f, 814f, 40f, 40f, typeof(Image));
            AddImage(icon, Resources.Load<Sprite>("UI/HUD/Money"));
            var gold = PlaceBox(parent, "GoldText", 1355f, 790f, 1545f, 840f);
            goldText = AddText(gold, 22, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleRight);
        }

        // 아이콘 칸 위에 겹쳐 두는 이름 글자. 마우스 반응은 아래 아이콘 Image가 받는다.
        private static Text CreateIconFallbackLabel(RectTransform slot)
        {
            var go = new GameObject("NameFallback", typeof(RectTransform), typeof(Text), typeof(Outline));
            var rect = (RectTransform)go.transform;
            rect.SetParent(slot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-4f, 0f);
            rect.offsetMax = new Vector2(4f, 0f);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = string.Empty;
            go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            return text;
        }

        // 아이콘 칸 하나를 채운다. 그림이 있으면 그림을, 없으면 투명한 칸 위에 이름 글자를 띄운다.
        // 그림이 없어도 Image는 켜 둬야 마우스를 올려 상세 정보를 볼 수 있다.
        private static void FillIconSlot(Image icon, Text nameLabel, ItemData item)
        {
            if (item == null)
            {
                icon.enabled = false;
                nameLabel.text = string.Empty;
                return;
            }

            var sprite = item.LoadIcon();
            icon.sprite = sprite;
            icon.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            icon.enabled = true;
            nameLabel.text = sprite != null ? string.Empty : item.itemName;
        }

        private void BuildDetailPanel(RectTransform parent)
        {
            detailPanel = ItemDetailPanel.Create(parent, DetailPanelWidth);
            var detailRect = detailPanel.Rect;
            detailRect.anchorMin = detailRect.anchorMax = new Vector2(0.5f, 0.5f);
        }

        // unequip이 true면 해제 확인, 아니면 장착 확인.
        private void ShowEquipConfirm(ItemData item, bool unequip)
        {
            pendingItem = item;
            pendingUnequip = unequip;
            detailPanel.Hide();
            ChoicePopup.Show(new ChoicePopup.Request
            {
                Message = unequip ? "해제하시겠습니까?" : "장착하시겠습니까?",
                SubMessage = item.DisplayName(inventory.GetEnhanceLevel(item)),
                Icon = item.LoadIcon(),
                ConfirmLabel = unequip ? "해제" : "장착",
                CancelLabel = "취소",
                OnConfirm = ConfirmEquipYes,
                OnCancel = ConfirmEquipNo,
            });
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

            // 재료·기타는 짧은 창이라 크기가 달라서, 먼저 내용을 채운(크기가 정해진) 뒤에 자리를 잡는다.
            detailPanel.Show(item, inventory != null ? inventory.GetEnhanceLevel(item) : 0);
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
        }

        // 선택한 칸 위에 덮이는 빛나는 커서(inv_cursor). 고른 칸의 자식으로 옮겨 붙여 칸 크기에 맞춘다.
        private void BuildGridHighlight(Transform parent)
        {
            var go = new GameObject("SelectCursor", typeof(RectTransform), typeof(Image));
            gridHighlight = (RectTransform)go.transform;
            gridHighlight.SetParent(parent, false);
            gridHighlightImage = AddImage(gridHighlight, Load("inv_cursor"), preserveAspect: false);
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
            if (nameText != null && player != null) nameText.text = $"네크로시아   <size=16>Lv.{player.Level}</size>";
            RefreshEquipIcons();
            RefreshTab();
            transition.PlayOpen();
            // 나중에 추가된 HUD 요소(퀘스트 트래커, 토스트 등)가 형제 순서상 더 위라 이 화면 위에
            // 겹쳐 보였다 — 열 때마다 맨 앞으로 올려서 다른 UI를 완전히 가린다.
            panel.transform.SetAsLastSibling();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            CloseEquipConfirm();
            transition.PlayClose();
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
                FillIconSlot(equipIcons[i], equipNameLabels[i], equipped);
                if (equipEmblems[i] != null) equipEmblems[i].enabled = equipped == null; // 빈 칸에만 흐린 문양
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
                FillIconSlot(gridIcons[i], gridNameLabels[i], i < currentItems.Count ? currentItems[i].item : null);
            }

            for (int i = 0; i < tabImages.Length; i++)
            {
                bool on = i == currentTab;
                if (tabImages[i] != null) tabImages[i].sprite = on && tabOnSprite != null ? tabOnSprite : tabSprite;
                if (tabLabels[i] != null) tabLabels[i].color = on ? TabTextOn : TabTextOff;
            }

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

            // 고른 칸의 자식으로 붙여 칸보다 살짝 크게 덮는다(칸 그림이 화면 비율에 따라 늘어나도 같이 맞는다).
            gridHighlight.gameObject.SetActive(true);
            gridHighlight.SetParent(gridFrames[selectedIndex], false);
            gridHighlight.anchorMin = new Vector2(-0.09f, -0.09f);
            gridHighlight.anchorMax = new Vector2(1.09f, 1.09f);
            gridHighlight.offsetMin = gridHighlight.offsetMax = Vector2.zero;
            gridHighlight.SetAsLastSibling();
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
                if (newIndex != selectedIndex) UiSfx.Move();
                selectedIndex = newIndex;
                RefreshSelectionHighlight();
                ShowSelectedDetail();
            }
        }

        // 인벤토리가 열려 있는 동안(Time.timeScale=0) 탭/칸 표시가 은은하게 맥동하도록 실시간으로 잰다.
        private void UpdateGlowPulse()
        {
            float wave = Mathf.Sin(Time.unscaledTime * 4f);
            if (gridHighlight.gameObject.activeSelf)
            {
                gridHighlightImage.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * wave);
                gridHighlight.localScale = Vector3.one * (1f + 0.025f * wave);
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

                // 장착 확인 팝업이 떠 있거나 방금 닫혔으면(닫은 그 Z/X/Esc가 장비창까지 닫지 않게) 키를 쓰지 않는다.
                if (ChoicePopup.ConsumesInput) return;

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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Data;

namespace Aethoria.UI
{
    // 노아 대화에서 "[1] 상점"을 고르면 뜨는 구매 화면(UI/NoahsShopUI, 1536x1024 — 원본은 Sprite/UI/Menus/Shop).
    // 가운데 4x3 상품 칸에서 고르면 오른쪽 상세 창에 아이콘·이름·등급·능력치(지금 장비와 비교)·설명·가격이 뜨고,
    // [구매하기]로 산다. 그림의 탭 이름·"아이템 정보"/"설명" 머리말·구매하기 글자는 배경에 그려져 있고, 나머지는 코드로 얹는다.
    // 원본 그림은 "무기" 탭이 켜진 모습이라, 게임에 넣을 때 그 탭 바탕을 다른 탭처럼 어둡게 눌러 두었다(선택 표시는 코드로 그린다).
    // 여닫기는 장비창과 같은 연출(PanelTransition — 아래에서 올라오며 펼쳐지고, 닫을 때 접히듯 사라짐)이고,
    // 열거나 탭을 바꾸면 상품 아이콘이 차례로 튀어나오고, 다른 칸을 고르면 상세 창 내용이 옆에서 살짝 밀려 들어온다.
    // 조작: 방향키로 칸 선택, Q/E 탭 전환, Z/Enter 구매, X/Esc 닫기.
    // 마우스: 칸에 올리거나 누르면 선택, [구매하기]를 누르면 구매, 탭을 누르면 그 탭으로, 오른쪽 위 X로 닫기.
    public class ShopUI : MonoBehaviour
    {
        // 아래 좌표는 모두 배경 그림(1536x1024)의 픽셀(위에서부터 y)이다.
        private const float ArtWidth = 1536f;
        private const float ArtHeight = 1024f;

        private const int GridColumns = 4;
        private const int GridRows = 3;
        private static readonly Vector2[] GridColumnX = { new(568f, 682f), new(697f, 810f), new(825f, 939f), new(954f, 1068f) };
        private static readonly Vector2[] GridRowY = { new(334f, 463f), new(493f, 622f), new(652f, 781f) };

        // 무기/장비/장신구/기타 탭(좌우 x), 탭 줄(위아래 y).
        private static readonly Vector2[] TabX = { new(567f, 686f), new(696f, 814f), new(824f, 943f), new(952f, 1071f) };
        private static readonly Vector2 TabY = new(203f, 283f);

        private static readonly Rect GoldBox = Rect.MinMaxRect(1240f, 26f, 1425f, 66f);
        private static readonly Rect CloseBox = Rect.MinMaxRect(1462f, 18f, 1516f, 74f);
        private static readonly Rect DetailIconBox = Rect.MinMaxRect(1255f, 175f, 1366f, 285f);
        private static readonly Rect DetailNameBox = Rect.MinMaxRect(1162f, 322f, 1466f, 360f);
        private static readonly Rect DetailInfoBox = Rect.MinMaxRect(1162f, 428f, 1466f, 546f);
        private static readonly Rect DetailDescBox = Rect.MinMaxRect(1162f, 617f, 1466f, 737f);
        private static readonly Rect PriceBox = Rect.MinMaxRect(1248f, 766f, 1428f, 806f);
        private static readonly Rect BuyButtonBox = Rect.MinMaxRect(1162f, 832f, 1466f, 896f);

        // 탭 하나가 어떤 카테고리들을 묶어서 보여주는지(InventoryUI의 탭을 상점 아트의 4탭에 맞춰 합쳤다).
        private static readonly ItemCategory[][] TabFilters =
        {
            new[] { ItemCategory.Weapon },
            new[] { ItemCategory.Top, ItemCategory.Bottom, ItemCategory.Shoes },
            new[] { ItemCategory.Necklace, ItemCategory.Ring },
            new[] { ItemCategory.Materials, ItemCategory.Misc },
        };

        private static readonly Color GoldColor = new(1f, 0.85f, 0.3f);
        private static readonly Color TabSelectedColor = new(0.62f, 0.35f, 1f, 0.32f);
        private static readonly Color DisabledButtonColor = new(0.05f, 0.03f, 0.07f, 0.6f);
        private static readonly Color BodyTextColor = new(0.86f, 0.83f, 0.92f);
        private static readonly Color FlavorColor = new(0.78f, 0.62f, 1f);
        private const string UpColor = "#7CFF8A";
        private const string DownColor = "#FF7A7A";
        private const string StarOnColor = "#FFD25A";
        private const string StarOffColor = "#5A4A66";
        private const string SetEffectColor = "#9FE8C8";

        private Character player;
        private CharacterMovement2D playerMovement;
        private Inventory inventory;
        private Font font;

        private GameObject panel;
        private PanelTransition transition;

        // 상품 아이콘이 차례로 튀어나오는 연출(열 때·탭을 바꿀 때).
        private const float SlotPopDuration = 0.24f;
        private const float SlotPopStagger = 0.035f;
        private const float SlotPopOpenDelay = 0.14f;   // 창이 올라오는 동안은 기다렸다가 시작한다
        private const float SlotPopStartScale = 0.45f;
        private float slotPopStart = -10f;
        private bool slotPopDone = true;

        // 상세 창 내용이 옆에서 밀려 들어오며 나타나는 연출(다른 칸을 고를 때).
        private const float DetailFadeDuration = 0.16f;
        private const float DetailSlideDistance = 14f;
        private RectTransform detailRoot;
        private CanvasGroup detailGroup;
        private float detailFadeStart = -10f;
        private Text goldText;
        private RectTransform gridHighlight;
        private RawImage gridHighlightImage;
        private RectTransform tabHighlight;
        private readonly Image[] gridIcons = new Image[GridColumns * GridRows];
        private readonly Text[] gridNameLabels = new Text[GridColumns * GridRows];
        private readonly Text[] gridPriceLabels = new Text[GridColumns * GridRows];

        private Image detailIcon;
        private Text detailIconName;
        private Text detailName;
        private Text detailInfo;
        private Text detailDesc;
        private Text priceText;
        private Image buyButtonDim;

        private int currentTab;
        private List<ItemData> currentItems = new();
        private int selectedIndex = -1;
        private int openedFrame = -1;

        // 포션처럼 여러 개 살 수 있는 물건을 한 번에 살 수 있는 최대 개수.
        private const int MaxBuyQuantity = 99;

        public bool IsOpen => panel != null && panel.activeSelf && (transition == null || !transition.IsClosing);

        public void Initialize(Character character, CharacterMovement2D movement)
        {
            player = character;
            playerMovement = movement;
            inventory = character.GetComponent<Inventory>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildPanel();
        }

        // ---------------------------------------------------------------- 화면 짓기

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
            panel.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/Menus/NoahsShopUI");

            tabHighlight = Box(rect, "TabHighlight", Rect.MinMaxRect(TabX[0].x + 3f, TabY.x + 3f, TabX[0].y - 3f, TabY.y - 3f));
            var tabImage = tabHighlight.gameObject.AddComponent<Image>();
            tabImage.color = TabSelectedColor;
            tabImage.raycastTarget = false;

            goldText = MakeText(rect, "GoldText", GoldBox, 24, GoldColor, TextAnchor.MiddleLeft, bold: true);

            BuildGridHighlight(rect); // 아이콘보다 먼저 만들어서 뒤에 깔리게 한다.
            BuildGrid(rect);
            BuildDetail(rect);
            BuildMouseTargets(rect); // 글자/아이콘보다 위에 깔려야 마우스를 받는다.

            transition = panel.AddComponent<PanelTransition>();
            panel.SetActive(false);
        }

        // 그림 픽셀 상자(x, 위에서 y)를 화면 비율 앵커로 옮긴 빈 RectTransform.
        private static RectTransform Box(Transform parent, string name, Rect box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(box.xMin / ArtWidth, 1f - box.yMax / ArtHeight);
            rect.anchorMax = new Vector2(box.xMax / ArtWidth, 1f - box.yMin / ArtHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Text MakeText(Transform parent, string name, Rect box, int size, Color color, TextAnchor alignment, bool bold = false)
        {
            var rect = Box(parent, name, box);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Rect CellBox(int index)
        {
            var x = GridColumnX[index % GridColumns];
            var y = GridRowY[index / GridColumns];
            return Rect.MinMaxRect(x.x, y.x, x.y, y.y);
        }

        // 4x3 상품 칸: 가운데 아이콘(전용 그림이 없으면 이름 글자), 칸 아래쪽에 가격.
        private void BuildGrid(Transform parent)
        {
            for (int i = 0; i < gridIcons.Length; i++)
            {
                var cell = CellBox(i);
                var iconRect = Box(parent, $"SlotIcon_{i}", Rect.MinMaxRect(cell.xMin + 16f, cell.yMin + 12f, cell.xMax - 16f, cell.yMax - 34f));
                var icon = iconRect.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;
                gridIcons[i] = icon;

                var name = MakeText(parent, $"SlotName_{i}", Rect.MinMaxRect(cell.xMin + 6f, cell.yMin + 10f, cell.xMax - 6f, cell.yMax - 34f),
                    13, Color.white, TextAnchor.MiddleCenter, bold: true);
                name.enabled = false;
                gridNameLabels[i] = name;

                var price = MakeText(parent, $"SlotPrice_{i}", Rect.MinMaxRect(cell.xMin + 4f, cell.yMax - 32f, cell.xMax - 4f, cell.yMax - 8f),
                    13, GoldColor, TextAnchor.MiddleCenter, bold: true);
                price.enabled = false;
                gridPriceLabels[i] = price;
            }
        }

        private void BuildGridHighlight(Transform parent)
        {
            gridHighlight = Box(parent, "GridHighlight", CellBox(0));
            gridHighlightImage = gridHighlight.gameObject.AddComponent<RawImage>();
            gridHighlightImage.texture = Resources.Load<Texture2D>("UI/Menus/Inventory/ItemSelectEffect2");
            gridHighlightImage.raycastTarget = false;
            gridHighlight.gameObject.SetActive(false);
        }

        // 오른쪽 상세 창: 큰 아이콘, 이름(등급 색), 아이템 정보(별·분류·능력치 비교), 설명(소개·설명·세트 효과), 가격, 구매 버튼.
        private void BuildDetail(Transform panelRoot)
        {
            // 상세 창 내용은 한 묶음(화면 전체를 덮는 빈 판)에 모아 함께 흐려졌다 나타나게 한다.
            detailRoot = Box(panelRoot, "Detail", Rect.MinMaxRect(0f, 0f, ArtWidth, ArtHeight));
            detailGroup = detailRoot.gameObject.AddComponent<CanvasGroup>();
            detailGroup.blocksRaycasts = false;
            Transform parent = detailRoot;

            var iconRect = Box(parent, "DetailIcon", DetailIconBox);
            detailIcon = iconRect.gameObject.AddComponent<Image>();
            detailIcon.preserveAspect = true;
            detailIcon.raycastTarget = false;
            detailIconName = MakeText(parent, "DetailIconName", DetailIconBox, 15, Color.white, TextAnchor.MiddleCenter, bold: true);

            detailName = MakeText(parent, "DetailName", DetailNameBox, 19, Color.white, TextAnchor.MiddleCenter, bold: true);

            detailInfo = MakeText(parent, "DetailInfo", DetailInfoBox, 14, BodyTextColor, TextAnchor.UpperLeft);
            detailInfo.resizeTextForBestFit = true;
            detailInfo.resizeTextMinSize = 10;
            detailInfo.resizeTextMaxSize = 14;
            detailInfo.lineSpacing = 1.05f;

            detailDesc = MakeText(parent, "DetailDesc", DetailDescBox, 13, BodyTextColor, TextAnchor.UpperLeft);
            detailDesc.resizeTextForBestFit = true;
            detailDesc.resizeTextMinSize = 9;
            detailDesc.resizeTextMaxSize = 13;

            priceText = MakeText(parent, "Price", PriceBox, 20, GoldColor, TextAnchor.MiddleLeft, bold: true);

            // 살 수 없을 때(이미 보유·골드 부족·빈 칸) 버튼을 어둡게 누른다.
            var dimRect = Box(parent, "BuyButtonDim", Rect.MinMaxRect(BuyButtonBox.xMin + 10f, BuyButtonBox.yMin + 6f, BuyButtonBox.xMax - 10f, BuyButtonBox.yMax - 6f));
            buyButtonDim = dimRect.gameObject.AddComponent<Image>();
            buyButtonDim.color = DisabledButtonColor;
            buyButtonDim.raycastTarget = false;
        }

        // 마우스 입력을 받는 투명한 판들(상품 칸, 탭, 구매 버튼, 닫기).
        private void BuildMouseTargets(Transform parent)
        {
            for (int i = 0; i < gridIcons.Length; i++) CreateMouseTarget(parent, "SlotTarget_" + i, CellBox(i), TargetKind.Slot, i);
            for (int i = 0; i < TabX.Length; i++)
                CreateMouseTarget(parent, "TabTarget_" + i, Rect.MinMaxRect(TabX[i].x, TabY.x, TabX[i].y, TabY.y), TargetKind.Tab, i);
            CreateMouseTarget(parent, "BuyTarget", BuyButtonBox, TargetKind.Buy, 0);
            CreateMouseTarget(parent, "CloseTarget", CloseBox, TargetKind.Close, 0);
        }

        private enum TargetKind { Slot, Tab, Buy, Close }

        private void CreateMouseTarget(Transform parent, string name, Rect box, TargetKind kind, int index)
        {
            var rect = Box(parent, name, box);
            rect.gameObject.AddComponent<Image>().color = Color.clear;
            var target = rect.gameObject.AddComponent<MouseTarget>();
            target.owner = this;
            target.kind = kind;
            target.index = index;
        }

        // InventoryUI.SlotInteraction과 같은 이유로 OnPointerClick 대신 OnPointerDown을 쓴다.
        private class MouseTarget : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
        {
            public ShopUI owner;
            public TargetKind kind;
            public int index;

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (kind == TargetKind.Slot) owner.SelectSlot(index);
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                switch (kind)
                {
                    case TargetKind.Slot: owner.SelectSlot(index); break;
                    case TargetKind.Tab: owner.SelectTab(index); break;
                    case TargetKind.Buy: owner.TryBuySelected(); break;
                    case TargetKind.Close: owner.Hide(); break;
                }
            }
        }

        // ---------------------------------------------------------------- 열고 닫기

        public void Show()
        {
            currentTab = 0;
            openedFrame = Time.frameCount;
            RefreshGold();
            RefreshTab();
            transition.PlayOpen();
            slotPopStart += SlotPopOpenDelay;
            // InventoryUI와 같은 이유로, 나중에 추가된 HUD 요소가 위에 겹쳐 보이지 않도록 맨 앞으로 올린다.
            panel.transform.SetAsLastSibling();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            if (!IsOpen) return;
            transition.PlayClose();
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        // ---------------------------------------------------------------- 갱신

        private void RefreshGold()
        {
            if (goldText != null && player != null) goldText.text = player.Gold.ToString("N0");
        }

        private bool IsOwned(ItemData item) => !item.IsConsumable && inventory.GetCount(item.id) > 0;

        private void RefreshTab()
        {
            var categories = TabFilters[currentTab];
            currentItems = ItemDatabase.AllItems
                .Where(item => categories.Contains(item.category) && item.forSale)
                .OrderBy(item => item.shopPrice).ToList();

            var tabBox = Rect.MinMaxRect(TabX[currentTab].x + 3f, TabY.x + 3f, TabX[currentTab].y - 3f, TabY.y - 3f);
            tabHighlight.anchorMin = new Vector2(tabBox.xMin / ArtWidth, 1f - tabBox.yMax / ArtHeight);
            tabHighlight.anchorMax = new Vector2(tabBox.xMax / ArtWidth, 1f - tabBox.yMin / ArtHeight);

            for (int i = 0; i < gridIcons.Length; i++)
            {
                bool filled = i < currentItems.Count;
                if (!filled)
                {
                    gridIcons[i].enabled = false;
                    gridNameLabels[i].enabled = false;
                    gridPriceLabels[i].enabled = false;
                    continue;
                }

                var item = currentItems[i];
                // 아이콘 아트가 없으면(iconPath가 비었거나 그 경로에 그림이 아직 없으면) 이름을 글자로 대신 보여준다.
                var iconSprite = item.LoadIcon();
                gridIcons[i].sprite = iconSprite;
                gridIcons[i].enabled = iconSprite != null;
                gridNameLabels[i].text = item.itemName;
                gridNameLabels[i].enabled = iconSprite == null;
                gridPriceLabels[i].enabled = true;
                RefreshSlotPrice(i);
            }

            selectedIndex = currentItems.Count > 0 ? 0 : -1;
            RefreshSelection();
            StartSlotPop();
        }

        // 소모품(포션)은 여러 개 살 수 있으므로 "보유 중"으로 막지 않는다.
        private void RefreshSlotPrice(int index)
        {
            var item = currentItems[index];
            bool owned = IsOwned(item);
            gridPriceLabels[index].text = owned ? "보유 중" : PriceLabel(item);
            gridPriceLabels[index].color = owned ? Color.gray : GoldColor;
        }

        private static string PriceLabel(ItemData item) => item.shopPrice > 0 ? item.shopPrice.ToString("N0") + " G" : "무료";

        private void SelectSlot(int index)
        {
            if (index < 0 || index >= currentItems.Count || index == selectedIndex) return;
            selectedIndex = index;
            RefreshSelection();
        }

        private void SelectTab(int tab)
        {
            if (tab == currentTab) return;
            currentTab = tab;
            RefreshTab();
        }

        private void ChangeTab(int delta)
        {
            currentTab = (currentTab + delta + TabFilters.Length) % TabFilters.Length;
            RefreshTab();
        }

        // 방향키는 지금 탭 안의 칸 사이만 움직인다(끝에서는 멈춤). 탭 전환은 Q/E나 마우스로만 한다.
        private void MoveSelection(int dCol, int dRow)
        {
            if (selectedIndex < 0) return;

            int col = selectedIndex % GridColumns + dCol;
            int row = Mathf.Clamp(selectedIndex / GridColumns + dRow, 0, GridRows - 1);
            if (col < 0 || col >= GridColumns) return;

            int newIndex = row * GridColumns + col;
            if (newIndex < currentItems.Count) SelectSlot(newIndex);
        }

        private void RefreshSelection()
        {
            if (selectedIndex < 0)
            {
                gridHighlight.gameObject.SetActive(false);
                ShowDetail(null);
                return;
            }

            // 선택 테두리는 칸보다 살짝 크게 깐다(ItemSelectEffect2 원본 비율 271x319에 가깝게).
            var cell = CellBox(selectedIndex);
            var glow = Rect.MinMaxRect(cell.xMin - 9f, cell.yMin - 10f, cell.xMax + 9f, cell.yMax + 10f);
            gridHighlight.anchorMin = new Vector2(glow.xMin / ArtWidth, 1f - glow.yMax / ArtHeight);
            gridHighlight.anchorMax = new Vector2(glow.xMax / ArtWidth, 1f - glow.yMin / ArtHeight);
            gridHighlight.gameObject.SetActive(true);
            ShowDetail(currentItems[selectedIndex]);
        }

        private void ShowDetail(ItemData item)
        {
            detailFadeStart = Time.unscaledTime;
            ApplyDetailFade();

            if (item == null)
            {
                detailIcon.enabled = false;
                detailIconName.text = string.Empty;
                detailName.text = string.Empty;
                detailInfo.text = "<color=#8A7A99>이 탭에는 파는 물건이 없어요.</color>";
                detailDesc.text = string.Empty;
                priceText.text = string.Empty;
                buyButtonDim.enabled = true;
                return;
            }

            var icon = item.LoadIcon();
            detailIcon.sprite = icon;
            detailIcon.enabled = icon != null;
            detailIconName.text = icon == null ? item.itemName : string.Empty;

            detailName.text = item.itemName;
            detailName.color = Aethoria.Fx.LootFx.RarityColor(item.rarity);
            detailInfo.text = BuildInfoText(item);
            detailDesc.text = BuildDescriptionText(item);

            bool owned = IsOwned(item);
            priceText.text = owned ? "보유 중" : PriceLabel(item);
            priceText.color = owned ? Color.gray : player.Gold >= item.shopPrice ? GoldColor : new Color(1f, 0.45f, 0.45f);
            buyButtonDim.enabled = owned || player.Gold < item.shopPrice;
        }

        // 아이템 정보 칸: 별 등급 + "희귀 무기", 그 아래 능력치 줄(장비면 지금 낀 같은 부위 장비와 비교해 ▲▼).
        private string BuildInfoText(ItemData item)
        {
            var sb = new StringBuilder();
            sb.Append($"<color={StarOnColor}>{new string('★', item.StarCount)}</color>");
            sb.Append($"<color={StarOffColor}>{new string('★', 5 - item.StarCount)}</color>  ");
            sb.Append(item.GradeLabel);

            if (item.IsConsumable)
            {
                sb.Append($"\n체력 회복  <b>+{item.healAmount:0}</b>");
                sb.Append($"\n<color=#B8A8C8>보유 {inventory.GetCount(item.id)}개</color>");
                return sb.ToString();
            }
            if (!item.IsEquipment) return sb.ToString();

            var equipped = inventory.GetEquipped(item.category);
            bool sameItem = equipped == item;
            var mine = item.TotalStats;
            var theirs = equipped != null ? equipped.TotalStatsAt(inventory.GetEnhanceLevel(equipped)) : default;

            AppendStat(sb, "공격력", mine.attack, theirs.attack, "", sameItem);
            AppendStat(sb, "치명타 확률", mine.critChance, theirs.critChance, "%", sameItem);
            AppendStat(sb, "치명타 피해", mine.critDamage, theirs.critDamage, "%", sameItem);
            AppendStat(sb, "스킬 피해", mine.skillDamage, theirs.skillDamage, "%", sameItem);

            if (sameItem) sb.Append("\n<color=#B8A8C8>지금 장착 중</color>");
            else if (equipped != null) sb.Append($"\n<color=#8A7A99>비교: {equipped.DisplayName(inventory.GetEnhanceLevel(equipped))}</color>");
            return sb.ToString();
        }

        private static void AppendStat(StringBuilder sb, string label, float value, float equippedValue, string suffix, bool sameItem)
        {
            if (Mathf.Approximately(value, 0f) && Mathf.Approximately(equippedValue, 0f)) return;

            sb.Append($"\n{label}  <b>+{value:0.#}{suffix}</b>");
            if (sameItem) return;
            float diff = value - equippedValue;
            if (Mathf.Abs(diff) < 0.05f) sb.Append("  <color=#8A7A99>(=)</color>");
            else if (diff > 0f) sb.Append($"  <color={UpColor}>▲{diff:0.#}{suffix}</color>");
            else sb.Append($"  <color={DownColor}>▼{-diff:0.#}{suffix}</color>");
        }

        // 설명 칸: 한 줄 소개(보라) → 긴 설명 → 세트 효과(있으면).
        private static string BuildDescriptionText(ItemData item)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(item.flavorText))
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(FlavorColor)}><i>{item.flavorText}</i></color>\n");
            if (!string.IsNullOrEmpty(item.description)) sb.Append(item.description);
            string set = item.SetEffectText;
            if (!string.IsNullOrEmpty(set)) sb.Append($"\n<color={SetEffectColor}>세트 효과: {set}</color>");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 연출

        private void StartSlotPop()
        {
            slotPopStart = Time.unscaledTime;
            slotPopDone = false;
            ApplySlotPop();
        }

        // 칸마다 순서대로 조금씩 늦게, 작게 시작해 살짝 넘쳤다가 제 크기로 돌아오며 나타난다.
        private void ApplySlotPop()
        {
            if (slotPopDone) return;
            float elapsed = Time.unscaledTime - slotPopStart;
            bool allDone = true;
            for (int i = 0; i < gridIcons.Length; i++)
            {
                float t = Mathf.Clamp01((elapsed - i * SlotPopStagger) / SlotPopDuration);
                if (t < 1f) allDone = false;
                float scale = Mathf.LerpUnclamped(SlotPopStartScale, 1f, EaseOutBack(t));
                float alpha = Mathf.Clamp01(t * 1.6f);
                gridIcons[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
                gridNameLabels[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
                SetAlpha(gridIcons[i], alpha);
                SetAlpha(gridNameLabels[i], alpha);
                SetAlpha(gridPriceLabels[i], alpha);
            }
            slotPopDone = allDone;
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }

        private void ApplyDetailFade()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - detailFadeStart) / DetailFadeDuration);
            float k = 1f - (1f - t) * (1f - t);
            detailGroup.alpha = k;
            detailRoot.anchoredPosition = new Vector2(DetailSlideDistance * (1f - k), 0f);
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.6f, c3 = c1 + 1f;
            float m = x - 1f;
            return 1f + c3 * m * m * m + c1 * m * m;
        }

        private void UpdateGlowPulse()
        {
            if (!gridHighlight.gameObject.activeSelf) return;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            gridHighlightImage.color = new Color(1f, 1f, 1f, pulse);
        }

        // ---------------------------------------------------------------- 구매

        // [구매하기]/Z: 바로 사지 않고 구매 확인 팝업(ChoicePopup)을 띄운다. 포션은 수량(↑/↓)도 고른다.
        // 이미 가진 장비·골드 부족은 팝업 없이 알림 띠로 알려 준다.
        private void TryBuySelected()
        {
            if (selectedIndex < 0 || selectedIndex >= currentItems.Count) return;
            if (player == null || inventory == null) return;

            var item = currentItems[selectedIndex];
            if (IsOwned(item))
            {
                ChoicePopup.Toast("이미 보유한 아이템입니다.");
                return;
            }
            if (player.Gold < item.shopPrice)
            {
                ChoicePopup.Toast("골드가 부족합니다.");
                return;
            }

            int maxQuantity = 1;
            if (item.IsConsumable)
                maxQuantity = item.shopPrice > 0 ? Mathf.Clamp(player.Gold / item.shopPrice, 1, MaxBuyQuantity) : MaxBuyQuantity;

            int index = selectedIndex;
            ChoicePopup.Show(new ChoicePopup.Request
            {
                Message = maxQuantity >= 2 ? $"{item.itemName}을(를) 몇 개 구매할까요?" : $"{item.itemName}을(를) 구매할까요?",
                SubMessage = maxQuantity >= 2 ? null : $"{PriceLabel(item)}  (보유 {player.Gold:N0} G)",
                Icon = item.LoadIcon(),
                ConfirmLabel = "구매",
                CancelLabel = "취소",
                MaxQuantity = maxQuantity,
                QuantityLabel = count => $"{count}개   ·   {(item.shopPrice * count):N0} G",
                OnConfirmQuantity = count => Buy(item, index, count),
            });
        }

        private void Buy(ItemData item, int index, int count)
        {
            int total = item.shopPrice * count;
            // 무료 아이템(0골드)은 TrySpendGold(0)이 실패로 취급하므로 그냥 바로 지급한다.
            if (total > 0 && !player.TrySpendGold(total))
            {
                ChoicePopup.Toast("골드가 부족합니다.");
                return;
            }

            for (int i = 0; i < count; i++) inventory.AddItem(item);
            RefreshGold();
            if (index < currentItems.Count && currentItems[index] == item)
            {
                RefreshSlotPrice(index);
                if (index == selectedIndex) ShowDetail(item);
            }
            ChoicePopup.Toast(item.IsConsumable ? $"{item.itemName} {count}개 구매 (보유 {inventory.GetCount(item.id)}개)" : $"{item.itemName} 구매 완료", good: true);
        }

        private void Update()
        {
            if (!IsOpen) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            UpdateGlowPulse();
            ApplySlotPop();
            ApplyDetailFade();

            // 상점을 연 그 프레임의 입력(Z 등)이 같은 프레임에 다시 소비되어 바로 닫히지 않게 한다.
            if (Time.frameCount == openedFrame) return;
            // 구매 확인 팝업이 떠 있거나 방금 닫혔으면(닫은 그 X/Esc로 상점까지 닫히지 않게) 키를 쓰지 않는다.
            if (ChoicePopup.ConsumesInput) return;

            if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
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
                TryBuySelected();
            }
        }
    }
}

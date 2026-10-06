using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Data;

namespace Aethoria.UI
{
    // 대장장이 엘리의 장비 강화 화면(UI/Elly_Enhancement). 원본 그림(Sprite/UI/Elly_Enhancement_UI.png)에서
    // 안내 문구와 수치 자리의 "-"를 지운 배경 위에, 고른 장비의 강화 정보를 덮어 보여준다.
    // 조작: 장비 칸 클릭으로 목록을 열어 원하는 장비를 바로 선택, ←/→(화살표 버튼)로 하나씩 넘기기도 가능,
    // Z/Enter(또는 [강화하기])로 강화, X/Esc(또는 [X])로 닫기.
    // 강화 규칙(확률/재료/능력치 상승)은 EnhanceRules에 있다. 재련 탭은 아직 준비 중이다.
    public class EnhanceUI : MonoBehaviour
    {
        private const float ImageWidth = 1536f;
        private const float ImageHeight = 1024f;
        // 그림 픽셀 → 캔버스 단위(기준 해상도 720 높이) 글자 크기 환산.
        private const float PxToCanvas = 720f / ImageHeight;

        // 예상 옵션 4줄(공격력/치명타 확률/치명타 피해/스킬 데미지 증가)의 세로 중심. 그림 속 순서 그대로다.
        private static readonly float[] StatRowY = { 501f, 547f, 594f, 640f };
        // 필요 재료 4칸의 가로 중심.
        private static readonly float[] MaterialX = { 915f, 1043f, 1171f, 1300f };

        private static readonly Color TextColor = new Color(0.92f, 0.88f, 0.95f);
        private static readonly Color DimColor = new Color(0.55f, 0.52f, 0.6f);
        private static readonly Color UpColor = new Color(0.55f, 1f, 0.6f);
        private static readonly Color GoodColor = new Color(0.6f, 1f, 0.65f);
        private static readonly Color BadColor = new Color(1f, 0.45f, 0.45f);
        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.3f);

        private const float ResultMessageDuration = 2f;

        // 장비 고르기 팝업: 보유한 강화 가능 장비를 격자로 보여주고 바로 클릭해서 고를 수 있게 한다.
        private const int PickerColumns = 6;
        private const int PickerRows = 3;
        private const int PickerSlotCount = PickerColumns * PickerRows;

        private Character player;
        private CharacterMovement2D playerMovement;
        private Inventory inventory;

        private GameObject panel;
        private Image itemIcon;
        private Text itemFallbackLabel;
        private Text messageText;
        private readonly Text[] currentValues = new Text[4];
        private readonly Text[] nextValues = new Text[4];
        private Text successRateText;
        private readonly Image[] materialIcons = new Image[4];
        private readonly Text[] materialCounts = new Text[4];
        private Text goldText;

        private GameObject pickerPanel;
        private readonly Image[] pickerSlotBgs = new Image[PickerSlotCount];
        private readonly Image[] pickerIcons = new Image[PickerSlotCount];
        private readonly Text[] pickerLabels = new Text[PickerSlotCount];

        private List<ItemData> candidates = new();
        private int selected;
        private int openedFrame = -1;
        private float resultTimer;
        private Coroutine popRoutine;

        public bool IsOpen => panel != null && panel.activeSelf;
        public bool IsPickerOpen => pickerPanel != null && pickerPanel.activeSelf;

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

            panel = new GameObject("EnhancePanel", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/Elly_Enhancement");

            // 장비 칸: 아이콘(없으면 이름 글자). 칸을 누르면 다음 장비로 넘어간다.
            itemIcon = CreateImage(rect, "ItemIcon", 1036f, 166f, 1172f, 292f);
            itemIcon.preserveAspect = true;
            itemFallbackLabel = CreateText(rect, "ItemName", 1036f, 166f, 1172f, 292f, 22f, TextColor, TextAnchor.MiddleCenter);
            CreateButton(rect, "ItemSlotButton", 1022f, 152f, 1186f, 306f, null, OpenPicker);
            CreateButton(rect, "PrevButton", 944f, 205f, 994f, 255f, "◀", () => ChangeSelection(-1));
            CreateButton(rect, "NextButton", 1214f, 205f, 1264f, 255f, "▶", () => ChangeSelection(1));

            messageText = CreateText(rect, "Message", 905f, 343f, 1305f, 398f, 24f, TextColor, TextAnchor.MiddleCenter);
            messageText.lineSpacing = 0.9f;

            for (int i = 0; i < 4; i++)
            {
                currentValues[i] = CreateText(rect, "Current" + i, 1060f, StatRowY[i] - 18f, 1190f, StatRowY[i] + 18f, 24f, TextColor, TextAnchor.MiddleCenter);
                nextValues[i] = CreateText(rect, "Next" + i, 1235f, StatRowY[i] - 18f, 1350f, StatRowY[i] + 18f, 24f, TextColor, TextAnchor.MiddleCenter);
                nextValues[i].fontStyle = FontStyle.Bold;
            }

            successRateText = CreateText(rect, "SuccessRate", 1100f, 688f, 1345f, 720f, 24f, GoldColor, TextAnchor.MiddleRight);
            successRateText.fontStyle = FontStyle.Bold;

            for (int i = 0; i < 4; i++)
            {
                materialIcons[i] = CreateImage(rect, "MaterialIcon" + i, MaterialX[i] - 34f, 748f, MaterialX[i] + 34f, 816f);
                materialIcons[i].preserveAspect = true;
                materialCounts[i] = CreateText(rect, "MaterialCount" + i, MaterialX[i] - 55f, 830f, MaterialX[i] + 55f, 858f, 22f, TextColor, TextAnchor.MiddleCenter);
            }

            goldText = CreateText(rect, "GoldCost", 1062f, 870f, 1188f, 904f, 24f, GoldColor, TextAnchor.MiddleCenter);
            goldText.fontStyle = FontStyle.Bold;

            CreateButton(rect, "EnhanceButton", 905f, 915f, 1305f, 988f, null, TryEnhance);
            CreateButton(rect, "CloseButton", 1452f, 18f, 1522f, 88f, null, Hide);
            CreateButton(rect, "EnhanceTab", 1410f, 163f, 1517f, 287f, null, () => { });
            CreateButton(rect, "RefineTab", 1410f, 303f, 1517f, 427f, null, () => ShowResult("재련은 아직 준비 중이에요.", DimColor));

            BuildPicker(rect); // 가장 마지막에 만들어서 다른 모든 요소 위에 덮이게 한다.

            panel.SetActive(false);
        }

        // 장비 고르기 팝업: 장비 칸을 클릭하면 열리고, 보유한 강화 가능 장비를 격자로 보여준다.
        // 칸을 바로 클릭하면 그 장비가 선택되고 팝업이 닫힌다.
        private void BuildPicker(Transform parent)
        {
            pickerPanel = new GameObject("EnhancePickerPanel", typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)pickerPanel.transform;
            dimRect.SetParent(parent, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dim = pickerPanel.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            dim.raycastTarget = true;

            const float boxX0 = 368f, boxY0 = 140f, boxX1 = 1168f, boxY1 = 884f;
            var box = CreateImage(dimRect, "Box", boxX0, boxY0, boxX1, boxY1);
            box.enabled = true;
            box.raycastTarget = true;
            box.color = new Color(0.08f, 0.05f, 0.12f, 0.97f);

            var title = CreateText(dimRect, "PickerTitle", boxX0 + 32f, boxY0 + 16f, boxX1 - 32f, boxY0 + 60f, 28f, TextColor, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            title.text = "강화할 장비 선택";

            CreateButton(dimRect, "PickerClose", boxX1 - 66f, boxY0 + 12f, boxX1 - 18f, boxY0 + 60f, "X", ClosePicker);

            const float gridX0 = boxX0 + 32f, gridX1 = boxX1 - 32f, gridY0 = boxY0 + 90f, gridY1 = boxY1 - 24f;
            float cellW = (gridX1 - gridX0) / PickerColumns;
            float cellH = (gridY1 - gridY0) / PickerRows;

            for (int i = 0; i < PickerSlotCount; i++)
            {
                int row = i / PickerColumns;
                int col = i % PickerColumns;
                float cellX0 = gridX0 + col * cellW;
                float cellX1 = cellX0 + cellW;
                float cellY0 = gridY0 + row * cellH;
                float cellY1 = cellY0 + cellH;

                int captured = i;
                var slotRect = CreateRect(dimRect, "PickerSlot" + i, cellX0 + 6f, cellY0 + 6f, cellX1 - 6f, cellY1 - 6f, typeof(RectTransform), typeof(Image), typeof(Button));
                var bg = slotRect.GetComponent<Image>();
                bg.color = new Color(1f, 1f, 1f, 0.05f);
                slotRect.GetComponent<Button>().onClick.AddListener(() => SelectFromPicker(captured));
                pickerSlotBgs[i] = bg;

                float iconSize = Mathf.Min(cellW, cellH) - 60f;
                float cellCx = (cellX0 + cellX1) / 2f;
                pickerIcons[i] = CreateImage(dimRect, "PickerIcon" + i, cellCx - iconSize / 2f, cellY0 + 14f, cellCx + iconSize / 2f, cellY0 + 14f + iconSize);
                pickerIcons[i].preserveAspect = true;

                pickerLabels[i] = CreateText(dimRect, "PickerLabel" + i, cellX0 + 4f, cellY0 + 14f + iconSize + 4f, cellX1 - 4f, cellY1 - 6f, 16f, TextColor, TextAnchor.MiddleCenter);
            }

            pickerPanel.SetActive(false);
        }

        private void OpenPicker()
        {
            if (candidates.Count == 0) return;
            RefreshPicker();
            pickerPanel.SetActive(true);
        }

        private void ClosePicker()
        {
            pickerPanel.SetActive(false);
        }

        private void RefreshPicker()
        {
            for (int i = 0; i < PickerSlotCount; i++)
            {
                bool has = i < candidates.Count;
                pickerSlotBgs[i].gameObject.SetActive(has);
                if (!has)
                {
                    pickerIcons[i].enabled = false;
                    pickerLabels[i].text = "";
                    continue;
                }

                var item = candidates[i];
                int level = inventory.GetEnhanceLevel(item);
                bool equipped = inventory.IsEquipped(item);

                var sprite = string.IsNullOrEmpty(item.iconPath) ? null : Resources.Load<Sprite>(item.iconPath);
                pickerIcons[i].sprite = sprite;
                pickerIcons[i].enabled = sprite != null;

                pickerLabels[i].text = (equipped ? "★ " : "") + item.DisplayName(level);
                pickerLabels[i].color = equipped ? GoldColor : TextColor;

                pickerSlotBgs[i].color = i == selected
                    ? new Color(0.55f, 0.35f, 0.75f, 0.55f)
                    : new Color(1f, 1f, 1f, 0.05f);
            }
        }

        private void SelectFromPicker(int index)
        {
            if (index < 0 || index >= candidates.Count) return;
            selected = index;
            resultTimer = 0f;
            ClosePicker();
            Refresh();
        }

        public void Show()
        {
            openedFrame = Time.frameCount;
            RefreshCandidates();
            selected = 0;
            resultTimer = 0f;
            Refresh();
            panel.SetActive(true);
            // 인벤토리/상점과 같은 이유로, 나중에 추가된 HUD 요소가 위에 겹쳐 보이지 않도록 맨 앞으로 올린다.
            panel.transform.SetAsLastSibling();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            ClosePicker();
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        // 강화할 수 있는 장비 목록: 장착 중인 것 먼저, 나머지는 id 순.
        private void RefreshCandidates()
        {
            candidates = inventory == null
                ? new List<ItemData>()
                : inventory.All().Select(entry => entry.item).Where(item => item.IsEquipment)
                    .OrderByDescending(item => inventory.IsEquipped(item)).ThenBy(item => item.id).ToList();
        }

        private ItemData Selected => selected >= 0 && selected < candidates.Count ? candidates[selected] : null;

        private void ChangeSelection(int delta)
        {
            if (candidates.Count == 0) return;
            selected = (selected + delta + candidates.Count) % candidates.Count;
            resultTimer = 0f;
            Refresh();
        }

        private void Refresh()
        {
            var item = Selected;
            if (item == null)
            {
                itemIcon.enabled = false;
                itemFallbackLabel.text = "";
                if (resultTimer <= 0f) SetMessage("강화할 장비가 없어요.", DimColor);
                for (int i = 0; i < 4; i++) { SetStat(currentValues[i], 0f, "", DimColor); SetStat(nextValues[i], 0f, "", DimColor); }
                successRateText.text = "";
                ClearMaterials();
                goldText.text = "-";
                goldText.color = DimColor;
                return;
            }

            int level = inventory.GetEnhanceLevel(item);
            bool maxed = level >= EnhanceRules.MaxLevel;

            var sprite = string.IsNullOrEmpty(item.iconPath) ? null : Resources.Load<Sprite>(item.iconPath);
            itemIcon.sprite = sprite;
            itemIcon.enabled = sprite != null;
            itemFallbackLabel.text = sprite != null ? "" : item.itemName;

            if (resultTimer <= 0f)
            {
                string status = maxed ? "최대 강화 단계예요." : $"+{level} → +{level + 1} 강화";
                string equippedMark = inventory.IsEquipped(item) ? " (장착 중)" : "";
                SetMessage($"{item.DisplayName(level)}{equippedMark}\n<size={Mathf.RoundToInt(18f * PxToCanvas)}>{status}   장비 칸 클릭으로 선택</size>", TextColor);
            }

            var now = item.BaseStatsAt(level);
            var next = maxed ? now : item.BaseStatsAt(level + 1);
            FillStatRow(0, now.attack, next.attack, "", maxed);
            FillStatRow(1, now.critChance, next.critChance, "%", maxed);
            FillStatRow(2, now.critDamage, next.critDamage, "%", maxed);
            FillStatRow(3, now.skillDamage, next.skillDamage, "%", maxed);

            if (maxed)
            {
                successRateText.text = "";
                ClearMaterials();
                goldText.text = "-";
                goldText.color = DimColor;
                return;
            }

            successRateText.text = $"성공 확률 {Mathf.RoundToInt(EnhanceRules.SuccessRate(level) * 100f)}%";

            ClearMaterials();
            int needStones = EnhanceRules.StoneCost(level);
            int haveStones = inventory.GetCount(ItemDatabase.EnhanceStone.id);
            materialIcons[0].sprite = Resources.Load<Sprite>(ItemDatabase.EnhanceStone.iconPath);
            materialIcons[0].enabled = materialIcons[0].sprite != null;
            materialCounts[0].text = $"{haveStones} / {needStones}";
            materialCounts[0].color = haveStones >= needStones ? GoodColor : BadColor;

            int needGold = EnhanceRules.GoldCost(item, level);
            goldText.text = needGold.ToString("N0");
            goldText.color = player.Gold >= needGold ? GoldColor : BadColor;
        }

        private void FillStatRow(int row, float now, float next, string suffix, bool maxed)
        {
            SetStat(currentValues[row], now, suffix, TextColor);
            if (maxed) SetStat(nextValues[row], 0f, "", DimColor);
            else SetStat(nextValues[row], next, suffix, next > now ? UpColor : TextColor);
        }

        private static void SetStat(Text label, float value, string suffix, Color color)
        {
            if (Mathf.Approximately(value, 0f))
            {
                label.text = "-";
                label.color = DimColor;
                return;
            }
            label.text = "+" + value.ToString("0.#") + suffix;
            label.color = color;
        }

        private void ClearMaterials()
        {
            for (int i = 0; i < 4; i++)
            {
                materialIcons[i].enabled = false;
                materialCounts[i].text = "-";
                materialCounts[i].color = DimColor;
            }
        }

        private void SetMessage(string text, Color color)
        {
            messageText.text = text;
            messageText.color = color;
        }

        // 성공/실패/안내 문구를 잠깐 보여준 뒤 다시 장비 정보로 돌아간다.
        private void ShowResult(string text, Color color)
        {
            SetMessage(text, color);
            resultTimer = ResultMessageDuration;
        }

        private void TryEnhance()
        {
            var item = Selected;
            if (item == null) return;

            int level = inventory.GetEnhanceLevel(item);
            if (level >= EnhanceRules.MaxLevel)
            {
                ShowResult("이미 최대 강화 단계예요.", DimColor);
                return;
            }

            int needStones = EnhanceRules.StoneCost(level);
            int needGold = EnhanceRules.GoldCost(item, level);
            if (inventory.GetCount(ItemDatabase.EnhanceStone.id) < needStones)
            {
                ShowResult("강화석이 부족해요.", BadColor);
                return;
            }
            if (player.Gold < needGold)
            {
                ShowResult("골드가 부족해요.", BadColor);
                return;
            }

            // 재료는 성공/실패와 상관없이 소모된다.
            inventory.TryRemoveItem(ItemDatabase.EnhanceStone, needStones);
            player.TrySpendGold(needGold);

            if (Random.value < EnhanceRules.SuccessRate(level))
            {
                inventory.SetEnhanceLevel(item, level + 1);
                ShowResult($"강화 성공!\n{item.DisplayName(level + 1)}", GoodColor);
                if (popRoutine != null) StopCoroutine(popRoutine);
                popRoutine = StartCoroutine(PopIcon());
            }
            else
            {
                ShowResult($"강화 실패...\n재료만 사라지고 +{level}은(는) 그대로예요.", BadColor);
            }

            Refresh();
        }

        // 강화 성공 시 장비 아이콘이 한 번 통통 튄다(시간이 멈춰 있으므로 실시간으로 잰다).
        private System.Collections.IEnumerator PopIcon()
        {
            var iconRect = itemIcon.rectTransform;
            const float duration = 0.25f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float bump = 1f + 0.3f * Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                iconRect.localScale = Vector3.one * bump;
                yield return null;
            }
            iconRect.localScale = Vector3.one;
            popRoutine = null;
        }

        private void Update()
        {
            if (!IsOpen) return;

            if (resultTimer > 0f)
            {
                resultTimer -= Time.unscaledDeltaTime;
                if (resultTimer <= 0f) Refresh();
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 연 그 프레임의 입력(엘리 대화의 숫자키 등)이 같은 프레임에 다시 소비되지 않게 한다.
            if (Time.frameCount == openedFrame) return;

            if (IsPickerOpen)
            {
                if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                {
                    ClosePicker();
                }
                return;
            }

            if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                Hide();
                return;
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame) ChangeSelection(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame) ChangeSelection(1);

            if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                TryEnhance();
            }
        }

        // ---- 그림 픽셀 좌표(왼쪽 위 x0,y0 ~ 오른쪽 아래 x1,y1)로 요소를 만드는 도우미들 ----

        private static RectTransform CreateRect(Transform parent, string name, float x0, float y0, float x1, float y1, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0 / ImageWidth, 1f - y1 / ImageHeight);
            rect.anchorMax = new Vector2(x1 / ImageWidth, 1f - y0 / ImageHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image CreateImage(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var image = CreateRect(parent, name, x0, y0, x1, y1, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        // fontPx는 그림 위에서의 글자 크기(px).
        private static Text CreateText(Transform parent, string name, float x0, float y0, float x1, float y1, float fontPx, Color color, TextAnchor alignment)
        {
            var text = CreateRect(parent, name, x0, y0, x1, y1, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(fontPx * PxToCanvas));
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // 그림에 이미 그려진 버튼 위에 투명한 클릭 영역을 덮는다. label이 있으면 글자도 얹는다.
        private static void CreateButton(Transform parent, string name, float x0, float y0, float x1, float y1, string label, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreateRect(parent, name, x0, y0, x1, y1, typeof(RectTransform), typeof(Image), typeof(Button));
            var image = rect.GetComponent<Image>();
            image.color = label != null ? new Color(0.1f, 0.06f, 0.14f, 0.85f) : new Color(1f, 1f, 1f, 0f);
            rect.GetComponent<Button>().onClick.AddListener(onClick);

            if (label == null) return;
            var text = CreateText(rect, "Label", 0f, 0f, ImageWidth, ImageHeight, 26f, TextColor, TextAnchor.MiddleCenter);
            text.text = label;
        }
    }
}

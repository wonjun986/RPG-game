using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Data;

namespace Aethoria.UI
{
    // 대장장이 엘리의 장비 강화 화면.
    //  - 왼쪽: 엘리 일러스트(UI/Menus/Elly_Enhance_Art, 오른쪽 끝은 어둡게 녹아든다).
    //  - 오른쪽 패널: [보유 장비 목록(3칸 x 5줄, 넘치면 쪽 넘김)] + [고른 장비: 큰 칸 · 이름 · +n → +n+1 · 능력치 현재→다음 ·
    //    성공 확률 게이지 · 필요 재료(강화석·골드) · 강화하기 버튼].
    //  - 강화하기: 망치질 3번(칸이 움찔하며 불꽃이 튐) → 성공이면 금빛 섬광·고리·불꽃 + "강화 성공!", 실패면 연기·흔들림 + "강화 실패".
    // 조작: 방향키(WASD)로 목록 이동, Z/Enter 강화, X/Esc 닫기. 마우스로 칸·버튼을 눌러도 된다.
    // 패널 그림은 UI/Enhance/ 아래(Panel · ItemSlot · ListSlot · StatRow · Gauge · Button)를 쓰고, 없으면 코드로 그린 판을 쓴다.
    // 강화 규칙(확률/재료/능력치 상승)은 EnhanceRules에 있다.
    public class EnhanceUI : MonoBehaviour
    {
        private const float PanelWidth = 674f, PanelHeight = 672f;
        private const int ListColumns = 3, ListRows = 5, ListPage = ListColumns * ListRows;
        // 패널 판(UI/Enhance/Panel)은 테두리가 두껍고(옆 약 32, 위 약 52 — 가운데 모루 장식은 97까지, 아래 약 38)
        // 오른쪽 위 모서리에 사슬이 늘어져 있어서, 내용은 그 안쪽(x 46~630, y 56~634)에 둔다.
        private const float ListX = 62f, ListY = 122f;
        private const float ListSlotSize = 54f, ListGap = 6f;
        private const float ColumnX = 248f, ColumnWidth = 382f;
        private const float SlotCenterY = 151f;
        // 새 엘리 일러스트(UI/Menus/Elly_Enhance_Art, 4:5). 없으면 예전 그림(UI/Menus/Elly_Enhancement)의 왼쪽 822px만 쓴다.
        private const string ArtPath = "UI/Menus/Elly_Enhance_Art";
        private const float OldArtUvWidth = 822f / 1536f;
        // 확률 게이지 틀(UI/Enhance/Gauge, 약 6.5:1) 안쪽 검은 홈의 위치(그림 크기 대비 비율).
        private static readonly Vector2 GaugeChannelMin = new(0.1185f, 0.3375f);
        private static readonly Vector2 GaugeChannelMax = new(0.8872f, 0.675f);

        private static readonly Color TextColor = new(0.92f, 0.88f, 0.95f);
        private static readonly Color DimColor = new(0.5f, 0.47f, 0.56f);
        private static readonly Color UpColor = new(0.55f, 1f, 0.6f);
        private static readonly Color GoodColor = new(0.6f, 1f, 0.65f);
        private static readonly Color BadColor = new(1f, 0.45f, 0.45f);
        private static readonly Color GoldColor = new(1f, 0.85f, 0.35f);
        private static readonly Color SelectColor = new(1f, 0.85f, 0.45f, 1f);

        private static readonly (string label, string suffix)[] StatRows =
        {
            ("공격력", ""), ("치명타 확률", "%"), ("치명타 피해", "%"), ("스킬 데미지 증가", "%"),
        };

        private Character player;
        private CharacterMovement2D playerMovement;
        private Inventory inventory;
        private Font font;

        private GameObject panel;          // 화면 전체(엘리 + 패널)
        private RectTransform panelRect;   // 오른쪽 패널
        private RectTransform fxRoot;

        // 목록
        private readonly Image[] listFrames = new Image[ListPage];
        private readonly Image[] listIcons = new Image[ListPage];
        private readonly Text[] listNames = new Text[ListPage];
        private readonly Text[] listLevels = new Text[ListPage];
        private readonly Text[] listEquipped = new Text[ListPage];
        private readonly Image[] listGlows = new Image[ListPage];
        private Text pageText;

        // 고른 장비
        private RectTransform itemSlotRect;
        private Image itemGlow;
        private Image itemIcon;
        private Text itemFallbackLabel;
        private Text itemName;
        private Text itemGrade;
        private Text levelText;
        private readonly Text[] statNow = new Text[4];
        private readonly Text[] statNext = new Text[4];
        private readonly Text[] statArrow = new Text[4];
        private readonly Text[] statLabels = new Text[4];
        private Image gaugeFill;
        private Text gaugeText;
        private Text noteText;
        private Image stoneIcon;
        private Text stoneText;
        private Text goldText;
        private Text messageText;
        private Image buttonImage;
        private Text buttonLabel;

        private List<ItemData> candidates = new();
        private int selected;
        private int openedFrame = -1;
        private float messageUntil;
        private bool busy;              // 망치질 연출 중(입력 막음)

        public bool IsOpen => panel != null && panel.activeSelf;
        public bool IsPickerOpen => false;

        public void Initialize(Character character, CharacterMovement2D movement)
        {
            player = character;
            playerMovement = movement;
            inventory = character.GetComponent<Inventory>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildPanel();
        }

        // ================================================================ 열고 닫기

        public void Show()
        {
            openedFrame = Time.frameCount;
            TitleScreenUI.EnsureEventSystem();
            RefreshCandidates();
            selected = 0;
            messageUntil = 0f;
            busy = false;
            Refresh();
            panel.SetActive(true);
            // 인벤토리/상점과 같은 이유로, 나중에 추가된 HUD 요소가 위에 겹쳐 보이지 않도록 맨 앞으로 올린다.
            panel.transform.SetAsLastSibling();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            if (busy) return;
            if (panel.activeSelf) UiSfx.Close();
            for (int i = fxRoot.childCount - 1; i >= 0; i--) Destroy(fxRoot.GetChild(i).gameObject);
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

        private void Select(int index)
        {
            if (busy || candidates.Count == 0) return;
            index = Mathf.Clamp(index, 0, candidates.Count - 1);
            if (index == selected) return;
            UiSfx.Move();
            selected = index;
            messageUntil = 0f;
            Refresh();
        }

        // ================================================================ 화면 갱신

        private void Refresh()
        {
            RefreshList();
            var item = Selected;
            if (item == null)
            {
                itemIcon.enabled = false;
                itemFallbackLabel.text = "";
                itemName.text = "강화할 장비가 없어요";
                itemName.color = DimColor;
                itemGrade.text = "";
                levelText.text = "";
                for (int i = 0; i < 4; i++) SetStatRow(i, 0f, 0f, true);
                SetGauge(-1f);
                stoneText.text = "-";
                stoneText.color = DimColor;
                goldText.text = "-";
                goldText.color = DimColor;
                SetButton(false, "강화하기");
                if (Time.unscaledTime >= messageUntil) SetMessage("장비를 얻으면 이곳에서 강화할 수 있어요.", DimColor);
                return;
            }

            int level = inventory.GetEnhanceLevel(item);
            bool maxed = level >= EnhanceRules.MaxLevel;

            var sprite = item.LoadIcon();
            itemIcon.sprite = sprite;
            itemIcon.enabled = sprite != null;
            itemFallbackLabel.text = sprite != null ? "" : item.itemName;
            itemName.text = item.DisplayName(level) + (inventory.IsEquipped(item) ? "  <size=13><color=#B9A6D6>(장착 중)</color></size>" : "");
            itemName.color = Aethoria.Fx.LootFx.RarityColor(item.rarity);
            itemGrade.text = item.GradeLabel;
            levelText.text = maxed ? "최대 강화 단계" : $"+{level}  ▶  <color=#FFE08A>+{level + 1}</color>";
            itemGlow.color = WithAlpha(Aethoria.Fx.LootFx.RarityColor(item.rarity), 0.35f);

            var now = item.BaseStatsAt(level);
            var next = maxed ? now : item.BaseStatsAt(level + 1);
            SetStatRow(0, now.attack, next.attack, maxed);
            SetStatRow(1, now.critChance, next.critChance, maxed);
            SetStatRow(2, now.critDamage, next.critDamage, maxed);
            SetStatRow(3, now.skillDamage, next.skillDamage, maxed);

            if (maxed)
            {
                SetGauge(-1f);
                stoneText.text = "-";
                stoneText.color = DimColor;
                goldText.text = "-";
                goldText.color = DimColor;
                SetButton(false, "최대 강화");
                if (Time.unscaledTime >= messageUntil) SetMessage("더 이상 강화할 수 없어요.", DimColor);
                return;
            }

            SetGauge(EnhanceRules.SuccessRate(level));
            int needStones = EnhanceRules.StoneCost(level);
            int haveStones = inventory.GetCount(ItemDatabase.EnhanceStone.id);
            stoneText.text = $"{haveStones} / {needStones}";
            stoneText.color = haveStones >= needStones ? GoodColor : BadColor;
            int needGold = EnhanceRules.GoldCost(item, level);
            goldText.text = $"{needGold:N0}  <size=12><color=#9D90B0>(보유 {player.Gold:N0})</color></size>";
            goldText.color = player.Gold >= needGold ? GoldColor : BadColor;

            bool affordable = haveStones >= needStones && player.Gold >= needGold;
            SetButton(affordable, "강화하기");
            if (Time.unscaledTime >= messageUntil)
                SetMessage(affordable ? "[Z] 강화하기" : (haveStones < needStones ? "강화석이 부족해요." : "골드가 부족해요."),
                    affordable ? DimColor : BadColor);
        }

        private void RefreshList()
        {
            int page = candidates.Count == 0 ? 0 : selected / ListPage;
            int pages = Mathf.Max(1, (candidates.Count + ListPage - 1) / ListPage);
            pageText.text = pages > 1 ? $"{page + 1} / {pages}" : "";
            for (int i = 0; i < ListPage; i++)
            {
                int index = page * ListPage + i;
                bool has = index < candidates.Count;
                listFrames[i].color = has ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                listGlows[i].enabled = has && index == selected;
                if (!has)
                {
                    listIcons[i].enabled = false;
                    listNames[i].text = "";
                    listLevels[i].text = "";
                    listEquipped[i].text = "";
                    continue;
                }
                var item = candidates[index];
                var sprite = item.LoadIcon();
                listIcons[i].sprite = sprite;
                listIcons[i].enabled = sprite != null;
                listNames[i].text = sprite != null ? "" : item.itemName;
                int level = inventory.GetEnhanceLevel(item);
                listLevels[i].text = level > 0 ? "+" + level : "";
                listEquipped[i].text = inventory.IsEquipped(item) ? "E" : "";
            }
        }

        private void SetStatRow(int row, float now, float next, bool maxed)
        {
            bool none = Mathf.Approximately(now, 0f) && Mathf.Approximately(next, 0f);
            string suffix = StatRows[row].suffix;
            statLabels[row].color = none ? DimColor : TextColor;
            statNow[row].text = none ? "-" : "+" + now.ToString("0.#") + suffix;
            statNow[row].color = none ? DimColor : TextColor;
            statArrow[row].text = none || maxed ? "" : "▶";
            statNext[row].text = none || maxed ? "" : "+" + next.ToString("0.#") + suffix;
            statNext[row].color = next > now ? UpColor : TextColor;
        }

        private void SetGauge(float rate)
        {
            if (rate < 0f)
            {
                gaugeFill.fillAmount = 0f;
                gaugeText.text = "-";
                gaugeText.color = DimColor;
                noteText.text = "";
                return;
            }
            gaugeFill.fillAmount = rate;
            gaugeFill.color = rate >= 0.7f ? new Color(0.45f, 0.9f, 0.5f) : rate >= 0.4f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.4f, 0.4f);
            gaugeText.text = Mathf.RoundToInt(rate * 100f) + "%";
            gaugeText.color = Color.white;
            noteText.text = "실패해도 강화 단계는 내려가지 않아요 · 재료만 사라져요";
        }

        private void SetButton(bool enabled, string label)
        {
            buttonImage.color = enabled ? Color.white : new Color(0.45f, 0.42f, 0.5f, 1f);
            buttonLabel.text = label;
            buttonLabel.color = enabled ? new Color(1f, 0.95f, 0.85f) : new Color(0.7f, 0.66f, 0.75f);
        }

        private void SetMessage(string text, Color color, float hold = 0f)
        {
            messageText.text = text;
            messageText.color = color;
            if (hold > 0f) messageUntil = Time.unscaledTime + hold;
        }

        // ================================================================ 강화

        private void TryEnhance()
        {
            if (busy) return;
            var item = Selected;
            if (item == null) return;

            int level = inventory.GetEnhanceLevel(item);
            if (level >= EnhanceRules.MaxLevel)
            {
                UiSfx.Error();
                SetMessage("이미 최대 강화 단계예요.", DimColor, 1.5f);
                return;
            }
            int needStones = EnhanceRules.StoneCost(level);
            int needGold = EnhanceRules.GoldCost(item, level);
            if (inventory.GetCount(ItemDatabase.EnhanceStone.id) < needStones)
            {
                UiSfx.Error();
                SetMessage("강화석이 부족해요.", BadColor, 1.5f);
                Shake(itemSlotRect, 6f);
                return;
            }
            if (player.Gold < needGold)
            {
                UiSfx.Error();
                SetMessage("골드가 부족해요.", BadColor, 1.5f);
                Shake(itemSlotRect, 6f);
                return;
            }

            // 재료는 성공/실패와 상관없이 소모된다. 결과는 지금 정하고, 망치질 연출이 끝나면 보여 준다.
            inventory.TryRemoveItem(ItemDatabase.EnhanceStone, needStones);
            player.TrySpendGold(needGold);
            bool success = Random.value < EnhanceRules.SuccessRate(level);
            StartCoroutine(EnhanceSequence(item, level, success));
        }

        private IEnumerator EnhanceSequence(ItemData item, int level, bool success)
        {
            busy = true;
            SetButton(false, "강화 중...");
            SetMessage("", TextColor);
            Vector2 slotCenter = SlotCenter();

            // 망치질 3번: 점점 세게.
            for (int hit = 0; hit < 3; hit++)
            {
                UiSfx.Anvil();
                float power = 0.7f + hit * 0.2f;
                SpawnSparks(slotCenter, 10 + hit * 4, new Color(1f, 0.7f, 0.3f), 260f * power, 0.45f);
                SpawnFlash(slotCenter, 160f * power, new Color(1f, 0.85f, 0.6f, 0.7f), 0.18f);
                yield return Punch(itemSlotRect, 0.86f, 0.16f);
                yield return Wait(0.12f);
            }
            yield return Wait(0.18f);

            if (success)
            {
                inventory.SetEnhanceLevel(item, level + 1);
                UiSfx.EnhanceSuccess();
                SpawnFlash(slotCenter, 340f, new Color(1f, 0.92f, 0.6f, 0.95f), 0.5f);
                SpawnRing(slotCenter, new Color(1f, 0.85f, 0.4f, 1f), 0.6f);
                SpawnSparks(slotCenter, 34, new Color(1f, 0.85f, 0.35f), 420f, 0.9f);
                SpawnFloatingText(slotCenter + new Vector2(0f, 40f), $"강화 성공!  +{level + 1}", GoldColor, 30);
                SetMessage($"{item.DisplayName(level + 1)} 이(가) 되었어요!", GoodColor, 2.2f);
                Refresh();
                yield return Punch(itemSlotRect, 1.18f, 0.3f);
            }
            else
            {
                UiSfx.EnhanceFail();
                SpawnSmoke(slotCenter);
                SpawnFloatingText(slotCenter + new Vector2(0f, 40f), "강화 실패...", BadColor, 26);
                SetMessage($"재료만 사라지고 +{level}은(는) 그대로예요.", BadColor, 2.2f);
                Refresh();
                yield return ShakeRoutine(itemSlotRect, 10f, 0.4f);
            }
            busy = false;
            Refresh();
        }

        // ================================================================ 매 프레임

        private void Update()
        {
            if (!IsOpen) return;
            if (messageUntil > 0f && Time.unscaledTime >= messageUntil)
            {
                messageUntil = 0f;
                if (!busy) Refresh();
            }

            // 고른 칸 빛이 은은하게 숨 쉰다.
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            foreach (var glow in listGlows) if (glow.enabled) glow.color = WithAlpha(SelectColor, 0.8f * pulse);
            itemGlow.rectTransform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.unscaledTime * 2f));

            var keyboard = Keyboard.current;
            if (keyboard == null || busy) return;
            // 연 그 프레임의 입력(엘리 대화의 숫자키 등)이 같은 프레임에 다시 소비되지 않게 한다.
            if (Time.frameCount == openedFrame) return;

            if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame) { Hide(); return; }
            if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) { TryEnhance(); return; }
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) Select(selected - 1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) Select(selected + 1);
            else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) Select(selected - ListColumns);
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) Select(selected + ListColumns);
        }

        // ================================================================ 짓기

        private void BuildPanel()
        {
            panel = new GameObject("EnhancePanel", typeof(RectTransform), typeof(Image));
            var root = (RectTransform)panel.transform;
            root.SetParent(transform, false);
            Stretch(root);
            panel.GetComponent<Image>().color = new Color(0.02f, 0.01f, 0.03f, 1f);   // 뒤 HUD 클릭도 막는다

            // ---- 엘리 일러스트(왼쪽) + 오른쪽 끝을 어둡게 녹이는 그라데이션
            var art = new GameObject("EllyArt", typeof(RectTransform), typeof(RawImage));
            var artRect = (RectTransform)art.transform;
            artRect.SetParent(root, false);
            artRect.anchorMin = new Vector2(0f, 0f);
            artRect.anchorMax = new Vector2(0f, 1f);
            artRect.pivot = new Vector2(0f, 0.5f);
            var artImage = art.GetComponent<RawImage>();
            var newArt = Resources.Load<Texture2D>(ArtPath);
            if (newArt != null)
            {
                artImage.texture = newArt;
                artRect.sizeDelta = new Vector2(720f * newArt.width / newArt.height, 0f);
            }
            else
            {
                artImage.texture = Resources.Load<Texture2D>("UI/Menus/Elly_Enhancement");
                artImage.uvRect = new Rect(0f, 0f, OldArtUvWidth, 1f);
                artRect.sizeDelta = new Vector2(720f * 822f / 1024f, 0f);
            }
            artImage.raycastTarget = false;
            var fade = new GameObject("ArtFade", typeof(RectTransform), typeof(RawImage));
            var fadeRect = (RectTransform)fade.transform;
            fadeRect.SetParent(artRect, false);
            fadeRect.anchorMin = new Vector2(1f, 0f);
            fadeRect.anchorMax = new Vector2(1f, 1f);
            fadeRect.pivot = new Vector2(1f, 0.5f);
            fadeRect.sizeDelta = new Vector2(150f, 0f);
            var fadeImage = fade.GetComponent<RawImage>();
            fadeImage.texture = HorizontalFade();
            fadeImage.color = new Color(0.02f, 0.01f, 0.03f, 1f);
            fadeImage.raycastTarget = false;

            // ---- 오른쪽 패널
            panelRect = NewRect("Panel", root);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.anchoredPosition = new Vector2(-16f, 0f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            var panelImage = panelRect.gameObject.AddComponent<Image>();
            panelImage.sprite = UiArt.LoadOr("UI/Enhance/Panel",
                () => UiArt.RoundedPanel(new Color(0.07f, 0.04f, 0.1f, 0.96f), new Color(0.72f, 0.52f, 0.42f, 1f), 14, 3));
            panelImage.type = Image.Type.Sliced;

            var title = MakeText(panelRect, "Title", "장비 강화", 22, GoldColor, TextAnchor.MiddleLeft);
            Place(title.rectTransform, 116f, 58f, 200f, 30f); // 왼쪽 위 사슬 오른쪽
            var close = MakeButton(panelRect, "Close", 536f, 60f, 30f, 30f, // 오른쪽 위 사슬 왼쪽
                UiArt.LoadOr("UI/Common/KeyCap", () => UiArt.RoundedPanel(new Color(0.16f, 0.1f, 0.22f, 1f), new Color(0.9f, 0.75f, 0.5f, 1f), 6, 2)), Hide);
            var closeLabel = MakeText(close.transform, "X", "X", 18, TextColor, TextAnchor.MiddleCenter);
            Stretch(closeLabel.rectTransform);

            BuildList();
            BuildDetail();

            fxRoot = NewRect("Fx", panelRect);
            Stretch(fxRoot);

            panel.AddComponent<PanelFadeIn>(); // 켜질 때 공통 연출 + 여는 소리
            panel.SetActive(false);
        }

        private void BuildList()
        {
            var label = MakeText(panelRect, "ListLabel", "보유 장비", 15, new Color(0.8f, 0.72f, 0.92f), TextAnchor.MiddleLeft);
            Place(label.rectTransform, ListX, 96f, 190f, 22f);

            var slotSprite = UiArt.LoadOr("UI/Enhance/ListSlot", () => UiArt.LoadOr("UI/HUD/PotionSlot",
                () => UiArt.RoundedPanel(new Color(0.08f, 0.05f, 0.12f, 0.95f), new Color(0.55f, 0.42f, 0.7f, 1f), 8, 2)));
            for (int i = 0; i < ListPage; i++)
            {
                int slot = i;
                float x = ListX + (i % ListColumns) * (ListSlotSize + ListGap);
                float y = ListY + (i / ListColumns) * (ListSlotSize + ListGap);

                var glowRect = NewRect("Glow", panelRect);
                Place(glowRect, x - 10f, y - 10f, ListSlotSize + 20f, ListSlotSize + 20f);
                listGlows[i] = glowRect.gameObject.AddComponent<Image>();
                listGlows[i].sprite = Aethoria.Fx.FxUtil.SoftCircleSprite;
                listGlows[i].raycastTarget = false;

                var button = MakeButton(panelRect, "Slot" + i, x, y, ListSlotSize, ListSlotSize, slotSprite, () =>
                {
                    int page = candidates.Count == 0 ? 0 : selected / ListPage;
                    Select(page * ListPage + slot);
                });
                listFrames[i] = button.GetComponent<Image>();
                var icon = NewRect("Icon", button.transform);
                Stretch(icon, 10f);
                listIcons[i] = icon.gameObject.AddComponent<Image>();
                listIcons[i].preserveAspect = true;
                listIcons[i].raycastTarget = false;
                listNames[i] = MakeText(button.transform, "Name", "", 10, TextColor, TextAnchor.MiddleCenter);
                Stretch(listNames[i].rectTransform, 6f);
                listNames[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                listLevels[i] = MakeText(button.transform, "Level", "", 13, GoldColor, TextAnchor.LowerRight);
                Stretch(listLevels[i].rectTransform, 6f);
                listEquipped[i] = MakeText(button.transform, "Equipped", "", 12, new Color(0.7f, 0.9f, 1f), TextAnchor.UpperLeft);
                Stretch(listEquipped[i].rectTransform, 6f);
            }

            pageText = MakeText(panelRect, "Page", "", 13, DimColor, TextAnchor.MiddleCenter);
            Place(pageText.rectTransform, ListX, ListY + ListRows * (ListSlotSize + ListGap), ListColumns * ListSlotSize + (ListColumns - 1) * ListGap, 20f);

            var hint = MakeText(panelRect, "Hint", "방향키  고르기\nZ  강화하기\nX  닫기", 13, DimColor, TextAnchor.LowerLeft);
            Place(hint.rectTransform, ListX, 548f, 190f, 70f);
            hint.lineSpacing = 1.2f;
        }

        private void BuildDetail()
        {
            float cx = ColumnX + ColumnWidth / 2f;

            // 큰 장비 칸 + 뒤에서 숨 쉬는 등급 색 빛
            var glowRect = NewRect("ItemGlow", panelRect);
            Place(glowRect, cx - 95f, SlotCenterY - 85f, 190f, 170f);
            itemGlow = glowRect.gameObject.AddComponent<Image>();
            itemGlow.sprite = Aethoria.Fx.FxUtil.SoftCircleSprite;
            itemGlow.raycastTarget = false;

            itemSlotRect = NewRect("ItemSlot", panelRect);
            Place(itemSlotRect, cx - 55f, SlotCenterY - 55f, 110f, 110f);
            itemSlotRect.pivot = new Vector2(0.5f, 0.5f);
            itemSlotRect.anchoredPosition += new Vector2(55f, -55f);   // 가운데 기준으로 튀고 흔들리게
            var slotImage = itemSlotRect.gameObject.AddComponent<Image>();
            slotImage.sprite = UiArt.LoadOr("UI/Enhance/ItemSlot", () => UiArt.LoadOr("UI/HUD/PotionSlot",
                () => UiArt.RoundedPanel(new Color(0.08f, 0.05f, 0.12f, 0.95f), new Color(0.75f, 0.55f, 0.45f, 1f), 10, 3)));
            slotImage.raycastTarget = false;
            var icon = NewRect("Icon", itemSlotRect);
            Stretch(icon, 24f);
            itemIcon = icon.gameObject.AddComponent<Image>();
            itemIcon.preserveAspect = true;
            itemIcon.raycastTarget = false;
            itemFallbackLabel = MakeText(itemSlotRect, "Fallback", "", 14, TextColor, TextAnchor.MiddleCenter);
            Stretch(itemFallbackLabel.rectTransform, 12f);
            itemFallbackLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

            itemName = MakeText(panelRect, "ItemName", "", 20, TextColor, TextAnchor.MiddleCenter);
            Place(itemName.rectTransform, ColumnX, 212f, ColumnWidth, 26f);
            itemGrade = MakeText(panelRect, "Grade", "", 13, DimColor, TextAnchor.MiddleCenter);
            Place(itemGrade.rectTransform, ColumnX, 238f, ColumnWidth, 18f);
            levelText = MakeText(panelRect, "Level", "", 19, TextColor, TextAnchor.MiddleCenter);
            Place(levelText.rectTransform, ColumnX, 256f, ColumnWidth, 26f);

            // 능력치 4줄: 이름 · 현재 ▶ 다음
            var rowSprite = UiArt.LoadOr("UI/Enhance/StatRow",
                () => UiArt.RoundedPanel(new Color(0.1f, 0.06f, 0.14f, 0.9f), new Color(0.38f, 0.3f, 0.48f, 1f), 6, 1));
            for (int i = 0; i < 4; i++)
            {
                float y = 290f + i * 31f;
                var row = NewRect("StatRow" + i, panelRect);
                Place(row, ColumnX, y, ColumnWidth, 28f);
                var rowImage = row.gameObject.AddComponent<Image>();
                rowImage.sprite = rowSprite;
                rowImage.type = Image.Type.Sliced;
                rowImage.raycastTarget = false;
                statLabels[i] = MakeText(row, "Label", StatRows[i].label, 15, TextColor, TextAnchor.MiddleLeft);
                Place(statLabels[i].rectTransform, 18f, 0f, 150f, 28f);
                statNow[i] = MakeText(row, "Now", "", 15, TextColor, TextAnchor.MiddleRight);
                Place(statNow[i].rectTransform, 168f, 0f, 84f, 28f);
                statArrow[i] = MakeText(row, "Arrow", "", 12, new Color(0.8f, 0.7f, 0.5f), TextAnchor.MiddleCenter);
                Place(statArrow[i].rectTransform, 254f, 0f, 26f, 28f);
                statNext[i] = MakeText(row, "Next", "", 15, UpColor, TextAnchor.MiddleLeft);
                Place(statNext[i].rectTransform, 282f, 0f, 96f, 28f);
            }

            // 성공 확률 게이지
            var gaugeLabel = MakeText(panelRect, "GaugeLabel", "성공 확률", 15, TextColor, TextAnchor.MiddleLeft);
            Place(gaugeLabel.rectTransform, ColumnX, 420f, 86f, 24f);
            var gaugeArt = Resources.Load<Sprite>("UI/Enhance/Gauge");
            var gaugeRect = NewRect("Gauge", panelRect);
            if (gaugeArt != null) Place(gaugeRect, ColumnX + 88f, 415f, 222f, 34f);
            else Place(gaugeRect, ColumnX + 88f, 422f, 222f, 20f);
            var gaugeBg = gaugeRect.gameObject.AddComponent<Image>();
            gaugeBg.sprite = gaugeArt != null ? gaugeArt
                : UiArt.RoundedPanel(new Color(0.03f, 0.02f, 0.05f, 1f), new Color(0.6f, 0.47f, 0.4f, 1f), 6, 2);
            gaugeBg.type = gaugeArt != null ? Image.Type.Simple : Image.Type.Sliced;
            gaugeBg.raycastTarget = false;
            var fillRect = NewRect("Fill", gaugeRect);
            if (gaugeArt != null)
            {
                // 틀 안쪽 검은 홈에 맞춰 채운다.
                fillRect.anchorMin = GaugeChannelMin;
                fillRect.anchorMax = GaugeChannelMax;
                fillRect.offsetMin = new Vector2(1f, 1f);
                fillRect.offsetMax = new Vector2(-1f, -1f);
            }
            else Stretch(fillRect, 4f);
            gaugeFill = fillRect.gameObject.AddComponent<Image>();
            gaugeFill.UseAsFilled();
            gaugeFill.type = Image.Type.Filled;
            gaugeFill.fillMethod = Image.FillMethod.Horizontal;
            gaugeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            gaugeFill.raycastTarget = false;
            gaugeText = MakeText(panelRect, "GaugeText", "", 17, Color.white, TextAnchor.MiddleRight);
            Place(gaugeText.rectTransform, ColumnX + 312f, 420f, 70f, 24f);
            noteText = MakeText(panelRect, "Note", "", 12, DimColor, TextAnchor.MiddleCenter);
            Place(noteText.rectTransform, ColumnX, 452f, ColumnWidth, 18f);

            // 필요 재료: 강화석 · 골드
            var matLabel = MakeText(panelRect, "MatLabel", "필요 재료", 15, new Color(0.8f, 0.72f, 0.92f), TextAnchor.MiddleLeft);
            Place(matLabel.rectTransform, ColumnX, 474f, 200f, 22f);
            stoneIcon = MakeMaterialCell(ColumnX, ItemDatabase.EnhanceStone.LoadIcon(), out stoneText, 148f);
            MakeMaterialCell(ColumnX + 156f, Resources.Load<Sprite>("UI/HUD/Money"), out goldText, 226f);

            messageText = MakeText(panelRect, "Message", "", 15, DimColor, TextAnchor.MiddleCenter);
            Place(messageText.rectTransform, ColumnX, 548f, ColumnWidth, 24f);

            // 강화하기 버튼
            var buttonSprite = UiArt.LoadOr("UI/Enhance/Button", () => UiArt.LoadOr("UI/Common/Popup/popup_button",
                () => UiArt.RoundedPanel(new Color(0.35f, 0.18f, 0.5f, 1f), new Color(0.95f, 0.8f, 0.55f, 1f), 10, 2)));
            var button = MakeButton(panelRect, "EnhanceButton", cx - 115f, 572f, 230f, 62f, buttonSprite, TryEnhance);
            buttonImage = button.GetComponent<Image>();
            buttonLabel = MakeText(button.transform, "Label", "강화하기", 21, Color.white, TextAnchor.MiddleCenter);
            Stretch(buttonLabel.rectTransform);
        }

        private Image MakeMaterialCell(float x, Sprite iconSprite, out Text text, float width)
        {
            var cell = NewRect("Material", panelRect);
            Place(cell, x, 498f, width, 44f);
            var bg = cell.gameObject.AddComponent<Image>();
            bg.sprite = UiArt.LoadOr("UI/Enhance/StatRow",
                () => UiArt.RoundedPanel(new Color(0.1f, 0.06f, 0.14f, 0.9f), new Color(0.38f, 0.3f, 0.48f, 1f), 6, 1));
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;
            var iconRect = NewRect("Icon", cell);
            Place(iconRect, 10f, 5f, 34f, 34f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            text = MakeText(cell, "Count", "-", 16, TextColor, TextAnchor.MiddleLeft);
            Place(text.rectTransform, 50f, 0f, width - 56f, 44f);
            return icon;
        }

        // ================================================================ 연출

        private Vector2 SlotCenter() => new Vector2(ColumnX + ColumnWidth / 2f, -SlotCenterY);

        private RectTransform FxImage(Sprite sprite, Color color, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Fx", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(fxRoot, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private void SpawnSparks(Vector2 center, int count, Color color, float speed, float life)
        {
            for (int i = 0; i < count; i++)
            {
                bool streak = i % 3 == 0;
                float size = Random.Range(4f, 9f);
                var rect = FxImage(Aethoria.Fx.FxUtil.SoftCircleSprite, Color.Lerp(color, Color.white, Random.value * 0.6f), center,
                    streak ? new Vector2(size * 5f, size * 0.8f) : new Vector2(size, size));
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                var spark = rect.gameObject.AddComponent<FxSpark>();
                spark.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f + 0.4f) * speed * Random.Range(0.5f, 1.1f);
                spark.Life = life * Random.Range(0.7f, 1.2f);
                spark.Streak = streak;
            }
        }

        private void SpawnFlash(Vector2 center, float size, Color color, float life)
        {
            var rect = FxImage(Aethoria.Fx.FxUtil.SoftCircleSprite, color, center, new Vector2(size, size));
            rect.gameObject.AddComponent<FxFade>().Setup(life, 0.6f, 1.4f);
        }

        private void SpawnRing(Vector2 center, Color color, float life)
        {
            var rect = FxImage(Aethoria.Fx.FxUtil.RingSprite, color, center, new Vector2(120f, 120f));
            rect.gameObject.AddComponent<FxFade>().Setup(life, 0.4f, 3.2f);
        }

        private void SpawnSmoke(Vector2 center)
        {
            for (int i = 0; i < 9; i++)
            {
                var rect = FxImage(Aethoria.Fx.FxUtil.SoftCircleSprite, new Color(0.3f, 0.26f, 0.34f, 0.75f),
                    center + Random.insideUnitCircle * 30f, Vector2.one * Random.Range(50f, 80f));
                var fade = rect.gameObject.AddComponent<FxFade>();
                fade.Setup(Random.Range(0.7f, 1.1f), 0.6f, 1.8f);
                fade.Drift = new Vector2(Random.Range(-30f, 30f), Random.Range(40f, 90f));
            }
        }

        private void SpawnFloatingText(Vector2 pos, string content, Color color, int size)
        {
            var text = MakeText(fxRoot, "FloatText", content, size, color, TextAnchor.MiddleCenter);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(400f, 50f);
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.25f, 0.1f, 0.35f, 0.9f);
            shadow.effectDistance = new Vector2(0f, -3f);
            var fade = text.gameObject.AddComponent<FxFade>();
            fade.Setup(1.4f, 0.7f, 1f, popIn: true);
            fade.Drift = new Vector2(0f, 50f);
        }

        private IEnumerator Punch(RectTransform target, float scale, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                target.localScale = Vector3.one * Mathf.Lerp(scale, 1f, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        private void Shake(RectTransform target, float amount) => StartCoroutine(ShakeRoutine(target, amount, 0.25f));

        private IEnumerator ShakeRoutine(RectTransform target, float amount, float duration)
        {
            Vector2 basePos = target.anchoredPosition;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float decay = 1f - t / duration;
                target.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 70f) * amount * decay, 0f);
                yield return null;
            }
            target.anchoredPosition = basePos;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }

        // 불꽃: 튀었다가 아래로 떨어지며 사라진다(실제 시간).
        private class FxSpark : MonoBehaviour
        {
            public Vector2 Velocity;
            public float Life = 0.5f;
            public bool Streak;
            private float age;
            private Image image;
            private RectTransform rect;

            private void Awake()
            {
                image = GetComponent<Image>();
                rect = (RectTransform)transform;
            }

            private void Update()
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                age += dt;
                Velocity += new Vector2(0f, -700f) * dt;
                Velocity *= 1f - 2f * dt;
                rect.anchoredPosition += Velocity * dt;
                if (Streak) rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg);
                var c = image.color;
                c.a = Mathf.Clamp01(1f - age / Life);
                image.color = c;
                if (age >= Life) Destroy(gameObject);
            }
        }

        // 섬광·고리·연기·떠오르는 글자: 커지며(또는 톡 튀어나와) 옅어진다.
        private class FxFade : MonoBehaviour
        {
            public Vector2 Drift;
            private float life = 0.5f, startAlpha = 1f, from = 1f, to = 1f, age;
            private bool popIn;
            private Graphic graphic;
            private RectTransform rect;
            private Vector2 basePos;

            public void Setup(float life, float from, float to, bool popIn = false)
            {
                graphic = GetComponent<Graphic>();
                rect = (RectTransform)transform;
                basePos = rect.anchoredPosition;
                startAlpha = graphic.color.a;
                this.life = life;
                this.from = from;
                this.to = to;
                this.popIn = popIn;
            }

            private void Update()
            {
                age += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float k = Mathf.Clamp01(age / life);
                float scale = popIn
                    ? (k < 0.15f ? Mathf.Lerp(0.5f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((k - 0.15f) / 0.15f)))
                    : Mathf.Lerp(from, to, 1f - (1f - k) * (1f - k));
                rect.localScale = Vector3.one * scale;
                rect.anchoredPosition = basePos + Drift * k;
                var c = graphic.color;
                c.a = startAlpha * (popIn ? Mathf.Clamp01((1f - k) / 0.35f) : (1f - k));
                graphic.color = c;
                if (k >= 1f) Destroy(gameObject);
            }
        }

        // ================================================================ 도우미

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        // 패널 왼쪽 위 기준 (x, y아래로) 위치에 w x h 크기로 놓는다.
        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private Text MakeText(Transform parent, string name, string content, int size, Color color, TextAnchor alignment)
        {
            var rect = NewRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.text = content;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        private static Button MakeButton(RectTransform parent, string name, float x, float y, float w, float h, Sprite sprite, UnityEngine.Events.UnityAction onClick)
        {
            var rect = NewRect(name, parent);
            Place(rect, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            return button;
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private static Texture2D fadeTexture;

        // 왼쪽 투명 → 오른쪽 불투명(가로 그라데이션).
        private static Texture2D HorizontalFade()
        {
            if (fadeTexture != null) return fadeTexture;
            const int w = 64;
            fadeTexture = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < w; x++)
            {
                float k = x / (w - 1f);
                fadeTexture.SetPixel(x, 0, new Color(1f, 1f, 1f, k * k * (3f - 2f * k)));
            }
            fadeTexture.Apply();
            return fadeTexture;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Data;

namespace Aethoria.UI
{
    // 던전(어둠의 숲 보스 기사, 광활한 평야 보스 슬라임)의 보스를 잡으면 전투가 끝난 뒤 스테이지 클리어 창
    // (UI/StageClear)을 띄운다. 이번 던전에 들어온 뒤(BeginRun) 얻은 아이템(최대 5종)과 골드를 보여주고,
    // "다시하기"(그 던전 첫 맵부터) / "마을로 돌아가기"(노아 앞) 중 하나를 고른다.
    // 조작: ← → 선택, Z/Enter 확정, 버튼 클릭. 창이 떠 있는 동안 시간이 멈추고 캐릭터 조작이 잠긴다.
    public class StageClearUI : MonoBehaviour
    {
        // UI/StageClear(1122x1402) 그림 속 칸/버튼 좌표를 픽셀로 잰 값.
        private const float ImageWidth = 1122f;
        private const float ImageHeight = 1402f;
        private const int ItemSlotCount = 5;
        private const float ItemSlotLeft0 = 198f;
        private const float ItemSlotStep = 149f;
        private const float ItemSlotSize = 130f;
        private const float ItemSlotTop = 633f;
        private const float ItemIconInset = 18f;
        private static readonly Rect GoldTextBox = Rect.MinMaxRect(510f, 905f, 840f, 1015f);
        private static readonly Rect RetryButtonBox = Rect.MinMaxRect(175f, 1090f, 515f, 1210f);
        private static readonly Rect VillageButtonBox = Rect.MinMaxRect(610f, 1090f, 950f, 1210f);

        // 기준 해상도(1280x720) 높이에 거의 꽉 차게 그린다. 너비는 그림 비율대로.
        private const float WindowHeight = 700f;
        // 보스가 쓰러진 뒤 이만큼(게임 시간) 쓰러지는 모습을 보여준 뒤 창을 띄운다.
        private const float ShowDelaySeconds = 1.2f;
        private const float FadeInDuration = 0.3f;

        private Character player;
        private CharacterMovement2D playerMovement;
        private Inventory inventory;

        private GameObject panel;
        private CanvasGroup panelGroup;
        private readonly Image[] itemIcons = new Image[ItemSlotCount];
        private readonly Text[] itemCounts = new Text[ItemSlotCount];
        private Text goldText;
        private readonly Image[] buttonDimmers = new Image[2];
        private readonly RawImage[] buttonGlows = new RawImage[2];

        // 이번 던전에서 얻은 것들. 얻은 순서대로 보여준다.
        private readonly List<ItemData> earnedOrder = new();
        private readonly Dictionary<ItemData, int> earnedCounts = new();
        private int earnedGold;
        private int lastGold;

        private Action retryAction;
        private Coroutine showRoutine;
        private int selectedButton; // 0 = 다시하기, 1 = 마을로 돌아가기
        private int openedFrame;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize(Character playerCharacter)
        {
            player = playerCharacter;
            playerMovement = player.GetComponent<CharacterMovement2D>();
            inventory = player.GetComponent<Inventory>();
            lastGold = player.Gold;
            BuildPanel();

            if (inventory != null) inventory.OnItemAdded += HandleItemAdded;
            player.OnGoldChanged += HandleGoldChanged;
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.OnItemAdded -= HandleItemAdded;
            if (player != null) player.OnGoldChanged -= HandleGoldChanged;
        }

        // 던전 첫 맵에 새로 들어갈 때 호출한다. 그때부터 얻은 아이템/골드를 모은다.
        public void BeginRun()
        {
            earnedOrder.Clear();
            earnedCounts.Clear();
            earnedGold = 0;
            lastGold = player != null ? player.Gold : 0;
        }

        private void HandleItemAdded(ItemData item, int count)
        {
            if (item == null || count <= 0) return;

            if (earnedCounts.TryGetValue(item, out int existing))
            {
                earnedCounts[item] = existing + count;
            }
            else
            {
                earnedCounts[item] = count;
                earnedOrder.Add(item);
            }
        }

        // OnGoldChanged는 바뀐 뒤 총액만 알려주므로 직전 값과의 차이로 얻은 양을 구한다(쓴 골드는 세지 않음).
        private void HandleGoldChanged(int gold)
        {
            if (gold > lastGold) earnedGold += gold - lastGold;
            lastGold = gold;
        }

        private void BuildPanel()
        {
            TitleScreenUI.EnsureEventSystem();

            panel = new GameObject("StageClearPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
            panelGroup = panel.GetComponent<CanvasGroup>();

            var windowGO = new GameObject("Window", typeof(RectTransform), typeof(RawImage));
            var windowRect = (RectTransform)windowGO.transform;
            windowRect.SetParent(rect, false);
            windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(WindowHeight * ImageWidth / ImageHeight, WindowHeight);
            var windowImage = windowGO.GetComponent<RawImage>();
            windowImage.texture = Resources.Load<Texture2D>("UI/StageClear");
            windowImage.raycastTarget = false;

            for (int i = 0; i < ItemSlotCount; i++)
            {
                float left = ItemSlotLeft0 + ItemSlotStep * i;
                var slot = Rect.MinMaxRect(left, ItemSlotTop, left + ItemSlotSize, ItemSlotTop + ItemSlotSize);
                BuildItemSlot(windowRect, i, slot);
            }

            goldText = BuildText(windowRect, "Gold", GoldTextBox, 30, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);

            BuildButton(windowRect, 0, RetryButtonBox);
            BuildButton(windowRect, 1, VillageButtonBox);

            panel.SetActive(false);
        }

        private void BuildItemSlot(RectTransform parent, int index, Rect slot)
        {
            var inner = Rect.MinMaxRect(slot.xMin + ItemIconInset, slot.yMin + ItemIconInset, slot.xMax - ItemIconInset, slot.yMax - ItemIconInset);
            var iconGO = new GameObject("ItemIcon_" + index, typeof(RectTransform), typeof(Image));
            PlaceInImage((RectTransform)iconGO.transform, parent, inner);
            var icon = iconGO.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            itemIcons[index] = icon;

            // 개수는 칸 오른쪽 아래 구석에 작게.
            var countBox = Rect.MinMaxRect(slot.xMin, slot.yMax - 46f, slot.xMax - 10f, slot.yMax - 4f);
            var count = BuildText(parent, "ItemCount_" + index, countBox, 18, Color.white, TextAnchor.LowerRight);
            var outline = count.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            itemCounts[index] = count;
        }

        private void BuildButton(RectTransform parent, int index, Rect box)
        {
            // 선택된 버튼 아래쪽에 은은하게 맥동하는 빛(인벤토리 탭 표시와 같은 그림).
            var glowGO = new GameObject("ButtonGlow_" + index, typeof(RectTransform), typeof(RawImage));
            var glowBox = Rect.MinMaxRect(box.xMin + 30f, box.yMax - 24f, box.xMax - 30f, box.yMax + 14f);
            PlaceInImage((RectTransform)glowGO.transform, parent, glowBox);
            var glow = glowGO.GetComponent<RawImage>();
            glow.texture = Resources.Load<Texture2D>("UI/Select_Effect");
            glow.raycastTarget = false;
            buttonGlows[index] = glow;

            // 선택되지 않은 버튼은 어둡게 덮는다. 클릭도 이 위에서 받는다.
            var buttonGO = new GameObject("Button_" + index, typeof(RectTransform), typeof(Image), typeof(Button));
            var inner = Rect.MinMaxRect(box.xMin + 12f, box.yMin + 14f, box.xMax - 12f, box.yMax - 14f);
            PlaceInImage((RectTransform)buttonGO.transform, parent, inner);
            var dimmer = buttonGO.GetComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0f);
            buttonDimmers[index] = dimmer;

            var button = buttonGO.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Confirm(index));
        }

        // 그림 속 픽셀 사각형(왼쪽 위 기준)을 창 RectTransform 안의 앵커로 바꿔 붙인다.
        private static void PlaceInImage(RectTransform rect, RectTransform parent, Rect pixelBox)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(pixelBox.xMin / ImageWidth, 1f - pixelBox.yMax / ImageHeight);
            rect.anchorMax = new Vector2(pixelBox.xMax / ImageWidth, 1f - pixelBox.yMin / ImageHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text BuildText(RectTransform parent, string name, Rect pixelBox, int fontSize, Color color, TextAnchor alignment)
        {
            var textGO = new GameObject(name, typeof(RectTransform), typeof(Text));
            PlaceInImage((RectTransform)textGO.transform, parent, pixelBox);

            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        // 보스 몬스터의 OnDied에 연결된다. retry는 "다시하기"를 골랐을 때 들어갈 던전 첫 맵.
        public void Show(Action retry)
        {
            retryAction = retry;
            if (showRoutine != null) StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(ShowAfterBattle());
        }

        // 스테이지가 새로 지어질 때 호출한다. 창이 뜨기 전에 포탈로 나가버렸거나 판을 새로 시작했으면
        // 늦게 창이 뜨지 않도록 대기를 취소한다.
        public void Cancel()
        {
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }
            if (IsOpen) Close();
        }

        private IEnumerator ShowAfterBattle()
        {
            yield return new WaitForSeconds(ShowDelaySeconds);

            // 그 사이 플레이어가 쓰러졌다면 사망 연출(DeathReturnUI)이 우선이다.
            showRoutine = null;
            if (player == null || player.IsDead) yield break;

            Open();

            float t = 0f;
            while (t < FadeInDuration && IsOpen)
            {
                t += Time.unscaledDeltaTime;
                panelGroup.alpha = Mathf.Clamp01(t / FadeInDuration);
                yield return null;
            }
            panelGroup.alpha = 1f;
        }

        private void Open()
        {
            RefreshRewards();
            selectedButton = 1; // 실수로 Z를 연타해도 같은 던전에 다시 끌려가지 않게 기본은 마을로
            RefreshButtons();

            panelGroup.alpha = 0f;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling(); // 나중에 만들어진 HUD 요소들보다 위에 그린다
            openedFrame = Time.frameCount;
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        private void Close()
        {
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void RefreshRewards()
        {
            for (int i = 0; i < ItemSlotCount; i++)
            {
                if (i < earnedOrder.Count)
                {
                    var item = earnedOrder[i];
                    itemIcons[i].sprite = string.IsNullOrEmpty(item.iconPath) ? null : Resources.Load<Sprite>(item.iconPath);
                    itemIcons[i].enabled = itemIcons[i].sprite != null;
                    int count = earnedCounts[item];
                    itemCounts[i].text = count > 1 ? "x" + count : string.Empty;
                }
                else
                {
                    itemIcons[i].enabled = false;
                    itemCounts[i].text = string.Empty;
                }
            }

            goldText.text = "+" + earnedGold.ToString("N0") + " G";
        }

        private void RefreshButtons()
        {
            for (int i = 0; i < buttonDimmers.Length; i++)
            {
                bool selected = i == selectedButton;
                buttonDimmers[i].color = new Color(0f, 0f, 0f, selected ? 0f : 0.45f);
                buttonGlows[i].enabled = selected;
            }
        }

        private void Confirm(int index)
        {
            if (!IsOpen) return;

            Close();
            if (index == 0 && retryAction != null) retryAction();
            else GameBootstrap.ReturnToVillage(spawnAtNoa: true);
        }

        private void Update()
        {
            if (!IsOpen) return;

            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f);
            buttonGlows[selectedButton].color = new Color(1f, 1f, 1f, pulse);

            // 보스를 잡은 마지막 키 입력이 같은 프레임에 확정으로 먹히지 않게 연 프레임은 건너뛴다.
            var keyboard = Keyboard.current;
            if (keyboard == null || Time.frameCount == openedFrame) return;

            if (keyboard.leftArrowKey.wasPressedThisFrame && selectedButton != 0)
            {
                selectedButton = 0;
                RefreshButtons();
            }
            else if (keyboard.rightArrowKey.wasPressedThisFrame && selectedButton != 1)
            {
                selectedButton = 1;
                RefreshButtons();
            }
            else if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                Confirm(selectedButton);
            }
        }
    }
}

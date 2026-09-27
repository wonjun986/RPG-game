using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Quests;
using Aethoria.Save;

namespace Aethoria.UI
{
    // 슬롯 5칸짜리 저장/불러오기 화면(UI/Save_Screen.png).
    // - 저장 모드(Initialize): 노아에게서 연다. 이미 저장이 있는 슬롯은 한 번 더 확인(Z 예 / X 아니오)한 뒤 덮어쓴다.
    // - 불러오기 모드(InitializeForLoad): 타이틀의 CONTINUE에서 연다. 저장된 슬롯을 고르면 그 슬롯으로 게임을 시작한다.
    // ↑/↓ 또는 마우스 호버로 슬롯 선택, Z/Enter/클릭으로 실행, X/Esc로 닫기.
    // 선택된 슬롯에는 원본 그림의 빛나는 1번 슬롯을 잘라낸 UI/Save_SlotSelected.png를 덮어 그린다.
    // 저장 모드로 떠 있는 동안은 게임을 정지하고 플레이어 입력을 잠근다.
    public class SaveScreenUI : MonoBehaviour
    {
        private enum ScreenState { Browsing, ConfirmOverwrite, ConfirmDelete }

        // Save_Screen.png(1671x941) 픽셀 기준 좌표. 각 슬롯 테두리의 윗선 y.
        private const float ImageWidth = 1671f;
        private const float ImageHeight = 941f;
        private static readonly float[] SlotTopPx = { 158f, 291f, 423f, 555f, 688f };
        private const float SlotHeightPx = 118f;
        // Save_SlotSelected.png는 슬롯 윗선보다 14px 위에서 시작하고 높이 146px, x 342~1594 범위다.
        private const float HighlightTopOffsetPx = -14f;
        private const float HighlightHeightPx = 146f;
        private const float HighlightXMinPx = 342f;
        private const float HighlightXMaxPx = 1594f;
        // 슬롯 안의 썸네일 칸과 글자 칸.
        private const float ThumbXMinPx = 545f, ThumbXMaxPx = 763f;
        private const float InfoXMinPx = 800f, InfoXMaxPx = 1500f;

        private const float SavedMessageSeconds = 1.5f;
        private const string SaveBrowseHint = "↑↓ 슬롯 선택    [Z] 저장    [Delete] 삭제    [X] 닫기";
        private const string LoadBrowseHint = "↑↓ 슬롯 선택    [Z] 불러오기    [Delete] 삭제    [X] 닫기";

        private CharacterMovement2D playerMovement;
        private bool loadMode;
        private System.Action<int> onLoadSlot;
        private int openedFrame;
        private GameObject root;
        private RectTransform highlight;
        private readonly Text[] slotTexts = new Text[SaveSystem.SlotCount];
        private readonly Image[] slotThumbs = new Image[SaveSystem.SlotCount];
        private Text hintText;
        private Sprite portrait;

        private ScreenState state;
        private int selected;
        private Coroutine hintRoutine;

        public bool IsOpen => root != null && root.activeSelf;
        private string BrowseHint => loadMode ? LoadBrowseHint : SaveBrowseHint;

        // 저장 모드: 노아의 [3] 저장.
        public void Initialize(CharacterMovement2D movement)
        {
            playerMovement = movement;
            loadMode = false;
            Setup();
        }

        // 불러오기 모드: 타이틀의 CONTINUE. 저장된 슬롯을 고르면 onLoad(슬롯 번호 1~5)를 부른다.
        public void InitializeForLoad(System.Action<int> onLoad)
        {
            onLoadSlot = onLoad;
            loadMode = true;
            Setup();
        }

        private void Setup()
        {
            TitleScreenUI.EnsureEventSystem();

            var idleFrames = Resources.LoadAll<Sprite>("Art/Necrosia/Idle");
            if (idleFrames != null && idleFrames.Length > 0)
            {
                System.Array.Sort(idleFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
                portrait = idleFrames[0];
            }

            Build();
        }

        private void Build()
        {
            root = new GameObject("SaveScreen", typeof(RectTransform), typeof(RawImage));
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(transform, false);
            Stretch(rootRect);
            var background = root.GetComponent<RawImage>();
            background.texture = Resources.Load<Texture2D>("UI/Save_Screen");
            background.raycastTarget = true; // 뒤쪽 HUD 클릭을 막는다

            var highlightGO = new GameObject("SelectedSlot", typeof(RectTransform), typeof(RawImage));
            highlight = (RectTransform)highlightGO.transform;
            highlight.SetParent(rootRect, false);
            var highlightImage = highlightGO.GetComponent<RawImage>();
            highlightImage.texture = Resources.Load<Texture2D>("UI/Save_SlotSelected");
            highlightImage.raycastTarget = false;

            for (int i = 0; i < SaveSystem.SlotCount; i++)
            {
                float top = SlotTopPx[i];
                int index = i;

                var button = TitleScreenUI.BuildButton(rootRect, $"Slot{i + 1}",
                    PixelToAnchor(HighlightXMinPx, top + SlotHeightPx), PixelToAnchor(HighlightXMaxPx, top),
                    () => OnSlotClicked(index));
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.gameObject.AddComponent<HoverRelay>().Entered = () => Select(index);

                var thumbGO = new GameObject("Thumbnail", typeof(RectTransform), typeof(Image));
                var thumbRect = (RectTransform)thumbGO.transform;
                thumbRect.SetParent(rootRect, false);
                SetPixelRect(thumbRect, ThumbXMinPx, top + 12f, ThumbXMaxPx, top + SlotHeightPx - 12f);
                var thumb = thumbGO.GetComponent<Image>();
                thumb.sprite = portrait;
                thumb.preserveAspect = true;
                thumb.raycastTarget = false;
                slotThumbs[i] = thumb;

                var infoRect = CreateText(rootRect, "Info", 22, TextAnchor.MiddleLeft, out var info);
                SetPixelRect(infoRect, InfoXMinPx, top + 6f, InfoXMaxPx, top + SlotHeightPx - 6f);
                slotTexts[i] = info;
            }

            var hintRect = CreateText(rootRect, "Hint", 22, TextAnchor.MiddleRight, out hintText);
            SetPixelRect(hintRect, 700f, 30f, 1590f, 92f);

            root.SetActive(false);
        }

        // 저장 모드는 지금 자동 저장이 쓰고 있는 슬롯을, 불러오기 모드는 가장 최근에 저장된 슬롯을 먼저 선택해 둔다.
        public void Open()
        {
            if (IsOpen) return;

            state = ScreenState.Browsing;
            RefreshSlots();
            Select(Mathf.Clamp(MarkedSlot() - 1, 0, SaveSystem.SlotCount - 1));
            SetHint(BrowseHint);

            root.transform.SetAsLastSibling(); // 나중에 만들어진 HUD 요소들보다 위에 그린다
            root.SetActive(true);
            openedFrame = Time.frameCount;
            if (loadMode) return;

            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        private int MarkedSlot() => loadMode ? SaveSystem.MostRecentSlot() : GameBootstrap.ActiveSlot;

        private void Close()
        {
            if (hintRoutine != null)
            {
                StopCoroutine(hintRoutine);
                hintRoutine = null;
            }
            root.SetActive(false);
            if (loadMode) return;

            Time.timeScale = 1f;
            // X/Z는 스킬 키이기도 해서, 닫은 바로 그 프레임에 입력을 풀면 같은 키로 스킬까지 나간다. 한 프레임 뒤에 풀어준다.
            if (playerMovement != null) StartCoroutine(UnlockInputNextFrame());
        }

        private IEnumerator UnlockInputNextFrame()
        {
            yield return null;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void Update()
        {
            // 여는 데 쓴 키(타이틀 CONTINUE의 Enter 등)가 같은 프레임에 바로 실행으로 이어지지 않게 한다.
            if (!IsOpen || Time.frameCount == openedFrame) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool confirm = keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            bool cancel = keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame;

            if (state == ScreenState.ConfirmOverwrite)
            {
                if (confirm) SaveSelected();
                else if (cancel) BackToBrowsing();
                return;
            }

            if (state == ScreenState.ConfirmDelete)
            {
                if (confirm) DeleteSelected();
                else if (cancel) BackToBrowsing();
                return;
            }

            if (keyboard.upArrowKey.wasPressedThisFrame) Select((selected + SaveSystem.SlotCount - 1) % SaveSystem.SlotCount);
            else if (keyboard.downArrowKey.wasPressedThisFrame) Select((selected + 1) % SaveSystem.SlotCount);
            else if (confirm) Execute();
            else if (keyboard.deleteKey.wasPressedThisFrame) TryDelete();
            else if (cancel) Close();
        }

        private void Select(int index)
        {
            if (state != ScreenState.Browsing) return; // 덮어쓰기/삭제 확인 중에는 선택을 바꾸지 않는다
            selected = index;
            float top = SlotTopPx[index] + HighlightTopOffsetPx;
            SetPixelRect(highlight, HighlightXMinPx, top, HighlightXMaxPx, top + HighlightHeightPx);
        }

        private void OnSlotClicked(int index)
        {
            if (state == ScreenState.ConfirmOverwrite)
            {
                if (index == selected) SaveSelected();
                return;
            }
            if (state == ScreenState.ConfirmDelete) return; // 삭제 확인은 실수 방지를 위해 Z/X로만 받는다

            Select(index);
            Execute();
        }

        private void Execute()
        {
            if (loadMode) TryLoad();
            else TrySave();
        }

        private void TryLoad()
        {
            int slot = selected + 1;
            if (!SaveSystem.SlotExists(slot))
            {
                ShowTemporaryHint("비어 있는 슬롯입니다.");
                return;
            }
            if (SaveSystem.Load(slot) == null)
            {
                ShowTemporaryHint("저장 파일을 읽을 수 없습니다.");
                return;
            }

            Close();
            onLoadSlot?.Invoke(slot);
        }

        private void TrySave()
        {
            int slot = selected + 1;
            if (SaveSystem.SlotExists(slot))
            {
                state = ScreenState.ConfirmOverwrite;
                SetHint($"슬롯 {slot}에 덮어쓸까요?    [Z] 예    [X] 아니오");
                return;
            }
            SaveSelected();
        }

        private void SaveSelected()
        {
            state = ScreenState.Browsing;
            int slot = selected + 1;
            if (GameBootstrap.SaveGameToSlot(slot))
            {
                RefreshSlots();
                ShowTemporaryHint($"슬롯 {slot}에 저장했습니다.");
            }
            else
            {
                ShowTemporaryHint("지금은 저장할 수 없습니다.");
            }
        }

        private void BackToBrowsing()
        {
            state = ScreenState.Browsing;
            SetHint(BrowseHint);
        }

        private void TryDelete()
        {
            int slot = selected + 1;
            if (!SaveSystem.SlotExists(slot))
            {
                ShowTemporaryHint("비어 있는 슬롯입니다.");
                return;
            }

            state = ScreenState.ConfirmDelete;
            SetHint($"슬롯 {slot}을(를) 삭제할까요? 되돌릴 수 없습니다.    [Z] 예    [X] 아니오");
        }

        private void DeleteSelected()
        {
            state = ScreenState.Browsing;
            int slot = selected + 1;
            SaveSystem.Delete(slot);
            RefreshSlots();
            ShowTemporaryHint($"슬롯 {slot}을(를) 삭제했습니다.");
        }

        private void RefreshSlots()
        {
            var questLine = QuestDatabase.CreateMainLine();
            int markedSlot = MarkedSlot();
            for (int i = 0; i < SaveSystem.SlotCount; i++)
            {
                int slot = i + 1;
                var data = SaveSystem.Load(slot);
                slotThumbs[i].enabled = data != null && portrait != null;

                string header = $"<size=26><b>슬롯 {slot}</b></size>";
                if (slot == markedSlot) header += loadMode ? "   <color=#D9A8FF>● 최근 저장</color>" : "   <color=#D9A8FF>● 진행 중</color>";

                if (data == null)
                {
                    slotTexts[i].text = $"{header}\n<color=#8C7FA3>비어 있음</color>";
                    continue;
                }

                slotTexts[i].text = $"{header}\nLv. {data.level}    {DescribeQuest(questLine, data)}\n<color=#B9A6D6>{data.savedAt}</color>";
            }
        }

        private static string DescribeQuest(QuestData[] questLine, SaveData data)
        {
            var status = (QuestStatus)data.questStatus;
            if (status == QuestStatus.AllDone || data.questIndex < 0 || data.questIndex >= questLine.Length) return "모든 의뢰 완료";

            var quest = questLine[data.questIndex];
            switch (status)
            {
                case QuestStatus.InProgress: return $"{quest.title} ({data.questProgress}/{quest.requiredCount})";
                case QuestStatus.ReadyToTurnIn: return $"{quest.title} (보고 대기)";
                default: return $"다음 의뢰: {quest.title}";
            }
        }

        private void SetHint(string text)
        {
            if (hintRoutine != null)
            {
                StopCoroutine(hintRoutine);
                hintRoutine = null;
            }
            hintText.text = text;
        }

        private void ShowTemporaryHint(string text)
        {
            SetHint(text);
            hintRoutine = StartCoroutine(RestoreHintLater());
        }

        // 저장 화면이 떠 있는 동안 timeScale이 0이라 실시간으로 잰다.
        private IEnumerator RestoreHintLater()
        {
            yield return new WaitForSecondsRealtime(SavedMessageSeconds);
            hintRoutine = null;
            if (state == ScreenState.Browsing) hintText.text = BrowseHint;
        }

        private static RectTransform CreateText(Transform parent, string name, int fontSize, TextAnchor alignment, out Text text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.95f, 0.92f, 1f);
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // 그림 픽셀 좌표(왼쪽 위 원점)를 화면 비율 앵커(왼쪽 아래 원점)로 바꾼다.
        private static Vector2 PixelToAnchor(float x, float y) => new Vector2(x / ImageWidth, 1f - y / ImageHeight);

        private static void SetPixelRect(RectTransform rect, float xMin, float yTop, float xMax, float yBottom)
        {
            rect.anchorMin = PixelToAnchor(xMin, yBottom);
            rect.anchorMax = PixelToAnchor(xMax, yTop);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private class HoverRelay : MonoBehaviour, IPointerEnterHandler
        {
            public System.Action Entered;

            public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();
        }
    }
}

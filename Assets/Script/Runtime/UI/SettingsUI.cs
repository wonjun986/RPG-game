using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Save;

namespace Aethoria.UI
{
    // 설정 창(타이틀 SETTINGS, 필드 Esc 메뉴의 [설정]). 그림은 UI/Menus/Settings(원본 Sprite/UI/HUD) — 창 그림에 "설정" 제목,
    // 닫기 X, 탭 이름(화면/사운드/게임/조작)이 그려져 있고, 항목 줄·슬라이더·스위치·버튼은 코드로 얹는다. 바꾸는 즉시 적용·저장된다(GameSettings).
    //  화면: 화면 모드, 해상도, 프레임, 화면 효과 / 사운드: 전체·배경음·효과음 볼륨 / 게임: 화면 흔들림, 데미지 숫자, 대사 속도 / 조작: 키 안내.
    // 조작: Q/E 탭, ↑↓(W/S) 줄, ←→(A/D) 값, Z/Enter 누르기(스위치 뒤집기·버튼), Esc/X 닫기.
    // 마우스: 탭·◀▶·스위치를 누르고, 슬라이더는 누르거나 끌어서 바꾸고, 줄 위에 올리면 그 줄이 골라진다.
    // 여닫기는 장비창과 같은 연출(PanelTransition)이고, 탭을 바꾸면 줄이 차례로 밀려 들어온다.
    public class SettingsUI : MonoBehaviour
    {
        private const string ArtFolder = "UI/Menus/Settings/";
        // 창 그림(1122x1402) 속 좌표(픽셀, 위에서부터 y).
        private const float ArtWidth = 1122f;
        private const float ArtHeight = 1402f;
        private const float PanelHeight = 660f;     // 캔버스(1280x720) 단위
        private static readonly Vector2[] TabX = { new(85f, 315f), new(327f, 555f), new(567f, 795f), new(807f, 1037f) };
        private static readonly Vector2 TabY = new(225f, 312f);
        private static readonly Rect CloseBox = Rect.MinMaxRect(1003f, 103f, 1080f, 170f);
        private static readonly Rect ContentBox = Rect.MinMaxRect(104f, 372f, 1020f, 1150f);
        private static readonly Rect BottomBox = Rect.MinMaxRect(72f, 1190f, 1052f, 1312f);
        private const float RowHeightPx = 118f;
        private const float RowGapPx = 22f;

        private static readonly string[] TabNames = { "화면", "사운드", "게임", "조작" };
        private static readonly Color LabelColor = new(0.86f, 0.8f, 0.95f);
        private static readonly Color SelectedLabelColor = new(1f, 0.93f, 0.75f);
        private static readonly Color ValueColor = Color.white;
        private static readonly Color RowTint = new(0.78f, 0.74f, 0.84f);
        private static readonly Color TabSelectedColor = new(0.62f, 0.35f, 1f, 0.32f);
        private static readonly Color SelectedGlowColor = new(0.68f, 0.38f, 1f, 0.45f);

        private enum Kind { Choice, Toggle, Slider, Button }

        private class Row
        {
            public Kind kind;
            public RectTransform root;
            public CanvasGroup group;
            public Image plate;         // 줄 판(버튼이면 버튼 그림)
            public Image glow;          // 고른 줄 뒤의 보라 빛
            public Text label;
            public Text value;
            public Image toggleImage;
            public Image sliderFill;
            public RectTransform sliderKnob;
            public RectTransform sliderTrack;
            public System.Func<string> describe;
            public System.Func<bool> isOn;
            public System.Func<float> get01;
            public System.Action<float> set01;
            public System.Action<int> change;   // ←(-1)/→(+1)
            public System.Action press;         // Z/Enter
        }

        private Font font;
        private GameObject root;
        private RectTransform panel;
        private PanelTransition transition;
        private RectTransform tabHighlight;
        private readonly List<Row>[] tabRows = new List<Row>[4];
        private readonly GameObject[] tabPages = new GameObject[4];
        private readonly List<Row> bottomRows = new();
        private readonly List<Row> active = new();
        private int currentTab;
        private int selected;
        private int openedFrame = -1;
        private int closedFrame = -1;
        private float pageAnimStart = -10f;
        private System.Action onClosed;
        private Sprite toggleOn, toggleOff, softGlow;

        public bool IsOpen => root != null && root.activeSelf && (transition == null || !transition.IsClosing);
        // 닫는 그 프레임의 Enter/Esc가 뒤쪽 메뉴까지 누르지 않게, 부른 쪽은 이 프레임을 건너뛴다.
        public bool ClosedThisFrame => closedFrame == Time.frameCount;

        public static SettingsUI Create(Transform canvas)
        {
            var go = new GameObject("SettingsUI", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var ui = go.AddComponent<SettingsUI>();
            ui.Build(canvas);
            return ui;
        }

        // startTab: 처음 보여 줄 탭(0 화면, 1 사운드, 2 게임, 3 조작). 일시정지 메뉴의 "조작법"은 3으로 연다.
        public void Open(System.Action closed, int startTab = 0)
        {
            onClosed = closed;
            openedFrame = Time.frameCount;
            root.transform.SetAsLastSibling();
            // 창이 떠 있는 동안 Enter가 뒤쪽 메뉴 버튼까지 누르지 않도록 선택을 비운다.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            SelectTab(Mathf.Clamp(startTab, 0, TabNames.Length - 1), force: true);
            transition.PlayOpen();
            pageAnimStart += 0.12f; // 창이 올라오는 동안은 줄 연출을 조금 늦춘다
        }

        public void Close()
        {
            if (!IsOpen) return;
            closedFrame = Time.frameCount;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            transition.PlayClose();
            onClosed?.Invoke();
        }

        // ---------------------------------------------------------------- 짓기

        private void Build(Transform canvas)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            toggleOn = Load("toggle_on");
            toggleOff = Load("toggle_off");
            softGlow = MakeSoftGlow();

            root = new GameObject("SettingsDialog", typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)root.transform;
            dimRect.SetParent(canvas, false);
            Place(dimRect, 0f, 1f, 0f, 1f);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            panel = NewRect("Panel", dimRect);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelHeight * ArtWidth / ArtHeight, PanelHeight);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = Load("settings_panel");
            panelImage.color = panelImage.sprite != null ? Color.white : new Color(0.06f, 0.04f, 0.09f, 0.97f);

            tabHighlight = Box(panel, "TabHighlight", Rect.MinMaxRect(TabX[0].x + 4f, TabY.x + 4f, TabX[0].y - 4f, TabY.y - 4f));
            var tabImage = tabHighlight.gameObject.AddComponent<Image>();
            tabImage.color = TabSelectedColor;
            tabImage.raycastTarget = false;
            for (int i = 0; i < TabX.Length; i++)
            {
                int tab = i;
                var target = Box(panel, "Tab_" + TabNames[i], Rect.MinMaxRect(TabX[i].x, TabY.x, TabX[i].y, TabY.y));
                target.gameObject.AddComponent<Image>().color = Color.clear;
                target.gameObject.AddComponent<Pointer>().down = () => SelectTab(tab);
            }
            var close = Box(panel, "Close", CloseBox);
            close.gameObject.AddComponent<Image>().color = Color.clear;
            close.gameObject.AddComponent<Pointer>().down = Close;

            BuildDisplayPage();
            BuildSoundPage();
            BuildGamePage();
            BuildControlsPage();
            BuildBottomButtons();

            transition = root.AddComponent<PanelTransition>();
            root.SetActive(false);
        }

        private void BuildDisplayPage()
        {
            var resolutions = GameSettings.AvailableResolutions;
            var page = NewPage(0);
            AddToggle(page, 0, "전체 화면", () => GameSettings.Fullscreen, () => GameSettings.SetFullscreen(!GameSettings.Fullscreen));
            AddChoice(page, 0, "해상도", () => $"{GameSettings.Resolution.x} x {GameSettings.Resolution.y}",
                d => GameSettings.SetResolution(resolutions[Step(System.Array.IndexOf(resolutions, GameSettings.Resolution), d, resolutions.Length)]));
            AddChoice(page, 0, "프레임", () => FrameLabel(GameSettings.FrameLimit),
                d => GameSettings.SetFrameLimit(GameSettings.FrameLimits[Step(System.Array.IndexOf(GameSettings.FrameLimits, GameSettings.FrameLimit), d, GameSettings.FrameLimits.Length)]));
            AddToggle(page, 0, "화면 효과", () => GameSettings.PostProcessing, () => GameSettings.SetPostProcessing(!GameSettings.PostProcessing));
        }

        private void BuildSoundPage()
        {
            var page = NewPage(1);
            AddSlider(page, 1, "전체 볼륨", () => GameSettings.MasterVolume, GameSettings.SetMasterVolume);
            AddSlider(page, 1, "배경음", () => GameSettings.BgmVolume, GameSettings.SetBgmVolume);
            AddSlider(page, 1, "효과음", () => GameSettings.SfxVolume, GameSettings.SetSfxVolume);
        }

        private void BuildGamePage()
        {
            var page = NewPage(2);
            AddToggle(page, 2, "화면 흔들림", () => GameSettings.ScreenShake, () => GameSettings.SetScreenShake(!GameSettings.ScreenShake));
            AddToggle(page, 2, "데미지 숫자", () => GameSettings.DamageNumbers, () => GameSettings.SetDamageNumbers(!GameSettings.DamageNumbers));
            AddChoice(page, 2, "대사 속도", () => GameSettings.DialogueSpeedNames[GameSettings.DialogueSpeed],
                d => GameSettings.SetDialogueSpeed(Step(GameSettings.DialogueSpeed, d, GameSettings.DialogueSpeedNames.Length)));
        }

        // 조작 탭: 키와 동작을 두 줄씩 나란히 보여 준다(보기만).
        private static readonly (string key, string action)[] Controls =
        {
            ("← →", "이동"), ("↑", "점프 (공중에서 한 번 더)"), ("↓", "발판 아래로 내려가기"), ("Z", "공격 · 대화 · 확인"),
            ("Shift", "대시"), ("Q W E A S D R", "스킬"), ("F", "포션 마시기"), ("I", "장비창"),
            ("C", "스탯창"), ("M", "던전 지도 크게 보기"), ("Esc", "메뉴 · 닫기"), ("X", "취소 · 닫기"),
        };

        private void BuildControlsPage()
        {
            var page = NewPage(3);
            tabRows[3] = new List<Row>();
            float lineHeight = (ContentBox.height - 20f) / Controls.Length;
            for (int i = 0; i < Controls.Length; i++)
            {
                float top = ContentBox.yMin + 10f + i * lineHeight;
                var line = Box(page, "Control_" + i, Rect.MinMaxRect(ContentBox.xMin, top, ContentBox.xMax, top + lineHeight - 8f));
                var plate = line.gameObject.AddComponent<Image>();
                plate.sprite = SlicedRow();
                plate.type = Image.Type.Sliced;
                plate.pixelsPerUnitMultiplier = 4f;
                plate.color = new Color(1f, 1f, 1f, 0.55f);
                var key = MakeText(line, "Key", Controls[i].key, 17, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.45f), FontStyle.Bold);
                Place(key.rectTransform, 0.04f, 0.38f, 0f, 1f);
                var action = MakeText(line, "Action", Controls[i].action, 17, TextAnchor.MiddleLeft, LabelColor, FontStyle.Normal);
                Place(action.rectTransform, 0.42f, 0.97f, 0f, 1f);
                var group = line.gameObject.AddComponent<CanvasGroup>();
                tabRows[3].Add(new Row { kind = Kind.Button, root = line, group = group, label = action, plate = plate });
            }
        }

        private void BuildBottomButtons()
        {
            var buttonSprite = Resources.Load<Sprite>("UI/Common/Popup/popup_button");
            float centerY = (BottomBox.yMin + BottomBox.yMax) / 2f;
            float height = 82f;
            float width = buttonSprite != null ? height * buttonSprite.rect.width / buttonSprite.rect.height : 360f;
            AddBottomButton("기본값으로", 0.3f, centerY, width, height, buttonSprite, () => { GameSettings.ResetToDefaults(); Refresh(); });
            AddBottomButton("닫기", 0.7f, centerY, width, height, buttonSprite, Close);
        }

        private void AddBottomButton(string text, float centerXFraction, float centerY, float width, float height, Sprite sprite, System.Action press)
        {
            float cx = BottomBox.xMin + BottomBox.width * centerXFraction;
            var glowRect = Box(panel, text + "Glow", Rect.MinMaxRect(cx - width * 0.62f, centerY - height * 0.95f, cx + width * 0.62f, centerY + height * 0.95f));
            var glow = glowRect.gameObject.AddComponent<Image>();
            glow.sprite = softGlow;
            glow.color = SelectedGlowColor;
            glow.raycastTarget = false;

            var rect = Box(panel, text, Rect.MinMaxRect(cx - width / 2f, centerY - height / 2f, cx + width / 2f, centerY + height / 2f));
            var plate = rect.gameObject.AddComponent<Image>();
            plate.sprite = sprite;
            plate.color = sprite != null ? Color.white : new Color(0.15f, 0.1f, 0.2f);
            var label = MakeText(rect, "Label", text, 18, TextAnchor.MiddleCenter, LabelColor, FontStyle.Bold);
            var row = new Row { kind = Kind.Button, root = rect, plate = plate, glow = glow, label = label, press = press };
            AddRowPointer(rect, row);
            bottomRows.Add(row);
        }

        private RectTransform NewPage(int tab)
        {
            var page = NewRect("Page_" + TabNames[tab], panel);
            Place(page, 0f, 1f, 0f, 1f);
            tabPages[tab] = page.gameObject;
            tabRows[tab] = new List<Row>();
            return page;
        }

        // 줄 하나: 판(가로로 늘려도 테두리가 늘지 않게 9칸 자르기) + 고른 줄 뒤 빛 + 왼쪽 이름. 오른쪽 조절기는 종류마다 붙인다.
        private Row AddRow(RectTransform page, int tab, string label, Kind kind)
        {
            int index = tabRows[tab].Count;
            float top = ContentBox.yMin + index * (RowHeightPx + RowGapPx);
            var rowBox = Rect.MinMaxRect(ContentBox.xMin, top, ContentBox.xMax, top + RowHeightPx);

            var glowRect = Box(page, label + "Glow", Rect.MinMaxRect(rowBox.xMin - 30f, rowBox.yMin - 26f, rowBox.xMax + 30f, rowBox.yMax + 26f));
            var glow = glowRect.gameObject.AddComponent<Image>();
            glow.sprite = softGlow;
            glow.color = SelectedGlowColor;
            glow.raycastTarget = false;

            var rect = Box(page, label, rowBox);
            var plate = rect.gameObject.AddComponent<Image>();
            plate.sprite = SlicedRow();
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 4f;
            var text = MakeText(rect, "Label", label, 19, TextAnchor.MiddleLeft, LabelColor, FontStyle.Bold);
            Place(text.rectTransform, 0.08f, 0.42f, 0f, 1f);

            var group = rect.gameObject.AddComponent<CanvasGroup>();
            var row = new Row { kind = kind, root = rect, plate = plate, glow = glow, label = text, group = group };
            glowRect.SetParent(rect, true); // 빛도 줄과 함께 흐려지고 움직이게(줄 판보다 먼저 그려지도록 맨 앞에 둔다)
            glowRect.SetAsFirstSibling();
            AddRowPointer(rect, row);
            tabRows[tab].Add(row);
            return row;
        }

        private void AddChoice(RectTransform page, int tab, string label, System.Func<string> describe, System.Action<int> change)
        {
            var row = AddRow(page, tab, label, Kind.Choice);
            row.describe = describe;
            row.change = change;
            row.press = () => change(1);
            row.value = MakeText(row.root, "Value", "", 19, TextAnchor.MiddleCenter, ValueColor, FontStyle.Bold);
            Place(row.value.rectTransform, 0.52f, 0.86f, 0f, 1f);
            var arrow = Resources.Load<Sprite>("UI/Common/Popup/popup_arrow");
            MakeArrow(row.root, arrow, 0.47f, true, () => { SelectRow(row); ChangeSelected(-1); });
            MakeArrow(row.root, arrow, 0.91f, false, () => { SelectRow(row); ChangeSelected(1); });
        }

        private void AddToggle(RectTransform page, int tab, string label, System.Func<bool> isOn, System.Action flip)
        {
            var row = AddRow(page, tab, label, Kind.Toggle);
            row.isOn = isOn;
            row.press = flip;
            row.change = _ => flip();
            var toggleRect = NewRect("Toggle", row.root);
            toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(0.78f, 0.5f);
            toggleRect.sizeDelta = new Vector2(108f, 49f);
            row.toggleImage = toggleRect.gameObject.AddComponent<Image>();
            row.toggleImage.preserveAspect = true;
            toggleRect.gameObject.AddComponent<Pointer>().down = () => { SelectRow(row); flip(); Refresh(); };
            row.value = MakeText(row.root, "State", "", 16, TextAnchor.MiddleLeft, ValueColor, FontStyle.Bold);
            Place(row.value.rectTransform, 0.55f, 0.66f, 0f, 1f);
            row.describe = () => isOn() ? "켜기" : "끄기";
        }

        private void AddSlider(RectTransform page, int tab, string label, System.Func<float> get01, System.Action<float> set01)
        {
            var row = AddRow(page, tab, label, Kind.Slider);
            row.get01 = get01;
            row.set01 = set01;
            row.change = d => set01(Mathf.Round((get01() + d * 0.1f) * 10f) / 10f);
            row.describe = () => $"{Mathf.RoundToInt(get01() * 100f)}%";

            // 홈 그림(2112x136)이 위아래로 늘지 않게, 줄 폭의 42%를 쓰는 길이에 맞춘 높이(줄의 약 22%)로 둔다.
            var track = NewRect("Track", row.root);
            Place(track, 0.42f, 0.84f, 0.39f, 0.61f);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = Load("slider_track");
            row.sliderTrack = track;

            // 채움은 홈 안쪽(양끝 수정 장식 안)만 덮고, 왼쪽부터 값만큼 채운다.
            var fill = NewRect("Fill", track);
            Place(fill, 0.065f, 0.935f, 0.28f, 0.72f);
            row.sliderFill = fill.gameObject.AddComponent<Image>();
            row.sliderFill.sprite = Load("slider_fill");
            row.sliderFill.type = Image.Type.Filled;
            row.sliderFill.fillMethod = Image.FillMethod.Horizontal;
            row.sliderFill.raycastTarget = false;

            var knob = NewRect("Knob", fill);
            knob.anchorMin = knob.anchorMax = new Vector2(0f, 0.5f);
            knob.sizeDelta = new Vector2(34f, 36f);
            var knobImage = knob.gameObject.AddComponent<Image>();
            knobImage.sprite = Load("slider_knob");
            knobImage.preserveAspect = true;
            knobImage.raycastTarget = false;
            row.sliderKnob = knob;

            var drag = track.gameObject.AddComponent<SliderDrag>();
            drag.fillArea = fill;
            drag.onValue = v => { SelectRow(row); set01(Mathf.Round(v * 20f) / 20f); Refresh(); };

            row.value = MakeText(row.root, "Value", "", 18, TextAnchor.MiddleCenter, ValueColor, FontStyle.Bold);
            Place(row.value.rectTransform, 0.85f, 0.98f, 0f, 1f);
        }

        private void MakeArrow(RectTransform row, Sprite sprite, float centerX, bool left, System.Action onDown)
        {
            var rect = NewRect(left ? "Less" : "More", row);
            rect.anchorMin = rect.anchorMax = new Vector2(centerX, 0.5f);
            rect.sizeDelta = new Vector2(30f, 34f);
            if (left) rect.localScale = new Vector3(-1f, 1f, 1f);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            rect.gameObject.AddComponent<Pointer>().down = onDown;
        }

        private void AddRowPointer(RectTransform rect, Row row)
        {
            var pointer = rect.gameObject.AddComponent<Pointer>();
            pointer.enter = () => SelectRow(row);
            if (row.kind == Kind.Button) pointer.down = () => { SelectRow(row); row.press?.Invoke(); Refresh(); };
        }

        // ---------------------------------------------------------------- 조작

        private void SelectTab(int tab, bool force = false)
        {
            if (!force && tab == currentTab) return;
            if (!force) UiSfx.Move();
            currentTab = tab;
            for (int i = 0; i < tabPages.Length; i++) tabPages[i].SetActive(i == tab);
            var box = Rect.MinMaxRect(TabX[tab].x + 4f, TabY.x + 4f, TabX[tab].y - 4f, TabY.y - 4f);
            SetBox(tabHighlight, box);

            // 조작 탭은 보기만 하는 줄이라 고를 수 있는 건 아래 버튼뿐이다.
            active.Clear();
            if (tab != 3) active.AddRange(tabRows[tab]);
            active.AddRange(bottomRows);
            selected = 0;
            pageAnimStart = Time.unscaledTime;
            Refresh();
            ApplyPageAnim();
        }

        private void SelectRow(Row row)
        {
            int index = active.IndexOf(row);
            if (index < 0 || index == selected) return;
            UiSfx.Move();
            selected = index;
            Refresh();
        }

        private void MoveSelection(int delta)
        {
            if (active.Count == 0) return;
            UiSfx.Move();
            selected = (selected + delta + active.Count) % active.Count;
            Refresh();
        }

        private void ChangeSelected(int delta)
        {
            if (selected < 0 || selected >= active.Count) return;
            var row = active[selected];
            if (row.change == null) return;
            row.change(delta);
            Refresh();
        }

        private void PressSelected()
        {
            if (selected < 0 || selected >= active.Count) return;
            active[selected].press?.Invoke();
            Refresh();
        }

        private void Refresh()
        {
            for (int t = 0; t < tabRows.Length; t++)
            {
                foreach (var row in tabRows[t]) RefreshRow(row, active.IndexOf(row) == selected);
            }
            for (int i = 0; i < bottomRows.Count; i++) RefreshRow(bottomRows[i], active.IndexOf(bottomRows[i]) == selected);
        }

        private void RefreshRow(Row row, bool isSelected)
        {
            if (row.glow != null) row.glow.enabled = isSelected;
            if (row.plate != null && row.kind != Kind.Button) row.plate.color = isSelected ? Color.white : RowTint;
            else if (row.plate != null && row.glow != null) row.plate.color = isSelected ? Color.white : new Color(0.62f, 0.58f, 0.68f);
            if (row.label != null && row.glow != null) row.label.color = isSelected ? SelectedLabelColor : LabelColor;
            if (row.value != null && row.describe != null) row.value.text = row.describe();
            if (row.toggleImage != null) row.toggleImage.sprite = row.isOn() ? toggleOn : toggleOff;
            if (row.sliderFill != null)
            {
                float v = row.get01();
                row.sliderFill.fillAmount = v;
                row.sliderKnob.anchorMin = row.sliderKnob.anchorMax = new Vector2(v, 0.5f);
            }
        }

        private void Update()
        {
            if (!IsOpen) return;
            ApplyPageAnim();
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            if (selected >= 0 && selected < active.Count && active[selected].glow != null)
                active[selected].glow.color = new Color(SelectedGlowColor.r, SelectedGlowColor.g, SelectedGlowColor.b, SelectedGlowColor.a * pulse);

            if (Time.frameCount == openedFrame || ChoicePopup.ConsumesInput) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame) { Close(); return; }
            if (keyboard.qKey.wasPressedThisFrame) SelectTab((currentTab + 3) % 4);
            else if (keyboard.eKey.wasPressedThisFrame) SelectTab((currentTab + 1) % 4);
            else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) MoveSelection(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) MoveSelection(1);
            else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) ChangeSelected(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) ChangeSelected(1);
            else if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                PressSelected();
        }

        // 탭을 바꾸면 줄이 위에서부터 차례로, 오른쪽에서 살짝 밀려 들어오며 나타난다.
        private void ApplyPageAnim()
        {
            var rows = tabRows[currentTab];
            float elapsed = Time.unscaledTime - pageAnimStart;
            for (int i = 0; i < rows.Count; i++)
            {
                float t = Mathf.Clamp01((elapsed - i * 0.045f) / 0.22f);
                float k = 1f - (1f - t) * (1f - t) * (1f - t);
                if (rows[i].group != null) rows[i].group.alpha = k;
                rows[i].root.anchoredPosition = new Vector2(24f * (1f - k), 0f);
            }
        }

        // ---------------------------------------------------------------- 도우미

        private static Sprite Load(string name) => Resources.Load<Sprite>(ArtFolder + name);

        private Sprite slicedRow;

        // 항목 줄 그림(2480x128)을 가로로 늘려도 왼쪽 마름모 장식과 테두리가 늘어나지 않게 9칸 자르기 테두리를 준다.
        private Sprite SlicedRow()
        {
            if (slicedRow != null) return slicedRow;
            var source = Load("settings_row");
            if (source == null) return null;
            slicedRow = Sprite.Create(source.texture, source.rect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(150f, 24f, 40f, 24f));
            return slicedRow;
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

        private static int Step(int index, int delta, int count)
        {
            if (index < 0) index = 0;
            return Mathf.Clamp(index + delta, 0, count - 1);
        }

        private static string FrameLabel(int limit)
        {
            if (limit == 0) return "수직 동기화";
            if (limit < 0) return "무제한";
            return $"{limit} FPS";
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        // 창 그림 픽셀 상자를 창 안 비율 앵커로.
        private static RectTransform Box(Transform parent, string name, Rect box)
        {
            var rect = NewRect(name, parent);
            SetBox(rect, box);
            return rect;
        }

        private static void SetBox(RectTransform rect, Rect box)
        {
            rect.anchorMin = new Vector2(box.xMin / ArtWidth, 1f - box.yMax / ArtHeight);
            rect.anchorMax = new Vector2(box.xMax / ArtWidth, 1f - box.yMin / ArtHeight);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private Text MakeText(Transform parent, string name, string content, int size, TextAnchor anchor, Color color, FontStyle style)
        {
            var rect = NewRect(name, parent);
            Place(rect, 0f, 1f, 0f, 1f);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = content;
            return text;
        }

        private static void Place(RectTransform rect, float xMin, float xMax, float yMin, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // 클릭 대신 누르는 순간(OnPointerDown)에 반응한다(InventoryUI.SlotInteraction과 같은 이유).
        private class Pointer : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
        {
            public System.Action enter;
            public System.Action down;
            public void OnPointerEnter(PointerEventData eventData) => enter?.Invoke();
            public void OnPointerDown(PointerEventData eventData) => down?.Invoke();
        }

        // 슬라이더 홈을 누르거나 끌면 누른 자리의 비율(0~1)을 알려 준다.
        private class SliderDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
        {
            public RectTransform fillArea;
            public System.Action<float> onValue;
            public void OnPointerDown(PointerEventData eventData) => Report(eventData);
            public void OnDrag(PointerEventData eventData) => Report(eventData);

            private void Report(PointerEventData eventData)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(fillArea, eventData.position, eventData.pressEventCamera, out var local)) return;
                var r = fillArea.rect;
                onValue?.Invoke(Mathf.Clamp01((local.x - r.xMin) / r.width));
            }
        }
    }
}

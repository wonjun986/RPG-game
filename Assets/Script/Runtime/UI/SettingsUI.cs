using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Save;

namespace Aethoria.UI
{
    // 타이틀 화면 SETTINGS 창. 줄마다 [항목 이름  ◀ 값 ▶]이 있고, 바꾸는 즉시 적용·저장된다(GameSettings).
    // ↑↓(W/S)로 줄을 고르고 ←→(A/D)로 값을 바꾼다. 맨 아래 "기본값으로"/"닫기"는 Z/Enter로 누른다. Esc/X로 닫는다.
    // 마우스로는 ◀ ▶를 눌러 값을 바꾸고, 줄 위에 올리면 그 줄이 골라진다.
    public class SettingsUI : MonoBehaviour
    {
        private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color SelectedRowColor = new Color(0.6f, 0.35f, 0.95f, 0.45f);
        private static readonly Color LabelColor = new Color(0.85f, 0.8f, 0.95f);
        private static readonly Color ValueColor = Color.white;

        private class Row
        {
            public Image background;
            public Text value;
            public System.Action<int> change;   // ←(-1)/→(+1). 값을 바꾸는 줄만 있다
            public System.Action press;         // Z/Enter. 버튼 줄만 있다
            public System.Func<string> describe;
        }

        private GameObject panel;
        private readonly List<Row> rows = new List<Row>();
        private int selected;
        private int openedFrame = -1;
        private System.Action onClosed;

        public bool IsOpen => panel != null && panel.activeSelf;
        // 닫는 그 프레임의 Enter/Esc가 뒤쪽 타이틀 메뉴까지 누르지 않게, 타이틀이 이 프레임은 건너뛴다.
        public bool ClosedThisFrame => closedFrame == Time.frameCount;
        private int closedFrame = -1;

        public static SettingsUI Create(Transform canvas)
        {
            var go = new GameObject("SettingsUI", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var ui = go.AddComponent<SettingsUI>();
            ui.Build(canvas);
            return ui;
        }

        public void Open(System.Action closed)
        {
            onClosed = closed;
            selected = 0;
            openedFrame = Time.frameCount;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            // 창이 떠 있는 동안 Enter가 뒤쪽 메뉴 버튼까지 누르지 않도록 선택을 비운다.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            panel.SetActive(false);
            closedFrame = Time.frameCount;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            onClosed?.Invoke();
        }

        private void Build(Transform canvas)
        {
            panel = new GameObject("SettingsDialog", typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)panel.transform;
            dimRect.SetParent(canvas, false);
            Stretch(dimRect);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var boxGO = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var box = (RectTransform)boxGO.transform;
            box.SetParent(dimRect, false);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(640f, 580f);
            boxGO.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.09f, 0.97f);

            var title = MakeText(box, "Title", "SETTINGS", 28, TextAnchor.MiddleCenter, new Color(0.85f, 0.7f, 1f));
            title.fontStyle = FontStyle.Bold;
            Place(title.rectTransform, 0f, 1f, 0.89f, 0.98f);

            var resolutions = GameSettings.AvailableResolutions;
            AddOption(box, "화면 모드", () => GameSettings.Fullscreen ? "전체 화면" : "창 모드",
                _ => GameSettings.SetFullscreen(!GameSettings.Fullscreen));
            AddOption(box, "해상도", () => $"{GameSettings.Resolution.x} x {GameSettings.Resolution.y}",
                d => GameSettings.SetResolution(resolutions[Step(System.Array.IndexOf(resolutions, GameSettings.Resolution), d, resolutions.Length)]));
            AddOption(box, "프레임", () => FrameLabel(GameSettings.FrameLimit),
                d => GameSettings.SetFrameLimit(GameSettings.FrameLimits[Step(System.Array.IndexOf(GameSettings.FrameLimits, GameSettings.FrameLimit), d, GameSettings.FrameLimits.Length)]));
            AddOption(box, "전체 볼륨", () => $"{Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%",
                d => GameSettings.SetMasterVolume(Mathf.Round((GameSettings.MasterVolume + d * 0.1f) * 10f) / 10f));
            AddOption(box, "화면 흔들림", () => GameSettings.ScreenShake ? "켜기" : "끄기",
                _ => GameSettings.SetScreenShake(!GameSettings.ScreenShake));
            AddOption(box, "데미지 숫자", () => GameSettings.DamageNumbers ? "켜기" : "끄기",
                _ => GameSettings.SetDamageNumbers(!GameSettings.DamageNumbers));
            AddButton(box, "기본값으로 되돌리기", () => { GameSettings.ResetToDefaults(); Refresh(); });
            AddButton(box, "닫기", Close);

            var hint = MakeText(box, "Hint", "↑↓ 항목   ←→ 변경   [Z] 선택   [Esc] 닫기", 16, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
            Place(hint.rectTransform, 0f, 1f, 0.015f, 0.07f);

            panel.SetActive(false);
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

        // 줄은 위에서부터 차례로 쌓는다(줄 높이 0.085, 간격 0.012).
        private RectTransform AddRow(RectTransform box)
        {
            int index = rows.Count;
            float top = 0.86f - index * 0.097f;
            var go = new GameObject("Row" + index, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(box, false);
            Place(rect, 0.06f, 0.94f, top - 0.085f, top);
            go.GetComponent<Image>().color = RowColor;
            var hover = go.AddComponent<RowHover>();
            hover.owner = this;
            hover.index = index;
            return rect;
        }

        private void AddOption(RectTransform box, string label, System.Func<string> describe, System.Action<int> change)
        {
            var rect = AddRow(box);
            var row = new Row { background = rect.GetComponent<Image>(), change = change, describe = describe };
            var labelText = MakeText(rect, "Label", label, 22, TextAnchor.MiddleLeft, LabelColor);
            Place(labelText.rectTransform, 0.04f, 0.45f, 0f, 1f);
            row.value = MakeText(rect, "Value", "", 22, TextAnchor.MiddleCenter, ValueColor);
            row.value.fontStyle = FontStyle.Bold;
            Place(row.value.rectTransform, 0.52f, 0.9f, 0f, 1f);
            int index = rows.Count;
            MakeArrow(rect, "◀", 0.45f, 0.53f, () => { selected = index; Change(-1); });
            MakeArrow(rect, "▶", 0.89f, 0.97f, () => { selected = index; Change(1); });
            rows.Add(row);
        }

        private void AddButton(RectTransform box, string label, System.Action press)
        {
            var rect = AddRow(box);
            var row = new Row { background = rect.GetComponent<Image>(), press = press };
            var text = MakeText(rect, "Label", label, 22, TextAnchor.MiddleCenter, ValueColor);
            text.fontStyle = FontStyle.Bold;
            Place(text.rectTransform, 0f, 1f, 0f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => press());
            rows.Add(row);
        }

        private static void MakeArrow(RectTransform row, string glyph, float xMin, float xMax, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Arrow" + glyph, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(row, false);
            Place(rect, xMin, xMax, 0.1f, 0.9f);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(onClick);
            var text = MakeText(rect, "Glyph", glyph, 22, TextAnchor.MiddleCenter, new Color(0.85f, 0.7f, 1f));
            Place(text.rectTransform, 0f, 1f, 0f, 1f);
        }

        private void Update()
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame) { Close(); return; }
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) Select(selected - 1);
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) Select(selected + 1);
            else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) Change(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) Change(1);
            else if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                var row = rows[selected];
                if (row.press != null) row.press();
                else Change(1);
            }
        }

        private void Select(int index)
        {
            selected = (index + rows.Count) % rows.Count;
            Refresh();
        }

        private void Change(int delta)
        {
            var row = rows[selected];
            if (row.change == null) return;
            row.change(delta);
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                row.background.color = i == selected ? SelectedRowColor : RowColor;
                if (row.value != null) row.value.text = row.describe();
            }
        }

        private class RowHover : MonoBehaviour, IPointerEnterHandler
        {
            public SettingsUI owner;
            public int index;
            public void OnPointerEnter(PointerEventData eventData) => owner.Select(index);
        }

        private static Text MakeText(Transform parent, string name, string content, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
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

        private static void Stretch(RectTransform rect) => Place(rect, 0f, 1f, 0f, 1f);
    }
}

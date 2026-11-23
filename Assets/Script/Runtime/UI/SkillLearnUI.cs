using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 이시스에게 기술을 배우는 화면(UI/SkillLearn — Sprite/UI/HUD/스킬 배우기_ 어둠의 마법진, 1535x1024).
    // 가운데 7칸(위 A·S·D·Q, 아래 W·E·R)에서 스킬을 고르면 오른쪽에 이름·설명·쿨타임·마나가 뜨고, 레벨이 차 있으면
    // [스킬 배우기]로 익힌다(SkillBook). 그림 속 칸에 그려진 아이콘은 순서가 HUD와 달라서, HUD와 같은 아이콘(Art/UI/SkillSlotArt)을
    // 칸마다 덮어 그린다. 그림은 3:2라 화면(16:9)에 늘이지 않고, 어둡게 깐 배경 위 가운데에 비율 그대로 띄운다.
    // 조작: ←→↑↓ 고르기, Z/Enter(또는 [스킬 배우기]) 배우기, X/Esc(또는 ✕) 닫기. 마우스로 칸을 눌러 골라도 된다.
    public class SkillLearnUI : MonoBehaviour
    {
        private const float ImageWidth = 1535f;
        private const float ImageHeight = 1024f;
        private const float WindowHeightFraction = 0.94f;

        // 칸 자리(그림 픽셀, 왼쪽 위 x0,y0 ~ 오른쪽 아래 x1,y1). SkillBook.Skills 순서(A S D Q W E R)와 같다.
        private static readonly Rect[] SlotRects =
        {
            Px(395, 338, 530, 485), Px(549, 338, 684, 485), Px(700, 338, 835, 485), Px(851, 338, 984, 485),
            Px(447, 543, 590, 688), Px(615, 543, 763, 688), Px(792, 543, 946, 688),
        };
        // ↑↓로 옮겨 갈 칸: 아랫줄 3칸은 윗줄 4칸 사이에 엇갈려 있어서, 가로로 가장 가까운 칸으로 간다.
        private static readonly int[] DownNeighbor = { 4, 5, 5, 6, 4, 5, 6 };
        private static readonly int[] UpNeighbor = { 0, 1, 2, 3, 0, 1, 3 };
        private const float IconInset = 9f;

        private static readonly string[] Descriptions =
        {
            "낫을 크게 휘둘러 몸 주위의 적을 모두 벤다.\n충전이 남아 있으면 최대 3번까지 연달아 쓸 수 있다.",
            "전방으로 거대한 어둠의 주먹을 날린다.",
            "낫을 던졌다가 되돌려 받으며\n오가는 길의 적을 모두 벤다.",
            "앞 허공에 차원 균열을 열고,\n그 틈에서 보랏빛 참격을 연달아 쏟아붓는다.",
            "전방에 포탈을 열어\n악마의 손으로 적을 연타한다.",
            "적의 발밑에 사슬 회오리를 일으켜\n묶어 두고 연타한다.",
            "악마의 문을 열어 악룡을 불러내\n앞을 통째로 불태운다. (궁극기)",
        };

        private static readonly Color TextColor = new Color(0.93f, 0.88f, 1f);
        private static readonly Color DimColor = new Color(0.62f, 0.56f, 0.7f);
        private static readonly Color ReadyColor = new Color(0.95f, 0.85f, 0.35f);
        private static readonly Color BadColor = new Color(1f, 0.45f, 0.5f);
        private static readonly Color PanelInk = new Color(0.075f, 0.06f, 0.09f, 1f);
        private static readonly Color SelectColor = new Color(1f, 0.92f, 1f, 1f);
        private static readonly Color LearnableColor = new Color(0.95f, 0.8f, 0.3f, 1f);

        private Character player;
        private CharacterMovement2D playerMovement;
        private SkillBook skillBook;
        private Action onLearned;

        private GameObject panel;
        private RectTransform window;
        private RectTransform windowHolder;
        private float pxToCanvas;
        private readonly RawImage[] slotIcons = new RawImage[7];
        private readonly Text[] slotLabels = new Text[7];
        private readonly Image[][] slotBorders = new Image[7][];
        private RawImage detailIcon;
        private Text nameText;
        private Text levelText;
        private Text descText;
        private Text cooldownText;
        private Text manaText;
        private Text statusText;

        private int selected;
        private int openedFrame = -1;
        private float flashUntil;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize(Character character, CharacterMovement2D movement, Action learned)
        {
            player = character;
            playerMovement = movement;
            skillBook = character.GetComponent<SkillBook>();
            onLearned = learned;
            BuildPanel();
        }

        private static Rect Px(float x0, float y0, float x1, float y1) => Rect.MinMaxRect(x0, y0, x1, y1);

        private void BuildPanel()
        {
            TitleScreenUI.EnsureEventSystem();

            // 뒤 화면을 어둡게 덮는 배경(누르면 아무 일도 없다 — 뒤쪽 UI가 눌리지 않게 막는다).
            panel = new GameObject("SkillLearnPanel", typeof(RectTransform), typeof(Image));
            var panelRect = (RectTransform)panel.transform;
            panelRect.SetParent(transform, false);
            Stretch(panelRect);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            var holder = new GameObject("WindowArea", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(panelRect, false);
            holder.anchorMin = new Vector2(0f, (1f - WindowHeightFraction) / 2f);
            holder.anchorMax = new Vector2(1f, 1f - (1f - WindowHeightFraction) / 2f);
            holder.offsetMin = Vector2.zero;
            holder.offsetMax = Vector2.zero;

            var windowGO = new GameObject("Window", typeof(RectTransform), typeof(RawImage));
            window = (RectTransform)windowGO.transform;
            window.SetParent(holder, false);
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
            window.pivot = new Vector2(0.5f, 0.5f);
            windowGO.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/Menus/SkillLearn");
            windowHolder = holder;

            // 캔버스 기준 해상도(세로 720)에서 그림 1px이 몇 단위인지. 글자 크기를 그림 픽셀로 정하는 데 쓴다.
            pxToCanvas = 720f * WindowHeightFraction / ImageHeight;

            for (int i = 0; i < SlotRects.Length; i++) BuildSlot(i);

            var detail = Px(1048, 273, 1186, 412);
            detailIcon = CreateRect(window, "DetailIcon", Inset(detail), typeof(RawImage)).GetComponent<RawImage>();
            detailIcon.raycastTarget = false;

            nameText = CreateText(window, "Name", Px(1205, 288, 1465, 332), 34f, TextColor, TextAnchor.MiddleLeft);
            nameText.fontStyle = FontStyle.Bold;
            levelText = CreateText(window, "Level", Px(1325, 366, 1470, 402), 24f, DimColor, TextAnchor.MiddleLeft);
            descText = CreateText(window, "Desc", Px(1052, 425, 1470, 510), 26f, TextColor, TextAnchor.UpperLeft);
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 그림 속 수치 칸의 "—"를 가리고 그 위에 값을 쓴다.
            CreateRect(window, "CooldownCover", Px(1405, 588, 1462, 612), typeof(Image)).GetComponent<Image>().color = PanelInk;
            CreateRect(window, "ManaCover", Px(1405, 630, 1462, 654), typeof(Image)).GetComponent<Image>().color = PanelInk;
            cooldownText = CreateText(window, "Cooldown", Px(1290, 582, 1458, 618), 24f, TextColor, TextAnchor.MiddleRight);
            manaText = CreateText(window, "Mana", Px(1290, 624, 1458, 660), 24f, TextColor, TextAnchor.MiddleRight);

            statusText = CreateText(window, "Status", Px(1030, 700, 1490, 752), 28f, TextColor, TextAnchor.MiddleCenter);
            statusText.fontStyle = FontStyle.Bold;

            CreateButton(window, "LearnButton", Px(1095, 765, 1425, 840), TryLearn);
            CreateButton(window, "CloseButton", Px(1430, 22, 1510, 98), Hide);

            panel.AddComponent<PanelFadeIn>(); // 켜질 때 공통 연출 + 여는 소리
            panel.SetActive(false);
        }

        private void BuildSlot(int index)
        {
            var area = SlotRects[index];
            string key = SkillBook.Skills[index].Key;

            var icon = CreateRect(window, "Slot_" + key, Inset(area), typeof(RawImage), typeof(Button)).GetComponent<RawImage>();
            SetIcon(icon, key);
            int captured = index;
            icon.GetComponent<Button>().onClick.AddListener(() => Select(captured));
            slotIcons[index] = icon;

            // 칸 아래쪽 띠에 상태(Lv 조건 / 습득)를 쓴다.
            var label = CreateText(window, "SlotLabel_" + key, Rect.MinMaxRect(area.xMin, area.yMax - 34f, area.xMax, area.yMax - 6f), 22f, TextColor, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            slotLabels[index] = label;

            // 고른 칸/배울 수 있는 칸을 감싸는 테두리(위·아래·왼쪽·오른쪽 네 줄).
            const float t = 5f;
            slotBorders[index] = new[]
            {
                CreateRect(window, "BorderTop", Rect.MinMaxRect(area.xMin - t, area.yMin - t, area.xMax + t, area.yMin), typeof(Image)).GetComponent<Image>(),
                CreateRect(window, "BorderBottom", Rect.MinMaxRect(area.xMin - t, area.yMax, area.xMax + t, area.yMax + t), typeof(Image)).GetComponent<Image>(),
                CreateRect(window, "BorderLeft", Rect.MinMaxRect(area.xMin - t, area.yMin, area.xMin, area.yMax), typeof(Image)).GetComponent<Image>(),
                CreateRect(window, "BorderRight", Rect.MinMaxRect(area.xMax, area.yMin, area.xMax + t, area.yMax), typeof(Image)).GetComponent<Image>(),
            };
            foreach (var border in slotBorders[index]) border.raycastTarget = false;
        }

        // HUD 아이콘 그림(가로가 조금 긴 직사각형)을 가운데 기준 정사각형으로 잘라 칸에 맞춘다.
        private static void SetIcon(RawImage target, string key)
        {
            var sprite = Resources.Load<Sprite>("Art/UI/SkillSlotArt/" + key);
            if (sprite == null) return;
            var tex = sprite.texture;
            Rect r = sprite.textureRect;
            float side = Mathf.Min(r.width, r.height);
            target.texture = tex;
            target.uvRect = new Rect((r.x + (r.width - side) / 2f) / tex.width, (r.y + (r.height - side) / 2f) / tex.height, side / tex.width, side / tex.height);
        }

        public void Show()
        {
            openedFrame = Time.frameCount;
            // 처음 열 때는 지금 배울 수 있는 첫 기술(없으면 맨 앞)을 고른다.
            selected = 0;
            for (int i = 0; i < SkillBook.Skills.Length; i++)
            {
                if (State(i) == SlotState.Learnable) { selected = i; break; }
            }
            flashUntil = 0f;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            FitWindow();
            Refresh();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        // 그림 비율(3:2)을 지키며 화면 안에 들어가는 가장 큰 크기로 맞춘다(화면 비율이 바뀌어도 열 때마다 다시 잰다).
        private void FitWindow()
        {
            Canvas.ForceUpdateCanvases();
            Rect area = windowHolder.rect;
            float height = area.height;
            float width = height * ImageWidth / ImageHeight;
            if (width > area.width)
            {
                width = area.width;
                height = width * ImageHeight / ImageWidth;
            }
            window.sizeDelta = new Vector2(width, height);
        }

        public void Hide()
        {
            if (panel.activeSelf) UiSfx.Close();
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private enum SlotState { Learned, Learnable, Locked }

        private SlotState State(int index)
        {
            var skill = SkillBook.Skills[index];
            if (skillBook != null && skillBook.IsLearned(skill.Key)) return SlotState.Learned;
            return player.Level >= skill.RequiredLevel ? SlotState.Learnable : SlotState.Locked;
        }

        private void Select(int index)
        {
            if (Mathf.Clamp(index, 0, SkillBook.Skills.Length - 1) != selected) UiSfx.Move();
            selected = Mathf.Clamp(index, 0, SkillBook.Skills.Length - 1);
            flashUntil = 0f;
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < SkillBook.Skills.Length; i++)
            {
                var state = State(i);
                slotIcons[i].color = state == SlotState.Locked ? new Color(0.32f, 0.3f, 0.36f) : Color.white;
                slotLabels[i].text = state switch
                {
                    SlotState.Learned => "습득",
                    SlotState.Learnable => "배울 수 있음",
                    _ => $"Lv {SkillBook.Skills[i].RequiredLevel}",
                };
                slotLabels[i].color = state == SlotState.Learnable ? ReadyColor : state == SlotState.Learned ? TextColor : DimColor;
            }
            UpdateBorders();

            var skill = SkillBook.Skills[selected];
            SetIcon(detailIcon, skill.Key);
            var selectedState = State(selected);
            detailIcon.color = selectedState == SlotState.Locked ? new Color(0.45f, 0.42f, 0.5f) : Color.white;
            nameText.text = $"[{skill.Key}]  {skill.Name}";
            levelText.text = $"요구 Lv {skill.RequiredLevel}";
            levelText.color = player.Level >= skill.RequiredLevel ? DimColor : BadColor;
            descText.text = Descriptions[selected];
            cooldownText.text = CooldownLabel(skill.Key);
            manaText.text = ManaLabel(skill.Key);

            if (Time.unscaledTime < flashUntil) return; // 방금 배운 직후의 축하 문구는 잠깐 그대로 둔다
            switch (selectedState)
            {
                case SlotState.Learned:
                    statusText.text = "이미 익힌 기술이다";
                    statusText.color = DimColor;
                    break;
                case SlotState.Learnable:
                    statusText.text = "[Z] 이 기술을 배운다";
                    statusText.color = ReadyColor;
                    break;
                default:
                    statusText.text = $"Lv {skill.RequiredLevel}이 되면 배울 수 있다 (지금 Lv {player.Level})";
                    statusText.color = BadColor;
                    break;
            }
        }

        // 고른 칸은 밝은 테두리, 배울 수 있는 칸은 금빛 테두리가 숨 쉬듯 깜빡인다.
        private void UpdateBorders()
        {
            float pulse = 0.45f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < slotBorders.Length; i++)
            {
                Color color;
                if (i == selected) color = SelectColor;
                else if (State(i) == SlotState.Learnable) color = new Color(LearnableColor.r, LearnableColor.g, LearnableColor.b, pulse);
                else color = Color.clear;
                foreach (var border in slotBorders[i]) border.color = color;
            }
        }

        private string CooldownLabel(string key)
        {
            switch (key)
            {
                case "A":
                    var a = player.GetComponent<CharacterSkillA>();
                    return a != null ? $"충전 {a.MaxCharges}회 · {a.RechargeCooldown:0.#}초" : "-";
                case "S": return Seconds(player.GetComponent<CharacterSkillS>()?.Cooldown);
                case "D": return Seconds(player.GetComponent<CharacterSkillD>()?.Cooldown);
                case "Q": return Seconds(player.GetComponent<CharacterSkillQ>()?.Cooldown);
                case "W": return Seconds(player.GetComponent<CharacterSkillW>()?.Cooldown);
                case "E": return Seconds(player.GetComponent<CharacterSkillE>()?.Cooldown);
                case "R": return Seconds(player.GetComponent<CharacterSkillR>()?.Cooldown);
            }
            return "-";
        }

        private string ManaLabel(string key)
        {
            float? mana = key switch
            {
                "A" => player.GetComponent<CharacterSkillA>()?.ManaCost,
                "S" => player.GetComponent<CharacterSkillS>()?.ManaCost,
                "D" => player.GetComponent<CharacterSkillD>()?.ManaCost,
                "Q" => player.GetComponent<CharacterSkillQ>()?.ManaCost,
                "W" => player.GetComponent<CharacterSkillW>()?.ManaCost,
                "E" => player.GetComponent<CharacterSkillE>()?.ManaCost,
                "R" => player.GetComponent<CharacterSkillR>()?.ManaCost,
                _ => null,
            };
            if (mana == null) return "-";
            return mana.Value <= 0f ? "없음" : $"MP {mana.Value:0}";
        }

        private static string Seconds(float? value) => value == null ? "-" : $"{value.Value:0.#}초";

        private void TryLearn()
        {
            if (skillBook == null || State(selected) != SlotState.Learnable) return;

            var skill = SkillBook.Skills[selected];
            skillBook.Learn(skill.Key);
            statusText.text = $"{skill.Name}을(를) 익혔다!";
            statusText.color = ReadyColor;
            flashUntil = Time.unscaledTime + 1.5f;
            Refresh();
            onLearned?.Invoke();
        }

        private void Update()
        {
            if (!IsOpen) return;

            UpdateBorders();

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            // 연 그 프레임의 입력(이시스 대화의 숫자키 등)이 같은 프레임에 다시 소비되지 않게 한다.
            if (Time.frameCount == openedFrame) return;

            if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                Hide();
                return;
            }
            if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                TryLearn();
                return;
            }

            int count = SkillBook.Skills.Length;
            if (keyboard.rightArrowKey.wasPressedThisFrame) Select((selected + 1) % count);
            else if (keyboard.leftArrowKey.wasPressedThisFrame) Select((selected - 1 + count) % count);
            else if (keyboard.downArrowKey.wasPressedThisFrame) Select(DownNeighbor[selected]);
            else if (keyboard.upArrowKey.wasPressedThisFrame) Select(UpNeighbor[selected]);
            else if (Time.unscaledTime >= flashUntil && flashUntil > 0f) { flashUntil = 0f; Refresh(); }
        }

        // ---- 그림 픽셀 좌표로 요소를 만드는 도우미들 ----

        private static Rect Inset(Rect r) => Rect.MinMaxRect(r.xMin + IconInset, r.yMin + IconInset, r.xMax - IconInset, r.yMax - IconInset);

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform CreateRect(Transform parent, string name, Rect px, params Type[] components)
        {
            var types = new Type[components.Length + 1];
            types[0] = typeof(RectTransform);
            components.CopyTo(types, 1);
            var go = new GameObject(name, types);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(px.xMin / ImageWidth, 1f - px.yMax / ImageHeight);
            rect.anchorMax = new Vector2(px.xMax / ImageWidth, 1f - px.yMin / ImageHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Text CreateText(Transform parent, string name, Rect px, float fontPx, Color color, TextAnchor alignment)
        {
            var text = CreateRect(parent, name, px, typeof(Text)).GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(fontPx * pxToCanvas));
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        // 그림에 이미 그려진 버튼 위에 투명한 클릭 영역만 덮는다.
        private static void CreateButton(Transform parent, string name, Rect px, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreateRect(parent, name, px, typeof(Image), typeof(Button));
            rect.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            rect.GetComponent<Button>().onClick.AddListener(onClick);
        }
    }
}

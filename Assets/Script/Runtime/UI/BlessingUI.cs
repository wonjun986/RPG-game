using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Data;

namespace Aethoria.UI
{
    // 이리아의 축복 화면(UI/Iria_Blessing). 장비 강화와 달리 캐릭터 전체에 붙는 단계 하나뿐이라
    // 고를 장비도, 성공 확률도, 재료도 없다 — 골드만 내면 공격력/방어력/치명타 확률/치명타 피해량/
    // 스킬 피해량이 한 번에 조금씩 같이 오른다. 조작: Z/Enter(또는 [기도하기])로 축복, X/Esc로 닫기.
    public class BlessingUI : MonoBehaviour
    {
        private const float ImageWidth = 1672f;
        private const float ImageHeight = 941f;
        private const float PxToCanvas = 720f / ImageHeight;

        // 5개 스탯 줄(공격력/방어력/치명타 확률/치명타 피해량/스킬 피해량)의 세로 중심. 그림 속 순서 그대로다.
        private static readonly float[] StatRowY = { 270f, 340f, 410f, 480f, 550f };

        private static readonly Color TextColor = new Color(0.3f, 0.22f, 0.15f);
        private static readonly Color DimColor = new Color(0.6f, 0.55f, 0.5f);
        private static readonly Color UpColor = new Color(0.2f, 0.55f, 0.25f);
        private static readonly Color BadColor = new Color(0.75f, 0.15f, 0.15f);

        private Character player;
        private CharacterMovement2D playerMovement;

        private GameObject panel;
        private readonly Text[] statValues = new Text[5];
        private Text costText;
        private int openedFrame = -1;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize(Character character, CharacterMovement2D movement)
        {
            player = character;
            playerMovement = movement;
            BuildPanel();
        }

        private void BuildPanel()
        {
            TitleScreenUI.EnsureEventSystem();

            panel = new GameObject("BlessingPanel", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/Iria_Blessing");

            for (int i = 0; i < StatRowY.Length; i++)
            {
                statValues[i] = CreateText(rect, "Stat" + i, 1245f, StatRowY[i] - 22f, 1440f, StatRowY[i] + 22f, 24f, TextColor, TextAnchor.MiddleCenter);
                statValues[i].fontStyle = FontStyle.Bold;
            }

            costText = CreateText(rect, "Cost", 1000f, 668f, 1370f, 712f, 26f, TextColor, TextAnchor.MiddleCenter);
            costText.fontStyle = FontStyle.Bold;

            CreateButton(rect, "PrayButton", 895f, 790f, 1450f, 865f, TryBless);

            panel.SetActive(false);
        }

        public void Show()
        {
            openedFrame = Time.frameCount;
            Refresh();
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void Refresh()
        {
            int level = player.BlessingLevel;
            bool maxed = level >= BlessingRules.MaxLevel;

            SetStatRow(0, BlessingRules.AttackBonus(level), BlessingRules.AttackBonus(level + 1), "", maxed);
            SetStatRow(1, BlessingRules.DefenseBonus(level), BlessingRules.DefenseBonus(level + 1), "", maxed);
            SetStatRow(2, BlessingRules.CritChanceBonus(level), BlessingRules.CritChanceBonus(level + 1), "%", maxed);
            SetStatRow(3, BlessingRules.CritDamageBonus(level), BlessingRules.CritDamageBonus(level + 1), "%", maxed);
            SetStatRow(4, BlessingRules.SkillDamageBonus(level), BlessingRules.SkillDamageBonus(level + 1), "%", maxed);

            if (maxed)
            {
                costText.text = "축복이 가득 찼어요";
                costText.color = DimColor;
                return;
            }

            int cost = BlessingRules.GoldCost(level);
            costText.text = cost.ToString("N0");
            costText.color = player.Gold >= cost ? TextColor : BadColor;
        }

        private void SetStatRow(int row, float now, float next, string suffix, bool maxed)
        {
            string nowText = FormatValue(now, suffix);
            if (maxed)
            {
                statValues[row].text = nowText;
                statValues[row].color = TextColor;
                return;
            }
            statValues[row].text = $"{nowText} <color=#{ColorUtility.ToHtmlStringRGB(UpColor)}>→ {FormatValue(next, suffix)}</color>";
            statValues[row].color = TextColor;
        }

        private static string FormatValue(float value, string suffix)
        {
            return Mathf.Approximately(value, 0f) ? "-" : "+" + value.ToString("0.#") + suffix;
        }

        private void TryBless()
        {
            int level = player.BlessingLevel;
            if (level >= BlessingRules.MaxLevel) return;

            int cost = BlessingRules.GoldCost(level);
            if (!player.TrySpendGold(cost)) return;

            player.RaiseBlessingLevel();
            Refresh();
        }

        private void Update()
        {
            if (!IsOpen) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 연 그 프레임의 입력(이리아 대화의 숫자키 등)이 같은 프레임에 다시 소비되지 않게 한다.
            if (Time.frameCount == openedFrame) return;

            if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                Hide();
                return;
            }

            if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                TryBless();
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
            text.supportRichText = true;
            return text;
        }

        // 그림에 이미 그려진 버튼("기도하기") 위에 투명한 클릭 영역만 덮는다.
        private static void CreateButton(Transform parent, string name, float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreateRect(parent, name, x0, y0, x1, y1, typeof(RectTransform), typeof(Image), typeof(Button));
            var image = rect.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            rect.GetComponent<Button>().onClick.AddListener(onClick);
        }
    }
}

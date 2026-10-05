using UnityEngine;
using UnityEngine.UI;
using Aethoria.Data;

namespace Aethoria.UI
{
    // 아이템 상세 정보 창(UI/Item_Detail). 원본 그림(Sprite/Items/Item_UI.png)에서 이름/등급/소개/설명 글자와
    // 능력치 자리의 "-"를 지운 배경 위에, 선택한 아이템의 정보를 글자로 덮어 보여준다.
    // 능력치 줄 이름(공격력/치명타 확률/...)과 아이콘, 별 5개는 배경에 그려져 있는 걸 그대로 쓴다.
    public class ItemDetailPanel : MonoBehaviour
    {
        // 배경 그림 크기. 아래 좌표들은 모두 이 그림 위에서 픽셀로 잰 값이다(y는 위에서부터).
        private const float ImageWidth = 1218f;
        private const float ImageHeight = 1292f;

        // 기본 능력치 / 추가 옵션 각 4줄의 세로 중심.
        private static readonly float[] BaseStatRowY = { 260f, 320f, 377f, 433f };
        private static readonly float[] BonusStatRowY = { 580f, 636f, 692f, 748f };
        // 별 5개의 중심 x(세로 중심은 StarRowY). 꺼진 별 그림(UI/Item_Detail_StarOff, 52px)을 이 위치에 덮는다.
        private static readonly float[] StarX = { 100f, 148.5f, 196f, 243.5f, 290f };
        private const float StarRowY = 350f;
        private const float StarSize = 52f;

        private static readonly Color TitleColor = new Color(0.93f, 0.78f, 1f);
        private static readonly Color GradeColor = new Color(0.78f, 0.62f, 1f);
        private static readonly Color SubTextColor = new Color(0.82f, 0.8f, 0.88f);
        private static readonly Color ValueColor = new Color(0.95f, 0.9f, 1f);
        private static readonly Color EmptyValueColor = new Color(0.6f, 0.58f, 0.66f);

        private float scale; // 그림 픽셀 → 캔버스 단위
        private RectTransform root;
        private Image icon;
        private Text iconFallbackLabel;
        private Text gradeText;
        private Text titleText;
        private Text flavorText;
        private readonly Text[] baseValues = new Text[4];
        private readonly Text[] bonusValues = new Text[4];
        private readonly RawImage[] starDimmers = new RawImage[5];
        private Text setEffectText;
        private Text descriptionText;

        // parent 안에 width(캔버스 단위) 너비로 만든다. 높이는 그림 비율대로 정해진다.
        public static ItemDetailPanel Create(RectTransform parent, float width)
        {
            var go = new GameObject("ItemDetailPanel", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(width, width * ImageHeight / ImageWidth);

            var background = go.GetComponent<RawImage>();
            background.texture = Resources.Load<Texture2D>("UI/Item_Detail");
            background.raycastTarget = false;

            var panel = go.AddComponent<ItemDetailPanel>();
            panel.root = rect;
            panel.scale = width / ImageWidth;
            panel.Build();
            go.SetActive(false);
            return panel;
        }

        public RectTransform Rect => root;

        private void Build()
        {
            icon = CreateImage("Icon", 75f, 75f, 322f, 300f);
            icon.preserveAspect = true;
            iconFallbackLabel = CreateText("IconName", 75f, 75f, 322f, 300f, 34f, SubTextColor, TextAnchor.MiddleCenter);

            // 등급보다 많은 별은 꺼진 별 그림으로 덮어서 어둡게 보이게 한다.
            var starOff = Resources.Load<Texture2D>("UI/Item_Detail_StarOff");
            for (int i = 0; i < starDimmers.Length; i++)
            {
                float half = StarSize / 2f;
                var rect = CreateRect("StarOff" + i, StarX[i] - half, StarRowY - half, StarX[i] + half, StarRowY + half,
                    typeof(RectTransform), typeof(RawImage));
                starDimmers[i] = rect.GetComponent<RawImage>();
                starDimmers[i].texture = starOff;
                starDimmers[i].raycastTarget = false;
            }

            gradeText = CreateText("Grade", 428f, 55f, 820f, 95f, 32f, GradeColor, TextAnchor.MiddleLeft);
            titleText = CreateText("Title", 375f, 95f, 960f, 172f, 60f, TitleColor, TextAnchor.MiddleLeft);
            titleText.fontStyle = FontStyle.Bold;
            // 이름이 길면(예: 공허의 자색 크리스털 갑주) 한 줄에 들어가도록 글자를 줄인다.
            // (Best Fit은 넘침 모드가 Wrap/Truncate일 때만 동작한다)
            titleText.verticalOverflow = VerticalWrapMode.Truncate;
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMaxSize = titleText.fontSize;
            titleText.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(titleText.fontSize * 0.5f));
            var glow = titleText.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(0.55f, 0.15f, 0.85f, 0.6f);
            glow.effectDistance = new Vector2(1.5f, -1.5f);
            flavorText = CreateText("Flavor", 378f, 172f, 960f, 214f, 32f, SubTextColor, TextAnchor.MiddleLeft);

            for (int i = 0; i < 4; i++)
            {
                baseValues[i] = CreateText("BaseValue" + i, 900f, BaseStatRowY[i] - 26f, 1140f, BaseStatRowY[i] + 26f, 34f, ValueColor, TextAnchor.MiddleRight);
                baseValues[i].fontStyle = FontStyle.Bold;
                bonusValues[i] = CreateText("BonusValue" + i, 880f, BonusStatRowY[i] - 26f, 1120f, BonusStatRowY[i] + 26f, 32f, ValueColor, TextAnchor.MiddleRight);
                bonusValues[i].fontStyle = FontStyle.Bold;
            }

            setEffectText = CreateText("SetEffect", 90f, 885f, 1140f, 985f, 32f, SubTextColor, TextAnchor.MiddleCenter);
            descriptionText = CreateText("Description", 90f, 1095f, 1000f, 1222f, 32f, SubTextColor, TextAnchor.UpperLeft);
            descriptionText.lineSpacing = 1.15f;
        }

        // enhanceLevel: 강화 단계. 이름 뒤에 "+N"을 붙이고 기본 능력치를 강화된 값으로 보여준다.
        public void Show(ItemData item, int enhanceLevel = 0)
        {
            if (item == null)
            {
                Hide();
                return;
            }

            var sprite = string.IsNullOrEmpty(item.iconPath) ? null : Resources.Load<Sprite>(item.iconPath);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            iconFallbackLabel.text = sprite != null ? "" : item.itemName;

            for (int i = 0; i < starDimmers.Length; i++) starDimmers[i].enabled = i >= item.StarCount;

            gradeText.text = item.GradeLabel;
            titleText.text = item.DisplayName(enhanceLevel);
            flavorText.text = item.flavorText;

            FillStats(baseValues, item.BaseStatsAt(enhanceLevel));
            FillStats(bonusValues, item.bonusStats);

            setEffectText.text = string.IsNullOrEmpty(item.setEffect) ? "세트 효과 없음" : item.setEffect;
            descriptionText.text = item.description;

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private static void FillStats(Text[] labels, ItemStats stats)
        {
            SetValue(labels[0], stats.attack, "");
            SetValue(labels[1], stats.critChance, "%");
            SetValue(labels[2], stats.skillDamage, "%");
            SetValue(labels[3], stats.critDamage, "%");
        }

        private static void SetValue(Text label, float value, string suffix)
        {
            if (Mathf.Approximately(value, 0f))
            {
                label.text = "-";
                label.color = EmptyValueColor;
                return;
            }

            label.text = (value > 0f ? "+" : "") + value.ToString("0.#") + suffix;
            label.color = ValueColor;
        }

        // 그림 픽셀 좌표(왼쪽 위 x0,y0 ~ 오른쪽 아래 x1,y1)를 그대로 앵커로 옮긴다.
        private RectTransform CreateRect(string name, float x0, float y0, float x1, float y1, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(x0 / ImageWidth, 1f - y1 / ImageHeight);
            rect.anchorMax = new Vector2(x1 / ImageWidth, 1f - y0 / ImageHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Image CreateImage(string name, float x0, float y0, float x1, float y1)
        {
            var image = CreateRect(name, x0, y0, x1, y1, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        // fontPx는 그림 위에서의 글자 크기(px). 실제 크기는 창 너비에 맞춰 줄어든다.
        private Text CreateText(string name, float x0, float y0, float x1, float y1, float fontPx, Color color, TextAnchor alignment)
        {
            var text = CreateRect(name, x0, y0, x1, y1, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(fontPx * scale));
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}

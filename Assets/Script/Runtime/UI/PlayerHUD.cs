using UnityEngine;
using UnityEngine.UI;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 화면 좌상단에 HP/MP 바를 그리고 매 프레임 캐릭터 상태에 맞춰 갱신한다.
    public class PlayerHUD : MonoBehaviour
    {
        private Character target;
        private Image hpFill;
        private Image mpFill;
        private Text hpLabel;
        private Text mpLabel;

        public void Initialize(Character character)
        {
            target = character;
            BuildBars();
        }

        private void BuildBars()
        {
            var root = new GameObject("StatusPanel", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(16f, -16f);
            rootRect.sizeDelta = new Vector2(220f, 50f);

            hpFill = CreateBar(rootRect, "HP", 0f, new Color(0.8f, 0.15f, 0.15f), out hpLabel);
            mpFill = CreateBar(rootRect, "MP", -26f, new Color(0.2f, 0.4f, 0.85f), out mpLabel);
        }

        private Image CreateBar(RectTransform parent, string label, float yOffset, Color fillColor, out Text text)
        {
            const float width = 200f;
            const float height = 20f;

            var bg = new GameObject(label + "_BG", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bg.transform;
            bgRect.SetParent(parent, false);
            bgRect.anchorMin = new Vector2(0f, 1f);
            bgRect.anchorMax = new Vector2(0f, 1f);
            bgRect.pivot = new Vector2(0f, 1f);
            bgRect.anchoredPosition = new Vector2(0f, yOffset);
            bgRect.sizeDelta = new Vector2(width, height);
            bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

            var fillGO = new GameObject(label + "_Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGO.transform;
            fillRect.SetParent(bgRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            var fillImage = fillGO.GetComponent<Image>();
            fillImage.color = fillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;

            var textGO = new GameObject(label + "_Text", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(bgRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            return fillImage;
        }

        private void Update()
        {
            if (target == null) return;

            float hpRatio = target.MaxHp > 0f ? target.CurrentHp / target.MaxHp : 0f;
            float mpRatio = target.MaxMana > 0f ? target.CurrentMana / target.MaxMana : 0f;

            hpFill.fillAmount = Mathf.Clamp01(hpRatio);
            mpFill.fillAmount = Mathf.Clamp01(mpRatio);

            hpLabel.text = $"HP {Mathf.CeilToInt(target.CurrentHp)}/{Mathf.CeilToInt(target.MaxHp)}";
            mpLabel.text = $"MP {Mathf.CeilToInt(target.CurrentMana)}/{Mathf.CeilToInt(target.MaxMana)}";
        }
    }
}

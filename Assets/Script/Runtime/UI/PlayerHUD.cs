using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 화면 좌상단에 HP/MP/EXP 바를 그리고, 그 아래에 쿨타임이 있는 스킬들의 아이콘을 나열해
    // 매 프레임 캐릭터 상태에 맞춰 갱신한다.
    public class PlayerHUD : MonoBehaviour
    {
        private Character target;
        private Image hpFill;
        private Image mpFill;
        private Image expFill;
        private Text hpLabel;
        private Text mpLabel;
        private Text expLabel;

        // 아이콘마다 "쿨타임 진행률(0=바로 사용 가능, 1=방금 씀)"을 구하는 함수만 따로 들고 있고,
        // 매 프레임 그 값을 오버레이 채우기/라벨 밝기에 그대로 반영한다.
        private readonly List<(Image overlay, Text label, Func<float> cooldownRatio)> skillIcons = new();

        public void Initialize(Character character)
        {
            target = character;
            BuildBars();
            BuildSkillIcons(character);
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
            rootRect.sizeDelta = new Vector2(220f, 76f);

            hpFill = CreateBar(rootRect, "HP", 0f, new Color(0.8f, 0.15f, 0.15f), out hpLabel);
            mpFill = CreateBar(rootRect, "MP", -26f, new Color(0.2f, 0.4f, 0.85f), out mpLabel);
            expFill = CreateBar(rootRect, "EXP", -52f, new Color(0.85f, 0.75f, 0.15f), out expLabel);
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

        // 체력바(HP/MP/EXP 패널) 바로 아래에, 쿨타임이 있는 스킬들을 키 순서대로 한 줄로 늘어놓는다.
        // X는 쿨타임이 없어(마나만 소모) 제외한다.
        private void BuildSkillIcons(Character character)
        {
            var row = new GameObject("SkillRow", typeof(RectTransform));
            row.transform.SetParent(transform, false);
            var rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(16f, -100f);

            float x = 0f;

            // Dash는 전용 아이콘 아트가 없어 기존처럼 글자만 보여준다.
            var dash = character.GetComponent<CharacterSkillDash>();
            if (dash != null) AddCooldownIcon(rowRect, ref x, "Sh", () => Ratio(dash.CooldownRemaining, dash.Cooldown));

            var q = character.GetComponent<CharacterSkillQ>();
            if (q != null) AddCooldownIcon(rowRect, ref x, "Q", () => Ratio(q.CooldownRemaining, q.Cooldown), LoadSkillIcon("Q"));

            var w = character.GetComponent<CharacterSkillW>();
            if (w != null) AddCooldownIcon(rowRect, ref x, "W", () => Ratio(w.CooldownRemaining, w.Cooldown), LoadSkillIcon("W"));

            var e = character.GetComponent<CharacterSkillE>();
            if (e != null) AddCooldownIcon(rowRect, ref x, "E", () => Ratio(e.CooldownRemaining, e.Cooldown), LoadSkillIcon("E"));

            // A는 고정 쿨타임이 아니라 최대 3충전 방식이라, "충전이 얼마나 소모됐는지"를 쿨타임처럼 보여준다.
            var a = character.GetComponent<CharacterSkillA>();
            if (a != null) AddCooldownIcon(rowRect, ref x, "A", () => a.MaxCharges > 0 ? 1f - (float)a.CurrentCharges / a.MaxCharges : 0f, LoadSkillIcon("A"));

            var s = character.GetComponent<CharacterSkillS>();
            if (s != null) AddCooldownIcon(rowRect, ref x, "S", () => Ratio(s.CooldownRemaining, s.Cooldown), LoadSkillIcon("S"));

            var d = character.GetComponent<CharacterSkillD>();
            if (d != null) AddCooldownIcon(rowRect, ref x, "D", () => Ratio(d.CooldownRemaining, d.Cooldown), LoadSkillIcon("D"));

            var r = character.GetComponent<CharacterSkillR>();
            if (r != null) AddCooldownIcon(rowRect, ref x, "R", () => Ratio(r.CooldownRemaining, r.Cooldown), LoadSkillIcon("R"));
        }

        // Assets/Resources/Art/UI/SkillIcons 아래의 스킬별 아이콘 아트(글자 배지가 그림에 이미 포함되어 있다)를 불러온다.
        private static Sprite LoadSkillIcon(string label)
        {
            return Resources.Load<Sprite>("Art/UI/SkillIcons/" + label);
        }

        private static float Ratio(float remaining, float total)
        {
            return total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
        }

        private void AddCooldownIcon(RectTransform parent, ref float x, string label, Func<float> cooldownRatio, Sprite iconSprite = null)
        {
            const float size = 34f;
            const float gap = 4f;

            var bg = new GameObject(label + "_Icon", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bg.transform;
            bgRect.SetParent(parent, false);
            bgRect.anchorMin = new Vector2(0f, 1f);
            bgRect.anchorMax = new Vector2(0f, 1f);
            bgRect.pivot = new Vector2(0f, 1f);
            bgRect.anchoredPosition = new Vector2(x, 0f);
            bgRect.sizeDelta = new Vector2(size, size);

            var bgImage = bg.GetComponent<Image>();
            if (iconSprite != null)
            {
                bgImage.sprite = iconSprite;
                bgImage.color = Color.white;
            }
            else
            {
                bgImage.color = new Color(0.5f, 0.2f, 0.7f, 0.9f);
            }

            var overlayGO = new GameObject(label + "_Overlay", typeof(RectTransform), typeof(Image));
            var overlayRect = (RectTransform)overlayGO.transform;
            overlayRect.SetParent(bgRect, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlay = overlayGO.GetComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.75f);
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Vertical;
            overlay.fillOrigin = (int)Image.OriginVertical.Bottom;
            overlay.fillAmount = 0f;

            var textGO = new GameObject(label + "_Text", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(bgRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            // 아이콘 아트에는 이미 글자 배지가 그려져 있으므로, 아트가 있으면 중복되는 글자 라벨은 숨긴다.
            if (iconSprite != null) textGO.SetActive(false);

            skillIcons.Add((overlay, text, cooldownRatio));
            x += size + gap;
        }

        private void Update()
        {
            if (target == null) return;

            float hpRatio = target.MaxHp > 0f ? target.CurrentHp / target.MaxHp : 0f;
            float mpRatio = target.MaxMana > 0f ? target.CurrentMana / target.MaxMana : 0f;
            float expRatio = target.ExpToNextLevel > 0 ? (float)target.CurrentExp / target.ExpToNextLevel : 0f;

            hpFill.fillAmount = Mathf.Clamp01(hpRatio);
            mpFill.fillAmount = Mathf.Clamp01(mpRatio);
            expFill.fillAmount = Mathf.Clamp01(expRatio);

            hpLabel.text = $"HP {Mathf.CeilToInt(target.CurrentHp)}/{Mathf.CeilToInt(target.MaxHp)}";
            mpLabel.text = $"MP {Mathf.CeilToInt(target.CurrentMana)}/{Mathf.CeilToInt(target.MaxMana)}";
            expLabel.text = $"Lv.{target.Level}  EXP {target.CurrentExp}/{target.ExpToNextLevel}";

            foreach (var (overlay, label, cooldownRatio) in skillIcons)
            {
                float ratio = cooldownRatio();
                overlay.fillAmount = Mathf.Clamp01(ratio);
                label.color = ratio > 0f ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
            }
        }
    }
}

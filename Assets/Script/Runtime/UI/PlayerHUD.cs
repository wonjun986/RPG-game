using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 화면 좌상단에 HP/MP 프레임(아트) + EXP/골드(코드로 그린 막대)를 그리고, 그 아래에
    // 스킬 프레임(아트) 위에 쿨타임 오버레이를 얹어 보여준다.
    public class PlayerHUD : MonoBehaviour
    {
        // Player UI.png / Skill UI.png 원본 캔버스는 2172x724(정확히 3:1)라서, 화면에 그릴 때도
        // 같은 비율로 맞춰야 안의 HP/MP 바·스킬 슬롯 좌표(아래 앵커 값들)가 어긋나지 않는다.
        private const float PlayerFrameWidth = 340f;
        private const float PlayerFrameHeight = PlayerFrameWidth / 3f;
        private const float SkillFrameWidth = 480f;
        private const float SkillFrameHeight = SkillFrameWidth / 3f;
        // 체력 프레임과 스킬 프레임 사이 간격은 바짝 붙이고, 스킬 프레임과 그 아래 EXP/골드
        // 사이만 약간 띄운다.
        private const float TopGap = 4f;
        private const float BottomGap = 10f;

        // 아래 앵커 좌표들은 PlayerUI.png / SkillUI.png 안에 이미 그려진 검은 게이지 칸,
        // 다이아몬드 슬롯의 픽셀 위치(2172x724 기준)를 이미지 크기로 나눈 비율이다.
        private static readonly Vector2 HpBarMin = new(0.4042f, 0.5235f);
        private static readonly Vector2 HpBarMax = new(0.8734f, 0.6478f);
        private static readonly Vector2 MpBarMin = new(0.4042f, 0.2514f);
        private static readonly Vector2 MpBarMax = new(0.8734f, 0.3660f);

        private const float SkillSlotMinY = 0.4185f;
        private const float SkillSlotMaxY = 0.6257f;
        private static readonly (string key, float xMin, float xMax)[] SkillSlotColumns =
        {
            ("A", 0.2247f, 0.2997f),
            ("S", 0.3255f, 0.3946f),
            ("D", 0.4194f, 0.4871f),
            ("Q", 0.5133f, 0.5810f),
            ("W", 0.6077f, 0.6731f),
            ("E", 0.6989f, 0.7693f),
            ("ULT", 0.8232f, 0.8812f),
        };

        private Character target;
        private Image hpFill;
        private Image mpFill;
        private Image expFill;
        private Text hpLabel;
        private Text mpLabel;
        private Text expLabel;
        private Text goldLabel;

        // 아이콘마다 "쿨타임 진행률(0=바로 사용 가능, 1=방금 씀)"을 구하는 함수만 따로 들고 있고,
        // 매 프레임 그 값을 오버레이 채우기/라벨 밝기에 그대로 반영한다.
        private readonly List<(Image overlay, Text label, Func<float> cooldownRatio, Text countdown, Func<float> remainingSeconds)> skillIcons = new();

        public void Initialize(Character character)
        {
            target = character;
            BuildBars();
            BuildSkillIcons(character);
        }

        private void BuildBars()
        {
            var frameRect = CreateArtFrame("PlayerFrame", "UI/PlayerUI", new Vector2(16f, -16f), PlayerFrameWidth, PlayerFrameHeight);

            hpFill = CreateArtBar(frameRect, HpBarMin, HpBarMax, new Color(0.85f, 0.15f, 0.2f), out hpLabel);
            mpFill = CreateArtBar(frameRect, MpBarMin, MpBarMax, new Color(0.35f, 0.55f, 0.95f), out mpLabel);

            // HP/MP 프레임과 그 아래 스킬 프레임(BuildSkillIcons에서 만듦)을 둘 다 지나서 배치한다.
            float belowBothFrames = -16f - PlayerFrameHeight - TopGap - SkillFrameHeight - BottomGap;
            var root = new GameObject("StatusPanel", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(16f, belowBothFrames);
            rootRect.sizeDelta = new Vector2(220f, 60f);

            expFill = CreateBar(rootRect, "EXP", 0f, new Color(0.85f, 0.75f, 0.15f), out expLabel);
            goldLabel = CreateGoldRow(rootRect, -26f);
        }

        // PlayerUI.png / SkillUI.png를 원본 비율(3:1) 그대로 좌상단에 띄우는 배경 프레임.
        // RawImage를 쓰는 이유는 Resources.Load<Texture2D>가 스프라이트 슬라이스 모드에 상관없이
        // 항상 원본 텍스처를 그대로 불러오기 때문이다(TitleScreenUI의 BuildBackground와 같은 방식).
        private RectTransform CreateArtFrame(string name, string resourcesPath, Vector2 anchoredPosition, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(width, height);

            var image = go.GetComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>(resourcesPath);
            image.raycastTarget = false;
            return rect;
        }

        // 프레임 아트 안에 이미 뚫려 있는 검은 게이지 칸(anchorMin~anchorMax, 프레임 크기 대비 비율)에
        // 맞춰 채우기 바를 얹는다. 배경/테두리는 아트가 담당하므로 여기서는 색 채우기만 그린다.
        private Image CreateArtBar(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color fillColor, out Text text)
        {
            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGO.transform;
            fillRect.SetParent(parent, false);
            fillRect.anchorMin = anchorMin;
            fillRect.anchorMax = anchorMax;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGO.GetComponent<Image>();
            fillImage.UseAsFilled();
            fillImage.color = fillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(parent, false);
            textRect.anchorMin = anchorMin;
            textRect.anchorMax = anchorMax;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 15;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            return fillImage;
        }

        // 골드는 막대가 아니라 주머니 아이콘 + 숫자로 보여준다(찰 수 있는 상한이 없어서 막대와 안 맞음).
        private Text CreateGoldRow(RectTransform parent, float yOffset)
        {
            const float iconSize = 20f;

            var iconGO = new GameObject("Gold_Icon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)iconGO.transform;
            iconRect.SetParent(parent, false);
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, yOffset);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            var iconImage = iconGO.GetComponent<Image>();
            iconImage.sprite = Resources.Load<Sprite>("UI/Money");
            iconImage.preserveAspect = true;

            var textGO = new GameObject("Gold_Text", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(parent, false);
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = new Vector2(iconSize + 6f, yOffset - 2f);
            textRect.sizeDelta = new Vector2(180f, iconSize);

            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(1f, 0.85f, 0.3f);
            return text;
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
            fillImage.UseAsFilled();
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

        // 체력 프레임 바로 아래에 스킬 프레임(아트)을 띄우고, 그 안의 A/S/D/Q/W/E/ULT 슬롯 자리에
        // 맞춰 쿨타임이 있는 스킬 아이콘을 얹는다. Dash(Shift)는 쿨타임이 짧아 따로 표시하지 않는다.
        private void BuildSkillIcons(Character character)
        {
            float skillFrameY = -16f - PlayerFrameHeight - TopGap;
            var skillFrameRect = CreateArtFrame("SkillFrame", "UI/SkillUI_UltOn", new Vector2(16f, skillFrameY), SkillFrameWidth, SkillFrameHeight);

            foreach (var (key, xMin, xMax) in SkillSlotColumns)
            {
                var anchorMin = new Vector2(xMin, SkillSlotMinY);
                var anchorMax = new Vector2(xMax, SkillSlotMaxY);

                switch (key)
                {
                    case "A":
                        var a = character.GetComponent<CharacterSkillA>();
                        // A는 고정 쿨타임이 아니라 충전식이라 "몇 초 남았는지"가 없어 숫자는 표시하지 않는다.
                        if (a != null) AddSlotIcon(skillFrameRect, anchorMin, anchorMax, "A", () => a.MaxCharges > 0 ? 1f - (float)a.CurrentCharges / a.MaxCharges : 0f, null);
                        break;
                    case "S":
                        var s = character.GetComponent<CharacterSkillS>();
                        if (s != null) AddSlotIcon(skillFrameRect, anchorMin, anchorMax, "S", () => Ratio(s.CooldownRemaining, s.Cooldown), () => s.CooldownRemaining);
                        break;
                    case "D":
                        var d = character.GetComponent<CharacterSkillD>();
                        if (d != null) AddSlotIcon(skillFrameRect, anchorMin, anchorMax, "D", () => Ratio(d.CooldownRemaining, d.Cooldown), () => d.CooldownRemaining);
                        break;
                    case "Q":
                        var q = character.GetComponent<CharacterSkillQ>();
                        if (q != null) AddSlotIcon(skillFrameRect, anchorMin, anchorMax, "Q", () => Ratio(q.CooldownRemaining, q.Cooldown), () => q.CooldownRemaining);
                        break;
                    case "W":
                        var w = character.GetComponent<CharacterSkillW>();
                        if (w != null) AddSlotIcon(skillFrameRect, anchorMin, anchorMax, "W", () => Ratio(w.CooldownRemaining, w.Cooldown), () => w.CooldownRemaining);
                        break;
                    case "E":
                        var e = character.GetComponent<CharacterSkillE>();
                        if (e != null) AddSlotIcon(skillFrameRect, anchorMin, anchorMax, "E", () => Ratio(e.CooldownRemaining, e.Cooldown), () => e.CooldownRemaining);
                        break;
                    case "ULT":
                        var r = character.GetComponent<CharacterSkillR>();
                        if (r != null) AddSlotIcon(skillFrameRect, anchorMin, anchorMax, "R", () => Ratio(r.CooldownRemaining, r.Cooldown), () => r.CooldownRemaining);
                        break;
                }
            }
        }

        // 아이콘 전체를 덮는 초 단위 쿨타임 숫자 텍스트(검은 외곽선을 둘러 밝은/어두운 아이콘 위에서도 읽히게 한다).
        private static Text CreateCountdownText(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Countdown", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            return text;
        }

        // Assets/Resources/Art/UI/SkillIcons 아래의 스킬별 아이콘 아트를 SkillUI 프레임의 슬롯 자리에
        // 얹고, 그 위에 쿨타임 오버레이만 따로 그린다(슬롯 글자는 프레임 아트에 이미 그려져 있다).
        private void AddSlotIcon(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, string label, Func<float> cooldownRatio, Func<float> remainingSeconds)
        {
            var iconGO = new GameObject(label + "_Icon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)iconGO.transform;
            iconRect.SetParent(parent, false);
            iconRect.anchorMin = anchorMin;
            iconRect.anchorMax = anchorMax;
            iconRect.offsetMin = new Vector2(3f, 3f);
            iconRect.offsetMax = new Vector2(-3f, -3f);
            var iconImage = iconGO.GetComponent<Image>();
            iconImage.sprite = LoadSkillIcon(label);
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;

            var overlayGO = new GameObject(label + "_Overlay", typeof(RectTransform), typeof(Image));
            var overlayRect = (RectTransform)overlayGO.transform;
            overlayRect.SetParent(parent, false);
            overlayRect.anchorMin = anchorMin;
            overlayRect.anchorMax = anchorMax;
            overlayRect.offsetMin = new Vector2(3f, 3f);
            overlayRect.offsetMax = new Vector2(-3f, -3f);
            var overlay = overlayGO.GetComponent<Image>();
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Vertical;
            overlay.fillOrigin = (int)Image.OriginVertical.Bottom;
            overlay.color = new Color(0f, 0f, 0f, 0.75f);
            overlay.fillAmount = 0f;

            Text countdown = remainingSeconds != null ? CreateCountdownText(parent, anchorMin, anchorMax) : null;

            skillIcons.Add((overlay, null, cooldownRatio, countdown, remainingSeconds));
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

        private void Update()
        {
            if (target == null) return;

            float hpRatio = target.MaxHp > 0f ? target.CurrentHp / target.MaxHp : 0f;
            float mpRatio = target.MaxMana > 0f ? target.CurrentMana / target.MaxMana : 0f;
            float expRatio = target.ExpToNextLevel > 0 ? (float)target.CurrentExp / target.ExpToNextLevel : 0f;

            hpFill.fillAmount = Mathf.Clamp01(hpRatio);
            mpFill.fillAmount = Mathf.Clamp01(mpRatio);
            expFill.fillAmount = Mathf.Clamp01(expRatio);

            hpLabel.text = $"{Mathf.CeilToInt(target.CurrentHp)}/{Mathf.CeilToInt(target.MaxHp)}";
            mpLabel.text = $"{Mathf.CeilToInt(target.CurrentMana)}/{Mathf.CeilToInt(target.MaxMana)}";
            expLabel.text = $"Lv.{target.Level}  EXP {target.CurrentExp}/{target.ExpToNextLevel}";
            goldLabel.text = target.Gold.ToString("N0");

            foreach (var (overlay, label, cooldownRatio, countdown, remainingSeconds) in skillIcons)
            {
                float ratio = cooldownRatio();
                overlay.fillAmount = Mathf.Clamp01(ratio);
                if (label != null) label.color = ratio > 0f ? new Color(1f, 1f, 1f, 0.5f) : Color.white;

                if (countdown != null)
                {
                    float remaining = remainingSeconds();
                    countdown.text = remaining > 0.05f ? Mathf.CeilToInt(remaining).ToString() : string.Empty;
                }
            }
        }
    }
}

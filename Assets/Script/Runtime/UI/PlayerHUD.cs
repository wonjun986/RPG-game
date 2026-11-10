using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 화면 좌상단에 플레이어 HUD 프레임(초상화 + HP/MP 칸 + Q/W/E/A/S/D/R 스킬 슬롯이 한 장에 그려진 아트)을 띄우고,
    // 그 칸들에 HP/MP 채우기와 스킬 그림·쿨타임 오버레이를 얹는다. 그 아래에 EXP/골드(코드로 그린 막대)를 둔다.
    public class PlayerHUD : MonoBehaviour
    {
        // PlayerHUD.png(고딕 퍼플 빈 스킬 슬롯 HUD) 원본 캔버스는 2172x724(정확히 3:1)라서, 화면에 그릴 때도
        // 같은 비율로 맞춰야 안의 HP/MP 칸·스킬 슬롯 좌표(아래 앵커 값들)가 어긋나지 않는다.
        // 기준 해상도(1280x720)에서 예전 스킬 프레임과 같은 폭. 더 넓히면 화면 가운데 위의 보스 체력바와 많이 겹친다.
        private const float HudFrameWidth = 480f;
        private const float HudFrameHeight = HudFrameWidth / 3f;
        // HUD 프레임과 그 아래 EXP/골드 사이 간격.
        private const float BottomGap = 6f;

        // 아래 앵커 좌표들은 PlayerHUD.png 안에 그려진 칸 테두리 안쪽의 픽셀 위치(2172x724 기준)를 이미지 크기로 나눈 비율이다
        // (y는 아래에서부터). HP 칸 x 623~1805 / y 191~231, MP 칸 x 613~1786 / y 261~299.
        private static readonly Vector2 HpBarMin = new(0.2868f, 0.6796f);
        private static readonly Vector2 HpBarMax = new(0.8315f, 0.7362f);
        private static readonly Vector2 MpBarMin = new(0.2822f, 0.5856f);
        private static readonly Vector2 MpBarMax = new(0.8227f, 0.6395f);

        // 스킬 슬롯: 아트에 적힌 순서(Q W E A S D)대로 y 361~517, R은 오른쪽 큰 슬롯(x 1790~2020 / y 345~495).
        private static readonly (string key, float xMin, float xMax, float yMin, float yMax)[] SkillSlots =
        {
            ("Q", 0.2086f, 0.2993f, 0.2845f, 0.5014f),
            ("W", 0.3117f, 0.4010f, 0.2845f, 0.5014f),
            ("E", 0.4121f, 0.5014f, 0.2845f, 0.5014f),
            ("A", 0.5124f, 0.6013f, 0.2845f, 0.5014f),
            ("S", 0.6119f, 0.6998f, 0.2845f, 0.5014f),
            ("D", 0.7113f, 0.8006f, 0.2845f, 0.5014f),
            ("ULT", 0.8241f, 0.9300f, 0.3163f, 0.5235f),
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
            var frameRect = CreateArtFrame("PlayerHudFrame", "UI/PlayerHUD", new Vector2(16f, -16f), HudFrameWidth, HudFrameHeight);
            BuildBars(frameRect);
            BuildSkillIcons(frameRect, character);
        }

        private void BuildBars(RectTransform frameRect)
        {
            hpFill = CreateArtBar(frameRect, HpBarMin, HpBarMax, new Color(0.85f, 0.15f, 0.2f), out hpLabel);
            mpFill = CreateArtBar(frameRect, MpBarMin, MpBarMax, new Color(0.35f, 0.55f, 0.95f), out mpLabel);

            // HUD 프레임 바로 아래에 배치한다.
            float belowBothFrames = -16f - HudFrameHeight - BottomGap;
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

        // PlayerHUD.png를 원본 비율(3:1) 그대로 좌상단에 띄우는 배경 프레임.
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
            // HUD 아트의 HP/MP 칸이 얇아서(약 9px) 작은 글씨를 칸 위아래로 살짝 넘치게 두고, 외곽선으로 읽히게 한다.
            text.fontSize = 11;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            var outline = textGO.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

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

        // HUD 프레임 안의 Q/W/E/A/S/D/R 슬롯 자리에 맞춰 쿨타임이 있는 스킬 그림을 얹는다.
        // Dash(Shift)는 쿨타임이 짧아 따로 표시하지 않는다.
        private void BuildSkillIcons(RectTransform skillFrameRect, Character character)
        {
            foreach (var (key, xMin, xMax, yMin, yMax) in SkillSlots)
            {
                var anchorMin = new Vector2(xMin, yMin);
                var anchorMax = new Vector2(xMax, yMax);

                switch (key)
                {
                    case "A":
                        var a = character.GetComponent<CharacterSkillA>();
                        // A는 충전식이라 충전이 남아 있으면 바로 쓸 수 있다. 숫자는 충전을 다 써서 못 쓸 때만 다음 충전까지 남은 초를 보여준다.
                        if (a != null) AddSlotIcon(skillFrameRect, anchorMin, anchorMax, "A", () => a.MaxCharges > 0 ? 1f - (float)a.CurrentCharges / a.MaxCharges : 0f, () => a.CurrentCharges > 0 ? 0f : a.RechargeRemaining);
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

        // Assets/Resources/Art/UI/SkillSlotArt 아래의 스킬별 그림을 HUD 프레임의 슬롯 자리에 꽉 차게
        // 얹고, 그 위에 쿨타임 오버레이만 따로 그린다(슬롯 테두리와 글자는 프레임 아트에 이미 그려져 있다).
        private void AddSlotIcon(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, string label, Func<float> cooldownRatio, Func<float> remainingSeconds)
        {
            var iconGO = new GameObject(label + "_Icon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)iconGO.transform;
            iconRect.SetParent(parent, false);
            iconRect.anchorMin = anchorMin;
            iconRect.anchorMax = anchorMax;
            iconRect.offsetMin = new Vector2(1f, 1f);
            iconRect.offsetMax = new Vector2(-1f, -1f);
            var iconImage = iconGO.GetComponent<Image>();
            iconImage.sprite = LoadSkillIcon(label);
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;

            var overlayGO = new GameObject(label + "_Overlay", typeof(RectTransform), typeof(Image));
            var overlayRect = (RectTransform)overlayGO.transform;
            overlayRect.SetParent(parent, false);
            overlayRect.anchorMin = anchorMin;
            overlayRect.anchorMax = anchorMax;
            overlayRect.offsetMin = new Vector2(1f, 1f);
            overlayRect.offsetMax = new Vector2(-1f, -1f);
            var overlay = overlayGO.GetComponent<Image>();
            overlay.UseAsFilled();
            overlay.raycastTarget = false;
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Vertical;
            overlay.fillOrigin = (int)Image.OriginVertical.Bottom;
            overlay.color = new Color(0f, 0f, 0f, 0.75f);
            overlay.fillAmount = 0f;

            Text countdown = remainingSeconds != null ? CreateCountdownText(parent, anchorMin, anchorMax) : null;

            skillIcons.Add((overlay, null, cooldownRatio, countdown, remainingSeconds));
        }

        // 스킬 아이콘 원본(쓰레기통/Resources/Art/UI/SkillIcons에 보관)에서 테두리와 글자 배지를 빼고 안쪽 그림만 잘라 둔 것(Art/UI/SkillSlotArt)을 불러온다.
        // 슬롯 테두리·글자는 HUD 프레임 아트에 그려져 있어서 아이콘의 것과 겹치지 않게 한다.
        private static Sprite LoadSkillIcon(string label)
        {
            return Resources.Load<Sprite>("Art/UI/SkillSlotArt/" + label);
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

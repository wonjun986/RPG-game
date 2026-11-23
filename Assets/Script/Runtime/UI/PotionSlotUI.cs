using UnityEngine;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Data;

namespace Aethoria.UI
{
    // 플레이어 HUD 프레임 오른쪽에 붙는 회복 포션 칸: [F] 키 표시 · 포션 그림 · 남은 개수 · 마신 뒤 쿨타임.
    // 개수가 바뀌면 칸이 톡 튀고, 체력이 30% 아래인데 포션이 있으면 칸 테두리가 붉게 맥동해 "마셔라"를 알린다.
    // 판 그림은 UI/HUD/PotionSlot(없으면 코드로 그린 둥근 판), 포션 그림은 아이템 아이콘(UI/Items/Potion)을 쓴다.
    public class PotionSlotUI : MonoBehaviour
    {
        // PlayerHUD 프레임(좌상단 16,-16 / 480x160)의 스킬 줄 높이에 맞춘다.
        private static readonly Vector2 SlotPosition = new Vector2(16f + 480f + 6f, -16f - 72f);
        private const float SlotSize = 54f;
        private const float LowHpFraction = 0.3f;

        private Character player;
        private Inventory inventory;
        private CharacterPotionUse potionUse;

        private RectTransform slot;
        private Image frame;
        private Image icon;
        private Image cooldown;
        private Image warnGlow;
        private Text countText;
        private int shownCount = -1;
        private float bumpUntil;

        public void Initialize(Character character)
        {
            player = character;
            inventory = character.GetComponent<Inventory>();
            potionUse = character.GetComponent<CharacterPotionUse>();
            Build();
        }

        private void Build()
        {
            slot = (RectTransform)new GameObject("PotionSlot", typeof(RectTransform)).transform;
            slot.SetParent(transform, false);
            slot.anchorMin = slot.anchorMax = new Vector2(0f, 1f);
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = SlotPosition + new Vector2(SlotSize / 2f, -SlotSize / 2f);
            slot.sizeDelta = new Vector2(SlotSize, SlotSize);

            // 체력이 낮을 때 칸 뒤에서 맥동하는 붉은 빛.
            warnGlow = MakeImage("WarnGlow", slot, Aethoria.Fx.FxUtil.SoftCircleSprite, new Color(1f, 0.2f, 0.25f, 0f));
            Stretch(warnGlow.rectTransform, -18f);

            frame = MakeImage("Frame", slot, UiArt.LoadOr("UI/HUD/PotionSlot",
                () => UiArt.RoundedPanel(new Color(0.07f, 0.04f, 0.1f, 0.88f), new Color(0.62f, 0.45f, 0.85f, 1f), 9, 2)), Color.white);
            frame.type = Image.Type.Sliced;
            Stretch(frame.rectTransform, 0f);

            var potionSprite = ItemDatabase.Potion.LoadIcon();
            icon = MakeImage("Icon", slot, potionSprite != null ? potionSprite : UiArt.PixelIcon("potion", PotionPixels, new Color(0.9f, 0.2f, 0.3f)), Color.white);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform, 10f);

            cooldown = MakeImage("Cooldown", slot, null, new Color(0f, 0f, 0f, 0.6f));
            cooldown.UseAsFilled();
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = (int)Image.Origin360.Top;
            cooldown.fillClockwise = false;
            cooldown.fillAmount = 0f;
            Stretch(cooldown.rectTransform, 4f);

            // [F] 키 표시(왼쪽 위 작은 키캡).
            var keyCap = MakeImage("KeyCap", slot, UiArt.LoadOr("UI/Common/KeyCap",
                () => UiArt.RoundedPanel(new Color(0.16f, 0.1f, 0.22f, 0.95f), new Color(0.95f, 0.8f, 0.5f, 1f), 4, 1)), Color.white);
            keyCap.type = Image.Type.Sliced;
            var capRect = keyCap.rectTransform;
            capRect.anchorMin = capRect.anchorMax = new Vector2(0f, 1f);
            capRect.pivot = new Vector2(0.5f, 0.5f);
            capRect.anchoredPosition = new Vector2(2f, -2f); // 왼쪽 위 보석 위에 얹는다
            capRect.sizeDelta = new Vector2(22f, 22f);
            var keyText = MakeText("Key", capRect, 12, new Color(1f, 0.88f, 0.55f), TextAnchor.MiddleCenter);
            keyText.text = "F";

            countText = MakeText("Count", slot, 16, Color.white, TextAnchor.LowerRight);
            var countRect = countText.rectTransform;
            countRect.anchorMin = Vector2.zero;
            countRect.anchorMax = Vector2.one;
            countRect.offsetMin = new Vector2(2f, 6f);
            countRect.offsetMax = new Vector2(-10f, -2f);
        }

        private void Update()
        {
            if (player == null || inventory == null) return;
            int count = inventory.GetCount(ItemDatabase.Potion.id);
            if (count != shownCount)
            {
                if (shownCount >= 0) bumpUntil = Time.unscaledTime + 0.25f;
                shownCount = count;
                countText.text = count > 99 ? "99+" : count.ToString();
                countText.color = count > 0 ? Color.white : new Color(1f, 0.45f, 0.45f);
            }

            bool empty = count <= 0;
            icon.color = empty ? new Color(0.3f, 0.28f, 0.34f, 0.8f) : Color.white;
            cooldown.fillAmount = potionUse != null && potionUse.Cooldown > 0f ? Mathf.Clamp01(potionUse.CooldownRemaining / potionUse.Cooldown) : 0f;

            // 톡 튀기(개수가 바뀌었을 때).
            float bump = Mathf.Clamp01((bumpUntil - Time.unscaledTime) / 0.25f);
            slot.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(bump * Mathf.PI));

            // 체력이 낮은데 포션이 있으면 붉게 맥동.
            bool warn = !empty && !player.IsDead && player.MaxHp > 0f && player.CurrentHp / player.MaxHp <= LowHpFraction;
            float pulse = warn ? 0.35f + 0.3f * Mathf.Sin(Time.unscaledTime * 7f) : 0f;
            var c = warnGlow.color;
            c.a = Mathf.MoveTowards(c.a, pulse, Time.unscaledDeltaTime * 4f);
            warnGlow.color = c;
        }

        private static readonly string[] PotionPixels =
        {
            "  ooo  ",
            "  o+o  ",
            "  o#o  ",
            " o###o ",
            "o#+###o",
            "o#####o",
            "o##o##o",
            " o###o ",
            "  ooo  ",
        };

        private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text MakeText(string name, Transform parent, int size, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}

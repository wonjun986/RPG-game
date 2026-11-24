using UnityEngine;
using UnityEngine.UI;
using Aethoria.Monsters;

namespace Aethoria.UI
{
    // 보스 전용 대형 체력바. 화면 하단 중앙에 이름과 함께 크게 표시하고,
    // 보스가 없거나 죽으면 숨긴다. 보스에는 몬스터 발밑의 작은 MonsterHealthBar를 붙이지 않는다(이 체력바 하나로 본다).
    public class BossHealthUI : MonoBehaviour
    {
        private GameObject panel;
        private Image fill;
        private Text nameLabel;
        private Text hpLabel;
        private Monster boss;
        private HealthBarTrail trail;
        private BossTitleCard titleCard;
        private Text subtitleLabel;
        private RectTransform barRect;
        private readonly System.Collections.Generic.List<(Image tick, float fraction)> ticks = new();

        // 보스 이름 아래 칭호(없으면 이름만). 이름은 MonsterData.monsterName과 같아야 한다.
        private static readonly System.Collections.Generic.Dictionary<string, string> Subtitles = new()
        {
            { "알리아스", "어둠의 숲을 지키는 붉은 기사" },
            { "보스 슬라임", "평야를 삼킨 거대한 점액" },
            { "황금 모래룡", "폐허에 잠든 사막의 군주" },
            { "아쿠아", "푸른 바다의 지배자" },
            { "루나벨", "절망의 언덕에 군림하는 자" },
        };

        public void Initialize()
        {
            BuildPanel();
            Hide();
        }

        // 보스가 스폰될 때 호출해서 이 체력바가 그 보스를 따라가게 한다.
        public void Bind(Monster bossMonster, string bossName)
        {
            if (boss != null) boss.OnDied -= HandleBossDied;

            boss = bossMonster;
            boss.OnDied += HandleBossDied;
            nameLabel.text = bossName;
            subtitleLabel.text = Subtitles.TryGetValue(bossName, out var subtitle) ? subtitle : string.Empty;
            BuildTicks(bossMonster.GetComponent<IBossPhaseMarks>());
            panel.SetActive(true);
            RefreshBar();
            trail?.Snap();
            // 보스 등장 컷: 레터박스 + 이름 자막.
            if (titleCard == null) titleCard = BossTitleCard.Create(transform);
            titleCard.Show(bossName);
        }

        private void HandleBossDied(Monster _)
        {
            Hide();
        }

        // 보스를 잡지 못한 채 스테이지를 나가도(포탈로 이전/다음 스테이지 이동) 체력바가 화면에
        // 남아있지 않도록, 스테이지가 바뀔 때마다 호출해서 이전 보스와의 연결을 끊는다.
        public void Hide()
        {
            if (boss != null) boss.OnDied -= HandleBossDied;
            boss = null;
            panel.SetActive(false);
        }

        private void Update()
        {
            if (boss == null) return;
            RefreshBar();
        }

        private void RefreshBar()
        {
            float ratio = boss.MaxHp > 0f ? Mathf.Clamp01(boss.CurrentHp / boss.MaxHp) : 0f;
            fill.fillAmount = ratio;
            hpLabel.text = $"{Mathf.CeilToInt(boss.CurrentHp)} / {Mathf.CeilToInt(boss.MaxHp)}";
            // 지나간 눈금은 흐려진다.
            foreach (var (tick, fraction) in ticks)
                tick.color = ratio <= fraction ? new Color(0.5f, 0.45f, 0.45f, 0.5f) : new Color(1f, 0.85f, 0.45f, 1f);
        }

        // 패턴이 바뀌는 체력 지점마다 막대 위에 금빛 눈금(세로 선 + 위아래 작은 마름모)을 세운다.
        private void BuildTicks(IBossPhaseMarks marks)
        {
            foreach (var (tick, _) in ticks) if (tick != null) Destroy(tick.gameObject);
            ticks.Clear();
            if (marks == null || marks.PhaseMarks == null) return;
            foreach (float fraction in marks.PhaseMarks)
            {
                if (fraction <= 0f || fraction >= 1f) continue;
                var go = new GameObject("PhaseTick", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(barRect, false);
                rect.SetSiblingIndex(hpLabel.transform.GetSiblingIndex()); // 체력 숫자보다 아래에 그린다
                rect.anchorMin = new Vector2(fraction, 0f);
                rect.anchorMax = new Vector2(fraction, 1f);
                rect.sizeDelta = new Vector2(3f, 8f);
                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
                outline.effectDistance = new Vector2(1f, -1f);
                for (int side = 0; side < 2; side++)
                {
                    var gem = new GameObject("Gem", typeof(RectTransform), typeof(Image));
                    var gemRect = (RectTransform)gem.transform;
                    gemRect.SetParent(rect, false);
                    gemRect.anchorMin = gemRect.anchorMax = new Vector2(0.5f, side);
                    gemRect.sizeDelta = new Vector2(7f, 7f);
                    gemRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    var gemImage = gem.GetComponent<Image>();
                    gemImage.raycastTarget = false;
                    gem.AddComponent<TickColorFollow>().Source = image;
                }
                ticks.Add((image, fraction));
            }
        }

        // 마름모가 눈금 선과 같은 색을 따라가게.
        private class TickColorFollow : MonoBehaviour
        {
            public Image Source;
            private Image self;
            private void Awake() => self = GetComponent<Image>();
            private void LateUpdate() { if (Source != null) self.color = Source.color; }
        }

        // 보스 체력바 틀 그림(UI/HUD/BossHpFrame, 약 7:1 — 왼쪽 해골, 오른쪽 초승달). 안쪽 검은 홈의 위치(그림 크기 대비 비율)에
        // 채우기 막대를 맞춘다. 그림이 없으면 예전처럼 검은 판 + 빨간 막대로 그린다.
        private const float FrameWidth = 660f;
        private const float FrameAspect = 1320f / 188f;
        private static readonly Vector2 ChannelMin = new(0.0872f, 0.4235f);
        private static readonly Vector2 ChannelMax = new(0.9323f, 0.6254f);

        private void BuildPanel()
        {
            var frameSprite = Resources.Load<Sprite>("UI/HUD/BossHpFrame");
            float frameHeight = FrameWidth / FrameAspect;
            panel = new GameObject("BossHealthPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            // 화면 위쪽은 왼쪽 플레이어 HUD·오른쪽 퀘스트 표시가 차지해서, 보스 체력바는 아래 가운데(바닥 아래 흙 부분)에 둔다.
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 16f);
            rect.sizeDelta = frameSprite != null ? new Vector2(FrameWidth, frameHeight + 44f) : new Vector2(640f, 88f);

            var bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bg.transform;
            bgRect.SetParent(rect, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            // 틀 그림이 있으면 뒤 판은 이름 아래만 은은하게(글자가 바닥 그림 위에서도 읽히게).
            bg.GetComponent<Image>().color = frameSprite != null ? new Color(0f, 0f, 0f, 0f) : new Color(0f, 0f, 0f, 0.55f);

            var nameGO = new GameObject("Name_Text", typeof(RectTransform), typeof(Text));
            var nameRect = (RectTransform)nameGO.transform;
            nameRect.SetParent(rect, false);
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -6f);
            nameRect.sizeDelta = new Vector2(0f, 26f);
            nameLabel = nameGO.GetComponent<Text>();
            nameLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameLabel.fontSize = 20;
            nameLabel.fontStyle = FontStyle.Bold;
            nameLabel.alignment = TextAnchor.MiddleCenter;
            nameLabel.color = new Color(1f, 0.85f, 0.4f);
            AddOutline(nameLabel);

            var subGO = new GameObject("Subtitle_Text", typeof(RectTransform), typeof(Text));
            var subRect = (RectTransform)subGO.transform;
            subRect.SetParent(rect, false);
            subRect.anchorMin = new Vector2(0f, 1f);
            subRect.anchorMax = new Vector2(1f, 1f);
            subRect.pivot = new Vector2(0.5f, 1f);
            subRect.anchoredPosition = new Vector2(0f, -30f);
            subRect.sizeDelta = new Vector2(0f, 16f);
            subtitleLabel = subGO.GetComponent<Text>();
            subtitleLabel.font = nameLabel.font;
            subtitleLabel.fontSize = 13;
            subtitleLabel.alignment = TextAnchor.MiddleCenter;
            subtitleLabel.color = new Color(0.8f, 0.72f, 0.9f);
            AddOutline(subtitleLabel);

            var barBG = new GameObject("Bar_BG", typeof(RectTransform), typeof(Image));
            var barBGRect = (RectTransform)barBG.transform;
            if (frameSprite != null)
            {
                // 틀 그림을 아래에 깔고, 막대(채우기·숫자·눈금)는 그 안 검은 홈 자리에 둔다.
                var frameGO = new GameObject("Bar_Frame", typeof(RectTransform), typeof(Image));
                var frameRect = (RectTransform)frameGO.transform;
                frameRect.SetParent(rect, false);
                frameRect.anchorMin = new Vector2(0f, 0f);
                frameRect.anchorMax = new Vector2(1f, 0f);
                frameRect.pivot = new Vector2(0.5f, 0f);
                frameRect.anchoredPosition = Vector2.zero;
                frameRect.sizeDelta = new Vector2(0f, frameHeight);
                var frameImage = frameGO.GetComponent<Image>();
                frameImage.sprite = frameSprite;
                frameImage.raycastTarget = false;

                barBGRect.SetParent(frameRect, false);
                barBGRect.anchorMin = ChannelMin;
                barBGRect.anchorMax = ChannelMax;
                barBGRect.offsetMin = new Vector2(6f, 1f);    // 홈 양 끝이 둥글어서 살짝 안쪽부터
                barBGRect.offsetMax = new Vector2(-6f, -1f);
                barBG.GetComponent<Image>().color = new Color(0.12f, 0.02f, 0.04f, 0.6f);
            }
            else
            {
                barBGRect.SetParent(rect, false);
                barBGRect.anchorMin = new Vector2(0f, 0f);
                barBGRect.anchorMax = new Vector2(1f, 0f);
                barBGRect.pivot = new Vector2(0.5f, 0f);
                barBGRect.anchoredPosition = new Vector2(0f, 8f);
                barBGRect.sizeDelta = new Vector2(-20f, 32f);
                barBG.GetComponent<Image>().color = new Color(0.15f, 0.05f, 0.05f, 0.9f);
            }
            barBG.GetComponent<Image>().raycastTarget = false;
            barRect = barBGRect;

            var fillGO = new GameObject("Bar_Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGO.transform;
            fillRect.SetParent(barBGRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            float inset = frameSprite != null ? 1f : 3f;
            fillRect.offsetMin = new Vector2(inset, inset);
            fillRect.offsetMax = new Vector2(-inset, -inset);
            fill = fillGO.GetComponent<Image>();
            fill.UseAsFilled();
            fill.color = new Color(0.8f, 0.1f, 0.15f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            // 깎인 체력이 밝은 바로 잠깐 남았다가 따라 줄어든다.
            trail = HealthBarTrail.Attach(fill);

            var hpTextGO = new GameObject("HP_Text", typeof(RectTransform), typeof(Text));
            var hpTextRect = (RectTransform)hpTextGO.transform;
            hpTextRect.SetParent(barBGRect, false);
            hpTextRect.anchorMin = Vector2.zero;
            hpTextRect.anchorMax = Vector2.one;
            hpTextRect.offsetMin = Vector2.zero;
            hpTextRect.offsetMax = Vector2.zero;
            hpLabel = hpTextGO.GetComponent<Text>();
            hpLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hpLabel.fontSize = frameSprite != null ? 13 : 16;
            AddOutline(hpLabel);
            hpLabel.fontStyle = FontStyle.Bold;
            hpLabel.alignment = TextAnchor.MiddleCenter;
            hpLabel.color = Color.white;
        }

        private static void AddOutline(Text text)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
        }
    }
}

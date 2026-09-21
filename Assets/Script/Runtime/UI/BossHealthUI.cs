using UnityEngine;
using UnityEngine.UI;
using Aethoria.Monsters;

namespace Aethoria.UI
{
    // 보스 전용 대형 체력바. 화면 상단 중앙에 이름과 함께 크게 표시하고,
    // 보스가 없거나 죽으면 숨긴다. 몬스터 발밑의 작은 MonsterHealthBar와는 별개로 같이 붙는다.
    public class BossHealthUI : MonoBehaviour
    {
        private GameObject panel;
        private Image fill;
        private Text nameLabel;
        private Text hpLabel;
        private Monster boss;

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
            panel.SetActive(true);
            RefreshBar();
        }

        private void HandleBossDied(Monster _)
        {
            Hide();
        }

        private void Hide()
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
        }

        private void BuildPanel()
        {
            panel = new GameObject("BossHealthPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -16f);
            rect.sizeDelta = new Vector2(640f, 70f);

            var bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bg.transform;
            bgRect.SetParent(rect, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

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

            var barBG = new GameObject("Bar_BG", typeof(RectTransform), typeof(Image));
            var barBGRect = (RectTransform)barBG.transform;
            barBGRect.SetParent(rect, false);
            barBGRect.anchorMin = new Vector2(0f, 0f);
            barBGRect.anchorMax = new Vector2(1f, 0f);
            barBGRect.pivot = new Vector2(0.5f, 0f);
            barBGRect.anchoredPosition = new Vector2(0f, 8f);
            barBGRect.sizeDelta = new Vector2(-20f, 32f);
            barBG.GetComponent<Image>().color = new Color(0.15f, 0.05f, 0.05f, 0.9f);

            var fillGO = new GameObject("Bar_Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGO.transform;
            fillRect.SetParent(barBGRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            fill = fillGO.GetComponent<Image>();
            fill.UseAsFilled();
            fill.color = new Color(0.8f, 0.1f, 0.15f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            var hpTextGO = new GameObject("HP_Text", typeof(RectTransform), typeof(Text));
            var hpTextRect = (RectTransform)hpTextGO.transform;
            hpTextRect.SetParent(barBGRect, false);
            hpTextRect.anchorMin = Vector2.zero;
            hpTextRect.anchorMax = Vector2.one;
            hpTextRect.offsetMin = Vector2.zero;
            hpTextRect.offsetMax = Vector2.zero;
            hpLabel = hpTextGO.GetComponent<Text>();
            hpLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hpLabel.fontSize = 16;
            hpLabel.fontStyle = FontStyle.Bold;
            hpLabel.alignment = TextAnchor.MiddleCenter;
            hpLabel.color = Color.white;
        }
    }
}

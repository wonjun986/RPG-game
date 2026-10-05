using UnityEngine;
using UnityEngine.UI;
using Aethoria.Quests;

namespace Aethoria.UI
{
    // 화면 오른쪽 위, 메인 퀘스트 추적창(QuestTrackerUI) 바로 아래에 오늘의 일일 의뢰 진행 상황을 보여준다.
    // 수락 전이거나 오늘 치를 이미 끝냈으면 숨긴다.
    public class DailyQuestTrackerUI : MonoBehaviour
    {
        private const float TopOffset = -88f; // QuestTrackerUI(세로 64, -16 위치) 바로 아래

        private DailyQuestManager quests;
        private GameObject panel;
        private Text titleText;
        private Text progressText;

        public void Initialize(DailyQuestManager dailyQuestManager)
        {
            quests = dailyQuestManager;
            BuildPanel();
            quests.OnChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (quests != null) quests.OnChanged -= Refresh;
        }

        private void BuildPanel()
        {
            panel = new GameObject("DailyQuestTracker", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-16f, TopOffset);
            rect.sizeDelta = new Vector2(300f, 64f);
            var bg = panel.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.04f, 0.09f, 0.75f);
            bg.raycastTarget = false;

            titleText = BuildText(rect, "Title", 18, new Color(0.4f, 0.85f, 0.95f), new Vector2(0f, 0.5f), new Vector2(1f, 1f));
            progressText = BuildText(rect, "Progress", 17, Color.white, new Vector2(0f, 0f), new Vector2(1f, 0.5f));
        }

        private static Text BuildText(Transform parent, string name, int fontSize, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(12f, 0f);
            rect.offsetMax = new Vector2(-12f, 0f);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private void Refresh()
        {
            var quest = quests.Current;
            bool visible = quest != null
                && (quests.Status == DailyQuestStatus.InProgress || quests.Status == DailyQuestStatus.ReadyToTurnIn);
            panel.SetActive(visible);
            if (!visible) return;

            titleText.text = $"[일일 의뢰] {quest.title}";
            progressText.text = quests.Status == DailyQuestStatus.ReadyToTurnIn
                ? "완료! 마을의 노아에게 보고하세요"
                : $"{quest.targetMonsterName} 처치  {quests.Progress} / {quest.requiredCount}";
        }
    }
}

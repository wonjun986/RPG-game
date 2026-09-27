using UnityEngine;
using UnityEngine.UI;
using Aethoria.Quests;

namespace Aethoria.UI
{
    // 화면 오른쪽 위에 진행 중인 퀘스트의 목표와 처치 수를 보여준다.
    // 퀘스트를 받기 전이거나 모두 끝냈으면 숨긴다.
    public class QuestTrackerUI : MonoBehaviour
    {
        private QuestManager quests;
        private GameObject panel;
        private Text titleText;
        private Text progressText;

        public void Initialize(QuestManager questManager)
        {
            quests = questManager;
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
            panel = new GameObject("QuestTracker", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-16f, -16f);
            rect.sizeDelta = new Vector2(300f, 64f);
            var bg = panel.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.04f, 0.09f, 0.75f);
            bg.raycastTarget = false;

            titleText = BuildText(rect, "Title", 18, new Color(0.95f, 0.85f, 0.2f), new Vector2(0f, 0.5f), new Vector2(1f, 1f));
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
                && (quests.Status == QuestStatus.InProgress || quests.Status == QuestStatus.ReadyToTurnIn);
            panel.SetActive(visible);
            if (!visible) return;

            titleText.text = $"[퀘스트] {quest.title}";
            progressText.text = quests.Status == QuestStatus.ReadyToTurnIn
                ? "완료! 마을의 노아에게 보고하세요"
                : $"{quest.targetMonsterName} 처치  {quests.Progress} / {quest.requiredCount}";
        }
    }
}

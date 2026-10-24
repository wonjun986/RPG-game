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
            rect.sizeDelta = new Vector2(380f, 64f);
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
            text.resizeTextForBestFit = true; // 장소까지 붙어 길어진 줄은 칸에 맞춰 줄인다
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private void Refresh()
        {
            // 받을 퀘스트가 있을 때도 띄워서, 다음 이야기를 누구에게 들으러 가야 하는지 늘 보이게 한다.
            var quest = quests.Current;
            bool visible = quest != null && quests.Status != QuestStatus.AllDone;
            panel.SetActive(visible);
            if (!visible) return;

            string chapter = string.IsNullOrEmpty(quest.chapter) ? "퀘스트" : quest.chapter;
            switch (quests.Status)
            {
                case QuestStatus.Available:
                    titleText.text = $"[{chapter}] 다음 이야기";
                    progressText.text = $"{quest.giverNpc}에게 말을 걸어 보자";
                    break;
                case QuestStatus.ReadyToTurnIn:
                    titleText.text = $"[{chapter}] {quest.title}";
                    progressText.text = $"완료! {quest.turnInNpc}에게 보고하자";
                    break;
                default:
                    titleText.text = $"[{chapter}] {quest.title}";
                    progressText.text = quest.type == QuestType.Talk
                        ? $"{quest.targetNpc} 찾아가기 · {quest.location}"
                        : $"{quest.targetMonsterName} {quests.Progress}/{quest.requiredCount} · {quest.location}";
                    break;
            }
        }
    }
}

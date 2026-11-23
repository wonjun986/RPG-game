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
            rect.sizeDelta = new Vector2(380f, 64f);
            var bg = panel.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.04f, 0.09f, 0.75f);
            bg.raycastTarget = false;
            // 퀘스트 판 그림(UI/HUD/QuestPlate — 왼쪽 두루마리, 좌우 9-slice)이 있으면 그 위에 글자를 얹는다.
            var plate = Resources.Load<Sprite>("UI/HUD/QuestPlate");
            if (plate != null)
            {
                bg.sprite = plate;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }

            titleText = BuildText(rect, "Title", 18, new Color(0.4f, 0.85f, 0.95f), new Vector2(0f, 0.5f), new Vector2(1f, 1f));
            progressText = BuildText(rect, "Progress", 17, Color.white, new Vector2(0f, 0f), new Vector2(1f, 0.5f));
            if (plate != null)
            {
                // 글자는 두루마리 오른쪽, 판 안쪽 띠(위 23%~아래 15%) 안에 두 줄로.
                FitIntoPlate(titleText, 0.47f, 0.80f);
                FitIntoPlate(progressText, 0.15f, 0.48f);
            }
        }

        private static void FitIntoPlate(Text text, float yMin, float yMax)
        {
            var r = text.rectTransform;
            r.anchorMin = new Vector2(0f, yMin);
            r.anchorMax = new Vector2(1f, yMax);
            r.offsetMin = new Vector2(64f, 0f);
            r.offsetMax = new Vector2(-44f, 0f);
            text.fontSize = Mathf.Min(text.fontSize, 16);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 11;
            text.resizeTextMaxSize = text.fontSize;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);
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

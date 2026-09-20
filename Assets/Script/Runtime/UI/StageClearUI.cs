using UnityEngine;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // 마지막 스테이지의 보스를 잡으면 화면 전체를 덮는 패널과 "스테이지 클리어" 문구를 띄우고 게임을 정지시킨다.
    public class StageClearUI : MonoBehaviour
    {
        private GameObject panel;

        public void Initialize()
        {
            BuildPanel();
        }

        private void BuildPanel()
        {
            panel = new GameObject("StageClearPanel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var textGO = new GameObject("StageClearText", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(600f, 120f);

            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.95f, 0.85f, 0.2f);
            text.text = "스테이지 클리어!";

            panel.SetActive(false);
        }

        public void Show()
        {
            panel.SetActive(true);
            Time.timeScale = 0f;
        }
    }
}

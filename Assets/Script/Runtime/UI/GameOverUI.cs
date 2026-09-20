using UnityEngine;
using UnityEngine.UI;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 플레이어가 죽으면 화면 전체를 덮는 반투명 패널과 "게임 오버" 문구를 띄우고 게임을 정지시킨다.
    public class GameOverUI : MonoBehaviour
    {
        private Character target;
        private GameObject panel;

        public void Initialize(Character character)
        {
            target = character;
            BuildPanel();
            target.OnDied += HandlePlayerDied;
        }

        private void OnDestroy()
        {
            if (target != null) target.OnDied -= HandlePlayerDied;
        }

        private void BuildPanel()
        {
            panel = new GameObject("GameOverPanel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var textGO = new GameObject("GameOverText", typeof(RectTransform), typeof(Text));
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
            text.color = new Color(0.9f, 0.1f, 0.1f);
            text.text = "게임 오버";

            panel.SetActive(false);
        }

        private void HandlePlayerDied(Character character)
        {
            panel.SetActive(true);
            Time.timeScale = 0f;
        }
    }
}

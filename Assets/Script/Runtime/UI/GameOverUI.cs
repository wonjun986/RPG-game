using UnityEngine;
using UnityEngine.UI;
using Aethoria.Bootstrap;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 플레이어가 죽으면 GameOver 배경 이미지(UI/GameOver.png)를 띄우고 게임을 정지시킨다.
    // RETRY는 같은 판을 처음부터 다시 시작하고, TITLE은 타이틀 화면으로 돌아간다.
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
            TitleScreenUI.EnsureEventSystem();

            panel = new GameObject("GameOverPanel", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/GameOver");

            // 아래 앵커 좌표는 GameOver.png 안에 이미 그려진 "RETRY"/"TITLE" 버튼 위치에 맞춘 값이다.
            TitleScreenUI.BuildButton(panel.transform, "RetryButton", new Vector2(0.63f, 0.456f), new Vector2(0.94f, 0.506f), GameBootstrap.RestartGame);
            TitleScreenUI.BuildButton(panel.transform, "TitleButton", new Vector2(0.63f, 0.370f), new Vector2(0.94f, 0.420f), GameBootstrap.ReturnToTitle);

            panel.SetActive(false);
        }

        private void HandlePlayerDied(Character character)
        {
            panel.SetActive(true);
            Time.timeScale = 0f;
        }
    }
}

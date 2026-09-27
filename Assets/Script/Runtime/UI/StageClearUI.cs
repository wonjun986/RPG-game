using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Aethoria.Bootstrap;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 던전(어둠의 숲 보스 기사, 광활한 평야 보스 슬라임)의 보스를 잡으면 화면 위쪽에
    // "보스 처치! / N초 후 마을로 이동합니다..." 를 띄우고, 3초 뒤 마을의 노아 앞으로 이동시킨다.
    // 기다리는 동안에도 게임은 멈추지 않는다(남은 몬스터 처리, 쓰러지는 모습 감상 등).
    public class StageClearUI : MonoBehaviour
    {
        private const int ReturnDelaySeconds = 3;

        private Character player;
        private GameObject panel;
        private Text messageText;
        private Coroutine returnRoutine;

        public void Initialize(Character playerCharacter)
        {
            player = playerCharacter;
            BuildPanel();
        }

        private void BuildPanel()
        {
            panel = new GameObject("StageClearBanner", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.72f);
            rect.anchorMax = new Vector2(0.5f, 0.72f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(620f, 130f);
            var background = panel.GetComponent<Image>();
            background.color = new Color(0.06f, 0.04f, 0.09f, 0.85f);
            background.raycastTarget = false;

            BuildLabel(rect, "Title", "보스 처치!", 40, new Color(0.95f, 0.85f, 0.2f),
                new Vector2(0f, 0.5f), new Vector2(1f, 0.95f));
            messageText = BuildLabel(rect, "Message", string.Empty, 26, Color.white,
                new Vector2(0f, 0.08f), new Vector2(1f, 0.5f));

            panel.SetActive(false);
        }

        private static Text BuildLabel(Transform parent, string name, string content, int fontSize, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var textGO = new GameObject(name, typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(parent, false);
            textRect.anchorMin = anchorMin;
            textRect.anchorMax = anchorMax;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        // 보스 몬스터의 OnDied에 연결된다.
        public void Show()
        {
            if (returnRoutine != null) StopCoroutine(returnRoutine);
            returnRoutine = StartCoroutine(ReturnAfterCountdown());
        }

        // 스테이지가 새로 지어질 때 호출한다. 카운트다운 중에 포탈로 나가버렸거나 판을 새로 시작했으면
        // 늦게 마을로 끌려가지 않도록 대기를 취소하고 문구도 숨긴다.
        public void Cancel()
        {
            if (returnRoutine != null)
            {
                StopCoroutine(returnRoutine);
                returnRoutine = null;
            }
            panel.SetActive(false);
        }

        private IEnumerator ReturnAfterCountdown()
        {
            panel.transform.SetAsLastSibling(); // 나중에 만들어진 HUD 요소들보다 위에 그린다
            panel.SetActive(true);

            for (int remaining = ReturnDelaySeconds; remaining > 0; remaining--)
            {
                messageText.text = $"{remaining}초 후 마을로 이동합니다...";
                yield return new WaitForSeconds(1f);

                // 대기 중에 플레이어가 쓰러졌다면 사망 연출(DeathReturnUI)이 우선이다.
                if (player == null || player.IsDead)
                {
                    returnRoutine = null;
                    panel.SetActive(false);
                    yield break;
                }
            }

            returnRoutine = null;
            panel.SetActive(false);
            GameBootstrap.ReturnToVillage(spawnAtNoa: true);
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Aethoria.Bootstrap;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 플레이어가 쓰러지면 게임오버 화면 대신, 화면을 어둡게 하며 안내 문구를 잠깐 보여준 뒤
    // 체력/마나를 채워 마을로 돌려보낸다. 레벨/경험치/퀘스트 진행은 그대로 유지된다(패널티 없음).
    public class DeathReturnUI : MonoBehaviour
    {
        private const float FadeInDuration = 0.6f;
        private const float HoldDuration = 1.4f;

        private Character target;
        private CharacterMovement2D movement;
        private GameObject panel;
        private Image dim;
        private Text message;
        private Coroutine routine;

        public void Initialize(Character character)
        {
            target = character;
            movement = character.GetComponent<CharacterMovement2D>();
            BuildPanel();
            target.OnDied += HandlePlayerDied;
        }

        private void OnDestroy()
        {
            if (target != null) target.OnDied -= HandlePlayerDied;
        }

        private void BuildPanel()
        {
            panel = new GameObject("DeathReturnPanel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            dim = panel.GetComponent<Image>();
            dim.raycastTarget = true; // 연출 중 HUD 클릭 차단

            var textGO = new GameObject("Message", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = new Vector2(0f, 0.4f);
            textRect.anchorMax = new Vector2(1f, 0.6f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            message = textGO.GetComponent<Text>();
            message.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            message.fontSize = 34;
            message.fontStyle = FontStyle.Bold;
            message.alignment = TextAnchor.MiddleCenter;
            message.raycastTarget = false;
            message.text = "쓰러졌습니다... 마을로 돌아갑니다";

            panel.SetActive(false);
        }

        private void HandlePlayerDied(Character character)
        {
            if (routine != null) return;
            routine = StartCoroutine(ReturnRoutine());
        }

        private IEnumerator ReturnRoutine()
        {
            if (movement != null) movement.SetInputLocked(true);
            var body = target.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;

            panel.transform.SetAsLastSibling();
            panel.SetActive(true);

            // 보스 처치 팝업 등으로 timeScale이 0일 수도 있어서 실시간으로 잰다.
            float t = 0f;
            while (t < FadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Clamp01(t / FadeInDuration));
                yield return null;
            }
            SetAlpha(1f);
            yield return new WaitForSecondsRealtime(HoldDuration);

            Time.timeScale = 1f;
            GameBootstrap.ReturnToVillage(); // 체력/마나 회복 + 마을 재구성 + 자동 저장

            panel.SetActive(false);
            if (movement != null) movement.SetInputLocked(false);
            routine = null;
        }

        private void SetAlpha(float alpha)
        {
            dim.color = new Color(0f, 0f, 0f, 0.85f * alpha);
            message.color = new Color(0.9f, 0.85f, 0.95f, alpha);
        }
    }
}

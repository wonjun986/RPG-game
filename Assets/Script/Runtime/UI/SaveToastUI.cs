using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // 자동 저장이 될 때마다 화면 오른쪽 아래에 "저장됨"을 잠깐 띄웠다가 서서히 지운다.
    public class SaveToastUI : MonoBehaviour
    {
        private const float HoldDuration = 1.2f;
        private const float FadeDuration = 0.5f;

        private Text label;
        private Coroutine routine;

        public void Initialize()
        {
            var go = new GameObject("SaveToast", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-20f, 16f);
            rect.sizeDelta = new Vector2(200f, 30f);

            label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.LowerRight;
            label.color = new Color(0.95f, 0.85f, 0.2f, 0f);
            label.raycastTarget = false;
            label.text = "저장됨";
        }

        public void Show()
        {
            if (label == null || !isActiveAndEnabled) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(ShowRoutine());
        }

        // 게임오버/팝업 등으로 timeScale이 0이어도 사라지도록 실시간으로 잰다.
        private IEnumerator ShowRoutine()
        {
            SetAlpha(1f);
            yield return new WaitForSecondsRealtime(HoldDuration);

            float t = 0f;
            while (t < FadeDuration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(1f - t / FadeDuration);
                yield return null;
            }
            SetAlpha(0f);
            routine = null;
        }

        private void SetAlpha(float alpha)
        {
            var c = label.color;
            c.a = alpha;
            label.color = c;
        }
    }
}

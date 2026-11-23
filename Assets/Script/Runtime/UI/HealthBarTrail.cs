using UnityEngine;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // 체력바 잔상: 채우기 바(Filled Image) 바로 뒤에 밝은 바를 하나 더 깔아 두고, 체력이 깎이면 깎인 만큼이 잠깐 남아 있다가
    // 천천히 따라 줄어든다. 회복하면 바로 따라 올라간다. 원래 바를 갱신하는 코드는 그대로 두고 fillAmount만 지켜본다.
    public class HealthBarTrail : MonoBehaviour
    {
        private const float HoldTime = 0.45f;
        private const float CatchUpSpeed = 0.9f; // 초당 줄어드는 비율(바 전체 = 1)

        private Image source;
        private Image trail;
        private float shown = -1f;
        private float holdTimer;

        public static HealthBarTrail Attach(Image fill, Color? color = null)
        {
            if (fill == null) return null;
            var go = new GameObject(fill.name + "_Trail", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            var src = fill.rectTransform;
            rect.SetParent(src.parent, false);
            rect.SetSiblingIndex(src.GetSiblingIndex()); // 원래 바 바로 뒤(먼저 그려짐)
            rect.anchorMin = src.anchorMin;
            rect.anchorMax = src.anchorMax;
            rect.pivot = src.pivot;
            rect.offsetMin = src.offsetMin;
            rect.offsetMax = src.offsetMax;

            var image = go.GetComponent<Image>();
            image.UseAsFilled();
            image.type = Image.Type.Filled;
            image.fillMethod = fill.fillMethod;
            image.fillOrigin = fill.fillOrigin;
            image.fillAmount = fill.fillAmount;
            image.raycastTarget = false;
            image.color = color ?? new Color(1f, 0.92f, 0.75f, 0.9f);

            var trail = go.AddComponent<HealthBarTrail>();
            trail.source = fill;
            trail.trail = image;
            return trail;
        }

        private void LateUpdate()
        {
            if (source == null) { Destroy(gameObject); return; }
            float target = source.fillAmount;
            if (shown < 0f || target >= shown)
            {
                shown = target;
                holdTimer = HoldTime;
            }
            else if (holdTimer > 0f)
            {
                holdTimer -= Time.unscaledDeltaTime;
            }
            else
            {
                shown = Mathf.MoveTowards(shown, target, CatchUpSpeed * Time.unscaledDeltaTime);
            }
            trail.fillAmount = shown;
        }

        // 새 보스에 연결할 때처럼 잔상 없이 바로 맞춘다.
        public void Snap()
        {
            shown = -1f;
        }
    }
}

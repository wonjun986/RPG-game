using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // 자동 저장이 될 때마다 화면 오른쪽 아래에 "저장됨"을 잠깐 띄웠다가 서서히 지운다.
    // 알림 판(UI/HUD/ToastPlate)과 저장 아이콘(UI/HUD/SaveIcon)이 있으면 같이 띄운다.
    public class SaveToastUI : MonoBehaviour
    {
        private const float HoldDuration = 1.2f;
        private const float FadeDuration = 0.5f;

        private Text label;
        private CanvasGroup group;
        private Coroutine routine;

        public void Initialize()
        {
            var root = new GameObject("SaveToast", typeof(RectTransform), typeof(CanvasGroup));
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(transform, false);
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-16f, 12f);
            rootRect.sizeDelta = new Vector2(170f, 34f);
            group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            var plateSprite = Resources.Load<Sprite>("UI/HUD/ToastPlate");
            var iconSprite = Resources.Load<Sprite>("UI/HUD/SaveIcon");
            if (plateSprite != null)
            {
                var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
                var plateRect = (RectTransform)plate.transform;
                plateRect.SetParent(rootRect, false);
                plateRect.anchorMin = Vector2.zero;
                plateRect.anchorMax = Vector2.one;
                plateRect.offsetMin = new Vector2(-10f, -3f);
                plateRect.offsetMax = new Vector2(6f, 5f);
                var plateImage = plate.GetComponent<Image>();
                plateImage.sprite = plateSprite;
                plateImage.raycastTarget = false;
            }
            if (iconSprite != null)
            {
                var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                var iconRect = (RectTransform)iconGO.transform;
                iconRect.SetParent(rootRect, false);
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(22f, 0f);
                iconRect.sizeDelta = new Vector2(28f, 28f);
                var iconImage = iconGO.GetComponent<Image>();
                iconImage.sprite = iconSprite;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(rootRect, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(iconSprite != null ? 56f : 0f, 0f);
            rect.offsetMax = new Vector2(-18f, 0f);

            label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.fontStyle = FontStyle.Bold;
            label.alignment = plateSprite != null ? TextAnchor.MiddleCenter : TextAnchor.LowerRight;
            label.color = new Color(0.95f, 0.85f, 0.2f, 1f);
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
            group.alpha = alpha;
        }
    }
}

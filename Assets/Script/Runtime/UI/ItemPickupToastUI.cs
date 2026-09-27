using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Data;

namespace Aethoria.UI
{
    // 아이템을 얻으면(몬스터 드랍 등) 화면 위쪽에 아이콘과 "OOO 획득!"을 잠깐 띄웠다가 사라진다.
    public class ItemPickupToastUI : MonoBehaviour
    {
        private const float HoldDuration = 1.6f;
        private const float FadeDuration = 0.5f;

        private Inventory inventory;
        private Image icon;
        private Text label;
        private Coroutine routine;

        public void Initialize(Inventory targetInventory)
        {
            inventory = targetInventory;

            var root = new GameObject("ItemPickupToast", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(transform, false);
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -120f);
            rootRect.sizeDelta = new Vector2(340f, 50f);

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)iconGO.transform;
            iconRect.SetParent(rootRect, false);
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(44f, 44f);
            icon = iconGO.GetComponent<Image>();
            icon.preserveAspect = true;

            var textGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(rootRect, false);
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(52f, 0f);
            textRect.offsetMax = Vector2.zero;
            label = textGO.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(1f, 1f, 1f, 0f);

            SetAlpha(0f);

            if (inventory != null) inventory.OnItemAdded += HandleItemAdded;
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.OnItemAdded -= HandleItemAdded;
        }

        private void HandleItemAdded(ItemData item, int count)
        {
            if (item == null) return;

            icon.sprite = Resources.Load<Sprite>(item.iconPath);
            label.text = count > 1 ? $"{item.itemName} +{count} 획득!" : $"{item.itemName} 획득!";

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(ShowRoutine());
        }

        // 보스 처치 직후 타임스케일이 그대로 흐르는 상황이지만, 혹시 모를 정지 연출과 겹쳐도
        // 사라지도록 실시간으로 잰다(SaveToastUI와 동일한 방식).
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
            var labelColor = label.color;
            labelColor.a = alpha;
            label.color = labelColor;

            var iconColor = icon.color;
            iconColor.a = alpha;
            icon.color = iconColor;
        }
    }
}

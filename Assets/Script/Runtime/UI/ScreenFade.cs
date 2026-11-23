using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // 방을 넘어갈 때 화면을 잠깐 검게 덮었다가 걷어 낸다. 어두워진 순간(midpoint)에 맵을 다시 짓는다.
    // 시간이 멈춘 창과 겹쳐도 돌도록 unscaled 시간으로 잰다. 이미 전환 중이면 새 전환은 무시한다(출구 두 개를 동시에 밟는 경우).
    public class ScreenFade : MonoBehaviour
    {
        public const float DefaultOut = 0.16f;
        public const float DefaultHold = 0.05f;
        public const float DefaultIn = 0.3f;

        private static ScreenFade instance;
        private Image image;
        private bool busy;

        public static bool Busy => instance != null && instance.busy;

        public static void Run(Action midpoint, float outTime = DefaultOut, float holdTime = DefaultHold, float inTime = DefaultIn)
        {
            if (instance == null) Create();
            if (instance.busy) return;
            instance.StartCoroutine(instance.Routine(midpoint, outTime, holdTime, inTime));
        }

        private static void Create()
        {
            var go = new GameObject("ScreenFade", typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900; // HUD·대화창보다 위
            var imageGO = new GameObject("Black", typeof(RectTransform), typeof(Image));
            imageGO.transform.SetParent(go.transform, false);
            var rect = imageGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            instance = go.AddComponent<ScreenFade>();
            instance.image = imageGO.GetComponent<Image>();
            instance.image.raycastTarget = false;
            instance.SetAlpha(0f);
        }

        private void SetAlpha(float a)
        {
            image.color = new Color(0f, 0f, 0f, a);
            image.enabled = a > 0.001f;
        }

        private IEnumerator Routine(Action midpoint, float outTime, float holdTime, float inTime)
        {
            busy = true;
            for (float t = 0f; t < outTime; t += Time.unscaledDeltaTime)
            {
                SetAlpha(t / outTime);
                yield return null;
            }
            SetAlpha(1f);
            try { midpoint?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            // 새 맵에서 카메라가 자리를 잡을 때까지 한두 프레임 검은 채로 둔다.
            yield return null;
            for (float t = 0f; t < holdTime; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < inTime; t += Time.unscaledDeltaTime)
            {
                SetAlpha(1f - t / inTime);
                yield return null;
            }
            SetAlpha(0f);
            busy = false;
        }
    }
}

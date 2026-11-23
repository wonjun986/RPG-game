using System.Collections;
using UnityEngine;

namespace Aethoria.UI
{
    // 메뉴 창(장비창 등)을 여닫는 연출. 창이 바로 뜨지 않고
    //  - 열 때: 아래에서 스르륵 올라오며 흐릿하게 나타나고, 가운데에서 위아래로 펼쳐지며 살짝 튕기듯 자리를 잡는다.
    //  - 닫을 때: 살짝 내려가며 접히듯 흐려지고, 다 사라진 뒤 꺼진다.
    // 창이 열려 있는 동안 시간이 멈추므로(Time.timeScale = 0) 실제 시간으로 움직인다. 창 루트(RectTransform)에 붙인다.
    [RequireComponent(typeof(RectTransform))]
    public class PanelTransition : MonoBehaviour
    {
        private const float OpenDuration = 0.34f;
        private const float CloseDuration = 0.16f;
        private const float RiseDistance = 70f;     // 열 때 이만큼 아래에서 올라온다(캔버스 단위)
        private const float StartScaleY = 0.55f;    // 이만큼 접힌 채로 시작해 펼쳐진다
        private const float StartScaleX = 0.94f;

        private RectTransform rect;
        private CanvasGroup group;
        private Vector2 restPosition;
        private Coroutine routine;

        // 닫히는 중(아직 화면엔 보이지만 입력은 받지 않아야 할 때).
        public bool IsClosing { get; private set; }

        // 여닫을 때 메뉴 효과음(UiSfx.Open/Close)을 낼지.
        public bool PlaySounds = true;

        private void Awake() => Init();

        private void Init()
        {
            if (rect != null) return;
            rect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            restPosition = rect.anchoredPosition;
        }

        public void PlayOpen()
        {
            Init();
            IsClosing = false;
            if (!gameObject.activeSelf && PlaySounds) UiSfx.Open();
            gameObject.SetActive(true);
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Open());
        }

        // 닫는 연출이 끝나면 창을 끄고 onClosed를 부른다.
        public void PlayClose(System.Action onClosed = null)
        {
            Init();
            if (!gameObject.activeInHierarchy)
            {
                onClosed?.Invoke();
                return;
            }
            if (!IsClosing && PlaySounds) UiSfx.Close();
            IsClosing = true;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Close(onClosed));
        }

        private IEnumerator Open()
        {
            group.blocksRaycasts = true;
            for (float t = 0f; t < OpenDuration; t += Time.unscaledDeltaTime)
            {
                Apply(t);
                yield return null;
            }
            Apply(OpenDuration);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = restPosition;
            group.alpha = 1f;
            routine = null;
        }

        // 올라오기(앞 65%)와 펼치기(뒤쪽, 조금 늦게 시작해 살짝 넘쳤다 돌아옴)를 겹쳐서 한 동작처럼 보이게 한다.
        private void Apply(float t)
        {
            float rise = EaseOutCubic(Mathf.Clamp01(t / (OpenDuration * 0.65f)));
            float unfold = EaseOutBack(Mathf.Clamp01((t - OpenDuration * 0.2f) / (OpenDuration * 0.8f)));
            rect.anchoredPosition = restPosition + new Vector2(0f, -RiseDistance * (1f - rise));
            rect.localScale = new Vector3(Mathf.LerpUnclamped(StartScaleX, 1f, EaseOutCubic(Mathf.Clamp01(unfold))),
                                          Mathf.LerpUnclamped(StartScaleY, 1f, unfold), 1f);
            group.alpha = Mathf.Clamp01(rise * 1.3f);
        }

        private IEnumerator Close(System.Action onClosed)
        {
            group.blocksRaycasts = false;
            Vector2 from = rect.anchoredPosition;
            Vector3 fromScale = rect.localScale;
            float fromAlpha = group.alpha;
            for (float t = 0f; t < CloseDuration; t += Time.unscaledDeltaTime)
            {
                float k = EaseInCubic(t / CloseDuration);
                rect.anchoredPosition = Vector2.Lerp(from, restPosition + new Vector2(0f, -RiseDistance * 0.5f), k);
                rect.localScale = Vector3.Lerp(fromScale, new Vector3(0.97f, 0.88f, 1f), k);
                group.alpha = Mathf.Lerp(fromAlpha, 0f, k);
                yield return null;
            }
            gameObject.SetActive(false);
            rect.anchoredPosition = restPosition;
            rect.localScale = Vector3.one;
            group.alpha = 1f;
            group.blocksRaycasts = true;
            IsClosing = false;
            routine = null;
            onClosed?.Invoke();
        }

        private static float EaseOutCubic(float x) { x = 1f - x; return 1f - x * x * x; }
        private static float EaseInCubic(float x) => x * x * x;
        private static float EaseOutBack(float x)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            float m = x - 1f;
            return 1f + c3 * m * m * m + c1 * m * m;
        }
    }
}

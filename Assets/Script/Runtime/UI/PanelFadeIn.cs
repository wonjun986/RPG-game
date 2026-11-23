using UnityEngine;

namespace Aethoria.UI
{
    // 화면 전체를 덮는 창(스탯·강화·축복·도감·기술 배우기·저장)이 켜질 때의 공통 연출:
    // 살짝 크게(1.04배) 시작해 제자리로 줄어들며 흐릿하게 나타나고, 창 여는 소리(UiSfx.Open)를 낸다.
    // 크게 시작하므로 화면 가장자리가 비어 보이지 않는다. 창이 열려 있는 동안 시간이 멈춰 있어서 실제 시간으로 움직인다.
    // 아래에서 올라오는 PanelTransition은 가운데 떠 있는 창용이고, 이건 화면을 꽉 채우는 창용이다.
    [RequireComponent(typeof(RectTransform))]
    public class PanelFadeIn : MonoBehaviour
    {
        private const float Duration = 0.2f;
        private const float StartScale = 1.04f;

        private CanvasGroup group;
        private int createdFrame;
        private float startTime = -1f;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            createdFrame = Time.frameCount;
        }

        private void OnEnable()
        {
            if (group == null) return;
            if (Time.frameCount == createdFrame) return; // 처음 만들 때(곧 꺼짐)는 연출·소리 없음
            startTime = Time.unscaledTime;
            UiSfx.Open();
            Apply(0f);
        }

        private void Update()
        {
            if (startTime < 0f) return;
            float k = Mathf.Clamp01((Time.unscaledTime - startTime) / Duration);
            Apply(k);
            if (k >= 1f) startTime = -1f;
        }

        private void Apply(float k)
        {
            float e = 1f - (1f - k) * (1f - k) * (1f - k);
            transform.localScale = Vector3.one * Mathf.Lerp(StartScale, 1f, e);
            group.alpha = Mathf.Clamp01(k * 1.6f);
        }

        private void OnDisable()
        {
            startTime = -1f;
            transform.localScale = Vector3.one;
            if (group != null) group.alpha = 1f;
        }
    }
}

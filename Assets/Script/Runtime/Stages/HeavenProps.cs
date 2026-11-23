using UnityEngine;

namespace Aethoria.Stages
{
    // 천상의 신전 소품에 붙이는 작은 움직임들.

    // 제자리에서 위아래로 천천히 떠오르내린다(제단 위에 떠 있는 수정).
    public class FloatingBob : MonoBehaviour
    {
        public float Amplitude = 0.08f; // 칸
        public float Period = 3f;       // 초

        private Vector3 basePosition;
        private float phase;

        private void Start()
        {
            basePosition = transform.localPosition;
            phase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            float y = Amplitude * Mathf.Sin(Time.time * Mathf.PI * 2f / Period + phase);
            transform.localPosition = basePosition + new Vector3(0f, y, 0f);
        }
    }

    // 위에서 내리는 빛줄기처럼 그림 맨 윗줄이 가장 밝은 소품을 화면 위끝에 매단다(가로 위치는 월드에 그대로).
    // 바닥 기준으로 세워 두면 카메라가 위로 따라 올라갈 때 그림 윗변이 화면 안으로 들어와 빛이 뚝 잘려 보였다.
    // 아래 가운데 기준 스프라이트를 가정하고, 윗변을 화면 위끝보다 Overscan만큼 더 올린다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class ScreenTopAnchor : MonoBehaviour
    {
        public float Overscan = 0.3f;

        private SpriteRenderer spriteRenderer;
        private Camera cam;

        private void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || spriteRenderer.sprite == null) return;
            float top = cam.transform.position.y + cam.orthographicSize + Overscan;
            float height = spriteRenderer.bounds.size.y;
            var p = transform.position;
            transform.position = new Vector3(p.x, top - height, p.z);
        }
    }

    // 투명도를 천천히 오르내린다(성소 위로 내리는 빛줄기).
    [RequireComponent(typeof(SpriteRenderer))]
    public class AlphaPulse : MonoBehaviour
    {
        public float MinAlpha = 0.35f;
        public float MaxAlpha = 0.6f;
        public float Period = 6f;

        private SpriteRenderer spriteRenderer;

        private void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

        private void Update()
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / Period);
            var c = spriteRenderer.color;
            c.a = Mathf.Lerp(MinAlpha, MaxAlpha, t);
            spriteRenderer.color = c;
        }
    }
}

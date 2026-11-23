using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Aethoria.Fx;

namespace Aethoria.Stages
{
    // 숨겨진 포탈: 멀리서는 보이지 않고, 가끔 반짝이는 빛 한 점만 남아 "저기 뭔가 있다"는 걸 알려 준다.
    // 네크로시아가 가까이(RevealFar 안) 다가가면 서서히 드러나고 RevealNear 안에서는 완전히 보인다.
    public class HiddenPortalReveal : MonoBehaviour
    {
        private const float RevealNear = 2.5f;
        private const float RevealFar = 5.5f;
        private static readonly Color HintColor = new Color(0.85f, 0.7f, 1f);

        private Transform player;
        private readonly List<SpriteRenderer> renderers = new();
        private readonly List<Color> baseColors = new();
        private readonly List<Light2D> lights = new();
        private readonly List<float> baseIntensities = new();
        private SpriteRenderer hint;
        private float currentAlpha = -1f;
        private float hintTimer;

        public void Initialize(Transform playerTransform, Color tint)
        {
            player = playerTransform;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.color = sr.color * tint;
                renderers.Add(sr);
                baseColors.Add(sr.color);
            }
            foreach (var light in GetComponentsInChildren<Light2D>(true))
            {
                lights.Add(light);
                baseIntensities.Add(light.intensity);
            }

            // 멀리서도 보이는 희미한 빛 한 점(포탈이 숨은 자리 표시).
            hint = FxUtil.MakeSprite("HiddenPortalHint", FxUtil.SparkleSprite, transform.position, new Color(HintColor.r, HintColor.g, HintColor.b, 0f), 5, transform);
            Apply(0f);
        }

        private void Update()
        {
            if (player == null) return;

            float distance = Vector2.Distance(player.position, transform.position);
            float target = 1f - Mathf.InverseLerp(RevealNear, RevealFar, distance);
            float alpha = currentAlpha < 0f ? target : Mathf.MoveTowards(currentAlpha, target, Time.deltaTime * 2.5f);
            if (!Mathf.Approximately(alpha, currentAlpha)) Apply(alpha);

            // 다 드러나기 전에는 몇 초마다 한 번씩 반짝인다.
            hintTimer += Time.deltaTime;
            float cycle = hintTimer % 2.4f;
            float twinkle = cycle < 0.6f ? Mathf.Sin(cycle / 0.6f * Mathf.PI) : 0f;
            float hintAlpha = twinkle * 0.8f * (1f - alpha);
            hint.color = new Color(HintColor.r, HintColor.g, HintColor.b, hintAlpha);
            hint.transform.localScale = Vector3.one * (0.8f + 0.5f * twinkle);
        }

        private void Apply(float alpha)
        {
            currentAlpha = alpha;
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] == null) continue;
                Color c = baseColors[i];
                renderers[i].color = new Color(c.r, c.g, c.b, c.a * alpha);
            }
            for (int i = 0; i < lights.Count; i++)
            {
                if (lights[i] != null) lights[i].intensity = baseIntensities[i] * alpha;
            }
        }
    }
}

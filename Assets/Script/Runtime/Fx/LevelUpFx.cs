using System.Collections;
using UnityEngine;
using Aethoria.Bootstrap;
using Aethoria.Characters;

namespace Aethoria.Fx
{
    // 레벨업 연출: 발밑에서 금빛 빛기둥이 솟고 반짝이가 떠오르며, 주변을 잠깐 밝히고(2D 조명), 아주 잠깐 슬로우모션이 걸린다.
    // 효과음은 원래대로 Character가 낸다.
    public class LevelUpFx : MonoBehaviour
    {
        private const float SlowScale = 0.3f;
        private const float SlowDuration = 0.45f; // 실제 시간

        private static readonly Color Gold = new Color(1f, 0.85f, 0.35f);
        private Character character;
        private Collider2D bodyCollider;

        private void Awake()
        {
            character = GetComponent<Character>();
            bodyCollider = GetComponent<Collider2D>();
            if (character != null) character.OnLevelUp += HandleLevelUp;
        }

        private void OnDestroy()
        {
            if (character != null) character.OnLevelUp -= HandleLevelUp;
        }

        private void HandleLevelUp(Character _)
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            Vector3 feet = bodyCollider != null ? new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y, 0f) : transform.position;
            int order = FxUtil.OrderOf(this, 2);

            var pillar = FxUtil.MakeSprite("LevelUpPillar", FxUtil.PillarSprite, feet, new Color(Gold.r, Gold.g, Gold.b, 0.85f), order, transform);
            pillar.gameObject.AddComponent<FadeScale>().Init(1.1f, new Vector3(0.6f, 0.4f, 1f), new Vector3(2.2f, 3.4f, 1f), useUnscaledTime: true);

            var ring = FxUtil.MakeSprite("LevelUpRing", FxUtil.RingSprite, feet + Vector3.up * 0.05f, new Color(Gold.r, Gold.g, Gold.b, 0.9f), order);
            ring.gameObject.AddComponent<FadeScale>().Init(0.7f, new Vector3(0.5f, 0.18f, 1f), new Vector3(3.2f, 1.1f, 1f), useUnscaledTime: true);

            var sparks = FxUtil.Burst(feet, new FxUtil.BurstSpec
            {
                count = 28, lifeMin = 0.8f, lifeMax = 1.4f, speedMin = 1.2f, speedMax = 3.2f,
                sizeMin = 0.1f, sizeMax = 0.22f, sizeEnd = 0.2f, gravity = -0.25f, drag = 1.5f,
                arcStart = 30f, arc = 120f, radius = 0.4f,
                colorA = Gold, colorB = Color.white, texture = FxUtil.Sparkle, sortingOrder = order + 1,
            });
            var m = sparks.main;
            m.useUnscaledTime = true;

            var light = SceneLighting.AddPointLight(transform, new Vector3(0f, 0.8f, 0f), Gold, 4.5f, 1.4f);

            // 슬로우모션(다른 창이 멈춰 둔 상태면 건드리지 않는다)
            bool slowed = false;
            if (Mathf.Approximately(Time.timeScale, 1f)) { Time.timeScale = SlowScale; slowed = true; }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime;
                if (slowed && t >= SlowDuration)
                {
                    if (Mathf.Approximately(Time.timeScale, SlowScale)) Time.timeScale = 1f;
                    slowed = false;
                }
                if (light != null) light.intensity = Mathf.Lerp(1.4f, 0f, t);
                yield return null;
            }
            if (slowed && Mathf.Approximately(Time.timeScale, SlowScale)) Time.timeScale = 1f;
            if (light != null) Destroy(light.gameObject);
        }

        private void OnDisable()
        {
            if (Mathf.Approximately(Time.timeScale, SlowScale)) Time.timeScale = 1f;
        }
    }
}

using UnityEngine;

namespace Aethoria.Fx
{
    public enum AmbientPreset { None, Forest, Village, Plains, Desert, Sea, Heaven, Crimson, Indoor }

    // 맵 분위기에 맞는 떠다니는 입자(먼지·반딧불·꽃잎·모래·물방울·빛 알갱이)와 배경 앰비언스, 발소리 재질을 정한다.
    // 메인 카메라에 붙어 화면을 따라다니지만 입자는 월드 공간에 남아서, 카메라가 움직이면 자연스럽게 지나간다.
    public class AmbientFx : MonoBehaviour
    {
        private static AmbientFx instance;
        private ParticleSystem primary;
        private ParticleSystem secondary;
        private AmbientPreset current = (AmbientPreset)(-1);

        public static void Apply(AmbientPreset preset)
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (instance == null)
            {
                instance = cam.GetComponent<AmbientFx>();
                if (instance == null) instance = cam.gameObject.AddComponent<AmbientFx>();
            }
            instance.SetPreset(preset);
        }

        private void SetPreset(AmbientPreset preset)
        {
            PlayerMotionFx.CurrentSurface = SurfaceFor(preset);
            ProceduralAudio.SetAmbience(AmbienceFor(preset));
            if (preset == current) return;
            current = preset;

            if (primary != null) Destroy(primary.gameObject);
            if (secondary != null) Destroy(secondary.gameObject);
            primary = secondary = null;

            switch (preset)
            {
                case AmbientPreset.Forest:
                    primary = Make("Fireflies", FxUtil.SoftCircle, 5f, 4f, 7f, 0.08f, 0.16f, new Color(0.75f, 1f, 0.45f), new Color(1f, 0.95f, 0.5f), 0.25f, 0f, true);
                    secondary = Make("Dust", FxUtil.SoftCircle, 6f, 6f, 9f, 0.03f, 0.06f, new Color(1f, 1f, 0.9f, 0.5f), new Color(1f, 1f, 1f, 0.3f), 0.15f, -0.01f, false);
                    break;
                case AmbientPreset.Village:
                    primary = Make("Petals", FxUtil.Gem, 4f, 6f, 9f, 0.08f, 0.13f, new Color(1f, 0.75f, 0.85f, 0.9f), new Color(1f, 0.9f, 0.95f, 0.8f), 0.6f, 0.03f, false, drift: 0.6f, spin: true);
                    secondary = Make("Fireflies", FxUtil.SoftCircle, 2.5f, 4f, 7f, 0.07f, 0.13f, new Color(1f, 0.9f, 0.55f), new Color(1f, 1f, 0.75f), 0.25f, 0f, true);
                    break;
                case AmbientPreset.Plains:
                    primary = Make("Pollen", FxUtil.SoftCircle, 7f, 6f, 10f, 0.04f, 0.08f, new Color(1f, 1f, 0.8f, 0.7f), new Color(1f, 1f, 1f, 0.5f), 0.4f, -0.01f, false, drift: 0.5f);
                    break;
                case AmbientPreset.Desert:
                    primary = Make("Sand", FxUtil.SoftCircle, 14f, 2.5f, 4f, 0.03f, 0.07f, new Color(0.95f, 0.8f, 0.55f, 0.75f), new Color(0.85f, 0.7f, 0.45f, 0.5f), 0.4f, 0.02f, false, drift: -2.2f);
                    break;
                case AmbientPreset.Sea:
                    primary = Make("Bubbles", FxUtil.Ring, 5f, 4f, 7f, 0.08f, 0.2f, new Color(0.75f, 0.95f, 1f, 0.8f), new Color(1f, 1f, 1f, 0.6f), 0.3f, -0.08f, false);
                    secondary = Make("Glints", FxUtil.Sparkle, 3f, 1.5f, 2.5f, 0.08f, 0.16f, new Color(0.7f, 0.95f, 1f), Color.white, 0.1f, 0f, true);
                    break;
                case AmbientPreset.Heaven:
                    primary = Make("Motes", FxUtil.Sparkle, 6f, 4f, 7f, 0.08f, 0.18f, new Color(1f, 0.95f, 0.75f), Color.white, 0.25f, -0.03f, true);
                    secondary = Make("Feathers", FxUtil.Gem, 1.5f, 7f, 10f, 0.1f, 0.15f, new Color(1f, 1f, 1f, 0.85f), new Color(1f, 0.97f, 0.9f, 0.7f), 0.4f, 0.02f, false, drift: 0.3f, spin: true);
                    break;
                case AmbientPreset.Crimson:
                    primary = Make("Embers", FxUtil.SoftCircle, 8f, 3f, 5f, 0.05f, 0.1f, new Color(1f, 0.3f, 0.2f), new Color(1f, 0.6f, 0.3f), 0.35f, -0.06f, true);
                    secondary = Make("Petals", FxUtil.Gem, 2.5f, 5f, 8f, 0.08f, 0.13f, new Color(0.75f, 0.05f, 0.12f, 0.9f), new Color(0.5f, 0.02f, 0.08f, 0.8f), 0.5f, 0.03f, false, drift: -0.4f, spin: true);
                    break;
                case AmbientPreset.Indoor:
                    primary = Make("Dust", FxUtil.SoftCircle, 5f, 7f, 10f, 0.03f, 0.06f, new Color(1f, 0.95f, 0.85f, 0.55f), new Color(1f, 1f, 1f, 0.35f), 0.12f, -0.005f, false);
                    break;
            }
        }

        private static Surface SurfaceFor(AmbientPreset p) => p switch
        {
            AmbientPreset.Desert => Surface.Sand,
            AmbientPreset.Sea => Surface.Wet,
            AmbientPreset.Heaven or AmbientPreset.Crimson => Surface.Stone,
            AmbientPreset.Indoor => Surface.Wood,
            _ => Surface.Grass,
        };

        private static Ambience AmbienceFor(AmbientPreset p) => p switch
        {
            AmbientPreset.Forest => Ambience.Forest,
            AmbientPreset.Village => Ambience.Forest,
            AmbientPreset.Plains => Ambience.Wind,
            AmbientPreset.Desert => Ambience.Desert,
            AmbientPreset.Sea => Ambience.Sea,
            AmbientPreset.Heaven => Ambience.Heaven,
            AmbientPreset.Crimson => Ambience.Crimson,
            _ => Ambience.None,
        };

        // rate: 초당 개수, life: 수명, size: 크기, speed: 무작위 떠다님 세기, gravity: 양수면 떨어지고 음수면 떠오른다,
        // twinkle: 반딧불처럼 서서히 켜졌다 꺼짐, drift: 바람(가로 속도), spin: 꽃잎처럼 돈다.
        private ParticleSystem Make(string name, Texture2D texture, float rate, float lifeMin, float lifeMax, float sizeMin, float sizeMax,
            Color colorA, Color colorB, float speed, float gravity, bool twinkle, float drift = 0f, bool spin = false)
        {
            var cam = GetComponent<Camera>();
            float halfH = cam != null && cam.orthographic ? cam.orthographicSize : 5f;
            float halfW = halfH * (cam != null ? cam.aspect : 1.78f);

            var go = new GameObject("Ambient_" + name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -transform.position.z); // 월드 z = 0
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            main.prewarm = true;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            // 화면보다 넓게 뿌려서 카메라가 옆으로 가도 빈 곳이 없게 한다(바람이 불면 바람 반대쪽으로 더 넓게).
            shape.scale = new Vector3(halfW * 2f + 4f + Mathf.Abs(drift) * 4f, halfH * 2f + 1f, 0.1f);
            shape.position = new Vector3(-drift * 2f, 0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = speed * 0.8f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;

            if (Mathf.Abs(drift) > 0.001f)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(drift * 0.7f, drift * 1.3f);
                vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }

            if (spin)
            {
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            }

            FxUtil.ApplyFadeAndSize(ps, 1f, fadeInEnd: 0.2f);
            if (twinkle)
            {
                var col = ps.colorOverLifetime;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.2f, 0.45f),
                            new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = FxUtil.MaterialFor(texture);
            renderer.sortingOrder = 20; // 캐릭터 앞쪽을 지나간다(UI 아래)

            ps.Play();
            return ps;
        }
    }
}

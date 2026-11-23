using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Aethoria.Save;

namespace Aethoria.Bootstrap
{
    // URP 2D 조명 + 포스트 프로세싱을 한곳에서 만든다. 메인 카메라에 한 번 붙이면 스테이지가 바뀌어도 남아 있다.
    //  - Global Light 2D: 모든 Sprite-Lit 스프라이트의 기본 밝기. 1보다 살짝 낮게 둬서 포인트 라이트(주인공 오라, 포탈)가 돋보이게 한다.
    //  - Volume(전역): 블룸(보라 빛이 번지게), 비네트(화면 가장자리 어둡게), 약한 색 보정.
    //    설정의 "화면 효과"를 끄면 카메라의 포스트 프로세싱만 끈다(조명은 그대로).
    public class SceneLighting : MonoBehaviour
    {
        public const float DefaultAmbient = 0.88f;

        private Light2D globalLight;
        private Volume volume;
        private Camera cam;

        public static SceneLighting Instance { get; private set; }

        public static SceneLighting Ensure(Camera camera)
        {
            if (camera == null) return null;
            var existing = camera.GetComponent<SceneLighting>();
            if (existing != null) return existing;
            return camera.gameObject.AddComponent<SceneLighting>();
        }

        private void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();

            var lightGO = new GameObject("Global Light 2D");
            lightGO.transform.SetParent(transform, false);
            globalLight = lightGO.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.color = Color.white;
            globalLight.intensity = DefaultAmbient;

            var volumeGO = new GameObject("Global Volume");
            volumeGO.transform.SetParent(transform, false);
            volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.profile = BuildProfile();

            ApplyPostProcessingSetting();
            GameSettings.Changed += ApplyPostProcessingSetting;
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplyPostProcessingSetting;
            if (Instance == this) Instance = null;
        }

        private static VolumeProfile BuildProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Runtime Post Processing";

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(0.92f, 0.85f, 1f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.05f, 0f, 0.08f));

            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(8f);
            color.saturation.Override(6f);
            return profile;
        }

        private void ApplyPostProcessingSetting()
        {
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = GameSettings.PostProcessing;
        }

        // 스테이지 분위기에 맞춰 기본 밝기/색을 바꾼다(예: 심해는 푸르고 어둡게). 기본값은 DefaultAmbient의 흰빛.
        public void SetAmbient(Color color, float intensity)
        {
            if (globalLight == null) return;
            globalLight.color = color;
            globalLight.intensity = intensity;
        }

        public void ResetAmbient() => SetAmbient(Color.white, DefaultAmbient);

        // 오브젝트에 붙는 부드러운 원형 포인트 라이트. 부모가 사라지면 같이 사라진다.
        public static Light2D AddPointLight(Transform parent, Vector3 localPosition, Color color, float outerRadius, float intensity)
        {
            var go = new GameObject("Point Light 2D");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightInnerRadius = outerRadius * 0.15f;
            light.pointLightOuterRadius = outerRadius;
            light.falloffIntensity = 0.7f;
            return light;
        }
    }
}

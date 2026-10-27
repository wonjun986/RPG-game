using UnityEngine;

namespace Aethoria.Combat
{
    // 피해를 입었을 때 대상 위치에서 잠깐 떠오르다 사라지는 데미지 숫자를 생성한다.
    public class DamagePopup : MonoBehaviour
    {
        private const float MoveSpeed = 1.2f;
        private const float Lifetime = 0.8f;
        private const float FadeStartRatio = 0.5f;

        private TextMesh textMesh;
        private Color baseColor;
        private float timer;

        // sizeMultiplier: 치명타처럼 강조할 숫자는 크게 띄운다.
        public static void Create(Vector3 worldPosition, float damageAmount, Color color, float sizeMultiplier = 1f)
        {
            // 설정에서 데미지 숫자를 끄면(GameSettings.DamageNumbers) 아무것도 띄우지 않는다.
            if (!Aethoria.Save.GameSettings.DamageNumbers) return;
            Vector3 spawnPosition = worldPosition + new Vector3(Random.Range(-0.1f, 0.1f), 1f, 0f);

            var go = new GameObject("DamagePopup");
            go.transform.position = spawnPosition;

            var textMesh = go.AddComponent<TextMesh>();
            textMesh.text = Mathf.RoundToInt(damageAmount).ToString();
            textMesh.characterSize = 0.08f * sizeMultiplier;
            textMesh.fontSize = 32;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = color;

            MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = 100;

            DamagePopup popup = go.AddComponent<DamagePopup>();
            popup.textMesh = textMesh;
            popup.baseColor = color;
        }

        private void Update()
        {
            // 리듬전투 중에는 Time.timeScale=0이라, scaled deltaTime을 쓰면 타이머가 전혀 흐르지
            // 않아 Destroy가 호출되지 않고 팝업이 계속 쌓인다(Character.cs의 무적 타이머와 같은 문제).
            float deltaTime = Time.unscaledDeltaTime;
            transform.position += Vector3.up * MoveSpeed * deltaTime;
            timer += deltaTime;

            if (timer >= Lifetime * FadeStartRatio)
            {
                float fadeT = (timer - Lifetime * FadeStartRatio) / (Lifetime * (1f - FadeStartRatio));
                Color c = baseColor;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                textMesh.color = c;
            }

            if (timer >= Lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}

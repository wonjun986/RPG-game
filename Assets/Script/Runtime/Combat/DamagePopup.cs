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

        public static void Create(Vector3 worldPosition, float damageAmount, Color color)
        {
            Vector3 spawnPosition = worldPosition + new Vector3(Random.Range(-0.1f, 0.1f), 1f, 0f);

            var go = new GameObject("DamagePopup");
            go.transform.position = spawnPosition;

            var textMesh = go.AddComponent<TextMesh>();
            textMesh.text = Mathf.RoundToInt(damageAmount).ToString();
            textMesh.characterSize = 0.08f;
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
            transform.position += Vector3.up * MoveSpeed * Time.deltaTime;
            timer += Time.deltaTime;

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

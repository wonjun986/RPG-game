using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // 아트 리소스가 준비되기 전까지, 공격 시 손에 든 낫을 휘두르는 모션을 임시로 만들어 준다.
    public class ScytheVisual : MonoBehaviour
    {
        [SerializeField] private float swingDuration = 0.2f;
        [SerializeField] private float restAngle = -20f;
        [SerializeField] private float swingFromAngle = 70f;
        [SerializeField] private float swingToAngle = -110f;

        private Transform pivot;
        private Coroutine swingRoutine;

        private void Awake()
        {
            BuildVisual();
        }

        public void PlaySwing(Vector2 facingDirection)
        {
            if (swingRoutine != null) StopCoroutine(swingRoutine);
            swingRoutine = StartCoroutine(SwingRoutine(facingDirection));
        }

        private void BuildVisual()
        {
            var pivotGO = new GameObject("ScythePivot");
            pivotGO.transform.SetParent(transform, false);
            pivotGO.transform.localPosition = new Vector3(0.15f, 0.9f, 0f);
            pivot = pivotGO.transform;
            pivot.localRotation = Quaternion.Euler(0f, 0f, restAngle);

            var weaponGO = new GameObject("ScytheSprite");
            weaponGO.transform.SetParent(pivot, false);
            var spriteRenderer = weaponGO.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 1;

            const int width = 20;
            const int height = 40;
            var texture = new Texture2D(width, height) { filterMode = FilterMode.Point };
            texture.SetPixels(GenerateScythePixels(width, height));
            texture.Apply();

            spriteRenderer.sprite = Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.05f),
                32);
        }

        private IEnumerator SwingRoutine(Vector2 facingDirection)
        {
            float flip = facingDirection.x < 0f ? -1f : 1f;
            pivot.localScale = new Vector3(flip, 1f, 1f);

            float elapsed = 0f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / swingDuration);
                float angle = Mathf.Lerp(swingFromAngle, swingToAngle, t);
                pivot.localRotation = Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }

            pivot.localRotation = Quaternion.Euler(0f, 0f, restAngle);
            swingRoutine = null;
        }

        // 텍스처 좌표 기준 y=0은 손잡이 끝(아래), y=height는 낫날 쪽(위).
        // 가운데 세로 막대(손잡이) + 위쪽에서 오른쪽으로 굽어 넓어지는 갈고리(날)를 그린다.
        private static Color[] GenerateScythePixels(int width, int height)
        {
            var pixels = new Color[width * height];
            var transparent = new Color(0f, 0f, 0f, 0f);
            var handleColor = new Color(0.35f, 0.22f, 0.1f);
            var bladeColor = new Color(0.75f, 0.78f, 0.82f);

            int handleWidth = Mathf.Max(2, width / 6);
            int handleX = width / 2 - handleWidth / 2;
            int bladeStartY = (int)(height * 0.55f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixelColor = transparent;

                    if (x >= handleX && x < handleX + handleWidth)
                    {
                        pixelColor = handleColor;
                    }

                    if (y >= bladeStartY)
                    {
                        float t = (float)(y - bladeStartY) / (height - bladeStartY);
                        int bladeReach = handleX + handleWidth + Mathf.RoundToInt(t * (width - handleX - handleWidth));
                        int bladeThickness = Mathf.Max(2, 3 + Mathf.RoundToInt(t * 2));
                        int bladeInner = Mathf.Max(handleX + handleWidth, bladeReach - bladeThickness);

                        if (x >= bladeInner && x < bladeReach)
                        {
                            pixelColor = bladeColor;
                        }
                    }

                    pixels[y * width + x] = pixelColor;
                }
            }

            return pixels;
        }
    }
}

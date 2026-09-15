using UnityEngine;

namespace Aethoria.Characters
{
    public enum PlaceholderShape
    {
        Humanoid,
        Blob
    }

    // 아트 리소스가 준비되기 전까지 대략적인 사람/몬스터 실루엣을 임시로 생성해 준다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterPlaceholderVisual : MonoBehaviour
    {
        [SerializeField] private Color color = new Color(0.4f, 0.7f, 1f);
        [SerializeField] private int pixelWidth = 32;
        [SerializeField] private int pixelHeight = 48;
        [SerializeField] private int pixelsPerUnit = 32;
        [SerializeField] private PlaceholderShape shape = PlaceholderShape.Humanoid;

        private void Awake()
        {
            GenerateSprite(forceRegenerate: false);
        }

        // 코드에서 몬스터/캐릭터별로 색·크기·모양을 다르게 지정할 때 사용 (AddComponent 직후 호출).
        public void Configure(Color newColor, int width, int height, PlaceholderShape newShape = PlaceholderShape.Blob)
        {
            color = newColor;
            pixelWidth = width;
            pixelHeight = height;
            shape = newShape;
            GenerateSprite(forceRegenerate: true);
        }

        private void GenerateSprite(bool forceRegenerate)
        {
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (!forceRegenerate && spriteRenderer.sprite != null) return;

            var pixels = shape == PlaceholderShape.Humanoid
                ? GenerateHumanoidPixels(pixelWidth, pixelHeight, color)
                : GenerateBlobPixels(pixelWidth, pixelHeight, color);

            var texture = new Texture2D(pixelWidth, pixelHeight) { filterMode = FilterMode.Point };
            texture.SetPixels(pixels);
            texture.Apply();

            spriteRenderer.sprite = Sprite.Create(
                texture,
                new Rect(0, 0, pixelWidth, pixelHeight),
                new Vector2(0.5f, 0f),
                pixelsPerUnit);
        }

        private static Color[] GenerateBlobPixels(int width, int height, Color bodyColor)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bodyColor;
            return pixels;
        }

        // 텍스처 좌표 기준 y=0은 발(하단), y=height는 머리(상단).
        // 다리(하단 30%) + 몸통(중간 38%) + 머리(상단 32%)로 나눠서 대략적인 사람 실루엣을 그린다.
        private static Color[] GenerateHumanoidPixels(int width, int height, Color bodyColor)
        {
            var pixels = new Color[width * height];
            var skin = new Color(0.96f, 0.80f, 0.65f);
            var transparent = new Color(0f, 0f, 0f, 0f);

            float headBottom = height * 0.68f;
            float torsoBottom = height * 0.30f;

            float headLeft = width * 0.28f;
            float headRight = width * 0.72f;

            float torsoLeft = width * 0.20f;
            float torsoRight = width * 0.80f;

            float legGap = width * 0.10f;
            float legWidth = (torsoRight - torsoLeft - legGap) / 2f;
            float leftLegRight = torsoLeft + legWidth;
            float rightLegLeft = torsoRight - legWidth;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixelColor = transparent;

                    if (y >= headBottom)
                    {
                        if (x >= headLeft && x < headRight) pixelColor = skin;
                    }
                    else if (y >= torsoBottom)
                    {
                        if (x >= torsoLeft && x < torsoRight) pixelColor = bodyColor;
                    }
                    else
                    {
                        bool inLeftLeg = x >= torsoLeft && x < leftLegRight;
                        bool inRightLeg = x >= rightLegLeft && x < torsoRight;
                        if (inLeftLeg || inRightLeg) pixelColor = bodyColor;
                    }

                    pixels[y * width + x] = pixelColor;
                }
            }

            return pixels;
        }
    }
}

using UnityEngine;

namespace Aethoria.Monsters
{
    // 몬스터 발 아래에 체력바를 띄우고, 데미지를 받을 때마다 채워진 비율을 갱신한다.
    [RequireComponent(typeof(Monster))]
    public class MonsterHealthBar : MonoBehaviour
    {
        [SerializeField] private float width = 0.6f;
        [SerializeField] private float height = 0.08f;
        [SerializeField] private float verticalOffset = 0.15f; // 발(피벗) 기준 아래로 얼마나 띄울지
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private Color fillColor = new Color(0.85f, 0.15f, 0.15f);

        private static Sprite sharedPixelSprite;
        private static Sprite frameSprite;
        private static bool frameLoaded;

        // 틀 그림(UI/HUD/MonsterHpFrame, 520x60, PPU 270, 좌우 9-slice). 안쪽 검은 홈이 높이 약 21px = 0.078칸이라
        // 막대 두께(height)를 그 홈에 맞추고, 홈 바깥 양 끝 장식(각 약 45px)만큼 틀을 더 넓게 그린다.
        private const float FrameEndsUnits = (44.9f + 44.4f) / 270f;
        private const float FrameChannelHeight = 0.074f;

        private Monster monster;
        private Transform fillTransform;

        private void Awake()
        {
            monster = GetComponent<Monster>();
        }

        private void Start()
        {
            BuildBar();
        }

        // 몬스터 크기에 맞춰 체력바 크기/위치를 조정할 때 사용 (AddComponent 직후 Start 전에 호출).
        public void Configure(float newWidth, float newVerticalOffset)
        {
            width = newWidth;
            verticalOffset = newVerticalOffset;
        }

        private void LateUpdate()
        {
            float ratio = monster.MaxHp > 0f ? Mathf.Clamp01(monster.CurrentHp / monster.MaxHp) : 0f;
            fillTransform.localScale = new Vector3(width * ratio, height, 1f);
        }

        private void BuildBar()
        {
            var sprite = GetSharedPixelSprite();
            float leftX = -width / 2f;

            if (!frameLoaded)
            {
                frameSprite = Resources.Load<Sprite>("UI/HUD/MonsterHpFrame");
                frameLoaded = true;
            }
            if (frameSprite != null)
            {
                // 틀 안쪽 홈이 검은색이라 따로 배경 막대를 깔지 않는다.
                height = FrameChannelHeight;
                var frameGO = new GameObject("HealthBar_Frame");
                frameGO.transform.SetParent(transform, false);
                frameGO.transform.localPosition = new Vector3(0f, -verticalOffset, 0f);
                var frameRenderer = frameGO.AddComponent<SpriteRenderer>();
                frameRenderer.sprite = frameSprite;
                frameRenderer.drawMode = SpriteDrawMode.Sliced;
                frameRenderer.size = new Vector2(width + FrameEndsUnits, frameSprite.rect.height / frameSprite.pixelsPerUnit);
                frameRenderer.sortingOrder = 10;
            }

            var bgGO = new GameObject("HealthBar_BG");
            bgGO.transform.SetParent(transform, false);
            bgGO.transform.localPosition = new Vector3(leftX, -verticalOffset, 0f);
            var bgRenderer = bgGO.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = sprite;
            bgRenderer.color = backgroundColor;
            bgRenderer.sortingOrder = 10;
            bgGO.transform.localScale = new Vector3(width, height, 1f);
            if (frameSprite != null) bgRenderer.enabled = false;

            var fillGO = new GameObject("HealthBar_Fill");
            fillGO.transform.SetParent(transform, false);
            fillGO.transform.localPosition = new Vector3(leftX, -verticalOffset, 0f);
            var fillRenderer = fillGO.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = sprite;
            fillRenderer.color = fillColor;
            fillRenderer.sortingOrder = 11;
            fillTransform = fillGO.transform;
            fillTransform.localScale = new Vector3(width, height, 1f);
        }

        // 왼쪽 끝을 기준으로 가로로만 늘었다 줄었다 하도록, 피벗이 왼쪽-가운데인 1x1 흰 픽셀을 공유해서 쓴다.
        private static Sprite GetSharedPixelSprite()
        {
            if (sharedPixelSprite != null) return sharedPixelSprite;

            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            sharedPixelSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            return sharedPixelSprite;
        }
    }
}

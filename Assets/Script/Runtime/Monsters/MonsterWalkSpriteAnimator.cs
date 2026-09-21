using UnityEngine;

namespace Aethoria.Monsters
{
    // Resources/Art/Monsters/... 아래의 이동 프레임을 불러와서, 좌우로 움직이는 동안 순서대로 재생한다.
    // 프레임을 못 찾으면 비활성화되어 기존 CharacterPlaceholderVisual 이미지가 그대로 남는다.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class MonsterWalkSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string resourcesPath = "Art/Monsters/Knight/Walk";
        [SerializeField] private float frameRate = 8f;
        [SerializeField] private float moveThreshold = 0.05f;

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float timer;
        private int currentIndex;

        public bool HasFrames => frames != null && frames.Length > 0;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            LoadFrames();
        }

        // 슬라임/보스처럼 몬스터마다 프레임 경로가 다를 때, AddComponent 직후 호출해서 덮어쓴다.
        public void Configure(string newResourcesPath)
        {
            resourcesPath = newResourcesPath;
            LoadFrames();
        }

        private void LoadFrames()
        {
            frames = Resources.LoadAll<Sprite>(resourcesPath);
            if (frames == null || frames.Length == 0)
            {
                enabled = false;
                return;
            }

            enabled = true;
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            spriteRenderer.sprite = frames[0];
        }

        private void Update()
        {
            bool isMoving = Mathf.Abs(body.linearVelocity.x) > moveThreshold;

            if (isMoving)
            {
                timer += Time.deltaTime;
                float frameDuration = 1f / frameRate;
                if (timer >= frameDuration)
                {
                    timer -= frameDuration;
                    currentIndex = (currentIndex + 1) % frames.Length;
                    spriteRenderer.sprite = frames[currentIndex];
                }
            }
            else
            {
                timer = 0f;
                currentIndex = 0;
                spriteRenderer.sprite = frames[0];
            }
        }
    }
}

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
        // 멈춰 있을 때 반복할 대기 프레임(보스 기사처럼 대기 모션이 따로 있는 몬스터만). 없으면 걷기 첫 프레임에 멈춘다.
        private Sprite[] idleFrames;
        private float idleFrameRate = 5f;

        public bool HasFrames => frames != null && frames.Length > 0;
        // 리듬 전투 화면이 몬스터 대기 모션으로 그대로 가져다 쓴다.
        public Sprite[] Frames => frames;

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

        public void ConfigureIdle(string idleResourcesPath, float newIdleFrameRate)
        {
            idleFrames = Resources.LoadAll<Sprite>(idleResourcesPath);
            if (idleFrames != null && idleFrames.Length > 0)
            {
                System.Array.Sort(idleFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            idleFrameRate = newIdleFrameRate;
            currentIndex = 0;
        }

        public float IdleFrameRate => idleFrameRate;

        // 루나벨 2페이즈처럼 모습이 바뀐 뒤 걷기/대기 프레임을 통째로 갈아 끼운다. newIdleFrameRate를 주면 대기 속도도 바꾼다.
        public void OverrideFrames(Sprite[] newFrames, Sprite[] newIdleFrames, float newIdleFrameRate = -1f)
        {
            frames = newFrames;
            idleFrames = newIdleFrames;
            if (newIdleFrameRate > 0f) idleFrameRate = newIdleFrameRate;
            currentIndex = 0;
            timer = 0f;
            enabled = HasFrames;
            if (HasFrames) spriteRenderer.sprite = frames[0];
        }

        public bool HasIdleFrames => idleFrames != null && idleFrames.Length > 0;
        // 리듬 전투 화면이 대기 모션이 따로 있는 몬스터(보스 기사)는 걷기 대신 이걸 기본 모션으로 쓴다.
        public Sprite[] IdleFrames => idleFrames;

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

            if (!isMoving && HasIdleFrames)
            {
                timer += Time.deltaTime;
                float idleDuration = 1f / idleFrameRate;
                if (timer >= idleDuration)
                {
                    timer -= idleDuration;
                    currentIndex++;
                }
                currentIndex %= idleFrames.Length;
                spriteRenderer.sprite = idleFrames[currentIndex];
                return;
            }

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

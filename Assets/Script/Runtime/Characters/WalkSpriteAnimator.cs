using UnityEngine;

namespace Aethoria.Characters
{
    // Resources/Art/Necrosia/Walk 아래의 실제 걷기 프레임을 불러와서 이동 중일 때 순서대로 재생한다.
    // 프레임을 못 찾으면 기존 절차적 플레이스홀더(WalkBobVisual)를 그대로 둔다.
    [RequireComponent(typeof(CharacterMovement2D))]
    public class WalkSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string resourcesPath = "Art/Necrosia/Walk";
        [SerializeField] private float frameRate = 10f;

        private CharacterMovement2D movement;
        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float timer;
        private int currentIndex;

        private void Awake()
        {
            movement = GetComponent<CharacterMovement2D>();
            var visual = transform.Find("Visual");
            spriteRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();

            frames = Resources.LoadAll<Sprite>(resourcesPath);
            if (frames == null || frames.Length == 0)
            {
                enabled = false;
                return;
            }

            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            spriteRenderer.sprite = frames[0];

            var bob = GetComponent<WalkBobVisual>();
            if (bob != null) Destroy(bob);
        }

        private void Update()
        {
            // 원본 프레임은 오른쪽을 보고 있다고 가정하고, 왼쪽을 볼 때는 스프라이트를 좌우 반전한다.
            spriteRenderer.flipX = movement.FacingDirection.x < 0f;

            if (movement.IsMoving && movement.IsGrounded)
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

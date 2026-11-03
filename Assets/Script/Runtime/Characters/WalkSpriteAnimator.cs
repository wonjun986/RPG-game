using UnityEngine;

namespace Aethoria.Characters
{
    // Resources/Art/Necrosia/Walk, /Idle 아래의 실제 프레임을 불러와서 이동 중엔 걷기를,
    // 멈춰서 있으면 대기 모션을 재생한다. 둘 다 못 찾으면 기존 절차적 플레이스홀더(WalkBobVisual)를 그대로 둔다.
    [RequireComponent(typeof(CharacterMovement2D))]
    public class WalkSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string walkResourcesPath = "Art/Necrosia/Walk";
        [SerializeField] private string idleResourcesPath = "Art/Necrosia/Idle";
        [SerializeField] private float walkFrameRate = 12f;
        // 달리기(고딕 사신의 질주, 8프레임)는 8장을 같은 캔버스(326px)로 잘랐고 머리 장식~부츠가 약 320px로 캔버스를 거의 채운다.
        // 예전 걷기(프레임 445px를 1.5로, 머리 장식~부츠 351px)와 같은 몸 키(약 1.18)가 되도록 캔버스 키를 1.2로 맞춘다.
        [SerializeField] private float walkTargetHeight = 1.2f;
        // 대기는 6프레임(여섯 프레임 고딕 사신 스프라이트 시트)이라, 예전처럼 1초에 한 바퀴 돌게 6으로 둔다.
        [SerializeField] private float idleFrameRate = 6f;
        // 대기 포즈는 낫을 머리 위로 높이 들고 있어서 프레임 전체 높이 중 캐릭터 몸통이 차지하는
        // 비중이 걷기보다 작다. 기준 키(1.5)를 그대로 쓰면 몸통이 걷기보다 작아 보여서, 대기 전용으로
        // 조금 더 큰 기준 키를 쓴다.
        [SerializeField] private float idleTargetHeight = 1.8f;

        private CharacterMovement2D movement;
        private SpriteRenderer spriteRenderer;
        private Sprite[] walkFrames;
        private Sprite[] idleFrames;
        private float timer;
        private int currentIndex;
        private bool wasMoving;

        private void Awake()
        {
            movement = GetComponent<CharacterMovement2D>();
            var visual = transform.Find("Visual");
            spriteRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();

            walkFrames = LoadSortedFrames(walkResourcesPath);
            idleFrames = LoadSortedFrames(idleResourcesPath);

            bool hasWalk = walkFrames != null && walkFrames.Length > 0;
            bool hasIdle = idleFrames != null && idleFrames.Length > 0;
            if (!hasWalk && !hasIdle)
            {
                enabled = false;
                return;
            }

            spriteRenderer.sprite = hasIdle ? idleFrames[0] : walkFrames[0];

            var bob = GetComponent<WalkBobVisual>();
            if (bob != null) Destroy(bob);
        }

        private static Sprite[] LoadSortedFrames(string path)
        {
            var frames = Resources.LoadAll<Sprite>(path);
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            return frames;
        }

        private void Update()
        {
            spriteRenderer.flipX = movement.FacingDirection.x < 0f;

            bool moving = movement.IsMoving && movement.IsGrounded;
            bool hasWalk = walkFrames != null && walkFrames.Length > 0;
            bool hasIdle = idleFrames != null && idleFrames.Length > 0;

            Sprite[] activeFrames = moving && hasWalk ? walkFrames : hasIdle ? idleFrames : walkFrames;
            if (activeFrames == null || activeFrames.Length == 0) return;

            bool activeIsWalk = activeFrames == walkFrames;
            if (activeIsWalk != wasMoving)
            {
                wasMoving = activeIsWalk;
                timer = 0f;
                currentIndex = 0;
            }

            float frameRate = activeIsWalk ? walkFrameRate : idleFrameRate;
            timer += Time.deltaTime;
            float frameDuration = 1f / frameRate;
            if (timer >= frameDuration)
            {
                timer -= frameDuration;
                currentIndex = (currentIndex + 1) % activeFrames.Length;
            }

            Sprite frame = activeFrames[currentIndex];
            spriteRenderer.sprite = frame;
            // 원본 프레임마다 캐릭터를 감싸는 크롭 크기가 제각각이라(특히 Idle은 머리 위로 든 낫까지
            // 포함해서 프레임마다 최대 20px 넘게 차이난다), 스킬 애니메이터들과 같은 방식으로
            // 매 프레임 기준 키에 맞춰 보정한다. PPU를 프레임별로 정교하게 맞출 필요가 없어진다.
            if (activeIsWalk)
            {
                SkillFrameNormalizer.Apply(spriteRenderer.transform, frame, walkTargetHeight);
            }
            else
            {
                SkillFrameNormalizer.Apply(spriteRenderer.transform, frame, idleTargetHeight);
            }
        }
    }
}

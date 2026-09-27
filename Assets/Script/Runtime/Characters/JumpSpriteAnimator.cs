using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // 공중에 떠 있는 동안 점프 프레임을, 2단 점프 순간에는 낫 플립 연출을 재생한다.
    // WalkSpriteAnimator와 스프라이트를 두고 다투지 않도록, 공중에 있는 동안은 그쪽을 꺼둔다.
    [RequireComponent(typeof(CharacterMovement2D))]
    public class JumpSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string jumpResourcesPath = "Art/Necrosia/Jump";
        [SerializeField] private string doubleJumpResourcesPath = "Art/Necrosia/DoubleJump";
        [SerializeField] private float doubleJumpDuration = 0.4f;
        [SerializeField] private float landingFrameHold = 0.08f;

        private CharacterMovement2D movement;
        private WalkSpriteAnimator walkAnimator;
        private SpriteRenderer spriteRenderer;
        private Sprite[] jumpFrames;
        private Sprite[] doubleJumpFrames;
        private Coroutine doubleJumpRoutine;
        private Coroutine landRoutine;
        private bool wasGrounded;

        private void Awake()
        {
            movement = GetComponent<CharacterMovement2D>();
            walkAnimator = GetComponent<WalkSpriteAnimator>();

            var visual = transform.Find("Visual");
            spriteRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();

            jumpFrames = LoadSortedFrames(jumpResourcesPath);
            doubleJumpFrames = LoadSortedFrames(doubleJumpResourcesPath);

            if ((jumpFrames == null || jumpFrames.Length == 0) && (doubleJumpFrames == null || doubleJumpFrames.Length == 0))
            {
                enabled = false;
                return;
            }

            wasGrounded = movement.IsGrounded;
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

        private void OnEnable()
        {
            movement.OnDoubleJump += HandleDoubleJump;
        }

        private void OnDisable()
        {
            movement.OnDoubleJump -= HandleDoubleJump;

            // 대시 등 다른 스킬이 이 애니메이터를 잠시 꺼둘 때, 진행 중이던 코루틴은 enabled와
            // 상관없이 계속 돌아서 그쪽 스프라이트/스케일과 계속 충돌했다(대시+점프 시 크기가
            // 제멋대로 바뀌던 원인). 꺼지는 시점에 확실히 멈춘다.
            if (doubleJumpRoutine != null)
            {
                StopCoroutine(doubleJumpRoutine);
                doubleJumpRoutine = null;
            }
            if (landRoutine != null)
            {
                StopCoroutine(landRoutine);
                landRoutine = null;
            }
        }

        private void Update()
        {
            bool grounded = movement.IsGrounded;

            if (!grounded)
            {
                if (walkAnimator != null) walkAnimator.enabled = false;
                spriteRenderer.flipX = movement.FacingDirection.x < 0f;

                if (doubleJumpRoutine == null && jumpFrames != null && jumpFrames.Length > 0)
                {
                    // 올라가는 중엔 상승 프레임, 정점을 지나 내려가는 중엔 체공/하강 프레임을 보여준다.
                    int frameIndex = movement.VerticalVelocity > 0.5f ? 1 : 2;
                    frameIndex = Mathf.Clamp(frameIndex, 0, jumpFrames.Length - 1);
                    SetFrame(jumpFrames[frameIndex]);
                }
            }
            else if (!wasGrounded)
            {
                HandleLanded();
            }

            wasGrounded = grounded;
        }

        private void HandleDoubleJump()
        {
            if (doubleJumpFrames == null || doubleJumpFrames.Length == 0) return;

            if (landRoutine != null)
            {
                StopCoroutine(landRoutine);
                landRoutine = null;
            }
            if (doubleJumpRoutine != null) StopCoroutine(doubleJumpRoutine);
            doubleJumpRoutine = StartCoroutine(PlayDoubleJumpRoutine());
        }

        private IEnumerator PlayDoubleJumpRoutine()
        {
            if (walkAnimator != null) walkAnimator.enabled = false;

            float frameDuration = doubleJumpDuration / doubleJumpFrames.Length;
            for (int i = 0; i < doubleJumpFrames.Length; i++)
            {
                SetFrame(doubleJumpFrames[i]);
                yield return new WaitForSeconds(frameDuration);
            }

            doubleJumpRoutine = null;
        }

        private void HandleLanded()
        {
            if (doubleJumpRoutine != null)
            {
                StopCoroutine(doubleJumpRoutine);
                doubleJumpRoutine = null;
            }

            if (jumpFrames != null && jumpFrames.Length > 0)
            {
                SetFrame(jumpFrames[jumpFrames.Length - 1]); // 착지 프레임
                landRoutine = StartCoroutine(ReturnToWalkAfterLanding());
            }
            else if (walkAnimator != null)
            {
                walkAnimator.enabled = true;
            }
        }

        // 걷기 애니메이터와 같은 기준 키로 매 프레임 보정한다. 점프/더블점프 시트도 프레임마다
        // 캐릭터를 감싸는 크롭 크기가 제각각인 데다, 대시 등 다른 스킬이 스케일을 건드려 놓고
        // 넘겨줄 수도 있어서, 원본 PPU만 믿고 그리면 크기가 프레임마다·상황마다 들쭉날쭉해진다.
        private void SetFrame(Sprite frame)
        {
            spriteRenderer.sprite = frame;
            SkillFrameNormalizer.Apply(spriteRenderer.transform, frame);
        }

        private IEnumerator ReturnToWalkAfterLanding()
        {
            yield return new WaitForSeconds(landingFrameHold);
            if (walkAnimator != null) walkAnimator.enabled = true;
            landRoutine = null;
        }
    }
}

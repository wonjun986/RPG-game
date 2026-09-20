using UnityEngine;
using UnityEngine.InputSystem;

namespace Aethoria.Characters
{
    // 좌우 이동 + 위쪽 방향키로 점프하는 옛날 플래시 액션 게임 스타일 조작.
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterMovement2D : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float gravityScale = 4f;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private readonly Collider2D[] groundCheckResults = new Collider2D[8];
        private float horizontalInput;
        private bool jumpRequested;
        private bool isGrounded;
        private bool inputLocked;

        public Vector2 FacingDirection { get; private set; } = Vector2.right;
        public bool IsMoving => Mathf.Abs(horizontalInput) > 0.01f;
        public bool IsGrounded => isGrounded;
        public bool IsInputLocked => inputLocked;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            body.gravityScale = gravityScale;
            body.freezeRotation = true;
        }

        private void Update()
        {
            if (!inputLocked) ReadInput();
        }

        // 돌진기 등 스킬이 물리 이동을 직접 제어하는 동안 평소 좌우/점프 입력을 잠근다.
        public void SetInputLocked(bool locked)
        {
            inputLocked = locked;
            if (locked) horizontalInput = 0f;
        }

        private void FixedUpdate()
        {
            isGrounded = CheckGrounded();

            if (inputLocked) return;

            body.linearVelocity = new Vector2(horizontalInput * moveSpeed, body.linearVelocity.y);

            if (jumpRequested && isGrounded)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
                isGrounded = false; // 착지 전까지 재점프 금지
            }
            jumpRequested = false;
        }

        // 발밑(자신의 콜라이더 바로 아래)에 뭔가 있는지로 착지 여부를 매 프레임 새로 판정한다.
        // 콜리전 이벤트 기반보다 단순하고, "공중에서 점프가 다시 풀리는" 문제가 없다.
        // 트리거 콜라이더(퀘스트/영역 감지 등)와 자기 자신의 콜라이더는 바닥으로 치지 않는다.
        // (이걸 걸러내지 않으면 공중의 트리거 안에서도 착지 판정이 나서 무한 점프가 가능해진다.)
        private bool CheckGrounded()
        {
            Vector2 checkCenter = (Vector2)transform.position + Vector2.down * 0.05f;
            Vector2 checkSize = new Vector2(0.5f, 0.06f);

            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.OverlapBox(checkCenter, checkSize, 0f, filter, groundCheckResults);
            for (int i = 0; i < count; i++)
            {
                if (groundCheckResults[i] != bodyCollider) return true;
            }
            return false;
        }

        private void ReadInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                horizontalInput = 0f;
                return;
            }

            float input = 0f;
            if (keyboard.leftArrowKey.isPressed) input -= 1f;
            if (keyboard.rightArrowKey.isPressed) input += 1f;
            horizontalInput = input;

            if (Mathf.Abs(input) > 0.01f)
            {
                FacingDirection = input > 0f ? Vector2.right : Vector2.left;
            }

            if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                jumpRequested = true;
            }
        }
    }
}

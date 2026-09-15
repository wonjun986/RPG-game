using UnityEngine;

namespace Aethoria.Characters
{
    // 실제 걷기 스프라이트 애니메이션(8프레임)이 들어오기 전까지, 이동 중일 때 위아래로
    // 살짝 들썩이는 느낌만 코드로 흉내낸다. "Visual" 자식이 있으면 그쪽만 움직여서
    // 콜라이더/Rigidbody2D가 붙은 루트는 건드리지 않는다.
    [RequireComponent(typeof(CharacterMovement2D))]
    public class WalkBobVisual : MonoBehaviour
    {
        [SerializeField] private float bobAmplitude = 0.04f;
        [SerializeField] private float bobFrequency = 8f;

        private CharacterMovement2D movement;
        private Transform visualRoot;
        private float bobTimer;

        private void Awake()
        {
            movement = GetComponent<CharacterMovement2D>();
            var visualChild = transform.Find("Visual");
            visualRoot = visualChild != null ? visualChild : transform;
        }

        private void Update()
        {
            if (movement.IsMoving && movement.IsGrounded)
            {
                bobTimer += Time.deltaTime * bobFrequency;
                float offset = Mathf.Abs(Mathf.Sin(bobTimer)) * bobAmplitude;
                visualRoot.localPosition = new Vector3(0f, offset, 0f);
            }
            else
            {
                bobTimer = 0f;
                visualRoot.localPosition = Vector3.zero;
            }
        }
    }
}

using UnityEngine;

namespace Aethoria.Characters
{
    // 메인 카메라가 주인공의 좌우 이동을 부드럽게 따라간다.
    // 맵 경계 밖이 보이지 않도록, 카메라 중심이 갈 수 있는 X 범위를 화면 절반 너비만큼 안쪽으로 제한한다.
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private float smoothTime = 0.15f;

        private Transform target;
        private float minX;
        private float maxX;
        private float fixedY;
        private Vector3 velocity;

        // 스테이지가 새로 지어질 때마다 GameBootstrap이 호출해 추적 대상과 맵 범위를 다시 알려준다.
        public void Follow(Transform newTarget, float mapXMin, float mapXMax, float cameraY)
        {
            target = newTarget;
            fixedY = cameraY;

            var cam = GetComponent<Camera>();
            float halfWidth = cam.orthographicSize * cam.aspect;
            float mapRightEdge = mapXMax + 1f; // 타일 좌표는 왼쪽 끝 기준이라 오른쪽 벽은 +1 지점

            minX = mapXMin + halfWidth;
            maxX = mapRightEdge - halfWidth;

            if (minX > maxX)
            {
                float mid = (mapXMin + mapRightEdge) / 2f;
                minX = maxX = mid;
            }

            transform.position = new Vector3(Mathf.Clamp(target.position.x, minX, maxX), fixedY, transform.position.z);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float desiredX = Mathf.Clamp(target.position.x, minX, maxX);
            Vector3 desired = new Vector3(desiredX, fixedY, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }
    }
}

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
        private float floorY;
        private float maxRise;
        private Vector3 velocity;
        // 흔들림을 뺀 실제 카메라 위치. 흔들림 값을 transform에 그대로 더하면 다음 프레임 SmoothDamp의 출발점에 쌓여서
        // (특히 위쪽으로만 흔들 때) 카메라가 점점 밀려 날아갔다. 따라가기는 이 값으로 하고, 흔들림은 그릴 때만 더한다.
        private Vector3 basePosition;
        private float defaultSmoothTime = -1f;

        // 연출용 클로즈업: orthographicSize를 zoomTarget까지 부드럽게 바꾼다. 바닥이 화면에서 같은 높이에 오도록 카메라 높이도
        // 줄어든 만큼(크기 x 0.6) 같이 내리고, 맵 끝 제한도 줄어든 화면 폭으로 다시 잰다. ResetZoom으로 원래 크기로 돌아간다.
        private Camera cam;
        private float baseOrthoSize = -1f;
        private float zoomTarget = -1f;
        private float zoomSpeed;
        private float mapLeft;
        private float mapRight;
        // 연출용 화면 흔들림(남은 시간/세기).
        private float shakeTime;
        private float shakeDuration;
        private float shakeMagnitude;

        // 바닥에서 이 높이까지는(점프 등) 카메라가 위아래로 출렁이지 않게 그대로 둔다.
        private const float RiseDeadZone = 1.5f;

        // 스테이지가 새로 지어질 때마다 GameBootstrap이 호출해 추적 대상과 맵 범위를 다시 알려준다.
        // maxRise > 0이면 플레이어가 바닥(mapFloorY)보다 높은 지형에 올라갔을 때 카메라도 최대 그만큼 따라 올라간다.
        public void Follow(Transform newTarget, float mapXMin, float mapXMax, float cameraY, float mapFloorY = 0f, float newMaxRise = 0f)
        {
            target = newTarget;
            fixedY = cameraY;
            floorY = mapFloorY;
            maxRise = newMaxRise;

            cam = GetComponent<Camera>();
            baseOrthoSize = cam.orthographicSize;
            zoomTarget = -1f;
            shakeTime = 0f;
            mapLeft = mapXMin;
            mapRight = mapXMax + 1f; // 타일 좌표는 왼쪽 끝 기준이라 오른쪽 벽은 +1 지점
            RecalculateBounds();

            basePosition = new Vector3(Mathf.Clamp(target.position.x, minX, maxX), DesiredY(), transform.position.z);
            transform.position = basePosition;
            velocity = Vector3.zero;
        }

        // 보스 등장 연출처럼 잠깐 다른 대상을 비출 때 쓴다. 맵 범위/높이는 그대로 두고 따라갈 대상과
        // 따라가는 속도만 바꾼다(newSmoothTime이 크면 천천히 미끄러지듯 이동). ResetSmoothTime으로 되돌린다.
        public void SetTarget(Transform newTarget, float newSmoothTime)
        {
            if (defaultSmoothTime < 0f) defaultSmoothTime = smoothTime;
            target = newTarget;
            smoothTime = newSmoothTime;
        }

        public void ResetSmoothTime()
        {
            if (defaultSmoothTime >= 0f) smoothTime = defaultSmoothTime;
        }

        // 화면을 size(orthographicSize)까지 duration초에 걸쳐 확대/축소한다.
        public void ZoomTo(float size, float duration)
        {
            if (cam == null || baseOrthoSize < 0f) return;
            zoomTarget = size;
            zoomSpeed = Mathf.Abs(cam.orthographicSize - size) / Mathf.Max(0.01f, duration);
        }

        public void ResetZoom(float duration)
        {
            if (baseOrthoSize > 0f) ZoomTo(baseOrthoSize, duration);
        }

        public void Shake(float duration, float magnitude)
        {
            // 설정에서 화면 흔들림을 끄면(GameSettings.ScreenShake) 흔들지 않는다.
            if (!Aethoria.Save.GameSettings.ScreenShake) return;
            shakeDuration = shakeTime = duration;
            shakeMagnitude = magnitude;
        }

        private void RecalculateBounds()
        {
            float halfWidth = cam.orthographicSize * cam.aspect;
            minX = mapLeft + halfWidth;
            maxX = mapRight - halfWidth;
            if (minX > maxX)
            {
                float mid = (mapLeft + mapRight) / 2f;
                minX = maxX = mid;
            }
        }

        private float DesiredY()
        {
            // 확대하면 화면 아래 바닥 위치가 그대로 보이게, 줄어든 크기만큼(크기 x 0.6 = FitCamera와 같은 비율) 카메라를 내린다.
            float zoomOffset = cam != null && baseOrthoSize > 0f ? (cam.orthographicSize - baseOrthoSize) * 0.6f : 0f;
            if (maxRise <= 0f) return fixedY + zoomOffset;
            return fixedY + zoomOffset + Mathf.Clamp(target.position.y - floorY - RiseDeadZone, 0f, maxRise);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (cam != null && zoomTarget > 0f && !Mathf.Approximately(cam.orthographicSize, zoomTarget))
            {
                cam.orthographicSize = Mathf.MoveTowards(cam.orthographicSize, zoomTarget, zoomSpeed * Time.deltaTime);
                RecalculateBounds();
            }

            float desiredX = Mathf.Clamp(target.position.x, minX, maxX);
            Vector3 desired = new Vector3(desiredX, DesiredY(), basePosition.z);
            basePosition = Vector3.SmoothDamp(basePosition, desired, ref velocity, smoothTime);
            transform.position = basePosition;

            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                float strength = shakeMagnitude * Mathf.Clamp01(shakeTime / Mathf.Max(0.01f, shakeDuration));
                // 아래로 흔들리면 화면 아래 맵이 없는 곳(배경색)이 드러나서, 세로는 위쪽으로만 흔든다.
                var offset = Random.insideUnitCircle * strength;
                offset.y = Mathf.Abs(offset.y);
                transform.position += (Vector3)offset;
            }
        }
    }
}

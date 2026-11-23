using Unity.Cinemachine;
using UnityEngine;

namespace Aethoria.Characters
{
    // 메인 카메라가 주인공의 좌우 이동을 부드럽게 따라간다. 실제 카메라 움직임은 Cinemachine이 맡는다.
    //  - 메인 카메라에 CinemachineBrain, 따로 만든 "CM PlayerCamera"에 CinemachineCamera를 둔다.
    //  - 따라가기: CinemachineFollow(위치 감쇠)가 "CameraAnchor"를 쫓는다. 앵커의 X는 대상 X, Y는 이 스크립트가
    //    정하는 높이(바닥 기준 고정 높이 + 높은 지형에 오르면 maxRise까지 따라 오르기)다.
    //  - 맵 경계: CinemachineConfiner2D가 맵 좌우 끝을 감싸는 상자(꺼 둔 BoxCollider2D, 물리 충돌 없음) 안으로 화면을 가둔다.
    //    확대/축소하면 줄어든 화면 폭에 맞춰 자동으로 다시 잰다.
    //  - 흔들림: UpwardShake 확장이 맨 마지막 단계에서 더한다(아래로 흔들리면 맵 아래 빈 곳이 보여서 세로는 위쪽만).
    // 컷신·보스·스킬이 쓰는 공개 함수(Follow/SetTarget/ZoomTo/Shake/ShiftView 등)는 예전과 같다.
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-200)] // CinemachineBrain보다 먼저 앵커를 옮겨 둔다.
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private float smoothTime = 0.15f;
        // 예전 SmoothDamp(smoothTime)와 비슷한 체감이 되도록 Cinemachine 감쇠 시간 = smoothTime x 이 값.
        // (SmoothDamp는 smoothTime의 약 3배가 지나야 거의 다 따라붙는다.)
        private const float DampingPerSmoothTime = 3f;

        private Transform target;
        private float fixedY;
        private float floorY;
        private float maxRise;
        private float defaultSmoothTime = -1f;

        private Camera cam;
        private float baseOrthoSize = -1f;
        private float zoomTarget = -1f;
        private float zoomSpeed;
        private float shakeTime;
        private float shakeDuration;
        private float shakeMagnitude;

        private CinemachineBrain brain;
        private CinemachineCamera vcam;
        private CinemachineFollow followBody;
        private CinemachineConfiner2D confiner;
        private BoxCollider2D boundsBox;
        private UpwardShake shaker;
        private Transform anchor;

        // 바닥에서 이 높이까지는(점프 등) 카메라가 위아래로 출렁이지 않게 그대로 둔다.
        private const float RiseDeadZone = 1.5f;
        // 맵 경계 상자의 세로 크기. 세로는 가두지 않으므로(위로 따라 오르기는 maxRise가 정한다) 넉넉하게 잡는다.
        private const float BoundsHalfHeight = 500f;

        public CinemachineCamera VirtualCamera => vcam;

        private void EnsureRig()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (brain == null)
            {
                brain = GetComponent<CinemachineBrain>();
                if (brain == null) brain = gameObject.AddComponent<CinemachineBrain>();
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            }
            if (vcam != null) return;

            var rig = new GameObject("CM PlayerCamera");
            if (transform.parent != null) rig.transform.SetParent(transform.parent, false);
            vcam = rig.AddComponent<CinemachineCamera>();
            vcam.Priority = 10;

            followBody = rig.AddComponent<CinemachineFollow>();
            followBody.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;

            // 경계 상자와 앵커는 가상 카메라의 자식으로 두면 카메라가 움직일 때 같이 끌려가므로(경계가 따라다녀 맵 밖이 보였다)
            // 따로 떨어진 오브젝트로 둔다.
            var boundsGO = new GameObject("CM Bounds");
            if (transform.parent != null) boundsGO.transform.SetParent(transform.parent, false);
            boundsBox = boundsGO.AddComponent<BoxCollider2D>();
            boundsBox.isTrigger = true;
            // Confiner2D는 상자의 모양만 읽는다. 꺼 두면 몬스터·투사체·포탈 판정과 절대 부딪히지 않는다.
            boundsBox.enabled = false;

            confiner = rig.AddComponent<CinemachineConfiner2D>();
            confiner.BoundingShape2D = boundsBox;
            confiner.Damping = 0f;

            shaker = rig.AddComponent<UpwardShake>();

            var anchorGO = new GameObject("CameraAnchor");
            if (transform.parent != null) anchorGO.transform.SetParent(transform.parent, false);
            anchor = anchorGO.transform;
            vcam.Follow = anchor;
        }

        // 스테이지가 새로 지어질 때마다 GameBootstrap이 호출해 추적 대상과 맵 범위를 다시 알려준다.
        // maxRise > 0이면 플레이어가 바닥(mapFloorY)보다 높은 지형에 올라갔을 때 카메라도 최대 그만큼 따라 올라간다.
        public void Follow(Transform newTarget, float mapXMin, float mapXMax, float cameraY, float mapFloorY = 0f, float newMaxRise = 0f)
        {
            EnsureRig();
            target = newTarget;
            fixedY = cameraY;
            floorY = mapFloorY;
            maxRise = newMaxRise;

            // FitCamera가 방금 정한 크기를 Cinemachine 렌즈로 옮긴다(이후로는 Brain이 렌즈 값을 카메라에 쓴다).
            baseOrthoSize = cam.orthographicSize;
            var lens = vcam.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = baseOrthoSize;
            lens.NearClipPlane = cam.nearClipPlane;
            lens.FarClipPlane = cam.farClipPlane;
            vcam.Lens = lens;
            zoomTarget = -1f;
            shakeTime = 0f;
            shaker.Offset = Vector2.zero;

            float mapLeft = mapXMin;
            float mapRight = mapXMax + 1f; // 타일 좌표는 왼쪽 끝 기준이라 오른쪽 벽은 +1 지점
            // Confiner2D는 BoxCollider2D의 offset을 무시하고 트랜스폼 위치 + size만 읽으므로 위치로 맞춘다.
            boundsBox.transform.position = new Vector3((mapLeft + mapRight) * 0.5f, fixedY, 0f);
            boundsBox.offset = Vector2.zero;
            boundsBox.size = new Vector2(mapRight - mapLeft, BoundsHalfHeight * 2f);
            confiner.InvalidateBoundingShapeCache();

            ApplyDamping();
            UpdateAnchor();
            followBody.FollowOffset = new Vector3(0f, 0f, transform.position.z - anchor.position.z);

            // 새 스테이지에서는 미끄러져 오지 않고 바로 그 자리에서 시작한다.
            vcam.ForceCameraPosition(new Vector3(anchor.position.x, anchor.position.y, transform.position.z), Quaternion.identity);
            vcam.PreviousStateIsValid = false;

            // Brain은 이 프레임 끝에야 실제 카메라를 옮긴다. 그 전에 카메라 위치를 읽어 화면 기준을 잡는 연출(추격전 등)이
            // 이전 맵의 카메라 자리를 쓰지 않도록 지금 바로 옮겨 둔다. 맵이 화면보다 좁으면 Confiner처럼 가운데에 맞춘다.
            float halfWidth = cam.orthographicSize * cam.aspect;
            float x = mapRight - mapLeft <= halfWidth * 2f
                ? (mapLeft + mapRight) * 0.5f
                : Mathf.Clamp(anchor.position.x, mapLeft + halfWidth, mapRight - halfWidth);
            transform.position = new Vector3(x, anchor.position.y, transform.position.z);
        }

        // 맵의 좌우 끝을 바꾼다(카메라를 그 자리로 순간 이동시키지 않는다). 결전장 입구를 막아 전투 구역을 가둘 때 쓴다.
        public void SetHorizontalBounds(float mapXMin, float mapXMax)
        {
            if (boundsBox == null) return;
            float mapLeft = mapXMin, mapRight = mapXMax + 1f;
            boundsBox.transform.position = new Vector3((mapLeft + mapRight) * 0.5f, fixedY, 0f);
            boundsBox.size = new Vector2(mapRight - mapLeft, BoundsHalfHeight * 2f);
            confiner.InvalidateBoundingShapeCache();
        }

        // 보스 등장 연출처럼 잠깐 다른 대상을 비출 때 쓴다. 맵 범위/높이는 그대로 두고 따라갈 대상과
        // 따라가는 속도만 바꾼다(newSmoothTime이 크면 천천히 미끄러지듯 이동). ResetSmoothTime으로 되돌린다.
        public void SetTarget(Transform newTarget, float newSmoothTime)
        {
            if (defaultSmoothTime < 0f) defaultSmoothTime = smoothTime;
            target = newTarget;
            smoothTime = newSmoothTime;
            ApplyDamping();
        }

        public void ResetSmoothTime()
        {
            if (defaultSmoothTime >= 0f) smoothTime = defaultSmoothTime;
            ApplyDamping();
        }

        // 화면을 size(orthographicSize)까지 duration초에 걸쳐 확대/축소한다.
        public void ZoomTo(float size, float duration)
        {
            if (vcam == null || baseOrthoSize < 0f) return;
            zoomTarget = size;
            zoomSpeed = Mathf.Abs(vcam.Lens.OrthographicSize - size) / Mathf.Max(0.01f, duration);
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

        // 연출에서 카메라 높이를 잠깐 옮긴다(떨어지는 캐릭터를 따라 내려갈 때). 스테이지를 새로 지으면 Follow가 원래대로 잡는다.
        public void ShiftView(float deltaY) => fixedY += deltaY;

        private void ApplyDamping()
        {
            if (followBody == null) return;
            float d = Mathf.Max(0f, smoothTime * DampingPerSmoothTime);
            followBody.TrackerSettings.PositionDamping = new Vector3(d, d, 0f);
        }

        private float CurrentOrthoSize => vcam != null ? vcam.Lens.OrthographicSize : cam.orthographicSize;

        private float DesiredY()
        {
            // 확대하면 화면 아래 바닥 위치가 그대로 보이게, 줄어든 크기만큼(크기 x 0.6 = FitCamera와 같은 비율) 카메라를 내린다.
            float zoomOffset = baseOrthoSize > 0f ? (CurrentOrthoSize - baseOrthoSize) * 0.6f : 0f;
            if (maxRise <= 0f || target == null) return fixedY + zoomOffset;
            return fixedY + zoomOffset + Mathf.Clamp(target.position.y - floorY - RiseDeadZone, 0f, maxRise);
        }

        private void UpdateAnchor()
        {
            if (anchor == null || target == null) return;
            anchor.position = new Vector3(target.position.x, DesiredY(), 0f);
        }

        private void LateUpdate()
        {
            if (target == null || vcam == null) return;

            if (zoomTarget > 0f && !Mathf.Approximately(vcam.Lens.OrthographicSize, zoomTarget))
            {
                var lens = vcam.Lens;
                lens.OrthographicSize = Mathf.MoveTowards(lens.OrthographicSize, zoomTarget, zoomSpeed * Time.deltaTime);
                vcam.Lens = lens;
                confiner.InvalidateLensCache();
            }

            UpdateAnchor();

            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                float strength = shakeMagnitude * Mathf.Clamp01(shakeTime / Mathf.Max(0.01f, shakeDuration));
                var offset = Random.insideUnitCircle * strength;
                offset.y = Mathf.Abs(offset.y);
                shaker.Offset = offset;
            }
            else
            {
                shaker.Offset = Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            if (vcam != null) Destroy(vcam.gameObject);
            if (boundsBox != null) Destroy(boundsBox.gameObject);
            if (anchor != null) Destroy(anchor.gameObject);
        }
    }

    // CameraFollow가 정한 흔들림 값을 Cinemachine 파이프라인 마지막(Finalize) 단계에 더한다.
    // 맵 경계(Confiner2D)보다 뒤에 더해지므로 흔들림이 경계 계산에 섞여 쌓이지 않는다.
    [AddComponentMenu("")]
    public class UpwardShake : CinemachineExtension
    {
        public Vector2 Offset;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage == CinemachineCore.Stage.Finalize && Offset != Vector2.zero)
            {
                state.PositionCorrection += (Vector3)Offset;
            }
        }
    }
}

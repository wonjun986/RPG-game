using UnityEngine;
using Aethoria.Bootstrap;
using Aethoria.Characters;

namespace Aethoria.Stages
{
    // 맵 가장자리의 포탈. 보라색 소용돌이 포탈 스프라이트가 반복 재생되고, 플레이어가 일정 거리 안에
    // 들어오면 자동으로 다음(또는 이전) 스테이지(맵)로 넘어간다.
    // 되돌아가는 포탈은 입장 지점 바로 근처에 놓이는데, 플레이어가 스폰되자마자 트리거 범위 안에
    // 있으면 그대로 즉시 발동해버린다. 스폰 시점에 이미 범위 안이었다면, 한 번 벗어났다가 다시
    // 들어와야만 실제로 작동하게 해서 이 문제를 막는다.
    [RequireComponent(typeof(CircleCollider2D))]
    public class Portal : MonoBehaviour
    {
        private bool triggered;
        private bool backward;
        private bool needsExitFirst;
        private System.Action customTrigger;

        private const string FramesPath = "Art/Props/Portal";
        private const float FrameDuration = 0.12f;

        private SpriteRenderer visualRenderer;
        private Sprite[] frames;
        private int frameIndex;
        private float frameTimer;

        // 포탈 그림을 자식 오브젝트로 붙인다. 포탈은 맵 끝에서 0.5만큼 안쪽에 놓이므로 그림 중심을
        // 그대로 두면 절반 가까이 화면 밖으로 잘린다. inwardOffset만큼 맵 안쪽으로 밀어서 그린다.
        // floorOffset은 포탈 오브젝트 위치에서 바닥까지의 높이 차(스프라이트 피벗이 발판 높이에 있음).
        public void CreateVisual(float inwardOffset, float floorOffset)
        {
            frames = Resources.LoadAll<Sprite>(FramesPath);
            if (frames == null || frames.Length == 0) return;
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));

            var go = new GameObject("PortalVisual");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(backward ? inwardOffset : -inwardOffset, -floorOffset, 0f);

            visualRenderer = go.AddComponent<SpriteRenderer>();
            visualRenderer.sprite = frames[0];
            visualRenderer.sortingOrder = -5; // 배경보다 앞, 캐릭터/몬스터보다 뒤
        }

        private void Update()
        {
            if (visualRenderer == null || frames.Length < 2) return;

            frameTimer += Time.deltaTime;
            if (frameTimer < FrameDuration) return;
            frameTimer -= FrameDuration;

            frameIndex = (frameIndex + 1) % frames.Length;
            visualRenderer.sprite = frames[frameIndex];
        }

        public void Configure(bool isBackward, Transform playerTransform)
        {
            backward = isBackward;

            // Physics2D.OverlapCircleAll 등 물리 엔진 질의는, 플레이어가 스폰 스크립트로 방금
            // 옮겨진 위치가 그 시점까지 물리 엔진에 동기화되었는지(SyncTransforms 호출 여부,
            // 그 프레임이 FixedUpdate/Update 중 어디쯤인지 등)에 따라 결과가 달라질 수 있어서
            // 신뢰할 수 없었다. 대신 이미 알고 있는 플레이어 트랜스폼과의 순수 거리 계산으로
            // 판정하면 타이밍에 상관없이 항상 정확하다.
            if (playerTransform != null)
            {
                var col = GetComponent<CircleCollider2D>();
                float distance = Vector2.Distance(transform.position, playerTransform.position);
                if (distance <= col.radius)
                {
                    needsExitFirst = true;
                }
            }
        }

        // 월드맵처럼 밟는다고 바로 스테이지를 옮기지 않고 선택 UI만 띄우는 포탈에 쓴다. 설정해두면
        // OnTriggerEnter2D가 기본 이전/다음 스테이지 이동 대신 이 콜백을 대신 호출한다.
        public void SetCustomTrigger(System.Action onEnter)
        {
            customTrigger = onEnter;
        }

        // UI에서 취소하고 그대로 있기로 했을 때 호출한다. 다시 밟으면 발동하도록 풀어주되,
        // 아직 범위 안에 서 있는 채로 즉시 재발동하지 않도록 한 번 벗어났다 들어와야 하게 한다.
        public void ResetTrigger()
        {
            triggered = false;
            needsExitFirst = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (needsExitFirst && other.GetComponent<Character>() != null)
            {
                needsExitFirst = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (triggered || needsExitFirst) return;

            var character = other.GetComponent<Character>();
            if (character == null || character.IsDead) return;

            triggered = true;

            if (customTrigger != null) customTrigger();
            else if (backward) GameBootstrap.EnterPreviousStage();
            else GameBootstrap.EnterNextStage();
        }
    }
}

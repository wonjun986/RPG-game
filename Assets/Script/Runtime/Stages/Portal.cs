using UnityEngine;
using Aethoria.Bootstrap;
using Aethoria.Characters;

namespace Aethoria.Stages
{
    // 맵 가장자리의 포탈. 화면에는 아무것도 보이지 않지만, 플레이어가 일정 거리 안에 들어오면
    // 자동으로 다음(또는 이전) 스테이지(맵)로 넘어간다.
    // 되돌아가는 포탈은 입장 지점 바로 근처에 놓이는데, 플레이어가 스폰되자마자 트리거 범위 안에
    // 있으면 그대로 즉시 발동해버린다. 스폰 시점에 이미 범위 안이었다면, 한 번 벗어났다가 다시
    // 들어와야만 실제로 작동하게 해서 이 문제를 막는다.
    [RequireComponent(typeof(CircleCollider2D))]
    public class Portal : MonoBehaviour
    {
        private bool triggered;
        private bool backward;
        private bool needsExitFirst;

        public void Configure(bool isBackward)
        {
            backward = isBackward;
        }

        private void Start()
        {
            var col = GetComponent<CircleCollider2D>();
            var overlaps = Physics2D.OverlapCircleAll(transform.position, col.radius);
            foreach (var hit in overlaps)
            {
                if (hit.GetComponent<Character>() != null)
                {
                    needsExitFirst = true;
                    break;
                }
            }
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

            if (backward) GameBootstrap.EnterPreviousStage();
            else GameBootstrap.EnterNextStage();
        }
    }
}

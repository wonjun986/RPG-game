using UnityEngine;
using Aethoria.Bootstrap;
using Aethoria.Characters;

namespace Aethoria.Stages
{
    // 맵 가장자리의 포탈. 화면에는 아무것도 보이지 않지만, 플레이어가 일정 거리 안에 들어오면
    // 자동으로 새 스테이지(맵)로 넘어간다.
    [RequireComponent(typeof(CircleCollider2D))]
    public class Portal : MonoBehaviour
    {
        private bool triggered;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (triggered) return;

            var character = other.GetComponent<Character>();
            if (character == null || character.IsDead) return;

            triggered = true;
            GameBootstrap.EnterNextStage();
        }
    }
}

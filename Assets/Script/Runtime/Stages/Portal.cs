using UnityEngine;
using Aethoria.Bootstrap;
using Aethoria.Characters;

namespace Aethoria.Stages
{
    // 맵 가장자리의 포탈. 플레이어가 닿으면 새 스테이지(맵)로 넘어간다.
    [RequireComponent(typeof(BoxCollider2D))]
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

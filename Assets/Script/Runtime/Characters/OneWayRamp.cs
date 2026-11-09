using System.Collections.Generic;
using UnityEngine;

namespace Aethoria.Characters
{
    // 배경 그림 속 계단처럼 비스듬히 올라가는 단방향 길. 물리 콜라이더로 경사를 만들면 박스 몸이 모서리로 걸쳐 떠 보이고
    // 착지 판정(발밑 박스 검사)도 맞지 않아서, CharacterMovement2D가 이 선분 높이에 발을 직접 붙인다.
    // 아래에서 점프하면 그대로 뚫고 올라가고, 아래 방향키로 내려올 수 있다(단방향 발판과 같은 규칙).
    public class OneWayRamp : MonoBehaviour
    {
        private static readonly List<OneWayRamp> active = new List<OneWayRamp>();
        public static IReadOnlyList<OneWayRamp> Active => active;

        // 월드 좌표의 양 끝(아래쪽 끝, 위쪽 끝 순서는 상관없다).
        public Vector2 PointA { get; private set; }
        public Vector2 PointB { get; private set; }

        public void Initialize(Vector2 a, Vector2 b)
        {
            if (a.x > b.x) (a, b) = (b, a);
            PointA = a;
            PointB = b;
        }

        // x 위치에서의 길 높이. 길의 가로 범위 밖이면 false.
        public bool TryGetSurfaceY(float x, out float y)
        {
            y = 0f;
            if (x < PointA.x || x > PointB.x || PointB.x - PointA.x < 0.001f) return false;
            y = Mathf.Lerp(PointA.y, PointB.y, (x - PointA.x) / (PointB.x - PointA.x));
            return true;
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() => active.Remove(this);
    }
}

using UnityEngine;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 보스 슬라임이 쏘는 물대포. 옆으로 곧게 날아가다가 플레이어에게 맞으면 피해를 주고 사라진다.
    // 사거리를 다 날아가도 사라진다. 그림은 FlipbookSpriteEffect(2프레임 반복)가 그린다.
    public class SlimeWaterBall : MonoBehaviour
    {
        private float damageAttack;
        private Vector2 direction;
        private float speed;
        private float remainingDistance;
        private float hitRadius;
        private Vector2 hitOffset;

        public static void Launch(Vector2 bottomCenter, Vector2 direction, float attackPower, float speed, float range, float hitRadius)
        {
            var effect = FlipbookSpriteEffect.Create("SlimeWaterBall", "Art/Effects/SlimeWaterBall", bottomCenter, 0.08f, loop: true, sortingOrder: 3);
            var spriteRenderer = effect.GetComponent<SpriteRenderer>();
            spriteRenderer.flipX = direction.x < 0f;

            var ball = effect.gameObject.AddComponent<SlimeWaterBall>();
            ball.damageAttack = attackPower;
            ball.direction = direction.normalized;
            ball.speed = speed;
            ball.remainingDistance = range;
            ball.hitRadius = hitRadius;

            // 그림은 뒤로 물보라 꼬리가 길고, 실제로 맞는 물구슬은 진행 방향 앞쪽(그림 폭의 약 3/4 지점)에 있다.
            // 피벗(하단 중앙) 기준으로 그 물구슬 위치를 판정 중심으로 쓴다. 높이는 그림 세로 중앙.
            var sprite = Resources.Load<Sprite>("Art/Effects/SlimeWaterBall/waterball_1");
            Vector2 size = sprite != null ? (Vector2)sprite.bounds.size : new Vector2(6f, 2f);
            ball.hitOffset = new Vector2(ball.direction.x * size.x * 0.25f, size.y * 0.5f);
        }

        private void Update()
        {
            float step = speed * Time.deltaTime;
            transform.position += (Vector3)(direction * step);
            remainingDistance -= step;

            Vector2 hitCenter = (Vector2)transform.position + hitOffset;
            foreach (var hit in Physics2D.OverlapCircleAll(hitCenter, hitRadius))
            {
                var character = hit.GetComponent<Character>();
                if (character == null || character.IsDead) continue;

                character.TakeDamage(CombatMath.PhysicalDamage(damageAttack, character.Stats.defense));
                Destroy(gameObject);
                return;
            }

            if (remainingDistance <= 0f) Destroy(gameObject);
        }
    }
}

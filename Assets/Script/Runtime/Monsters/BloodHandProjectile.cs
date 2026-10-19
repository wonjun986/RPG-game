using UnityEngine;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 루나벨이 손을 뻗어 날리는 피의 손. 갈퀴를 앞세워 옆으로 곧게 날아가다가 플레이어에게 맞으면 피해를 주고 사라진다.
    // 사거리를 다 날아가도 사라진다. 그림(Art/Effects/BloodHand, 3프레임 반복)은 갈퀴가 오른쪽을 향해 있다.
    public class BloodHandProjectile : MonoBehaviour
    {
        public const string FramesPath = "Art/Effects/BloodHand";

        private float damageAttack;
        private Vector2 direction;
        private float speed;
        private float remainingDistance;
        private float hitRadius;
        private Vector2 hitOffset;

        public static void Launch(Vector2 bottomCenter, Vector2 direction, float attackPower, float speed, float range, float hitRadius)
        {
            var effect = FlipbookSpriteEffect.Create("BloodHand", FramesPath, bottomCenter, 0.07f, loop: true, sortingOrder: 3);
            effect.GetComponent<SpriteRenderer>().flipX = direction.x < 0f;

            var hand = effect.gameObject.AddComponent<BloodHandProjectile>();
            hand.damageAttack = attackPower;
            hand.direction = direction.normalized;
            hand.speed = speed;
            hand.remainingDistance = range;
            hand.hitRadius = hitRadius;

            // 실제로 할퀴는 갈퀴 끝은 진행 방향 앞쪽(그림 폭의 약 1/4 앞)에 있다. 높이는 그림 세로 중앙.
            var sprite = Resources.Load<Sprite>(FramesPath + "/bloodhand_1");
            Vector2 size = sprite != null ? (Vector2)sprite.bounds.size : new Vector2(0.8f, 1.4f);
            hand.hitOffset = new Vector2(hand.direction.x * size.x * 0.25f, size.y * 0.5f);
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

                character.TakeDamage(CombatMath.PhysicalDamage(damageAttack, character.Stats.defense), transform.position);
                Destroy(gameObject);
                return;
            }

            if (remainingDistance <= 0f) Destroy(gameObject);
        }
    }
}

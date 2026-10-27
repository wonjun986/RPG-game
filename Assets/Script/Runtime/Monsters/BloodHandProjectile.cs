using UnityEngine;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 루나벨이 손을 뻗어 날리는 피의 손. 갈퀴를 앞세워 옆으로 곧게 날아가며 플레이어 몸을 꿰뚫고 지나가는데,
    // 닿아 있는 동안 피격 무적 없이 hitInterval마다 계속 할퀸다(연타). 사거리를 다 날아가면 사라진다. 그림(Art/Effects/BloodHand, 3프레임 반복)은 갈퀴가 오른쪽을 향해 있다.
    public class BloodHandProjectile : MonoBehaviour
    {
        public const string FramesPath = "Art/Effects/BloodHand";

        private float damageAttack;
        private Vector2 direction;
        private float speed;
        private float remainingDistance;
        private float hitRadius;
        private Vector2 hitOffset;
        private float hitInterval;
        private float hitTimer;

        public static void Launch(Vector2 bottomCenter, Vector2 direction, float attackPower, float speed, float range, float hitRadius,
            float hitInterval = 0.08f)
        {
            var effect = FlipbookSpriteEffect.Create("BloodHand", FramesPath, bottomCenter, 0.07f, loop: true, sortingOrder: 3);
            effect.GetComponent<SpriteRenderer>().flipX = direction.x < 0f;

            var hand = effect.gameObject.AddComponent<BloodHandProjectile>();
            hand.damageAttack = attackPower;
            hand.direction = direction.normalized;
            hand.speed = speed;
            hand.remainingDistance = range;
            hand.hitRadius = hitRadius;
            hand.hitInterval = hitInterval;

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

            // 플레이어 충돌 판정은 발밑(높이 0.5)뿐이라 물리 질의로는 허리 높이로 날아가는 손이 그 위로 스쳐 지나갔다.
            // 그림 속 몸통(발밑에서 BodyHeight까지)과 손 판정 원이 겹치는지 직접 잰다.
            if (target == null) target = Object.FindAnyObjectByType<Character>();
            if (hitTimer > 0f) hitTimer -= Time.deltaTime;
            if (hitTimer <= 0f && target != null && !target.IsDead && TouchesBody(target.transform.position))
            {
                hitTimer = hitInterval;
                target.TakeDamage(CombatMath.PhysicalDamage(damageAttack, target.Stats.defense), transform.position, hitReaction: false);
            }

            if (remainingDistance <= 0f) Destroy(gameObject);
        }

        private const float BodyWidth = 0.6f;
        private const float BodyHeight = 1.5f; // 네크로시아 그림 키
        private Character target;

        private bool TouchesBody(Vector2 feet)
        {
            Vector2 hitCenter = (Vector2)transform.position + hitOffset;
            float closestX = Mathf.Clamp(hitCenter.x, feet.x - BodyWidth * 0.5f, feet.x + BodyWidth * 0.5f);
            float closestY = Mathf.Clamp(hitCenter.y, feet.y, feet.y + BodyHeight);
            return (new Vector2(closestX, closestY) - hitCenter).sqrMagnitude <= hitRadius * hitRadius;
        }
    }
}

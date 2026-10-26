using UnityEngine;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 황금 모래룡이 포효하며 일으키는 거대한 모래 회오리. 솟은 자리에서 정해진 방향으로 밀려 나가고,
    // 회오리 기둥 안에 있으면 일정 간격으로 피해를 입는다. 그림은 화면 안에 깔때기 끝까지 보이는 키(height)로 세우고,
    // 맞는 범위(hitHeight)는 그보다 조금 높게 잡아 땅에서는 점프로 못 넘고 대시 무적으로만 뚫고 지나갈 수 있다.
    // 그림(Art/Effects/SandWhirlwind, 6프레임 반복)은 피벗이 아래 가운데라 바닥 높이에 세운다.
    public class SandWhirlwind : MonoBehaviour
    {
        public const string FramesPath = "Art/Effects/SandWhirlwind";
        private const float FadeTime = 0.4f;

        private float damageAttack;
        private float damageMultiplier;
        private float direction;
        private float speed;
        private float lifetime;
        private float hitInterval;
        private Vector2 hitSize;
        private float age;
        private float hitTimer;
        private SpriteRenderer spriteRenderer;

        public static void Spawn(Vector2 bottomCenter, float direction, float attackPower, float damageMultiplier,
            float speed, float lifetime, float hitInterval, float hitWidth, float height, float hitHeight, string framesPath = FramesPath)
        {
            // 루나벨의 피의 회오리처럼 그림만 다른 회오리도 framesPath로 같은 동작을 쓴다.
            var effect = FlipbookSpriteEffect.Create("Whirlwind", framesPath, bottomCenter, 0.07f, loop: true, sortingOrder: 4);
            // 그림 키가 height가 되도록 비율 그대로 키운다(만들자마자 첫 장이 들어가 있다).
            var firstFrame = effect.GetComponent<SpriteRenderer>().sprite;
            float nativeHeight = firstFrame != null ? firstFrame.bounds.size.y : height;
            float scale = height / nativeHeight;
            effect.transform.localScale = new Vector3(scale, scale, 1f);
            var whirl = effect.gameObject.AddComponent<SandWhirlwind>();
            whirl.spriteRenderer = effect.GetComponent<SpriteRenderer>();
            whirl.spriteRenderer.flipX = direction < 0f;
            whirl.spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
            whirl.damageAttack = attackPower;
            whirl.damageMultiplier = damageMultiplier;
            whirl.direction = Mathf.Sign(direction);
            whirl.speed = speed;
            whirl.lifetime = lifetime;
            whirl.hitInterval = hitInterval;
            whirl.hitSize = new Vector2(hitWidth, hitHeight);
            whirl.hitTimer = FadeTime; // 다 솟아오른 뒤부터 맞는다
        }

        private void Update()
        {
            age += Time.deltaTime;
            transform.position += new Vector3(direction * speed * Time.deltaTime, 0f, 0f);

            // 솟아오를 때와 흩어질 때 서서히 나타나고 사라진다.
            float alpha = Mathf.Min(1f, age / FadeTime, (lifetime - age) / FadeTime);
            spriteRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            hitTimer -= Time.deltaTime;
            if (hitTimer > 0f || age > lifetime - FadeTime) return;

            Vector2 center = (Vector2)transform.position + Vector2.up * (hitSize.y * 0.5f);
            foreach (var hit in Physics2D.OverlapBoxAll(center, hitSize, 0f))
            {
                var character = hit.GetComponent<Character>();
                if (character == null || character.IsDead) continue;

                hitTimer = hitInterval;
                float damage = CombatMath.Finish(CombatMath.BaseDamage(damageAttack, character.Stats.defense) * damageMultiplier);
                character.TakeDamage(damage, transform.position);
                return;
            }
        }
    }
}

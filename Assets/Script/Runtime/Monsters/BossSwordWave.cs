using UnityEngine;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 보스 기사 알리아스가 검을 휘둘러 날리는 검기. 바닥에서 보랏빛 칼날이 솟구친 뒤(1번째 장) 땅을 타고
    // 앞으로 미끄러져 나가며(2~6번째 장 반복), 플레이어 몸에 닿으면 한 번 피해를 주고 사라진다.
    // 키가 낮지 않아 점프 한 번으로는 넘기 어렵고, 이단 점프나 대시로 피한다.
    // 그림(Art/Effects/BossSwordWave)은 칼날이 오른쪽으로 휘어 있고 피벗이 아래 가운데다.
    public class BossSwordWave : MonoBehaviour
    {
        private const float FrameDuration = 0.06f;
        private const float BodyHeight = 1.5f; // 네크로시아 그림 키(충돌 판정은 발밑뿐이라 몸통을 직접 잰다)

        private Sprite[] frames;
        private SpriteRenderer spriteRenderer;
        private Character target;
        private float damageAttack;
        private float direction;
        private float speed;
        private float remainingDistance;
        private Vector2 hitSize;
        private float frameTimer;
        private int frameIndex;
        private float fadeOut = -1f;
        // 0이 아니면 바닥을 타지 않고 이 방향(정규화)으로 곧게 날아간다(높은 발판 위 플레이어를 노리는 대각선 검기).
        private Vector2 aimDirection;

        public static void Launch(Vector2 bottomCenter, float direction, float attackPower, float speed, float range, float scale)
        {
            var loaded = Resources.LoadAll<Sprite>(BossAI.SwordWavePath);
            if (loaded == null || loaded.Length == 0) return;
            System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));

            var go = new GameObject("BossSwordWave");
            go.transform.position = bottomCenter;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var wave = go.AddComponent<BossSwordWave>();
            wave.frames = loaded;
            wave.spriteRenderer = go.AddComponent<SpriteRenderer>();
            wave.spriteRenderer.sprite = loaded[0];
            wave.spriteRenderer.sortingOrder = 4;
            wave.spriteRenderer.flipX = direction < 0f;
            wave.damageAttack = attackPower;
            wave.direction = Mathf.Sign(direction);
            wave.speed = speed;
            wave.remainingDistance = range;
            // 칼날 몸통(그림 폭의 절반쯤, 키의 대부분)만 맞는 범위로 친다.
            Vector2 size = loaded[1 % loaded.Length].bounds.size * scale;
            wave.hitSize = new Vector2(size.x * 0.5f, size.y * 0.85f);
            wave.target = Object.FindAnyObjectByType<Character>();
            lastLaunched = wave;
        }

        // 대각선 검기: 검을 휘두른 자리에서 플레이어 몸 쪽으로 비스듬히 날아간다. 칼날 그림을 날아가는 방향으로 기울이고,
        // 왼쪽으로 날아갈 때는 칼날이 뒤집혀 보이지 않게 위아래를 뒤집는다.
        public static void LaunchAimed(Vector2 origin, Vector2 aim, float attackPower, float speed, float range, float scale)
        {
            if (aim.sqrMagnitude < 0.0001f) return;
            Launch(origin, aim.x, attackPower, speed, range, scale);
            var wave = lastLaunched;
            if (wave == null) return;
            wave.aimDirection = aim.normalized;
            wave.spriteRenderer.flipX = false;
            wave.spriteRenderer.flipY = aim.x < 0f;
            wave.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        }

        private static BossSwordWave lastLaunched;

        private void Update()
        {
            // 첫 장(솟구침) 뒤로는 날아가는 장들만 돌린다.
            frameTimer += Time.deltaTime;
            if (frameTimer >= FrameDuration)
            {
                frameTimer -= FrameDuration;
                frameIndex = frameIndex + 1 < frames.Length ? frameIndex + 1 : Mathf.Min(1, frames.Length - 1);
                spriteRenderer.sprite = frames[frameIndex];
            }

            if (fadeOut >= 0f)
            {
                fadeOut += Time.deltaTime;
                spriteRenderer.color = new Color(1f, 1f, 1f, 1f - fadeOut / 0.2f);
                if (fadeOut >= 0.2f) Destroy(gameObject);
                return;
            }

            // 솟구치는 첫 장 동안은 제자리, 그다음부터 미끄러져 나간다.
            if (frameIndex > 0)
            {
                float step = speed * Time.deltaTime;
                Vector2 move = aimDirection != Vector2.zero ? aimDirection : new Vector2(direction, 0f);
                transform.position += (Vector3)(move * step);
                remainingDistance -= step;
                if (remainingDistance <= 0f) fadeOut = 0f;
            }

            if (target == null || target.IsDead) return;
            Vector2 feet = target.transform.position;
            if (aimDirection != Vector2.zero)
            {
                // 기울어진 칼날 한가운데를 원으로 보고, 플레이어 몸통(발밑~키)과 겹치는지 잰다.
                Vector2 bladeCenter = (Vector2)transform.TransformPoint(new Vector3(0f, spriteRenderer.sprite.bounds.size.y * 0.5f, 0f));
                float radius = Mathf.Min(hitSize.x, hitSize.y) * 0.5f;
                Vector2 closest = new Vector2(Mathf.Clamp(bladeCenter.x, feet.x - 0.3f, feet.x + 0.3f),
                    Mathf.Clamp(bladeCenter.y, feet.y, feet.y + BodyHeight));
                if ((closest - bladeCenter).sqrMagnitude <= radius * radius)
                {
                    target.TakeDamage(CombatMath.PhysicalDamage(damageAttack, target.Stats.defense), bladeCenter);
                    fadeOut = 0f;
                }
                return;
            }
            Vector2 center = (Vector2)transform.position + new Vector2(direction * hitSize.x * 0.2f, hitSize.y * 0.5f);
            bool overlapX = Mathf.Abs(feet.x - center.x) <= hitSize.x * 0.5f + 0.3f;
            bool overlapY = feet.y <= transform.position.y + hitSize.y && feet.y + BodyHeight >= transform.position.y;
            if (overlapX && overlapY)
            {
                target.TakeDamage(CombatMath.PhysicalDamage(damageAttack, target.Stats.defense), transform.position);
                fadeOut = 0f;
            }
        }
    }
}

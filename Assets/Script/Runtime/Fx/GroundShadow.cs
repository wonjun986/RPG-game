using UnityEngine;

namespace Aethoria.Fx
{
    // 발밑 그림자: 캐릭터·몬스터 바로 아래 땅(바닥, 발판, 계단)에 부드러운 타원 그림자를 깐다. 그림자가 없으면 일러스트 배경 위에서
    // 캐릭터가 붙여 놓은 그림처럼 떠 보였다. 점프하거나 공중에 뜨면 땅에 남은 채 작아지고 옅어진다.
    // 그림자는 주인의 자식이 아니라 따로 둔다(주인이 좌우로 뒤집히거나 공격 모션마다 크기가 바뀌어도 그림자는 그대로).
    public class GroundShadow : MonoBehaviour
    {
        private const float MaxDistance = 6f;      // 이보다 높이 뜨면 그림자를 숨긴다
        private const float FadeDistance = 3.5f;   // 이만큼 뜨면 거의 사라진다
        private const float BaseAlpha = 0.42f;
        private const float Flatness = 0.2f;       // 세로/가로
        private const int SortingOrder = -4;       // 발판(-5)·바닥(-9) 위, 캐릭터 아래

        private Collider2D body;
        private SpriteRenderer ownerRenderer;
        private Aethoria.Monsters.Monster monster;
        private Transform shadow;
        private SpriteRenderer shadowRenderer;
        private float width;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[12];

        // widthScale: 몸 폭에 곱할 그림자 폭(큰 보스는 몸통이 넓어 조금 줄인다).
        public static GroundShadow Attach(GameObject owner, float widthScale = 1.15f)
        {
            var existing = owner.GetComponent<GroundShadow>();
            if (existing != null) return existing;
            var s = owner.AddComponent<GroundShadow>();
            s.widthScale = widthScale;
            return s;
        }

        private float widthScale = 1.15f;

        private void Start()
        {
            body = GetComponent<Collider2D>();
            monster = GetComponent<Aethoria.Monsters.Monster>();
            var visual = transform.Find("Visual");
            ownerRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();
            width = Mathf.Clamp((body != null ? body.bounds.size.x : 0.6f) * widthScale, 0.5f, 4f);

            var go = new GameObject(name + "_Shadow", typeof(SpriteRenderer));
            shadow = go.transform;
            shadowRenderer = go.GetComponent<SpriteRenderer>();
            shadowRenderer.sprite = FxUtil.SoftCircleSprite;
            shadowRenderer.sortingOrder = SortingOrder;
            shadowRenderer.color = new Color(0f, 0f, 0f, 0f);
        }

        private void LateUpdate()
        {
            if (shadow == null) return;
            bool visible = ownerRenderer == null || (ownerRenderer.enabled && ownerRenderer.color.a > 0.05f && ownerRenderer.gameObject.activeInHierarchy);
            if (monster != null && (monster.IsDead || monster.IsMinigameOpponent)) visible = false;

            Vector2 feet = body != null ? new Vector2(body.bounds.center.x, body.bounds.min.y) : (Vector2)transform.position;
            Vector2 origin = feet + Vector2.up * 0.15f;
            float ground = float.NaN;
            if (visible)
            {
                int n = Physics2D.RaycastNonAlloc(origin, Vector2.down, hits, MaxDistance);
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    var c = hits[i].collider;
                    if (c == null || c.isTrigger) continue;
                    if (c.transform.IsChildOf(transform)) continue;
                    if (c.GetComponentInParent<Aethoria.Characters.Character>() != null) continue;
                    if (c.GetComponentInParent<Aethoria.Monsters.Monster>() != null) continue;
                    if (hits[i].distance < best) { best = hits[i].distance; ground = hits[i].point.y; }
                }
            }

            if (float.IsNaN(ground))
            {
                shadowRenderer.color = new Color(0f, 0f, 0f, 0f);
                return;
            }

            float height = Mathf.Max(0f, feet.y - ground);
            float k = Mathf.Clamp01(1f - height / FadeDistance);
            float w = width * Mathf.Lerp(0.55f, 1f, k);
            shadow.position = new Vector3(feet.x, ground + 0.02f, 0f);
            float spriteWidth = shadowRenderer.sprite != null ? shadowRenderer.sprite.bounds.size.x : 1f;
            shadow.localScale = new Vector3(w / spriteWidth, w * Flatness / spriteWidth, 1f);
            shadowRenderer.color = new Color(0f, 0f, 0f, BaseAlpha * k);
        }

        private void OnDestroy()
        {
            if (shadow != null) Destroy(shadow.gameObject);
        }

        private void OnDisable()
        {
            if (shadowRenderer != null) shadowRenderer.color = new Color(0f, 0f, 0f, 0f);
        }
    }
}

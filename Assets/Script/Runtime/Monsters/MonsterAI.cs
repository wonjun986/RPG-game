using UnityEngine;
using Aethoria.Characters;
using Aethoria.UI;

namespace Aethoria.Monsters
{
    // 일반 몬스터용 간단한 AI. 플레이어를 향해 다가가다가 근접 사거리에 들어오면
    // 더 이상 실시간으로 때리지 않고, 리듬전투 화면(RhythmBattleUI)으로 넘긴다.
    [RequireComponent(typeof(Monster))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class MonsterAI : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float attackRange = 0.9f;
        [SerializeField] private float gravityScale = 4f;
        // 플레이어가 이만큼 위에 있으면(점프로 넘어가는 중) 접촉으로 치지 않는다 — 싸우기 싫으면 뛰어넘어 지나갈 수 있게.
        [SerializeField] private float jumpOverHeight = 2f;

        private Monster monster;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Character target;
        private RhythmBattleUI rhythmBattleUI;

        private void Awake()
        {
            monster = GetComponent<Monster>();
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            body.gravityScale = gravityScale;
            body.freezeRotation = true;

            target = Object.FindFirstObjectByType<Character>();
            rhythmBattleUI = Object.FindFirstObjectByType<RhythmBattleUI>();
        }

        private void FixedUpdate()
        {
            if (monster.IsDead || monster.IsImmobilized || target == null || target.IsDead)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            float distance = target.transform.position.x - transform.position.x;
            bool playerJumpedOver = target.transform.position.y - transform.position.y > jumpOverHeight;

            if (!playerJumpedOver && Mathf.Abs(distance) <= attackRange)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                spriteRenderer.flipX = distance < 0f;
                if (rhythmBattleUI != null) rhythmBattleUI.TryBegin(monster, target);
            }
            else
            {
                float direction = Mathf.Sign(distance);
                body.linearVelocity = new Vector2(direction * moveSpeed, body.linearVelocity.y);
                spriteRenderer.flipX = direction < 0f;
            }
        }
    }
}

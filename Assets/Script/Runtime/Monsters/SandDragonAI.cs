using UnityEngine;
using Aethoria.Characters;
using Aethoria.UI;

namespace Aethoria.Monsters
{
    // 잊혀진 폐허의 필드 보스 황금 모래룡. 마지막 폐허 맵 오른쪽 끝 모래 속에 똬리를 틀고 있다가, 플레이어가 감지 거리 안에
    // 들어오면 모래 물결을 타고 천천히 미끄러져 다가온다. 몸에 닿으면 실시간으로 깎지 않고 리듬전투(보스 패턴)로 넘긴다.
    // 대지 강타(Attack 6프레임)와 꼬리 대지 폭발은 리듬전투 화면에서만 쓴다(RhythmBattleUI.SandDragonTailStrike).
    [RequireComponent(typeof(Monster))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class SandDragonAI : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float detectRange = 11f; // 이 거리 밖이면 제자리에서 꿈틀거리기만 한다
        [SerializeField] private float keepDistance = 1.4f;
        [SerializeField] private float gravityScale = 4f;
        [SerializeField] private float contactRange = 2f;

        private Monster monster;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Character target;
        private RhythmBattleUI rhythmBattleUI;
        // 등장 연출(SandDragonIntroCutscene) 동안에는 움직이지도, 닿아도 전투를 열지도 않는다(연출이 직접 연다).
        private bool heldForIntro;

        public void HoldForIntro(bool hold)
        {
            heldForIntro = hold;
            body.linearVelocity = Vector2.zero;
            body.bodyType = hold ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        }

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

        private void Update()
        {
            if (heldForIntro || monster.IsDead || target == null || target.IsDead) return;
            TryContactBattle();
        }

        private void FixedUpdate()
        {
            if (heldForIntro || monster.IsDead || monster.IsImmobilized || target == null || target.IsDead)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            float dx = target.transform.position.x - transform.position.x;
            float distance = Mathf.Abs(dx);
            if (distance > detectRange)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            // 원화가 오른쪽을 보고 있어서, 플레이어가 왼쪽에 있으면 뒤집는다.
            spriteRenderer.flipX = dx < 0f;

            float move = distance > keepDistance ? Mathf.Sign(dx) * moveSpeed : 0f;
            body.linearVelocity = new Vector2(move, body.linearVelocity.y);
        }

        private void TryContactBattle()
        {
            if (rhythmBattleUI == null) return;

            Vector2 offset = target.transform.position - transform.position;
            if (Mathf.Abs(offset.x) > contactRange || offset.y > 2.4f || offset.y < -0.5f) return;

            rhythmBattleUI.TryBegin(monster, target);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, contactRange);
        }
    }
}

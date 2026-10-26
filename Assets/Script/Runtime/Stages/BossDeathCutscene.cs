using System.Collections;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.Monsters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 보스 기사 사망 연출. 체력이 0이 되면 보스가 멈춰 선 뒤 잠깐 쉬었다가, 대기 모션으로
    // 선 채 유언을 남기고(보스 대화창, [Z]로 넘김) 쓰러지는 모션(Art/Monsters/BossKnight/Die 8프레임)을 재생한다.
    // 마지막 프레임에서 잠깐 멈췄다가 서서히 사라지고, 그다음 onFinished(스테이지 클리어 창)를 부른 뒤 보스를 지운다.
    // 보상(경험치/골드/드랍/퀘스트)은 쓰러진 순간 Monster.OnDied/AnyDied로 이미 처리된다.
    [RequireComponent(typeof(Monster))]
    public class BossDeathCutscene : MonoBehaviour
    {
        private const string DiePath = "Art/Monsters/BossKnight/Die";
        private const float DelayAfterBattle = 0.5f;
        private const float DieFrameInterval = 0.16f;
        private const float HoldLastFrame = 1.2f;
        private const float FadeOutDuration = 0.8f;

        private static readonly string[] LastWords =
        {
            "...크윽. 이 내가... 무릎을 꿇다니.",
            "훌륭하다... 그 낫이라면, 이 숲 너머의 어둠도... 베어낼 수 있겠지.",
            "부디... 그분을... 막아다오...",
        };

        private Monster monster;
        private Character player;
        private CharacterMovement2D playerMovement;
        private NpcDialogueUI dialogueUI;
        private System.Action onFinished;
        private SpriteRenderer spriteRenderer;
        private Sprite[] dieFrames;
        private bool running;
        private bool finished;

        public void Initialize(Character targetPlayer, NpcDialogueUI dialogue, System.Action finishedCallback)
        {
            player = targetPlayer;
            playerMovement = player != null ? player.GetComponent<CharacterMovement2D>() : null;
            dialogueUI = dialogue;
            onFinished = finishedCallback;

            monster = GetComponent<Monster>();
            monster.DestroyOnDeath = false;
            monster.OnDied += HandleDied;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            dieFrames = Resources.LoadAll<Sprite>(DiePath);
            System.Array.Sort(dieFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private void HandleDied(Monster _)
        {
            if (running) return;
            running = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // 쓰러진 보스가 더 움직이거나(슈퍼아머 깜빡임 등) 부딪히지 않게 멈춰 세운다.
            var bossAI = GetComponent<BossAI>();
            if (bossAI != null)
            {
                bossAI.StopAllCoroutines();
                bossAI.enabled = false;
            }
            var attackAnimator = GetComponent<MonsterAttackSpriteAnimator>();
            if (attackAnimator != null) attackAnimator.Stop();
            var body = GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
            foreach (var col in GetComponents<Collider2D>()) col.enabled = false;
            if (body != null) body.bodyType = RigidbodyType2D.Kinematic;
            spriteRenderer.color = Color.white;
            HideHealthBar();

            yield return new WaitForSeconds(DelayAfterBattle);

            if (playerMovement != null)
            {
                playerMovement.SetCutsceneLocked(true);
                playerMovement.Face(transform.position.x >= player.transform.position.x ? Vector2.right : Vector2.left);
            }
            spriteRenderer.flipX = player != null && player.transform.position.x < transform.position.x;

            // 유언: 대기 모션(MonsterWalkSpriteAnimator의 Idle)으로 선 채 말한다.
            for (int i = 0; i < LastWords.Length; i++)
            {
                BossIntroCutscene.ShowBossLine(dialogueUI, LastWords[i]);
                yield return null;
                while (!BossIntroCutscene.AdvanceKeyPressed()) yield return null;
            }
            dialogueUI?.Hide();

            // 쓰러지는 모션: 대기 애니메이터를 끄고 사망 프레임을 한 번 재생한 뒤 마지막 프레임에 멈춘다.
            var walkAnimator = GetComponent<MonsterWalkSpriteAnimator>();
            if (walkAnimator != null) walkAnimator.enabled = false;
            foreach (var frame in dieFrames)
            {
                spriteRenderer.sprite = frame;
                yield return new WaitForSeconds(DieFrameInterval);
            }
            yield return new WaitForSeconds(HoldLastFrame);

            for (float t = 0f; t < FadeOutDuration; t += Time.deltaTime)
            {
                spriteRenderer.color = new Color(1f, 1f, 1f, 1f - t / FadeOutDuration);
                yield return null;
            }

            Finish();
            onFinished?.Invoke();
            Destroy(gameObject);
        }

        private void HideHealthBar()
        {
            var bar = GetComponent<MonsterHealthBar>();
            if (bar != null) bar.enabled = false;
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("HealthBar")) child.gameObject.SetActive(false);
            }
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (playerMovement != null) playerMovement.SetCutsceneLocked(false);
        }

        // 연출 도중 스테이지가 다시 지어지면(사망 등) 보스와 함께 지워지므로, 막아 둔 것들을 풀고 나간다.
        private void OnDestroy()
        {
            if (monster != null) monster.OnDied -= HandleDied;
            if (!running || finished) return;
            dialogueUI?.Hide();
            Finish();
        }
    }
}

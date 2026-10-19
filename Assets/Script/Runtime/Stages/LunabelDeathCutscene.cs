using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Monsters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 루나벨(최종보스) 처치 연출. 2페이즈 체력이 0이 되는 순간 루나벨을 네크로시아 바로 앞으로 옮기고 패배 모션의 첫 프레임으로
    // 바꿔 둔다(전투가 열린 자리는 언덕 왼쪽이라 원래 자리에 두면 화면에 안 보이고, 2페이즈 모습이 먼저 비치는 것도 막는다).
    // 전투 화면이 닫혀 필드로 돌아오면:
    //   1) 패배 모션(Art/Monsters/FinalBoss/Defeat, Lunabel_Defeat 8프레임)을 끝까지 재생해 쓰러지고,
    //   2) 쓰러진 채 네크로시아와 마지막 대화를 나눈 뒤([Z]/Enter로 넘김),
    //   3) 서서히 사라지고,
    //   4) onFinished(마을 노아 앞으로 돌아가기)를 부른다.
    // 보상(경험치/골드/드랍/퀘스트)은 쓰러진 순간 Monster.OnDied/AnyDied로 이미 처리된다.
    [RequireComponent(typeof(Monster))]
    public class LunabelDeathCutscene : MonoBehaviour
    {
        public const string DefeatPath = "Art/Monsters/FinalBoss/Defeat";
        private const float DelayAfterBattle = 0.4f;
        public const float DefeatFrameInterval = 0.16f;
        public const float HoldAfterFall = 0.6f;
        public const float FadeOutDuration = 1.5f;
        private const float DelayBeforeReturn = 0.8f;
        // 네크로시아 앞 이만큼(유닛) 떨어진 자리에 루나벨을 세운다. 맵 끝에 걸리면 반대쪽에 세운다.
        private const float StandDistance = 2.2f;
        private const float MapEdgeMargin = 1f;
        private const string LunabelNextHint = "   <color=#E07A8A>[Z]</color>";
        private const string PlayerNextHint = "   <color=#B98CE8>[Z]</color>";

        public readonly struct Line
        {
            public readonly bool IsNecrosia;
            public readonly string Text;
            public Line(bool isNecrosia, string text) { IsNecrosia = isNecrosia; Text = text; }
        }

        // 타이틀 EXTRAS의 "루나벨 처치 장면"(LunabelDefeatReplayUI)도 같은 대사를 쓴다.
        public static readonly Line[] LastWords =
        {
            new Line(false, "...하, 하하... 이 내가... 무릎을 꿇다니."),
            new Line(true, "그날 무너졌던 리듬, 이제 전부 되찾았다."),
            new Line(false, "네 박자... 끝까지 한 번도 흐트러지지 않더라. 그날 밤의 너와는... 전혀 달라."),
            new Line(true, "빼앗긴 힘도, 이름도, 영광도. 이제 다시 내 것이다."),
            new Line(false, "후후... 축하해, 사신. 하지만 기억해 둬... 붉은 달은... 언젠가 다시... 떠오를 거야..."),
            new Line(true, "그때도 베어 주지. 몇 번이고."),
        };

        private Monster monster;
        private Character player;
        private CharacterMovement2D playerMovement;
        private NpcDialogueUI dialogueUI;
        private System.Action onFinished;
        private SpriteRenderer spriteRenderer;
        private Sprite[] defeatFrames;
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
            defeatFrames = Resources.LoadAll<Sprite>(DefeatPath);
            System.Array.Sort(defeatFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private void HandleDied(Monster _)
        {
            if (running) return;
            running = true;
            Freeze();
            PlaceInFrontOfPlayer();
            StartCoroutine(Run());
        }

        // 쓰러진 루나벨이 더 공격하거나(피의 손) 부딪히지 않게 멈춰 세우고, 대기 모션 대신 패배 모션 첫 프레임을 띄운다.
        private void Freeze()
        {
            var bossAI = GetComponent<FinalBossAI>();
            if (bossAI != null)
            {
                bossAI.StopAllCoroutines();
                bossAI.enabled = false;
            }
            var attackAnimator = GetComponent<MonsterAttackSpriteAnimator>();
            if (attackAnimator != null) attackAnimator.Stop();
            var walkAnimator = GetComponent<MonsterWalkSpriteAnimator>();
            if (walkAnimator != null) walkAnimator.enabled = false;
            var body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.bodyType = RigidbodyType2D.Kinematic;
            }
            foreach (var col in GetComponents<Collider2D>()) col.enabled = false;
            spriteRenderer.color = Color.white;
            if (defeatFrames.Length > 0) spriteRenderer.sprite = defeatFrames[0];
            HideHealthBar();
        }

        // 네크로시아가 바라보는 쪽 바로 앞(같은 바닥 높이)으로 옮기고, 서로 마주 보게 한다.
        private void PlaceInFrontOfPlayer()
        {
            if (player == null) return;
            var bounds = GameBootstrap.CurrentMapBounds;
            float side = playerMovement != null && playerMovement.FacingDirection.x < 0f ? -1f : 1f;
            float x = player.transform.position.x + side * StandDistance;
            if (x < bounds.xMin + MapEdgeMargin || x > bounds.xMax - MapEdgeMargin)
            {
                side = -side;
                x = player.transform.position.x + side * StandDistance;
            }
            transform.position = new Vector3(x, transform.position.y, transform.position.z);

            // 패배 프레임은 오른쪽을 보게 잘라 두었다. 네크로시아가 왼쪽에 있으면 뒤집어 네크로시아 쪽을 보게 한다.
            spriteRenderer.flipX = side > 0f;
            if (playerMovement != null) playerMovement.Face(new Vector2(side, 0f));
        }

        private IEnumerator Run()
        {
            // 전투 화면이 닫히고(같은 OnDied에서 EndBattle) 월드가 다시 흐를 때까지 기다린다.
            var battleUI = Object.FindAnyObjectByType<RhythmBattleUI>();
            while (battleUI != null && battleUI.IsOpen) yield return null;
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            yield return new WaitForSeconds(DelayAfterBattle);

            // 1) 쓰러지는 모션을 끝까지 재생하고 마지막 프레임(쓰러진 모습)에 멈춘다.
            for (int i = 1; i < defeatFrames.Length; i++)
            {
                spriteRenderer.sprite = defeatFrames[i];
                yield return new WaitForSeconds(DefeatFrameInterval);
            }
            yield return new WaitForSeconds(HoldAfterFall);

            // 2) 쓰러진 채 마지막 대화.
            for (int i = 0; i < LastWords.Length; i++)
            {
                var line = LastWords[i];
                if (line.IsNecrosia) dialogueUI?.ShowNecrosia(line.Text + PlayerNextHint);
                else dialogueUI?.ShowLunabel(line.Text + LunabelNextHint);
                yield return null;
                while (!AdvancePressed()) yield return null;
            }
            dialogueUI?.Hide();

            // 3) 핏빛 안개처럼 서서히 사라진다.
            for (float t = 0f; t < FadeOutDuration; t += Time.deltaTime)
            {
                spriteRenderer.color = new Color(1f, 1f, 1f, 1f - t / FadeOutDuration);
                yield return null;
            }
            spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
            yield return new WaitForSeconds(DelayBeforeReturn);

            // 4) 마을로 돌아간다. 스테이지가 통째로 다시 지어지며 이 오브젝트도 함께 지워진다.
            Finish();
            onFinished?.Invoke();
        }

        private static bool AdvancePressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
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
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        // 연출 도중 스테이지가 다시 지어지면 루나벨과 함께 지워지므로, 막아 둔 것들을 풀고 나간다.
        private void OnDestroy()
        {
            if (monster != null) monster.OnDied -= HandleDied;
            if (!running || finished) return;
            dialogueUI?.Hide();
            Finish();
        }
    }
}

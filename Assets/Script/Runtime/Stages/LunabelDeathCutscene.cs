using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Characters;
using Aethoria.Monsters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 루나벨(최종보스) 처치 연출. 2페이즈 체력이 0이 되면 전투 화면이 닫히고 필드로 돌아온 뒤:
    //   1) 패배 모션(Art/Monsters/FinalBoss/Defeat, Lunabel_Defeat 8프레임)의 앞부분을 재생해 휘청이다 무릎을 꿇고,
    //   2) 무릎 꿇은 채 네크로시아와 마지막 대화를 나눈 뒤([Z]/Enter로 넘김),
    //   3) 남은 프레임으로 완전히 쓰러져 서서히 사라지고,
    //   4) onFinished(마을로 돌아가기)를 부른다.
    // 보상(경험치/골드/드랍/퀘스트)은 쓰러진 순간 Monster.OnDied/AnyDied로 이미 처리된다.
    [RequireComponent(typeof(Monster))]
    public class LunabelDeathCutscene : MonoBehaviour
    {
        private const string DefeatPath = "Art/Monsters/FinalBoss/Defeat";
        private const float DelayAfterBattle = 0.5f;
        private const float DefeatFrameInterval = 0.16f;
        // 패배 모션 중 이 프레임(0부터)까지 재생하고 무릎 꿇은 채 대화한다. 나머지는 대화 뒤 쓰러질 때 재생한다.
        private const int KneelFrameIndex = 5;
        private const float HoldLastFrame = 1.2f;
        private const float FadeOutDuration = 1.2f;
        private const float DelayBeforeReturn = 0.8f;
        private const string LunabelNextHint = "   <color=#E07A8A>[Z]</color>";
        private const string PlayerNextHint = "   <color=#B98CE8>[Z]</color>";

        private readonly struct Line
        {
            public readonly bool IsNecrosia;
            public readonly string Text;
            public Line(bool isNecrosia, string text) { IsNecrosia = isNecrosia; Text = text; }
        }

        private static readonly Line[] LastWords =
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
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // 쓰러진 루나벨이 더 공격하거나(피의 손) 부딪히지 않게 멈춰 세운다.
            var bossAI = GetComponent<FinalBossAI>();
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

            // 전투 화면이 닫히고(같은 OnDied에서 EndBattle) 월드가 다시 흐를 때까지 기다린다.
            var battleUI = Object.FindAnyObjectByType<RhythmBattleUI>();
            while (battleUI != null && battleUI.IsOpen) yield return null;
            yield return new WaitForSeconds(DelayAfterBattle);

            if (playerMovement != null)
            {
                playerMovement.SetInputLocked(true);
                playerMovement.Face(transform.position.x >= player.transform.position.x ? Vector2.right : Vector2.left);
            }
            // 패배 프레임은 오른쪽을 보게 잘라 두었다. 플레이어가 왼쪽에 있으면 뒤집어 플레이어 쪽을 보게 한다.
            spriteRenderer.flipX = player != null && player.transform.position.x < transform.position.x;

            // 1) 휘청이다 무릎을 꿇는다(대기 애니메이터를 끄고 패배 프레임을 직접 넘긴다).
            var walkAnimator = GetComponent<MonsterWalkSpriteAnimator>();
            if (walkAnimator != null) walkAnimator.enabled = false;
            int kneel = Mathf.Min(KneelFrameIndex, defeatFrames.Length - 1);
            for (int i = 0; i <= kneel; i++)
            {
                spriteRenderer.sprite = defeatFrames[i];
                yield return new WaitForSeconds(DefeatFrameInterval);
            }

            // 2) 무릎 꿇은 채 마지막 대화.
            for (int i = 0; i < LastWords.Length; i++)
            {
                var line = LastWords[i];
                if (line.IsNecrosia) dialogueUI?.ShowNecrosia(line.Text + PlayerNextHint);
                else dialogueUI?.ShowLunabel(line.Text + LunabelNextHint);
                yield return null;
                while (!AdvancePressed()) yield return null;
            }
            dialogueUI?.Hide();

            // 3) 완전히 쓰러진 뒤 핏빛 안개처럼 서서히 사라진다.
            for (int i = kneel + 1; i < defeatFrames.Length; i++)
            {
                spriteRenderer.sprite = defeatFrames[i];
                yield return new WaitForSeconds(DefeatFrameInterval);
            }
            yield return new WaitForSeconds(HoldLastFrame);

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

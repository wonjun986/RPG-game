using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Characters;
using Aethoria.Monsters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 새 게임 튜토리얼(어둠의 숲). 도입 스토리(StoryIntroUI)가 끝나면 네크로시아가 숲에서 눈을 뜨고,
    // 화면 위쪽 안내(TutorialUI)를 따라 이동 → 점프 → 이단 점프 → 대시를 차례로 해 본다. 다 해내면 숲의 정령 한 마리가
    // 나타나고, 리듬 전투 안내창을 본 뒤 직접 싸운다. 정령을 잡으면 숲 출구 포탈이 열린다(onCleared).
    // 독백은 네크로시아 대화창(UI/Player_Talk)에 띄우고 [Z]/Enter로 넘긴다.
    public class ForestTutorial : MonoBehaviour
    {
        private const string PlayerNextHint = "   <color=#B98CE8>[Z]</color>";

        private const float MoveGoalDistance = 3f;
        private const float MonsterSpawnDistance = 6f;
        private const float StepPause = 0.4f;

        private static readonly string[] WakeLines =
        {
            "......",
            "...여긴... 숲인가.",
            "몸이 무겁다. 대체 얼마나 잠들어 있었던 거지.",
            "...루나벨. 그 붉은 달만큼은 잊을 리가 없다.",
            "우선 몸부터 다시 움직여 봐야겠군.",
        };

        private static readonly string[] EncounterLines =
        {
            "감각이 조금씩 돌아온다.",
            "...기척이다.",
        };

        private static readonly string[] BeforeBattleLines =
        {
            "저 녀석의 움직임... 박자가 보인다.",
            "적의 리듬을 읽는 힘. 아직 남아 있었군.",
        };

        private static readonly string[] ClearLines =
        {
            "리듬은 아직 이 몸 안에 남아 있다.",
            "루나벨... 이번에는 내가 이긴다. 잃어버린 모든 것을 되찾겠다.",
        };

        private Character player;
        private CharacterMovement2D movement;
        private CharacterSkillDash dash;
        private NpcDialogueUI dialogueUI;
        private TutorialUI tutorialUI;
        private StoryIntroUI storyUI;
        private RhythmBattleUI battleUI;
        private System.Func<float, Monster> spawnMonster;
        private System.Action onCleared;
        private float mapXMin, mapXMax;
        private bool doubleJumped;

        public void Initialize(Character targetPlayer, NpcDialogueUI dialogue, TutorialUI tutorial, StoryIntroUI story,
            float xMin, float xMax, System.Func<float, Monster> monsterSpawner, System.Action cleared)
        {
            player = targetPlayer;
            movement = player.GetComponent<CharacterMovement2D>();
            dash = player.GetComponent<CharacterSkillDash>();
            dialogueUI = dialogue;
            tutorialUI = tutorial;
            storyUI = story;
            mapXMin = xMin;
            mapXMax = xMax;
            spawnMonster = monsterSpawner;
            onCleared = cleared;

            // 스토리가 끝나기 전부터 눈을 감은 채로 둔다(스토리 창이 닫히는 순간 숲이 그대로 보이지 않게).
            tutorialUI.SetEyesClosed(true);
            if (movement != null)
            {
                movement.SetInputLocked(true);
                movement.Face(Vector2.right);
                movement.OnDoubleJump += HandleDoubleJump;
            }
        }

        private void Start()
        {
            battleUI = Object.FindFirstObjectByType<RhythmBattleUI>();
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            if (movement != null) movement.OnDoubleJump -= HandleDoubleJump;
            // 튜토리얼 도중 쓰러지거나 타이틀로 나가 숲이 지워지면 안내/눈꺼풀/대사창이 남지 않게 정리한다.
            if (tutorialUI != null)
            {
                tutorialUI.HideGuide();
                tutorialUI.SetGuideHidden(false);
                tutorialUI.SetEyesClosed(false);
            }
            if (dialogueUI != null) dialogueUI.Hide();
        }

        private void HandleDoubleJump() => doubleJumped = true;

        private void Update()
        {
            // 리듬 전투 화면이 떠 있는 동안에는 위쪽 안내가 전투 화면을 가리지 않게 숨긴다.
            if (tutorialUI != null) tutorialUI.SetGuideHidden(battleUI != null && battleUI.IsOpen);
        }

        private IEnumerator Run()
        {
            while (storyUI != null && storyUI.IsOpen) yield return null;

            yield return tutorialUI.OpenEyes();
            yield return Monologue(WakeLines);

            // ---- 이동 ----
            SetInputLocked(false);
            tutorialUI.ShowGuide("← → 키로 걸어 보자");
            float walked = 0f;
            float lastX = player.transform.position.x;
            while (walked < MoveGoalDistance)
            {
                float x = player.transform.position.x;
                walked += Mathf.Abs(x - lastX);
                lastX = x;
                yield return null;
            }
            yield return tutorialUI.CompleteGuide();
            yield return new WaitForSeconds(StepPause);

            // ---- 점프 ----
            tutorialUI.ShowGuide("↑ 키로 점프해 보자");
            while (movement.IsGrounded || movement.VerticalVelocity <= 0.5f) yield return null;
            yield return tutorialUI.CompleteGuide();
            yield return new WaitForSeconds(StepPause);

            // ---- 이단 점프 ----
            tutorialUI.ShowGuide("점프 중에 ↑ 를 한 번 더 — 이단 점프!");
            doubleJumped = false;
            while (!doubleJumped) yield return null;
            yield return tutorialUI.CompleteGuide();
            yield return new WaitForSeconds(StepPause);

            // ---- 대시 ----
            tutorialUI.ShowGuide("Shift 키로 대시해 보자 (대시 중에는 무적)");
            while (dash != null && !dash.IsDashing) yield return null;
            yield return tutorialUI.CompleteGuide();

            // 공중에서 대사가 나오면 떨어지는 도중에 굳어 보이므로 땅에 내려올 때까지 기다린다.
            while (!movement.IsGrounded) yield return null;
            yield return new WaitForSeconds(StepPause);

            // ---- 전투 ----
            yield return Monologue(EncounterLines);
            var enemy = spawnMonster(PickMonsterX());
            // 독백과 안내창을 보는 동안 다가와서 전투가 먼저 시작되지 않게 잠깐 세워 둔다.
            var enemyAI = enemy != null ? enemy.GetComponent<MonsterAI>() : null;
            if (enemyAI != null) enemyAI.enabled = false;
            yield return Monologue(BeforeBattleLines);

            bool pagesClosed = false;
            tutorialUI.Show(TutorialUI.BattlePages, () => pagesClosed = true);
            while (!pagesClosed) yield return null;
            if (enemyAI != null) enemyAI.enabled = true;

            tutorialUI.ShowGuide("숲의 정령에게 다가가 리듬 전투를 시작하자");
            bool enemyDead = enemy == null || enemy.IsDead;
            if (enemy != null) enemy.OnDied += _ => enemyDead = true;
            while (!enemyDead) yield return null;
            // 전투 결과 화면이 닫히고 필드로 돌아올 때까지 기다린다.
            while (battleUI != null && battleUI.IsOpen) yield return null;
            yield return tutorialUI.CompleteGuide();

            yield return Monologue(ClearLines);
            onCleared?.Invoke();
            tutorialUI.ShowGuide("→ 오른쪽 끝 포탈로 숲을 빠져나가자");
        }

        // 정령은 플레이어 앞쪽(오른쪽)에 세우되, 맵 끝에 너무 가까우면 반대쪽에 세운다.
        private float PickMonsterX()
        {
            float x = player.transform.position.x + MonsterSpawnDistance;
            if (x > mapXMax - 1.5f) x = player.transform.position.x - MonsterSpawnDistance;
            return Mathf.Clamp(x, mapXMin + 1.5f, mapXMax - 1.5f);
        }

        private IEnumerator Monologue(string[] lines)
        {
            SetInputLocked(true);
            for (int i = 0; i < lines.Length; i++)
            {
                dialogueUI.ShowNecrosia(lines[i] + PlayerNextHint);
                // 대사가 바뀐 그 프레임의 입력으로 바로 다음 대사가 넘어가지 않게 한 프레임 쉰다.
                yield return null;
                while (!AdvancePressed()) yield return null;
            }
            dialogueUI.Hide();
            SetInputLocked(false);
        }

        private void SetInputLocked(bool locked)
        {
            if (movement != null) movement.SetInputLocked(locked);
        }

        private static bool AdvancePressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
        }
    }
}

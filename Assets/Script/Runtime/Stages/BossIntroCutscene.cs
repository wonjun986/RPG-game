using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Characters;
using Aethoria.Monsters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 보스방 등장 연출. 보스 기사는 맵 오른쪽 끝에서 대기 모션으로 서 있고, 방에 들어오면 플레이어 조작을 막은 채
    // 카메라가 보스 쪽으로 천천히 넘어간다. 보스 대화창(UI/BossKnight_Talk)으로 대사를 [Z]/Enter로 넘기다가,
    // 마지막 대사가 끝나면 카메라가 플레이어에게 돌아오고 보스가 맵을 가로질러 네크로시아 머리 위로 날아든다.
    // 떨어져 내리기 직전 네크로시아가 대시로 보스 밑을 빠져나가 피하고, 보스가 빈 자리를 내려찍는 순간
    // 땅이 울리며 조작이 풀리고 곧바로 실시간 전투가 시작된다(BossAI.BeginFightWithJump, 내려찍기 피해 없음).
    // 이 연출을 이미 본 세이브(두 번째 만남부터)는 Esc를 누르면 남은 대사를 건너뛰고 바로 점프로 넘어간다.
    public class BossIntroCutscene : MonoBehaviour
    {
        private const string TalkFrame = "UI/BossKnight_Talk";
        private const string SpeakerName = "알리아스";
        // 대화창 그림(2172x724) 속 위치(픽셀로 잰 값). 이름판 안쪽은 x 640~920, y 414~468이고 대사 칸 왼쪽 위에 겹쳐 있다.
        // 어두운 대사 칸 안쪽은 x 585~1940, y 446~640인데, 글자는 이름판 아래(y 468~)부터 쓰고
        // 테두리에 붙지 않게 안쪽으로 조금 띄운다(x 615~1740, y ~625). 오른쪽 끝의 십자 문양(x 1770~)은 피한다.
        private const float TalkTextLeft = 615f / 2172f;
        private const float TalkTextRight = 1740f / 2172f;
        private const float TalkTextBottom = 1f - 625f / 724f;
        private const float NamePlateX0 = 640f, NamePlateY0 = 414f, NamePlateX1 = 920f, NamePlateY1 = 468f;
        private const string NextHint = "   <color=#9B7FC4>[Z]</color>";

        // 보스 기사 대화창(UI/BossKnight_Talk)에 대사 한 줄을 띄운다. 사망 연출(BossDeathCutscene)도 같이 쓴다.
        public static void ShowBossLine(NpcDialogueUI dialogueUI, string line)
        {
            dialogueUI?.Show(TalkFrame, line + NextHint, TalkTextLeft, SpeakerName,
                NamePlateX0, NamePlateY0, NamePlateX1, NamePlateY1, null, TalkTextBottom, TalkTextRight);
        }

        public static bool AdvanceKeyPressed() => AdvancePressed();

        private const float CameraPanSmoothTime = 0.6f;
        private const float DelayBeforeTalk = 1.3f;
        private const float DelayBeforeJump = 0.6f;
        // 맵을 거의 끝에서 끝까지 뛰어오는 거리라 평소 점프(1.4초)보다 길게 체공한다.
        private const float IntroJumpDuration = 1.9f;
        // 보스가 공중에 머무는 시간 중 이만큼 지났을 때 대시로 피한다(착지 직전에 빠져나가야 아슬아슬해 보인다).
        private const float DodgeAtAirFraction = 0.6f;
        private const float DodgeDashDistance = 4.5f;
        private const float DodgeDashDuration = 0.25f;
        private const float LandingShakeDuration = 0.35f;
        private const float LandingShakeMagnitude = 0.25f;

        private static readonly string[] Lines =
        {
            "...여기까지 걸어 들어온 자가 또 있었군.",
            "숲의 기사들을 쓰러뜨린 것이 너인가. 그 낫에서 죽음의 냄새가 나는구나.",
            "나는 숲의 기사단장 알리아스. 이 숲의 끝은 내가 지킨다. 지나가고 싶다면, 내 검을 넘어서라.",
            "자, 와라!",
        };

        // 체력이 절반 아래로 떨어져 땅에 검을 꽂은 알리아스의 대사(BossAI.EnrageRoutine이 이 대사가 끝나길 기다렸다가 검을 치켜든다).
        private static readonly string[] EnrageLines =
        {
            "...크윽. 이 내가, 무릎을 꿇다니.",
            "좋다, 사신이여. 기사단장의 이름을 걸고... 지금부터는 전력이다!",
        };

        // 체력이 30% 아래로 떨어져 종막 의식(BossAI.FinaleRoutine)을 시작하기 직전, 검을 꽂기 전에 하는 대사.
        private static readonly string[] FinaleLines =
        {
            "...아직이다. 기사단장의 검은 이대로 꺾이지 않는다.",
            "숲에 잠든 기사들의 검이여, 내 부름에 응하라!",
            "사신이여, 이 검들을 모두 꺾지 못한다면... 이 숲이 너의 무덤이 될 것이다.",
        };

        private BossAI boss;
        private Character player;
        private CharacterMovement2D playerMovement;
        private NpcDialogueUI dialogueUI;
        private CameraFollow cameraFollow;
        private bool finished;
        // 등장 연출을 이미 한 번 본 세이브면 Esc로 대사를 건너뛰고 곧바로 보스가 뛰어든다.
        private bool skippable;
        private System.Action onShown;

        private const string SkipHint = "   <color=#9B7FC4>[Esc] 스킵</color>";

        public void Initialize(BossAI bossAI, Character targetPlayer, NpcDialogueUI dialogue, bool skippable = false, System.Action onShown = null)
        {
            boss = bossAI;
            player = targetPlayer;
            playerMovement = player != null ? player.GetComponent<CharacterMovement2D>() : null;
            dialogueUI = dialogue;
            this.skippable = skippable;
            this.onShown = onShown;
            boss.HoldForIntro();
            boss.EnrageTalk = PlayEnrageLines;
            boss.FinaleTalk = () => PlayLines(FinaleLines);
        }

        private IEnumerator PlayEnrageLines() => PlayLines(EnrageLines);

        private IEnumerator PlayLines(string[] lines)
        {
            foreach (var line in lines)
            {
                ShowBossLine(dialogueUI, line);
                // 대사가 바뀐 그 프레임의 입력으로 바로 다음 대사가 넘어가지 않게 한 프레임 쉰다.
                yield return null;
                while (!AdvancePressed()) yield return null;
            }
            dialogueUI?.Hide();
        }

        private void Start()
        {
            var cam = Camera.main;
            cameraFollow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (playerMovement != null)
            {
                playerMovement.SetCutsceneLocked(true);
                playerMovement.Face(Vector2.right);
            }
            if (cameraFollow != null) cameraFollow.SetTarget(boss.transform, CameraPanSmoothTime);

            // 등장 연출을 만난 것으로 기록한다 — 다음 만남부터 스킵할 수 있다.
            onShown?.Invoke();

            bool skipped = false;
            for (float t = 0f; t < DelayBeforeTalk && !skipped; t += Time.deltaTime)
            {
                skipped = SkipPressed();
                yield return null;
            }

            for (int i = 0; i < Lines.Length && !skipped; i++)
            {
                string line = skippable ? Lines[i] + SkipHint : Lines[i];
                ShowBossLine(dialogueUI, line);
                // 대사가 바뀐 그 프레임의 입력으로 바로 다음 대사가 넘어가지 않게 한 프레임 쉰다.
                yield return null;
                while (!AdvancePressed())
                {
                    if (SkipPressed())
                    {
                        skipped = true;
                        break;
                    }
                    yield return null;
                }
            }

            dialogueUI?.Hide();
            if (cameraFollow != null && player != null) cameraFollow.SetTarget(player.transform, CameraPanSmoothTime);

            yield return new WaitForSeconds(DelayBeforeJump);

            boss.BeginFightWithJump(IntroJumpDuration, OnBossLanded);

            // 보스가 네크로시아 머리 위로 떨어져 내리는 순간, 네크로시아가 대시로 보스 밑을 빠져나가 내려찍기를 피한다.
            float airStart = IntroJumpDuration * BossAI.JumpTakeoffFraction;
            float airTime = IntroJumpDuration * (BossAI.JumpImpactFraction - BossAI.JumpTakeoffFraction);
            yield return new WaitForSeconds(airStart + airTime * DodgeAtAirFraction);
            var dash = player != null ? player.GetComponent<CharacterSkillDash>() : null;
            if (dash != null)
            {
                playerMovement?.Face(Vector2.right);
                dash.PlayCutsceneDash(DodgeDashDistance, DodgeDashDuration);
            }

            // 점프가 도중에 끊겨 착지 콜백이 안 오더라도 조작이 영영 잠기지 않게 한다.
            yield return new WaitForSeconds(IntroJumpDuration + 0.5f);
            Finish();
        }

        // 보스가 네크로시아가 서 있던 자리에 내려찍는 순간 땅이 울리고, 곧바로 전투가 시작된다.
        private void OnBossLanded()
        {
            if (cameraFollow != null) cameraFollow.Shake(LandingShakeDuration, LandingShakeMagnitude);
            Finish();
        }

        private bool SkipPressed()
        {
            var keyboard = Keyboard.current;
            return skippable && keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        private static bool AdvancePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame;
        }

        // 착지 직후(또는 연출이 중간에 끊겼을 때) 조작과 카메라를 원래대로 돌려준다. 두 번 불려도 안전하다.
        private void Finish()
        {
            if (finished) return;
            finished = true;

            if (playerMovement != null) playerMovement.SetCutsceneLocked(false);
            if (cameraFollow != null)
            {
                if (player != null) cameraFollow.SetTarget(player.transform, CameraPanSmoothTime);
                cameraFollow.ResetSmoothTime();
            }
        }

        // 연출 도중 스테이지가 다시 지어지면(포탈/사망 등) 보스와 함께 지워지므로, 막아 둔 것들을 풀고 나간다.
        private void OnDestroy()
        {
            if (finished) return;
            dialogueUI?.Hide();
            Finish();
        }
    }
}

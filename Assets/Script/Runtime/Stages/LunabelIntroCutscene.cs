using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Characters;
using Aethoria.Monsters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 절망의 언덕 등장 연출. 언덕에 들어오면 플레이어 조작을 막은 채 카메라가 언덕 끝의 루나벨에게 천천히 넘어간다.
    // 루나벨은 의자에 앉은 모습(Seated)으로 기다리고 있고, 루나벨(UI/Lunabel_Talk)과 네크로시아(UI/Player_Talk)가
    // 대사를 주고받는다([Z]/Enter로 넘김). 대화가 끝나면 루나벨 앞에서부터 플레이어 발밑까지 피의 기둥(BloodPillarRise)이
    // 차례로 솟구쳐 오르고, 카메라가 그 물결을 따라 플레이어에게 돌아온 뒤 루나벨이 일어서며 곧바로 리듬전투가 열린다.
    // 이 연출을 이미 본 세이브(두 번째 만남부터)는 Esc를 누르면 남은 대사와 피의 기둥을 건너뛴다.
    public class LunabelIntroCutscene : MonoBehaviour
    {
        // 루나벨 대사는 NpcDialogueUI.ShowLunabel(UI/Lunabel_Talk 창)로 띄운다.
        private const string NextHint = "   <color=#E07A8A>[Z]</color>";
        private const string SkipHint = "   <color=#E07A8A>[Esc] 스킵</color>";

        // 네크로시아 대사는 NpcDialogueUI.ShowNecrosia(UI/Player_Talk 창)로 띄운다.
        private const string PlayerNextHint = "   <color=#B98CE8>[Z]</color>";
        private const string PlayerSkipHint = "   <color=#B98CE8>[Esc] 스킵</color>";

        private const float CameraPanSmoothTime = 0.6f;
        private const float DelayBeforeTalk = 1.3f;
        // 가시 연출을 Esc로 건너뛰었을 때만 쓴다(카메라가 플레이어에게 돌아온 뒤 전투 시작).
        private const float DelayBeforeBattle = 0.5f;
        // 가시가 솟는 프레임 중 이만큼 진행됐을 때 네크로시아에게 닿는다고 보고, 그 순간 전투로 넘어간다.
        private const float PillarHitFraction = 0.5f;

        // 피의 기둥: 루나벨 앞(PillarStartOffset)에서 플레이어 발밑까지 PillarSpacing 간격으로 PillarInterval마다 하나씩 솟는다.
        private const float PillarStartOffset = 1.4f;
        private const float PillarSpacing = 1.5f;
        private const float PillarInterval = 0.09f;
        private const float PillarFrameDuration = 0.06f;
        private const float PillarHoldTime = 0.25f;
        private const float PillarFadeTime = 0.3f;
        private const float PillarCameraSmoothTime = 0.25f;
        private const int PillarSortingOrder = 3;
        private const float DelayBeforePillars = 0.4f;

        private struct Line
        {
            public bool isPlayer;
            public string text;
            public Line(bool isPlayer, string text) { this.isPlayer = isPlayer; this.text = text; }
        }

        private static readonly Line[] Lines =
        {
            new Line(false, "어머... 이 냄새. 설마 했는데, 정말 너였구나. 네크로시아."),
            new Line(true, "...루나벨. 오랜 잠 속에서도 그 붉은 달만큼은 잊지 않았다."),
            new Line(false, "그날 밤 분명 끝을 내 줬는데. 리듬이 무너진 채 무릎 꿇던 네 얼굴, 아직도 생생해."),
            new Line(true, "그래서 다시 왔다. 그날 빼앗긴 힘도, 이름도, 영광도... 전부 되찾으러."),
            new Line(false, "후후... 숲에서 겨우 눈을 뜬 사신이 여기까지 기어 올라왔다니. 앉은 채로 맞이하는 건 용서해 줘."),
            new Line(true, "일어날 필요 없다. 이번에는 네 공격의 박자, 하나도 놓치지 않는다."),
            new Line(false, "그럼 보여 줘. 그 리듬이 이번엔 얼마나 버티는지. 네 피의 맛, 다시 한번 맛보게 해 줘."),
        };

        private FinalBossAI boss;
        private Monster bossMonster;
        private MonsterWalkSpriteAnimator bossWalk;
        private Sprite[] standingFrames;
        private Sprite[] standingIdleFrames;
        private float standingIdleFrameRate;
        // 앉은 모습 4프레임은 숨 쉬듯 천천히 돈다(서 있는 대기는 6).
        private const float SeatedFrameRate = 3f;
        private Character player;
        private CharacterMovement2D playerMovement;
        private NpcDialogueUI dialogueUI;
        private CameraFollow cameraFollow;
        private Transform pillarFront;
        private bool skippable;
        private System.Action onShown;
        private bool finished;

        public void Initialize(FinalBossAI bossAI, Character targetPlayer, NpcDialogueUI dialogue, bool skippable = false, System.Action onShown = null)
        {
            boss = bossAI;
            bossMonster = bossAI.GetComponent<Monster>();
            player = targetPlayer;
            playerMovement = player != null ? player.GetComponent<CharacterMovement2D>() : null;
            dialogueUI = dialogue;
            this.skippable = skippable;
            this.onShown = onShown;
            boss.HoldForIntro();
            SitDown();
        }

        // 연출 동안 대기 모션을 의자에 앉은 모습으로 갈아 끼운다. 원래 서 있는 프레임은 StandUp에서 되돌린다.
        private void SitDown()
        {
            bossWalk = boss.GetComponent<MonsterWalkSpriteAnimator>();
            if (bossWalk == null) return;

            var seated = Resources.LoadAll<Sprite>(FinalBossAI.SeatedPath);
            if (seated == null || seated.Length == 0) return;
            System.Array.Sort(seated, (a, b) => string.CompareOrdinal(a.name, b.name));

            standingFrames = bossWalk.Frames;
            standingIdleFrames = bossWalk.IdleFrames;
            standingIdleFrameRate = bossWalk.IdleFrameRate;
            bossWalk.OverrideFrames(seated, seated, SeatedFrameRate);
        }

        private void StandUp()
        {
            if (bossWalk == null || standingFrames == null) return;
            bossWalk.OverrideFrames(standingFrames, standingIdleFrames, standingIdleFrameRate);
            standingFrames = null;
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
                playerMovement.SetInputLocked(true);
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
                ShowLine(Lines[i]);
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

            if (!skipped && player != null)
            {
                // 마지막 가시가 네크로시아에게 닿는 그 순간에 곧바로 전투 화면으로 넘어간다(따로 기다리지 않는다).
                yield return new WaitForSeconds(DelayBeforePillars);
                yield return PillarWave();
            }
            else
            {
                if (cameraFollow != null && player != null) cameraFollow.SetTarget(player.transform, CameraPanSmoothTime);
                yield return new WaitForSeconds(DelayBeforeBattle);
            }

            Finish();
            var battle = Object.FindFirstObjectByType<RhythmBattleUI>();
            if (battle != null && bossMonster != null && player != null) battle.TryBegin(bossMonster, player);
        }

        // 루나벨 앞에서 플레이어 발밑까지 피의 기둥을 차례로 솟구치게 한다. 카메라는 맨 앞 기둥을 따라간다.
        // 기둥은 스테이지 아래에 붙여서, 도중에 스테이지가 다시 지어져도 함께 지워지게 한다.
        private IEnumerator PillarWave()
        {
            var frames = Resources.LoadAll<Sprite>(FinalBossAI.BloodPillarRisePath);
            if (frames == null || frames.Length == 0) yield break;
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));

            Vector2 bossFeet = boss.transform.position;
            Vector2 playerFeet = player.transform.position;
            float direction = Mathf.Sign(playerFeet.x - bossFeet.x);
            if (direction == 0f) direction = -1f;
            Vector2 start = new Vector2(bossFeet.x + direction * PillarStartOffset, bossFeet.y);
            float distance = Mathf.Abs(playerFeet.x - start.x);
            int count = Mathf.Max(1, Mathf.CeilToInt(distance / PillarSpacing) + 1);

            Transform parent = boss.transform.parent;
            pillarFront = new GameObject("BloodPillarFront").transform;
            pillarFront.SetParent(parent, true);
            pillarFront.position = start;
            if (cameraFollow != null) cameraFollow.SetTarget(pillarFront, PillarCameraSmoothTime);

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 1f : (float)i / (count - 1);
                Vector2 position = Vector2.Lerp(start, playerFeet, t);
                pillarFront.position = position;
                StartCoroutine(PlayPillar(frames, position, parent));
                if (i == count - 1 || SkipPressed()) break;
                yield return new WaitForSeconds(PillarInterval);
            }

            // 마지막 가시(플레이어 발밑)가 솟구쳐 네크로시아에게 닿는 순간까지만 기다린다. 남은 가시는 뒤에서 마저 사라진다.
            yield return new WaitForSeconds(frames.Length * PillarFrameDuration * PillarHitFraction);
            if (pillarFront != null) Destroy(pillarFront.gameObject);
        }

        private IEnumerator PlayPillar(Sprite[] frames, Vector2 position, Transform parent)
        {
            var go = new GameObject("BloodPillarRise");
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = PillarSortingOrder;

            foreach (var frame in frames)
            {
                if (renderer == null) yield break;
                renderer.sprite = frame;
                yield return new WaitForSeconds(PillarFrameDuration);
            }
            yield return new WaitForSeconds(PillarHoldTime);

            for (float t = 0f; t < PillarFadeTime; t += Time.deltaTime)
            {
                if (renderer == null) yield break;
                renderer.color = new Color(1f, 1f, 1f, 1f - t / PillarFadeTime);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        private void ShowLine(Line line)
        {
            if (line.isPlayer)
            {
                dialogueUI?.ShowNecrosia(line.text + (skippable ? PlayerSkipHint : "") + PlayerNextHint);
                return;
            }
            string text = skippable ? line.text + SkipHint : line.text;
            dialogueUI?.ShowLunabel(text + NextHint);
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

        // 대사가 끝났을 때(또는 연출이 중간에 끊겼을 때) 루나벨을 일으켜 세우고 조작과 카메라, 보스 AI를 원래대로 돌려준다.
        // 두 번 불려도 안전하다.
        private void Finish()
        {
            if (finished) return;
            finished = true;

            StandUp();
            if (pillarFront != null) Destroy(pillarFront.gameObject);
            if (boss != null) boss.EndIntro();
            if (playerMovement != null) playerMovement.SetInputLocked(false);
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

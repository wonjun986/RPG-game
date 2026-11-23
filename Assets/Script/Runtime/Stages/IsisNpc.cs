using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 마을의 연습장 앞에 서 있는 이시스. 대기 애니메이션(4프레임)을 반복하다가 플레이어가 가까이 오면
    // 머리 위에 "[Z] 대화하기"를 띄운다(지금 배울 수 있는 기술이 있으면 그 아래에 알림을 붙인다).
    // 새 게임에서 처음 말을 걸면 스토리 대사(FirstMeetingLines)가 먼저 나온다.
    // Z를 누르면 선택지가 뜬다: [1] 연습장 입장(허수아비가 서 있는 연습장 맵으로 이동), [2] 기술 배우기(SkillLearnUI), [Z] 닫기.
    [RequireComponent(typeof(SpriteRenderer))]
    public class IsisNpc : MonoBehaviour
    {
        private enum TalkState { None, FirstMeeting, Offer }

        // 새 게임에서 처음 말을 걸면 연습장 입장 질문 전에 나오는 스토리 대사(이시스 ↔ 네크로시아). [Z]로 넘기고, 다 보면
        // 곧바로 입장 질문으로 이어진다. 도중에 멀어지면 끊기고, 다음에 말을 걸면 처음부터 다시 나온다.
        private readonly struct StoryLine
        {
            public readonly bool IsNecrosia;
            public readonly string Text;
            public StoryLine(bool isNecrosia, string text) { IsNecrosia = isNecrosia; Text = text; }
        }

        private static readonly StoryLine[] FirstMeetingLines =
        {
            new StoryLine(false, "...멈춰라. 처음 보는 얼굴이군. 그 낫, 평범한 물건이 아니야."),
            new StoryLine(true, "연습장이 있다고 들었다."),
            new StoryLine(false, "나는 이시스. 한때 이 땅의 기사였다. 붉은 달이 뜬 밤, 언덕으로 향한 동료들을 모두 잃었지."),
            new StoryLine(false, "루나벨은 피를 빨아 제 상처를 메운다더군. 한 번 붙잡힌 자는 하나도 살아 돌아오지 못했다."),
            new StoryLine(true, "...알고 있다. 내 낫도 한 번은 꺾였다. 하지만 이번엔 피 한 방울도 내주지 않는다."),
            new StoryLine(false, "흥, 말은 쉽지. 허수아비라면 얼마든지 베어도 좋다. 감각이 돌아올 때까지 휘둘러 봐라."),
        };
        private const string StoryNextHint = "   <color=#F2D94E>[Z]</color>";
        private const string StoryPlayerNextHint = "   <color=#B98CE8>[Z]</color>";

        private bool firstMeetingPending;
        private System.Action onFirstMeetingDone;
        private int storyIndex;

        private const string TalkFrame = "UI/Talk/Isis_Talk";
        // Isis_Talk은 이시스의 팔이 대사 칸 왼쪽(그림 x 약 815px)까지 걸쳐 있어서, 대사를 그보다 오른쪽에서 시작한다.
        private const float TalkTextLeft = 0.385f;
        // 대사 칸 위아래 테두리 안쪽(그림 y 약 340~545px)에만 글자를 쓴다. 기본값(0.22~0.53)은 아래 금색 테두리에 걸쳤다.
        private const float TalkTextTop = 1f - 345f / 724f;
        private const float TalkTextBottom = 1f - 540f / 724f;

        // 대기 모션: 숨쉬기(Breath 5장, 8프레임 기사 시트 첫 장에서 허리 위만 0~5px 들어 올린 그림)를 1→5→1로 오가며 숨을 쉬고,
        // 숨을 다 내쉰 순간에 가끔 눈을 감았다 뜬다(Idle 8장, NpcIdleAnimation). 기사답게 숨은 조금 느리게 쉰다.
        [SerializeField] private string resourcesPath = "Art/NPC/Isis/Idle";
        [SerializeField] private string breathResourcesPath = "Art/NPC/Isis/Breath";
        [SerializeField] private float breathPeriod = 3.8f;    // 한 번 들이쉬고 내쉬는 데 걸리는 시간(초)
        [SerializeField] private float frameInterval = 0.12f;  // 깜빡임 한 장 시간
        [SerializeField] private float blinkInterval = 5f;     // 깜빡임 사이 평균 간격(초)
        [SerializeField] private float interactRadius = 2f;
        [SerializeField] private float promptHeight = 2.2f;
        [SerializeField] private string offerLine = "...무슨 볼일이지. 기술이 필요하면 가르쳐 주지.\n[1] 연습장 입장   [2] 기술 배우기   [Z] 닫기";
        [SerializeField] private string newSkillHint = "\n<color=#F2D94E>...지금의 너라면 새 기술을 익힐 수 있겠군.</color>";
        private const string PromptLabel = "[Z] 대화하기";
        private const string PromptNewSkill = "\n<color=#F2D94E>새 기술!</color>";

        private SpriteRenderer spriteRenderer;
        private NpcIdleAnimation idleAnimation;
        private Transform player;
        private CharacterMovement2D playerMovement;
        private NpcDialogueUI dialogueUI;
        private InventoryUI inventoryUI;
        private SkillLearnUI skillLearnUI;
        private Character playerCharacter;
        private SkillBook skillBook;
        private TextMesh promptText;

        private bool inRange;
        private TalkState state;

        public void Initialize(Transform playerTransform, NpcDialogueUI dialogue, InventoryUI inventory, SkillLearnUI skillLearn)
        {
            player = playerTransform;
            playerMovement = playerTransform != null ? playerTransform.GetComponent<CharacterMovement2D>() : null;
            playerCharacter = playerTransform != null ? playerTransform.GetComponent<Character>() : null;
            skillBook = playerTransform != null ? playerTransform.GetComponent<SkillBook>() : null;
            dialogueUI = dialogue;
            inventoryUI = inventory;
            skillLearnUI = skillLearn;
        }

        private bool HasLearnableSkill => skillBook != null && playerCharacter != null && skillBook.Learnable(playerCharacter.Level).Count > 0;

        // 다음에 말을 걸 때 입장 질문 전에 첫 만남 스토리 대사를 먼저 보여준다. 다 보면 done을 부른다(기록/저장은 부른 쪽 몫).
        public void SetFirstMeeting(System.Action done)
        {
            firstMeetingPending = true;
            onFirstMeetingDone = done;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            // 플레이어(0)와 같은 순서면 겹칠 때 앞뒤가 번갈아 깜빡여서, 늘 네크로시아 뒤(건물 -5보다는 앞)에 그린다.
            spriteRenderer.sortingOrder = -1;

            idleAnimation = new NpcIdleAnimation(spriteRenderer, resourcesPath, breathResourcesPath)
            {
                BreathPeriod = breathPeriod,
                BlinkFrameTime = frameInterval,
                BlinkInterval = blinkInterval,
            };

            CreatePrompt();
        }

        private void CreatePrompt()
        {
            var go = new GameObject("TalkPrompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, promptHeight, 0f);

            promptText = go.AddComponent<TextMesh>();
            promptText.text = "[Z] 대화하기";
            promptText.characterSize = 0.08f;
            promptText.fontSize = 42;
            promptText.alignment = TextAlignment.Center;
            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.color = Color.white;

            go.GetComponent<MeshRenderer>().sortingOrder = 50;
            go.SetActive(false);
        }

        private void OnDisable()
        {
            if (state == TalkState.None) return;
            dialogueUI?.Hide();
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void Update()
        {
            idleAnimation?.Tick(Time.deltaTime);
            UpdateProximity();
            UpdateInteraction();
        }

        private void UpdateProximity()
        {
            if (player == null) return;

            bool nowInRange = ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude <= interactRadius * interactRadius;
            if (nowInRange == inRange) return;

            inRange = nowInRange;
            RefreshPrompt();
            if (promptText != null) promptText.gameObject.SetActive(inRange && state == TalkState.None);
        }

        // 레벨업으로 새 기술을 배울 수 있게 됐으면 머리 위 알림에 덧붙인다(다가올 때와 대화를 마칠 때 다시 잰다).
        private void RefreshPrompt()
        {
            if (promptText != null) promptText.text = HasLearnableSkill ? PromptLabel + PromptNewSkill : PromptLabel;
        }

        // 입력은 한 프레임에 한 번만 소비한다(NoaNpc와 같은 방식).
        private void UpdateInteraction()
        {
            // 인벤토리/기술 배우기 창이 떠 있는 동안의 입력은 그쪽 몫이다.
            if (inventoryUI != null && inventoryUI.IsOpen) return;
            if (skillLearnUI != null && skillLearnUI.IsOpen) return;

            if (state != TalkState.None && !inRange)
            {
                EndTalk();
                return;
            }

            var keyboard = Keyboard.current;
            bool zPressed = NpcDialogueUI.AdvancePressed(keyboard); // 타이핑 중이거나 남은 장이 있으면 대화창이 먼저 쓴다

            switch (state)
            {
                case TalkState.None:
                    if (inRange && zPressed)
                    {
                        if (promptText != null) promptText.gameObject.SetActive(false);
                        if (playerMovement != null) playerMovement.SetInputLocked(true);
                        if (firstMeetingPending)
                        {
                            state = TalkState.FirstMeeting;
                            storyIndex = 0;
                            ShowStoryLine();
                        }
                        else
                        {
                            EnterOffer();
                        }
                    }
                    break;

                case TalkState.FirstMeeting:
                    if (zPressed)
                    {
                        storyIndex++;
                        if (storyIndex < FirstMeetingLines.Length)
                        {
                            ShowStoryLine();
                        }
                        else
                        {
                            firstMeetingPending = false;
                            var done = onFirstMeetingDone;
                            onFirstMeetingDone = null;
                            done?.Invoke();
                            EnterOffer();
                        }
                    }
                    break;

                case TalkState.Offer:
                    if (zPressed || (keyboard != null && keyboard.xKey.wasPressedThisFrame))
                    {
                        EndTalk();
                    }
                    else if (keyboard != null && keyboard.digit1Key.wasPressedThisFrame)
                    {
                        EndTalk();
                        GameBootstrap.EnterPracticeRoom();
                    }
                    else if (keyboard != null && keyboard.digit2Key.wasPressedThisFrame)
                    {
                        EndTalk();
                        if (skillLearnUI != null) skillLearnUI.Show();
                    }
                    break;
            }
        }

        private void EnterOffer()
        {
            state = TalkState.Offer;
            dialogueUI?.Show(TalkFrame, HasLearnableSkill ? offerLine + newSkillHint : offerLine, TalkTextLeft, textTop: TalkTextTop, textBottom: TalkTextBottom);
        }

        private void ShowStoryLine()
        {
            var line = FirstMeetingLines[storyIndex];
            if (line.IsNecrosia) dialogueUI?.ShowNecrosia(line.Text + StoryPlayerNextHint);
            else dialogueUI?.Show(TalkFrame, line.Text + StoryNextHint, TalkTextLeft, textTop: TalkTextTop, textBottom: TalkTextBottom);
        }

        private void EndTalk()
        {
            state = TalkState.None;
            dialogueUI?.Hide();
            if (playerMovement != null) playerMovement.SetInputLocked(false);
            RefreshPrompt();
            if (promptText != null) promptText.gameObject.SetActive(inRange);
        }
    }
}

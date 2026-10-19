using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 마을의 연습장 앞에 서 있는 이시스. 대기 애니메이션(4프레임)을 반복하다가 플레이어가 가까이 오면
    // 머리 위에 "[Z] 대화하기"를 띄운다. 새 게임에서 처음 말을 걸면 스토리 대사(FirstMeetingLines)가 먼저 나온다.
    // Z를 누르면 연습장에 들어갈지 묻고(Z 입장 / X 취소),
    // 입장하면 허수아비가 서 있는 연습장 맵으로 이동한다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class IsisNpc : MonoBehaviour
    {
        private enum TalkState { None, FirstMeeting, Offer, Response }

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
            new StoryLine(false, "루나벨의 공격에는 리듬이 있다더군. 그 박자를 읽지 못한 자는 하나도 살아 돌아오지 못했다."),
            new StoryLine(true, "...알고 있다. 그 리듬, 한 번은 무너졌지만 이번엔 놓치지 않는다."),
            new StoryLine(false, "흥, 말은 쉽지. 허수아비라면 얼마든지 베어도 좋다. 감각이 돌아올 때까지 휘둘러 봐라."),
        };
        private const string StoryNextHint = "   <color=#F2D94E>[Z]</color>";
        private const string StoryPlayerNextHint = "   <color=#B98CE8>[Z]</color>";

        private bool firstMeetingPending;
        private System.Action onFirstMeetingDone;
        private int storyIndex;

        private const string TalkFrame = "UI/Isis_Talk";
        // Isis_Talk은 이시스의 팔이 대사 칸 왼쪽(그림 x 약 815px)까지 걸쳐 있어서, 대사를 그보다 오른쪽에서 시작한다.
        private const float TalkTextLeft = 0.385f;

        [SerializeField] private string resourcesPath = "Art/NPC/Isis/Idle";
        [SerializeField] private float frameInterval = 0.5f;
        [SerializeField] private float interactRadius = 2f;
        [SerializeField] private float promptHeight = 2.2f;
        [SerializeField] private float responseDuration = 2f;
        [SerializeField] private string offerLine = "...연습장에 볼일이 있나. 허수아비는 몇 번을 베어도 쓰러지지 않는다. 쓸데없이 힘 빼지 말고, 제대로 휘둘러라.\n[Z] 입장   [X] 돌아간다";
        [SerializeField] private string declineLine = "그래. 각오가 없다면 오지 마라.";

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private int frameIndex;
        private float animTimer;
        private Transform player;
        private CharacterMovement2D playerMovement;
        private NpcDialogueUI dialogueUI;
        private InventoryUI inventoryUI;
        private TextMesh promptText;

        private bool inRange;
        private TalkState state;
        private float stateTimer;

        public void Initialize(Transform playerTransform, NpcDialogueUI dialogue, InventoryUI inventory)
        {
            player = playerTransform;
            playerMovement = playerTransform != null ? playerTransform.GetComponent<CharacterMovement2D>() : null;
            dialogueUI = dialogue;
            inventoryUI = inventory;
        }

        // 다음에 말을 걸 때 입장 질문 전에 첫 만남 스토리 대사를 먼저 보여준다. 다 보면 done을 부른다(기록/저장은 부른 쪽 몫).
        public void SetFirstMeeting(System.Action done)
        {
            firstMeetingPending = true;
            onFirstMeetingDone = done;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            frames = Resources.LoadAll<Sprite>(resourcesPath);
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                spriteRenderer.sprite = frames[0];
            }

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
            AdvanceIdleFrame();
            UpdateProximity();
            UpdateInteraction();
        }

        private void AdvanceIdleFrame()
        {
            if (frames == null || frames.Length == 0) return;

            animTimer += Time.deltaTime;
            if (animTimer < frameInterval) return;

            animTimer -= frameInterval;
            frameIndex = (frameIndex + 1) % frames.Length;
            spriteRenderer.sprite = frames[frameIndex];
        }

        private void UpdateProximity()
        {
            if (player == null) return;

            bool nowInRange = ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude <= interactRadius * interactRadius;
            if (nowInRange == inRange) return;

            inRange = nowInRange;
            if (promptText != null) promptText.gameObject.SetActive(inRange && state == TalkState.None);
        }

        // 입력은 한 프레임에 한 번만 소비한다(NoaNpc와 같은 방식).
        private void UpdateInteraction()
        {
            // 인벤토리 창이 떠 있는 동안의 Z/X는 그쪽 몫이다.
            if (inventoryUI != null && inventoryUI.IsOpen) return;

            if (state != TalkState.None && !inRange)
            {
                EndTalk();
                return;
            }

            var keyboard = Keyboard.current;
            bool zPressed = keyboard != null && keyboard.zKey.wasPressedThisFrame;
            bool xPressed = keyboard != null && keyboard.xKey.wasPressedThisFrame;

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
                    if (zPressed)
                    {
                        EndTalk();
                        GameBootstrap.EnterPracticeRoom();
                    }
                    else if (xPressed)
                    {
                        state = TalkState.Response;
                        stateTimer = 0f;
                        dialogueUI?.Show(TalkFrame, declineLine, TalkTextLeft);
                    }
                    break;

                case TalkState.Response:
                    stateTimer += Time.deltaTime;
                    if (zPressed || stateTimer >= responseDuration) EndTalk();
                    break;
            }
        }

        private void EnterOffer()
        {
            state = TalkState.Offer;
            dialogueUI?.Show(TalkFrame, offerLine, TalkTextLeft);
        }

        private void ShowStoryLine()
        {
            var line = FirstMeetingLines[storyIndex];
            if (line.IsNecrosia) dialogueUI?.ShowNecrosia(line.Text + StoryPlayerNextHint);
            else dialogueUI?.Show(TalkFrame, line.Text + StoryNextHint, TalkTextLeft);
        }

        private void EndTalk()
        {
            state = TalkState.None;
            dialogueUI?.Hide();
            if (playerMovement != null) playerMovement.SetInputLocked(false);
            if (promptText != null) promptText.gameObject.SetActive(inRange);
        }
    }
}

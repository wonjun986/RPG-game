using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Characters;
using Aethoria.Quests;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 마을에 서 있는 노아. 제자리에서 깜빡이는 대기 애니메이션을 반복하다가, 플레이어가 가까이 오면
    // 머리 위에 "[Z] 대화하기" 프롬프트를 띄운다. Z를 누르면 인사말이 뜨고, 이어서 상점/퀘스트를
    // 고르는 선택창이 나온다(1=상점, 2=퀘스트, 3=저장). 상점은 구매 화면(ShopUI)을 연다.
    // 저장은 슬롯 5칸짜리 저장 화면(SaveScreenUI)을 연다.
    // 퀘스트는 QuestManager 상태에 따라 의뢰(Z 수락 / X 거절), 진행 상황 안내, 완료 보고(보상 지급)로 갈린다.
    // 받을 퀘스트가 있으면 머리 위에 노란 "!", 보고할 퀘스트가 있으면 "?"를 띄운다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class NoaNpc : MonoBehaviour
    {
        private enum TalkState { None, Greeting, Choice, QuestOffer, Response }

        [SerializeField] private string resourcesPath = "Art/NPC/Noa/Idle";
        [SerializeField] private float frameInterval = 0.5f;
        [SerializeField] private float interactRadius = 2f;
        [SerializeField] private float promptHeight = 2f;
        [SerializeField] private string promptLabel = "[Z] 대화하기";

        [Header("대사")]
        [SerializeField] private float greetingDuration = 2.5f;
        [SerializeField] private float responseDuration = 2.5f;
        [SerializeField] private string greetingLine = "어서 오세요! 마음에 드는 물건이 있으면 편히 둘러보세요.";
        [SerializeField] private string choicePrompt = "무엇을 도와드릴까요?\n[1] 상점   [2] 퀘스트   [3] 저장";
        [SerializeField] private string questAcceptLine = "고마워요! 끝나면 꼭 저한테 알려주세요.";
        [SerializeField] private string questDeclineLine = "그래요, 마음이 바뀌면 다시 말 걸어주세요.";
        [SerializeField] private string noQuestLine = "지금은 부탁드릴 일이 없어요. 늘 고마워요!";
        [SerializeField] private float markerHeight = 2.5f;

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private int frameIndex;
        private float animTimer;
        private Transform player;
        private CharacterMovement2D playerMovement;
        private NpcDialogueUI dialogueUI;
        private TextMesh promptText;
        private TextMesh questMarker;
        private QuestManager quests;
        private SaveScreenUI saveScreen;
        private InventoryUI inventoryUI;
        private ShopUI shopUI;

        private bool inRange;
        private TalkState state;
        private float stateTimer;

        public void Initialize(Transform playerTransform, NpcDialogueUI dialogue, QuestManager questManager, SaveScreenUI saveScreenUI, InventoryUI inventory, ShopUI shop)
        {
            saveScreen = saveScreenUI;
            inventoryUI = inventory;
            shopUI = shop;
            player = playerTransform;
            playerMovement = playerTransform != null ? playerTransform.GetComponent<CharacterMovement2D>() : null;
            dialogueUI = dialogue;
            quests = questManager;
            if (quests != null) quests.OnChanged += RefreshQuestMarker;
            RefreshQuestMarker();
        }

        private void OnDestroy()
        {
            if (quests != null) quests.OnChanged -= RefreshQuestMarker;
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
            CreateQuestMarker();
        }

        private void CreateQuestMarker()
        {
            var go = new GameObject("QuestMarker");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, markerHeight, 0f);

            questMarker = go.AddComponent<TextMesh>();
            questMarker.characterSize = 0.12f;
            questMarker.fontSize = 60;
            questMarker.fontStyle = FontStyle.Bold;
            questMarker.alignment = TextAlignment.Center;
            questMarker.anchor = TextAnchor.MiddleCenter;
            questMarker.color = new Color(1f, 0.85f, 0.2f);

            go.GetComponent<MeshRenderer>().sortingOrder = 50;
            go.SetActive(false);
        }

        private void RefreshQuestMarker()
        {
            if (questMarker == null) return;

            var status = quests != null ? quests.Status : QuestStatus.AllDone;
            bool show = status == QuestStatus.Available || status == QuestStatus.ReadyToTurnIn;
            questMarker.text = status == QuestStatus.ReadyToTurnIn ? "?" : "!";
            questMarker.gameObject.SetActive(show);
        }

        private void CreatePrompt()
        {
            var go = new GameObject("TalkPrompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, promptHeight, 0f);

            promptText = go.AddComponent<TextMesh>();
            promptText.text = promptLabel;
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

            float sqrDist = ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude;
            bool nowInRange = sqrDist <= interactRadius * interactRadius;

            if (nowInRange == inRange) return;

            inRange = nowInRange;
            if (promptText != null) promptText.gameObject.SetActive(inRange && state == TalkState.None);
        }

        // Z/숫자 입력은 한 프레임에 한 번만 소비한다. 대화 단계 전환은 전부 이 함수 하나에서만
        // 일어나서, 같은 프레임의 같은 입력이 "열기"와 "닫기"에 동시에 걸리는 일이 없다.
        private void UpdateInteraction()
        {
            // 저장 화면/인벤토리/상점 창이 떠 있는 동안의 Z는 그쪽 몫이다(다시 말을 거는 것으로 처리하지 않는다).
            if (saveScreen != null && saveScreen.IsOpen) return;
            if (inventoryUI != null && inventoryUI.IsOpen) return;
            if (shopUI != null && shopUI.IsOpen) return;

            if (state != TalkState.None && !inRange)
            {
                EndTalk();
                return;
            }

            var keyboard = Keyboard.current;
            bool zPressed = keyboard != null && keyboard.zKey.wasPressedThisFrame;

            switch (state)
            {
                case TalkState.None:
                    if (inRange && zPressed) EnterGreeting();
                    break;

                case TalkState.Greeting:
                    stateTimer += Time.deltaTime;
                    if (zPressed || stateTimer >= greetingDuration) EnterChoice();
                    break;

                case TalkState.Choice:
                    if (zPressed)
                    {
                        EndTalk();
                        break;
                    }
                    if (keyboard != null && keyboard.digit1Key.wasPressedThisFrame)
                    {
                        EndTalk();
                        if (shopUI != null) shopUI.Show();
                    }
                    else if (keyboard != null && keyboard.digit2Key.wasPressedThisFrame)
                    {
                        HandleQuestChoice();
                    }
                    else if (keyboard != null && keyboard.digit3Key.wasPressedThisFrame)
                    {
                        EndTalk();
                        if (saveScreen != null) saveScreen.Open();
                    }
                    break;

                case TalkState.QuestOffer:
                    if (zPressed)
                    {
                        quests.Accept();
                        EnterResponse(questAcceptLine);
                    }
                    else if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
                    {
                        EnterResponse(questDeclineLine);
                    }
                    break;

                case TalkState.Response:
                    stateTimer += Time.deltaTime;
                    if (zPressed || stateTimer >= responseDuration) EndTalk();
                    break;
            }
        }

        private void EnterGreeting()
        {
            state = TalkState.Greeting;
            stateTimer = 0f;
            if (promptText != null) promptText.gameObject.SetActive(false);
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            dialogueUI?.Show("UI/Noa_Talk", greetingLine);
        }

        private void EnterChoice()
        {
            state = TalkState.Choice;
            stateTimer = 0f;
            dialogueUI?.Show("UI/Noa_Talk", choicePrompt);
        }

        private void HandleQuestChoice()
        {
            var quest = quests != null ? quests.Current : null;
            var status = quests != null ? quests.Status : QuestStatus.AllDone;

            switch (status)
            {
                case QuestStatus.Available:
                    state = TalkState.QuestOffer;
                    stateTimer = 0f;
                    dialogueUI?.Show("UI/Noa_Talk",
                        $"{quest.offerLine}\n<color=#F2D94E>보상: 경험치 {quest.expReward}</color>\n[Z] 수락   [X] 거절");
                    break;

                case QuestStatus.InProgress:
                    EnterResponse($"{quest.title}, 잘 부탁드려요! ({quest.targetMonsterName} {quests.Progress}/{quest.requiredCount})");
                    break;

                case QuestStatus.ReadyToTurnIn:
                    var finished = quests.TurnIn();
                    EnterResponse($"{finished.completeLine}\n<color=#F2D94E>경험치 +{finished.expReward}</color>");
                    break;

                default:
                    EnterResponse(noQuestLine);
                    break;
            }
        }

        private void EnterResponse(string line)
        {
            state = TalkState.Response;
            stateTimer = 0f;
            dialogueUI?.Show("UI/Noa_Talk", line);
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

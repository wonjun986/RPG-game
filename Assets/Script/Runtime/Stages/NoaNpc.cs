using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 마을에 서 있는 노아. 제자리에서 깜빡이는 대기 애니메이션을 반복하다가, 플레이어가 가까이 오면
    // 머리 위에 "[Z] 대화하기" 프롬프트를 띄운다. Z를 누르면 인사말이 뜨고, 이어서 상점/퀘스트를
    // 고르는 선택창이 나온다(1=상점, 2=퀘스트). 실제 구매/판매·퀘스트 시스템은 아직 없어서
    // 무엇을 골라도 "준비 중" 안내만 짧게 보여주고 닫힌다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class NoaNpc : MonoBehaviour
    {
        private enum TalkState { None, Greeting, Choice, Response }

        [SerializeField] private string resourcesPath = "Art/NPC/Noa/Idle";
        [SerializeField] private float frameInterval = 0.5f;
        [SerializeField] private float interactRadius = 2f;
        [SerializeField] private float promptHeight = 2f;
        [SerializeField] private string promptLabel = "[Z] 대화하기";

        [Header("대사")]
        [SerializeField] private float greetingDuration = 2.5f;
        [SerializeField] private float responseDuration = 2.5f;
        [SerializeField] private string greetingLine = "어서 오세요! 마음에 드는 물건이 있으면 편히 둘러보세요.";
        [SerializeField] private string choicePrompt = "무엇을 도와드릴까요?\n[1] 상점   [2] 퀘스트";
        [SerializeField] private string shopResponseLine = "아직 상점을 준비 중이에요. 조금만 기다려 주세요!";
        [SerializeField] private string questResponseLine = "의뢰할 일이 생기면 제일 먼저 알려드릴게요!";

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private int frameIndex;
        private float animTimer;
        private Transform player;
        private NpcDialogueUI dialogueUI;
        private TextMesh promptText;

        private bool inRange;
        private TalkState state;
        private float stateTimer;

        public void Initialize(Transform playerTransform, NpcDialogueUI dialogue)
        {
            player = playerTransform;
            dialogueUI = dialogue;
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
            if (state != TalkState.None) dialogueUI?.Hide();
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
                        EnterResponse(shopResponseLine);
                    }
                    else if (keyboard != null && keyboard.digit2Key.wasPressedThisFrame)
                    {
                        EnterResponse(questResponseLine);
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
            dialogueUI?.Show("UI/Noa_Talk", greetingLine);
        }

        private void EnterChoice()
        {
            state = TalkState.Choice;
            stateTimer = 0f;
            dialogueUI?.Show("UI/Noa_Talk", choicePrompt);
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
            if (promptText != null) promptText.gameObject.SetActive(inRange);
        }
    }
}

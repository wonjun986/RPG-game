using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Characters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 마을 대장간 앞에 서 있는 대장장이 엘리. 대기 애니메이션(4프레임)을 반복하다가 플레이어가 가까이 오면
    // 머리 위에 "[Z] 대화하기"를 띄운다. Z를 누르면 선택지가 뜬다: [1] 장비 강화(EnhanceUI를 연다),
    // [2] 이야기하기(말을 걸 때마다 다음 대사로 넘어감), Z = 닫기.
    [RequireComponent(typeof(SpriteRenderer))]
    public class EllyNpc : MonoBehaviour
    {
        // 엘리 전용 대화 프레임 그림. 대사 칸 왼쪽 위 이름판에 "엘리"를 쓴다.
        private const string TalkFrame = "UI/Elly_Talk";
        private const string SpeakerName = "엘리";
        // 초상화 오른쪽, 이름판 왼쪽 끝(그림 x 약 650px)에서 대사를 시작한다.
        private const float TalkTextLeft = 0.3f;

        [SerializeField] private string resourcesPath = "Art/NPC/Elly/Idle";
        [SerializeField] private float frameInterval = 0.5f;
        [SerializeField] private float interactRadius = 2f;
        [SerializeField] private float promptHeight = 2.1f;
        [SerializeField] private float talkDuration = 3.5f;
        [SerializeField] private string[] lines =
        {
            "어서 와, 엘리의 대장간이야! 불은 늘 지펴 놨으니 언제든 들러.",
            "그 낫, 날이 꽤 상했네. 조만간 내가 제대로 손봐 줄게.",
            "좋은 광석을 구해 오면 더 튼튼한 걸 만들어 줄 수 있어. 기대해도 좋아!",
            "강화석은 몬스터들이 가끔 떨어뜨려. 보스한테선 꼭 나오니까, 센 녀석일수록 놓치지 마!",
            "강화는 단계가 오를수록 실패하기 쉬워져. 그래도 성공했을 때 손맛은... 최고지!",
            "노아 언니가 그러는데, 숲에서 걸어 나왔다며? 그 숲은 대장장이인 나도 무서워서 못 들어가는데.",
            "이시스 언니는 말투가 무뚝뚝해도, 연습장 허수아비는 매일 밤 혼자 고쳐 놓더라. 의외로 다정하다니까.",
            "붉은 달이 뜬 뒤로 광산 쪽 길이 다 막혔어. 쇳물 빛까지 붉게 보인다니까... 기분 탓이겠지?",
            "그 낫 말이야, 보통 쇠가 아니야. 두드릴 때마다 박자를 맞추는 것처럼 울려. 신기하지?",
            "언덕에 갈 거면 장비부터 단단히 챙겨. 그 낫 하나 믿고 덤볐다가 다쳐 오면, 강화 안 해 줄 거야!",
            "강화가 실패해도 너무 낙담하지 마. 나도 처음 망치 잡았을 땐 모루를 세 번이나 깨 먹었거든.",
            "폐허의 돌거인들 몸에 박힌 광석, 그거 꽤 좋은 거래. 혹시 부스러기라도 보이면 챙겨 와 줄래?",
            "바다 쪽에서 돌아온 사람들 말로는 물속에 옛 도시가 있대. 거기 대장간도 있었을까? 궁금하다~",
            "천상의 신전에 무녀님이 계신다며? 축복받은 쇠로 두드려 보는 게 내 평생 소원이야.",
            "망치질에도 박자가 있어. 땅, 땅, 땅! 너무 서두르면 쇠가 울어 버려. 너도 싸울 때 그렇지?",
            "어? 손에 굳은살 박인 것 좀 봐. 낫 쥐는 법이 처음 왔을 때랑 완전히 달라졌네.",
            "노아 언니 가게 물약이랑 내 장비만 있으면 무서울 게 없어! ...아마도.",
            "대장간 불은 밤새 안 꺼트려. 네가 언제 돌아와도 바로 손봐 줄 수 있게. 그러니까 꼭 돌아와야 해!",
        };

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private int frameIndex;
        private float animTimer;
        private Transform player;
        private CharacterMovement2D playerMovement;
        private NpcDialogueUI dialogueUI;
        private InventoryUI inventoryUI;
        private EnhanceUI enhanceUI;
        private TextMesh promptText;

        private enum TalkState { None, Choice, Line }

        private const string ChoiceLine = "뭘 도와줄까?   [1] 장비 강화   [2] 이야기하기   [Z] 닫기";

        private bool inRange;
        private TalkState state;
        private float talkTimer;
        private int nextLine;

        private bool talking => state != TalkState.None;

        public void Initialize(Transform playerTransform, NpcDialogueUI dialogue, InventoryUI inventory, EnhanceUI enhance)
        {
            player = playerTransform;
            playerMovement = playerTransform != null ? playerTransform.GetComponent<CharacterMovement2D>() : null;
            dialogueUI = dialogue;
            inventoryUI = inventory;
            enhanceUI = enhance;
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
            if (!talking) return;
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
            if (promptText != null) promptText.gameObject.SetActive(inRange && !talking);
        }

        // 입력은 한 프레임에 한 번만 소비한다(NoaNpc와 같은 방식).
        private void UpdateInteraction()
        {
            // 인벤토리/강화 창이 떠 있는 동안의 입력은 그쪽 몫이다.
            if (inventoryUI != null && inventoryUI.IsOpen) return;
            if (enhanceUI != null && enhanceUI.IsOpen) return;

            if (talking && !inRange)
            {
                EndTalk();
                return;
            }

            var keyboard = Keyboard.current;
            bool zPressed = keyboard != null && keyboard.zKey.wasPressedThisFrame;

            switch (state)
            {
                case TalkState.None:
                    if (inRange && zPressed) BeginTalk();
                    break;

                case TalkState.Choice:
                    if (zPressed)
                    {
                        EndTalk();
                    }
                    else if (keyboard != null && keyboard.digit1Key.wasPressedThisFrame)
                    {
                        EndTalk();
                        if (enhanceUI != null) enhanceUI.Show();
                    }
                    else if (keyboard != null && keyboard.digit2Key.wasPressedThisFrame)
                    {
                        ShowNextLine();
                    }
                    break;

                case TalkState.Line:
                    talkTimer += Time.deltaTime;
                    if (zPressed || talkTimer >= talkDuration) EndTalk();
                    break;
            }
        }

        private void BeginTalk()
        {
            state = TalkState.Choice;
            if (promptText != null) promptText.gameObject.SetActive(false);
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            dialogueUI?.Show(TalkFrame, ChoiceLine, TalkTextLeft, SpeakerName);
        }

        private void ShowNextLine()
        {
            if (lines == null || lines.Length == 0)
            {
                EndTalk();
                return;
            }

            state = TalkState.Line;
            talkTimer = 0f;
            dialogueUI?.Show(TalkFrame, lines[nextLine], TalkTextLeft, SpeakerName);
            nextLine = (nextLine + 1) % lines.Length;
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

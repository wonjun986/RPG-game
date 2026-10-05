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

using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Characters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 이리아의 성소(천상의 신전의 성소 건물 안) 제단 앞에 서 있는 이리아. 대기 애니메이션(6프레임)을 반복하다가 플레이어가 가까이 오면
    // 머리 위에 "[Z] 대화하기"를 띄운다. Z를 누르면 선택지가 뜬다: [1] 축복 받기(BlessingUI를 연다),
    // [2] 이야기하기(말을 걸 때마다 다음 대사로 넘어감), Z = 닫기(엘리와 같은 방식).
    [RequireComponent(typeof(SpriteRenderer))]
    public class IriaNpc : MonoBehaviour
    {
        private const string TalkFrame = "UI/Iria_Talk";
        private const string SpeakerName = "이리아";
        // 초상화가 대화 칸 왼쪽을 차지하는 폭이 엘리보다 조금 좁아(그림 x 약 600px) 그 오른쪽부터 대사를 시작한다.
        private const float TalkTextLeft = 0.3f;
        // 이름판이 엘리 쪽(그림 y 318~372)보다 훨씬 위쪽에 있다(그림 속 안쪽 흰 칸 기준 x 600~888,
        // y 230~312 — 픽셀 그리드를 직접 찍어서 테두리 금장 안쪽 흰 영역 경계로 측정했다).
        // 기본값 그대로 쓰면 이름표와 대사 칸 위치가 서로 안 맞아서 NpcDialogueUI.Show에 직접 넘긴다.
        private const float NamePlateX0 = 600f, NamePlateY0 = 230f, NamePlateX1 = 888f, NamePlateY1 = 312f;
        // 대사 칸 배경이 밝은 금장/크림색이라 기본 흰 글자(다른 어두운 프레임용)로는 안 보인다.
        // 진한 갈색 계열로 바꿔서 읽히게 한다.
        private static readonly Color TalkTextColor = new Color(0.25f, 0.17f, 0.1f);

        // 다른 마을 NPC(엘리/이시스, 약 1.8유닛)보다 약간 작은 캐릭터 키(1.5, Plains 발판 계산에 쓰는 것과
        // 같은 기준)에 맞춘다. 임포트 PPU 값에 기대지 않고 실제 스프라이트 높이를 재서 직접 비율을 계산한다
        // — 이 프로젝트에서는 새 스프라이트를 임포트할 때 PPU가 간헐적으로 기본값(240)으로 되돌아가는 일이
        // 있어서, 메타 파일의 PPU만 믿으면 크기가 다시 커지는 문제가 있었다.
        private const float TargetHeight = 1.7f;

        [SerializeField] private string resourcesPath = "Art/NPC/Iria/Idle";
        [SerializeField] private float frameInterval = 0.5f;
        [SerializeField] private float interactRadius = 2f;
        [SerializeField] private float promptHeight = 2.4f;
        [SerializeField] private float talkDuration = 4f;
        [SerializeField] private string[] lines =
        {
            "...이 분수 앞에 서 있으면, 물소리만 들어도 마음이 가라앉아.",
            "엔스룸은 다툴 일이 없는 곳이야. 잠깐 쉬었다 가도 좋아.",
            "저 멀리 폭포들 보이지? 저게 다 이 마을을 감싸고 흐르는 물줄기야.",
        };

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private int frameIndex;
        private float animTimer;
        private Transform player;
        private CharacterMovement2D playerMovement;
        private NpcDialogueUI dialogueUI;
        private BlessingUI blessingUI;
        private TextMesh promptText;

        private enum TalkState { None, Choice, Line }

        private const string ChoiceLine = "무엇을 도와줄까?   [1] 축복 받기   [2] 이야기하기   [Z] 닫기";

        private bool inRange;
        private TalkState state;
        private float talkTimer;
        private int nextLine;

        private bool talking => state != TalkState.None;

        public void Initialize(Transform playerTransform, NpcDialogueUI dialogue, BlessingUI blessing)
        {
            player = playerTransform;
            playerMovement = playerTransform != null ? playerTransform.GetComponent<CharacterMovement2D>() : null;
            dialogueUI = dialogue;
            blessingUI = blessing;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            frames = Resources.LoadAll<Sprite>(resourcesPath);
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                spriteRenderer.sprite = frames[0];

                float nativeHeight = frames[0].bounds.size.y;
                if (nativeHeight > 0f)
                {
                    float scale = TargetHeight / nativeHeight;
                    transform.localScale = new Vector3(scale, scale, 1f);
                }
            }

            CreatePrompt();
        }

        private void CreatePrompt()
        {
            // 루트 트랜스폼을 TargetHeight에 맞춰 줄여 놨기 때문에(스프라이트와 같은 오브젝트), 자식인
            // 이 프롬프트도 그대로 두면 같이 작아지고 낮아진다. 부모 배율의 역수로 되돌려서 월드 기준
            // 글자 크기/높이가 다른 NPC 프롬프트와 똑같이 보이게 한다.
            float parentScale = transform.localScale.x;
            float inv = parentScale > 0f ? 1f / parentScale : 1f;

            var go = new GameObject("TalkPrompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, promptHeight * inv, 0f);
            go.transform.localScale = new Vector3(inv, inv, 1f);

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

        // 입력은 한 프레임에 한 번만 소비한다(다른 마을 NPC와 같은 방식).
        private void UpdateInteraction()
        {
            // 축복 창이 떠 있는 동안의 입력은 그쪽 몫이다.
            if (blessingUI != null && blessingUI.IsOpen) return;

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
                        if (blessingUI != null) blessingUI.Show();
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
            dialogueUI?.Show(TalkFrame, ChoiceLine, TalkTextLeft, SpeakerName,
                NamePlateX0, NamePlateY0, NamePlateX1, NamePlateY1, TalkTextColor);
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
            dialogueUI?.Show(TalkFrame, lines[nextLine], TalkTextLeft, SpeakerName,
                NamePlateX0, NamePlateY0, NamePlateX1, NamePlateY1, TalkTextColor);
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

using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Characters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 엔스룸 마을 분수 광장의 릴리. 여행자들에게 들은 이야기를 적어 두는 엔스룸의 이야기꾼이라 몬스터 도감을 갖고 있다.
    // 대기 애니메이션(6프레임, 눈 깜빡임)을 반복하다가 플레이어가 가까이 오면 머리 위에 "[Z] 대화하기"를 띄운다.
    // Z를 누르면 선택지가 뜬다: [1] 도감 보기(BestiaryUI), [2] 이야기하기(말을 걸 때마다 다음 대사), Z = 닫기(엘리와 같은 방식).
    // 대사는 전용 대화 프레임(UI/Lily_Talk)에 쓰고, 그림이 없으면 기본 대사 칸에 이름을 첫 줄로 붙여 쓴다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class LilyNpc : MonoBehaviour
    {
        private const string TalkFrame = "UI/Lily_Talk";
        private const string SpeakerName = "릴리";
        private const string PlainNameTag = "<b><color=#E8B98C>릴리</color></b>\n";
        // Lily_Talk(2172x724): 이름판 안쪽 x 690~828, y 300~348. 어두운 대사 칸은 x 815~2040, y 363~546이라
        // 글자는 테두리에 붙지 않게 칸 안쪽 x 840~1940, y 372~540에만 쓴다(이름판 높이까지 올라가면 위 테두리 장식에 겹친다).
        private const float TalkTextLeft = 840f / 2172f;
        private const float TalkTextRight = 1940f / 2172f;
        private const float TalkTextBottom = 1f - 540f / 724f;
        private const float TalkTextTop = 1f - 372f / 724f;
        private const float NamePlateX0 = 690f, NamePlateY0 = 300f, NamePlateX1 = 828f, NamePlateY1 = 348f;

        // 다른 마을 NPC(엘리/이시스, 약 1.8유닛)와 비슷한 키. 이리아처럼 실제 스프라이트 높이로 배율을 계산해서
        // 임포트 PPU가 바뀌어도 크기가 흔들리지 않게 한다.
        private const float TargetHeight = 1.8f;

        [SerializeField] private string resourcesPath = "Art/NPC/Lily/Idle";
        [SerializeField] private float frameInterval = 0.35f;
        [SerializeField] private float interactRadius = 2f;
        [SerializeField] private float promptHeight = 2.1f;
        [SerializeField] private float talkDuration = 4f;
        [SerializeField] private string[] lines =
        {
            "어머, 처음 보는 얼굴이네. 엔스룸에 온 걸 환영해! 나는 릴리. 이 분수 광장이 내 단골 자리야.",
            "여행자들한테 들은 몬스터 이야기를 도감에 모으고 있어. 네가 싸운 녀석들 얘기도 들려주면, 도감에 예쁘게 적어 둘게!",
            "검은 옷에 커다란 낫이라니... 후후, 소문의 사신님이구나? 생각보다 훨씬 예쁘장한걸.",
            "이 분수 물은 천상에서 흘러내려 온대. 소원을 빌면 이리아 님이 몰래 들어 주신다나 봐.",
            "붉은 달이 뜬 뒤로 다들 밤에 창문을 꼭 닫고 자. 나? 나는 달 구경하는 게 좋아서... 쉿, 비밀이야.",
            "바다 쪽 사람들이 그러는데, 물속 폐허에서 노랫소리가 들린대. 낭만적이지 않아? 조금 무섭기도 하고.",
            "처음 마을의 엘리랑은 편지를 주고받는 사이야. 이번에 새로 만든 머리 장식도 엘리 솜씨라구.",
            "이 마을은 겉보기엔 평화롭지만... 밤이 되면 가끔 언덕 쪽 하늘이 붉게 물들어. 너도 봤니?",
            "싸우러 가는 길이면 무리하지 마. 다치고 돌아오면 내가 붕대 감아 줄 테니까. 아, 그렇다고 일부러 다치진 말고!",
            "언젠가 붉은 달이 사라지면, 이 광장에서 축제를 열 거야. 그때는 꼭 와서 같이 춤춰 줘. 약속!",
        };

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private int frameIndex;
        private float animTimer;
        private Transform player;
        private CharacterMovement2D playerMovement;
        private NpcDialogueUI dialogueUI;
        private BestiaryUI bestiaryUI;
        private TextMesh promptText;
        private bool hasTalkFrame;

        private enum TalkState { None, Choice, Line }

        private const string ChoiceLine = "무슨 이야기 할까?   [1] 도감 보기   [2] 이야기하기   [Z] 닫기";

        private bool inRange;
        private TalkState state;
        private float talkTimer;
        private int nextLine;

        private bool talking => state != TalkState.None;

        public void Initialize(Transform playerTransform, NpcDialogueUI dialogue, BestiaryUI bestiary)
        {
            player = playerTransform;
            playerMovement = playerTransform != null ? playerTransform.GetComponent<CharacterMovement2D>() : null;
            dialogueUI = dialogue;
            bestiaryUI = bestiary;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            hasTalkFrame = Resources.Load<Texture2D>(TalkFrame) != null;

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

        // 루트를 TargetHeight에 맞춰 줄였으니, 자식 프롬프트는 부모 배율의 역수로 되돌려 다른 NPC와 같은 크기/높이로 보이게 한다.
        private void CreatePrompt()
        {
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
            // 도감이 떠 있는 동안의 입력은 그쪽 몫이다.
            if (bestiaryUI != null && bestiaryUI.IsOpen) return;

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
                        if (bestiaryUI != null) bestiaryUI.Show();
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
            ShowLily(ChoiceLine);
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
            ShowLily(lines[nextLine]);
            nextLine = (nextLine + 1) % lines.Length;
        }

        private void ShowLily(string line)
        {
            if (hasTalkFrame)
                dialogueUI?.Show(TalkFrame, line, TalkTextLeft, SpeakerName, NamePlateX0, NamePlateY0, NamePlateX1, NamePlateY1,
                    null, TalkTextBottom, TalkTextRight, namedTextTop: TalkTextTop);
            else dialogueUI?.Show(TalkFrame, PlainNameTag + line);
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

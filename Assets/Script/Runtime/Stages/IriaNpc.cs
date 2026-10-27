using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Characters;
using Aethoria.Quests;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 이리아의 성소(천상의 신전의 성소 건물 안) 제단 앞에 서 있는 이리아. 대기 애니메이션(6프레임)을 반복하다가 플레이어가 가까이 오면
    // 머리 위에 "[Z] 대화하기"를 띄운다. Z를 누르면 선택지가 뜬다: [1] 축복 받기(BlessingUI를 연다),
    // [2] 이야기하기(말을 걸 때마다 다음 대사로 넘어감), Z = 닫기(엘리와 같은 방식).
    // 메인 퀘스트에서 이리아 차례(5장: 찾아와 말 걸기, 6장: 루나벨 토벌 의뢰)면 선택지 대신 그 이야기가 곧바로 나오고,
    // 머리 위에 노란 "!"(찾아오거나 의뢰받을 차례)를 띄운다. 5장을 보고하면 이어서 6장 의뢰까지 한 번에 이어진다.
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

        // 성소 안이 넓고 천장이 높아 1.7로는 작아 보인다는 피드백으로, 다른 마을 NPC(엘리/이시스, 약 1.8유닛)보다
        // 조금 크게(2.1) 맞춘다. 임포트 PPU 값에 기대지 않고 실제 스프라이트 높이를 재서 직접 비율을 계산한다
        // — 이 프로젝트에서는 새 스프라이트를 임포트할 때 PPU가 간헐적으로 기본값(240)으로 되돌아가는 일이
        // 있어서, 메타 파일의 PPU만 믿으면 크기가 다시 커지는 문제가 있었다.
        private const float TargetHeight = 2.1f;

        [SerializeField] private string resourcesPath = "Art/NPC/Iria/Idle";
        [SerializeField] private float frameInterval = 0.5f;
        [SerializeField] private float interactRadius = 2f;
        [SerializeField] private float promptHeight = 2.7f;
        [SerializeField] private float talkDuration = 4f;
        [SerializeField] private float markerHeight = 2.9f;
        [SerializeField] private string[] lines =
        {
            "...이 성소에 있으면, 구름 아래의 소란도 잠시 잊게 돼.",
            "축복은 네 몸을 조금 더 단단하게 붙잡아 줄 거야. 무리하지는 말고.",
            "붉은 달이 뜬 밤이면 이 제단의 빛도 흐려져. 그래서 너를 기다렸던 거야.",
            "사신이 무녀를 찾아오다니, 옛날 이야기였으면 분명 불길한 징조였겠지. ...하지만 난 반가워.",
            "이 성소는 천 년 동안 같은 노래를 불러 왔어. 귀를 기울이면 너도 그 박자를 느낄 수 있을 거야.",
            "루나벨도 한때는 달의 노래를 지키던 아이였어. 언제부터 그 노래가 피 냄새로 바뀌었을까.",
            "전투 중에 숨이 가빠지면, 한 걸음 물러서서 숨을 골라 봐. 서두르지 않는 자가 끝까지 서 있는 법이야.",
            "네 낫에서 들리는 소리, 처음엔 차가웠는데 지금은 조금 따뜻해. 마을에서 좋은 사람들을 만났구나.",
            "구름 아래 마을에 노아라는 아이가 있지? 너를 위해 매일 기도를 올리고 있어. 여기까지 들려.",
            "축복은 영원하지 않아. 그래서 소중한 거야. 필요할 때마다 다시 찾아와 줘.",
            "가끔은 나도 이 성소 밖으로 나가 보고 싶어. ...네가 돌아와서 바깥 이야기를 들려줄래?",
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
        private TextMesh questMarker;
        private QuestManager quests;

        private enum TalkState { None, Choice, Line, Story, QuestOffer }

        private const string StoryNextHint = "   <color=#8A5A2B>[Z]</color>";
        private const string StoryPlayerNextHint = "   <color=#B98CE8>[Z]</color>";
        private const string QuestAcceptLine = "...고마워. 아직 손이 떨리지만, 여기서 계속 기도할게. 네 낫이 다시는 꺾이지 않기를.";
        private const string QuestDeclineLine = "그, 그래... 서두르지 않아도 돼. 준비가 되면 다시 와 줘. 나는 여기 있을게.";

        private QuestStoryLine[] storyLines;
        private int storyIndex;
        private System.Action onStoryDone;

        private const string ChoiceLine = "무엇을 도와줄까?   [1] 축복 받기   [2] 이야기하기   [Z] 닫기";

        private bool inRange;
        private TalkState state;
        private float talkTimer;
        private int nextLine;

        private bool talking => state != TalkState.None;

        public void Initialize(Transform playerTransform, NpcDialogueUI dialogue, BlessingUI blessing, QuestManager questManager)
        {
            player = playerTransform;
            playerMovement = playerTransform != null ? playerTransform.GetComponent<CharacterMovement2D>() : null;
            dialogueUI = dialogue;
            blessingUI = blessing;
            quests = questManager;
            if (quests != null) quests.OnChanged += RefreshQuestMarker;
            RefreshQuestMarker();
        }

        private void OnDestroy()
        {
            if (quests != null) quests.OnChanged -= RefreshQuestMarker;
        }

        // 이리아가 의뢰하거나 찾아와야 하는 차례면 "!", 이리아에게 보고할 차례면 "?".
        private void RefreshQuestMarker()
        {
            if (questMarker == null) return;
            bool business = quests != null && quests.HasBusinessWith(QuestData.Iria);
            questMarker.text = business && quests.Status == QuestStatus.ReadyToTurnIn ? "?" : "!";
            questMarker.gameObject.SetActive(business);
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
            CreateQuestMarker();
        }

        // 프롬프트와 같은 이유로 부모 배율의 역수를 곱해 월드 기준 크기/높이를 다른 NPC 표시와 맞춘다.
        private void CreateQuestMarker()
        {
            float parentScale = transform.localScale.x;
            float inv = parentScale > 0f ? 1f / parentScale : 1f;

            var go = new GameObject("QuestMarker");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, markerHeight * inv, 0f);
            go.transform.localScale = new Vector3(inv, inv, 1f);

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

                case TalkState.Story:
                    if (zPressed)
                    {
                        storyIndex++;
                        if (storyIndex < storyLines.Length)
                        {
                            ShowStoryLine();
                        }
                        else
                        {
                            var done = onStoryDone;
                            onStoryDone = null;
                            done?.Invoke();
                        }
                    }
                    break;

                case TalkState.QuestOffer:
                    if (zPressed)
                    {
                        quests.Accept();
                        ShowLine(QuestAcceptLine);
                    }
                    else if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
                    {
                        ShowLine(QuestDeclineLine);
                    }
                    break;
            }
        }

        // 메인 퀘스트에서 이리아 차례인 장면을 이어서 보여 준다. 보고 → (다음 퀘스트도 이리아가 의뢰하면) 의뢰까지 한 번에 이어진다.
        private void ContinueQuest()
        {
            var quest = quests != null ? quests.Current : null;
            if (quest == null)
            {
                EndTalk();
                return;
            }

            // 5장: 찾아와 말을 건 것 자체가 목표라, 여기서 달성 처리한 뒤 바로 보고 장면으로 넘어간다.
            quests.CompleteTalk(QuestData.Iria);

            if (quests.Status == QuestStatus.ReadyToTurnIn && quest.turnInNpc == QuestData.Iria)
            {
                PlayStory(quest.completeStory, () =>
                {
                    var finished = quests.TurnIn();
                    var next = quests.Current;
                    string reward = $"{finished.completeLine}\n<color=#7A4A12>경험치 +{finished.expReward}</color>";
                    if (next != null && quests.Status == QuestStatus.Available && next.giverNpc == QuestData.Iria)
                    {
                        // 보상 안내를 한 장 보여 준 뒤 [Z]로 다음 의뢰 이야기로 넘어간다.
                        PlayStory(new[] { new QuestStoryLine(false, reward) }, ContinueQuest);
                    }
                    else
                    {
                        ShowLine(reward);
                    }
                });
            }
            else if (quests.Status == QuestStatus.Available && quest.giverNpc == QuestData.Iria)
            {
                PlayStory(quest.offerStory, () =>
                {
                    state = TalkState.QuestOffer;
                    ShowIria($"{quest.offerLine}\n<color=#7A4A12>보상: 경험치 {quest.expReward}</color>   [Z] 수락   [X] 거절");
                });
            }
            else
            {
                EndTalk();
            }
        }

        // 이리아 ↔ 네크로시아 대사를 [Z]로 한 줄씩 넘기고, 다 보면 done을 부른다. 대사가 없으면 바로 done.
        private void PlayStory(QuestStoryLine[] storyToPlay, System.Action done)
        {
            if (storyToPlay == null || storyToPlay.Length == 0)
            {
                done();
                return;
            }
            state = TalkState.Story;
            storyLines = storyToPlay;
            storyIndex = 0;
            onStoryDone = done;
            ShowStoryLine();
        }

        private void ShowStoryLine()
        {
            var line = storyLines[storyIndex];
            if (line.isNecrosia) dialogueUI?.ShowNecrosia(line.text + StoryPlayerNextHint);
            else ShowIria(line.text + StoryNextHint);
        }

        private void ShowIria(string text)
        {
            dialogueUI?.Show(TalkFrame, text, TalkTextLeft, SpeakerName,
                NamePlateX0, NamePlateY0, NamePlateX1, NamePlateY1, TalkTextColor);
        }

        // 한 장짜리 대사를 띄우고 [Z]나 시간이 지나면 닫는다.
        private void ShowLine(string text)
        {
            state = TalkState.Line;
            talkTimer = 0f;
            ShowIria(text);
        }

        private void BeginTalk()
        {
            if (promptText != null) promptText.gameObject.SetActive(false);
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            if (quests != null && quests.HasBusinessWith(QuestData.Iria))
            {
                ContinueQuest();
                return;
            }
            state = TalkState.Choice;
            ShowIria(ChoiceLine);
        }

        private void ShowNextLine()
        {
            if (lines == null || lines.Length == 0)
            {
                EndTalk();
                return;
            }

            ShowLine(lines[nextLine]);
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

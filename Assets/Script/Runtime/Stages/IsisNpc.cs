using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 마을의 연습장 앞에 서 있는 이시스. 대기 애니메이션(4프레임)을 반복하다가 플레이어가 가까이 오면
    // 머리 위에 "[Z] 대화하기"를 띄운다. Z를 누르면 연습장에 들어갈지 묻고(Z 입장 / X 취소),
    // 입장하면 허수아비가 서 있는 연습장 맵으로 이동한다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class IsisNpc : MonoBehaviour
    {
        private enum TalkState { None, Offer, Response }

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
                        state = TalkState.Offer;
                        if (promptText != null) promptText.gameObject.SetActive(false);
                        if (playerMovement != null) playerMovement.SetInputLocked(true);
                        dialogueUI?.Show(TalkFrame, offerLine, TalkTextLeft);
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

        private void EndTalk()
        {
            state = TalkState.None;
            dialogueUI?.Hide();
            if (playerMovement != null) playerMovement.SetInputLocked(false);
            if (promptText != null) promptText.gameObject.SetActive(inRange);
        }
    }
}

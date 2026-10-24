using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Quests;
using Aethoria.Stages;

namespace Aethoria.UI
{
    // 마을 출구 포탈을 밟으면 뜨는 월드맵(UI/All_Area_Map). 지금 실제로 갈 수 있는 지역은
    // 어둠의 숲(기존 1~3스테이지+보스), 광활한 평야, 잊혀진 폐허, 푸른 바다, 엔스룸 마을, 천상으로 향하는 계단,
    // 절망의 언덕이다. 지역은 메인 퀘스트(스토리) 진행에 맞춰 열린다 — 보고한 메인 퀘스트 수(QuestManager.QuestIndex)가
    // 그 지역의 RequiredQuests에 못 미치면 어둡게 덮고, 고르면 "이야기를 더 진행해야 한다"는 안내만 띄운다.
    // 관리자 모드(AdminUI)에서 AdminUnlockAll을 켜면 전부 열린다.
    // 잠긴 지역 위에는 자물쇠 쇠사슬(UI/MapChain/chain_1)이 걸려 있다. 퀘스트를 보고해 지역이 새로 열린 뒤 처음 지도를 열면,
    // 지도가 그 지역으로 확대되면서 쇠사슬이 부서지는 연출(chain_1~8)이 나오고 "○○ 해금!"이 뜬 뒤 다시 축소된다.
    // 어디까지 연출을 봤는지(unlockSeen = 그때의 메인 퀘스트 번호)는 저장 파일에 남는다. SD 캐릭터(UI/SD)가
    // 커서 역할을 하며 방향키로 지역
    // 사이를 옮겨 다니고, Enter/Z로 선택한다. "마을"을 선택하거나 Esc를 누르면 "마을로
    // 이동하시겠습니까?" 확인 창이 뜨고, 예를 골라야 실제로 창을 닫는다.
    public class WorldMapUI : MonoBehaviour
    {
        private class MapNode
        {
            public string Name;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public int RequiredQuests; // 이만큼 메인 퀘스트를 보고해야 열린다(0 = 처음부터 열림)
            public System.Action OnSelect;
            public GameObject LockOverlay;
            public RawImage Chain;

            public Vector2 Center => (AnchorMin + AnchorMax) * 0.5f;
        }

        private const float AlertDuration = 1.6f;

        private GameObject panel;
        private RectTransform cursor;
        private Text alertText;
        private Coroutine alertRoutine;
        private Portal sourcePortal;
        private CharacterMovement2D playerMovement;
        private QuestManager quests;

        // 관리자 모드: 스토리 진행과 상관없이 모든 지역을 연다.
        public static bool AdminUnlockAll { get; set; }

        private MapNode[] nodes;
        private MapNode villageNode;
        private int currentIndex;

        private GameObject confirmPanel;
        private bool confirmingExit;

        // ---- 쇠사슬 해금 연출 ----
        private const string ChainFramesPath = "UI/MapChain/chain_";
        private const int ChainFrameCount = 8;
        private static readonly Vector2 ChainSize = new Vector2(84f, 112f); // 캔버스(1280x720) 기준, 원본 384x512 비율
        private const float ZoomScale = 2.2f;
        private const float ZoomInDuration = 0.6f;
        private const float ZoomOutDuration = 0.5f;
        private const float ChainFrameInterval = 0.11f;
        private const float BannerHold = 1.1f;

        private readonly Texture2D[] chainFrames = new Texture2D[ChainFrameCount];
        private Text unlockBanner;
        private int unlockSeen;
        private bool playingUnlock;

        // 쇠사슬 해금 연출을 어디까지 봤는지(그때의 메인 퀘스트 번호). 저장/불러오기에 쓴다.
        public int UnlockSeen => unlockSeen;
        public void SetUnlockSeen(int questIndex) => unlockSeen = Mathf.Max(0, questIndex);

        public void Initialize(CharacterMovement2D movement)
        {
            playerMovement = movement;
            BuildPanel();
        }

        // QuestManager는 HUD에서 월드맵보다 늦게 만들어져서 따로 넘겨받는다.
        public void SetQuestManager(QuestManager questManager)
        {
            quests = questManager;
        }

        private bool IsLocked(MapNode node)
        {
            if (AdminUnlockAll || node.RequiredQuests <= 0 || quests == null) return false;
            return quests.QuestIndex < node.RequiredQuests;
        }

        private void BuildPanel()
        {
            TitleScreenUI.EnsureEventSystem();

            panel = new GameObject("WorldMapPanel", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/All_Area_Map");

            // 아래 좌표들은 All_Area_Map.png 안에 이미 그려진 각 지역 라벨 위치에 맞춘 값
            // (0~1, 왼쪽 아래 기준). 마을 = 취소하고 그대로 있기.
            // RequiredQuests는 QuestDatabase 순서(0 슬라임, 1 보스 슬라임, 2 기사, 3 보스 기사, 4 콜로서스, 5 심해 정령,
            // 6 이리아 찾기, 7 루나벨)에서 "그 지역을 소개하는 퀘스트의 번호" — 앞 퀘스트를 보고해야 열린다.
            nodes = new[]
            {
                new MapNode { Name = "마을", AnchorMin = new Vector2(0.11f, 0.69f), AnchorMax = new Vector2(0.25f, 0.81f) },
                new MapNode { Name = "어둠의 숲", AnchorMin = new Vector2(0.34f, 0.67f), AnchorMax = new Vector2(0.48f, 0.78f), RequiredQuests = 2, OnSelect = HandleEnterDarkForest },
                new MapNode { Name = "잊혀진 폐허", AnchorMin = new Vector2(0.59f, 0.68f), AnchorMax = new Vector2(0.73f, 0.79f), RequiredQuests = 4, OnSelect = HandleEnterRuins },
                new MapNode { Name = "절망의 언덕", AnchorMin = new Vector2(0.78f, 0.74f), AnchorMax = new Vector2(0.92f, 0.84f), RequiredQuests = 7, OnSelect = HandleEnterHill },
                new MapNode { Name = "광활한 평야", AnchorMin = new Vector2(0.17f, 0.42f), AnchorMax = new Vector2(0.33f, 0.52f), OnSelect = HandleEnterPlains },
                new MapNode { Name = "엔스룸 마을", AnchorMin = new Vector2(0.39f, 0.27f), AnchorMax = new Vector2(0.54f, 0.36f), OnSelect = HandleEnterEnsroom },
                new MapNode { Name = "푸른 바다", AnchorMin = new Vector2(0.73f, 0.35f), AnchorMax = new Vector2(0.88f, 0.44f), RequiredQuests = 5, OnSelect = HandleEnterBlueSea },
                new MapNode { Name = "천상으로 향하는 계단", AnchorMin = new Vector2(0.433f, 0.082f), AnchorMax = new Vector2(0.531f, 0.121f), RequiredQuests = 6, OnSelect = HandleEnterTemple },
            };

            villageNode = nodes[0];

            foreach (var node in nodes)
            {
                var localNode = node;
                BuildNodeButton(panel.transform, localNode);
            }

            for (int i = 0; i < ChainFrameCount; i++) chainFrames[i] = Resources.Load<Texture2D>(ChainFramesPath + (i + 1));
            foreach (var node in nodes)
            {
                if (node.RequiredQuests > 0) BuildChain(panel.transform, node);
            }

            BuildCursor(panel.transform);
            BuildAlertText(panel.transform);
            BuildConfirmDialog(panel.transform);
            BuildUnlockBanner();

            panel.SetActive(false);
        }

        // 지역 라벨 한가운데에 거는 자물쇠 쇠사슬. 지도와 함께 확대/축소되도록 지도 패널의 자식으로 둔다.
        private void BuildChain(Transform parent, MapNode node)
        {
            if (chainFrames[0] == null) return;

            var go = new GameObject(node.Name + "_Chain", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = node.Center;
            rect.sizeDelta = ChainSize;

            node.Chain = go.GetComponent<RawImage>();
            node.Chain.texture = chainFrames[0];
            node.Chain.raycastTarget = false;
        }

        // 확대되는 지도 위가 아니라 화면 기준으로 띄우는 "○○ 해금!" 글자.
        private void BuildUnlockBanner()
        {
            var go = new GameObject("UnlockBanner", typeof(RectTransform), typeof(Text), typeof(Outline));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0f, 0.12f);
            rect.anchorMax = new Vector2(1f, 0.26f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            unlockBanner = go.GetComponent<Text>();
            unlockBanner.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            unlockBanner.fontSize = 44;
            unlockBanner.fontStyle = FontStyle.Bold;
            unlockBanner.alignment = TextAnchor.MiddleCenter;
            unlockBanner.color = new Color(0.85f, 0.75f, 1f);
            unlockBanner.raycastTarget = false;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.02f, 0.25f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
            go.SetActive(false);
        }

        // 클릭으로도 고를 수 있게, 잠긴 지역이면 안내문을 띄우고 아니면 선택한다(키보드 확인과 동일한 경로).
        private void BuildNodeButton(Transform parent, MapNode node)
        {
            TitleScreenUI.BuildButton(parent, node.Name, node.AnchorMin, node.AnchorMax, () =>
            {
                int index = System.Array.IndexOf(nodes, node);
                if (index >= 0) currentIndex = index;
                MoveCursorTo(node);
                TrySelect(node);
            });

            // 잠금 덮개는 미리 만들어 두고, 지도를 열 때마다(Show) 지금 진행도에 맞춰 켜고 끈다.
            if (node.RequiredQuests > 0)
            {
                var go = new GameObject(node.Name + "_Locked", typeof(RectTransform), typeof(Image));
                node.LockOverlay = go;
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = node.AnchorMin;
                rect.anchorMax = node.AnchorMax;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.SetAsFirstSibling(); // 버튼보다 뒤에 그려서 클릭은 버튼이 그대로 받게 한다

                var image = go.GetComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0.6f);
                image.raycastTarget = false;
            }
        }

        private void BuildCursor(Transform parent)
        {
            var go = new GameObject("Cursor", typeof(RectTransform), typeof(RawImage));
            cursor = (RectTransform)go.transform;
            cursor.SetParent(parent, false);
            cursor.sizeDelta = new Vector2(72f, 76.6f);
            cursor.anchorMin = cursor.anchorMax = Vector2.zero;
            cursor.pivot = new Vector2(0.5f, 0.15f); // 발밑이 지역 라벨 위치에 오도록

            var image = go.GetComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>("UI/SD");
            image.raycastTarget = false;
        }

        private void BuildAlertText(Transform parent)
        {
            var go = new GameObject("AlertText", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.06f);
            rect.anchorMax = new Vector2(0.5f, 0.06f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(700f, 50f);

            alertText = go.GetComponent<Text>();
            alertText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            alertText.fontSize = 26;
            alertText.fontStyle = FontStyle.Bold;
            alertText.alignment = TextAnchor.MiddleCenter;
            alertText.color = new Color(1f, 0.4f, 0.4f);
            alertText.text = string.Empty;
        }

        // 마을 칸을 고르거나 Esc를 누르면 뜨는 확인 창. 실수로 지도를 닫는 걸 막는다.
        // Z/Enter = 예(그대로 진행), X/Esc = 아니오(지도로 돌아가기).
        private void BuildConfirmDialog(Transform parent)
        {
            confirmPanel = new GameObject("ExitConfirmPanel", typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)confirmPanel.transform;
            dimRect.SetParent(parent, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImage = confirmPanel.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.65f);
            dimImage.raycastTarget = true; // 뒤쪽 지도 클릭을 막는다

            var boxGO = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var boxRect = (RectTransform)boxGO.transform;
            boxRect.SetParent(confirmPanel.transform, false);
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(560f, 230f);
            boxGO.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.09f, 0.97f);

            var messageGO = new GameObject("Message", typeof(RectTransform), typeof(Text));
            var messageRect = (RectTransform)messageGO.transform;
            messageRect.SetParent(boxRect, false);
            messageRect.anchorMin = new Vector2(0f, 0.5f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.offsetMin = Vector2.zero;
            messageRect.offsetMax = Vector2.zero;
            var messageText = messageGO.GetComponent<Text>();
            messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            messageText.fontSize = 28;
            messageText.fontStyle = FontStyle.Bold;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.color = Color.white;
            messageText.text = "마을로 이동하시겠습니까?";

            BuildDialogButton(boxRect, "YesButton", "예 (Z)", new Vector2(0.08f, 0.12f), new Vector2(0.47f, 0.42f), ConfirmExitYes);
            BuildDialogButton(boxRect, "NoButton", "아니오 (X)", new Vector2(0.53f, 0.12f), new Vector2(0.92f, 0.42f), ConfirmExitNo);

            confirmPanel.SetActive(false);
        }

        internal static void BuildDialogButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
        }

        public void Show(Portal portal)
        {
            sourcePortal = portal;
            currentIndex = 0;
            confirmingExit = false;
            confirmPanel.SetActive(false);
            panel.SetActive(true);
            ResetZoom();
            unlockBanner.gameObject.SetActive(false);

            // 마지막으로 연출을 본 뒤에 새로 열린 지역은 아직 잠긴 모습(쇠사슬)으로 두었다가 연출에서 부순다.
            var newlyUnlocked = new System.Collections.Generic.List<MapNode>();
            int questIndex = quests != null ? quests.QuestIndex : 0;
            foreach (var node in nodes)
            {
                bool locked = IsLocked(node);
                bool pending = !locked && node.RequiredQuests > unlockSeen && node.RequiredQuests <= questIndex;
                if (pending) newlyUnlocked.Add(node);
                if (node.LockOverlay != null) node.LockOverlay.SetActive(locked || pending);
                if (node.Chain != null)
                {
                    node.Chain.gameObject.SetActive(locked || pending);
                    node.Chain.texture = chainFrames[0];
                    node.Chain.color = Color.white;
                }
            }
            MoveCursorTo(nodes[currentIndex]);
            HideAlert();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;

            if (newlyUnlocked.Count > 0) StartCoroutine(PlayUnlocks(newlyUnlocked));
            else unlockSeen = Mathf.Max(unlockSeen, questIndex);
        }

        // 새로 열린 지역마다 차례로: 그 지역으로 확대 → 쇠사슬이 금 가고 부서져 소용돌이로 사라짐 → "○○ 해금!" → 축소.
        // 연출 중에는 지도 조작을 막고, 끝나면 커서를 마지막으로 열린 지역에 둔다.
        private IEnumerator PlayUnlocks(System.Collections.Generic.List<MapNode> unlocked)
        {
            playingUnlock = true;
            yield return new WaitForSecondsRealtime(0.3f);

            foreach (var node in unlocked)
            {
                yield return Zoom(1f, ZoomScale, node.Center, ZoomInDuration);
                yield return new WaitForSecondsRealtime(0.25f);

                if (node.Chain != null)
                {
                    for (int i = 1; i < ChainFrameCount; i++)
                    {
                        node.Chain.texture = chainFrames[i] != null ? chainFrames[i] : node.Chain.texture;
                        // 부서지는 순간(5번째 프레임)에 덮개를 걷어 낸다.
                        if (i == 4 && node.LockOverlay != null) node.LockOverlay.SetActive(false);
                        yield return new WaitForSecondsRealtime(ChainFrameInterval);
                    }
                    // 마지막 소용돌이는 천천히 흐려지며 사라진다.
                    for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
                    {
                        node.Chain.color = new Color(1f, 1f, 1f, 1f - t / 0.4f);
                        yield return null;
                    }
                    node.Chain.gameObject.SetActive(false);
                }
                if (node.LockOverlay != null) node.LockOverlay.SetActive(false);

                unlockBanner.text = $"{node.Name} 해금!";
                unlockBanner.gameObject.SetActive(true);
                yield return new WaitForSecondsRealtime(BannerHold);
                unlockBanner.gameObject.SetActive(false);

                yield return Zoom(ZoomScale, 1f, node.Center, ZoomOutDuration);

                int index = System.Array.IndexOf(nodes, node);
                if (index >= 0)
                {
                    currentIndex = index;
                    MoveCursorTo(node);
                }
            }

            unlockSeen = Mathf.Max(unlockSeen, quests != null ? quests.QuestIndex : 0);
            playingUnlock = false;
        }

        // 지도 패널을 focus(0~1, 왼쪽 아래 기준) 지점을 화면 가운데로 끌어오며 확대/축소한다.
        // 가장자리가 비어 보이지 않도록 이동량은 지도가 화면을 다 덮는 범위 안으로 제한한다.
        private IEnumerator Zoom(float from, float to, Vector2 focus, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                ApplyZoom(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)), focus);
                yield return null;
            }
            ApplyZoom(to, focus);
        }

        private void ApplyZoom(float scale, Vector2 focus)
        {
            var rect = (RectTransform)panel.transform;
            Vector2 size = rect.rect.size;
            Vector2 offset = -(focus - new Vector2(0.5f, 0.5f)) * size * scale;
            Vector2 limit = size * (scale - 1f) * 0.5f;
            offset = new Vector2(Mathf.Clamp(offset.x, -limit.x, limit.x), Mathf.Clamp(offset.y, -limit.y, limit.y));
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.anchoredPosition = offset;
        }

        private void ResetZoom()
        {
            var rect = (RectTransform)panel.transform;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;
        }

        private void Hide()
        {
            if (playingUnlock)
            {
                // 연출 도중에 닫히는 경우(씬 전환 등)에도 다음에 같은 연출이 다시 나오지 않게 정리한다.
                StopAllCoroutines();
                playingUnlock = false;
                unlockSeen = Mathf.Max(unlockSeen, quests != null ? quests.QuestIndex : 0);
            }
            ResetZoom();
            unlockBanner.gameObject.SetActive(false);
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void Update()
        {
            if (panel == null || !panel.activeSelf) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 쇠사슬 해금 연출 중에는 지도 조작을 받지 않는다.
            if (playingUnlock) return;

            if (confirmingExit)
            {
                if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    ConfirmExitYes();
                }
                else if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                {
                    ConfirmExitNo();
                }
                return;
            }

            // 굳이 마을 칸까지 이동하지 않아도 Esc 한 번으로 나갈지 물어볼 수 있게 한다.
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                ShowExitConfirm();
                return;
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame) MoveSelection(Vector2.left);
            else if (keyboard.rightArrowKey.wasPressedThisFrame) MoveSelection(Vector2.right);
            else if (keyboard.upArrowKey.wasPressedThisFrame) MoveSelection(Vector2.up);
            else if (keyboard.downArrowKey.wasPressedThisFrame) MoveSelection(Vector2.down);

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                TrySelect(nodes[currentIndex]);
            }
        }

        // 현재 노드에서 direction 쪽으로 가장 "정면"에 가까우면서 가까운 노드를 찾아 커서를 옮긴다.
        // (같은 방향으로의 치우침이 클수록, 거리가 가까울수록 우선한다 — 표준 방향키 메뉴 내비게이션 방식)
        private void MoveSelection(Vector2 direction)
        {
            Vector2 currentPos = nodes[currentIndex].Center;
            int bestIndex = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < nodes.Length; i++)
            {
                if (i == currentIndex) continue;

                Vector2 delta = nodes[i].Center - currentPos;
                float along = Vector2.Dot(delta, direction);
                if (along <= 0.01f) continue;

                float perpendicular = (delta - direction * along).magnitude;
                float score = along + perpendicular * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0) return;

            currentIndex = bestIndex;
            MoveCursorTo(nodes[currentIndex]);
            HideAlert();
        }

        private void MoveCursorTo(MapNode node)
        {
            if (cursor == null) return;
            cursor.anchorMin = cursor.anchorMax = node.Center;
            cursor.anchoredPosition = Vector2.zero;
        }

        private void TrySelect(MapNode node)
        {
            if (playingUnlock) return; // 해금 연출 중의 마우스 클릭도 무시한다
            if (IsLocked(node))
            {
                ShowAlert($"{node.Name}은(는) 아직 갈 수 없어요. 이야기를 더 진행해 보세요.");
                return;
            }

            if (node == villageNode)
            {
                ShowExitConfirm();
                return;
            }

            node.OnSelect?.Invoke();
        }

        private void ShowExitConfirm()
        {
            confirmingExit = true;
            confirmPanel.SetActive(true);
        }

        private void ConfirmExitYes()
        {
            confirmingExit = false;
            confirmPanel.SetActive(false);
            HandleCancel();
        }

        private void ConfirmExitNo()
        {
            confirmingExit = false;
            confirmPanel.SetActive(false);
        }

        private void ShowAlert(string message)
        {
            alertText.text = message;
            if (alertRoutine != null) StopCoroutine(alertRoutine);
            alertRoutine = StartCoroutine(HideAlertAfterDelay());
        }

        private void HideAlert()
        {
            if (alertRoutine != null) StopCoroutine(alertRoutine);
            alertText.text = string.Empty;
        }

        // 월드맵이 떠 있는 동안 Time.timeScale = 0이라 WaitForSeconds는 흐르지 않는다.
        // 실시간으로 흐르는 WaitForSecondsRealtime을 써야 실제로 잠깐 뒤에 사라진다.
        private IEnumerator HideAlertAfterDelay()
        {
            yield return new WaitForSecondsRealtime(AlertDuration);
            alertText.text = string.Empty;
            alertRoutine = null;
        }

        private void HandleEnterDarkForest()
        {
            Hide();
            GameBootstrap.EnterNextStage();
        }

        private void HandleEnterPlains()
        {
            Hide();
            GameBootstrap.EnterPlains();
        }

        private void HandleEnterRuins()
        {
            Hide();
            GameBootstrap.EnterRuins();
        }

        private void HandleEnterBlueSea()
        {
            Hide();
            GameBootstrap.EnterBlueSea();
        }

        private void HandleEnterEnsroom()
        {
            Hide();
            GameBootstrap.EnterEnsroom();
        }

        private void HandleEnterTemple()
        {
            Hide();
            GameBootstrap.EnterTemple();
        }

        private void HandleEnterHill()
        {
            Hide();
            GameBootstrap.EnterHill();
        }

        private void HandleCancel()
        {
            Hide();
            sourcePortal?.ResetTrigger();
        }
    }
}

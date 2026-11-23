using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Data;
using Aethoria.Quests;
using Aethoria.Stages;

namespace Aethoria.UI
{
    // 마을 출구 포탈을 밟으면 뜨는 월드맵(UI/All_Area_Map). 지금 실제로 갈 수 있는 지역은
    // 어둠의 숲(기존 1~3스테이지+보스), 광활한 평야, 잊혀진 폐허, 푸른 바다, 엔스룸 마을, 천상의 신전,
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
            public string ChestRegion; // 보물상자가 숨겨진 사냥터면 TreasureChests 지역 키(지역 아래에 "보물 n/m"을 띄운다)
            public Text ChestLabel;
            public string Description; // 지역을 고르면 뜨는 설명 창의 본문
            public string ImageKey;    // 설명 창 왼쪽 풍경 그림(UI/WorldMap/RegionInfo/region_키)

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


        // ---- 쇠사슬 해금 연출 ----
        private const string ChainFramesPath = "UI/WorldMap/MapChain/chain_";
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
            panel.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/WorldMap/All_Area_Map");

            // 지도(UI/WorldMap/All_Area_Map, 1672x941)에는 글자가 없고 지역마다 길 위에 빛나는 표시점이 그려져 있다.
            // 노드는 그 표시점(그림 픽셀)을 가운데로 잡고, 지역 이름은 그 아래에 코드로 쓴다. 마을 = 취소하고 그대로 있기.
            // RequiredQuests는 QuestDatabase 순서(0 슬라임, 1 보스 슬라임, 2 기사, 3 보스 기사, 4 콜로서스, 5 황금 모래룡,
            // 6 이리아 찾기, 7 심해 정령, 8 아쿠아, 9 루나벨)에서 "그 지역을 소개하는 퀘스트의 번호" — 앞 퀘스트를 보고해야 열린다.
            // 이야기 순서: 평야 → 엔스룸 마을 → 어둠의 숲 → 잊혀진 폐허 → 천상의 신전 → 푸른 바다 → 절망의 언덕.
            nodes = new[]
            {
                At("마을", 176f, 730f),
                At("광활한 평야", 360f, 565f, onSelect: HandleEnterPlains, chests: TreasureChests.Plains),
                At("엔스룸 마을", 665f, 772f, required: 2, onSelect: HandleEnterEnsroom),
                At("어둠의 숲", 685f, 378f, required: 2, onSelect: HandleEnterDarkForest, chests: TreasureChests.Forest),
                At("잊혀진 폐허", 1040f, 316f, required: 4, onSelect: HandleEnterRuins, chests: TreasureChests.Ruins),
                At("천상의 신전", 1000f, 690f, required: 6, onSelect: HandleEnterTemple),
                At("푸른 바다", 1385f, 573f, required: 7, onSelect: HandleEnterBlueSea, chests: TreasureChests.Sea),
                At("절망의 언덕", 1465f, 270f, required: 9, onSelect: HandleEnterHill),
            };

            villageNode = nodes[0];

            Describe("광활한 평야", "plains",
                "바람이 끝없이 불어오는 초록빛 들판.\n숲에서 깨어난 네크로시아가 낫을 쥐는 감각을 되찾기 시작한 곳이다.");
            Describe("엔스룸 마을", "ensroom",
                "평야 너머에 자리한 조용한 마을.\n여행자들이 쉬어 가며 소문과 의뢰가 모여드는 곳이다.");
            Describe("어둠의 숲", "forest",
                "햇빛조차 닿지 않는 깊은 숲.\n붉은 기운에 붙들린 기사들이 길을 막고 서 있다.");
            Describe("잊혀진 폐허", "ruins",
                "모래에 반쯤 묻힌 옛 성소.\n아주 오래전 사신을 섬기던 자들의 흔적이 남아 있다.");
            Describe("천상의 신전", "stairs",
                "구름 위에 떠 있는 하얀 대리석 신전.\n성소 깊은 곳에서 천상의 무녀 이리아가 기다리고 있다.");
            Describe("푸른 바다", "sea",
                "붉은 달빛이 처음 스며들었다는 깊고 푸른 바다.\n물결 아래에서 무언가가 깨어나고 있다.");
            Describe("절망의 언덕", "hill",
                "붉은 달이 가장 가까이 떠오르는 언덕.\n모든 이야기가 시작된 그곳에서 루나벨이 기다린다.");

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
            BuildLabels(panel.transform); // 커서·사슬보다 위에
            BuildAlertText(panel.transform);
            BuildInfoDialog();
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

        private const float MapImageW = 1672f, MapImageH = 941f;
        private const float NodeHalfW = 0.06f, NodeHalfH = 0.065f;   // 클릭 영역·잠금 덮개 크기(화면 비율)

        // 지도 그림 속 표시점(px, py: 왼쪽 위 기준 픽셀)을 가운데로 하는 노드.
        private static MapNode At(string name, float px, float py, int required = 0, System.Action onSelect = null, string chests = null)
        {
            var c = new Vector2(px / MapImageW, 1f - py / MapImageH);
            return new MapNode
            {
                Name = name,
                AnchorMin = c - new Vector2(NodeHalfW, NodeHalfH),
                AnchorMax = c + new Vector2(NodeHalfW, NodeHalfH),
                RequiredQuests = required,
                OnSelect = onSelect,
                ChestRegion = chests,
            };
        }

        private static Text MakeMapLabel(Transform parent, string name, Vector2 min, Vector2 max, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.02f, 0.1f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        // 표시점 아래에 지역 이름(어두운 이름판 위), 그 아래에 보물상자 수집 현황("보물 1/3", 지도를 열 때마다 Show에서 갱신).
        // 밝은 구름·하늘 위에서도 읽히도록 이름 뒤에 부드러운 어두운 판을 깐다.
        private void BuildLabels(Transform parent)
        {
            foreach (var node in nodes)
            {
                Vector2 c = node.Center;
                float halfW = 0.012f + node.Name.Length * 0.0105f;
                var plate = new GameObject(node.Name + "_Plate", typeof(RectTransform), typeof(Image));
                var plateRect = (RectTransform)plate.transform;
                plateRect.SetParent(parent, false);
                plateRect.anchorMin = new Vector2(c.x - halfW, c.y - 0.083f);
                plateRect.anchorMax = new Vector2(c.x + halfW, c.y - 0.03f);
                plateRect.offsetMin = plateRect.offsetMax = Vector2.zero;
                var plateImage = plate.GetComponent<Image>();
                plateImage.sprite = Aethoria.Fx.FxUtil.SoftCircleSprite;
                plateImage.color = new Color(0.05f, 0.02f, 0.1f, 0.7f);
                plateImage.raycastTarget = false;

                MakeMapLabel(parent, node.Name + "_Name", new Vector2(c.x - 0.12f, c.y - 0.08f), new Vector2(c.x + 0.12f, c.y - 0.033f),
                    24, new Color(1f, 0.95f, 0.85f));
                if (node.ChestRegion != null)
                {
                    node.ChestLabel = MakeMapLabel(parent, node.Name + "_Chests", new Vector2(c.x - 0.1f, c.y - 0.115f), new Vector2(c.x + 0.1f, c.y - 0.08f),
                        16, new Color(0.95f, 0.92f, 0.85f));
                }
            }
        }

        // ---- 길(그림 속 빛나는 길)을 따라 이동 ----
        // 지도 그림(1672x941) 속 길: 이어진 두 지역과 그 사이 굽은 길을 따라가는 중간 점들(픽셀).
        private static readonly (string a, string b, Vector2[] via)[] Roads =
        {
            ("마을", "광활한 평야", new[] { new Vector2(250f, 610f) }),
            ("광활한 평야", "엔스룸 마을", new[] { new Vector2(440f, 640f), new Vector2(540f, 745f) }),
            ("엔스룸 마을", "어둠의 숲", new[] { new Vector2(587f, 747f), new Vector2(567f, 693f), new Vector2(600f, 613f), new Vector2(693f, 533f), new Vector2(720f, 460f) }),
            ("어둠의 숲", "잊혀진 폐허", new[] { new Vector2(790f, 365f), new Vector2(910f, 320f) }),
            ("엔스룸 마을", "천상의 신전", new[] { new Vector2(800f, 770f), new Vector2(910f, 735f) }),
            ("천상의 신전", "푸른 바다", new[] { new Vector2(1200f, 687f), new Vector2(1333f, 627f) }),
            ("푸른 바다", "절망의 언덕", new[] { new Vector2(1353f, 440f), new Vector2(1373f, 347f), new Vector2(1413f, 307f) }),
        };
        private const float RoadMoveSpeed = 0.75f; // 화면 비율/초

        private Coroutine walkRoutine;

        private int NodeIndex(string name) => System.Array.FindIndex(nodes, n => n.Name == name);

        private static Vector2 MapPoint(Vector2 px) => new Vector2(px.x / MapImageW, 1f - px.y / MapImageH);

        // 지금 지역에서 길로 이어진 지역 중 누른 방향에 가장 가까운 곳으로, 길을 따라 커서가 걸어간다.
        // 방향은 길의 첫 구간 방향으로 판단한다(굽은 길이라도 처음 꺾이는 쪽을 누르면 된다).
        private void MoveSelection(Vector2 direction)
        {
            if (walkRoutine != null) return;
            string here = nodes[currentIndex].Name;
            int bestIndex = -1;
            List<Vector2> bestPath = null;
            float bestScore = 0.35f; // 너무 엇나간 방향은 무시

            foreach (var road in Roads)
            {
                bool forward = road.a == here, backward = road.b == here;
                if (!forward && !backward) continue;
                int target = NodeIndex(forward ? road.b : road.a);
                if (target < 0) continue;

                var path = new List<Vector2> { nodes[currentIndex].Center };
                var via = road.via;
                if (forward) foreach (var v in via) path.Add(MapPoint(v));
                else for (int i = via.Length - 1; i >= 0; i--) path.Add(MapPoint(via[i]));
                path.Add(nodes[target].Center);

                Vector2 first = (path[1] - path[0]).normalized;
                Vector2 whole = (path[path.Count - 1] - path[0]).normalized;
                float score = Mathf.Max(Vector2.Dot(first, direction), Vector2.Dot(whole, direction) * 0.9f);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = target;
                    bestPath = path;
                }
            }

            if (bestIndex < 0) return;
            currentIndex = bestIndex;
            HideAlert();
            walkRoutine = StartCoroutine(WalkRoad(bestPath));
        }

        private IEnumerator WalkRoad(List<Vector2> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                Vector2 from = path[i - 1], to = path[i];
                float length = Vector2.Distance(from, to);
                for (float d = 0f; d < length; d += RoadMoveSpeed * Time.unscaledDeltaTime)
                {
                    SetCursorPoint(Vector2.Lerp(from, to, d / length));
                    SetCursorWalking(true, to.x - from.x);
                    yield return null;
                }
            }
            SetCursorPoint(path[path.Count - 1]);
            SetCursorWalking(false);
            walkRoutine = null;
        }

        private void SetCursorPoint(Vector2 point)
        {
            if (cursor == null) return;
            cursor.anchorMin = cursor.anchorMax = point;
            cursor.anchoredPosition = Vector2.zero;
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

            Vector2 c = node.Center;

            // 잠금 덮개는 미리 만들어 두고, 지도를 열 때마다(Show) 지금 진행도에 맞춰 켜고 끈다.
            if (node.RequiredQuests > 0)
            {
                var go = new GameObject(node.Name + "_Locked", typeof(RectTransform), typeof(Image));
                node.LockOverlay = go;
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                // 그림 지도 위에 네모를 덮으면 튀어서, 지역을 감싸는 부드러운 둥근 그림자로 어둡게 한다.
                Vector2 half = (node.AnchorMax - node.AnchorMin) * 0.85f;
                rect.anchorMin = c - half;
                rect.anchorMax = c + half;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.SetAsFirstSibling(); // 버튼보다 뒤에 그려서 클릭은 버튼이 그대로 받게 한다

                var image = go.GetComponent<Image>();
                image.sprite = Aethoria.Fx.FxUtil.SoftCircleSprite;
                image.color = new Color(0.02f, 0f, 0.05f, 0.85f);
                image.raycastTarget = false;
            }
        }

        // 커서 = SD 네크로시아. 길을 걸을 때는 걷기 8프레임(UI/WorldMap/SDWalk, 오른쪽을 보고 그려짐)을 돌리고,
        // 왼쪽으로 갈 때는 좌우를 뒤집는다. 멈춰 있을 때는 첫 프레임으로 선다. 걷기 그림이 없으면 예전 SD 한 장을 쓴다.
        private const int SdWalkFrameCount = 8;
        private const float SdWalkFps = 11f;
        private readonly Texture2D[] sdWalkFrames = new Texture2D[SdWalkFrameCount];
        private RawImage cursorImage;
        private float sdWalkTime;

        private void BuildCursor(Transform parent)
        {
            var go = new GameObject("Cursor", typeof(RectTransform), typeof(RawImage));
            cursor = (RectTransform)go.transform;
            cursor.SetParent(parent, false);
            cursor.anchorMin = cursor.anchorMax = Vector2.zero;

            cursorImage = go.GetComponent<RawImage>();
            cursorImage.raycastTarget = false;
            for (int i = 0; i < SdWalkFrameCount; i++) sdWalkFrames[i] = Resources.Load<Texture2D>($"UI/WorldMap/SDWalk/sd_walk_{i + 1}");
            if (sdWalkFrames[0] != null)
            {
                // 프레임(266x364)의 맨 아래가 발끝이다 — 발끝이 길 위의 표시점에 오게 한다.
                cursor.sizeDelta = new Vector2(64f, 64f * 364f / 266f);
                cursor.pivot = new Vector2(0.5f, 0.04f);
                cursorImage.texture = sdWalkFrames[0];
            }
            else
            {
                cursor.sizeDelta = new Vector2(72f, 76.6f);
                cursor.pivot = new Vector2(0.5f, 0.15f);
                cursorImage.texture = Resources.Load<Texture2D>("UI/HUD/SD");
            }
        }

        private void SetCursorWalking(bool walking, float moveX = 0f)
        {
            if (sdWalkFrames[0] == null || cursorImage == null) return;
            if (Mathf.Abs(moveX) > 0.0005f) cursor.localScale = new Vector3(moveX < 0f ? -1f : 1f, 1f, 1f);
            if (!walking)
            {
                sdWalkTime = 0f;
                cursorImage.texture = sdWalkFrames[0];
                return;
            }
            sdWalkTime += Time.unscaledDeltaTime;
            var frame = sdWalkFrames[Mathf.FloorToInt(sdWalkTime * SdWalkFps) % SdWalkFrameCount];
            if (frame != null) cursorImage.texture = frame;
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

        private void Describe(string name, string imageKey, string description)
        {
            int index = NodeIndex(name);
            if (index < 0) return;
            nodes[index].ImageKey = imageKey;
            nodes[index].Description = description;
        }

        // ---- 지역 설명 창 ----
        // 지역을 고르면 바로 들어가지 않고 이름·풍경·설명·보물 현황을 보여 준 뒤,
        // Z/Enter = 입장, X/Esc = 지도로 돌아가기. 확대되는 지도 패널이 아니라 화면(transform) 기준으로 띄운다.
        // 틀은 고딕 해골 대화창(RegionInfo/frame, 1774x887 — 위 가운데 제목판), 버튼은 보랏빛 크리스털(RegionInfo/button, 3:1).
        // 풍경은 각 지역 배경을 16:9로 미리 합쳐 둔 그림(RegionInfo/region_키, 960x540)이다.
        private const string RegionInfoFolder = "UI/WorldMap/RegionInfo/";
        private static readonly Vector2 InfoFrameSize = new Vector2(820f, 410f);  // 원본 2:1
        private const float InfoFrameRaise = 34f;                                 // 아래 버튼 자리만큼 틀을 위로
        private static readonly Vector2 InfoButtonSize = new Vector2(228f, 76f);  // 원본 3:1
        // 틀 안 배치(틀 기준 0~1, 왼쪽 아래 원점). 제목판은 그림의 x 540~1230, y 85~185px.
        private static readonly Vector2 InfoTitleMin = new Vector2(0.31f, 0.79f), InfoTitleMax = new Vector2(0.69f, 0.905f);
        private static readonly Vector2 InfoImageMin = new Vector2(0.075f, 0.2f), InfoImageMax = new Vector2(0.475f, 0.7f);

        private GameObject infoPanel;
        private Text infoTitle;
        private Text infoBody;
        private Text infoChests;
        private RawImage infoImage;
        private MapNode infoNode;

        private static Text MakeDialogText(Transform parent, string name, Vector2 min, Vector2 max, int size, FontStyle style, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.15f;
            text.raycastTarget = false;
            return text;
        }

        private void BuildInfoDialog()
        {
            infoPanel = new GameObject("RegionInfoPanel", typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)infoPanel.transform;
            dimRect.SetParent(transform, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImage = infoPanel.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.55f);
            dimImage.raycastTarget = true; // 뒤쪽 지도 클릭을 막는다

            // 틀: 해골 대화창 그림. 그림이 없으면 예전처럼 어두운 네모 상자로 그린다.
            var frameTexture = Resources.Load<Texture2D>(RegionInfoFolder + "frame");
            var boxGO = new GameObject("Frame", typeof(RectTransform));
            var boxRect = (RectTransform)boxGO.transform;
            boxRect.SetParent(infoPanel.transform, false);
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = InfoFrameSize;
            boxRect.anchoredPosition = new Vector2(0f, InfoFrameRaise);

            if (frameTexture != null)
            {
                var frameGO = new GameObject("FrameArt", typeof(RectTransform), typeof(RawImage));
                var frameRect = (RectTransform)frameGO.transform;
                frameRect.SetParent(boxRect, false);
                frameRect.anchorMin = Vector2.zero;
                frameRect.anchorMax = Vector2.one;
                frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
                var frameImage = frameGO.GetComponent<RawImage>();
                frameImage.texture = frameTexture;
                frameImage.raycastTarget = false;
            }
            else
            {
                boxGO.AddComponent<Image>().color = new Color(0.06f, 0.04f, 0.09f, 0.97f);
            }

            // 풍경 그림: 틀 안쪽 판(불투명) 위 왼쪽에 얹는다.
            var imageGO = new GameObject("RegionImage", typeof(RectTransform), typeof(RawImage), typeof(Outline));
            var imageRect = (RectTransform)imageGO.transform;
            imageRect.SetParent(boxRect, false);
            imageRect.anchorMin = InfoImageMin;
            imageRect.anchorMax = InfoImageMax;
            imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
            infoImage = imageGO.GetComponent<RawImage>();
            infoImage.raycastTarget = false;
            // 자리 비율(약 1.6:1)에 맞게 16:9 그림의 좌우를 조금 잘라 늘어나 보이지 않게 한다.
            Vector2 slot = Vector2.Scale(InfoImageMax - InfoImageMin, InfoFrameSize);
            float uvWidth = Mathf.Min(1f, (slot.x / slot.y) / (16f / 9f));
            infoImage.uvRect = new Rect((1f - uvWidth) * 0.5f, 0f, uvWidth, 1f);
            var imageOutline = imageGO.GetComponent<Outline>();
            imageOutline.effectColor = new Color(0.62f, 0.5f, 0.78f, 0.9f);
            imageOutline.effectDistance = new Vector2(2f, -2f);

            infoTitle = MakeDialogText(boxRect, "Title", InfoTitleMin, InfoTitleMax,
                30, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.92f, 0.84f, 1f));
            FitAndOutline(infoTitle, 18);

            infoBody = MakeDialogText(boxRect, "Body", new Vector2(0.505f, 0.36f), new Vector2(0.925f, 0.71f),
                21, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.95f, 0.93f, 0.9f));
            FitAndOutline(infoBody, 14);
            infoChests = MakeDialogText(boxRect, "Chests", new Vector2(0.505f, 0.22f), new Vector2(0.925f, 0.34f),
                17, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.4f));
            FitAndOutline(infoChests, 12);

            // 버튼은 틀 아래(틀 밑변 가운데 장식을 피해 좌우로).
            float buttonY = InfoFrameRaise - InfoFrameSize.y * 0.5f - InfoButtonSize.y * 0.5f + 4f;
            BuildCrystalButton(infoPanel.transform, "EnterButton", "입장 (Z)", new Vector2(-InfoButtonSize.x * 0.62f, buttonY), ConfirmEnter);
            BuildCrystalButton(infoPanel.transform, "BackButton", "돌아가기 (X)", new Vector2(InfoButtonSize.x * 0.62f, buttonY), CloseInfo);

            infoPanel.SetActive(false);
        }

        private static void FitAndOutline(Text text, int minSize)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = text.fontSize;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.01f, 0.1f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // 보랏빛 크리스털 버튼. 마우스를 올리면 밝아지고 누르면 살짝 어두워진다. 그림이 없으면 반투명 네모로 그린다.
        private static void BuildCrystalButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var texture = Resources.Load<Texture2D>(RegionInfoFolder + "button");
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = InfoButtonSize;
            rect.anchoredPosition = position;

            var image = go.GetComponent<RawImage>();
            if (texture != null) image.texture = texture;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = texture != null ? new Color(0.88f, 0.88f, 0.88f) : new Color(1f, 1f, 1f, 0.12f);
            colors.highlightedColor = texture != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.7f, 0.65f, 0.75f, texture != null ? 1f : 0.3f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None }; // 키보드는 Z/X로만
            button.onClick.AddListener(onClick);

            var text = MakeDialogText(rect, "Text", Vector2.zero, Vector2.one, 22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.02f, 0.22f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            text.text = label;
        }

        private void ShowInfo(MapNode node)
        {
            infoNode = node;
            infoTitle.text = node.Name;
            infoBody.text = node.Description ?? string.Empty;
            if (node.ChestRegion != null)
            {
                int found = TreasureChests.OpenedCount(node.ChestRegion), total = TreasureChests.Total(node.ChestRegion);
                infoChests.text = found >= total ? $"보물상자 {found}/{total} (모두 찾음!)" : $"보물상자 {found}/{total}";
            }
            else infoChests.text = string.Empty;
            var picture = node.ImageKey != null ? Resources.Load<Texture2D>(RegionInfoFolder + "region_" + node.ImageKey) : null;
            infoImage.texture = picture;
            infoImage.gameObject.SetActive(picture != null);
            HideAlert();
            infoPanel.SetActive(true);
        }

        private void ConfirmEnter()
        {
            var node = infoNode;
            CloseInfo();
            node?.OnSelect?.Invoke();
        }

        private void CloseInfo()
        {
            infoNode = null;
            if (infoPanel != null) infoPanel.SetActive(false);
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
            CloseInfo();
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
                if (node.ChestLabel != null)
                {
                    int found = TreasureChests.OpenedCount(node.ChestRegion), total = TreasureChests.Total(node.ChestRegion);
                    node.ChestLabel.gameObject.SetActive(!locked && !pending);
                    node.ChestLabel.text = $"보물 {found}/{total}";
                    node.ChestLabel.color = found >= total ? new Color(1f, 0.82f, 0.3f) : new Color(0.95f, 0.92f, 0.85f);
                }
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
            CloseInfo();
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

            // "마을로 이동하시겠습니까?" 팝업(ChoicePopup)이 떠 있거나 방금 닫혔으면 지도 조작을 받지 않는다.
            if (ChoicePopup.ConsumesInput) return;

            if (infoNode != null)
            {
                if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    ConfirmEnter();
                }
                else if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                {
                    CloseInfo();
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

        private void MoveCursorTo(MapNode node)
        {
            if (walkRoutine != null) { StopCoroutine(walkRoutine); walkRoutine = null; }
            SetCursorWalking(false);
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

            if (infoNode != null || ChoicePopup.IsOpen) return; // 창이 떠 있을 때 뒤쪽 클릭 무시
            ShowInfo(node);
        }

        // 마을 칸을 고르거나 Esc를 누르면 뜨는 확인 창(공통 선택 팝업). 실수로 지도를 닫는 걸 막는다.
        private void ShowExitConfirm()
        {
            ChoicePopup.Ask("마을로 이동하시겠습니까?", HandleCancel);
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

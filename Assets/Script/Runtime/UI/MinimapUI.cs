using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // 던전(방 구조 지역)에서 오른쪽 아래에 작은 지도를 띄우고, M을 누르면 화면 가운데에 크게 펼친다.
    // 방 배치·방문 여부·방 안 표시(열쇠·상자·정예·퍼즐·전멸방)는 GameBootstrap이 넘겨주고, 이 화면은 그리기와 플레이어 점 위치만 맡는다.
    // 큰 지도에는 방 이름과 아이콘 설명(범례)이 같이 나온다.
    // 지역에 들어서거나 처음 가 보는 방에 들어가면 화면 위쪽 가운데에 이름을 잠깐 띄운다(ShowAreaTitle).
    // 아이콘 그림은 UI/Map/icon_key 등(없으면 코드로 그린 픽셀 아이콘), 지도 판은 UI/Map/MinimapFrame(없으면 둥근 판).
    public class MinimapUI : MonoBehaviour
    {
        public enum IconKind { Key, Chest, Elite, Puzzle, Arena }

        public struct Icon
        {
            public IconKind Kind;
            public bool Done;       // 이미 주웠다/열었다/잡았다/풀었다 → 흐리게
        }

        // 가 본 방에서 아직 안 가 본 쪽으로 난 길(방 가장자리의 한 점과 바깥 방향, 지도 좌표).
        public struct ExitStub
        {
            public Vector2 Point;
            public Vector2 Direction;
        }

        public struct Room
        {
            public string Id;
            public string Name;
            public Rect Rect;       // 지도 좌표(단위 자유, y는 위로 갈수록 큼)
            public bool Current;
            public bool Visited;    // false면 "옆방으로 알려진" 방 — 어둡게 테두리만
            public bool IsBoss;
            public List<Icon> Icons;
            public List<ExitStub> Exits;
        }

        // 판 그림(UI/Map/MinimapFrame, 약 1.9:1)과 같은 비율. 위쪽 가운데에 초승달 장식, 네 모서리에 보석이 있어서
        // 글자와 지도는 테두리선(위 약 14%, 옆 약 6%) 안쪽에 둔다.
        private const float SmallWidth = 240f, SmallHeight = 126f;
        private const float BigWidth = 900f, BigHeight = 474f;

        private sealed class View
        {
            public bool Big;
            public GameObject Panel;
            public RectTransform Content;
            public Text Title;
            public Text Status;
            public RectTransform Dot;
            public Rect CurrentRoomRect;    // Content 안 픽셀 좌표
            public readonly List<GameObject> RoomObjects = new();
        }

        private View small;
        private View big;
        private Func<Vector2> playerInRoom;     // 지금 방 안에서 플레이어의 위치(0~1)
        private bool active;
        private bool bigOpen;

        // 진행 상태(깃발·연 상자)가 바뀌면 다시 그리게 한다(정예를 잡거나 열쇠를 주운 순간 아이콘이 흐려지게).
        public Func<int> StateVersion;
        public Action RequestRefresh;
        private int shownVersion = int.MinValue;

        // 지역·방 이름 띄우기
        private CanvasGroup areaGroup;
        private Text areaMain;
        private Text areaSub;
        private RectTransform areaLine;
        private float areaStart = -10f;
        private float areaHold;

        public void Initialize()
        {
            small = BuildView("Minimap", false, new Vector2(1f, 0f), new Vector2(-16f, 50f), new Vector2(SmallWidth, SmallHeight));
            big = BuildView("MinimapBig", true, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BigWidth, BigHeight));
            BuildLegend(big);
            BuildAreaTitle();
            Hide();
        }

        public void Show(string title, string status, List<Room> rooms, Func<Vector2> playerPosition)
        {
            active = true;
            playerInRoom = playerPosition;
            Render(small, title, status, rooms);
            Render(big, title, status, rooms);
            small.Panel.SetActive(!bigOpen);
            big.Panel.SetActive(bigOpen);
        }

        public void Hide()
        {
            active = false;
            bigOpen = false;
            playerInRoom = null;
            small.Panel.SetActive(false);
            big.Panel.SetActive(false);
        }

        // 화면 위쪽 가운데에 큰 이름(+작은 부제)을 떠올렸다가 사라지게 한다.
        public void ShowAreaTitle(string main, string sub, bool large)
        {
            areaMain.text = main;
            areaMain.fontSize = large ? 40 : 26;
            areaSub.text = sub ?? string.Empty;
            areaSub.gameObject.SetActive(!string.IsNullOrEmpty(sub));
            areaHold = large ? 1.8f : 1.1f;
            areaStart = Time.unscaledTime;
            areaGroup.gameObject.SetActive(true);
            areaGroup.transform.SetAsLastSibling();
        }

        private void Update()
        {
            UpdateAreaTitle();
            if (!active) return;
            if (StateVersion != null && RequestRefresh != null)
            {
                int v = StateVersion();
                if (v != shownVersion)
                {
                    shownVersion = v;
                    RequestRefresh();
                }
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame && Time.timeScale > 0f)
            {
                bigOpen = !bigOpen;
                small.Panel.SetActive(!bigOpen);
                big.Panel.SetActive(bigOpen);
                if (bigOpen) { big.Panel.transform.SetAsLastSibling(); UiSfx.Open(); }
                else UiSfx.Close();
            }

            var view = bigOpen ? big : small;
            if (view.Dot == null || playerInRoom == null) return;
            Vector2 p = playerInRoom();
            p.x = Mathf.Clamp01(p.x);
            p.y = Mathf.Clamp01(p.y);
            var r = view.CurrentRoomRect;
            view.Dot.anchoredPosition = new Vector2(r.xMin + r.width * p.x, r.yMin + r.height * p.y);
            // 깜빡여서 눈에 띄게.
            var img = view.Dot.GetComponent<Image>();
            var c = img.color;
            c.a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
            img.color = c;
        }

        private void UpdateAreaTitle()
        {
            if (!areaGroup.gameObject.activeSelf) return;
            float t = Time.unscaledTime - areaStart;
            const float fadeIn = 0.45f, fadeOut = 0.7f;
            float total = fadeIn + areaHold + fadeOut;
            if (t >= total)
            {
                areaGroup.gameObject.SetActive(false);
                return;
            }
            float a = t < fadeIn ? t / fadeIn : t > fadeIn + areaHold ? 1f - (t - fadeIn - areaHold) / fadeOut : 1f;
            areaGroup.alpha = a * a * (3f - 2f * a);
            // 장식 줄이 가운데에서 양옆으로 펼쳐진다.
            float open = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.6f), 3f);
            areaLine.localScale = new Vector3(open, 1f, 1f);
            // 글자는 살짝 아래에서 떠오른다.
            ((RectTransform)areaMain.transform).anchoredPosition = new Vector2(0f, 14f - 8f * (1f - Mathf.Clamp01(t / fadeIn)));
        }

        // ---------------------------------------------------------------- 그리기

        private void Render(View view, string title, string status, List<Room> rooms)
        {
            foreach (var go in view.RoomObjects) Destroy(go);
            view.RoomObjects.Clear();
            view.Title.text = title;
            view.Status.text = status ?? string.Empty;
            if (rooms.Count == 0) return;

            float gap = view.Big ? 4f : 2f;
            Rect bounds = rooms[0].Rect;
            foreach (var room in rooms)
                bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, room.Rect.xMin), Mathf.Min(bounds.yMin, room.Rect.yMin),
                    Mathf.Max(bounds.xMax, room.Rect.xMax), Mathf.Max(bounds.yMax, room.Rect.yMax));

            Vector2 area = view.Content.rect.size;
            float scale = Mathf.Min(area.x / Mathf.Max(bounds.width, 0.01f), area.y / Mathf.Max(bounds.height, 0.01f));
            // 방이 몇 개뿐일 때 지나치게 커지지 않게.
            scale = Mathf.Min(scale, area.y / 3f);
            Vector2 offset = (area - bounds.size * scale) * 0.5f;

            foreach (var room in rooms)
            {
                var px = new Rect(offset.x + (room.Rect.xMin - bounds.xMin) * scale, offset.y + (room.Rect.yMin - bounds.yMin) * scale,
                    room.Rect.width * scale, room.Rect.height * scale);
                px = Rect.MinMaxRect(px.xMin + gap * 0.5f, px.yMin + gap * 0.5f, px.xMax - gap * 0.5f, px.yMax - gap * 0.5f);
                if (room.Current) view.CurrentRoomRect = px;

                Color fill, edge;
                if (room.Current)
                {
                    fill = room.IsBoss ? new Color(0.85f, 0.25f, 0.3f, 0.95f) : new Color(0.62f, 0.48f, 0.9f, 0.95f);
                    edge = new Color(1f, 0.9f, 0.55f, 1f);
                }
                else if (room.Visited)
                {
                    fill = room.IsBoss ? new Color(0.55f, 0.14f, 0.18f, 0.9f) : new Color(0.32f, 0.27f, 0.45f, 0.9f);
                    edge = new Color(0.75f, 0.7f, 0.9f, 0.9f);
                }
                else
                {
                    fill = room.IsBoss ? new Color(0.3f, 0.06f, 0.08f, 0.75f) : new Color(0.1f, 0.08f, 0.14f, 0.7f);
                    edge = room.IsBoss ? new Color(0.85f, 0.3f, 0.3f, 0.8f) : new Color(0.45f, 0.42f, 0.55f, 0.6f);
                }

                var roomGO = new GameObject("Room_" + room.Id, typeof(RectTransform), typeof(Image), typeof(Outline));
                var rect = (RectTransform)roomGO.transform;
                rect.SetParent(view.Content, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = px.position;
                rect.sizeDelta = px.size;
                var image = roomGO.GetComponent<Image>();
                image.color = fill;
                image.raycastTarget = false;
                var outline = roomGO.GetComponent<Outline>();
                outline.effectColor = edge;
                outline.effectDistance = new Vector2(1f, -1f) * (view.Big ? 2f : 1f);
                view.RoomObjects.Add(roomGO);

                // 큰 지도: 방 이름(가 본 방과 보스방만).
                if (view.Big && (room.Visited || room.IsBoss) && !string.IsNullOrEmpty(room.Name))
                {
                    var name = MakeText(rect, "Name", room.IsBoss ? 15 : 13,
                        room.Current ? new Color(1f, 0.95f, 0.75f) : new Color(0.88f, 0.85f, 0.95f, room.Visited ? 1f : 0.7f), TextAnchor.LowerCenter);
                    Stretch((RectTransform)name.transform, 3f);
                    name.text = room.Name;
                }
                else if (room.IsBoss && px.height > 16f)
                {
                    var mark = MakeText(rect, "BossMark", Mathf.RoundToInt(Mathf.Min(px.height * 0.4f, 20f)),
                        new Color(1f, 0.85f, 0.8f, room.Visited || room.Current ? 1f : 0.7f), TextAnchor.MiddleCenter);
                    Stretch((RectTransform)mark.transform, 0f);
                    mark.text = "보스";
                }

                if (room.Visited && room.Icons != null && room.Icons.Count > 0) DrawIcons(rect, px, room.Icons, view.Big);

                // 안 가 본 쪽으로 난 길: 방 가장자리에서 바깥으로 짧게 삐져나온 금빛 표시(끝이 흐려짐).
                if (room.Exits != null)
                {
                    foreach (var exit in room.Exits)
                    {
                        Vector2 p = offset + (exit.Point - bounds.min) * scale;
                        bool horizontal = Mathf.Abs(exit.Direction.x) > 0.5f;
                        float len = view.Big ? 16f : 7f, thick = view.Big ? 10f : 5f;
                        var stub = new GameObject("UnexploredExit", typeof(RectTransform), typeof(Image));
                        var stubRect = (RectTransform)stub.transform;
                        stubRect.SetParent(view.Content, false);
                        stubRect.anchorMin = stubRect.anchorMax = Vector2.zero;
                        stubRect.pivot = new Vector2(horizontal ? (exit.Direction.x > 0f ? 0f : 1f) : 0.5f,
                                                     horizontal ? 0.5f : (exit.Direction.y > 0f ? 0f : 1f));
                        stubRect.anchoredPosition = p;
                        stubRect.sizeDelta = horizontal ? new Vector2(len, thick) : new Vector2(thick, len);
                        var stubImage = stub.GetComponent<Image>();
                        stubImage.sprite = Aethoria.Fx.FxUtil.SoftCircleSprite;
                        stubImage.color = new Color(1f, 0.82f, 0.45f, 0.85f);
                        stubImage.raycastTarget = false;
                        view.RoomObjects.Add(stub);
                    }
                }
            }

            // 점은 방들 위에 그린다.
            view.Dot.SetAsLastSibling();
        }

        // 방 오른쪽 위에 아이콘을 한 줄로 늘어놓는다.
        private static void DrawIcons(RectTransform roomRect, Rect px, List<Icon> icons, bool big)
        {
            float size = big ? 18f : Mathf.Clamp(px.height * 0.42f, 8f, 11f);
            float step = size + (big ? 3f : 1f);
            for (int i = 0; i < icons.Count; i++)
            {
                var go = new GameObject("Icon_" + icons[i].Kind, typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(roomRect, false);
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-2f - i * step, -2f);
                rect.sizeDelta = new Vector2(size, size);
                var image = go.GetComponent<Image>();
                image.sprite = IconSprite(icons[i].Kind);
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = icons[i].Done ? new Color(0.6f, 0.6f, 0.65f, 0.45f) : Color.white;
            }
        }

        // ---------------------------------------------------------------- 아이콘

        private static readonly string[] KeyPixels =
        {
            "  ###  ",
            " #+  # ",
            " #   # ",
            "  ###  ",
            "   #   ",
            "   ##  ",
            "   #   ",
            "   ##  ",
        };
        private static readonly string[] ChestPixels =
        {
            " ##### ",
            "#ooooo#",
            "#######",
            "#oo+oo#",
            "#ooooo#",
            "#######",
        };
        private static readonly string[] ElitePixels =
        {
            " ##### ",
            "#######",
            "#oo#oo#",
            "#oo#oo#",
            "#######",
            " ## ## ",
            " # # # ",
        };
        private static readonly string[] PuzzlePixels =
        {
            "   #   ",
            "  #+#  ",
            " #+o+# ",
            "#+ooo+#",
            " #+o+# ",
            "  #+#  ",
            "   #   ",
        };
        private static readonly string[] ArenaPixels =
        {
            "+     +",
            " #   # ",
            "  # #  ",
            "   #   ",
            "  # #  ",
            "oo   oo",
            "oo   oo",
        };

        public static Sprite IconSprite(IconKind kind) => kind switch
        {
            IconKind.Key => UiArt.LoadOr("UI/Map/icon_key", () => UiArt.PixelIcon("map_key", KeyPixels, new Color(1f, 0.82f, 0.3f))),
            IconKind.Chest => UiArt.LoadOr("UI/Map/icon_chest", () => UiArt.PixelIcon("map_chest", ChestPixels, new Color(0.9f, 0.62f, 0.25f))),
            IconKind.Elite => UiArt.LoadOr("UI/Map/icon_elite", () => UiArt.PixelIcon("map_elite", ElitePixels, new Color(0.95f, 0.32f, 0.32f))),
            IconKind.Puzzle => UiArt.LoadOr("UI/Map/icon_puzzle", () => UiArt.PixelIcon("map_puzzle", PuzzlePixels, new Color(0.45f, 0.85f, 1f))),
            _ => UiArt.LoadOr("UI/Map/icon_arena", () => UiArt.PixelIcon("map_arena", ArenaPixels, new Color(1f, 0.58f, 0.22f))),
        };

        private static readonly (IconKind kind, string label)[] LegendEntries =
        {
            (IconKind.Key, "열쇠"), (IconKind.Chest, "보물상자"), (IconKind.Elite, "정예 몬스터"), (IconKind.Puzzle, "퍼즐"), (IconKind.Arena, "전멸방"),
        };

        // ---------------------------------------------------------------- 만들기

        private View BuildView(string name, bool isBig, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var view = new View { Big = isBig };
            view.Panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)view.Panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var bg = view.Panel.GetComponent<Image>();
            bg.sprite = UiArt.LoadOr("UI/Map/MinimapFrame",
                () => UiArt.RoundedPanel(new Color(0.03f, 0.02f, 0.06f, 0.78f), new Color(0.55f, 0.45f, 0.75f, 0.9f), 10, 2));
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;

            int titleSize = isBig ? 24 : 13;
            float titleHeight = titleSize + 10f;
            view.Title = MakeText(rect, "Title", titleSize, new Color(1f, 0.88f, 0.55f), isBig ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
            var titleRect = (RectTransform)view.Title.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, isBig ? -70f : -17f);
            titleRect.sizeDelta = new Vector2(isBig ? 0f : -40f, titleHeight);

            // 열쇠·보물 개수: 작은 지도는 제목 줄 오른쪽, 큰 지도는 제목 아래.
            view.Status = MakeText(rect, "Status", isBig ? 15 : 11, new Color(0.85f, 0.8f, 0.95f), isBig ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight);
            var statusRect = (RectTransform)view.Status.transform;
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.anchoredPosition = isBig ? new Vector2(0f, -70f - titleHeight) : new Vector2(0f, -17f);
            statusRect.sizeDelta = new Vector2(isBig ? 0f : -40f, isBig ? 20f : titleHeight);

            float padX = isBig ? 60f : 17f;
            float top = isBig ? 70f + titleHeight + 24f : 17f + titleHeight;
            float bottom = isBig ? 62f : 12f;
            var contentGO = new GameObject("Content", typeof(RectTransform));
            view.Content = (RectTransform)contentGO.transform;
            view.Content.SetParent(rect, false);
            view.Content.anchorMin = Vector2.zero;
            view.Content.anchorMax = Vector2.one;
            view.Content.offsetMin = new Vector2(padX, bottom);
            view.Content.offsetMax = new Vector2(-padX, -top);

            float dotSize = isBig ? 12f : 6f;
            var dotGO = new GameObject("PlayerDot", typeof(RectTransform), typeof(Image), typeof(Outline));
            view.Dot = (RectTransform)dotGO.transform;
            view.Dot.SetParent(view.Content, false);
            view.Dot.anchorMin = view.Dot.anchorMax = Vector2.zero;
            view.Dot.pivot = new Vector2(0.5f, 0.5f);
            view.Dot.sizeDelta = new Vector2(dotSize, dotSize);
            var dotImage = dotGO.GetComponent<Image>();
            dotImage.sprite = Aethoria.Fx.FxUtil.SoftCircleSprite;
            dotImage.color = new Color(1f, 0.95f, 0.6f);
            dotImage.raycastTarget = false;
            dotGO.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return view;
        }

        // 큰 지도 아래: 아이콘 설명 + 닫기 안내.
        private void BuildLegend(View view)
        {
            var row = (RectTransform)new GameObject("Legend", typeof(RectTransform)).transform;
            row.SetParent(view.Panel.transform, false);
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, 32f);
            row.sizeDelta = new Vector2(-120f, 22f);

            float x = 0f;
            foreach (var (kind, label) in LegendEntries)
            {
                var iconGO = new GameObject("LegendIcon", typeof(RectTransform), typeof(Image));
                var iconRect = (RectTransform)iconGO.transform;
                iconRect.SetParent(row, false);
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(x, 0f);
                iconRect.sizeDelta = new Vector2(18f, 18f);
                var img = iconGO.GetComponent<Image>();
                img.sprite = IconSprite(kind);
                img.preserveAspect = true;
                img.raycastTarget = false;

                var text = MakeText(row, "LegendLabel", 13, new Color(0.85f, 0.82f, 0.92f), TextAnchor.MiddleLeft);
                var textRect = (RectTransform)text.transform;
                textRect.anchorMin = textRect.anchorMax = new Vector2(0f, 0.5f);
                textRect.pivot = new Vector2(0f, 0.5f);
                textRect.anchoredPosition = new Vector2(x + 22f, 0f);
                textRect.sizeDelta = new Vector2(90f, 20f);
                text.text = label;
                x += 22f + label.Length * 13f + 18f;
            }

            var hint = MakeText(row, "Hint", 14, new Color(0.8f, 0.78f, 0.9f, 0.8f), TextAnchor.MiddleRight);
            Stretch((RectTransform)hint.transform, 0f);
            hint.text = "[M] 닫기";
        }

        private void BuildAreaTitle()
        {
            var root = new GameObject("AreaTitle", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)root.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.74f);
            rect.sizeDelta = new Vector2(700f, 110f);
            areaGroup = root.GetComponent<CanvasGroup>();
            areaGroup.blocksRaycasts = false;
            areaGroup.interactable = false;

            // 장식 줄(그림이 있으면 UI/Map/AreaTitleOrnament, 없으면 가운데가 밝은 가는 빛줄기).
            var lineGO = new GameObject("Line", typeof(RectTransform), typeof(Image));
            areaLine = (RectTransform)lineGO.transform;
            areaLine.SetParent(rect, false);
            areaLine.anchorMin = areaLine.anchorMax = new Vector2(0.5f, 0.5f);
            areaLine.anchoredPosition = new Vector2(0f, -16f);
            var ornament = Resources.Load<Sprite>("UI/Map/AreaTitleOrnament");
            var lineImage = lineGO.GetComponent<Image>();
            lineImage.raycastTarget = false;
            if (ornament != null)
            {
                lineImage.sprite = ornament;
                areaLine.sizeDelta = new Vector2(520f, 520f * ornament.rect.height / ornament.rect.width);
            }
            else
            {
                lineImage.sprite = Aethoria.Fx.FxUtil.SoftCircleSprite;
                lineImage.color = new Color(0.85f, 0.7f, 1f, 0.9f);
                areaLine.sizeDelta = new Vector2(460f, 5f);
            }

            areaMain = MakeText(rect, "Main", 40, new Color(1f, 0.93f, 0.78f), TextAnchor.MiddleCenter);
            var mainRect = (RectTransform)areaMain.transform;
            mainRect.anchorMin = new Vector2(0f, 0.5f);
            mainRect.anchorMax = new Vector2(1f, 0.5f);
            mainRect.pivot = new Vector2(0.5f, 0f);
            mainRect.sizeDelta = new Vector2(0f, 50f);
            var shadow = areaMain.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.35f, 0.15f, 0.55f, 0.8f);
            shadow.effectDistance = new Vector2(0f, -3f);

            areaSub = MakeText(rect, "Sub", 18, new Color(0.85f, 0.8f, 0.95f), TextAnchor.MiddleCenter);
            var subRect = (RectTransform)areaSub.transform;
            subRect.anchorMin = new Vector2(0f, 0.5f);
            subRect.anchorMax = new Vector2(1f, 0.5f);
            subRect.pivot = new Vector2(0.5f, 1f);
            subRect.anchoredPosition = new Vector2(0f, ornament != null ? -50f : -18f);
            subRect.sizeDelta = new Vector2(0f, 26f);

            root.SetActive(false);
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Text MakeText(Transform parent, string name, int fontSize, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }
    }
}

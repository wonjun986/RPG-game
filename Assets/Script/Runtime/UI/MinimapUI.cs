using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // 던전(방 구조 지역)에서 오른쪽 아래에 작은 지도를 띄우고, M을 누르면 화면 가운데에 크게 펼친다.
    // 방 배치·방문 여부는 GameBootstrap이 넘겨주고, 이 화면은 그리기와 플레이어 점 위치만 맡는다.
    public class MinimapUI : MonoBehaviour
    {
        public struct Room
        {
            public string Id;
            public Rect Rect;       // 지도 좌표(단위 자유, y는 위로 갈수록 큼)
            public bool Current;
            public bool Visited;    // false면 "옆방으로 알려진" 방 — 어둡게 테두리만
            public bool IsBoss;
        }

        private const float SmallWidth = 220f, SmallHeight = 124f;
        private const float BigWidth = 820f, BigHeight = 470f;

        private sealed class View
        {
            public GameObject Panel;
            public RectTransform Content;
            public Text Title;
            public RectTransform Dot;
            public Rect CurrentRoomRect;    // Content 안 픽셀 좌표
            public readonly List<GameObject> RoomObjects = new();
        }

        private View small;
        private View big;
        private Func<Vector2> playerInRoom;     // 지금 방 안에서 플레이어의 위치(0~1)
        private bool active;
        private bool bigOpen;

        public void Initialize()
        {
            small = BuildView("Minimap", new Vector2(1f, 0f), new Vector2(-16f, 50f), new Vector2(SmallWidth, SmallHeight), 13, 6f);
            big = BuildView("MinimapBig", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BigWidth, BigHeight), 22, 12f);
            var hint = MakeText(big.Panel.transform, "Hint", 14, new Color(0.8f, 0.78f, 0.9f, 0.8f), TextAnchor.LowerRight);
            var hintRect = (RectTransform)hint.transform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(-12f, 6f);
            hintRect.sizeDelta = new Vector2(0f, 20f);
            hint.text = "M  닫기";
            Hide();
        }

        public void Show(string title, List<Room> rooms, Func<Vector2> playerPosition)
        {
            active = true;
            playerInRoom = playerPosition;
            Render(small, title, rooms, 2f);
            Render(big, title, rooms, 4f);
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

        private void Update()
        {
            if (!active) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            {
                bigOpen = !bigOpen;
                small.Panel.SetActive(!bigOpen);
                big.Panel.SetActive(bigOpen);
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

        private void Render(View view, string title, List<Room> rooms, float gap)
        {
            foreach (var go in view.RoomObjects) Destroy(go);
            view.RoomObjects.Clear();
            view.Title.text = title;
            if (rooms.Count == 0) return;

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
                outline.effectDistance = new Vector2(1f, -1f) * (gap > 3f ? 2f : 1f);
                view.RoomObjects.Add(roomGO);

                if (room.IsBoss && px.height > 16f)
                {
                    var mark = MakeText(rect, "BossMark", Mathf.RoundToInt(Mathf.Min(px.height * 0.4f, 20f)),
                        new Color(1f, 0.85f, 0.8f, room.Visited || room.Current ? 1f : 0.7f), TextAnchor.MiddleCenter);
                    var markRect = (RectTransform)mark.transform;
                    markRect.anchorMin = Vector2.zero;
                    markRect.anchorMax = Vector2.one;
                    markRect.offsetMin = markRect.offsetMax = Vector2.zero;
                    mark.text = "보스";
                }
            }

            // 점은 방들 위에 그린다.
            view.Dot.SetAsLastSibling();
        }

        private View BuildView(string name, Vector2 anchor, Vector2 position, Vector2 size, int titleSize, float dotSize)
        {
            var view = new View();
            view.Panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            var rect = (RectTransform)view.Panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var bg = view.Panel.GetComponent<Image>();
            bg.color = new Color(0.03f, 0.02f, 0.06f, 0.72f);
            bg.raycastTarget = false;
            var outline = view.Panel.GetComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.45f, 0.75f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            float titleHeight = titleSize + 8f;
            view.Title = MakeText(rect, "Title", titleSize, new Color(1f, 0.88f, 0.55f), TextAnchor.MiddleCenter);
            var titleRect = (RectTransform)view.Title.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -2f);
            titleRect.sizeDelta = new Vector2(0f, titleHeight);

            float pad = size.x > 400f ? 28f : 8f;
            var contentGO = new GameObject("Content", typeof(RectTransform));
            view.Content = (RectTransform)contentGO.transform;
            view.Content.SetParent(rect, false);
            view.Content.anchorMin = Vector2.zero;
            view.Content.anchorMax = Vector2.one;
            view.Content.offsetMin = new Vector2(pad, pad);
            view.Content.offsetMax = new Vector2(-pad, -titleHeight - 2f);

            var dotGO = new GameObject("PlayerDot", typeof(RectTransform), typeof(Image), typeof(Outline));
            view.Dot = (RectTransform)dotGO.transform;
            view.Dot.SetParent(view.Content, false);
            view.Dot.anchorMin = view.Dot.anchorMax = Vector2.zero;
            view.Dot.pivot = new Vector2(0.5f, 0.5f);
            view.Dot.sizeDelta = new Vector2(dotSize, dotSize);
            var dotImage = dotGO.GetComponent<Image>();
            dotImage.color = new Color(1f, 0.95f, 0.6f);
            dotImage.raycastTarget = false;
            dotGO.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return view;
        }

        private static Text MakeText(Transform parent, string name, int fontSize, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }
    }
}

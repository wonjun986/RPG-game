using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aethoria.Stages
{
    // 시차 배경: 여러 층의 그림을 겹쳐 깔고, 카메라가 움직일 때 층마다 다른 속도로 따라가게 한다.
    // 먼 층일수록 조금 움직여(하늘은 거의 고정) 깊이감이 생기고, 이어 붙일 수 있는 층은 화면 폭만큼 자동으로 이어 붙인다.
    // 카메라(Cinemachine)가 위치를 다 정한 뒤 그리기 직전(beginCameraRendering)에 층을 옮겨서 떨리지 않는다.
    public class ParallaxBackground : MonoBehaviour
    {
        public sealed class Layer
        {
            public string Path;
            // 세상 기준으로 얼마나 움직이는지. 1 = 땅처럼 세상에 붙어 있음, 0 = 화면에 붙어 있음(카메라와 같이 움직임),
            // 0.3 = 카메라가 10칸 갈 때 이 층은 화면에서 3칸만 흘러감. 1보다 크면 캐릭터보다 빨리 지나가는 앞 장식.
            public float ScrollX = 1f;
            public float ScrollY = 1f;
            // 카메라가 기본 높이(땅에 서 있을 때)에 있을 때 그림 밑변의 높이(바닥 기준).
            public float BottomAboveFloor;
            public float Scale = 1f;
            public int SortingOrder = -12;
            public bool Tile = true;     // 화면 폭만큼 이어 붙인다
            public bool Mirror;          // 이어 붙일 때 하나 건너 좌우 반전(좌우가 이어지지 않는 그림용)
            public bool ExtendBottom;    // 밑변 색을 아래로 길게 늘려, 층이 떠올라도 밑이 비지 않게
            public bool ExtendTop;       // 윗변 색을 위로 길게 늘려(하늘), 카메라가 올라가도 위가 비지 않게
            public Color Tint = Color.white;
            public float Drift;          // 카메라와 상관없이 저절로 흘러가는 속도(칸/초). 안개처럼 바람에 밀리는 층용
        }

        private const float ExtensionHeight = 14f;

        private sealed class LayerState
        {
            public Layer Def;
            public Sprite Sprite;
            public float TileWidth;
            public readonly List<Transform> Copies = new();
            public Transform Root;
        }

        private readonly List<LayerState> layers = new();
        private float floorY;
        private float restCameraY;
        private float anchorX;

        // anchorX: 이 x에 카메라가 있을 때 층들이 기본 자리에 놓인다(보통 맵 가운데).
        public void Initialize(IEnumerable<Layer> defs, float mapFloorY, float cameraRestY, float mapAnchorX)
        {
            floorY = mapFloorY;
            restCameraY = cameraRestY;
            anchorX = mapAnchorX;

            foreach (var def in defs)
            {
                var sprite = Resources.Load<Sprite>(def.Path);
                if (sprite == null)
                {
                    Debug.LogWarning($"Aethoria: 시차 배경 그림을 찾을 수 없음 ({def.Path})");
                    continue;
                }

                var root = new GameObject("Parallax_" + System.IO.Path.GetFileName(def.Path)).transform;
                root.SetParent(transform, false);
                var state = new LayerState
                {
                    Def = def,
                    Sprite = sprite,
                    TileWidth = sprite.bounds.size.x * def.Scale,
                    Root = root,
                };
                layers.Add(state);
                EnsureCopies(state, def.Tile ? 3 : 1);
            }

            var cam = Camera.main;
            if (cam != null) Reposition(cam);
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam == Camera.main) Reposition(cam);
        }

        private void Reposition(Camera cam)
        {
            Vector3 c = cam.transform.position;
            float halfView = cam.orthographicSize * cam.aspect;

            foreach (var state in layers)
            {
                var def = state.Def;
                float originX = anchorX + (c.x - anchorX) * (1f - def.ScrollX) + Time.time * def.Drift;
                float y = floorY + def.BottomAboveFloor + (c.y - restCameraY) * (1f - def.ScrollY);

                if (!def.Tile)
                {
                    state.Copies[0].position = new Vector3(originX, y, 0f);
                    continue;
                }

                float w = state.TileWidth;
                int needed = Mathf.CeilToInt(halfView * 2f / w) + 2;
                EnsureCopies(state, needed);

                // 화면 왼쪽 끝을 덮는 칸부터 오른쪽으로 차례로 놓는다. k번째 칸의 가운데 = originX + k·w.
                int first = Mathf.FloorToInt((c.x - halfView - originX) / w + 0.5f);
                for (int i = 0; i < state.Copies.Count; i++)
                {
                    int k = first + i;
                    var copy = state.Copies[i];
                    copy.position = new Vector3(originX + k * w, y, 0f);
                    if (def.Mirror)
                    {
                        bool flip = (k & 1) != 0;
                        foreach (var sr in copy.GetComponentsInChildren<SpriteRenderer>()) sr.flipX = flip;
                    }
                }
            }
        }

        private void EnsureCopies(LayerState state, int count)
        {
            while (state.Copies.Count < count) state.Copies.Add(CreateCopy(state));
        }

        private Transform CreateCopy(LayerState state)
        {
            var def = state.Def;
            var go = new GameObject("Tile", typeof(SpriteRenderer));
            go.transform.SetParent(state.Root, false);
            go.transform.localScale = new Vector3(def.Scale, def.Scale, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = state.Sprite;
            sr.sortingOrder = def.SortingOrder;
            sr.color = def.Tint;

            if (def.ExtendBottom) AddExtension(go.transform, state.Sprite, def, top: false);
            if (def.ExtendTop) AddExtension(go.transform, state.Sprite, def, top: true);
            return go.transform;
        }

        // 그림 맨 위(또는 맨 아래) 몇 줄을 잘라 세로로 길게 늘린 띠. 가장자리 1줄은 빼고 그 안쪽 2줄을 쓴다.
        private static void AddExtension(Transform parent, Sprite source, Layer def, bool top)
        {
            Rect r = source.rect;
            var strip = top ? new Rect(r.x, r.yMax - 3f, r.width, 2f) : new Rect(r.x, r.y + 1f, r.width, 2f);
            var sprite = Sprite.Create(source.texture, strip, new Vector2(0.5f, top ? 0f : 1f), source.pixelsPerUnit);

            var go = new GameObject(top ? "ExtendTop" : "ExtendBottom", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            float stripHeight = 2f / source.pixelsPerUnit;
            // 부모(층 그림)가 이미 Scale만큼 커져 있으니 세로 늘림은 그만큼 나눠 준다.
            go.transform.localScale = new Vector3(1f, ExtensionHeight / def.Scale / stripHeight, 1f);
            // 이음매가 비치지 않게 그림 가장자리에 살짝 겹친다.
            float overlap = 0.02f;
            go.transform.localPosition = new Vector3(0f, top ? source.bounds.size.y - overlap : overlap, 0f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = def.SortingOrder;
            sr.color = def.Tint;
        }
    }
}

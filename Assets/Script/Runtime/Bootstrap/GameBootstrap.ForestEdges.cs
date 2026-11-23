using System.Collections.Generic;
using UnityEngine;
using Aethoria.Stages;

namespace Aethoria.Bootstrap
{
    // 어둠의 숲 방 테두리 그림: 좌우 흙벽 기둥, 천장 흙 띠, 출구 자리의 숲길 입구·천장 뿌리·빛기둥·절벽 틈.
    // 충돌은 그대로(MapBounds 벽)이고 그림만 얹는다. 벽은 방 바깥(xMin 왼쪽, xMax+1 오른쪽)에 서고,
    // 카메라를 방 밖으로 ForestCameraMargin칸 더 보내서 벽이 화면에 들어오게 한다.
    // 그림은 Resources/Art/Props/ForestEdge/(1칸 = 135px로 미리 줄인 것, 피벗 아래 가운데).
    // 흙 타일은 이음매가 맞게 그린 그림이 아니라서, 옆 칸끼리 좌우(세로로 쌓을 땐 상하)를 번갈아 뒤집어 거울처럼 이어 붙인다.
    public static partial class GameBootstrap
    {
        private const string ForestEdgeFolder = "Art/Props/ForestEdge/";
        private const float EdgePx = 135f;
        private const float EdgeTile = 1.5f;            // 흙 타일 한 칸(곧게 잘린 몸통)
        private const float EdgeBandAbove = 10f;        // 천장 위로 덮는 흙 두께(카메라가 올라가도 하늘이 비치지 않게)
        private const float EdgeBelowFloor = 3f;        // 벽 기둥이 바닥 아래로 내려가는 깊이
        private const float ArchOpening = 3.4f;         // 좌우 출구에서 벽을 비우는 높이(입구 그림 안쪽 어두운 통로 높이)
        private const int ForestCameraMargin = 1;
        private const int EdgeWallOrder = -8, EdgeCeilingOrder = -7, EdgeDecorOrder = -6, EdgeLightOrder = -8;
        private const int EdgeShadowOrder = -8, EdgeShaftOrder = -7;   // 빛기둥은 천장 그늘 위에
        private const int EdgeRootsOrder = -9;                         // 늘어진 뿌리는 천장 그늘 뒤
        private static readonly Color RootsTint = new Color(0.78f, 0.72f, 0.88f);
        private const float TopGapHalf = 1.3f, FloorGapHalf = 1.25f;

        // 그림 속 기준선(px, 그림 왼쪽 위 기준). 잘라 낼 때 잰 값.
        private const float WallBodyPx = 186f;          // edge_wall: 왼쪽 끝 ~ 드러난 오른쪽 면(그 너머는 덩굴)
        private const float WallTopBodyPx = 212f, WallTopGrassPx = 28f; // edge_wall_top: 면 위치, 윗면 위로 솟은 풀
        private const float CliffLSurfacePx = 65f, CliffLLipPx = 384f;  // edge_cliff_l: 밟는 윗면 높이, 구멍 쪽 돌 끝
        private const float CliffRSurfacePx = 44f, CliffRLipPx = 164f;

        private static readonly Dictionary<string, Sprite> edgeSprites = new();

        private static Sprite EdgeSprite(string name)
        {
            if (!edgeSprites.TryGetValue(name, out var sprite) || sprite == null)
                edgeSprites[name] = sprite = Resources.Load<Sprite>(ForestEdgeFolder + name);
            return sprite;
        }

        private static Vector2 EdgeSize(string name)
        {
            var sprite = EdgeSprite(name);
            return sprite != null ? (Vector2)sprite.bounds.size : Vector2.zero;
        }

        // 그림의 왼쪽 아래 모서리를 (x0, y0)에 놓는다. 뒤집어도 같은 칸을 차지한다. fitWidth > 0이면 가로를 그 폭에 맞춰 늘린다.
        private static SpriteRenderer EdgePiece(Transform parent, string name, float x0, float y0, bool flipX, bool flipY,
            int order, float fitWidth = 0f)
        {
            var sprite = EdgeSprite(name);
            if (sprite == null) return null;
            Vector2 size = sprite.bounds.size;
            float sx = fitWidth > 0f ? fitWidth / size.x : 1f;
            var go = new GameObject("Edge_" + name, typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.localScale = new Vector3(sx, 1f, 1f);
            // 피벗이 아래 가운데라 flipY면 그림이 피벗 아래로 그려진다 → 그땐 피벗을 위끝에 둔다.
            go.transform.position = new Vector3(x0 + size.x * sx / 2f, flipY ? y0 + size.y : y0, 0f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flipX;
            sr.flipY = flipY;
            sr.sortingOrder = order;
            return sr;
        }

        private static void BuildForestEdges(Transform root, List<ForestExit> exits, float left, float right, int floorY, int height)
        {
            var parent = new GameObject("ForestEdges").transform;
            parent.SetParent(root);
            float ceil = floorY + height;

            // 천장: 위로 뚫린 출구 자리만 비우고 방 밖 벽 위까지 덮는다.
            var gaps = new List<Vector2>();
            foreach (var e in exits)
                if (e.Side == ExitSide.Top) gaps.Add(new Vector2(e.Pos - TopGapHalf, e.Pos + TopGapHalf));
            gaps.Sort((a, b) => a.x.CompareTo(b.x));
            float runStart = left - ForestCameraMargin - EdgeTile;
            bool gapBefore = false;
            foreach (var g in gaps)
            {
                CeilingRun(parent, runStart, g.x, ceil, gapBefore, true);
                CeilingShadow(parent, runStart, g.x, ceil, gapBefore, true);
                runStart = g.y;
                gapBefore = true;
            }
            CeilingRun(parent, runStart, right + ForestCameraMargin + EdgeTile, ceil, gapBefore, false);
            CeilingShadow(parent, runStart, right + ForestCameraMargin + EdgeTile, ceil, gapBefore, false);

            SideWall(parent, exits, ExitSide.Left, left, floorY, ceil);
            SideWall(parent, exits, ExitSide.Right, right, floorY, ceil);

            foreach (var e in exits)
            {
                switch (e.Side)
                {
                    case ExitSide.Left:
                    case ExitSide.Right:
                    {
                        // 숲길 입구: 방 끝선에 가운데를 맞춰 반은 벽 속, 반은 방 안에 선다. 출구마다 늘 같은 모양.
                        int hash = 0;
                        foreach (char ch in forestRoom + e.Id) hash = hash * 31 + ch;
                        string arch = "edge_arch_" + (1 + Mathf.Abs(hash) % 3);
                        Vector2 size = EdgeSize(arch);
                        float cx = e.Side == ExitSide.Left ? left : right;
                        EdgePiece(parent, arch, cx - size.x / 2f, floorY + e.Pos - 0.12f, e.Side == ExitSide.Right, false, EdgeDecorOrder);
                        break;
                    }
                    case ExitSide.Top:
                    {
                        // 뚫린 천장 가장자리에 늘어진 뿌리. 위 돌 띠가 천장 아랫면을 살짝 덮게 올려 붙인다.
                        float gapL = e.Pos - TopGapHalf, gapR = e.Pos + TopGapHalf;
                        Vector2 rl = EdgeSize("edge_roots_l"), rr = EdgeSize("edge_roots_r");
                        // 천장 그늘 뒤에 깔아서 윗부분은 어둠에 묻히고 끝자락만 드러나게, 색도 장면 톤으로 조금 누른다.
                        var rootL = EdgePiece(parent, "edge_roots_l", gapL + 0.35f - rl.x, ceil + 0.5f - rl.y, false, false, EdgeRootsOrder);
                        var rootR = EdgePiece(parent, "edge_roots_r", gapR - 0.35f, ceil + 0.5f - rr.y, false, false, EdgeRootsOrder);
                        if (rootL != null) rootL.color = RootsTint;
                        if (rootR != null) rootR.color = RootsTint;
                        // 열린 길이면 위에서 빛기둥이 내려온다(숨겨진·막힌 길은 빼고).
                        if (e.Open == null || e.Open())
                        {
                            FadedShaft(parent, e.Pos, ceil + 0.6f, new Color(1f, 1f, 1f, 0.6f));
                        }
                        HoleDarkness(parent, gapL, gapR, ceil);
                        break;
                    }
                }
            }
        }

        // 빛줄기: 그림 맨 위가 곧게 잘려 있고 거기가 가장 밝아서, 위로 뚫린 구멍 안에서 빛이 허공에서 뚝 끊겨 보였다.
        // 윗부분(ShaftFadeFraction)을 얇은 가로 조각으로 잘라 위로 갈수록 투명하게 그려서 빛이 어둠 속에서 서서히 피어나게 한다.
        // 조각은 같은 그림의 정확한 픽셀 구간이라 이음매가 생기지 않는다.
        private const float ShaftFadeFraction = 0.4f;
        private const int ShaftFadeSlices = 24;
        private static readonly List<Sprite> shaftSlices = new();
        private static Texture2D shaftSliceTexture;

        private static void FadedShaft(Transform parent, float x, float topY, Color tint)
        {
            var sprite = EdgeSprite("edge_shaft");   // 숲 그림을 다른 지역도 색만 바꿔 쓴다
            if (sprite == null) return;
            Rect r = sprite.rect;
            float ppu = sprite.pixelsPerUnit;
            int slicePx = Mathf.Max(1, Mathf.RoundToInt(r.height * ShaftFadeFraction / ShaftFadeSlices));
            float fadePx = slicePx * ShaftFadeSlices, bodyPx = r.height - fadePx;
            if (shaftSliceTexture != sprite.texture || shaftSlices.Count == 0 || shaftSlices[0] == null)
            {
                shaftSlices.Clear();
                shaftSliceTexture = sprite.texture;
                shaftSlices.Add(Sprite.Create(sprite.texture, new Rect(r.x, r.y, r.width, bodyPx), new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect));
                for (int k = 0; k < ShaftFadeSlices; k++)
                    shaftSlices.Add(Sprite.Create(sprite.texture, new Rect(r.x, r.y + bodyPx + k * slicePx, r.width, slicePx), new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect));
            }
            float bottomY = topY - r.height / ppu;
            for (int k = 0; k <= ShaftFadeSlices; k++)
            {
                float a = 1f;
                if (k > 0)
                {
                    float t = 1f - (k - 0.5f) / ShaftFadeSlices;        // 아래 1 → 위 0
                    a = t * t * (3f - 2f * t);
                }
                var go = new GameObject("Edge_Shaft", typeof(SpriteRenderer));
                go.transform.SetParent(parent);
                float y = k == 0 ? bottomY : bottomY + (bodyPx + (k - 1) * slicePx) / ppu;
                go.transform.position = new Vector3(x, y, 0f);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = shaftSlices[k];
                sr.color = new Color(tint.r, tint.g, tint.b, tint.a * a);
                sr.sortingOrder = EdgeShaftOrder;
            }
        }

        // 위로 뚫린 구멍 속 그늘: 천장선 근처에서는 투명하고 위로 올라갈수록 짙어져 HoleDarkTop부터는 꽉 찬 어둠이 된다.
        // 구멍 너머 밤하늘 배경을 가려서, 빛줄기가 깊은 어둠 속에서 새어 나오는 것처럼 보이게 한다.
        // 띠마다 자기 높이부터 맨 위까지 덮게 겹쳐 쌓아(짙기를 누적) 띠 사이에 가는 줄이 생기지 않게 한다.
        private const float HoleDarkBottom = -0.4f, HoleDarkTop = 1.4f;
        private const int HoleDarkSteps = 20;

        private static void HoleDarkness(Transform parent, float x0, float x1, float ceil)
        {
            float width = x1 - x0, cx = (x0 + x1) / 2f;
            float top = ceil + EdgeBandAbove;
            var c = CeilingShadowColor;
            float prev = 0f;
            for (int i = 1; i <= HoleDarkSteps; i++)
            {
                float v = (float)i / HoleDarkSteps;
                float target = v * v * (3f - 2f * v) * c.a;            // 이 높이까지 쌓인 짙기
                float a = 1f - (1f - target) / (1f - prev);             // 이번 띠가 더해야 할 짙기
                prev = target;
                float y0 = ceil + HoleDarkBottom + (HoleDarkTop - HoleDarkBottom) * (i - 1f) / HoleDarkSteps;
                RoomArt.Rect(parent, "Edge_HoleDark", new Vector2(cx, (y0 + top) / 2f), new Vector2(width, top - y0),
                    new Color(c.r, c.g, c.b, a), EdgeDecorOrder);
            }
        }

        // 천장 아래로 드리우는 그늘: 천장선에서 짙고 CeilingShadowDepth만큼 내려오며 사라진다. 천장이 화면 밖에 있어도
        // 위쪽이 어둑해져서, 늘어진 뿌리가 허공(밤하늘 배경)에 떠 있지 않고 어둠 속 흙 천장에서 자라 나온 것처럼 보인다.
        // 뚫린 구멍 쪽 끝은 옆으로도 옅어져서 구멍 아래는 밝게 남는다(빛이 들어오는 자리).
        private const float CeilingShadowDepth = 7f;
        private static readonly Color CeilingShadowColor = new Color(0.035f, 0.02f, 0.06f, 0.92f);
        private static readonly Dictionary<int, Sprite> shadowSprites = new();

        private static void CeilingShadow(Transform parent, float x0, float x1, float ceil, bool holeOnLeft, bool holeOnRight)
        {
            float width = x1 - x0;
            if (width <= 0.1f) return;
            const float sideFade = 1.6f;   // 구멍 쪽으로 옅어지는 폭(뿌리 그림이 덮는 정도)
            int key = (holeOnLeft ? 1 : 0) | (holeOnRight ? 2 : 0) | (Mathf.RoundToInt(width * 4f) << 2);
            if (!shadowSprites.TryGetValue(key, out var sprite) || sprite == null)
            {
                const int tw = 128, th = 64;
                var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color[tw * th];
                float fadeFrac = Mathf.Min(0.5f, sideFade / width);
                for (int y = 0; y < th; y++)
                {
                    float v = y / (th - 1f);                 // 0 = 아래(옅음), 1 = 천장선(짙음)
                    float va = v * v * (3f - 2f * v);
                    for (int x = 0; x < tw; x++)
                    {
                        float u = x / (tw - 1f), ha = 1f;
                        if (holeOnLeft) ha = Mathf.Min(ha, Mathf.Clamp01(u / fadeFrac));
                        if (holeOnRight) ha = Mathf.Min(ha, Mathf.Clamp01((1f - u) / fadeFrac));
                        ha = ha * ha * (3f - 2f * ha);
                        px[y * tw + x] = new Color(1f, 1f, 1f, va * ha);
                    }
                }
                tex.SetPixels(px);
                tex.Apply();
                sprite = Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 1f), tw);
                shadowSprites[key] = sprite;
            }
            var go = new GameObject("Edge_CeilingShadow", typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = new Vector3((x0 + x1) / 2f, ceil, 0f);
            go.transform.localScale = new Vector3(width, CeilingShadowDepth * 2f, 1f); // 스프라이트 크기 1 x 0.5
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = CeilingShadowColor;
            sr.sortingOrder = EdgeShadowOrder;
        }

        // 천장 한 구간(x0~x1). 뚫린 구멍에 닿은 칸은 구멍 쪽으로 면이 드러난 모서리·벽 타일을 쓴다.
        private static void CeilingRun(Transform parent, float x0, float x1, float ceil, bool gapOnLeft, bool gapOnRight)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / EdgeTile - 0.01f));
            float wallW = EdgeSize("edge_wall").x, cornerW = EdgeSize("edge_ceiling_corner").x;
            float ceilingH = EdgeSize("edge_ceiling").y;
            for (int i = 0; i < n; i++)
            {
                // 구멍이 오른쪽에만 있으면 구멍 끝에서부터 왼쪽으로 깐다(구멍 가장자리 칸이 딱 맞게).
                float x = gapOnRight && !gapOnLeft ? x1 - EdgeTile * (n - i) : Mathf.Min(x0 + EdgeTile * i, x1 - EdgeTile);
                bool edgeL = gapOnLeft && i == 0, edgeR = gapOnRight && i == n - 1;
                bool mirror = (Mathf.FloorToInt(x / EdgeTile + 0.5f) & 1) != 0;
                float rowY = ceil - 0.05f;

                if (edgeR) EdgePiece(parent, "edge_ceiling_corner", x, rowY, false, false, EdgeCeilingOrder);
                else if (edgeL) EdgePiece(parent, "edge_ceiling_corner", x + EdgeTile - cornerW, rowY, true, false, EdgeCeilingOrder);
                else EdgePiece(parent, "edge_ceiling", x, rowY, mirror, false, EdgeCeilingOrder, EdgeTile);

                int k = 0;
                for (float y = rowY + ceilingH - 0.03f; y < ceil + EdgeBandAbove; y += EdgeTile - 0.01f, k++)
                {
                    bool flipY = (k & 1) != 0;
                    if (edgeR) EdgePiece(parent, "edge_wall", x, y, false, flipY, EdgeCeilingOrder);
                    else if (edgeL) EdgePiece(parent, "edge_wall", x + EdgeTile - wallW, y, true, flipY, EdgeCeilingOrder);
                    else EdgePiece(parent, "edge_fill", x, y, mirror, flipY, EdgeCeilingOrder, EdgeTile);
                }
            }
        }

        // 방 왼쪽(오른쪽) 끝 바깥에 서는 흙벽 기둥. 그 쪽 출구 자리(바닥 위 턱 높이부터 ArchOpening만큼)는 비운다.
        private static void SideWall(Transform parent, List<ForestExit> exits, ExitSide side, float edgeX, int floorY, float ceil)
        {
            bool isLeft = side == ExitSide.Left;
            Vector2 wall = EdgeSize("edge_wall"), wallTop = EdgeSize("edge_wall_top");
            if (wall == Vector2.zero) return;
            float body = WallBodyPx / EdgePx;
            // 드러난 면(몸통 끝)이 방 끝선에 오게. 오른쪽 벽은 좌우를 뒤집어 면이 왼쪽을 본다.
            float wallX = isLeft ? edgeX - body : edgeX - (wall.x - body);
            float topBody = WallTopBodyPx / EdgePx;
            float wallTopX = isLeft ? edgeX - topBody : edgeX - (wallTop.x - topBody);

            var holes = new List<Vector2>();
            foreach (var e in exits)
                if (e.Side == side) holes.Add(new Vector2(floorY + e.Pos, floorY + e.Pos + ArchOpening));
            holes.Sort((a, b) => a.x.CompareTo(b.x));

            float segBottom = floorY - EdgeBelowFloor;
            foreach (var h in holes)
            {
                // 구멍 아래 구간은 구멍 밑선에서부터 아래로 쌓는다. 바닥보다 높은 턱이면 맨 위에 풀 덮인 윗면 타일.
                float y = h.x;
                bool ledge = h.x > floorY + 0.3f;
                if (ledge && wallTop != Vector2.zero)
                {
                    float topH = wallTop.y - WallTopGrassPx / EdgePx;
                    EdgePiece(parent, "edge_wall_top", wallTopX, y - topH, !isLeft, false, EdgeWallOrder);
                    y -= topH - 0.01f;
                }
                for (int k = 0; y > segBottom; k++)
                {
                    y -= EdgeTile - 0.01f;
                    EdgePiece(parent, "edge_wall", wallX, y, !isLeft, (k & 1) != 0, EdgeWallOrder);
                }
                segBottom = h.y;
            }
            // 마지막 구멍 위(구멍이 없으면 바닥 아래부터) 천장까지는 아래에서 위로 쌓는다.
            int j = 0;
            for (float y = segBottom; y < ceil; y += EdgeTile - 0.01f, j++)
                EdgePiece(parent, "edge_wall", wallX, y, !isLeft, (j & 1) != 0, EdgeWallOrder);
        }

        // 바닥 구멍 그림: 어두운 구덩이 양옆에 무너진 절벽 끝을 세운다. 절벽 윗면 돌 끝이 구멍 가장자리에 맞는다.
        private static void FloorGapArt(Transform root, float x, int floorY)
        {
            // 구덩이 속 어둠(edge_pit: 폭 5칸, 가운데 2.6칸은 꽉 차고 양옆 1.2칸은 서서히 옅어진다).
            // 절벽 조각 뒤에 깔려서 튀어나온 돌 끝 밑의 그늘까지 어둡게 한다.
            Vector2 pit = EdgeSize("edge_pit");
            if (pit != Vector2.zero) EdgePiece(root, "edge_pit", x - pit.x / 2f, floorY + 0.02f - pit.y, false, false, EdgeLightOrder);
            Vector2 cl = EdgeSize("edge_cliff_l"), cr = EdgeSize("edge_cliff_r");
            if (cl != Vector2.zero)
                EdgePiece(root, "edge_cliff_l", x - FloorGapHalf - CliffLLipPx / EdgePx, floorY + 0.03f - (cl.y - CliffLSurfacePx / EdgePx), false, false, EdgeDecorOrder);
            if (cr != Vector2.zero)
                EdgePiece(root, "edge_cliff_r", x + FloorGapHalf - CliffRLipPx / EdgePx, floorY + 0.03f - (cr.y - CliffRSurfacePx / EdgePx), false, false, EdgeDecorOrder);
        }
    }
}

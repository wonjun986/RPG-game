using System.Collections.Generic;
using UnityEngine;
using Aethoria.Data;

namespace Aethoria.Bootstrap
{
    // 평야·폐허·바다 방 테두리 그림. 지역 폴더(Resources/Art/Props/<지역>Edge/)에 그림이 있으면 그 그림으로, 없으면
    // 색 사각형(BuildRegionEdges의 임시 그림)으로 그린다. 충돌은 그대로(MapBounds 벽)이고 그림만 얹는다.
    // 그림(1칸 = 135px, 피벗 아래 가운데):
    //   edge_fill     이음매 없는 돌벽(3×3칸) — 좌우 벽과 천장 위를 채운다. 구간 길이에 맞게 살짝 늘려 끝을 딱 맞춘다.
    //   edge_ceiling  천장 아랫면 띠(6칸 폭, 위 0.63칸이 돌, 그 아래로 덩굴이 늘어짐) — 좌우를 번갈아 뒤집어 잇는다.
    //   edge_corner   위로 뚫린 구멍 가장자리(왼쪽이 부서진 면). 구멍 오른쪽엔 그대로, 왼쪽엔 뒤집어 놓는다.
    //   edge_arch     옆 통로 석문. 방 끝선에 가운데를 맞춰 반은 벽 속, 반은 방 안.
    //   edge_cliff_l / edge_cliff_r  바닥 구멍 왼쪽·오른쪽 절벽,  edge_pit  구멍 속 어둠(없으면 어두운 사각형),  edge_wall_top  높은 턱 윗면.
    //   edge_arch_1~3  문틀 여러 종(있으면 출구마다 하나를 고른다),  edge_hang_l / edge_hang_r  천장에 늘어진 장식.
    public static partial class GameBootstrap
    {
        private const float RegionFillTile = 3f;
        private const float RegionCeilingSink = 0.38f;                 // 천장 띠 돌 아랫선이 천장선보다 이만큼 아래로 내려온다
        private const float RegionHangSpacing = 9f;                    // 천장에 늘어진 장식(edge_hang_l/r)을 다는 간격

        // 그림 속 기준선(px, 그림 위끝 기준, 1칸 = 135px로 줄인 뒤). 잘라 낼 때 잰 값.
        private sealed class RegionEdgeMetrics
        {
            public float CeilingStonePx;            // edge_ceiling: 위에서 돌 띠가 끝나는 줄
            public float CornerUnderPx;             // edge_corner 오른쪽 끝: 돌 아랫선
            public float CliffLSurfacePx, CliffLLipPx, CliffRSurfacePx, CliffRLipPx;
            public float WallTopSurfacePx;          // edge_wall_top: 밟는 윗면
        }

        private static readonly Dictionary<string, RegionEdgeMetrics> RegionMetrics = new()
        {
            { TreasureChests.Plains, new RegionEdgeMetrics { CeilingStonePx = 85f, CornerUnderPx = 287f, CliffLSurfacePx = 40f, CliffLLipPx = 330f,
                CliffRSurfacePx = 26f, CliffRLipPx = 32f, WallTopSurfacePx = 45f } },
            { TreasureChests.Ruins, new RegionEdgeMetrics { CeilingStonePx = 83f, CornerUnderPx = 392f, CliffLSurfacePx = 55f, CliffLLipPx = 330f,
                CliffRSurfacePx = 53f, CliffRLipPx = 60f, WallTopSurfacePx = 72f } },
            { TreasureChests.Sea, new RegionEdgeMetrics { CeilingStonePx = 100f, CornerUnderPx = 465f, CliffLSurfacePx = 89f, CliffLLipPx = 398f,
                CliffRSurfacePx = 85f, CliffRLipPx = 4f, WallTopSurfacePx = 94f } },
        };

        private static string RegionEdgeFolder(string region) => region == TreasureChests.Plains ? "Art/Props/PlainsEdge/"
            : region == TreasureChests.Ruins ? "Art/Props/RuinsEdge/" : "Art/Props/SeaEdge/";

        private static readonly Dictionary<string, Sprite> regionEdgeSprites = new();

        private static Sprite RegionEdgeSprite(string region, string name)
        {
            string key = RegionEdgeFolder(region) + name;
            if (!regionEdgeSprites.TryGetValue(key, out var s) || s == null)
                regionEdgeSprites[key] = s = Resources.Load<Sprite>(key);
            return s;
        }

        private static bool HasRegionEdgeArt(string region) => RegionEdgeSprite(region, "edge_fill") != null;

        // 그림의 왼쪽 아래 모서리를 (x0, y0)에 놓는다. sx·sy는 늘리는 배율.
        private static SpriteRenderer RegionPiece(Transform parent, Sprite sprite, float x0, float y0, bool flipX, int order,
            float sx = 1f, float sy = 1f, Color? tint = null)
        {
            if (sprite == null) return null;
            Vector2 size = sprite.bounds.size;
            var go = new GameObject("Edge_" + sprite.name, typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.localScale = new Vector3(sx, sy, 1f);
            go.transform.position = new Vector3(x0 + size.x * sx / 2f, y0, 0f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flipX;
            sr.sortingOrder = order;
            if (tint.HasValue) sr.color = tint.Value;
            return sr;
        }

        // [x0,x1]×[y0,y1] 사각형을 돌벽 타일로 빈틈없이 채운다(타일 수를 정하고 그만큼 늘려 끝을 딱 맞춘다).
        private static void FillRect(Transform parent, Sprite fill, float x0, float x1, float y0, float y1, Color tint)
        {
            if (fill == null || x1 - x0 < 0.05f || y1 - y0 < 0.05f) return;
            Vector2 size = fill.bounds.size;
            int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / RegionFillTile - 0.05f));
            int ny = Mathf.Max(1, Mathf.CeilToInt((y1 - y0) / RegionFillTile - 0.05f));
            float w = (x1 - x0) / nx, h = (y1 - y0) / ny;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    RegionPiece(parent, fill, x0 + i * w, y0 + j * h, false, EdgeWallOrder, (w + 0.01f) / size.x, (h + 0.01f) / size.y, tint);
        }

        private static void BuildRegionEdgeArt(RegionRoomCtx c, List<ForestExit> exits, bool dark)
        {
            string r = c.Region;
            var m = RegionMetrics[r];
            var parent = new GameObject("RoomEdges").transform;
            parent.SetParent(c.Root);
            Color tint = dark ? new Color(0.62f, 0.62f, 0.72f) : Color.white;
            Color wallTint = dark ? new Color(0.5f, 0.5f, 0.6f) : new Color(0.82f, 0.82f, 0.82f); // 벽 속은 조금 어둡게(앞의 장식이 도드라지게)
            var fill = RegionEdgeSprite(r, "edge_fill");
            const float thick = 3.5f, above = 10f, below = 3f;
            float ceil = c.FloorY + c.Height;

            // 천장 위 돌벽(위 출구 자리는 비운다)
            var gaps = new List<float>();
            foreach (var e in exits) if (e.Side == ExitSide.Top) gaps.Add(e.Pos);
            gaps.Sort();
            var runs = new List<Vector2>();
            float run = c.Left - thick;
            foreach (float g in gaps) { runs.Add(new Vector2(run, g - TopGapHalf)); run = g + TopGapHalf; }
            runs.Add(new Vector2(run, c.Right + thick));
            foreach (var seg in runs) FillRect(parent, fill, seg.x, seg.y, ceil, ceil + above, wallTint);

            // 좌우 벽(옆 출구 자리는 비운다)
            foreach (var side in new[] { ExitSide.Left, ExitSide.Right })
            {
                bool isLeft = side == ExitSide.Left;
                float x0 = isLeft ? c.Left - thick : c.Right, x1 = isLeft ? c.Left : c.Right + thick;
                var holes = new List<Vector2>();
                foreach (var e in exits) if (e.Side == side) holes.Add(new Vector2(c.FloorY + e.Pos, c.FloorY + e.Pos + ArchOpening));
                holes.Sort((a, b) => a.x.CompareTo(b.x));
                float y = c.FloorY - below;
                foreach (var h in holes)
                {
                    FillRect(parent, fill, x0, x1, y, h.x, wallTint);
                    y = h.y;
                }
                FillRect(parent, fill, x0, x1, y, ceil, wallTint);
            }

            // 천장 아랫면 덩굴 띠: 돌 아랫선이 천장선보다 조금 아래(RegionCeilingSink)에 오게.
            var strip = RegionEdgeSprite(r, "edge_ceiling");
            if (strip != null)
            {
                Vector2 ss = strip.bounds.size;
                float stripBottom = ceil - RegionCeilingSink + m.CeilingStonePx / EdgePx - ss.y;
                foreach (var seg in runs)
                {
                    int n = Mathf.Max(1, Mathf.CeilToInt((seg.y - seg.x) / ss.x - 0.05f));
                    float w = (seg.y - seg.x) / n;
                    for (int i = 0; i < n; i++)
                    {
                        float x = seg.x + i * w;
                        bool flip = (Mathf.FloorToInt(x / ss.x + 0.5f) & 1) != 0;
                        RegionPiece(parent, strip, x, stripBottom, flip, EdgeCeilingOrder, (w + 0.02f) / ss.x, 1f, tint);
                    }
                }
            }

            // 천장에서 늘어진 장식(모래 줄기·해초 등): 구멍 가장자리와 벽에서 떨어진 곳에 간격을 두고 좌우 번갈아.
            var hangL = RegionEdgeSprite(r, "edge_hang_l");
            var hangR = RegionEdgeSprite(r, "edge_hang_r");
            if (hangL != null || hangR != null)
            {
                int k = 0;
                foreach (var seg in runs)
                {
                    float from = Mathf.Max(seg.x, c.Left) + 3f, to = Mathf.Min(seg.y, c.Right) - 3f;
                    for (float x = from + 1.5f; x <= to; x += RegionHangSpacing, k++)
                    {
                        var hs = (k & 1) == 0 ? (hangL ?? hangR) : (hangR ?? hangL);
                        Vector2 sz = hs.bounds.size;
                        RegionPiece(parent, hs, x - sz.x / 2f, ceil - RegionCeilingSink + 0.2f - sz.y, false, EdgeCeilingOrder - 1, 1f, 1f, tint);   // 천장 띠 뒤로 윗부분이 숨는다
                    }
                }
            }

            // 위 구멍 가장자리 모서리(부서진 면이 구멍 쪽) + 위에서 내려오는 빛줄기
            var corner = RegionEdgeSprite(r, "edge_corner");
            foreach (var e in exits)
            {
                if (e.Side != ExitSide.Top) continue;
                if (corner != null)
                {
                    Vector2 cs = corner.bounds.size;
                    float bottom = ceil - RegionCeilingSink - (cs.y - m.CornerUnderPx / EdgePx);
                    RegionPiece(parent, corner, e.Pos + TopGapHalf - 0.3f, bottom, false, EdgeDecorOrder, 1f, 1f, tint);
                    RegionPiece(parent, corner, e.Pos - TopGapHalf + 0.3f - cs.x, bottom, true, EdgeDecorOrder, 1f, 1f, tint);
                }
                if (e.Open == null || e.Open())
                    FadedShaft(parent, e.Pos, ceil + 0.6f, RegionShaftTint(r));
            }

            // 옆 통로 석문(높은 턱이면 턱 윗면도)
            var arches = new List<Sprite>();
            foreach (var n in new[] { "edge_arch", "edge_arch_1", "edge_arch_2", "edge_arch_3" })
            {
                var sp = RegionEdgeSprite(r, n);
                if (sp != null) arches.Add(sp);
            }
            var wallTop = RegionEdgeSprite(r, "edge_wall_top");
            foreach (var e in exits)
            {
                if (e.Side != ExitSide.Left && e.Side != ExitSide.Right) continue;
                float cx = e.Side == ExitSide.Left ? c.Left : c.Right;
                float baseY = c.FloorY + e.Pos;
                if (e.Pos > 0.3f && wallTop != null)
                {
                    Vector2 ws = wallTop.bounds.size;
                    RegionPiece(parent, wallTop, cx - ws.x / 2f, baseY - (ws.y - m.WallTopSurfacePx / EdgePx), e.Side == ExitSide.Right, EdgeDecorOrder, 1f, 1f, tint);
                }
                if (arches.Count > 0)
                {
                    // 출구마다 늘 같은 문틀(방 이름 + 출구 이름으로 고른다).
                    int hash = 0;
                    foreach (char ch in regionRoom + e.Id) hash = hash * 31 + ch;
                    var arch = arches[Mathf.Abs(hash) % arches.Count];
                    Vector2 a = arch.bounds.size;
                    RegionPiece(parent, arch, cx - a.x / 2f, baseY - 0.1f, e.Side == ExitSide.Right, EdgeDecorOrder, 1f, 1f, tint);
                }
            }

            // 바닥 구멍: 구덩이 어둠 + 양쪽 무너진 절벽(밟는 윗면이 바닥선, 돌 끝이 구멍 가장자리)
            var pit = RegionEdgeSprite(r, "edge_pit");
            var cliffL = RegionEdgeSprite(r, "edge_cliff_l");
            var cliffR = RegionEdgeSprite(r, "edge_cliff_r");
            foreach (var e in exits)
            {
                if (e.Side != ExitSide.Bottom || (e.Open != null && !e.Open())) continue;
                if (pit != null)
                {
                    Vector2 p = pit.bounds.size;
                    RegionPiece(parent, pit, e.Pos - p.x / 2f, c.FloorY + 0.1f - p.y, false, EdgeLightOrder, 1f, 1f, tint);
                }
                else
                {
                    // 구덩이 그림이 없는 지역: 어두운 구덩이(절벽 조각 뒤에 깔린다)
                    Aethoria.Stages.RoomArt.Rect(parent, "Edge_Pit", new Vector2(e.Pos, c.FloorY - 2.9f), new Vector2(FloorGapHalf * 2f + 0.6f, 6f),
                        new Color(0.03f, 0.025f, 0.04f), EdgeLightOrder);
                }
                if (cliffL != null)
                    RegionPiece(parent, cliffL, e.Pos - FloorGapHalf - m.CliffLLipPx / EdgePx,
                        c.FloorY + 0.03f - (cliffL.bounds.size.y - m.CliffLSurfacePx / EdgePx), false, EdgeDecorOrder, 1f, 1f, tint);
                if (cliffR != null)
                    RegionPiece(parent, cliffR, e.Pos + FloorGapHalf - m.CliffRLipPx / EdgePx,
                        c.FloorY + 0.03f - (cliffR.bounds.size.y - m.CliffRSurfacePx / EdgePx), false, EdgeDecorOrder, 1f, 1f, tint);
            }
        }

        // 숲 빛줄기 그림을 지역 빛 색으로 물들여 쓴다.
        private static Color RegionShaftTint(string region) => region == TreasureChests.Plains ? new Color(1f, 0.95f, 0.72f, 0.55f)
            : region == TreasureChests.Ruins ? new Color(1f, 0.78f, 0.5f, 0.55f) : new Color(0.6f, 0.95f, 1f, 0.55f);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Characters;
using Aethoria.Data;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 어둠의 숲 방(비선형 맵 시범)에 쓰는 장치들. 그림이 아직 없어서 색 사각형·원과 기존 그림을 임시로 쓴다.

    // 임시 그림: 흰 1x1 사각형과 부드러운 원 스프라이트를 만들어 두고 색·크기만 바꿔 쓴다.
    public static class RoomArt
    {
        private static UnityEngine.Sprite square, circle;

        public static UnityEngine.Sprite Square
        {
            get
            {
                if (square != null) return square;
                var t = new Texture2D(4, 4) { filterMode = FilterMode.Bilinear };
                var px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                t.SetPixels(px);
                t.Apply();
                square = UnityEngine.Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                return square;
            }
        }

        public static UnityEngine.Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int size = 64;
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var c = new Vector2((size - 1) / 2f, (size - 1) / 2f);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), c) / (size / 2f);
                        t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 3f)));
                    }
                t.Apply();
                circle = UnityEngine.Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                return circle;
            }
        }

        // 가운데가 center, 크기가 size(유닛)인 색 사각형.
        public static SpriteRenderer Rect(Transform parent, string name, Vector2 center, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = Square;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        public static SpriteRenderer Dot(Transform parent, string name, Vector2 center, float diameter, Color color, int sortingOrder)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = center;
            go.transform.localScale = new Vector3(diameter, diameter, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = Circle;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        // 기존 그림(수정 등)을 높이 height로 맞춰 세운다(그림 피벗이 아래 가운데라는 전제).
        public static SpriteRenderer Picture(Transform parent, string name, string path, Vector2 bottom, float height, Color tint, int sortingOrder)
        {
            var sprite = Resources.Load<UnityEngine.Sprite>(path);
            if (sprite == null) return Dot(parent, name, bottom + Vector2.up * height * 0.5f, height * 0.6f, tint, sortingOrder);
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = bottom;
            float scale = height / sprite.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        // 그림(피벗 아래 가운데)을 bottom에 세운다. width·height가 둘 다 0이면 원래 크기, 하나만 주면 비율 유지,
        // 둘 다 주면 그 칸에 맞춰 늘린다. 그림이 없으면 null(부르는 쪽이 임시 그림을 그린다).
        public static SpriteRenderer Sized(Transform parent, string name, string path, Vector2 bottom, float width, float height, Color tint, int sortingOrder)
        {
            var sprite = Resources.Load<UnityEngine.Sprite>(path);
            if (sprite == null) return null;
            Vector2 size = sprite.bounds.size;
            float sx = 1f, sy = 1f;
            if (width > 0f && height > 0f) { sx = width / size.x; sy = height / size.y; }
            else if (width > 0f) sx = sy = width / size.x;
            else if (height > 0f) sx = sy = height / size.y;
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = bottom;
            go.transform.localScale = new Vector3(sx, sy, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        // 플레이어가 공격(Z)을 눌렀고 이 자리 가까이에 있는가. 공격 판정이 몬스터만 찾기 때문에 장치는 이렇게 맞는다.
        public static bool PlayerStruck(Character player, Vector2 center, Vector2 halfReach)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || player == null || player.IsDead || !keyboard.zKey.wasPressedThisFrame) return false;
            var movement = player.GetComponent<CharacterMovement2D>();
            if (movement != null && movement.IsInputLocked) return false;
            if (ChoicePopup.ConsumesInput || Time.timeScale == 0f) return false;
            Vector2 p = (Vector2)player.transform.position + Vector2.up * 0.9f;
            return Mathf.Abs(p.x - center.x) <= halfReach.x && Mathf.Abs(p.y - center.y) <= halfReach.y;
        }
    }

    // 방 출구: 플레이어 몸이 이 칸에 들어오면 옆 방으로 넘어간다. 들어오자마자 다시 넘어가지 않게, 방이 지어진 뒤
    // 잠깐 기다렸다가 플레이어가 한 번 이 칸 밖에 있는 것을 확인한 다음부터 작동한다. open이 false면 작동하지 않는다.
    public class RoomExit : MonoBehaviour
    {
        private Character player;
        private Collider2D playerCollider;
        private Rect area;
        private System.Action onEnter;
        private System.Func<bool> open;
        private float armTime;
        private bool armed;
        private bool fired;

        public void Initialize(Character target, Rect worldArea, System.Action enter, System.Func<bool> isOpen = null)
        {
            player = target;
            playerCollider = target.GetComponent<Collider2D>();
            area = worldArea;
            onEnter = enter;
            open = isOpen;
            armTime = Time.unscaledTime + 0.35f;
        }

        private bool PlayerInside()
        {
            if (playerCollider == null) return area.Contains(player.transform.position);
            var b = playerCollider.bounds;
            return b.max.x > area.xMin && b.min.x < area.xMax && b.max.y > area.yMin && b.min.y < area.yMax;
        }

        private void Update()
        {
            if (fired || player == null || player.IsDead) return;
            bool inside = PlayerInside();
            if (!armed)
            {
                if (Time.unscaledTime >= armTime && !inside) armed = true;
                return;
            }
            if (!inside || (open != null && !open())) return;
            fired = true;
            onEnter?.Invoke();
        }
    }

    // 공격하면 부서지는 막힌 길(썩은 나뭇가지 등). 부서지면 깃발을 켜서 다음에 와도 부서진 채로 둔다.
    public class BreakableBarrier : MonoBehaviour
    {
        private Character player;
        private string flag;
        private SpriteRenderer visual;
        private Collider2D solid;
        private Vector2 center;
        private Vector2 reach;
        private float breakTime = -1f;
        private string message = "썩은 나뭇가지가 부서졌다!";
        // 부서질 때 튀는 조각 그림(없으면 조각 없이 사라지기만 한다).
        public UnityEngine.Sprite[] Debris;

        public void Initialize(Character target, string flagId, SpriteRenderer sr, Collider2D col, Vector2 worldCenter, Vector2 halfReach,
            string brokenMessage = null)
        {
            if (brokenMessage != null) message = brokenMessage;
            player = target;
            flag = flagId;
            visual = sr;
            solid = col;
            center = worldCenter;
            reach = halfReach;
        }

        private void Update()
        {
            if (breakTime >= 0f)
            {
                float t = (Time.time - breakTime) / 0.35f;
                if (visual != null)
                {
                    var c = visual.color;
                    c.a = 1f - t;
                    visual.color = c;
                    visual.transform.localScale = new Vector3(visual.transform.localScale.x * (1f + Time.deltaTime), visual.transform.localScale.y, 1f);
                }
                if (t >= 1f) Destroy(gameObject);
                return;
            }
            if (!RoomArt.PlayerStruck(player, center, reach)) return;
            WorldFlags.Set(flag);
            if (solid != null) solid.enabled = false;
            breakTime = Time.time;
            if (Debris != null && Debris.Length > 0 && solid != null) DebrisBurst.Spawn(solid.bounds, Debris, 8, transform.parent);
            CameraShake(0.15f, 0.08f);
            ChoicePopup.Toast(message, true);
        }

        private static void CameraShake(float duration, float magnitude)
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.Shake(duration, magnitude);
        }
    }

    // 봉인의 열쇠: 다가가면 줍는다. 깃발(flag)로 저장되고, 모은 개수(prefix로 센다)를 알림 띠로 보여 준다.
    public class KeyPickup : MonoBehaviour
    {
        public const string KeyPrefix = "forest_key_";
        public const int KeysNeeded = 3;

        private Character player;
        private string flag;
        private Vector3 basePosition;
        private SpriteRenderer glow;

        public void Initialize(Character target, string flagId, SpriteRenderer glowRenderer)
        {
            player = target;
            flag = flagId;
            glow = glowRenderer;
            basePosition = transform.position;
        }

        private void Update()
        {
            float t = Time.time;
            transform.position = basePosition + Vector3.up * (0.12f * Mathf.Sin(t * 2.4f));
            if (glow != null) glow.color = new Color(1f, 0.85f, 0.4f, 0.35f + 0.2f * Mathf.Sin(t * 4f));
            if (player == null || player.IsDead) return;
            Vector2 d = (Vector2)(player.transform.position + Vector3.up * 0.9f) - (Vector2)transform.position;
            if (Mathf.Abs(d.x) > 0.9f || Mathf.Abs(d.y) > 1.3f) return;

            WorldFlags.Set(flag);
            int count = WorldFlags.Count(KeyPrefix);
            ChoicePopup.Toast(count >= KeysNeeded ? $"봉인의 열쇠를 모두 모았다! ({count}/{KeysNeeded})" : $"봉인의 열쇠를 얻었다 ({count}/{KeysNeeded})", true, 2f);
            Aethoria.Bootstrap.SfxPlayer.TryPlay(Aethoria.Bootstrap.SfxPlayer.ItemPickup);
            Destroy(gameObject);
        }
    }

    // 보스방 앞 봉인된 철창문: 룬 장식 3개가 열쇠 자리다. 열쇠 3개를 모아 다가가면 연출이 시작된다 —
    // 열쇠가 하나씩 네크로시아에게서 날아가 룬 자리에 박히며 금빛으로 켜지고, 셋 다 켜지면 땅이 울리며 철창이 먼지를 날리며 올라간다.
    // 연출 중에는 조작을 잠근다. 한 번 열리면(깃발) 다음부터는 처음부터 올라가 있다.
    public class KeyGate : MonoBehaviour
    {
        public const string OpenFlag = "forest_gate_open";
        private const float RiseHeight = 5.8f;
        private static readonly Color EmptySocket = new Color(0.08f, 0.06f, 0.1f, 0.88f);
        private static readonly Color LitSocket = new Color(1f, 0.84f, 0.35f, 0.92f);

        private Character player;
        private Collider2D solid;
        private readonly List<SpriteRenderer> sockets = new();
        private readonly List<SpriteRenderer> glows = new();
        private UnityEngine.Sprite keySprite;
        private float nextHint;
        private bool opening;
        private Vector3 closedPosition;

        public void Initialize(Character target, Collider2D col, IEnumerable<SpriteRenderer> socketRenderers, UnityEngine.Sprite key = null)
        {
            player = target;
            solid = col;
            keySprite = key;
            sockets.AddRange(socketRenderers);
            closedPosition = transform.position;
            foreach (var s in sockets)
            {
                // 켜졌을 때 뒤에서 번지는 금빛(처음엔 꺼 둔다)
                var g = RoomArt.Dot(transform, s.name + "Glow", s.transform.position, s.transform.localScale.x * 1.8f, new Color(1f, 0.8f, 0.3f, 0f), s.sortingOrder - 1);
                glows.Add(g);
            }
            bool open = WorldFlags.Has(OpenFlag);
            for (int k = 0; k < sockets.Count; k++) SetSocket(k, open, open ? 0.22f : 0f);
            if (open)
            {
                if (solid != null) solid.enabled = false;
                transform.position = closedPosition + Vector3.up * RiseHeight;
            }
        }

        private void SetSocket(int k, bool lit, float glow)
        {
            sockets[k].color = lit ? LitSocket : EmptySocket;
            var c = glows[k].color;
            c.a = glow;
            glows[k].color = c;
        }

        private void Update()
        {
            if (opening || WorldFlags.Has(OpenFlag) || player == null || player.IsDead) return;
            float dist = Mathf.Abs(player.transform.position.x - closedPosition.x);
            if (dist > 2.6f) return;
            int keys = WorldFlags.Count(KeyPickup.KeyPrefix);
            if (keys >= KeyPickup.KeysNeeded)
            {
                opening = true;
                StartCoroutine(OpenSequence());
            }
            else if (Time.time >= nextHint)
            {
                nextHint = Time.time + 4f;
                ChoicePopup.Toast($"봉인된 철창이다. 봉인의 열쇠가 필요하다 ({keys}/{KeyPickup.KeysNeeded})", false, 2f);
            }
        }

        private System.Collections.IEnumerator OpenSequence()
        {
            WorldFlags.Set(OpenFlag);
            var move = player.GetComponent<CharacterMovement2D>();
            if (move != null) move.SetCutsceneLocked(true);
            player.SetCombatLocked(true);
            player.SetInvincible(true);   // 연출 중에 몬스터에게 맞지 않게
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            if (move != null) move.Face(closedPosition.x > player.transform.position.x ? Vector2.right : Vector2.left);
            ChoicePopup.Toast("봉인의 열쇠가 철창에 반응한다...", true, 1.8f);
            yield return new WaitForSeconds(0.5f);

            // 1) 열쇠가 하나씩 날아가 룬 자리에 박힌다(아래 자리부터).
            for (int k = 0; k < sockets.Count; k++)
            {
                Vector3 from = player.transform.position + new Vector3(0f, 1.3f, 0f);
                yield return FlyKey(from, sockets[k].transform.position, k);
                SetSocket(k, true, 0.4f);
                PuffFx.Spawn(transform, sockets[k].transform.position, 0.3f, 1.4f, new Color(1f, 0.85f, 0.4f, 0.7f), 0.35f, 0f, 6);
                Aethoria.Bootstrap.SfxPlayer.TryPlay(Aethoria.Bootstrap.SfxPlayer.ItemPickup);
                Shake(0.15f, 0.06f);
                yield return new WaitForSeconds(0.28f);
            }

            // 2) 세 룬이 함께 맥박치듯 빛난다.
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                float pulse = 0.25f + 0.25f * Mathf.Abs(Mathf.Sin(t * 9f));
                for (int k = 0; k < sockets.Count; k++) SetSocket(k, true, pulse);
                yield return null;
            }
            ChoicePopup.Toast("봉인이 풀렸다. 철창이 열린다!", true, 2f);

            // 3) 땅이 울리며 철창이 올라간다. 처음엔 덜컹거리며 느리게, 점점 빨라진다. 바닥에서 먼지가 피어오른다.
            Shake(2.0f, 0.1f);
            const float riseTime = 2.0f;
            float nextDust = 0f;
            for (float t = 0f; t < riseTime; t += Time.deltaTime)
            {
                float u = t / riseTime;
                float eased = u * u * (3f - 2f * u);
                float jitter = (1f - u) * 0.04f * Mathf.Sin(t * 60f);
                transform.position = closedPosition + new Vector3(jitter, RiseHeight * eased, 0f);
                if (solid != null && u > 0.45f) solid.enabled = false;
                for (int k = 0; k < sockets.Count; k++) SetSocket(k, true, 0.22f + 0.15f * (1f - u));
                if (t >= nextDust && u < 0.8f)
                {
                    nextDust = t + 0.09f;
                    var at = new Vector3(closedPosition.x + Random.Range(-1.1f, 1.1f), closedPosition.y - 2.6f + Random.Range(0f, 0.3f), 0f);
                    PuffFx.Spawn(transform.parent, at, Random.Range(0.5f, 0.9f), Random.Range(1.6f, 2.4f), new Color(0.55f, 0.5f, 0.6f, 0.55f), Random.Range(0.6f, 0.9f), Random.Range(0.4f, 0.9f), 9);
                }
                yield return null;
            }
            transform.position = closedPosition + Vector3.up * RiseHeight;
            if (solid != null) solid.enabled = false;
            for (int k = 0; k < sockets.Count; k++) SetSocket(k, true, 0.22f);

            yield return new WaitForSeconds(0.25f);
            if (player != null)
            {
                if (move != null) move.SetCutsceneLocked(false);
                player.SetCombatLocked(false);
                player.SetInvincible(false);
            }
        }

        // 열쇠 하나가 포물선을 그리며 빙글 돌아 룬 자리로 날아가 작아지며 박힌다.
        private System.Collections.IEnumerator FlyKey(Vector3 from, Vector3 to, int index)
        {
            SpriteRenderer key;
            if (keySprite != null)
            {
                var go = new GameObject("FlyingKey" + index, typeof(SpriteRenderer));
                go.transform.SetParent(transform.parent);
                key = go.GetComponent<SpriteRenderer>();
                key.sprite = keySprite;
                key.sortingOrder = 20;
            }
            else key = RoomArt.Dot(transform.parent, "FlyingKey" + index, from, 0.5f, new Color(1f, 0.85f, 0.4f), 20);
            var glow = RoomArt.Dot(key.transform, "Glow", from, 1.4f, new Color(1f, 0.85f, 0.4f, 0.45f), 19);
            float baseScale = key.transform.localScale.x;
            const float duration = 0.6f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = t / duration;
                float e = u * u * (3f - 2f * u);
                Vector3 p = Vector3.Lerp(from, to, e) + Vector3.up * (1.4f * Mathf.Sin(u * Mathf.PI));
                key.transform.position = p - Vector3.up * (key.bounds.size.y * 0.5f * (keySprite != null ? 1f : 0f));
                key.transform.rotation = Quaternion.Euler(0f, 0f, -360f * e);
                float sc = Mathf.Lerp(1f, 0.55f, e) * baseScale;
                key.transform.localScale = new Vector3(sc, sc, 1f);
                yield return null;
            }
            Destroy(key.gameObject);
            if (glow != null) Destroy(glow.gameObject);
        }

        private static void Shake(float duration, float magnitude)
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.Shake(duration, magnitude);
        }
    }

    // 퍼져 나가며 사라지는 원(먼지·빛 번짐).
    public class PuffFx : MonoBehaviour
    {
        private SpriteRenderer sr;
        private float start, life, size0, size1, rise;
        private Color color;

        public static void Spawn(Transform parent, Vector3 at, float fromSize, float toSize, Color color, float duration, float riseSpeed, int order)
        {
            var dot = RoomArt.Dot(parent, "Puff", at, fromSize, color, order);
            var p = dot.gameObject.AddComponent<PuffFx>();
            p.sr = dot;
            p.start = Time.time;
            p.life = duration;
            p.size0 = fromSize;
            p.size1 = toSize;
            p.rise = riseSpeed;
            p.color = color;
        }

        private void Update()
        {
            float t = (Time.time - start) / life;
            if (t >= 1f) { Destroy(gameObject); return; }
            float s = Mathf.Lerp(size0, size1, 1f - (1f - t) * (1f - t));
            transform.localScale = new Vector3(s, s, 1f);
            transform.position += Vector3.up * (rise * Time.deltaTime);
            sr.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
        }
    }

    // 수정 스위치 퍼즐: 벽화에 그려진 순서대로 수정을 치면(공격) 풀린다. 틀리면 모두 꺼지고 처음부터.
    public class CrystalPuzzle : MonoBehaviour
    {
        public const string SolvedFlag = "forest_puzzle_solved";

        private Character player;
        private string solvedFlag = SolvedFlag;
        private string solvedMessage = "수정이 공명한다... 철창이 열렸다!";
        private string failMessage = "수정의 빛이 꺼졌다. 벽화를 다시 살펴보자.";
        private readonly List<SpriteRenderer> crystals = new();
        private readonly List<Color> colors = new();
        private int[] order;
        private int progress;
        private readonly List<int> lit = new();
        private System.Action onSolved;
        private float failFlash = -10f;

        public void Initialize(Character target, IList<SpriteRenderer> crystalRenderers, IList<Color> crystalColors, int[] solveOrder, System.Action solved,
            string flag = null, string solvedText = null, string failText = null)
        {
            player = target;
            if (flag != null) solvedFlag = flag;
            if (solvedText != null) solvedMessage = solvedText;
            if (failText != null) failMessage = failText;
            crystals.AddRange(crystalRenderers);
            colors.AddRange(crystalColors);
            order = solveOrder;
            onSolved = solved;
            if (WorldFlags.Has(solvedFlag)) for (int i = 0; i < crystals.Count; i++) lit.Add(i);
            Refresh();
        }

        private void Refresh()
        {
            bool flashing = Time.time - failFlash < 0.35f;
            for (int i = 0; i < crystals.Count; i++)
            {
                var c = colors[i];
                crystals[i].color = flashing ? new Color(1f, 0.25f, 0.25f) : lit.Contains(i) ? c : new Color(c.r * 0.3f + 0.15f, c.g * 0.3f + 0.15f, c.b * 0.3f + 0.15f);
            }
        }

        private void Update()
        {
            Refresh();
            if (WorldFlags.Has(solvedFlag)) return;
            for (int i = 0; i < crystals.Count; i++)
            {
                Vector2 center = (Vector2)crystals[i].transform.position + Vector2.up * 0.8f;
                if (!RoomArt.PlayerStruck(player, center, new Vector2(1.4f, 1.6f))) continue;
                Hit(i);
                break;
            }
        }

        private void Hit(int index)
        {
            if (lit.Contains(index)) return;
            if (order[progress] == index)
            {
                lit.Add(index);
                progress++;
                if (progress >= order.Length)
                {
                    WorldFlags.Set(solvedFlag);
                    ChoicePopup.Toast(solvedMessage, true, 2f);
                    onSolved?.Invoke();
                }
            }
            else
            {
                lit.Clear();
                progress = 0;
                failFlash = Time.time;
                ChoicePopup.Toast(failMessage, false, 1.6f);
            }
        }
    }

    // 정예 몬스터: 애니메이션이 그림을 바꿔도 붉은 빛이 계속 남게 매 프레임 색을 덮는다.
    public class EliteTint : MonoBehaviour
    {
        private SpriteRenderer target;
        public void Initialize(SpriteRenderer sr) => target = sr;
        private void LateUpdate()
        {
            if (target == null) return;
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
            target.color = new Color(1f, 0.45f * pulse + 0.1f, 0.45f * pulse + 0.1f, target.color.a);
        }
    }
}

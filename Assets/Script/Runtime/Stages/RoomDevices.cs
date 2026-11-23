using System.Collections.Generic;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.Data;
using Aethoria.Monsters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 평야·폐허·바다 방에 쓰는 장치들(어둠의 숲 장치는 ForestRoomProps). 그림은 아직 임시(색 사각형·원)다.

    // 전멸방: 플레이어가 방 안으로 들어서면 양쪽 문이 내려와 잠기고, 몬스터가 웨이브로 나온다.
    // 모든 웨이브를 잡으면 문이 올라가고 깃발이 켜져서 다음부터는 열린 채로 둔다.
    public class ArenaLock : MonoBehaviour
    {
        private Character player;
        private string flag;
        private float triggerMinX, triggerMaxX;
        private readonly List<Transform> gates = new();
        private readonly List<Vector3> gateClosed = new();
        private List<System.Action<Transform>> waves;
        private System.Action onCleared;
        private Transform waveRoot;

        private enum State { Waiting, Closing, Fighting, Between, Opening, Done }
        private State state;
        private int waveIndex;
        private float stateTime;
        private readonly List<Monster> alive = new();
        private const float GateTravel = 5f;

        public void Initialize(Character target, string clearedFlag, float minX, float maxX, IEnumerable<Transform> gateObjects,
            List<System.Action<Transform>> waveSpawners, System.Action cleared)
        {
            player = target;
            flag = clearedFlag;
            triggerMinX = minX;
            triggerMaxX = maxX;
            waves = waveSpawners;
            onCleared = cleared;
            foreach (var g in gateObjects)
            {
                gates.Add(g);
                gateClosed.Add(g.position);
                g.position += Vector3.up * GateTravel;   // 처음엔 위로 걷혀 있다
                SetGateSolid(g, false);
            }
            state = WorldFlags.Has(flag) ? State.Done : State.Waiting;
            waveRoot = new GameObject("ArenaWaves").transform;
            waveRoot.SetParent(transform);
        }

        private static void SetGateSolid(Transform g, bool solid)
        {
            foreach (var c in g.GetComponentsInChildren<Collider2D>()) c.enabled = solid;
        }

        private void MoveGates(float closedAmount)
        {
            for (int i = 0; i < gates.Count; i++)
                if (gates[i] != null) gates[i].position = gateClosed[i] + Vector3.up * (GateTravel * (1f - closedAmount));
        }

        private void Update()
        {
            if (player == null) return;
            float t = Time.time - stateTime;
            switch (state)
            {
                case State.Waiting:
                {
                    float x = player.transform.position.x;
                    if (player.IsDead || x < triggerMinX || x > triggerMaxX) return;
                    state = State.Closing;
                    stateTime = Time.time;
                    foreach (var g in gates) SetGateSolid(g, true);
                    Shake(0.4f, 0.12f);
                    ChoicePopup.Toast("문이 닫혔다! 모든 적을 쓰러뜨려라.", false, 2f);
                    break;
                }
                case State.Closing:
                    MoveGates(Mathf.Clamp01(t / 0.35f));
                    if (t >= 0.9f) SpawnWave();
                    break;
                case State.Fighting:
                    alive.RemoveAll(m => m == null || m.IsDead);
                    if (alive.Count > 0) return;
                    if (waveIndex >= waves.Count)
                    {
                        state = State.Opening;
                        stateTime = Time.time;
                        WorldFlags.Set(flag);
                        Shake(0.5f, 0.08f);
                        ChoicePopup.Toast("모든 적을 물리쳤다! 문이 열린다.", true, 2.2f);
                        Aethoria.Bootstrap.SfxPlayer.TryPlay(Aethoria.Bootstrap.SfxPlayer.ItemPickup);
                        onCleared?.Invoke();
                    }
                    else
                    {
                        state = State.Between;
                        stateTime = Time.time;
                        ChoicePopup.Toast($"다음 무리가 온다! ({waveIndex + 1}/{waves.Count})", false, 1.4f);
                    }
                    break;
                case State.Between:
                    if (t >= 1.2f) SpawnWave();
                    break;
                case State.Opening:
                    MoveGates(1f - Mathf.Clamp01(t / 1.0f));
                    if (t >= 1f)
                    {
                        foreach (var g in gates) SetGateSolid(g, false);
                        state = State.Done;
                    }
                    break;
            }
        }

        private void SpawnWave()
        {
            var holder = new GameObject("Wave" + waveIndex).transform;
            holder.SetParent(waveRoot);
            waves[waveIndex]?.Invoke(holder);
            waveIndex++;
            var manager = FindFirstObjectByType<StageManager>();
            foreach (var m in holder.GetComponentsInChildren<Monster>())
            {
                alive.Add(m);
                manager?.Track(m);
                SummonFlash.Spawn(m.transform.position + Vector3.up * 0.8f, transform);
            }
            state = State.Fighting;
            stateTime = Time.time;
        }

        private static void Shake(float duration, float magnitude)
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.Shake(duration, magnitude);
        }
    }

    // 벽·바위가 부서질 때 튀어 나가는 조각들: 위로 흩어졌다가 떨어지며 돌고 사라진다.
    public class DebrisBurst : MonoBehaviour
    {
        private Vector2 velocity;
        private float spin, start;
        private SpriteRenderer sr;

        public static void Spawn(Bounds area, UnityEngine.Sprite[] sprites, int count, Transform parent)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Debris", typeof(SpriteRenderer));
                go.transform.SetParent(parent);
                go.transform.position = new Vector3(Random.Range(area.min.x, area.max.x), Random.Range(area.min.y, area.max.y), 0f);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprites[Random.Range(0, sprites.Length)];
                sr.sortingOrder = 8;
                var d = go.AddComponent<DebrisBurst>();
                d.sr = sr;
                d.velocity = new Vector2(Random.Range(-3.5f, 3.5f), Random.Range(2f, 6f));
                d.spin = Random.Range(-540f, 540f);
                d.start = Time.time;
                go.transform.localScale = Vector3.one * Random.Range(0.7f, 1.2f);
            }
        }

        private void Update()
        {
            float t = (Time.time - start) / 1.1f;
            if (t >= 1f) { Destroy(gameObject); return; }
            velocity.y -= 18f * Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.Rotate(0f, 0f, spin * Time.deltaTime);
            var c = sr.color;
            c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            sr.color = c;
        }
    }

    // 몬스터가 소환될 때 번쩍이는 원(임시 연출).
    public class SummonFlash : MonoBehaviour
    {
        private SpriteRenderer sr;
        private float start;

        public static void Spawn(Vector3 position, Transform parent)
        {
            var dot = RoomArt.Dot(parent, "SummonFlash", position, 2.4f, new Color(0.8f, 0.6f, 1f, 0.8f), 20);
            var flash = dot.gameObject.AddComponent<SummonFlash>();
            flash.sr = dot;
            flash.start = Time.time;
        }

        private void Update()
        {
            float t = (Time.time - start) / 0.5f;
            if (t >= 1f) { Destroy(gameObject); return; }
            transform.localScale = Vector3.one * (2.4f * (1f + t));
            var c = sr.color;
            c.a = 0.8f * (1f - t);
            sr.color = c;
        }
    }

    // 시간 제한 스위치: 처음 하나를 치면 시간이 흐르기 시작하고, 시간 안에 모든 스위치를 쳐야 풀린다. 늦으면 모두 꺼진다.
    // 스위치는 일부러 서로 멀리, 높낮이 다르게 둬서 발판을 빠르게 타야 한다.
    public class TimedSwitchPuzzle : MonoBehaviour
    {
        private Character player;
        private string flag;
        private readonly List<SpriteRenderer> switches = new();
        private readonly List<bool> lit = new();
        private Color onColor, offColor;
        private float timeLimit;
        private float startTime = -1f;
        private System.Action onSolved;
        private string solvedMessage;
        private string switchName;
        private UnityEngine.Sprite onSprite, offSprite;   // 있으면 색 대신 그림을 바꿔 켜고 끈다

        public void SetSprites(UnityEngine.Sprite on, UnityEngine.Sprite off)
        {
            onSprite = on;
            offSprite = off;
        }

        public void Initialize(Character target, IList<SpriteRenderer> switchRenderers, Color on, Color off, float seconds, string solvedFlag,
            System.Action solved, string name, string solvedText)
        {
            player = target;
            switches.AddRange(switchRenderers);
            foreach (var _ in switches) lit.Add(false);
            onColor = on;
            offColor = off;
            timeLimit = seconds;
            flag = solvedFlag;
            onSolved = solved;
            switchName = name;
            solvedMessage = solvedText;
            bool solvedAlready = WorldFlags.Has(flag);
            for (int i = 0; i < lit.Count; i++) lit[i] = solvedAlready;
        }

        private void Update()
        {
            for (int i = 0; i < switches.Count; i++)
            {
                float pulse = lit[i] && !WorldFlags.Has(flag) ? 0.85f + 0.15f * Mathf.Sin(Time.time * 10f) : 1f;
                if (onSprite != null && offSprite != null)
                {
                    switches[i].sprite = lit[i] ? onSprite : offSprite;
                    switches[i].color = new Color(pulse, pulse, pulse, 1f);
                    continue;
                }
                var c = lit[i] ? onColor : offColor;
                switches[i].color = new Color(c.r * pulse, c.g * pulse, c.b * pulse, c.a);
            }
            if (WorldFlags.Has(flag)) return;

            if (startTime >= 0f && Time.time - startTime > timeLimit)
            {
                for (int i = 0; i < lit.Count; i++) lit[i] = false;
                startTime = -1f;
                ChoicePopup.Toast($"{switchName}의 빛이 꺼졌다. 더 빨리 움직여야 한다!", false, 1.8f);
                return;
            }

            for (int i = 0; i < switches.Count; i++)
            {
                if (lit[i]) continue;
                Vector2 center = switches[i].bounds.center;
                if (!RoomArt.PlayerStruck(player, center, new Vector2(1.4f, 1.5f))) continue;
                lit[i] = true;
                if (startTime < 0f) startTime = Time.time;
                int count = 0;
                foreach (bool b in lit) if (b) count++;
                Aethoria.Bootstrap.SfxPlayer.TryPlay(Aethoria.Bootstrap.SfxPlayer.ItemPickup);
                if (count >= lit.Count)
                {
                    WorldFlags.Set(flag);
                    ChoicePopup.Toast(solvedMessage, true, 2f);
                    onSolved?.Invoke();
                }
                else
                {
                    float left = timeLimit - (Time.time - startTime);
                    ChoicePopup.Toast($"{switchName} {count}/{lit.Count} — 남은 시간 {left:0.0}초", true, 1.2f);
                }
                break;
            }
        }
    }
}

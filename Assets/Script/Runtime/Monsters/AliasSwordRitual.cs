using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Data;

namespace Aethoria.Monsters
{
    // 알리아스의 종막 패턴 "검의 의식"(BossAI.FinaleRoutine이 만든다). 땅에 검을 꽂은 알리아스 발밑에 커다란 마법진이 열리고,
    // 맵의 발판마다 하늘에서 마검이 하나씩 떨어져 꽂힌다(발판 위 작은 마법진 → 마검 낙하 → 충격).
    // 마검마다 체력이 있어 제한 시간 안에 모두 부수면 의식이 깨지고(성공), 하나라도 남기면(실패)
    //   마검이 꽂혀 있던 발판이 모두 산산이 부서지고 → 마법진에서 뻗어 나온 사슬이 네크로시아를 옭아매고 →
    //   보랏빛 참격이 사방에서 무차별로 쏟아진 뒤 → 마지막 일격에 즉사한다.
    // 마검은 Monster라서 플레이어의 모든 공격/스킬에 맞지만, 경험치/드랍/퀘스트/도감에는 잡히지 않는다(CountsAsKill=false).
    public class AliasSwordRitual : MonoBehaviour
    {
        public const string SwordPath = "Art/Effects/AliasRitualSword";
        public const string CirclePath = "Art/Effects/AliasMagicCircle";
        public const string ChainPath = "Art/Effects/AliasChainBind";
        public const string SlashPath = "Art/Effects/AliasRitualSlash";

        public struct Settings
        {
            public int swordCount;
            public float swordHp;
            public float timeLimit;
            public float bossCircleScale;     // 알리아스 발밑 마법진(지름 4 x 배율)
            public float platformCircleScale; // 마검 밑 마법진
            public float swordFallHeight;     // 마검이 떨어지기 시작하는 높이(꽂히는 자리 위로)
            public float swordFallTime;
            public float swordDropInterval;
            public int executionSlashes;      // 실패했을 때 쏟아지는 참격 수(마지막 일격 제외)
            public float executionDuration;
            public float executionSlashDamage; // 참격 한 번마다 깎는 체력 비율(연출용, 마지막 일격이 즉사)
        }

        // 바닥에 눕힌 마법진: 원화를 세로로 이만큼 눌러 땅 위에 비스듬히 깔린 것처럼 보이게 한다.
        private const float CircleSquash = 0.32f;
        private const float CircleFrameTime = 0.07f;

        public bool Finished { get; private set; }
        public bool Succeeded { get; private set; }
        // 마검이 모두 꽂혀 시간이 흐르기 시작하는 순간(BossAI가 이때 플레이어 조작을 풀어 준다).
        public event System.Action OnSwordsLanded;

        private Settings settings;
        private Character player;
        private float groundY;
        private Vector2 bossFeet;
        private Sprite[] circleFrames;
        private readonly List<Monster> swords = new List<Monster>();
        private readonly List<GameObject> platforms = new List<GameObject>();
        private readonly List<GameObject> effects = new List<GameObject>();
        private Text timerText;
        private GameObject timerCanvas;

        public static AliasSwordRitual Begin(Vector2 bossFeet, float groundY, Character player, Settings settings)
        {
            var go = new GameObject("AliasSwordRitual");
            var ritual = go.AddComponent<AliasSwordRitual>();
            ritual.bossFeet = bossFeet;
            ritual.groundY = groundY;
            ritual.player = player;
            ritual.settings = settings;
            return ritual;
        }

        public IEnumerator Run()
        {
            circleFrames = LoadSorted(CirclePath);
            var cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;

            // 1) 알리아스 발밑에 커다란 마법진이 열린다.
            if (cameraFollow != null) cameraFollow.Shake(0.6f, 0.15f);
            yield return OpenCircle(bossFeet, settings.bossCircleScale, sortingOrder: -2);
            yield return new WaitForSeconds(0.3f);

            // 2) 발판마다 작은 마법진이 열리고, 하늘에서 마검이 떨어져 꽂힌다.
            var spots = PickSwordSpots();
            var swordFrames = LoadSorted(SwordPath);
            for (int i = 0; i < spots.Count; i++)
            {
                StartCoroutine(OpenCircle(spots[i], settings.platformCircleScale, sortingOrder: 1));
                StartCoroutine(DropSword(spots[i], swordFrames.Length > 0 ? swordFrames[i % swordFrames.Length] : null, i));
                yield return new WaitForSeconds(settings.swordDropInterval);
            }
            yield return new WaitForSeconds(settings.swordFallTime + 0.2f);
            OnSwordsLanded?.Invoke();

            // 3) 제한 시간 안에 마검을 모두 부숴야 한다.
            BuildTimerUi();
            float remaining = settings.timeLimit;
            while (remaining > 0f && AliveSwordCount() > 0)
            {
                if (player == null || player.IsDead) break;
                remaining -= Time.deltaTime;
                UpdateTimerUi(remaining);
                yield return null;
            }
            Destroy(timerCanvas);

            if (player == null || player.IsDead)
            {
                Cleanup();
                Finished = true;
                yield break;
            }

            if (AliveSwordCount() == 0)
            {
                // 성공: 의식이 깨지며 마법진이 흩어진다.
                Succeeded = true;
                if (cameraFollow != null) cameraFollow.Shake(0.4f, 0.2f);
                yield return FadeEffects(0.5f);
                Cleanup();
                Finished = true;
                yield break;
            }

            // 실패: 처형.
            yield return Execute(cameraFollow);
            Cleanup();
            Finished = true;
        }

        // ---- 마검 ----

        // 보스 맵의 발판(한쪽 방향 발판)들 윗면 가운데. 발판이 모자라면 맵 바닥에 고르게 나눠 꽂는다.
        private List<Vector2> PickSwordSpots()
        {
            var spots = new List<Vector2>();
            var found = new List<(float x, Vector2 top, GameObject go)>();
            foreach (var effector in Object.FindObjectsByType<PlatformEffector2D>())
            {
                var col = effector.GetComponent<Collider2D>();
                if (col == null || !col.enabled) continue;
                Bounds b = col.bounds;
                found.Add((b.center.x, new Vector2(b.center.x, b.max.y), effector.gameObject));
            }
            found.Sort((a, b) => a.x.CompareTo(b.x));
            // 발판이 많으면 고르게 골라 쓴다.
            for (int i = 0; i < settings.swordCount && found.Count > 0; i++)
            {
                int index = settings.swordCount == 1 ? 0 : Mathf.RoundToInt(i * (found.Count - 1f) / (settings.swordCount - 1f));
                index = Mathf.Clamp(index, 0, found.Count - 1);
                if (platforms.Contains(found[index].go)) continue;
                spots.Add(found[index].top);
                platforms.Add(found[index].go);
            }
            Rect map = GameBootstrap.CurrentMapBounds;
            for (int i = spots.Count; i < settings.swordCount; i++)
            {
                float x = Mathf.Lerp(map.xMin + 2f, map.xMax - 2f, (i + 0.5f) / settings.swordCount);
                spots.Add(new Vector2(x, groundY));
            }
            return spots;
        }

        private IEnumerator DropSword(Vector2 landing, Sprite sprite, int index)
        {
            if (sprite == null) yield break;
            var go = new GameObject("AliasRitualSword");
            go.transform.position = landing + Vector2.up * settings.swordFallHeight;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 3;
            effects.Add(go);

            // 떨어지는 동안은 맞지 않는다(꽂힌 뒤에 부순다). 점점 빨라지며 떨어진다.
            Vector2 from = go.transform.position;
            for (float t = 0f; t < settings.swordFallTime; t += Time.deltaTime)
            {
                if (go == null) yield break;
                float k = t / settings.swordFallTime;
                go.transform.position = Vector2.Lerp(from, landing, k * k);
                yield return null;
            }
            if (go == null) yield break;
            go.transform.position = landing;
            var cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cameraFollow != null) cameraFollow.Shake(0.2f, 0.12f);
            ShockwaveBurst.Play(this, landing, null, new ShockwaveBurst.Settings
            {
                rings = 2, interval = 0.08f, duration = 0.45f, maxSize = 2.6f, squash = 0.35f,
                ringColor = new Color(0.75f, 0.45f, 1f, 0.85f), flashColor = new Color(0f, 0f, 0f, 0f), sortingOrder = 4,
            });

            // 꽂힌 마검은 체력이 있는 부술 대상이 된다.
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "마검";
            data.maxHp = settings.swordHp;
            data.defense = 0f;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            float height = sprite.bounds.size.y;
            collider.size = new Vector2(sprite.bounds.size.x * 0.45f, height * 0.85f);
            // 기준점이 키의 12% 위라 그림은 기준점 아래 0.12h ~ 위 0.88h에 걸쳐 있다. 그 한가운데(0.38h)에 맞춘다.
            collider.offset = new Vector2(0f, height * 0.38f);
            var monster = go.AddComponent<Monster>();
            monster.AssignData(data);
            monster.CountsAsKill = false;
            monster.OnDied += m => SwordBroken(m, renderer);
            go.AddComponent<MonsterHealthBar>().Configure(newWidth: 0.9f, newVerticalOffset: -(height * 0.9f));
            swords.Add(monster);
        }

        // 부서진 마검: 보랏빛으로 번쩍이며 조각난 듯 위로 튀었다가 흩어진다(Monster는 바로 지워지므로 그림만 복사해 남긴다).
        private void SwordBroken(Monster sword, SpriteRenderer source)
        {
            if (source == null) return;
            var go = new GameObject("BrokenSword");
            go.transform.position = source.transform.position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.sortingOrder = source.sortingOrder;
            effects.Add(go);
            StartCoroutine(ShatterRoutine(go, renderer));
            ShockwaveBurst.Play(this, (Vector2)source.transform.position + Vector2.up * 1f, null, new ShockwaveBurst.Settings
            {
                rings = 2, interval = 0.07f, duration = 0.4f, maxSize = 2.2f, squash = 0.9f,
                ringColor = new Color(0.85f, 0.6f, 1f, 0.9f), flashColor = new Color(0f, 0f, 0f, 0f), sortingOrder = 5,
            });
        }

        private IEnumerator ShatterRoutine(GameObject go, SpriteRenderer renderer)
        {
            Vector3 start = go.transform.position;
            float spin = Random.value < 0.5f ? -1f : 1f;
            for (float t = 0f; t < 0.45f && go != null; t += Time.deltaTime)
            {
                float k = t / 0.45f;
                go.transform.position = start + new Vector3(spin * 0.4f * k, 0.6f * k - 1.2f * k * k, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, spin * 50f * k);
                renderer.color = new Color(1f, 0.8f + 0.2f * k, 1f, 1f - k);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        private int AliveSwordCount()
        {
            int n = 0;
            foreach (var s in swords) if (s != null && !s.IsDead) n++;
            return n;
        }

        // ---- 마법진 ----

        // 마법진이 점점 커지며 열리고(6프레임), 다 열리면 마지막 장으로 남아 은은하게 맥동한다.
        private IEnumerator OpenCircle(Vector2 center, float scale, int sortingOrder)
        {
            if (circleFrames == null || circleFrames.Length == 0) yield break;
            var go = new GameObject("AliasMagicCircle");
            go.transform.position = center;
            go.transform.localScale = new Vector3(scale, scale * CircleSquash, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            effects.Add(go);
            foreach (var frame in circleFrames)
            {
                if (go == null) yield break;
                renderer.sprite = frame;
                yield return new WaitForSeconds(CircleFrameTime);
            }
            StartCoroutine(PulseCircle(go, renderer));
        }

        private IEnumerator PulseCircle(GameObject go, SpriteRenderer renderer)
        {
            for (float t = 0f; go != null && !fading; t += Time.deltaTime)
            {
                renderer.color = new Color(1f, 1f, 1f, 0.8f + 0.2f * Mathf.Sin(t * 5f));
                yield return null;
            }
        }

        private bool fading;

        private IEnumerator FadeEffects(float duration)
        {
            fading = true;
            var renderers = new List<SpriteRenderer>();
            foreach (var e in effects) if (e != null) renderers.AddRange(e.GetComponentsInChildren<SpriteRenderer>());
            var startAlpha = new float[renderers.Count];
            for (int i = 0; i < renderers.Count; i++) startAlpha[i] = renderers[i].color.a;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                for (int i = 0; i < renderers.Count; i++)
                {
                    if (renderers[i] == null) continue;
                    Color c = renderers[i].color;
                    renderers[i].color = new Color(c.r, c.g, c.b, startAlpha[i] * (1f - t / duration));
                }
                yield return null;
            }
        }

        // ---- 실패: 처형 ----

        private IEnumerator Execute(CameraFollow cameraFollow)
        {
            // 남은 마검이 보랏빛으로 타오르며 꽂혀 있던 발판을 모두 부순다.
            if (cameraFollow != null) cameraFollow.Shake(0.9f, 0.35f);
            foreach (var s in swords) if (s != null) Destroy(s.gameObject);
            foreach (var platform in platforms) if (platform != null) StartCoroutine(BreakPlatform(platform));
            yield return new WaitForSeconds(0.8f);

            if (player == null || player.IsDead) yield break;
            var movement = player.GetComponent<CharacterMovement2D>();
            var body = player.GetComponent<Rigidbody2D>();
            if (movement != null) movement.SetCutsceneLocked(true);
            player.SetInvincible(false);

            // 바닥으로 떨어질 때까지 잠깐 기다렸다가(발판이 무너져 공중에 있었다면) 사슬이 감는다.
            for (float t = 0f; t < 0.6f && movement != null && !movement.IsGrounded; t += Time.deltaTime) yield return null;
            RigidbodyType2D savedType = body != null ? body.bodyType : RigidbodyType2D.Dynamic;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.bodyType = RigidbodyType2D.Kinematic;
            }

            // 사슬이 날아들어 몸을 감는다(6프레임 → 마지막 두 장 반복).
            var chainFrames = LoadSorted(ChainPath);
            GameObject chain = null;
            if (chainFrames.Length > 0)
            {
                chain = new GameObject("AliasChainBind");
                chain.transform.position = player.transform.position;
                var chainRenderer = chain.AddComponent<SpriteRenderer>();
                chainRenderer.sortingOrder = 6;
                effects.Add(chain);
                for (int i = 0; i < chainFrames.Length; i++)
                {
                    chainRenderer.sprite = chainFrames[i];
                    yield return new WaitForSeconds(0.08f);
                }
                StartCoroutine(LoopFrames(chainRenderer, new[] { chainFrames[chainFrames.Length - 2], chainFrames[chainFrames.Length - 1] }, 0.1f));
            }
            if (cameraFollow != null) cameraFollow.Shake(0.25f, 0.15f);
            yield return new WaitForSeconds(0.35f);

            // 사방에서 참격이 무차별로 쏟아진다.
            var slashFrames = LoadSorted(SlashPath);
            Vector2 center = (Vector2)player.transform.position + Vector2.up * 0.75f;
            float gap = settings.executionDuration / Mathf.Max(1, settings.executionSlashes);
            for (int i = 0; i < settings.executionSlashes; i++)
            {
                if (player == null) break;
                Vector2 at = center + Random.insideUnitCircle * 0.6f;
                StartCoroutine(PlaySlash(slashFrames, at, Random.Range(0f, 360f), Random.Range(0.8f, 1.25f), 0.035f));
                if (!player.IsDead && player.CurrentHp > player.MaxHp * settings.executionSlashDamage * 1.5f)
                    player.TakeDamage(player.MaxHp * settings.executionSlashDamage, at, hitReaction: false);
                if (cameraFollow != null && i % 3 == 0) cameraFollow.Shake(0.12f, 0.12f);
                yield return new WaitForSeconds(gap * Random.Range(0.6f, 1.4f));
            }

            // 마지막 일격: 화면을 가르는 커다란 참격과 함께 즉사.
            yield return new WaitForSeconds(0.25f);
            StartCoroutine(PlaySlash(slashFrames, center, -35f, 2.6f, 0.05f));
            if (cameraFollow != null) cameraFollow.Shake(0.6f, 0.45f);
            ShockwaveBurst.Play(this, center, null, new ShockwaveBurst.Settings
            {
                rings = 3, interval = 0.06f, duration = 0.5f, maxSize = 7f, squash = 1f,
                ringColor = new Color(0.85f, 0.55f, 1f, 0.9f), flashColor = new Color(1f, 0.9f, 1f, 0.6f), flashDuration = 0.35f, sortingOrder = 55,
            });
            yield return new WaitForSeconds(0.1f);

            if (body != null) body.bodyType = savedType;
            if (player != null && !player.IsDead)
            {
                player.SetInvincible(false);
                player.TakeDamage(player.MaxHp * 100f, center, hitReaction: false);
            }
            if (chain != null) Destroy(chain);
            // 관리자 무적(GodMode)으로 살아남았으면 조작을 돌려준다(쓰러졌으면 사망 연출이 넘겨받는다).
            if (movement != null) movement.SetCutsceneLocked(false);
            yield return new WaitForSeconds(0.3f);
        }

        private IEnumerator BreakPlatform(GameObject platform)
        {
            foreach (var col in platform.GetComponentsInChildren<Collider2D>()) col.enabled = false;
            var renderer = platform.GetComponent<SpriteRenderer>();
            Vector3 start = platform.transform.position;
            Vector2 burst = renderer != null ? (Vector2)renderer.bounds.center : (Vector2)start;
            ShockwaveBurst.Play(this, burst, null, new ShockwaveBurst.Settings
            {
                rings = 2, interval = 0.08f, duration = 0.5f, maxSize = 3.5f, squash = 0.6f,
                ringColor = new Color(0.8f, 0.5f, 1f, 0.9f), flashColor = new Color(0f, 0f, 0f, 0f), sortingOrder = 5,
            });
            // 부르르 떨다가 기울며 아래로 떨어져 흩어진다.
            for (float t = 0f; t < 0.25f && platform != null; t += Time.deltaTime)
            {
                platform.transform.position = start + new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(-0.05f, 0.05f), 0f);
                yield return null;
            }
            float spin = Random.value < 0.5f ? -1f : 1f;
            for (float t = 0f; t < 0.7f && platform != null; t += Time.deltaTime)
            {
                float k = t / 0.7f;
                platform.transform.position = start + new Vector3(spin * 0.3f * k, -3f * k * k, 0f);
                platform.transform.rotation = Quaternion.Euler(0f, 0f, spin * 25f * k);
                if (renderer != null) renderer.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }
            if (platform != null) Destroy(platform);
        }

        private IEnumerator PlaySlash(Sprite[] frames, Vector2 at, float angle, float scale, float frameTime)
        {
            if (frames.Length == 0) yield break;
            var go = new GameObject("AliasRitualSlash");
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 8;
            renderer.flipX = Random.value < 0.5f;
            effects.Add(go);
            foreach (var frame in frames)
            {
                if (go == null) yield break;
                renderer.sprite = frame;
                yield return new WaitForSeconds(frameTime);
            }
            if (go != null) Destroy(go);
        }

        private static IEnumerator LoopFrames(SpriteRenderer renderer, Sprite[] frames, float frameTime)
        {
            for (int i = 0; renderer != null; i++)
            {
                renderer.sprite = frames[i % frames.Length];
                yield return new WaitForSeconds(frameTime);
            }
        }

        // ---- 제한 시간 표시 ----

        private void BuildTimerUi()
        {
            timerCanvas = new GameObject("AliasRitualTimer", typeof(Canvas), typeof(CanvasScaler));
            var canvas = timerCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = timerCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
            var rect = (RectTransform)textGO.transform;
            rect.SetParent(timerCanvas.transform, false);
            rect.anchorMin = new Vector2(0.2f, 0.78f);
            rect.anchorMax = new Vector2(0.8f, 0.86f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            timerText = textGO.GetComponent<Text>();
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.fontSize = 30;
            timerText.fontStyle = FontStyle.Bold;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.raycastTarget = false;
            textGO.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        }

        private void UpdateTimerUi(float remaining)
        {
            if (timerText == null) return;
            int alive = AliveSwordCount();
            timerText.color = remaining <= 5f ? new Color(1f, 0.35f, 0.4f) : new Color(0.9f, 0.75f, 1f);
            timerText.text = $"마검을 모두 부숴라!   남은 마검 {alive}/{swords.Count}   {Mathf.Max(0f, remaining):0.0}초";
        }

        private void Cleanup()
        {
            if (timerCanvas != null) Destroy(timerCanvas);
            foreach (var s in swords) if (s != null) Destroy(s.gameObject);
            foreach (var e in effects) if (e != null) Destroy(e);
            Destroy(gameObject, 0.1f);
        }

        // 의식 도중 보스가 사라지거나 스테이지가 바뀌면 남은 마검/이펙트/표시를 모두 걷어 낸다.
        public void Abort()
        {
            StopAllCoroutines();
            Cleanup();
            Finished = true;
        }

        private static Sprite[] LoadSorted(string path)
        {
            var loaded = Resources.LoadAll<Sprite>(path);
            System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
            return loaded;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aethoria.Fx
{
    // 일반 몬스터가 쓰러질 때: 그 자리에 몸 그림을 복사해 남기고(본체는 바로 사라진다),
    // 플레이어 반대쪽으로 튕겨 날아가며 하얗게 깜빡이다가 3x3 조각으로 부서져 흩어진다. 먼지와 반짝이도 함께 튄다.
    public class MonsterDeathFx : MonoBehaviour
    {
        private const int Cols = 3, Rows = 3;
        private const float FlyTime = 0.32f;
        private const float ShardLife = 0.75f;

        // 처치 이펙트 그림(burst_1.png, burst_2.png ...). 폴더가 비어 있으면 조각·파티클만 나온다.
        // 그림 크기와 상관없이 몬스터 몸 크기에 맞춰 키운다(몸 높이의 BurstSizeRatio배).
        private const string BurstPath = "Art/Effects/MonsterDeathBurst";
        private const float BurstFrameTime = 0.05f;
        private const float BurstSizeRatio = 1.6f;
        private static Sprite[] burstFrames;
        private static bool burstLoaded;

        private static Sprite[] BurstFrames
        {
            get
            {
                if (burstLoaded) return burstFrames;
                burstLoaded = true;
                burstFrames = Resources.LoadAll<Sprite>(BurstPath);
                if (burstFrames != null && burstFrames.Length > 0)
                    System.Array.Sort(burstFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
                return burstFrames;
            }
        }

        public static void Play(Component monster, Transform killer)
        {
            var source = FindBodyRenderer(monster);
            if (source == null || source.sprite == null || !source.enabled) return;

            var go = new GameObject("MonsterCorpse");
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            go.transform.localScale = source.transform.lossyScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = source.sprite;
            sr.flipX = source.flipX;
            sr.flipY = source.flipY;
            sr.color = source.color;
            sr.sharedMaterial = source.sharedMaterial;
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder;

            float away = killer != null && killer.position.x > monster.transform.position.x ? -1f : 1f;
            go.AddComponent<MonsterDeathFx>().Begin(sr, away);
        }

        private static SpriteRenderer FindBodyRenderer(Component monster)
        {
            var own = monster.GetComponent<SpriteRenderer>();
            if (own != null) return own;
            var visual = monster.transform.Find("Visual");
            if (visual != null && visual.TryGetComponent(out SpriteRenderer v)) return v;
            SpriteRenderer best = null;
            float bestArea = 0f;
            foreach (var r in monster.GetComponentsInChildren<SpriteRenderer>())
            {
                float area = r.bounds.size.x * r.bounds.size.y;
                if (area > bestArea) { bestArea = area; best = r; }
            }
            return best;
        }

        private SpriteRenderer body;
        private float away;

        private void Begin(SpriteRenderer sr, float direction)
        {
            body = sr;
            away = direction;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Vector3 start = transform.position;
            Color baseColor = body.color;
            float spin = Random.Range(25f, 45f) * -away;
            float t = 0f;
            while (t < FlyTime)
            {
                t += Time.deltaTime;
                float k = t / FlyTime;
                // 뒤로 튕기며 살짝 떠오르는 포물선
                transform.position = start + new Vector3(away * 1.1f * k, 0.9f * k - 1.2f * k * k, 0f);
                transform.rotation = Quaternion.Euler(0f, 0f, spin * k);
                // 빠르게 깜빡이며 점점 하얗게 바랜다
                bool blink = Mathf.FloorToInt(t / 0.05f) % 2 == 0;
                var c = Color.Lerp(baseColor, Color.white, k * 0.7f);
                c.a = baseColor.a * (blink ? 1f : 0.35f);
                body.color = c;
                yield return null;
            }
            Shatter();
            Destroy(gameObject);
        }

        private void Shatter()
        {
            var sprite = body.sprite;
            Rect tex = sprite.textureRect;
            Bounds local = sprite.bounds;
            float ppu = sprite.pixelsPerUnit;
            var created = new List<Sprite>();
            Vector3 center = body.bounds.center;

            for (int cy = 0; cy < Rows; cy++)
            {
                for (int cx = 0; cx < Cols; cx++)
                {
                    var cell = new Rect(tex.x + tex.width * cx / Cols, tex.y + tex.height * cy / Rows, tex.width / Cols, tex.height / Rows);
                    var piece = Sprite.Create(sprite.texture, cell, new Vector2(0.5f, 0.5f), ppu);
                    created.Add(piece);

                    float lx = local.min.x + local.size.x * (cx + 0.5f) / Cols;
                    float ly = local.min.y + local.size.y * (cy + 0.5f) / Rows;
                    if (body.flipX) lx = -lx;
                    Vector3 world = transform.TransformPoint(new Vector3(lx, ly, 0f));

                    var go = new GameObject("Shard");
                    go.transform.SetPositionAndRotation(world, transform.rotation);
                    go.transform.localScale = transform.lossyScale;
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = piece;
                    sr.flipX = body.flipX;
                    sr.sharedMaterial = body.sharedMaterial;
                    sr.sortingLayerID = body.sortingLayerID;
                    sr.sortingOrder = body.sortingOrder;
                    sr.color = new Color(1f, 1f, 1f, body.color.a);

                    Vector2 outward = ((Vector2)(world - center)).normalized;
                    if (outward == Vector2.zero) outward = Vector2.up;
                    Vector2 velocity = outward * Random.Range(2f, 4f) + new Vector2(away * 1.2f, Random.Range(2f, 4f));
                    go.AddComponent<Shard>().Init(velocity, Random.Range(-540f, 540f), ShardLife, piece);
                }
            }

            PlayBurstArt(center, body.bounds.size.y, body.sortingLayerID, body.sortingOrder + 3);

            FxUtil.Burst(center, new FxUtil.BurstSpec
            {
                count = 14, lifeMin = 0.3f, lifeMax = 0.6f, speedMin = 1.5f, speedMax = 3.5f,
                sizeMin = 0.08f, sizeMax = 0.16f, sizeEnd = 0.2f, drag = 2f,
                colorA = new Color(1f, 0.95f, 0.85f), colorB = new Color(0.8f, 0.6f, 1f),
                texture = FxUtil.Sparkle, sortingOrder = body.sortingOrder + 2,
            });
            FxUtil.Burst(center, new FxUtil.BurstSpec
            {
                count = 8, lifeMin = 0.4f, lifeMax = 0.7f, speedMin = 0.6f, speedMax = 1.4f,
                sizeMin = 0.35f, sizeMax = 0.6f, sizeEnd = 1.8f, drag = 3f, radius = 0.3f,
                colorA = new Color(0.6f, 0.55f, 0.65f, 0.5f), colorB = new Color(0.4f, 0.35f, 0.45f, 0.35f),
                texture = FxUtil.SoftCircle, sortingOrder = body.sortingOrder + 1,
            });
        }

        private static void PlayBurstArt(Vector3 center, float bodyHeight, int sortingLayerID, int sortingOrder)
        {
            var frames = BurstFrames;
            if (frames == null || frames.Length == 0) return;

            var fx = Aethoria.Characters.FlipbookSpriteEffect.Create("MonsterDeathBurst", BurstPath, center, BurstFrameTime, loop: false, sortingOrder);
            var sr = fx.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = FxUtil.SpriteMaterial; // 2D 조명과 상관없이 같은 밝기로
            sr.sortingLayerID = sortingLayerID;

            float artHeight = 0f;
            foreach (var f in frames) artHeight = Mathf.Max(artHeight, f.bounds.size.y);
            if (artHeight > 0.001f)
            {
                float scale = Mathf.Max(bodyHeight, 0.8f) * BurstSizeRatio / artHeight;
                fx.transform.localScale = new Vector3(scale, scale, 1f);
            }
            fx.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-15f, 15f));
        }

        // 중력을 받으며 돌다가 옅어지며 사라지는 조각.
        private class Shard : MonoBehaviour
        {
            private Vector2 velocity;
            private float spin, life, age;
            private SpriteRenderer sr;
            private Sprite owned;

            public void Init(Vector2 v, float spinSpeed, float lifetime, Sprite sprite)
            {
                velocity = v;
                spin = spinSpeed;
                life = lifetime;
                owned = sprite;
                sr = GetComponent<SpriteRenderer>();
            }

            private void Update()
            {
                age += Time.deltaTime;
                velocity += Vector2.down * 14f * Time.deltaTime;
                transform.position += (Vector3)(velocity * Time.deltaTime);
                transform.Rotate(0f, 0f, spin * Time.deltaTime);
                float k = age / life;
                var c = sr.color;
                c.a = Mathf.Clamp01(1f - k * k);
                sr.color = c;
                transform.localScale *= 1f - Time.deltaTime * 0.6f;
                if (age >= life) Destroy(gameObject);
            }

            private void OnDestroy()
            {
                if (owned != null) Destroy(owned);
            }
        }
    }
}

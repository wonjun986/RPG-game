using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Aethoria.Characters;

namespace Aethoria.Stages
{
    // 마지막 푸른 바다 맵 한가운데에서 땅이 꺼지는 연출. 배경(blue_sea_broken)에는 다리가 끊긴 구멍이 이미 그려져 있고,
    // 그 위를 멀쩡한 돌길 조각 8장(Backgrounds/BlueSeaCollapse)이 덮고 있다. 조각이 떨어지면 뒤에 있던 진짜 구멍이 드러난다.
    // 전조(작은 돌과 먼지) → 균열(조각이 살짝 내려앉음) → 붕괴(가까운 조각부터 시간차로 낙하) → 낙하 → 입수(물보라와 섬광)
    // → 물빛으로 어두워지며 물속 추격전(SeaChaseController)으로 넘어간다.
    public class SeaCollapseCutscene : MonoBehaviour
    {
        // 아래 좌표는 모두 blue_sea_broken.png(1672x941)의 픽셀이다.
        private const float ImageCenterPx = 836f;
        private const float GroundPx = 788f;
        private const float TriggerPx = 770f;   // 구멍 위로 이만큼 들어서면 무너진다
        private const float BlockerPx = 900f;   // 구멍을 넘어가지 못하게 막는 자리
        private const float PixelsPerUnit = 100f;

        private const string PiecesPath = "Backgrounds/BlueSeaCollapse/piece_";
        // 조각마다 그림 속 가운데 자리. 앞 네 장이 돌길 윗단, 뒤 네 장이 아랫단이다.
        private static readonly Vector2[] PieceCenters =
        {
            new Vector2(697.0f, 835.0f), new Vector2(774.0f, 833.5f), new Vector2(858.5f, 835.5f), new Vector2(922.5f, 837.0f),
            new Vector2(705.5f, 901.5f), new Vector2(778.5f, 897.0f), new Vector2(853.5f, 896.0f), new Vector2(924.5f, 902.5f),
        };
        private const int UpperPieceCount = 4;
        private const int PieceSortingOrder = -9;   // 배경(-10)보다 앞, 발판/캐릭터보다 뒤

        private const float OmenDuration = 1.1f;
        private const float OmenShake = 0.05f;
        private const int OmenPebbles = 4;
        private const float CrackDuration = 0.7f;
        private const float CrackShake = 0.14f;
        private const float CollapseShakeDuration = 1.2f;
        private const float CollapseShake = 0.3f;
        private const float PieceFallStagger = 0.07f;
        private const float LowerPieceExtraDelay = 0.12f;
        private const float PieceGravity = 16f;

        private const float FallGravityScale = 1.5f;    // 떨어지는 모습이 보이도록 평소보다 천천히 떨어진다
        private const float FallDepth = 1.7f;           // 바닥 아래 이만큼에 수면이 그려져 있다
        private const float FallTimeout = 1.4f;
        private const float FallCameraDrop = 0.45f;     // 그림 아래끝이 드러나지 않을 만큼만 내려간다

        private const string DebrisFolder = "Art/Props/Sea/";
        private static readonly string[] DebrisSprites = { "sea_rock_tiny", "sea_rock_small", "sea_rock_wedge" };
        private static readonly Color DustColor = new Color(0.72f, 0.7f, 0.68f, 0.55f);
        private static readonly Color SplashColor = new Color(0.85f, 0.95f, 1f, 0.9f);
        private const int ParticleSortingOrder = 5;

        private const float FlashTime = 0.12f;
        private const float FlashAlpha = 0.85f;
        private const float FadeOutTime = 0.35f;

        private class Particle
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Gravity;
            public float Spin;
            public float Delay;
            public float Life;      // 0이면 사라지지 않는다
            public float Age;
            public float Grow;
            public float StartAlpha;
        }

        private static Sprite softCircle;

        private Character player;
        private CharacterMovement2D movement;
        private Rigidbody2D body;
        private Collider2D playerCollider;
        private Collider2D floor;
        private CameraFollow cameraFollow;
        private Vector2 anchor;     // 그림의 (가로 가운데, 돌길 높이)가 놓인 월드 좌표
        private float unitsPerPixel;
        private float savedGravity;
        private System.Action onFinished;
        private readonly List<Transform> pieces = new List<Transform>();
        private readonly List<Particle> particles = new List<Particle>();
        private bool started;
        private bool falling;
        private bool finished;

        private float WorldX(float px) => anchor.x + (px - ImageCenterPx) * unitsPerPixel;
        private float WorldY(float py) => anchor.y + (GroundPx - py) * unitsPerPixel;

        public void Initialize(Character targetPlayer, Collider2D floorCollider, float imageCenterX, float mapFloorY, float backgroundScale, System.Action finishedCallback)
        {
            player = targetPlayer;
            movement = player.GetComponent<CharacterMovement2D>();
            body = player.GetComponent<Rigidbody2D>();
            playerCollider = player.GetComponent<Collider2D>();
            floor = floorCollider;
            anchor = new Vector2(imageCenterX, mapFloorY);
            unitsPerPixel = backgroundScale / PixelsPerUnit;
            onFinished = finishedCallback;

            for (int i = 0; i < PieceCenters.Length; i++) SpawnPiece(i, backgroundScale);

            var blocker = new GameObject("CollapseBlocker", typeof(BoxCollider2D));
            blocker.transform.SetParent(transform);
            blocker.transform.position = new Vector3(WorldX(BlockerPx), mapFloorY + 10f, 0f);
            blocker.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 20f);
        }

        private void SpawnPiece(int index, float scale)
        {
            var sprite = Resources.Load<Sprite>(PiecesPath + (index + 1));
            var go = new GameObject("CollapsePiece", typeof(SpriteRenderer));
            go.transform.SetParent(transform);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.transform.position = new Vector3(WorldX(PieceCenters[index].x), WorldY(PieceCenters[index].y), 0f);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = PieceSortingOrder;
            pieces.Add(go.transform);
        }

        private void Update()
        {
            UpdateParticles();

            if (started || player == null || player.IsDead || movement.IsInputLocked) return;
            if (player.transform.position.x < WorldX(TriggerPx) || !movement.IsGrounded) return;
            if (player.transform.position.y > anchor.y + 0.5f) return; // 발판 위는 지나간다

            started = true;
            StartCoroutine(CollapseRoutine());
        }

        private IEnumerator CollapseRoutine()
        {
            movement.SetCutsceneLocked(true);
            player.SetInvincible(true);
            cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            float playerX = player.transform.position.x;

            // 전조: 약하게 울리며 돌길 가장자리에서 작은 돌과 먼지가 흘러내린다.
            Shake(OmenDuration, OmenShake);
            for (int i = 0; i < OmenPebbles; i++)
            {
                float x = playerX + Random.Range(-1.5f, 1.5f);
                SpawnDebris(new Vector2(x, anchor.y - 0.1f), new Vector2(Random.Range(-0.4f, 0.4f), 0f), 0.25f);
                SpawnDust(new Vector2(x, anchor.y), 2, 0.4f);
                yield return new WaitForSeconds(OmenDuration / OmenPebbles);
            }

            // 균열: 흔들림이 세지고 조각들이 제각각 살짝 내려앉는다.
            Shake(CrackDuration, CrackShake);
            foreach (var piece in pieces)
            {
                piece.position += Vector3.down * Random.Range(0.04f, 0.12f);
                piece.Rotate(0f, 0f, Random.Range(-2f, 2f));
            }
            SpawnDust(new Vector2(playerX, anchor.y), 6, 0.7f);
            yield return new WaitForSeconds(CrackDuration);

            // 붕괴: 발밑에서 가까운 조각부터 시간차를 두고 떨어진다.
            Shake(CollapseShakeDuration, CollapseShake);
            DropPieces(playerX);
            SpawnDust(new Vector2(playerX, anchor.y), 14, 1.2f);
            for (int i = 0; i < 6; i++)
            {
                SpawnDebris(new Vector2(playerX + Random.Range(-2f, 2f), anchor.y),
                    new Vector2(Random.Range(-2f, 2f), Random.Range(3f, 7f)), Random.Range(0.3f, 0.7f));
            }

            // 낙하: 바닥 판정만 이 캐릭터에게서 꺼서, 맵에 남은 몬스터는 그대로 서 있게 둔다.
            if (floor != null) Physics2D.IgnoreCollision(playerCollider, floor, true);
            savedGravity = body.gravityScale;
            body.gravityScale = FallGravityScale;
            falling = true;
            if (cameraFollow != null) cameraFollow.ShiftView(-FallCameraDrop);

            float surfaceY = anchor.y - FallDepth;
            for (float t = 0f; t < FallTimeout && player.transform.position.y > surfaceY; t += Time.deltaTime)
            {
                yield return null;
            }

            // 입수: 물보라와 함께 화면이 번쩍이고 물빛으로 잠긴다.
            SpawnSplash(new Vector2(player.transform.position.x, surfaceY));
            var flash = SeaChaseController.CreateFade(transform, Color.white);
            yield return SeaChaseController.FadeTo(flash, FlashAlpha, FlashTime);
            var fade = SeaChaseController.CreateFade(transform, SeaChaseController.DeepWaterColor);
            yield return SeaChaseController.FadeTo(fade, 1f, FadeOutTime);

            RestoreFall();
            finished = true;
            onFinished?.Invoke();
        }

        private void Shake(float duration, float magnitude)
        {
            if (cameraFollow != null) cameraFollow.Shake(duration, magnitude);
        }

        private void DropPieces(float playerX)
        {
            var order = new List<int>();
            for (int i = 0; i < pieces.Count; i++) order.Add(i);
            order.Sort((a, b) => Mathf.Abs(pieces[a].position.x - playerX).CompareTo(Mathf.Abs(pieces[b].position.x - playerX)));

            for (int rank = 0; rank < order.Count; rank++)
            {
                int index = order[rank];
                particles.Add(new Particle
                {
                    Transform = pieces[index],
                    Velocity = new Vector2(Random.Range(-0.6f, 0.6f), 0f),
                    Gravity = PieceGravity,
                    Spin = Random.Range(-70f, 70f),
                    Delay = rank * PieceFallStagger + (index >= UpperPieceCount ? LowerPieceExtraDelay : 0f),
                });
            }
        }

        private void SpawnDebris(Vector2 position, Vector2 velocity, float height)
        {
            var sprite = Resources.Load<Sprite>(DebrisFolder + DebrisSprites[Random.Range(0, DebrisSprites.Length)]);
            if (sprite == null) return;

            var go = new GameObject("CollapseDebris", typeof(SpriteRenderer));
            go.transform.SetParent(transform);
            float scale = height / sprite.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.transform.position = position;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = ParticleSortingOrder;
            particles.Add(new Particle
            {
                Transform = go.transform,
                Velocity = velocity,
                Gravity = PieceGravity,
                Spin = Random.Range(-240f, 240f),
            });
        }

        // 먼지 그림이 나오면 이 자리를 프레임 재생으로 바꾼다. 지금은 부드러운 원을 키우며 흐리게 사라지게 한다.
        private void SpawnDust(Vector2 center, int count, float size)
        {
            for (int i = 0; i < count; i++)
            {
                var position = center + new Vector2(Random.Range(-2f, 2f), Random.Range(-0.1f, 0.2f));
                var velocity = new Vector2(Random.Range(-0.7f, 0.7f), Random.Range(0.2f, 0.9f));
                SpawnPuff(position, velocity, 0f, size * Random.Range(0.6f, 1.2f), Random.Range(0.6f, 1.1f), DustColor, size);
            }
        }

        // 물보라 그림이 나오면 이 자리를 프레임 재생으로 바꾼다. 지금은 물방울을 위로 튀기고 가운데에 하얀 기둥을 세운다.
        private void SpawnSplash(Vector2 center)
        {
            for (int i = 0; i < 26; i++)
            {
                var velocity = new Vector2(Random.Range(-3f, 3f), Random.Range(4f, 10f));
                SpawnPuff(center, velocity, PieceGravity, Random.Range(0.12f, 0.35f), Random.Range(0.4f, 0.7f), SplashColor, 0f);
            }
            SpawnPuff(center, Vector2.up * 1.5f, 0f, 1f, 0.45f, SplashColor, 4f);
        }

        private void SpawnPuff(Vector2 position, Vector2 velocity, float gravity, float size, float life, Color color, float grow)
        {
            var go = new GameObject("CollapsePuff", typeof(SpriteRenderer));
            go.transform.SetParent(transform);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = SoftCircle;
            renderer.color = color;
            renderer.sortingOrder = ParticleSortingOrder;
            particles.Add(new Particle
            {
                Transform = go.transform,
                Renderer = renderer,
                Velocity = velocity,
                Gravity = gravity,
                Life = life,
                Grow = grow,
                StartAlpha = color.a,
            });
        }

        // 지름 1유닛의 가장자리가 흐린 원.
        private static Sprite SoftCircle
        {
            get
            {
                if (softCircle != null) return softCircle;

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var center = new Vector2((size - 1) / 2f, (size - 1) / 2f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center) / (size / 2f);
                        float alpha = Mathf.Clamp01(1f - distance);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                    }
                }
                texture.Apply();
                softCircle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                return softCircle;
            }
        }

        private void UpdateParticles()
        {
            float dt = Time.deltaTime;
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var particle = particles[i];
                if (particle.Delay > 0f)
                {
                    particle.Delay -= dt;
                    continue;
                }

                particle.Velocity.y -= particle.Gravity * dt;
                particle.Transform.position += (Vector3)(particle.Velocity * dt);
                particle.Transform.Rotate(0f, 0f, particle.Spin * dt);
                if (particle.Life <= 0f) continue;

                particle.Age += dt;
                particle.Transform.localScale += Vector3.one * (particle.Grow * dt);
                var color = particle.Renderer.color;
                color.a = particle.StartAlpha * (1f - particle.Age / particle.Life);
                particle.Renderer.color = color;
                if (particle.Age < particle.Life) continue;

                Destroy(particle.Transform.gameObject);
                particles.RemoveAt(i);
            }
        }

        private void RestoreFall()
        {
            if (!falling) return;

            falling = false;
            body.gravityScale = savedGravity;
            if (floor != null) Physics2D.IgnoreCollision(playerCollider, floor, false);
        }

        // 연출 도중 맵이 사라지면(관리자 순간이동 등) 걸어 둔 조작 잠금과 무적, 낙하 상태를 풀어 준다.
        private void OnDestroy()
        {
            if (!started || finished || player == null) return;

            RestoreFall();
            movement.SetCutsceneLocked(false);
            player.SetInvincible(false);
        }
    }
}

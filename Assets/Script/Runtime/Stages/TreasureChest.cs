using UnityEngine;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Data;
using Aethoria.Fx;

namespace Aethoria.Stages
{
    // 사냥터에 숨겨 둔 보물상자(TreasureChests). 네크로시아가 닿으면 저절로 열려 골드/강화석/포션을 쏟아 낸다.
    // 한 번 연 상자는 세이브에 남아서, 다시 와도 열린 채로 놓여 있다(보상 없음).
    // 그림: Art/Props/Chest/chest_closed, chest_open이 있으면 그걸 쓰고, 없으면 코드로 그린 픽셀 상자를 쓴다.
    public class TreasureChest : MonoBehaviour
    {
        private const string ClosedArtPath = "Art/Props/Chest/chest_closed";
        private const string OpenArtPath = "Art/Props/Chest/chest_open";
        private const float OpenRangeX = 0.8f;
        private const float OpenRangeUp = 1.3f;
        private const float OpenRangeDown = 0.4f;
        private static readonly Color GlowColor = new Color(1f, 0.8f, 0.35f);

        private static Sprite closedSprite, openSprite;

        private Character player;
        private TreasureChests.Entry entry;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer glow;
        private UnityEngine.Rendering.Universal.Light2D glowLight;
        private bool opened;
        private float sparkleTimer;

        // position은 상자 밑면 가운데(밟는 면 위).
        public static TreasureChest Spawn(Transform parent, string id, Vector3 position, Character player)
        {
            var entry = TreasureChests.Find(id);
            if (entry == null) return null;

            var go = new GameObject("TreasureChest_" + id);
            go.transform.SetParent(parent);
            go.transform.position = position;
            var chest = go.AddComponent<TreasureChest>();
            chest.player = player;
            chest.entry = entry;
            chest.Build();
            return chest;
        }

        private void Build()
        {
            EnsureSprites();
            opened = TreasureChests.IsOpened(entry.Id);

            // 비밀의 장소 상자는 조금 더 크게.
            float scale = entry.Secret ? 1.35f : 1f;
            transform.localScale = new Vector3(scale, scale, 1f);

            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = opened ? openSprite : closedSprite;
            spriteRenderer.sortingOrder = -3; // 발판(-5)보다 앞, 캐릭터보다 뒤

            if (!opened)
            {
                // 아직 안 연 상자는 뒤에서 은은하게 금빛이 일렁여 멀리서도 눈에 띈다.
                glow = FxUtil.MakeSprite("ChestGlow", FxUtil.SoftCircleSprite, transform.position + Vector3.up * 0.35f,
                    new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0.45f), -4, transform);
                glow.transform.localScale = new Vector3(2.2f, 1.6f, 1f);
                glowLight = SceneLighting.AddPointLight(transform, new Vector3(0f, 0.4f, 0f), GlowColor, 2.2f, 0.6f);
            }
        }

        private void Update()
        {
            if (opened || player == null || player.IsDead) return;

            if (glow != null)
            {
                float pulse = 0.35f + 0.15f * Mathf.Sin(Time.time * 2.5f);
                glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, pulse);
            }

            sparkleTimer -= Time.deltaTime;
            if (sparkleTimer <= 0f)
            {
                sparkleTimer = Random.Range(0.35f, 0.7f);
                FxUtil.Burst(transform.position + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.2f, 0.7f), 0f), new FxUtil.BurstSpec
                {
                    count = 1, lifeMin = 0.5f, lifeMax = 0.8f, speedMin = 0.1f, speedMax = 0.3f,
                    sizeMin = 0.1f, sizeMax = 0.18f, sizeEnd = 0.1f, gravity = -0.2f, drag = 1f,
                    arc = 360f, colorA = new Color(1f, 0.95f, 0.6f), colorB = GlowColor,
                    texture = FxUtil.Sparkle, sortingOrder = 6,
                });
            }

            Vector2 d = player.transform.position - transform.position;
            if (Mathf.Abs(d.x) <= OpenRangeX && d.y <= OpenRangeUp && d.y >= -OpenRangeDown) Open();
        }

        private void Open()
        {
            opened = true;
            TreasureChests.MarkOpened(entry.Id);
            spriteRenderer.sprite = openSprite;
            if (glow != null) Destroy(glow.gameObject);
            if (glowLight != null) glowLight.intensity = 0.3f;

            Vector3 top = transform.position + Vector3.up * 0.6f;
            float floorY = transform.position.y;

            FxUtil.Burst(top, new FxUtil.BurstSpec
            {
                count = entry.Secret ? 40 : 24, lifeMin = 0.5f, lifeMax = 1.1f, speedMin = 2f, speedMax = 5.5f,
                sizeMin = 0.12f, sizeMax = 0.26f, sizeEnd = 0.1f, gravity = 2f, drag = 2f,
                arcStart = 30f, arc = 120f, radius = 0.2f,
                colorA = new Color(1f, 0.95f, 0.6f), colorB = GlowColor,
                texture = FxUtil.Sparkle, sortingOrder = 31,
            });

            // 보상: 실제로 넣는 건 바로, 튀어나오는 그림은 연출만(몬스터 드롭과 같은 방식).
            player.AddGold(entry.Gold);
            LootFx.SpawnGold(top, floorY, entry.Gold);
            var inventory = player.GetComponent<Inventory>();
            if (inventory != null)
            {
                if (entry.EnhanceStones > 0)
                {
                    inventory.AddItem(ItemDatabase.EnhanceStone, entry.EnhanceStones);
                    LootFx.SpawnItem(top, floorY, ItemDatabase.EnhanceStone, entry.EnhanceStones);
                }
                if (entry.Potions > 0)
                {
                    inventory.AddItem(ItemDatabase.Potion, entry.Potions);
                    LootFx.SpawnItem(top, floorY, ItemDatabase.Potion, entry.Potions);
                }
            }

            if (!SfxPlayer.TryPlay(SfxPlayer.ItemPickup)) ProceduralAudio.PlayHit(HitSound.Critical);
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.Shake(0.12f, 0.05f);

            string region = TreasureChests.RegionName(entry.Region);
            ShowFloatingText(entry.Secret ? "비밀의 보물을 찾았다!" : "보물상자를 찾았다!",
                $"{region} 보물 {TreasureChests.OpenedCount(entry.Region)}/{TreasureChests.Total(entry.Region)}  ·  +{entry.Gold:N0}G");

            GameBootstrap.SaveGame();
        }

        private void ShowFloatingText(string title, string detail)
        {
            var go = new GameObject("ChestText");
            go.transform.position = transform.position + Vector3.up * 1.6f;
            var text = go.AddComponent<TextMesh>();
            text.text = title + "\n<size=30>" + detail + "</size>";
            text.richText = true;
            text.characterSize = 0.07f;
            text.fontSize = 44;
            text.alignment = TextAlignment.Center;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.88f, 0.45f);
            go.GetComponent<MeshRenderer>().sortingOrder = 60;
            go.AddComponent<FloatingTextFade>();
        }

        private static void EnsureSprites()
        {
            if (closedSprite != null && openSprite != null) return;
            closedSprite = Resources.Load<Sprite>(ClosedArtPath);
            openSprite = Resources.Load<Sprite>(OpenArtPath);
            if (closedSprite == null || openSprite == null)
            {
                closedSprite = MakeChestSprite(false);
                openSprite = MakeChestSprite(true);
            }
        }

        // 24x22 픽셀 상자(아래 가운데 피벗, 폭 약 0.86유닛). 나무 몸통 + 금테 두 줄 + 자물쇠.
        private static Sprite MakeChestSprite(bool open)
        {
            const int W = 24, H = 22;
            var px = new Color32[W * H];
            var outline = new Color32(28, 14, 10, 255);
            var wood = new Color32(118, 66, 36, 255);
            var woodDark = new Color32(84, 44, 24, 255);
            var woodLight = new Color32(156, 96, 52, 255);
            var gold = new Color32(232, 184, 70, 255);
            var goldDark = new Color32(168, 118, 38, 255);
            var inside = new Color32(36, 18, 12, 255);
            var glint = new Color32(255, 240, 160, 255);

            void Set(int x, int y, Color32 c) { if (x >= 0 && x < W && y >= 0 && y < H) px[y * W + x] = c; }
            void Fill(int x0, int y0, int x1, int y1, Color32 c) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y, c); }

            // 몸통(y 0~10)
            Fill(1, 0, 22, 10, outline);
            Fill(2, 1, 21, 9, wood);
            Fill(2, 1, 21, 1, woodDark);
            Fill(2, 5, 21, 5, woodDark);
            Fill(2, 9, 21, 9, woodLight);
            Fill(5, 1, 6, 9, gold); Fill(6, 1, 6, 9, goldDark);
            Fill(17, 1, 18, 9, gold); Fill(18, 1, 18, 9, goldDark);

            if (!open)
            {
                // 닫힌 뚜껑(y 10~17, 위가 둥글다)
                Fill(1, 10, 22, 16, outline);
                Fill(2, 17, 21, 17, outline);
                Fill(2, 11, 21, 16, wood);
                Fill(3, 16, 20, 16, woodLight);
                Fill(2, 11, 21, 11, gold);
                Fill(5, 12, 6, 16, gold); Fill(17, 12, 18, 16, gold);
                // 자물쇠
                Fill(10, 7, 13, 12, outline);
                Fill(11, 8, 12, 11, gold);
                Set(11, 9, outline); Set(12, 10, glint);
            }
            else
            {
                // 열린 입구(안쪽 그늘 + 쌓인 금화)와 뒤로 젖혀진 뚜껑(y 11~21)
                Fill(2, 9, 21, 10, inside);
                Fill(4, 10, 19, 11, gold);
                Set(7, 11, glint); Set(12, 11, glint); Set(16, 11, glint); Set(9, 12, gold); Set(14, 12, gold);
                Fill(2, 13, 21, 21, outline);
                Fill(3, 14, 20, 20, woodDark);
                Fill(3, 14, 20, 14, gold);
                Fill(5, 15, 6, 20, goldDark); Fill(17, 15, 18, 20, goldDark);
            }

            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(px);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 28f);
        }
    }

    // 머리 위에 뜬 글자가 천천히 떠오르며 사라진다.
    public class FloatingTextFade : MonoBehaviour
    {
        private const float Duration = 2.2f;
        private TextMesh text;
        private Color baseColor;
        private float age;

        private void Start()
        {
            text = GetComponent<TextMesh>();
            baseColor = text.color;
        }

        private void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (0.5f * Time.deltaTime);
            float alpha = age < Duration - 0.6f ? 1f : Mathf.Clamp01((Duration - age) / 0.6f);
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            if (age >= Duration) Destroy(gameObject);
        }
    }
}

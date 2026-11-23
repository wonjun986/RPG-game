using UnityEngine;
using Aethoria.Data;

namespace Aethoria.Fx
{
    // 몬스터가 떨군 아이템/골드 연출. 보상 자체는 원래대로 쓰러진 순간 바로 들어가고(가방·골드), 이건 보이는 연출만 맡는다.
    //  - 아이템: 아이콘이 튀어 올랐다가 바닥에 통통 튀고, 등급 색 빛기둥이 선다. 잠시 뒤 플레이어 쪽으로 빨려 들어온다.
    //  - 골드: 동전 몇 개가 사방으로 튀었다가 곧바로 플레이어에게 빨려 들어온다.
    public class LootFx : MonoBehaviour
    {
        public static Transform Target;

        private const float Gravity = 18f;
        private const float MagnetDelayItem = 0.9f, MagnetDelayGold = 0.45f;
        private const float MagnetRange = 3.5f;   // 이 거리 안이면 기다리지 않고 바로 끌려온다
        private const int SortingOrder = 30;

        public static Color RarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => new Color(0.4f, 1f, 0.45f),
            ItemRarity.Rare => new Color(0.35f, 0.65f, 1f),
            ItemRarity.Epic => new Color(0.8f, 0.45f, 1f),
            ItemRarity.Legendary => new Color(1f, 0.7f, 0.2f),
            _ => new Color(0.95f, 0.95f, 0.95f),
        };

        public static void SpawnItem(Vector2 from, float floorY, ItemData item, int count = 1)
        {
            if (item == null) return;
            var icon = item.LoadIcon();
            Color color = RarityColor(item.rarity);
            for (int i = 0; i < Mathf.Clamp(count, 1, 3); i++)
            {
                var sr = FxUtil.MakeSprite("LootItem", icon != null ? icon : FxUtil.GemSprite, from, icon != null ? Color.white : color, SortingOrder + 1);
                // 아이콘 크기가 제각각이라 가로세로 큰 쪽을 0.5유닛으로 맞춘다.
                float size = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
                float scale = size > 0f ? 0.5f / size : 1f;
                sr.transform.localScale = Vector3.one * scale;

                var pillar = FxUtil.MakeSprite("LootPillar", FxUtil.PillarSprite, from, new Color(color.r, color.g, color.b, 0.55f), SortingOrder, sr.transform);
                pillar.transform.localScale = new Vector3(0.9f, 2.6f, 1f) / scale;
                pillar.transform.localPosition = new Vector3(0f, -0.25f / scale, 0f);

                var loot = sr.gameObject.AddComponent<LootFx>();
                loot.Init(new Vector2(Random.Range(-2.2f, 2.2f), Random.Range(6f, 8f)), floorY, MagnetDelayItem, scale, pillar, color, (int)item.rarity >= (int)ItemRarity.Epic);
            }
        }

        public static void SpawnGold(Vector2 from, float floorY, int amount)
        {
            if (amount <= 0) return;
            int coins = Mathf.Clamp(amount / 5, 2, 8);
            for (int i = 0; i < coins; i++)
            {
                var sr = FxUtil.MakeSprite("LootGold", FxUtil.CoinSprite, from, Color.white, SortingOrder + 1);
                var loot = sr.gameObject.AddComponent<LootFx>();
                loot.Init(new Vector2(Random.Range(-2.6f, 2.6f), Random.Range(4.5f, 7f)), floorY, MagnetDelayGold + Random.Range(0f, 0.2f), 1f, null, new Color(1f, 0.85f, 0.3f), false);
            }
        }

        private Vector2 velocity;
        private float floorY;
        private float magnetDelay;
        private float age;
        private float baseScale;
        private SpriteRenderer pillar;
        private Color color;
        private bool fancy;
        private bool magnet;
        private float magnetSpeed;
        private int bounces;
        private float sparkleTimer;

        private void Init(Vector2 v, float floor, float delay, float scale, SpriteRenderer pillarRenderer, Color c, bool sparkle)
        {
            velocity = v;
            floorY = floor + 0.15f;
            magnetDelay = delay;
            baseScale = scale;
            pillar = pillarRenderer;
            color = c;
            fancy = sparkle;
        }

        private void Update()
        {
            age += Time.deltaTime;
            Vector3 p = transform.position;

            if (!magnet && Target != null && (age >= magnetDelay || (age > 0.25f && Vector2.Distance(p, Target.position) < MagnetRange)) && bounces > 0)
            {
                magnet = true;
                magnetSpeed = 2f;
            }

            if (magnet && Target != null)
            {
                Vector3 goal = Target.position + new Vector3(0f, 0.6f, 0f);
                magnetSpeed += 30f * Time.deltaTime;
                transform.position = Vector3.MoveTowards(p, goal, magnetSpeed * Time.deltaTime);
                transform.localScale = Vector3.one * baseScale * Mathf.Clamp(Vector3.Distance(transform.position, goal), 0.4f, 1f);
                if (pillar != null)
                {
                    var c = pillar.color;
                    c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * 3f);
                    pillar.color = c;
                }
                if (Vector3.Distance(transform.position, goal) < 0.15f)
                {
                    FxUtil.Burst(goal, new FxUtil.BurstSpec
                    {
                        count = 6, lifeMin = 0.2f, lifeMax = 0.35f, speedMin = 1f, speedMax = 2f,
                        sizeMin = 0.08f, sizeMax = 0.14f, sizeEnd = 0.2f, drag = 3f,
                        colorA = color, colorB = Color.white, texture = FxUtil.Sparkle, sortingOrder = SortingOrder + 2,
                    });
                    Destroy(gameObject);
                }
                return;
            }

            if (bounces < 3 || p.y > floorY)
            {
                velocity.y -= Gravity * Time.deltaTime;
                p += (Vector3)(velocity * Time.deltaTime);
                if (p.y <= floorY && velocity.y < 0f)
                {
                    p.y = floorY;
                    bounces++;
                    velocity = new Vector2(velocity.x * 0.5f, -velocity.y * 0.42f);
                    if (bounces >= 3 || velocity.y < 0.8f) { velocity = Vector2.zero; bounces = 3; }
                }
                transform.position = p;
            }
            else
            {
                // 바닥에 떨어져 기다리는 동안 살짝 둥실거린다
                transform.position = new Vector3(p.x, floorY + Mathf.Sin(age * 4f) * 0.05f, p.z);
            }

            if (fancy)
            {
                sparkleTimer -= Time.deltaTime;
                if (sparkleTimer <= 0f)
                {
                    sparkleTimer = 0.12f;
                    FxUtil.Burst(transform.position, new FxUtil.BurstSpec
                    {
                        count = 1, lifeMin = 0.4f, lifeMax = 0.6f, speedMin = 0.2f, speedMax = 0.6f, radius = 0.25f,
                        sizeMin = 0.1f, sizeMax = 0.18f, sizeEnd = 0.1f, gravity = -0.1f,
                        colorA = color, colorB = Color.white, texture = FxUtil.Sparkle, sortingOrder = SortingOrder + 2,
                    });
                }
            }

            if (age > 6f) Destroy(gameObject); // 플레이어가 없어도 오래 남지 않게
        }
    }
}

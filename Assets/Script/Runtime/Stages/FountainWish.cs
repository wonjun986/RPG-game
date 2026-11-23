using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Fx;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 마을 천사 석상 분수의 소원 동전. 분수 앞에서 [Z]를 누르면 1G 동전을 던진다.
    // 동전이 포물선을 그리며 아래 수반에 퐁당 떨어지고, 1% 확률로 분수가 10G를 돌려준다.
    // 나머지는 기도를 올린 짧은 독백이 분수 위에 떠오른다(같은 말이 연달아 나오지 않게).
    public class FountainWish : MonoBehaviour
    {
        private const int CoinCost = 1;
        private const int JackpotGold = 10;
        private const float JackpotChance = 0.01f;
        private const float RangeX = 1.6f;          // 분수 가운데에서 좌우로 이만큼 안이면 던질 수 있다
        private const float Cooldown = 0.9f;        // 연타 방지(동전이 날아가 떨어지는 시간 정도)
        private const float FlightTime = 0.5f;
        private const float WaterHeight = 0.95f;    // 분수 밑면에서 아래 수반 물 높이(유닛)
        private const float BasinHalfWidth = 0.6f;

        private static readonly Color LineColor = new Color(0.85f, 0.8f, 1f);
        private static readonly Color JackpotColor = new Color(1f, 0.88f, 0.45f);
        private static readonly Color WaterA = new Color(0.95f, 0.95f, 1f);
        private static readonly Color WaterB = new Color(0.7f, 0.75f, 1f);

        private static readonly string[] WishLines =
        {
            "동전을 던지고 조용히 기도를 올렸다...\n기분이 조금 좋아졌다.",
            "소원을 빌었다.\n좋은 일이 있을지도...?",
            "퐁당.\n물결이 동그랗게 퍼져 나간다.",
            "천사 석상이 살짝 웃은 것 같다...\n기분 탓이겠지.",
            "오늘은 왠지 운이 좋을 것 같다.",
            "무슨 소원을 빌었는지는...\n비밀이다.",
            "동전이 다른 동전들 사이로 가라앉았다.\n다들 무슨 소원을 빌었을까.",
            "물에 비친 얼굴이\n평소보다 조금 부드러워 보인다.",
            "...아무 일도 일어나지 않았다.\n그래도 마음은 가볍다.",
            "분수 물소리에\n마음이 차분해진다.",
            "1G로 행복을 살 수 있다면\n싼 편이다.",
            "동전이 반짝이며 가라앉는다.\n소원이 닿았을까?",
            "영혼 대신 동전을 바치는 것도\n나쁘지 않네.",
            "내일은 조금 더 강해질 수 있기를.",
            "두 손을 모았다.\n이상하게 가슴이 따뜻해진다.",
            "물보라가 볼에 튀었다.\n...차갑다.",
        };

        private const string EmptyLine = "주머니가 텅 비었다...\n동전이 한 닢도 없다.";
        private const string JackpotLine = "분수가 눈부시게 반짝인다!\n동전이 10G가 되어 돌아왔다!";

        private Character player;
        private CharacterMovement2D movement;
        private TextMesh prompt;
        private bool inRange;
        private float cooldownLeft;
        private int lastLine = -1;

        // position: 분수 밑면 가운데. promptHeight: 안내 글자를 띄울 높이(분수 수반 위).
        public static FountainWish Spawn(Transform parent, Vector3 position, Character player, float promptHeight = 2.2f)
        {
            if (player == null) return null;
            var go = new GameObject("FountainWish");
            go.transform.SetParent(parent);
            go.transform.position = position;
            var wish = go.AddComponent<FountainWish>();
            wish.player = player;
            wish.movement = player.GetComponent<CharacterMovement2D>();
            wish.CreatePrompt(promptHeight);
            return wish;
        }

        private void CreatePrompt(float height)
        {
            var go = new GameObject("WishPrompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            prompt = go.AddComponent<TextMesh>();
            prompt.text = $"[Z] 동전 던지기 ({CoinCost}G)";
            prompt.characterSize = 0.08f;
            prompt.fontSize = 42;
            prompt.alignment = TextAlignment.Center;
            prompt.anchor = TextAnchor.MiddleCenter;
            prompt.color = Color.white;
            go.GetComponent<MeshRenderer>().sortingOrder = 50;
            go.SetActive(false);
        }

        private void Update()
        {
            if (player == null || player.IsDead) return;
            if (cooldownLeft > 0f) cooldownLeft -= Time.deltaTime;

            Vector2 d = player.transform.position - transform.position;
            bool now = Mathf.Abs(d.x) <= RangeX && d.y > -0.5f && d.y < 2f;
            if (now != inRange)
            {
                inRange = now;
                prompt.gameObject.SetActive(inRange);
            }
            if (!inRange || cooldownLeft > 0f) return;

            // 대화·창·일시정지 중의 [Z]는 그쪽 몫이다.
            if (Time.timeScale <= 0f || ChoicePopup.IsOpen) return;
            if (movement != null && movement.IsInputLocked) return;
            if (!NpcDialogueUI.AdvancePressed(Keyboard.current)) return;

            cooldownLeft = Cooldown;
            if (!player.TrySpendGold(CoinCost))
            {
                ShowLine(EmptyLine, new Color(1f, 0.6f, 0.6f));
                return;
            }
            StartCoroutine(TossRoutine());
        }

        private IEnumerator TossRoutine()
        {
            Vector3 from = player.transform.position + new Vector3(0f, 1f, 0f);
            Vector3 to = transform.position + new Vector3(Random.Range(-BasinHalfWidth, BasinHalfWidth), WaterHeight, 0f);
            var coin = FxUtil.MakeSprite("WishCoin", FxUtil.CoinSprite, from, Color.white, 40);
            coin.transform.localScale = Vector3.one * 1.3f;

            float peak = Mathf.Max(from.y, to.y) + 1.2f;
            for (float t = 0f; t < FlightTime; t += Time.deltaTime)
            {
                float k = t / FlightTime;
                float x = Mathf.Lerp(from.x, to.x, k);
                // 시작·끝 높이를 지나며 peak까지 솟는 포물선
                float y = Mathf.Lerp(from.y, to.y, k) + (peak - Mathf.Max(from.y, to.y)) * 4f * k * (1f - k);
                coin.transform.position = new Vector3(x, y, 0f);
                // 동전이 빙글빙글 도는 느낌(가로 폭을 줄였다 늘렸다)
                float spin = Mathf.Abs(Mathf.Cos(t * 22f));
                coin.transform.localScale = new Vector3(1.3f * Mathf.Max(0.15f, spin), 1.3f, 1f);
                yield return null;
            }
            Destroy(coin.gameObject);

            Splash(to);
            ProceduralAudio.PlayHit(HitSound.Normal);
            yield return new WaitForSeconds(0.35f);

            if (Random.value < JackpotChance) Jackpot(to);
            else ShowLine(PickLine(), LineColor);
        }

        private void Splash(Vector3 at)
        {
            FxUtil.Burst(at, new FxUtil.BurstSpec
            {
                count = 10, lifeMin = 0.3f, lifeMax = 0.55f, speedMin = 1f, speedMax = 2.2f,
                sizeMin = 0.05f, sizeMax = 0.09f, sizeEnd = 0.4f, gravity = 5f, drag = 0.5f,
                arcStart = 60f, arc = 60f, radius = 0.05f,
                colorA = WaterA, colorB = WaterB, texture = FxUtil.SoftCircle, sortingOrder = 41,
            });
            // 수면에 퍼지는 물결
            FxUtil.Burst(at, new FxUtil.BurstSpec
            {
                count = 1, lifeMin = 0.6f, lifeMax = 0.6f, speedMin = 0f, speedMax = 0f,
                sizeMin = 0.2f, sizeMax = 0.2f, sizeEnd = 4f,
                colorA = new Color(0.85f, 0.9f, 1f, 0.6f), colorB = new Color(0.85f, 0.9f, 1f, 0.6f),
                texture = FxUtil.Ring, sortingOrder = 41,
            });
        }

        private void Jackpot(Vector3 water)
        {
            player.AddGold(JackpotGold);
            LootFx.SpawnGold(water + Vector3.up * 0.3f, player.transform.position.y, JackpotGold);
            FxUtil.Burst(water + Vector3.up * 0.5f, new FxUtil.BurstSpec
            {
                count = 30, lifeMin = 0.6f, lifeMax = 1.2f, speedMin = 1.5f, speedMax = 4f,
                sizeMin = 0.12f, sizeMax = 0.24f, sizeEnd = 0.1f, gravity = 1f, drag = 2f,
                arcStart = 20f, arc = 140f, radius = 0.3f,
                colorA = new Color(1f, 0.95f, 0.6f), colorB = JackpotColor,
                texture = FxUtil.Sparkle, sortingOrder = 42,
            });
            if (!SfxPlayer.TryPlay(SfxPlayer.ItemPickup)) ProceduralAudio.PlayHit(HitSound.Critical);
            ShowLine(JackpotLine, JackpotColor);
            GameBootstrap.SaveGame();
        }

        private string PickLine()
        {
            int i = Random.Range(0, WishLines.Length);
            if (i == lastLine) i = (i + 1 + Random.Range(0, WishLines.Length - 1)) % WishLines.Length;
            lastLine = i;
            return WishLines[i];
        }

        // 분수 위에 떠올랐다 사라지는 글자. 연달아 던지면 앞의 글자는 지운다(겹쳐 읽기 어렵지 않게).
        private GameObject currentLine;

        private void ShowLine(string text, Color color)
        {
            if (currentLine != null) Destroy(currentLine);
            var go = new GameObject("WishLine");
            go.transform.position = transform.position + Vector3.up * 2.9f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = 0.07f;
            tm.fontSize = 40;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sortingOrder = 60;
            go.AddComponent<FloatingTextFade>();
            currentLine = go;
        }
    }
}

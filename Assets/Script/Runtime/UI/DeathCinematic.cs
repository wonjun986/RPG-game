using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // 네크로시아의 죽음 연출 화면. 플레이어가 쓰러졌을 때(DeathReturnUI)와 타이틀 화면 EXTRAS의
    // "데스 모션 보기"에서 같은 연출을 재생한다.
    // 연출 순서: 화면이 붉게 물들며 네크로시아 실루엣이 피격당해 흔들리고 → 무릎 꿇고 엎어지는 다이 모션 →
    // 암전 → 게임오버 로고(UI/GameOverLogo)가 떠오르고 → Z/Enter를 누르면 Play가 끝난다.
    // 다이 모션(UI/NecrosiaDieMotion/die_01~08)은 원본 시트(Sprite/SoulEater Sprite/Motions/necrosia_die_motion.png,
    // 빨간 배경 위 검은 실루엣 4x2칸)에서 빨간 배경을 투명하게 빼고 한 장씩 잘라, 같은 크기 캔버스에 발밑(아래쪽)을
    // 맞춰 둔 것이다 — 뒤의 붉은 배경(DeathRed)이 그대로 비쳐 원본과 똑같이 보인다.
    // 모든 타이밍은 실시간으로 재므로 timeScale이 0이어도 그대로 재생된다.
    public class DeathCinematic : MonoBehaviour
    {
        private static readonly Color DeathRed = new Color(251f / 255f, 1f / 255f, 1f / 255f);

        private const float RedFadeDuration = 0.12f;
        private const float HitFlashDuration = 0.25f;
        private const float HitShakeDuration = 0.6f;
        private const float HitShakeAmplitude = 28f;
        private const float HitShakeFrequency = 60f;
        private const float HitPunchScale = 1.06f;
        private const float BeforeFallPause = 0.25f;
        // 다이 모션 2번째 프레임부터 한 장씩 넘긴다. 쓰러질수록 조금씩 느려져 털썩 주저앉는 느낌을 낸다.
        private const float FallFrameDuration = 0.12f;
        private const float FallFrameSlowdown = 0.02f;
        private const float AfterFallHold = 0.9f;
        private const float BlackoutDuration = 0.8f;
        private const float LogoFadeDuration = 1.0f;
        private const float LogoStartScale = 1.15f;
        private const float PromptDelay = 0.5f;
        private const float PromptBlinkSpeed = 3f;

        // 캔버스(1280x720 기준) 위에서의 프레임 크기와, 프레임 아래쪽 가운데(발밑)가 놓이는 위치.
        private const float FigureHeight = 560f;
        private static readonly Vector2 FigureFeetPosition = new Vector2(0f, -300f);

        private Texture2D[] dieFrames = System.Array.Empty<Texture2D>();

        private Image redBackdrop;
        private RawImage figure;
        private RectTransform figureRect;
        private Image hitFlash;
        private Image blackout;
        private RawImage logo;
        private RectTransform logoRect;
        private Text prompt;

        public bool IsShowing => gameObject.activeSelf;
        // Play(allowAlternate: true)에서 Z/Enter 대신 X를 눌러 끝냈는지(쓰러졌을 때: Z = 재도전, X = 마을로).
        public bool AlternateChosen { get; private set; }

        // parent(캔버스) 아래에 화면 전체를 덮는 연출 패널을 만든다. 처음에는 숨겨져 있다.
        // promptText: 마지막에 깜빡이는 안내 문구(예: "[Z] 마을로 돌아가기").
        public static DeathCinematic Create(Transform parent, string promptText)
        {
            var go = new GameObject("DeathCinematic", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            StretchFull(rect);
            var blocker = go.GetComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);
            blocker.raycastTarget = true; // 연출 중 뒤쪽 UI 클릭 차단

            var cinematic = go.AddComponent<DeathCinematic>();
            cinematic.Build(rect, promptText);
            go.SetActive(false);
            return cinematic;
        }

        private void Build(RectTransform rect, string promptText)
        {
            redBackdrop = CreateFullImage(rect, "RedBackdrop");

            var figureGO = new GameObject("NecrosiaDie", typeof(RectTransform), typeof(RawImage));
            figureRect = (RectTransform)figureGO.transform;
            figureRect.SetParent(rect, false);
            figureRect.anchorMin = new Vector2(0.5f, 0.5f);
            figureRect.anchorMax = new Vector2(0.5f, 0.5f);
            figureRect.pivot = new Vector2(0.5f, 0f);
            figure = figureGO.GetComponent<RawImage>();
            figure.raycastTarget = false;
            dieFrames = Resources.LoadAll<Texture2D>("UI/NecrosiaDieMotion");
            System.Array.Sort(dieFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
            float aspect = dieFrames.Length > 0 ? (float)dieFrames[0].width / dieFrames[0].height : 1f;
            figureRect.sizeDelta = new Vector2(FigureHeight * aspect, FigureHeight);

            hitFlash = CreateFullImage(rect, "HitFlash");
            blackout = CreateFullImage(rect, "Blackout");

            var logoGO = new GameObject("GameOverLogo", typeof(RectTransform), typeof(RawImage));
            logoRect = (RectTransform)logoGO.transform;
            logoRect.SetParent(rect, false);
            logoRect.anchorMin = new Vector2(0.5f, 0.5f);
            logoRect.anchorMax = new Vector2(0.5f, 0.5f);
            logoRect.anchoredPosition = new Vector2(0f, 40f);
            logo = logoGO.GetComponent<RawImage>();
            logo.raycastTarget = false;
            var logoTexture = Resources.Load<Texture2D>("UI/GameOverLogo");
            logo.texture = logoTexture;
            float logoAspect = logoTexture != null ? (float)logoTexture.width / logoTexture.height : 3f;
            logoRect.sizeDelta = new Vector2(900f, 900f / logoAspect);

            var promptGO = new GameObject("Prompt", typeof(RectTransform), typeof(Text));
            var promptRect = (RectTransform)promptGO.transform;
            promptRect.SetParent(rect, false);
            promptRect.anchorMin = new Vector2(0f, 0.2f);
            promptRect.anchorMax = new Vector2(1f, 0.28f);
            promptRect.offsetMin = Vector2.zero;
            promptRect.offsetMax = Vector2.zero;
            prompt = promptGO.GetComponent<Text>();
            prompt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            prompt.fontSize = 26;
            prompt.fontStyle = FontStyle.Bold;
            prompt.alignment = TextAnchor.MiddleCenter;
            prompt.raycastTarget = false;
            prompt.text = promptText;
        }

        // 연출 화면을 처음 상태(아무것도 안 보이는 투명 화면)로 되돌려 맨 앞에 띄운다.
        public void Show()
        {
            redBackdrop.color = WithAlpha(DeathRed, 0f);
            hitFlash.color = WithAlpha(Color.white, 0f);
            blackout.color = WithAlpha(Color.black, 0f);
            figure.enabled = false;
            figure.texture = dieFrames.Length > 0 ? dieFrames[0] : null;
            figureRect.anchoredPosition = FigureFeetPosition;
            figureRect.localScale = Vector3.one;
            logo.enabled = false;
            logo.color = WithAlpha(Color.white, 0f);
            prompt.enabled = false;

            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // Show() 뒤에 호출한다. 연출을 끝까지 재생하고 Z/Enter 입력까지 기다린다(화면은 그대로 남는다).
        // allowAlternate면 X로도 끝낼 수 있고, 그때는 AlternateChosen이 true가 된다.
        public IEnumerator Play(bool allowAlternate = false)
        {
            AlternateChosen = false;
            // 1) 붉은 화면 + 피격 섬광, 실루엣(다이 모션 첫 프레임)이 맞은 충격으로 흔들린다.
            figure.enabled = figure.texture != null;
            yield return Animate(RedFadeDuration, p => redBackdrop.color = WithAlpha(DeathRed, p));
            yield return Animate(HitShakeDuration, p =>
            {
                float decay = 1f - p;
                float offsetX = Mathf.Sin(p * HitShakeDuration * HitShakeFrequency) * HitShakeAmplitude * decay;
                figureRect.anchoredPosition = FigureFeetPosition + new Vector2(offsetX, 0f);
                figureRect.localScale = Vector3.one * Mathf.Lerp(HitPunchScale, 1f, p);
                float flash = Mathf.Clamp01(1f - p * HitShakeDuration / HitFlashDuration);
                hitFlash.color = new Color(1f, 1f, 1f, 0.8f * flash);
            });
            figureRect.anchoredPosition = FigureFeetPosition;
            hitFlash.color = WithAlpha(Color.white, 0f);
            yield return new WaitForSecondsRealtime(BeforeFallPause);

            // 2) 다이 모션: 비틀거리다 무릎을 꿇고 바닥에 엎어진다. 마지막 프레임에서 멈춘다.
            for (int i = 1; i < dieFrames.Length; i++)
            {
                figure.texture = dieFrames[i];
                yield return new WaitForSecondsRealtime(FallFrameDuration + FallFrameSlowdown * (i - 1));
            }
            yield return new WaitForSecondsRealtime(AfterFallHold);

            // 3) 암전 후 게임오버 로고가 떠오른다.
            yield return Animate(BlackoutDuration, p => blackout.color = WithAlpha(Color.black, p));
            logo.enabled = true;
            yield return Animate(LogoFadeDuration, p =>
            {
                float eased = 1f - (1f - p) * (1f - p);
                logo.color = WithAlpha(Color.white, p);
                logoRect.localScale = Vector3.one * Mathf.Lerp(LogoStartScale, 1f, eased);
            });
            yield return new WaitForSecondsRealtime(PromptDelay);

            // 4) Z/Enter를 누를 때까지 안내 문구를 깜빡인다.
            prompt.enabled = true;
            float blink = 0f;
            while (!ConfirmPressed())
            {
                var keyboard = Keyboard.current;
                if (allowAlternate && keyboard != null && keyboard.xKey.wasPressedThisFrame)
                {
                    AlternateChosen = true;
                    break;
                }
                blink += Time.unscaledDeltaTime;
                prompt.color = new Color(0.9f, 0.85f, 0.95f, 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Cos(blink * PromptBlinkSpeed)));
                yield return null;
            }
        }

        private static bool ConfirmPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
        }

        // duration 동안 진행률(0→1)을 매 프레임 넘겨준다. 마지막에는 정확히 1로 한 번 더 부른다.
        private static IEnumerator Animate(float duration, System.Action<float> step)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                step(Mathf.Clamp01(t / duration));
                yield return null;
            }
            step(1f);
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image CreateFullImage(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            StretchFull(rect);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    }
}

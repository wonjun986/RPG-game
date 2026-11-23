using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Aethoria.Monsters;
using Aethoria.Save;
using Aethoria.Stages;

namespace Aethoria.UI
{
    // 게임 시작 시 표시되는 타이틀 화면(UI/Title.png). GAME START는 새 게임(빈 슬롯에 저장), CONTINUE는
    // 가장 최근에 저장된 슬롯에서 이어하기, EXIT는 게임 종료. 슬롯 5칸이 모두 차 있는데 GAME START를 누르면
    // 가장 오래된 슬롯을 덮어쓸지 한 번 더 묻는다.
    // 저장이 없으면 CONTINUE 글자를 어둡게 덮고, 눌러도 안내만 띄운다.
    // EXTRAS는 작은 창을 띄워 네크로시아의 죽음 연출(DeathCinematic)을 다시 보거나, 루나벨의 모션을 하나씩 돌려 볼 수 있게 한다(MotionGalleryUI).
    // SETTINGS는 화면 모드/해상도/프레임/볼륨/화면 흔들림/데미지 숫자를 바꾸는 설정 창을 띄운다(SettingsUI, GameSettings).
    public class TitleScreenUI : MonoBehaviour
    {
        private static readonly Vector2 ContinueMin = new Vector2(0.65f, 0.378f);
        private static readonly Vector2 ContinueMax = new Vector2(0.95f, 0.422f);
        private static readonly Vector2 SettingsMin = new Vector2(0.65f, 0.320f);
        private static readonly Vector2 SettingsMax = new Vector2(0.95f, 0.362f);
        private static readonly Vector2 ExtrasMin = new Vector2(0.65f, 0.264f);
        private static readonly Vector2 ExtrasMax = new Vector2(0.95f, 0.308f);

        // 십자가 커서를 더하면서 빛줄기는 옅게 깐다(글자 양옆 십자가가 주인공).
        private static readonly Color GlowColor = new Color(0.72f, 0.5f, 1f, 0.42f);

        // ---- 십자가 커서: 선택된 메뉴 글자 양옆에 로고의 십자가(Title_Cross, 1672x941 캔버스 속 x 1180~1359, y 72~371)를 작게 띄운다.
        private static readonly Rect CrossUv = new Rect(1180f / 1672f, 1f - 372f / 941f, 180f / 1672f, 300f / 941f);
        private static readonly Vector2 CursorCrossSize = new Vector2(20f, 34f); // 기준 해상도 1280x720
        private const float CursorCrossGap = 12f;      // 글자 끝에서 십자가까지
        private const float CursorSlideIn = 14f;       // 나타날 때 바깥에서 안쪽으로 미끄러져 들어오는 거리
        // 메뉴 글자(Title_MenuText) 각 줄의 좌우 끝(화면 비율). GAME START, CONTINUE, SETTINGS, EXTRAS, EXIT 순서.
        private static readonly Vector2[] MenuTextSpans =
        {
            new Vector2(0.7147f, 0.8200f), new Vector2(0.7255f, 0.8098f), new Vector2(0.7291f, 0.8074f),
            new Vector2(0.7380f, 0.7990f), new Vector2(0.7494f, 0.7871f),
        };
        private RectTransform[,] cursorCrosses;
        private RawImage[,] cursorCrossImages;
        private float[] cursorShow;
        private static readonly Vector2 GlowSize = new Vector2(268f, 34f); // 기준 해상도 1280x720. 원본(3:1)보다 납작하게 눌러 가는 줄로 보이게 한다
        private const float GlowAnchorX = (0.768f - 0.65f) / (0.95f - 0.65f); // 버튼 영역(0.65~0.95) 안에서 글자 중심 위치

        private Text alertText;
        private Button[] menuButtons;
        private bool zBlockedLastFrame;
        private RawImage[] menuGlows;
        private int selectedIndex;

        private SaveScreenUI loadScreen;
        private SettingsUI settingsUI;

        private GameObject extrasPanel;
        private Button deathMotionButton;
        private Button lunabelMotionButton;
        private Button knightMotionButton;
        private Button lunabelDefeatButton;
        private DeathCinematic deathCinematic;
        private MotionGalleryUI lunabelGallery;
        private MotionGalleryUI knightGallery;
        private LunabelDefeatReplayUI lunabelDefeatReplay;

        // onContinue: 불러오기 화면에서 고른 슬롯 번호(1~5)를 넘겨받는다.
        public void Initialize(UnityAction onGameStart, UnityAction<int> onContinue)
        {
            EnsureEventSystem();

            var canvasGO = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            // 원화와 그 위 연출은 한 묶음(Art)으로 둬서 시작 연출에서 함께 천천히 줌아웃한다.
            artRoot = NewStretchedRect("Art", canvasGO.transform);
            // 메뉴 글자를 지운 원화(Title_NoMenu) 위에 메뉴 글자 레이어를 따로 얹어, 시작 연출에서 메뉴만 나중에 떠오르게 한다.
            // (지운 그림이 없으면 예전처럼 글자가 박힌 원화를 쓴다.)
            bool hasCleanArt = Resources.Load<Texture2D>("UI/Title/Title_NoMenu") != null;
            // 로고 글자·십자가를 따로 뽑은 레이어가 다 있으면 로고 없는 배경 위에 둘을 얹어, 시작 연출에서 십자가를 꽂는다.
            var logoTexture = Resources.Load<Texture2D>("UI/Title/Title_LogoText");
            var crossTexture = Resources.Load<Texture2D>("UI/Title/Title_Cross");
            bool layeredLogo = logoTexture != null && crossTexture != null && Resources.Load<Texture2D>("UI/Title/Title_NoLogo") != null;
            BuildBackground(artRoot, layeredLogo ? "UI/Title/Title_NoLogo" : hasCleanArt ? "UI/Title/Title_NoMenu" : "UI/Title/Title");
            if (layeredLogo)
            {
                logoLayer = BuildLayer(artRoot, "LogoText", logoTexture, LogoCenter);
                for (int g = 0; g < crossGhosts.Length; g++) crossGhosts[g] = BuildLayer(artRoot, "CrossGhost", crossTexture, CrossCenter);
                crossLayer = BuildLayer(artRoot, "Cross", crossTexture, CrossCenter);
                impactFxRoot = NewStretchedRect("ImpactFx", artRoot);
            }
            // 로고 맥동·빛줄기, 달빛, 흩날리는 꽃잎, 피어오르는 불씨, 흐르는 안개(메뉴 버튼/글자보다 아래).
            ambience = TitleAmbience.Create(artRoot);

            // 메뉴 버튼·선택 빛·메뉴 글자·CONTINUE 어둡게 덮기를 한 묶음으로 둬서 한꺼번에 나타나게 한다.
            var menuRect = NewStretchedRect("Menu", canvasGO.transform);
            menuGroup = menuRect.gameObject.AddComponent<CanvasGroup>();
            var menuParent = (Transform)menuRect;

            bool hasSave = SaveSystem.HasSave;

            UnityAction startNew = () =>
            {
                Destroy(gameObject);
                onGameStart?.Invoke();
            };

            // 아래 앵커 좌표는 Title.png 안에 이미 그려진 "GAME START"/"CONTINUE"/"EXIT" 글자 위치에 맞춘 값이다.
            var gameStartButton = BuildButton(menuParent, "GameStartButton", new Vector2(0.65f, 0.433f), new Vector2(0.95f, 0.477f), () =>
            {
                // 빈 슬롯이 있으면 거기서 새로 시작하므로 기존 저장은 건드리지 않는다. 모든 슬롯이 차 있을 때만 묻는다.
                // 새로 시작하면 마을에 들어가는 순간 가장 오래된 슬롯이 자동 저장으로 덮어써진다(공통 선택 팝업으로 묻는다).
                if (!SaveSystem.HasEmptySlot)
                {
                    ChoicePopup.Ask("저장 슬롯이 모두 찼습니다.", () => startNew(), confirmLabel: "새로 시작", cancelLabel: "취소",
                        subMessage: $"새로 시작하면 가장 오래된 슬롯({SaveSystem.SlotForNewGame()}번)에 덮어써집니다.");
                }
                else startNew();
            });
            var continueButton = BuildButton(menuParent, "ContinueButton", ContinueMin, ContinueMax, () =>
            {
                if (SaveSystem.HasSave) OpenLoadScreen();
                else ShowAlert("저장된 데이터가 없습니다.");
            });
            var settingsButton = BuildButton(menuParent, "SettingsButton", SettingsMin, SettingsMax, OpenSettings);
            var extrasButton = BuildButton(menuParent, "ExtrasButton", ExtrasMin, ExtrasMax, OpenExtras);
            var exitButton = BuildButton(menuParent, "ExitButton", new Vector2(0.65f, 0.205f), new Vector2(0.95f, 0.255f), Application.Quit);

            SetupMenuSelection(new[] { gameStartButton, continueButton, settingsButton, extrasButton, exitButton }, hasSave ? 1 : 0);
            // 메뉴 글자만 따로 뽑은 레이어(UI/Title_MenuText.png)를 빛 위에 다시 덮어 글자가 가려지지 않게 한다.
            BuildBackground(menuParent, "UI/Title/Title_MenuText").GetComponent<RawImage>().raycastTarget = false;

            if (!hasSave) BuildContinueDimmer(menuParent);
            BuildAlert(canvasGO.transform);
            BuildExtrasDialog(canvasGO.transform);
            deathCinematic = DeathCinematic.Create(canvasGO.transform, "[Z] 돌아가기");
            lunabelGallery = MotionGalleryUI.Create(canvasGO.transform, "루나벨 모션", BuildLunabelClips());
            lunabelGallery.Closed += () =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(lunabelMotionButton.gameObject);
            };
            knightGallery = MotionGalleryUI.Create(canvasGO.transform, "기사 모션", BuildKnightClips());
            knightGallery.Closed += () =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(knightMotionButton.gameObject);
            };
            lunabelDefeatReplay = LunabelDefeatReplayUI.Create(canvasGO.transform);
            settingsUI = SettingsUI.Create(canvasGO.transform);

            // CONTINUE를 누르면 노아의 저장 화면과 같은 슬롯 창을 불러오기 모드로 띄운다.
            loadScreen = canvasGO.AddComponent<SaveScreenUI>();
            loadScreen.InitializeForLoad(slot =>
            {
                Destroy(gameObject);
                onContinue?.Invoke(slot);
            });

            // 시작 연출: 게임을 켠 뒤 처음 한 번은 전체 연출, 게임 중 타이틀로 돌아오면 짧게 밝아지기만 한다.
            BuildIntroCurtain(canvasGO.transform);
            introRoutine = StartCoroutine(PlayIntro(!introShownThisRun));
        }

        // ------------------------------------------------------------------ 시작 연출
        //  1) 검은 화면에 스튜디오 이름이 떠올랐다 사라진다.
        //  2) 검은 화면 한가운데에 로고("Black Requiem")가 떠오른다(로고 레이어가 있을 때).
        //  3) 로고가 오른쪽 제자리로 미끄러져 가는 동안 원화가 어둠 속에서 밝아지며, 살짝 확대된 상태에서 제자리로 줌아웃한다.
        //     로고가 자리에 닿으면 십자가가 위에서 내리꽂힌다(꽃잎·불씨는 처음부터 흐른다).
        //  4) 메뉴가 아래에서 살짝 떠오르며 나타난다.
        // 아무 키나 마우스 클릭으로 바로 끝 상태로 건너뛴다. 연출 중에는 메뉴를 누를 수 없다.
        private static bool introShownThisRun;

        // 이 프로젝트는 플레이 모드에 들어갈 때 도메인 리로드를 끄고 있어서(Enter Play Mode Options) static 값이 이전 플레이에서
        // 그대로 남는다. 플레이를 새로 시작할 때마다 지워서 처음 켤 때의 전체 연출이 다시 나오게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetIntroFlag() => introShownThisRun = false;
        private const float IntroStartScale = 1.12f;
        private const float MenuRiseOffset = 16f;

        private RectTransform artRoot;
        private TitleAmbience ambience;
        private CanvasGroup menuGroup;
        private Image introCurtain;
        private Text studioText;
        private Coroutine introRoutine;
        private bool introPlaying;
        private float introStartedAt;
        private int skipFrame = -1;

        // ---- 로고 레이어(로고 글자·십자가를 따로 뽑은 그림, 원화와 같은 1672x941 캔버스에 제자리로 그려 둠)
        // 화면 비율 좌표(왼쪽 아래 기준). 글자는 가운데 기준으로 커졌다 작아지고, 십자가는 꽂히는 자리 기준으로 흔들린다.
        private static readonly Vector2 LogoCenter = new Vector2(1256f / 1672f, 1f - 292f / 941f);
        private static readonly Vector2 CrossCenter = new Vector2(1270f / 1672f, 1f - 222f / 941f);
        private static readonly Vector2 CrossImpactPoint = new Vector2(1270f / 1672f, 0.675f);
        private const float CrossDropHeight = 430f;   // 기준 해상도(1280x720) 단위
        private static readonly Color SparkColorA = new Color(0.86f, 0.62f, 1f, 1f);
        private static readonly Color SparkColorB = new Color(1f, 0.95f, 1f, 1f);

        private RawImage logoLayer;
        private RawImage crossLayer;
        private readonly RawImage[] crossGhosts = new RawImage[2];
        private RectTransform impactFxRoot;
        private Image impactFlash;

        private static RawImage BuildLayer(Transform parent, string name, Texture2D texture, Vector2 pivot)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform), typeof(RawImage)).transform;
            rect.SetParent(parent, false);
            rect.pivot = pivot;   // 가장자리(offset)를 정하기 전에 기준점을 잡아야 자리가 밀리지 않는다
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        private static void SetLayerAlpha(RawImage image, float alpha)
        {
            var c = image.color;
            c.a = alpha;
            image.color = c;
        }

        // 십자가가 위에서 빛 꼬리를 끌며 떨어져 글자 가운데에 꽂힌다 → 섬광·화면 흔들림·파편·충격파 →
        // 반동으로 살짝 튀어 올랐다가 제자리에 박힌다.
        // shake: 꽂힐 때 흔들 묶음(시작 연출에서는 한가운데의 로고 묶음). 그 자리를 기준으로 흔들고 제자리에 돌려놓는다.
        private IEnumerator CrossStrike(RectTransform shake)
        {
            Vector2 shakeBase = shake.anchoredPosition;
            var cross = crossLayer.rectTransform;
            SetLayerAlpha(crossLayer, 1f);
            yield return Tween(0.3f, k =>
            {
                float e = k * k * k;                       // 점점 빨라진다
                float y = CrossDropHeight * (1f - e);
                cross.anchoredPosition = new Vector2(0f, y);
                for (int g = 0; g < crossGhosts.Length; g++)
                {
                    crossGhosts[g].rectTransform.anchoredPosition = new Vector2(0f, y + (g + 1) * 70f * (0.3f + e));
                    SetLayerAlpha(crossGhosts[g], 0.38f / (g + 1));
                }
            });

            // 꽂힌 순간
            foreach (var ghost in crossGhosts) SetLayerAlpha(ghost, 0f);
            cross.anchoredPosition = Vector2.zero;
            if (ambience != null) ambience.FlashLogo();
            if (impactFlash == null)
            {
                var flashRect = NewStretchedRect("ImpactFlash", artRoot.parent);
                flashRect.SetSiblingIndex(artRoot.GetSiblingIndex() + 1); // 원화 바로 위, 메뉴보다 아래
                impactFlash = flashRect.gameObject.AddComponent<Image>();
                impactFlash.raycastTarget = false;
            }
            impactFlash.gameObject.SetActive(true);
            SpawnImpactFx();

            var logoRect = logoLayer.rectTransform;
            yield return Tween(0.6f, k =>
            {
                float decay = (1f - k) * (1f - k);
                shake.anchoredPosition = shakeBase + Random.insideUnitCircle * 12f * decay;
                impactFlash.color = new Color(0.95f, 0.88f, 1f, 0.38f * decay * decay);
                logoRect.localScale = Vector3.one * (1f + 0.03f * decay);
                // 반동: 앞 60% 동안 살짝 튀어 올랐다가 다시 박힌다.
                float r = Mathf.Clamp01(k / 0.6f);
                cross.anchoredPosition = new Vector2(0f, 18f * Mathf.Sin(Mathf.PI * r) * (1f - r * 0.4f));
            });
            shake.anchoredPosition = shakeBase;
            cross.anchoredPosition = Vector2.zero;
            logoRect.localScale = Vector3.one;
            impactFlash.gameObject.SetActive(false);
        }

        // 꽂힌 자리에서 보랏빛·흰 파편이 튀고, 충격파 고리가 퍼진다.
        private void SpawnImpactFx()
        {
            var ring = new GameObject("ImpactRing", typeof(RectTransform), typeof(Image), typeof(TitleImpactRing));
            var ringRect = (RectTransform)ring.transform;
            ringRect.SetParent(impactFxRoot, false);
            ringRect.anchorMin = ringRect.anchorMax = CrossImpactPoint;
            ringRect.sizeDelta = new Vector2(120f, 60f);   // 납작한 고리(바닥에 퍼지는 느낌)
            var ringImage = ring.GetComponent<Image>();
            ringImage.sprite = Aethoria.Fx.FxUtil.RingSprite;
            ringImage.color = SparkColorA;
            ringImage.raycastTarget = false;

            for (int i = 0; i < 22; i++)
            {
                var go = new GameObject("Spark", typeof(RectTransform), typeof(Image), typeof(TitleSpark));
                var rect = (RectTransform)go.transform;
                rect.SetParent(impactFxRoot, false);
                rect.anchorMin = rect.anchorMax = CrossImpactPoint;
                float size = Random.Range(5f, 13f);
                rect.sizeDelta = new Vector2(size, size);
                var image = go.GetComponent<Image>();
                image.sprite = i % 3 == 0 ? Aethoria.Fx.FxUtil.SparkleSprite : Aethoria.Fx.FxUtil.SoftCircleSprite;
                image.color = Color.Lerp(SparkColorA, SparkColorB, Random.value);
                image.raycastTarget = false;
                float angle = Random.Range(15f, 165f) * Mathf.Deg2Rad;   // 위쪽 반원으로 튄다
                float speed = Random.Range(160f, 420f);
                go.GetComponent<TitleSpark>().Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            }
        }

        // 꽂힘 파편: 위로 튀었다가 중력으로 떨어지며 사라진다(실제 시간).
        private class TitleSpark : MonoBehaviour
        {
            public Vector2 Velocity;
            private float age;
            private const float Life = 0.75f;
            private Image image;
            private RectTransform rect;

            private void Awake()
            {
                image = GetComponent<Image>();
                rect = (RectTransform)transform;
            }

            private void Update()
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                age += dt;
                Velocity += new Vector2(0f, -620f) * dt;
                Velocity *= 1f - 1.5f * dt;
                rect.anchoredPosition += Velocity * dt;
                var c = image.color;
                c.a = Mathf.Clamp01(1f - age / Life);
                image.color = c;
                if (age >= Life) Destroy(gameObject);
            }
        }

        // 충격파 고리: 빠르게 커지며 옅어진다.
        private class TitleImpactRing : MonoBehaviour
        {
            private float age;
            private const float Life = 0.5f;
            private Image image;

            private void Awake() => image = GetComponent<Image>();

            private void Update()
            {
                age += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float k = Mathf.Clamp01(age / Life);
                transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 4f, 1f - (1f - k) * (1f - k));
                var c = image.color;
                c.a = 0.8f * (1f - k);
                image.color = c;
                if (age >= Life) Destroy(gameObject);
            }
        }

        private static RectTransform NewStretchedRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private void BuildIntroCurtain(Transform canvas)
        {
            var curtainRect = NewStretchedRect("IntroCurtain", canvas);
            introCurtain = curtainRect.gameObject.AddComponent<Image>();
            introCurtain.color = Color.black;
            introCurtain.raycastTarget = true; // 연출 중 클릭이 메뉴로 새지 않게 막는다

            var textRect = NewStretchedRect("Studio", curtainRect);
            studioText = textRect.gameObject.AddComponent<Text>();
            studioText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            studioText.fontSize = 26;
            studioText.alignment = TextAnchor.MiddleCenter;
            studioText.supportRichText = true;
            studioText.raycastTarget = false;
            studioText.lineSpacing = 1.6f;
            // <color> 태그는 글자 투명도를 덮어써서 페이드가 안 먹으므로 크기만 바꾼다.
            studioText.text = "S T U D I O    N O C T U R N E\n<size=14>p r e s e n t s</size>";
            studioText.color = new Color(0.86f, 0.8f, 0.95f, 0f);
        }

        private IEnumerator PlayIntro(bool full)
        {
            introPlaying = true;
            introStartedAt = Time.unscaledTime;
            menuGroup.alpha = 0f;
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            var menuRect = (RectTransform)menuGroup.transform;

            if (full)
            {
                artRoot.localScale = Vector3.one * IntroStartScale;
                if (logoLayer != null)
                {
                    // 로고 레이어가 있으면 글자·십자가는 처음엔 숨겨 둔다(아래에서 차례로 드러낸다).
                    SetLayerAlpha(logoLayer, 0f);
                    SetLayerAlpha(crossLayer, 0f);
                    foreach (var ghost in crossGhosts) SetLayerAlpha(ghost, 0f);
                    if (ambience != null) ambience.SetLogoGlowHidden(true);
                }
                yield return Wait(0.3f);
                // 스튜디오 이름: 떠오르고 → 머물고 → 사라진다(짧게).
                yield return Tween(0.5f, SetStudioAlpha);
                yield return Wait(0.6f);
                yield return Tween(0.4f, k => SetStudioAlpha(1f - k));
                yield return Wait(0.1f);

                if (logoLayer != null)
                {
                    // 로고 레이어가 있으면(속도감 있게):
                    //  검은 화면 한가운데에 글자가 확 떠오름 → 십자가가 한가운데에 쾅 꽂힘 → 글자·십자가가 오른쪽 제자리로 빠르게
                    //  날아감 → 그다음 일러스트가 어둠 속에서 드러난다.
                    // 글자·십자가·파편은 검은 막보다 위에 보여야 해서, 그동안만 막 위의 층(IntroLogo) 안 한 묶음(Group)으로 옮긴다.
                    BeginIntroLogoGroup(out Vector2 centerOffset);
                    var group = introLogoGroup;

                    // 한가운데: 크게 → 제 크기로 빠르게 박히듯 나타난다.
                    yield return Tween(0.45f, k =>
                    {
                        float e = 1f - (1f - k) * (1f - k) * (1f - k);
                        SetLayerAlpha(logoLayer, Mathf.Clamp01(k * 1.6f));
                        group.localScale = Vector3.one * Mathf.Lerp(IntroLogoCenterScale * 1.35f, IntroLogoCenterScale, e);
                    });
                    yield return Wait(0.15f);

                    yield return CrossStrike(group);
                    yield return Wait(0.2f);

                    // 오른쪽으로 빠르게: 살짝 뒤로 당겼다가(예비 동작) 휙 날아가 제자리에 멈춘다.
                    yield return Tween(0.5f, k =>
                    {
                        float back = k < 0.25f ? Mathf.Sin(k / 0.25f * Mathf.PI) * 0.04f : 0f;
                        float t = Mathf.Clamp01((k - 0.15f) / 0.85f);
                        float move = 1f - Mathf.Pow(1f - t, 4f);
                        group.anchoredPosition = Vector2.LerpUnclamped(centerOffset, Vector2.zero, move - back);
                        group.localScale = Vector3.one * Mathf.Lerp(IntroLogoCenterScale, 1f, move);
                    });
                    RestoreLogoLayer();

                    // 일러스트가 드러난다(빠르게 밝아지며 살짝 줌아웃).
                    artRoot.localScale = Vector3.one * IntroRevealScale;
                    if (ambience != null) ambience.FlashLogo();
                    yield return Tween(0.7f, k =>
                    {
                        float zoom = 1f - Mathf.Pow(1f - k, 3f);
                        artRoot.localScale = Vector3.one * Mathf.Lerp(IntroRevealScale, 1f, zoom);
                        float reveal = k * k * (3f - 2f * k);
                        introCurtain.color = new Color(0f, 0f, 0f, 1f - reveal);
                    });
                }
                else
                {
                    // 원화가 밝아지며 줌아웃. 다 밝아질 즈음 로고가 번쩍인다.
                    bool flashed = false;
                    yield return Tween(4.2f, k =>
                    {
                        float zoom = 1f - Mathf.Pow(1f - k, 3f);
                        artRoot.localScale = Vector3.one * Mathf.Lerp(IntroStartScale, 1f, zoom);
                        float reveal = Mathf.Clamp01(k / 0.55f);
                        introCurtain.color = new Color(0f, 0f, 0f, 1f - reveal * reveal * (3f - 2f * reveal));
                        if (!flashed && k >= 0.5f)
                        {
                            flashed = true;
                            if (ambience != null) ambience.FlashLogo();
                        }
                    });
                    yield return Wait(0.1f);
                }
            }
            else
            {
                artRoot.localScale = Vector3.one * 1.03f;
                yield return Tween(0.7f, k =>
                {
                    artRoot.localScale = Vector3.one * Mathf.Lerp(1.03f, 1f, 1f - (1f - k) * (1f - k));
                    introCurtain.color = new Color(0f, 0f, 0f, 1f - k);
                });
            }

            // 메뉴가 아래에서 떠오르며 나타난다.
            introCurtain.gameObject.SetActive(false);
            yield return Tween(full ? 0.5f : 0.4f, k =>
            {
                float e = 1f - (1f - k) * (1f - k);
                menuGroup.alpha = e;
                menuRect.anchoredPosition = new Vector2(0f, -MenuRiseOffset * (1f - e));
            });
            introRoutine = null;
            FinishIntro();
        }

        // ---- 시작 연출에서 로고 묶음(글자·십자가 잔상·십자가·파편)을 검은 막 위로 잠시 옮겨 둔다 ----
        private const float IntroLogoCenterScale = 1.15f;
        private const float IntroRevealScale = 1.06f;
        private RectTransform introLogoOverlay;
        private RectTransform introLogoGroup;
        private RectTransform[] logoGroupMembers;
        private int[] logoHomeIndices;

        private void BeginIntroLogoGroup(out Vector2 centerOffset)
        {
            introLogoOverlay = NewStretchedRect("IntroLogo", introCurtain.transform.parent);
            introLogoOverlay.SetAsLastSibling();
            // 섬광: 묶음 뒤에 깔아 검은 화면 위에서 번쩍인다(CrossStrike가 쓴다).
            var flashRect = NewStretchedRect("ImpactFlash", introLogoOverlay);
            impactFlash = flashRect.gameObject.AddComponent<Image>();
            impactFlash.raycastTarget = false;
            impactFlash.gameObject.SetActive(false);

            introLogoGroup = (RectTransform)new GameObject("Group", typeof(RectTransform)).transform;
            introLogoGroup.SetParent(introLogoOverlay, false);
            introLogoGroup.pivot = LogoCenter;   // 글자 가운데를 기준으로 커졌다 작아진다
            introLogoGroup.anchorMin = Vector2.zero;
            introLogoGroup.anchorMax = Vector2.one;
            introLogoGroup.offsetMin = introLogoGroup.offsetMax = Vector2.zero;

            // 원화 묶음 안의 순서를 기억해 두고(되돌릴 때 같은 자리로) 차례대로 옮긴다.
            var members = new System.Collections.Generic.List<RectTransform> { logoLayer.rectTransform };
            foreach (var ghost in crossGhosts) members.Add(ghost.rectTransform);
            members.Add(crossLayer.rectTransform);
            members.Add(impactFxRoot);
            members.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
            logoGroupMembers = members.ToArray();
            logoHomeIndices = new int[logoGroupMembers.Length];
            for (int i = 0; i < logoGroupMembers.Length; i++) logoHomeIndices[i] = logoGroupMembers[i].GetSiblingIndex();
            foreach (var m in logoGroupMembers) m.SetParent(introLogoGroup, false);

            centerOffset = Vector2.Scale(new Vector2(0.5f, 0.5f) - LogoCenter, introLogoOverlay.rect.size);
            introLogoGroup.anchoredPosition = centerOffset;
            introLogoGroup.localScale = Vector3.one * IntroLogoCenterScale;
        }

        private void RestoreLogoLayer()
        {
            if (introLogoOverlay == null) return;
            // 원래 순서가 작은 것부터 넣어야 앞의 것이 뒤의 자리를 밀어내지 않는다.
            for (int i = 0; i < logoGroupMembers.Length; i++)
            {
                var m = logoGroupMembers[i];
                m.SetParent(artRoot, false);
                m.SetSiblingIndex(logoHomeIndices[i]);
                m.anchoredPosition = Vector2.zero;
                m.localScale = Vector3.one;
            }
            Destroy(introLogoOverlay.gameObject);
            introLogoOverlay = null;
            introLogoGroup = null;
            impactFlash = null;
        }

        private void SetStudioAlpha(float k)
        {
            var c = studioText.color;
            c.a = k * k * (3f - 2f * k);
            studioText.color = c;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }

        // k를 0에서 1까지 실제 시간으로 올리며 매 프레임 apply를 부른다(마지막엔 꼭 1로 한 번).
        private static IEnumerator Tween(float seconds, System.Action<float> apply)
        {
            float start = Time.unscaledTime;
            while (true)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - start) / seconds);
                apply(k);
                if (k >= 1f) yield break;
                yield return null;
            }
        }

        // 연출을 끝 상태로 맞춘다(끝까지 봤을 때도, 건너뛰었을 때도).
        private void FinishIntro()
        {
            if (introRoutine != null) StopCoroutine(introRoutine);
            introRoutine = null;
            introPlaying = false;
            introShownThisRun = true;
            artRoot.localScale = Vector3.one;
            artRoot.anchoredPosition = Vector2.zero;
            introCurtain.gameObject.SetActive(false);
            if (logoLayer != null)
            {
                RestoreLogoLayer(); // 한가운데·이동 중에 건너뛰었으면 제자리로
                SetLayerAlpha(logoLayer, 1f);
                logoLayer.rectTransform.localScale = Vector3.one;
                logoLayer.rectTransform.anchoredPosition = Vector2.zero;
                SetLayerAlpha(crossLayer, 1f);
                crossLayer.rectTransform.anchoredPosition = Vector2.zero;
                foreach (var ghost in crossGhosts) SetLayerAlpha(ghost, 0f);
                if (impactFlash != null) impactFlash.gameObject.SetActive(false);
                for (int i = impactFxRoot.childCount - 1; i >= 0; i--) Destroy(impactFxRoot.GetChild(i).gameObject);
                if (ambience != null) ambience.SetLogoGlowHidden(false);
            }
            menuGroup.alpha = 1f;
            menuGroup.interactable = true;
            menuGroup.blocksRaycasts = true;
            ((RectTransform)menuGroup.transform).anchoredPosition = Vector2.zero;
            // 건너뛰는 데 쓴 키(Z·Enter)가 같은 프레임에 메뉴 버튼까지 누르지 않게, 선택은 다음 프레임 Update가 다시 잡는다.
            zBlockedLastFrame = true;
            skipFrame = Time.frameCount;
        }


        private void OpenLoadScreen()
        {
            // 불러오기 창이 떠 있는 동안 Enter가 뒤쪽 메뉴 버튼까지 누르지 않도록 선택을 비운다(보라색 표시는 남는다).
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            loadScreen.Open();
        }

        // SETTINGS: 설정 창을 띄우고, 닫히면 커서를 SETTINGS 항목에 그대로 둔다(다시 고르는 건 다음 프레임에 Update가 한다).
        private void OpenSettings()
        {
            if (settingsUI.IsOpen) return;
            settingsUI.Open(() =>
            {
                int index = System.Array.FindIndex(menuButtons, b => b.name == "SettingsButton");
                if (index >= 0) selectedIndex = index;
            });
        }

        // EXTRAS: 다시 볼 수 있는 연출 목록 창(데스 모션, 루나벨 모션). Esc/X 또는 "닫기"로 닫는다.
        private void BuildExtrasDialog(Transform parent)
        {
            extrasPanel = new GameObject("ExtrasDialog", typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)extrasPanel.transform;
            dimRect.SetParent(parent, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            extrasPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var boxGO = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var boxRect = (RectTransform)boxGO.transform;
            boxRect.SetParent(dimRect, false);
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(420f, 540f);
            boxGO.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.09f, 0.97f);

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
            var titleRect = (RectTransform)titleGO.transform;
            titleRect.SetParent(boxRect, false);
            titleRect.anchorMin = new Vector2(0f, 0.86f);
            titleRect.anchorMax = new Vector2(1f, 0.97f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleText = titleGO.GetComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.85f, 0.7f, 1f);
            titleText.text = "EXTRAS";

            WorldMapUI.BuildDialogButton(boxRect, "DeathMotionButton", "데스 모션 보기", new Vector2(0.12f, 0.71f), new Vector2(0.88f, 0.82f), PlayDeathMotion);
            WorldMapUI.BuildDialogButton(boxRect, "LunabelMotionButton", "루나벨 모션 보기", new Vector2(0.12f, 0.57f), new Vector2(0.88f, 0.68f), OpenLunabelGallery);
            WorldMapUI.BuildDialogButton(boxRect, "KnightMotionButton", "기사 모션 보기", new Vector2(0.12f, 0.43f), new Vector2(0.88f, 0.54f), OpenKnightGallery);
            WorldMapUI.BuildDialogButton(boxRect, "LunabelDefeatButton", "루나벨 처치 장면", new Vector2(0.12f, 0.29f), new Vector2(0.88f, 0.40f), PlayLunabelDefeat);
            WorldMapUI.BuildDialogButton(boxRect, "CloseButton", "닫기", new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.19f), CloseExtras);
            deathMotionButton = boxRect.Find("DeathMotionButton").GetComponent<Button>();
            lunabelMotionButton = boxRect.Find("LunabelMotionButton").GetComponent<Button>();
            knightMotionButton = boxRect.Find("KnightMotionButton").GetComponent<Button>();
            lunabelDefeatButton = boxRect.Find("LunabelDefeatButton").GetComponent<Button>();

            extrasPanel.SetActive(false);
        }

        private void OpenExtras()
        {
            extrasPanel.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(deathMotionButton.gameObject);
        }

        private void CloseExtras()
        {
            extrasPanel.SetActive(false);
            // 닫힌 창의 버튼에 선택이 남지 않게, 커서를 메뉴의 EXTRAS 항목으로 바로 돌려놓는다.
            int extrasIndex = System.Array.FindIndex(menuButtons, b => b.name == "ExtrasButton");
            if (extrasIndex >= 0) selectedIndex = extrasIndex;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(menuButtons[selectedIndex].gameObject);
        }

        private void PlayDeathMotion()
        {
            if (deathCinematic.IsShowing) return;
            StartCoroutine(PlayDeathMotionRoutine());
        }

        private IEnumerator PlayDeathMotionRoutine()
        {
            // 연출을 끝내는 Enter가 선택된 버튼까지 눌러 연출이 다시 시작되지 않도록 선택을 비워 둔다.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            deathCinematic.Show();
            yield return deathCinematic.Play();
            deathCinematic.Hide();
            yield return null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(deathMotionButton.gameObject);
        }

        private void OpenLunabelGallery()
        {
            if (lunabelGallery.IsShowing) return;
            // 창이 떠 있는 동안 Enter가 뒤쪽 EXTRAS 버튼을 다시 누르지 않도록 선택을 비운다.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            lunabelGallery.Show();
        }

        private void OpenKnightGallery()
        {
            if (knightGallery.IsShowing) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            knightGallery.Show();
        }

        private void PlayLunabelDefeat()
        {
            if (lunabelDefeatReplay.IsShowing) return;
            StartCoroutine(PlayLunabelDefeatRoutine());
        }

        private IEnumerator PlayLunabelDefeatRoutine()
        {
            // 대사를 넘기는 Enter가 선택된 버튼까지 눌러 장면이 다시 시작되지 않도록 선택을 비워 둔다.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            yield return lunabelDefeatReplay.Play();
            yield return null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(lunabelDefeatButton.gameObject);
        }

        // 기사들이 게임에서 쓰는 모션/이펙트. 보스 기사(어둠의 숲 보스)와 일반 기사. 게임처럼 왼쪽(플레이어 쪽)을 보게 뒤집는다.
        private static MotionGalleryUI.Clip[] BuildKnightClips()
        {
            const string boss = "Art/Monsters/BossKnight/";
            const string knight = "Art/Monsters/Knight/";
            return new[]
            {
                new MotionGalleryUI.Clip("알리아스 - 대기", 1f / 5f, true, false, true, boss + "Idle"),
                new MotionGalleryUI.Clip("알리아스 - 걷기", 0.12f, true, false, true, boss + "Walk"),
                new MotionGalleryUI.Clip("알리아스 - 공격", 0.08f, false, false, true, boss + "Attack"),
                new MotionGalleryUI.Clip("알리아스 - 점프 내려찍기", 0.08f, false, false, true, boss + "JumpAttack"),
                new MotionGalleryUI.Clip("알리아스 필살기 - 검 들어올리기", 0.12f, false, false, true, BossAI.SpecialRaisePath),
                new MotionGalleryUI.Clip("알리아스 필살기 - 기운 모으기", 0.1f, true, false, true, BossAI.SpecialChargePath),
                new MotionGalleryUI.Clip("알리아스 필살기 - 전체 모션", 0.1f, false, false, true,
                    BossAI.SpecialRaisePath, BossAI.SpecialChargePath, BossAI.SpecialChargePath),
                new MotionGalleryUI.Clip("알리아스 - 쓰러짐", 0.16f, false, false, true, boss + "Die"),
                new MotionGalleryUI.Clip("이펙트 - 검기", 0.07f, true, true, true, BossAI.SwordWavePath),
                new MotionGalleryUI.Clip("종막 - 검 꽂기", 0.16f, false, false, true, BossAI.FinalePlantPath),
                new MotionGalleryUI.Clip("종막 - 꽂은 뒤 대기", 0.2f, true, false, true, BossAI.FinalePlantIdlePath),
                new MotionGalleryUI.Clip("종막 - 마법진", 0.1f, true, true, false, AliasSwordRitual.CirclePath),
                new MotionGalleryUI.Clip("종막 - 마검", 0.3f, true, true, false, AliasSwordRitual.SwordPath),
                new MotionGalleryUI.Clip("종막 - 사슬 구속", 0.1f, true, true, false, AliasSwordRitual.ChainPath),
                new MotionGalleryUI.Clip("종막 - 참격", 0.06f, true, true, false, AliasSwordRitual.SlashPath),
                new MotionGalleryUI.Clip("일반 기사 - 걷기", 0.12f, true, false, true, knight + "Walk"),
                new MotionGalleryUI.Clip("일반 기사 - 공격", 0.1f, false, false, true, knight + "Attack"),
            };
        }

        // 루나벨이 게임에서 쓰는 모션/이펙트. 캐릭터 모션은 게임처럼 왼쪽(플레이어 쪽)을 보게 뒤집는다.
        private static MotionGalleryUI.Clip[] BuildLunabelClips()
        {
            const string root = "Art/Monsters/FinalBoss/Phase1/";
            return new[]
            {
                new MotionGalleryUI.Clip("첫 조우 - 의자에 앉은 모습", 1f / 3f, true, false, true, FinalBossAI.SeatedPath),
                new MotionGalleryUI.Clip("1페이즈 대기", 1f / 6f, true, false, true, root + "Idle"),
                new MotionGalleryUI.Clip("피의 손 뻗기 (공격)", 0.125f, false, false, true, root + "Attack"),
                new MotionGalleryUI.Clip("피격", 0.1f, false, false, true, FinalBossAI.HitPath),
                new MotionGalleryUI.Clip("혈류 흡수", 0.15f, false, false, true, FinalBossAI.AbsorbPath),
                new MotionGalleryUI.Clip("2페이즈 변신", 0.15f, false, false, true, FinalBossAI.PhaseTwoPath),
                new MotionGalleryUI.Clip("2페이즈 대기", 1f / 6f, true, false, true, FinalBossAI.PhaseTwoIdlePath),
                new MotionGalleryUI.Clip("2페이즈 공격", 1f / 8f, false, false, true, FinalBossAI.PhaseTwoAttackPath),
                new MotionGalleryUI.Clip("2페이즈 순간이동 - 소용돌이에 빨려 듦", 0.14f, false, false, false, FinalBossAI.PortalEnterPhaseTwoPath),
                new MotionGalleryUI.Clip("이펙트 - 2페이즈 소용돌이 닫힘", 0.1f, false, true, false, FinalBossAI.PortalCollapsePhaseTwoPath),
                new MotionGalleryUI.Clip("필살기 - 하늘로 손 뻗기", 0.14f, false, false, true, FinalBossAI.SpecialPath),
                new MotionGalleryUI.Clip("필살기 - 구체 모으기", 0.1f, true, false, true, FinalBossAI.SpecialChargePath),
                new MotionGalleryUI.Clip("필살기 - 구체 던지기", 0.1f, false, false, true, FinalBossAI.ThrowPath),
                new MotionGalleryUI.Clip("필살기 - 전체 모션", 0.1f, false, false, true,
                    FinalBossAI.SpecialPath, FinalBossAI.SpecialChargePath, FinalBossAI.SpecialChargePath, FinalBossAI.ThrowPath),
                new MotionGalleryUI.Clip("패배 - 쓰러지는 모션", LunabelDeathCutscene.DefeatFrameInterval, false, false, true, LunabelDeathCutscene.DefeatPath),
                new MotionGalleryUI.Clip("전투 시작 - BATTLE 연출", 0.09f, false, true, false, "Art/Effects/LunabelBattleSplash"),
                new MotionGalleryUI.Clip("이펙트 - 피의 회오리", 0.08f, true, true, false, FinalBossAI.BloodVortexPath),
                new MotionGalleryUI.Clip("이펙트 - 피의 손", 0.06f, true, true, true, BloodHandProjectile.FramesPath),
                new MotionGalleryUI.Clip("이펙트 - 핏빛 구체", 0.1f, true, true, false, FinalBossAI.BloodOrbPath),
                new MotionGalleryUI.Clip("이펙트 - 거대한 에너지 구체 (2페이즈 개막)", 0.08f, true, true, false, FinalBossAI.GiantOrbPath),
                new MotionGalleryUI.Clip("이펙트 - 피의 기둥", 0.07f, false, true, false,
                    FinalBossAI.BloodPillarRisePath, FinalBossAI.BloodPillarTornadoPath, FinalBossAI.BloodPillarTornadoPath, FinalBossAI.BloodPillarTornadoPath),
            };
        }

        // 저장이 없을 때 CONTINUE 글자 위를 반투명하게 덮어 비활성처럼 보이게 한다(클릭은 버튼이 받는다).
        private static void BuildContinueDimmer(Transform parent)
        {
            var go = new GameObject("ContinueDimmer", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = ContinueMin;
            rect.anchorMax = ContinueMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = new Color(0.03f, 0.02f, 0.05f, 0.6f);
            image.raycastTarget = false;
        }

        private void BuildAlert(Transform parent)
        {
            var go = new GameObject("TitleAlert", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.6f, 0.13f);
            rect.anchorMax = new Vector2(1f, 0.19f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            alertText = go.GetComponent<Text>();
            alertText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            alertText.fontSize = 22;
            alertText.fontStyle = FontStyle.Bold;
            alertText.alignment = TextAnchor.MiddleCenter;
            alertText.color = new Color(1f, 0.5f, 0.5f);
            alertText.raycastTarget = false;
            alertText.text = string.Empty;
        }

        private void ShowAlert(string message)
        {
            alertText.text = message;
            CancelInvoke(nameof(ClearAlert));
            Invoke(nameof(ClearAlert), 2f);
        }

        private void ClearAlert()
        {
            alertText.text = string.Empty;
        }

        private static GameObject BuildBackground(Transform parent, string resourcesPath)
        {
            var go = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(resourcesPath);
            return go;
        }

        // anchorMin/anchorMax: 화면 비율(0~1) 기준의 투명 버튼 영역. 배경 이미지 위에 그대로 겹친다.
        internal static Button BuildButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // 보이지 않지만 클릭/레이캐스트는 받는다
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            return button;
        }

        // 메뉴 선택: 방향키(위/아래)·마우스 호버로 항목을 고르고 Enter/클릭으로 실행한다.
        // 선택된 항목에는 보라색 빛이 맥동하며 남고, 빈 곳을 클릭해도 선택이 풀리지 않는다.
        private void SetupMenuSelection(Button[] buttons, int initialIndex)
        {
            menuButtons = buttons;
            menuGlows = new RawImage[buttons.Length];
            selectedIndex = initialIndex;

            var glowTexture = Resources.Load<Texture2D>("UI/Common/Select_Effect");
            var crossTexture = Resources.Load<Texture2D>("UI/Title/Title_Cross");
            cursorCrosses = new RectTransform[buttons.Length, 2];
            cursorCrossImages = new RawImage[buttons.Length, 2];
            cursorShow = new float[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                if (crossTexture != null && i < MenuTextSpans.Length) BuildCursorCrosses(buttons[i], i, crossTexture);
                var button = buttons[i];
                button.transition = Selectable.Transition.None;

                // 위/아래 이동은 Update가 직접 처리한다(MoveMenuSelection). EventSystem 자동 이동에 맡기면 EXTRAS 창을
                // 열고 닫은 뒤 이동이 멈춰 커서가 안 움직이는 일이 있었다.
                button.navigation = new Navigation { mode = Navigation.Mode.None };

                button.gameObject.AddComponent<SelectOnHover>();

                // 글자 뒤 빛: 십자가 커서가 있으면 양옆 십자가 사이를 덮는 은은한 보랏빛 번짐(부드러운 원)을 깔고,
                // 없으면 예전처럼 UI/Select_Effect.png(양 끝에 별빛이 있는 보라색 가로 줄)를 깐다.
                // Title.png의 메뉴 글자는 버튼 영역 가운데가 아니라 화면 x≈0.768에 있으므로 거기에 맞춘다.
                bool crossCursor = crossTexture != null && i < MenuTextSpans.Length;
                var glowGO = new GameObject("SelectGlow", typeof(RectTransform), typeof(RawImage));
                var glowRect = (RectTransform)glowGO.transform;
                glowRect.SetParent(button.transform, false);
                glowRect.pivot = new Vector2(0.5f, 0.5f);
                var glow = glowGO.GetComponent<RawImage>();
                if (crossCursor)
                {
                    var bRect = (RectTransform)button.transform;
                    float bSpan = bRect.anchorMax.x - bRect.anchorMin.x;
                    var text = MenuTextSpans[i];
                    glowRect.anchorMin = glowRect.anchorMax = new Vector2(((text.x + text.y) / 2f - bRect.anchorMin.x) / bSpan, 0.5f);
                    glowRect.sizeDelta = new Vector2((text.y - text.x) * 1280f + 2f * (CursorCrossGap + CursorCrossSize.x), 30f);
                    glow.texture = Aethoria.Fx.FxUtil.SoftCircle;
                }
                else
                {
                    glowRect.anchorMin = glowRect.anchorMax = new Vector2(GlowAnchorX, 0.5f);
                    glowRect.sizeDelta = GlowSize;
                    glow.texture = glowTexture;
                }
                glow.color = GlowColor;
                glow.raycastTarget = false;
                glowGO.SetActive(false);
                menuGlows[i] = glow;
            }

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(buttons[selectedIndex].gameObject);
        }

        // 버튼 영역(가로 0.65~0.95) 안에서 글자 왼쪽·오른쪽 끝 바로 바깥에 십자가 두 개를 만든다(처음엔 숨김).
        private void BuildCursorCrosses(Button button, int index, Texture2D crossTexture)
        {
            var buttonRect = (RectTransform)button.transform;
            float span = buttonRect.anchorMax.x - buttonRect.anchorMin.x;
            for (int side = 0; side < 2; side++)
            {
                float edge = side == 0 ? MenuTextSpans[index].x : MenuTextSpans[index].y;
                var go = new GameObject(side == 0 ? "CursorCrossL" : "CursorCrossR", typeof(RectTransform), typeof(RawImage));
                var rect = (RectTransform)go.transform;
                rect.SetParent(button.transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2((edge - buttonRect.anchorMin.x) / span, 0.5f);
                rect.pivot = new Vector2(side == 0 ? 1f : 0f, 0.5f);
                rect.sizeDelta = CursorCrossSize;
                var image = go.GetComponent<RawImage>();
                image.texture = crossTexture;
                image.uvRect = CrossUv;
                image.raycastTarget = false;
                image.color = new Color(1f, 1f, 1f, 0f);
                cursorCrosses[index, side] = rect;
                cursorCrossImages[index, side] = image;
            }
        }

        // 선택된 항목의 십자가는 바깥에서 안쪽으로 미끄러져 들어오며 나타나고, 은은하게 맥동하며 위아래로 살짝 떠 있다.
        // 선택이 풀린 항목의 십자가는 바깥으로 빠지며 사라진다.
        private void UpdateCursorCrosses()
        {
            if (cursorCrosses == null) return;
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            for (int i = 0; i < cursorShow.Length; i++)
            {
                if (cursorCrosses[i, 0] == null) continue;
                float target = i == selectedIndex ? 1f : 0f;
                cursorShow[i] = Mathf.MoveTowards(cursorShow[i], target, dt / 0.18f);
                float e = 1f - (1f - cursorShow[i]) * (1f - cursorShow[i]);
                float glow = 0.82f + 0.18f * Mathf.Sin(t * 4f);
                float bob = Mathf.Sin(t * 2.6f) * 1.5f * e;
                for (int side = 0; side < 2; side++)
                {
                    float dir = side == 0 ? -1f : 1f;   // 바깥쪽
                    cursorCrosses[i, side].anchoredPosition = new Vector2(dir * (CursorCrossGap + CursorSlideIn * (1f - e)), bob);
                    cursorCrossImages[i, side].color = new Color(1f, 1f, 1f, e * glow);
                }
            }
        }

        private void Update()
        {
            if (menuButtons == null) return;

            if (introPlaying)
            {
                // 아무 키나 클릭하면 건너뛴다(켜자마자 눌려 있던 입력은 무시하도록 아주 잠깐은 받지 않는다).
                var kb = Keyboard.current;
                var mouse = Mouse.current;
                bool skip = (kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
                if (skip && Time.unscaledTime - introStartedAt > 0.2f) FinishIntro();
                return;
            }
            if (Time.frameCount == skipFrame) return;

            bool extrasOpen = extrasPanel != null && extrasPanel.activeSelf;
            bool cinematicPlaying = (deathCinematic != null && deathCinematic.IsShowing)
                || (lunabelGallery != null && (lunabelGallery.IsShowing || lunabelGallery.ClosedThisFrame))
                || (knightGallery != null && (knightGallery.IsShowing || knightGallery.ClosedThisFrame))
                || (lunabelDefeatReplay != null && lunabelDefeatReplay.IsShowing);
            var keyboard = Keyboard.current;
            if (extrasOpen && !cinematicPlaying && keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame))
            {
                CloseExtras();
                extrasOpen = false;
            }

            // Z도 Enter처럼 고른 버튼을 누른다. 바로 전 프레임까지 다른 창(모션 재생·설정·불러오기·확인창)이 Z를 받고 있었으면
            // 그 창을 닫은 Z가 메뉴 버튼으로 이어지지 않게 이번 프레임은 건너뛴다.
            bool zPressed = keyboard != null && keyboard.zKey.wasPressedThisFrame && !zBlockedLastFrame;
            if (zPressed && extrasOpen && !cinematicPlaying && !ChoicePopup.ConsumesInput && EventSystem.current != null)
            {
                var selectedGO = EventSystem.current.currentSelectedGameObject;
                var selectedButton = selectedGO != null ? selectedGO.GetComponent<Button>() : null;
                if (selectedButton != null && selectedButton.interactable && selectedButton.gameObject.activeInHierarchy)
                    selectedButton.onClick.Invoke();
                zPressed = false;
            }

            var eventSystem = EventSystem.current;
            bool settingsOpen = settingsUI != null && (settingsUI.IsOpen || settingsUI.ClosedThisFrame);
            bool dialogOpen = ChoicePopup.ConsumesInput || (loadScreen != null && loadScreen.IsOpen) || extrasOpen
                || cinematicPlaying || settingsOpen;
            if (eventSystem != null && !dialogOpen)
            {
                int index = System.Array.FindIndex(menuButtons, b => b.gameObject == eventSystem.currentSelectedGameObject);
                if (index >= 0) selectedIndex = index;
                else eventSystem.SetSelectedGameObject(menuButtons[selectedIndex].gameObject); // 선택이 풀렸으면 마지막 항목을 다시 선택

                if (keyboard != null)
                {
                    if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) MoveMenuSelection(-1);
                    else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) MoveMenuSelection(1);
                    else if (zPressed && menuButtons[selectedIndex].interactable) menuButtons[selectedIndex].onClick.Invoke();
                }
            }
            zBlockedLastFrame = cinematicPlaying || settingsOpen || ChoicePopup.ConsumesInput || (loadScreen != null && loadScreen.IsOpen);

            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            for (int i = 0; i < menuGlows.Length; i++)
            {
                bool selected = i == selectedIndex;
                if (menuGlows[i].gameObject.activeSelf != selected) menuGlows[i].gameObject.SetActive(selected);
                if (selected) menuGlows[i].color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, GlowColor.a * pulse);
            }
            UpdateCursorCrosses();
        }

        // 메뉴 커서를 위(-1)/아래(+1)로 한 칸 옮긴다. 끝에서는 반대쪽 끝으로 넘어간다.
        private void MoveMenuSelection(int delta)
        {
            selectedIndex = (selectedIndex + delta + menuButtons.Length) % menuButtons.Length;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(menuButtons[selectedIndex].gameObject);
        }

        private class SelectOnHover : MonoBehaviour, IPointerEnterHandler
        {
            public void OnPointerEnter(PointerEventData eventData)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }

        internal static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}

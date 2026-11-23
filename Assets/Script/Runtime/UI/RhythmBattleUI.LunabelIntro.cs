using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Combat;
using Aethoria.Data;
using Aethoria.Monsters;

namespace Aethoria.UI
{
    // 루나벨전 등장 연출: 필드 인트로, 이름 카드, 시작 대사.
    public partial class RhythmBattleUI
    {
        // ---- 필드 루나벨전 등장 연출 ----
        // 리듬 전투는 이제 릴리의 미니게임에서만 열리므로, 필드 루나벨전에서는 등장 연출(LunabelIntroCutscene)이 가시 폭풍 뒤에
        // 이걸 불러 리듬 전투 화면의 연출 부분만 빌려 쓴다: 암전 → 루나벨 이름 카드 → 암전이 걷힌다.
        // 전투 UI(contentRoot)는 열지 않고, 월드 시간도 멈추지 않는다(연출이 조작과 보스 AI를 이미 잡아 두고 있다).
        private const float FieldIntroFadeOut = 0.35f;
        private bool playingFieldIntro;

        public IEnumerator PlayLunabelFieldIntro()
        {
            if (panel == null || IsOpen) yield break;
            playingFieldIntro = true;

            contentRoot.SetActive(false);
            battleSplashImage.gameObject.SetActive(false);
            Color baseColor = dimBackdrop.color;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();

            for (float t = 0f; t < BlackoutFadeDuration; t += Time.unscaledDeltaTime)
            {
                dimBackdrop.color = new Color(baseColor.r, baseColor.g, baseColor.b, t / BlackoutFadeDuration);
                yield return null;
            }
            dimBackdrop.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);

            yield return LunabelNameCard();

            for (float t = 0f; t < FieldIntroFadeOut; t += Time.unscaledDeltaTime)
            {
                dimBackdrop.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - t / FieldIntroFadeOut);
                yield return null;
            }
            panel.SetActive(false);
            dimBackdrop.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
            contentRoot.SetActive(true);
            playingFieldIntro = false;
        }

        // ---- 루나벨 이름 카드 ----
        // 루나벨전이 열리면 암전 직후, BATTLE 연출 전에 나온다: 화면 가득 루나벨 일러스트(UI/LunabelIntro, 16:9)가 서서히
        // 떠오르고, 아래쪽 어두운 띠 위 가운데에 핏빛 풀네임 "Lunabel Cruora"(Sprite/UI/Lunabel Curora를 글자별로 자른 UI/LunabelName/letter_01~13)가
        // 한 글자씩 튀어나오듯 찍힌다. 찍힌 글자는 저마다 따로 떨리듯 진동한다. 다 찍히면 잠깐 머문 뒤 사라진다. Z/Enter로 건너뛴다.
        // 글자 자리는 원래 이름 그림(2172x724) 속 픽셀 사각형(x0, y0, x1, y1) 그대로 놓는다.
        private const string LunabelIllustrationPath = "UI/Battle/LunabelIntro";
        private const string LunabelNameLetterPath = "UI/Battle/LunabelName/letter_";
        private const float NameImageWidth = 2172f, NameImageHeight = 724f;
        private static readonly Vector4[] LunabelNameLetterRects =
        {
            // Lunabel
            new Vector4(28f, 0f, 409f, 378f),
            new Vector4(391f, 73f, 693f, 385f),
            new Vector4(703f, 77f, 983f, 385f),
            new Vector4(1006f, 73f, 1277f, 378f),
            new Vector4(1299f, 0f, 1581f, 392f),
            new Vector4(1606f, 76f, 1870f, 387f),
            new Vector4(1932f, 0f, 2142f, 403f),
            // Cruora
            new Vector4(97f, 378f, 426f, 724f),
            new Vector4(467f, 421f, 727f, 724f),
            new Vector4(754f, 414f, 1068f, 724f),
            new Vector4(1073f, 409f, 1358f, 724f),
            new Vector4(1423f, 426f, 1682f, 724f),
            new Vector4(1733f, 412f, 2017f, 724f),
        };
        private const float NameCardFadeIn = 0.5f;
        private const float NameLetterInterval = 0.13f;
        private const float NameLetterPopTime = 0.12f;
        private const float NameLetterPopScale = 1.6f;
        private const float NameCardHold = 1.3f;
        private const float NameCardFadeOut = 0.4f;
        private const float NameShakeAmount = 4f;   // 글자 떨림 폭(캔버스 px)
        private const float NameShakeAngle = 2.5f;  // 글자 떨림 기울기(도)
        private const float NameShakeInterval = 0.03f;
        private CanvasGroup nameCardGroup;
        private RawImage nameCardIllustration;
        private AspectRatioFitter nameCardIllustrationFitter;
        private Image nameCardBand;
        private const float NameBandAlpha = 0.6f;
        private readonly List<RawImage> nameLetters = new List<RawImage>();

        private void BuildLunabelNameCard(RectTransform parent)
        {
            var cardGO = new GameObject("LunabelNameCard", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            var cardRect = (RectTransform)cardGO.transform;
            cardRect.SetParent(parent, false);
            cardRect.anchorMin = Vector2.zero;
            cardRect.anchorMax = Vector2.one;
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;
            var backdrop = cardGO.GetComponent<Image>();
            backdrop.color = new Color(0.08f, 0f, 0.02f, 1f);
            backdrop.raycastTarget = false;
            nameCardGroup = cardGO.GetComponent<CanvasGroup>();

            // 화면 가득: 16:9 일러스트(루나벨이 가운데 서 있다). 비율이 조금 달라도 빈틈 없이 덮게 EnvelopeParent.
            var illustGO = new GameObject("Illustration", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            var illustRect = (RectTransform)illustGO.transform;
            illustRect.SetParent(cardRect, false);
            nameCardIllustration = illustGO.GetComponent<RawImage>();
            nameCardIllustration.raycastTarget = false;
            nameCardIllustration.texture = Resources.Load<Texture2D>(LunabelIllustrationPath);
            nameCardIllustrationFitter = illustGO.GetComponent<AspectRatioFitter>();
            nameCardIllustrationFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            var tex = nameCardIllustration.texture;
            if (tex != null) nameCardIllustrationFitter.aspectRatio = (float)tex.width / tex.height;

            // 배경도 붉어서 핏빛 글자가 묻히지 않게, 이름 뒤 화면 아래쪽에 어두운 띠를 깐다(일러스트와 같이 떠오른다).
            var bandGO = new GameObject("NameBand", typeof(RectTransform), typeof(Image));
            var bandRect = (RectTransform)bandGO.transform;
            bandRect.SetParent(cardRect, false);
            bandRect.anchorMin = new Vector2(0f, 0f);
            bandRect.anchorMax = new Vector2(1f, 0.42f);
            bandRect.offsetMin = Vector2.zero;
            bandRect.offsetMax = Vector2.zero;
            nameCardBand = bandGO.GetComponent<Image>();
            nameCardBand.raycastTarget = false;

            // 아래쪽 가운데: 이름 글자를 놓을 3:1 칸(FitInParent는 부모 전체에 맞추므로, 자리 잡을 칸을 하나 두고 그 안에 맞춘다).
            var nameAreaGO = new GameObject("NameArea", typeof(RectTransform));
            var nameAreaRect = (RectTransform)nameAreaGO.transform;
            nameAreaRect.SetParent(cardRect, false);
            nameAreaRect.anchorMin = new Vector2(0.15f, 0.04f);
            nameAreaRect.anchorMax = new Vector2(0.85f, 0.4f);
            nameAreaRect.offsetMin = Vector2.zero;
            nameAreaRect.offsetMax = Vector2.zero;
            var nameGO = new GameObject("Name", typeof(RectTransform), typeof(AspectRatioFitter));
            var nameRect = (RectTransform)nameGO.transform;
            nameRect.SetParent(nameAreaRect, false);
            var nameFitter = nameGO.GetComponent<AspectRatioFitter>();
            nameFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            nameFitter.aspectRatio = NameImageWidth / NameImageHeight;

            for (int i = 0; i < LunabelNameLetterRects.Length; i++)
            {
                var texture = Resources.Load<Texture2D>(LunabelNameLetterPath + (i + 1).ToString("00"));
                if (texture == null) continue;
                var r = LunabelNameLetterRects[i];
                var letterGO = new GameObject("Letter" + (i + 1), typeof(RectTransform), typeof(RawImage));
                var letterRect = (RectTransform)letterGO.transform;
                letterRect.SetParent(nameRect, false);
                letterRect.anchorMin = new Vector2(r.x / NameImageWidth, 1f - r.w / NameImageHeight);
                letterRect.anchorMax = new Vector2(r.z / NameImageWidth, 1f - r.y / NameImageHeight);
                letterRect.offsetMin = Vector2.zero;
                letterRect.offsetMax = Vector2.zero;
                var letter = letterGO.GetComponent<RawImage>();
                letter.texture = texture;
                letter.raycastTarget = false;
                nameLetters.Add(letter);
            }

            cardGO.SetActive(false);
        }

        private IEnumerator LunabelNameCard()
        {
            if (nameCardGroup == null || nameLetters.Count == 0) yield break;

            var card = nameCardGroup.gameObject;
            card.SetActive(true);
            nameCardGroup.alpha = 1f;
            nameCardIllustration.color = new Color(1f, 1f, 1f, 0f);
            foreach (var letter in nameLetters)
            {
                letter.gameObject.SetActive(false);
                letter.rectTransform.localScale = Vector3.one;
                letter.rectTransform.anchoredPosition = Vector2.zero;
                letter.rectTransform.localRotation = Quaternion.identity;
            }

            var shake = StartCoroutine(ShakeNameLetters());
            bool skipped = false;

            // 일러스트가 어둠 속에서 떠오른다.
            for (float t = 0f; t < NameCardFadeIn && !skipped; t += Time.unscaledDeltaTime)
            {
                float k = t / NameCardFadeIn;
                nameCardIllustration.color = new Color(1f, 1f, 1f, k);
                nameCardBand.color = new Color(0f, 0f, 0f, NameBandAlpha * k);
                skipped = NameCardSkipPressed();
                yield return null;
            }
            nameCardIllustration.color = Color.white;
            nameCardBand.color = new Color(0f, 0f, 0f, NameBandAlpha);

            // 글자를 하나씩 찍는다(크게 튀어나왔다가 제자리로 박힌다).
            for (int i = 0; i < nameLetters.Count && !skipped; i++)
            {
                var letter = nameLetters[i];
                letter.gameObject.SetActive(true);
                for (float t = 0f; t < NameLetterInterval && !skipped; t += Time.unscaledDeltaTime)
                {
                    float pop = Mathf.Clamp01(t / NameLetterPopTime);
                    letter.rectTransform.localScale = Vector3.one * Mathf.Lerp(NameLetterPopScale, 1f, pop);
                    letter.color = new Color(1f, 1f, 1f, pop);
                    skipped = NameCardSkipPressed();
                    yield return null;
                }
                letter.rectTransform.localScale = Vector3.one;
                letter.color = Color.white;
            }
            foreach (var letter in nameLetters)
            {
                letter.gameObject.SetActive(true);
                letter.rectTransform.localScale = Vector3.one;
                letter.color = Color.white;
            }

            for (float t = 0f; t < NameCardHold && !skipped; t += Time.unscaledDeltaTime)
            {
                skipped = NameCardSkipPressed();
                yield return null;
            }

            for (float t = 0f; t < NameCardFadeOut; t += Time.unscaledDeltaTime)
            {
                nameCardGroup.alpha = 1f - t / NameCardFadeOut;
                yield return null;
            }
            StopCoroutine(shake);
            card.SetActive(false);
        }

        // 찍힌 글자마다 따로 무작위로 흔들어, 피가 끓어오르듯 떨리는 느낌을 준다.
        private IEnumerator ShakeNameLetters()
        {
            while (nameCardGroup.gameObject.activeSelf)
            {
                foreach (var letter in nameLetters)
                {
                    if (!letter.gameObject.activeSelf) continue;
                    letter.rectTransform.anchoredPosition = Random.insideUnitCircle * NameShakeAmount;
                    letter.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-NameShakeAngle, NameShakeAngle));
                }
                yield return new WaitForSecondsRealtime(NameShakeInterval);
            }
        }

        private static bool NameCardSkipPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
        }

        // 루나벨전 첫 턴 전 연출: 노드가 날아오기 전에 루나벨이 한마디 하고 → 행동 선택 창이 떠서 도주 칸에 핏빛 사슬이
        // 감기고(잠금 애니메이션) → 네크로시아가 독백한 뒤에 첫 적 턴이 시작된다. 대사는 [Z]/Enter로 넘긴다.
        // 내 턴 창이 화면 높이를 다 덮어 대화창과 겹치므로, 잠금 장면 동안은 대화창을 잠깐 내린다.
        private const string LunabelOpeningLine = "어딜 보는 거야? 이 언덕에 발을 들인 순간부터, 넌 내 사냥감이야. 도망칠 길 따위 남겨 두지 않았어.";
        private const string NecrosiaOpeningLine = "...도망칠 생각 따윈 없다. 그날 무너진 리듬, 여기서 되찾는다.";
        private const string OpeningNextHint = "   <color=#E07A8A>[Z]</color>";
        private const string OpeningPlayerNextHint = "   <color=#B98CE8>[Z]</color>";
        private const float FleeLockHoldTime = 0.9f;

        private IEnumerator LunabelBattleOpening()
        {
            var dialogue = FindFirstObjectByType<NpcDialogueUI>();
            if (dialogue == null) yield break;
            dialogue.BringToFront();

            dialogue.ShowLunabel(LunabelOpeningLine + OpeningNextHint);
            yield return WaitForAdvance();
            dialogue.Hide();
            if (BattleEnded()) yield break;

            // 도주 칸 잠금 장면. 선택 빛줄기는 숨기고 잠금 애니메이션만 보여준다.
            myTurnTitleText.text = "";
            actionMessageText.text = "";
            actionCursorImage.enabled = false;
            myTurnFrame.SetActive(true);
            actionSelectPanel.SetActive(true);
            ShowFleeLock();
            yield return new WaitForSecondsRealtime(FleeLockFrameCount * FleeLockFrameInterval);
            actionMessageText.text = "루나벨의 핏빛 사슬이 도주를 봉인했다!";
            yield return new WaitForSecondsRealtime(FleeLockHoldTime);
            actionSelectPanel.SetActive(false);
            myTurnFrame.SetActive(false);
            actionCursorImage.enabled = true;
            actionMessageText.text = "";
            if (BattleEnded()) yield break;

            dialogue.ShowNecrosia(NecrosiaOpeningLine + OpeningPlayerNextHint);
            yield return WaitForAdvance();
            dialogue.Hide();
        }

        // 대사가 바뀐 그 프레임의 입력으로 바로 넘어가지 않게 한 프레임 쉰 뒤, [Z]/Enter를 기다린다.
        private IEnumerator WaitForAdvance()
        {
            yield return null;
            while (!BattleEnded())
            {
                var keyboard = Keyboard.current;
                if (NpcDialogueUI.AdvancePressed(keyboard, allowEnter: true)) yield break; // 타이핑 마저 보여 주기/다음 장은 대화창이 먼저 쓴다
                yield return null;
            }
        }

        private IEnumerator FleeLockRoutine()
        {
            foreach (var frame in fleeLockFrames)
            {
                if (frame != null) fleeLockImage.texture = frame;
                yield return new WaitForSecondsRealtime(FleeLockFrameInterval);
                if (!fleeLockImage.gameObject.activeInHierarchy) break;
            }
            fleeLockImage.texture = fleeLockFrames[FleeLockFrameCount - 1];
        }
    }
}

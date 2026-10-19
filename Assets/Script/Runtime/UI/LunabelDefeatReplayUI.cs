using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Stages;

namespace Aethoria.UI
{
    // 타이틀 EXTRAS의 "루나벨 처치 장면" 다시보기. 게임의 처치 연출(LunabelDeathCutscene)을 화면 위에서 그대로 재현한다:
    // 절망의 언덕 배경 앞에 네크로시아와 루나벨이 마주 서 있고 → 루나벨이 쓰러지는 모션(Defeat 8프레임)을 재생하고 →
    // 쓰러진 채 마지막 대화를 나눈 뒤([Z]/Enter로 넘김) → 서서히 사라진다. Esc를 누르면 언제든 끝낸다.
    // 대사와 타이밍은 LunabelDeathCutscene과 같은 값을 쓴다. 모든 타이밍은 실시간으로 잰다.
    public class LunabelDefeatReplayUI : MonoBehaviour
    {
        private const string BackgroundPath = "Backgrounds/HillOfDespair";
        private const string NecrosiaIdlePath = "Art/Necrosia/Idle";
        private const float NecrosiaIdleInterval = 0.25f;
        private const float NecrosiaIdleHeight = 1.8f;
        // 월드 1유닛을 화면 몇 px로 그릴지. 루나벨(약 2.1유닛)과 네크로시아(약 1.5유닛)의 키 비율을 게임과 같게 유지한다.
        private const float PixelsPerUnit = 135f;
        // 배경(2172x724)은 화면 높이에 맞춰 깔리므로(EnvelopeParent), 그림 속 땅(위에서 628px, 게임의 HillGroundFraction과 같은 줄)이
        // 화면 아래에서 이 비율 높이에 온다. 두 캐릭터의 발밑을 그 줄에 맞춰 땅 위에 서 있게 한다.
        private const float GroundAnchorY = 1f - 628f / 724f;
        // 캐릭터가 화면 아래쪽 땅에 서므로, 대화창은 화면 위쪽 절반에 띄워 겹치지 않게 한다.
        // 0.45면 루나벨 대화창(초상화가 그림 맨 위까지 올라온다)도 화면 위로 잘리지 않고, 캐릭터 머리와도 겹치지 않는다.
        private const float DialogueAreaBottom = 0.45f;
        private const float CharacterGap = 170f;   // 화면 가운데에서 각 캐릭터까지
        private const float ScreenFade = 0.5f;
        private const float StartPause = 0.4f;
        private const float EndPause = 0.8f;
        private const string LunabelNextHint = "   <color=#E07A8A>[Z]</color>";
        private const string PlayerNextHint = "   <color=#B98CE8>[Z]</color>";

        private GameObject root;
        private Image blackout;
        private Image necrosia;
        private Image lunabel;
        private NpcDialogueUI dialogue;
        private Sprite[] necrosiaFrames = System.Array.Empty<Sprite>();
        private Sprite[] defeatFrames = System.Array.Empty<Sprite>();
        private bool skipped;

        public bool IsShowing => root != null && root.activeSelf;

        public static LunabelDefeatReplayUI Create(Transform parent)
        {
            var go = new GameObject("LunabelDefeatReplay", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            var replay = go.AddComponent<LunabelDefeatReplayUI>();
            replay.Build(rect);
            return replay;
        }

        private void Build(RectTransform rect)
        {
            root = new GameObject("Root", typeof(RectTransform), typeof(Image));
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(rect, false);
            Stretch(rootRect);
            var backdrop = root.GetComponent<Image>();
            backdrop.color = Color.black;
            backdrop.raycastTarget = true; // 재생 중 뒤쪽 버튼 클릭 차단

            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            var bgRect = (RectTransform)bgGO.transform;
            bgRect.SetParent(rootRect, false);
            var bg = bgGO.GetComponent<RawImage>();
            bg.raycastTarget = false;
            bg.texture = Resources.Load<Texture2D>(BackgroundPath);
            bg.color = new Color(0.7f, 0.65f, 0.65f, 1f);
            var fitter = bgGO.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            if (bg.texture != null) fitter.aspectRatio = (float)bg.texture.width / bg.texture.height;

            necrosiaFrames = LoadSorted(NecrosiaIdlePath);
            defeatFrames = LoadSorted(LunabelDeathCutscene.DefeatPath);
            // 네크로시아는 오른쪽(루나벨 쪽)을 보고 왼쪽에, 루나벨은 왼쪽(네크로시아 쪽)을 보도록 뒤집어 오른쪽에 선다.
            necrosia = CreateCharacter(rootRect, "Necrosia", -CharacterGap, false);
            lunabel = CreateCharacter(rootRect, "Lunabel", CharacterGap, true);

            // 대화창은 게임과 같은 NpcDialogueUI를 이 화면 안에 하나 따로 둔다.
            var dialogueGO = new GameObject("Dialogue", typeof(RectTransform));
            var dialogueRect = (RectTransform)dialogueGO.transform;
            dialogueRect.SetParent(rootRect, false);
            Stretch(dialogueRect);
            dialogueRect.anchorMin = new Vector2(0f, DialogueAreaBottom);
            dialogue = dialogueGO.AddComponent<NpcDialogueUI>();
            dialogue.Initialize();

            var blackGO = new GameObject("Blackout", typeof(RectTransform), typeof(Image));
            var blackRect = (RectTransform)blackGO.transform;
            blackRect.SetParent(rootRect, false);
            Stretch(blackRect);
            blackout = blackGO.GetComponent<Image>();
            blackout.raycastTarget = false;

            root.SetActive(false);
        }

        private static Image CreateCharacter(RectTransform parent, string name, float x, bool flip)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, GroundAnchorY);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        // 스프라이트의 월드 크기(PPU 반영)에 맞춰 칸 크기를 정한다 — 게임 속 키 비율이 그대로 유지된다.
        // targetHeight를 주면 게임처럼 그 키(유닛)에 맞춰 늘리거나 줄인다(네크로시아 대기 = WalkSpriteAnimator의 1.8).
        private static void SetFrame(Image image, Sprite sprite, float targetHeight = 0f)
        {
            if (sprite == null) return;
            image.sprite = sprite;
            Vector2 size = sprite.bounds.size;
            if (targetHeight > 0f && size.y > 0f) size *= targetHeight / size.y;
            image.rectTransform.sizeDelta = size * PixelsPerUnit;
        }

        public IEnumerator Play()
        {
            skipped = false;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            dialogue.Hide();
            lunabel.color = Color.white;
            SetFrame(lunabel, defeatFrames.Length > 0 ? defeatFrames[0] : null);
            var idle = StartCoroutine(AnimateNecrosia());

            yield return Fade(1f, 0f);
            yield return Wait(StartPause);

            // 1) 쓰러지는 모션.
            for (int i = 1; i < defeatFrames.Length && !skipped; i++)
            {
                SetFrame(lunabel, defeatFrames[i]);
                yield return Wait(LunabelDeathCutscene.DefeatFrameInterval);
            }
            yield return Wait(LunabelDeathCutscene.HoldAfterFall);

            // 2) 마지막 대화.
            foreach (var line in LunabelDeathCutscene.LastWords)
            {
                if (skipped) break;
                if (line.IsNecrosia) dialogue.ShowNecrosia(line.Text + PlayerNextHint);
                else dialogue.ShowLunabel(line.Text + LunabelNextHint);
                yield return null;
                while (!skipped && !AdvancePressed())
                {
                    skipped = SkipPressed();
                    yield return null;
                }
            }
            dialogue.Hide();

            // 3) 서서히 사라진다.
            for (float t = 0f; t < LunabelDeathCutscene.FadeOutDuration && !skipped; t += Time.unscaledDeltaTime)
            {
                lunabel.color = new Color(1f, 1f, 1f, 1f - t / LunabelDeathCutscene.FadeOutDuration);
                skipped = SkipPressed();
                yield return null;
            }
            lunabel.color = new Color(1f, 1f, 1f, 0f);
            yield return Wait(EndPause);

            yield return Fade(0f, 1f);
            StopCoroutine(idle);
            root.SetActive(false);
        }

        private IEnumerator AnimateNecrosia()
        {
            int index = 0;
            while (necrosiaFrames.Length > 0)
            {
                SetFrame(necrosia, necrosiaFrames[index], NecrosiaIdleHeight);
                index = (index + 1) % necrosiaFrames.Length;
                yield return new WaitForSecondsRealtime(NecrosiaIdleInterval);
            }
        }

        // Esc로 건너뛰는 중이면 기다리지 않는다.
        private IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !skipped; t += Time.unscaledDeltaTime)
            {
                skipped = SkipPressed();
                yield return null;
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < ScreenFade; t += Time.unscaledDeltaTime)
            {
                blackout.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / ScreenFade));
                yield return null;
            }
            blackout.color = new Color(0f, 0f, 0f, to);
        }

        private static Sprite[] LoadSorted(string path)
        {
            var frames = Resources.LoadAll<Sprite>(path);
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            return frames;
        }

        private static bool AdvancePressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
        }

        private static bool SkipPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}

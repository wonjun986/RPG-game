using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // 타이틀 EXTRAS에서 캐릭터 모션을 하나씩 돌려 보는 창. ←/→(또는 화면의 ◀/▶)로 모션을 바꾸고 Esc/X/Z로 닫는다.
    // 캐릭터 모션은 게임과 같은 크기 비율(스프라이트 월드 크기 x UnitsToCanvas)로 바닥에 세워 그리고,
    // 이펙트처럼 크기가 제각각인 것은 FitBox로 무대 칸 안에 꽉 차게 맞춘다.
    public class MotionGalleryUI : MonoBehaviour
    {
        public class Clip
        {
            public readonly string Name;
            public readonly float Interval;
            public readonly bool Loop;      // false면 마지막 프레임에서 잠깐 멈췄다가 처음부터 다시 튼다.
            public readonly bool FitBox;
            public readonly bool Flip;      // 게임에서처럼 왼쪽(플레이어 쪽)을 보게 좌우로 뒤집는다.
            public readonly string[] Paths; // 여러 폴더를 이어 붙여 한 모션으로 튼다(같은 폴더를 두 번 넣으면 두 번 반복).
            public Sprite[] Frames;

            public Clip(string name, float interval, bool loop, bool fitBox, bool flip, params string[] paths)
            {
                Name = name;
                Interval = interval;
                Loop = loop;
                FitBox = fitBox;
                Flip = flip;
                Paths = paths;
            }
        }

        private const float UnitsToCanvas = 140f;
        private const float OneShotEndPause = 0.6f;
        private static readonly Vector2 StageSize = new Vector2(760f, 520f);

        private Clip[] clips;
        private int clipIndex;
        private int frameIndex;
        private float frameTimer;

        private GameObject root;
        private Image image;
        private Text nameText;
        private Text frameText;

        public bool IsShowing => root != null && root.activeSelf;
        // 닫는 키(Esc/X)가 같은 프레임에 뒤쪽 창(EXTRAS)까지 닫지 않도록, 방금 닫혔는지 알려 준다.
        public bool ClosedThisFrame { get; private set; }
        private int closedFrame = -1;
        public event System.Action Closed;

        public static MotionGalleryUI Create(Transform parent, string title, Clip[] clips)
        {
            var go = new GameObject("MotionGallery", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            var gallery = go.AddComponent<MotionGalleryUI>();
            gallery.Build(rect, title, clips);
            return gallery;
        }

        private void Build(RectTransform rect, string title, Clip[] newClips)
        {
            clips = newClips;
            foreach (var clip in clips)
            {
                var frames = new List<Sprite>();
                foreach (var path in clip.Paths)
                {
                    var sprites = Resources.LoadAll<Sprite>(path);
                    System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
                    frames.AddRange(sprites);
                }
                clip.Frames = frames.ToArray();
            }

            root = rect.gameObject;
            var dim = root.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.01f, 0.04f, 0.94f);

            var titleText = CreateText(rect, "Title", new Vector2(0f, 0.9f), new Vector2(1f, 0.98f), 30, new Color(0.85f, 0.7f, 1f));
            titleText.fontStyle = FontStyle.Bold;
            titleText.text = title;
            nameText = CreateText(rect, "ClipName", new Vector2(0f, 0.84f), new Vector2(1f, 0.9f), 24, Color.white);
            frameText = CreateText(rect, "FrameIndex", new Vector2(0f, 0.04f), new Vector2(1f, 0.1f), 18, new Color(1f, 1f, 1f, 0.7f));

            // 무대: 화면 가운데, 아래 가운데에 발밑을 붙인다.
            var stageGO = new GameObject("Stage", typeof(RectTransform));
            var stage = (RectTransform)stageGO.transform;
            stage.SetParent(rect, false);
            stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = StageSize;
            stage.anchoredPosition = new Vector2(0f, -10f);

            var imageGO = new GameObject("Motion", typeof(RectTransform), typeof(Image));
            var imageRect = (RectTransform)imageGO.transform;
            imageRect.SetParent(stage, false);
            imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0f);
            imageRect.pivot = new Vector2(0.5f, 0f);
            image = imageGO.GetComponent<Image>();
            image.raycastTarget = false;

            WorldMapUI.BuildDialogButton(rect, "PrevButton", "◀", new Vector2(0.03f, 0.45f), new Vector2(0.09f, 0.55f), () => ChangeClip(-1));
            WorldMapUI.BuildDialogButton(rect, "NextButton", "▶", new Vector2(0.91f, 0.45f), new Vector2(0.97f, 0.55f), () => ChangeClip(1));
            WorldMapUI.BuildDialogButton(rect, "CloseButton", "닫기", new Vector2(0.86f, 0.03f), new Vector2(0.97f, 0.1f), Hide);

            root.SetActive(false);
        }

        public void Show()
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            SelectClip(0);
        }

        public void Hide()
        {
            if (!IsShowing) return;
            root.SetActive(false);
            closedFrame = Time.frameCount;
            Closed?.Invoke();
        }

        private void ChangeClip(int delta)
        {
            SelectClip((clipIndex + delta + clips.Length) % clips.Length);
        }

        private void SelectClip(int index)
        {
            clipIndex = index;
            frameIndex = 0;
            frameTimer = 0f;
            var clip = clips[clipIndex];
            nameText.text = $"{clip.Name}  ({clipIndex + 1}/{clips.Length})";
            image.rectTransform.localScale = new Vector3(clip.Flip ? -1f : 1f, 1f, 1f);
            ShowFrame();
        }

        private void ShowFrame()
        {
            var clip = clips[clipIndex];
            if (clip.Frames.Length == 0)
            {
                image.enabled = false;
                frameText.text = "프레임이 없습니다";
                return;
            }

            var sprite = clip.Frames[frameIndex];
            image.enabled = true;
            image.sprite = sprite;
            Vector2 size;
            if (clip.FitBox)
            {
                float scale = Mathf.Min(StageSize.x / sprite.rect.width, StageSize.y / sprite.rect.height);
                size = sprite.rect.size * scale;
            }
            else
            {
                size = sprite.rect.size / sprite.pixelsPerUnit * UnitsToCanvas;
            }
            image.rectTransform.sizeDelta = size;
            frameText.text = $"프레임 {frameIndex + 1}/{clip.Frames.Length}   [←/→] 모션 바꾸기   [Esc/X/Z] 닫기";
        }

        private void Update()
        {
            ClosedThisFrame = closedFrame == Time.frameCount;
            if (!IsShowing) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame)
                {
                    Hide();
                    ClosedThisFrame = true;
                    return;
                }
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) ChangeClip(-1);
                else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) ChangeClip(1);
            }

            var clip = clips[clipIndex];
            if (clip.Frames.Length < 2) return;
            frameTimer += Time.unscaledDeltaTime;
            bool atEnd = frameIndex == clip.Frames.Length - 1;
            float wait = atEnd && !clip.Loop ? clip.Interval + OneShotEndPause : clip.Interval;
            if (frameTimer < wait) return;
            frameTimer -= wait;
            frameIndex = (frameIndex + 1) % clip.Frames.Length;
            ShowFrame();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }
    }
}

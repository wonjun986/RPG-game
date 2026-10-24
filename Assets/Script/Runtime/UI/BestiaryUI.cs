using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Data;

namespace Aethoria.UI
{
    // 릴리의 몬스터 도감(엔스룸의 릴리에게 [1] 도감 보기). 바탕은 UI/Bestiary(원화 "보라빛 고딕 도감 UI 프레임", 1536x1024)에서
    // 바깥 체크무늬와 자리표시 글자("???", ◇◇◇◇◇)를 지운 그림이다. 고른 칸에는 UI/Bestiary_Selected(빛나는 보라 테두리)를 겹치고,
    // 목록 오른쪽 틈에는 스크롤바(원화 "보랏빛 고딕 판타지 스크롤바")를 트랙(UI/Bestiary_ScrollTrack)과 손잡이(UI/Bestiary_ScrollThumb)로
    // 나눠 그려서 손잡이가 목록 위치를 따라 움직인다. 3:2 그림이라 화면(16:9)에 늘이지 않고 세로에 맞춰 가운데에 놓는다.
    // 왼쪽 목록(7칸) / 가운데 그림·이름·위험도·처치 수 / 오른쪽 번호·종류·서식지·크기·설명(또는 릴리의 비밀 메모)·주요 드롭 아이템.
    // 한 번도 못 잡은 몬스터는 "???"와 실루엣으로 가리고, secretKills만큼 잡으면 비밀 메모가 열린다.
    // 조작: ↑↓ 고르기, ←→ 설명/비밀 메모 넘기기, 마우스 휠·손잡이 끌기로 목록 스크롤, X/Esc(또는 오른쪽 위 X) 닫기.
    // 열려 있는 동안 게임을 멈추고 플레이어 입력을 잠근다.
    public class BestiaryUI : MonoBehaviour
    {
        private const float ImageWidth = 1536f;
        private const float ImageHeight = 1024f;
        // 그림을 캔버스(1280x720) 세로에 맞춰 그리므로, 그림 픽셀 → 캔버스 단위 배율.
        private const float PxToCanvas = 720f / ImageHeight;

        // 목록 칸 안쪽 윗변(그림 y). 칸 높이는 82px, 왼쪽 썸네일 칸은 x 93~185, 글자 칸은 x 188~449.
        private static readonly float[] RowTops = { 239f, 337f, 435f, 533f, 632f, 730f, 829f };
        private const float RowHeight = 82f;
        // 선택 테두리 그림(401x118)은 칸 테두리(x 85~458, 윗변-4~아랫변+4) 바깥으로 14px씩 빛이 번진다.
        private const float SelectedX = 71f, SelectedOffsetY = -18f, SelectedWidth = 401f, SelectedHeight = 118f;

        // 스크롤바: 목록 오른쪽 틈(x 459~479) 위에 세운다. 원화(170x1507)를 가로 0.36배, 세로 0.46배로 그려서
        // 가는 몸통(레일)이 틈에 딱 들어가고 위아래 가시 장식은 틈 밖으로 조금 삐져나온다.
        private const float TrackX0 = 439f, TrackX1 = 500f, TrackY0 = 228f, TrackY1 = 921f;
        // 손잡이가 오르내리는 범위(원화 y 300~1225 = 위아래 장식 사이)와 손잡이 폭(원화 x 484~539).
        private const float TravelY0 = 363.7f, TravelY1 = 789f;
        private const float ThumbX0 = 459f, ThumbX1 = 479f;

        // 주요 드롭 아이템 4칸(그림 x 범위, y 616~692)과 칸 아래 이름/확률 줄(y 704~732).
        private static readonly float[] DropSlotX0 = { 1085f, 1173f, 1261f, 1349f };
        private const float DropSlotWidth = 71f, DropSlotY0 = 616f, DropSlotY1 = 692f;

        private static readonly Color InkColor = new Color(0.25f, 0.17f, 0.13f);
        private static readonly Color DimInkColor = new Color(0.55f, 0.47f, 0.42f);
        private static readonly Color LightColor = new Color(0.93f, 0.88f, 0.95f);
        private static readonly Color DimLightColor = new Color(0.6f, 0.55f, 0.62f);
        // 못 잡은 몬스터 실루엣. 가운데 양피지 위엔 검게, 어두운 목록 칸 위엔 검은색이 묻히니 흐린 보라 그림자로.
        private static readonly Color SilhouetteColor = new Color(0.12f, 0.08f, 0.1f, 0.92f);
        private static readonly Color ThumbSilhouetteColor = new Color(0.38f, 0.3f, 0.45f, 0.85f);
        private const string DangerColor = "#B0413E";
        private const string SecretColor = "#7A3F86";

        private CharacterMovement2D playerMovement;
        private GameObject panel;
        private RectTransform book;
        private RectTransform selectedFrame;
        private RectTransform scrollTrack;
        private RectTransform scrollThumb;
        private readonly Image[] rowThumbs = new Image[RowTops.Length];
        private readonly Text[] rowNames = new Text[RowTops.Length];
        private readonly Text[] rowDangers = new Text[RowTops.Length];
        private readonly Image[] dropIcons = new Image[4];
        private readonly Text[] dropLabels = new Text[4];
        private Image portrait;
        private Text centerName;
        private Text dangerText;
        private Text killText;
        private Text numberName;
        private Text kindText;
        private Text habitatText;
        private Text sizeText;
        private Text pageHint;
        private Text descriptionText;
        private Text progressText;
        private int selected;
        private int scroll;
        private bool showSecretPage;
        private float dragAccumulator;
        private int openedFrame = -1;

        public bool IsOpen => panel != null && panel.activeSelf;

        private static int EntryCount => Bestiary.Entries.Length;
        private static int VisibleRows => RowTops.Length;
        private static int MaxScroll => Mathf.Max(0, EntryCount - VisibleRows);

        public void Initialize(CharacterMovement2D movement)
        {
            playerMovement = movement;
            BuildPanel();
        }

        private void BuildPanel()
        {
            TitleScreenUI.EnsureEventSystem();

            panel = new GameObject("BestiaryPanel", typeof(RectTransform), typeof(Image));
            var dim = (RectTransform)panel.transform;
            dim.SetParent(transform, false);
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var bookGO = new GameObject("Book", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            book = (RectTransform)bookGO.transform;
            book.SetParent(dim, false);
            book.anchorMin = Vector2.zero;
            book.anchorMax = Vector2.one;
            book.offsetMin = Vector2.zero;
            book.offsetMax = Vector2.zero;
            bookGO.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/Bestiary");
            var fitter = bookGO.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = ImageWidth / ImageHeight;

            // 왼쪽 목록
            for (int i = 0; i < VisibleRows; i++)
            {
                int slot = i;
                float top = RowTops[i];
                var rowButton = CreateRect(book, "Row" + i, 85f, top, 458f, top + RowHeight, typeof(Image), typeof(Button));
                rowButton.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
                rowButton.GetComponent<Button>().onClick.AddListener(() => Select(scroll + slot));

                rowThumbs[i] = CreateImage(book, "Thumb" + i, 97f, top + 5f, 181f, top + RowHeight - 5f);
                rowNames[i] = CreateText(book, "RowName" + i, 205f, top + 10f, 445f, top + 44f, 25f, LightColor, TextAnchor.MiddleLeft);
                rowNames[i].fontStyle = FontStyle.Bold;
                rowDangers[i] = CreateText(book, "RowDanger" + i, 205f, top + 48f, 445f, top + 74f, 19f, LightColor, TextAnchor.MiddleLeft);
            }

            selectedFrame = CreateRawImage(book, "Selected", "UI/Bestiary_Selected", SelectedX, RowTops[0] + SelectedOffsetY,
                SelectedX + SelectedWidth, RowTops[0] + SelectedOffsetY + SelectedHeight);

            // 스크롤바: 트랙(가만히) + 손잡이(목록 위치를 따라 움직이고, 마우스로 끌 수 있다)
            scrollTrack = CreateRawImage(book, "ScrollTrack", "UI/Bestiary_ScrollTrack", TrackX0, TrackY0, TrackX1, TrackY1);
            scrollThumb = CreateRawImage(book, "ScrollThumb", "UI/Bestiary_ScrollThumb", ThumbX0, TravelY0, ThumbX1, TravelY1);
            var thumbRaw = scrollThumb.GetComponent<RawImage>();
            thumbRaw.raycastTarget = true;
            scrollThumb.gameObject.AddComponent<ScrollThumbDrag>().OnDragPixels = DragThumb;

            // 가운데: 아치 안 그림, 아래 어두운 이름 띠, 그 아래 양피지 두 줄(위험도/처치 수)
            portrait = CreateImage(book, "Portrait", 590f, 225f, 940f, 655f);
            centerName = CreateText(book, "CenterName", 612f, 697f, 925f, 738f, 30f, LightColor, TextAnchor.MiddleCenter);
            centerName.fontStyle = FontStyle.Bold;
            // 양피지에 그려진 구분선(y 약 785, 850) 사이사이에 한 줄씩.
            dangerText = CreateText(book, "Danger", 545f, 759f, 980f, 782f, 20f, InkColor, TextAnchor.MiddleCenter);
            killText = CreateText(book, "Kills", 545f, 792f, 980f, 842f, 22f, InkColor, TextAnchor.MiddleCenter);

            // 오른쪽: 번호+이름 띠, 종류/서식지/크기, 설명 칸(설명 ↔ 릴리의 비밀 메모), 드롭 아이템, 발견 수
            numberName = CreateText(book, "NumberName", 1108f, 194f, 1420f, 230f, 24f, LightColor, TextAnchor.MiddleLeft);
            numberName.fontStyle = FontStyle.Bold;
            kindText = CreateText(book, "Kind", 1196f, 251f, 1440f, 277f, 20f, InkColor, TextAnchor.MiddleLeft);
            habitatText = CreateText(book, "Habitat", 1196f, 284f, 1440f, 310f, 20f, InkColor, TextAnchor.MiddleLeft);
            sizeText = CreateText(book, "Size", 1196f, 318f, 1440f, 344f, 20f, InkColor, TextAnchor.MiddleLeft);
            pageHint = CreateText(book, "PageHint", 1240f, 376f, 1425f, 402f, 16f, DimInkColor, TextAnchor.MiddleRight);
            descriptionText = CreateText(book, "Description", 1078f, 415f, 1425f, 541f, 19f, InkColor, TextAnchor.UpperLeft);
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
            descriptionText.resizeTextForBestFit = true;
            descriptionText.resizeTextMinSize = 9;
            descriptionText.resizeTextMaxSize = Mathf.RoundToInt(19f * PxToCanvas);
            descriptionText.lineSpacing = 1.1f;

            for (int i = 0; i < dropIcons.Length; i++)
            {
                float x0 = DropSlotX0[i];
                dropIcons[i] = CreateImage(book, "DropIcon" + i, x0 + 8f, DropSlotY0 + 8f, x0 + DropSlotWidth - 8f, DropSlotY1 - 8f);
                dropLabels[i] = CreateText(book, "DropLabel" + i, x0 - 12f, 704f, x0 + DropSlotWidth + 12f, 732f, 15f, InkColor, TextAnchor.MiddleCenter);
            }

            var hint = CreateText(book, "Hint", 1070f, 840f, 1392f, 866f, 15f, DimInkColor, TextAnchor.MiddleRight);
            hint.text = "↑↓ 고르기  ←→ 비밀 메모  휠 스크롤  X 닫기";
            progressText = CreateText(book, "Progress", 1070f, 868f, 1392f, 900f, 20f, DimInkColor, TextAnchor.MiddleRight);

            // 오른쪽 위 X 문양을 누르면 닫힌다.
            var close = CreateRect(book, "Close", 1432f, 72f, 1508f, 148f, typeof(Image), typeof(Button));
            close.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            close.GetComponent<Button>().onClick.AddListener(Hide);

            panel.SetActive(false);
        }

        public void Show()
        {
            openedFrame = Time.frameCount;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
            Select(selected);
        }

        public void Hide()
        {
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void Update()
        {
            if (!IsOpen) return;
            // 연 그 프레임의 입력(릴리 대화의 숫자키 등)이 같은 프레임에 다시 소비되지 않게 한다.
            if (Time.frameCount == openedFrame) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                {
                    Hide();
                    return;
                }

                int count = EntryCount;
                if (keyboard.upArrowKey.wasPressedThisFrame) Select((selected - 1 + count) % count);
                else if (keyboard.downArrowKey.wasPressedThisFrame) Select((selected + 1) % count);
                else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                {
                    showSecretPage = !showSecretPage;
                    Refresh();
                }
            }

            // 마우스 휠: 고른 칸은 그대로 두고 목록만 한 칸씩 민다.
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0.01f) ScrollTo(scroll - 1);
                else if (wheel < -0.01f) ScrollTo(scroll + 1);
            }
        }

        private void Select(int index)
        {
            int newIndex = Mathf.Clamp(index, 0, EntryCount - 1);
            if (newIndex != selected) showSecretPage = false;
            selected = newIndex;

            // 고른 칸이 보이도록 목록을 민다.
            if (selected < scroll) scroll = selected;
            else if (selected >= scroll + VisibleRows) scroll = selected - VisibleRows + 1;
            scroll = Mathf.Clamp(scroll, 0, MaxScroll);
            Refresh();
        }

        private void ScrollTo(int newScroll)
        {
            newScroll = Mathf.Clamp(newScroll, 0, MaxScroll);
            if (newScroll == scroll) return;
            scroll = newScroll;
            Refresh();
        }

        // 손잡이를 끈 거리(화면 픽셀)를 그림 픽셀로 바꿔, 손잡이 한 칸 이동 거리만큼 쌓일 때마다 목록을 한 칸 민다.
        private void DragThumb(float screenDeltaY)
        {
            if (MaxScroll <= 0) return;
            float bookHeight = book.rect.height;
            var canvas = book.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            if (bookHeight <= 0f || scale <= 0f) return;

            float imagePx = -screenDeltaY / scale * ImageHeight / bookHeight; // 화면 위쪽이 +라 부호를 뒤집는다
            float travel = TravelY1 - TravelY0;
            float thumbLength = travel * VisibleRows / EntryCount;
            float stepPx = (travel - thumbLength) / MaxScroll;
            dragAccumulator += imagePx;
            while (dragAccumulator >= stepPx) { dragAccumulator -= stepPx; ScrollTo(scroll + 1); }
            while (dragAccumulator <= -stepPx) { dragAccumulator += stepPx; ScrollTo(scroll - 1); }
        }

        private void Refresh()
        {
            var entries = Bestiary.Entries;

            for (int slot = 0; slot < VisibleRows; slot++)
            {
                int i = scroll + slot;
                bool exists = i < entries.Length;
                rowThumbs[slot].gameObject.SetActive(exists);
                rowNames[slot].gameObject.SetActive(exists);
                rowDangers[slot].gameObject.SetActive(exists);
                if (!exists) continue;

                var entry = entries[i];
                bool found = Bestiary.IsDiscovered(entry);
                SetPortrait(rowThumbs[slot], entry, found, ThumbSilhouetteColor);
                rowNames[slot].text = (found ? entry.key : "???") + (found && Bestiary.IsSecretUnlocked(entry) ? "  <color=#E8C46A>★</color>" : "");
                rowNames[slot].color = found ? LightColor : DimLightColor;
                rowDangers[slot].text = found ? $"<color={DangerColor}>{Diamonds(entry.danger)}</color>" : "◇◇◇◇◇";
                rowDangers[slot].color = found ? LightColor : DimLightColor;
            }

            // 선택 테두리: 고른 칸이 목록에 보일 때만.
            int visibleSlot = selected - scroll;
            bool selectedVisible = visibleSlot >= 0 && visibleSlot < VisibleRows;
            selectedFrame.gameObject.SetActive(selectedVisible);
            if (selectedVisible)
            {
                float selTop = RowTops[visibleSlot] + SelectedOffsetY;
                SetRect(selectedFrame, SelectedX, selTop, SelectedX + SelectedWidth, selTop + SelectedHeight);
            }

            // 스크롤바: 넘칠 게 없으면 손잡이가 트랙을 꽉 채운다.
            float travel = TravelY1 - TravelY0;
            float thumbLength = MaxScroll > 0 ? travel * VisibleRows / EntryCount : travel;
            float thumbTop = TravelY0 + (MaxScroll > 0 ? (travel - thumbLength) * scroll / MaxScroll : 0f);
            SetRect(scrollThumb, ThumbX0, thumbTop, ThumbX1, thumbTop + thumbLength);

            progressText.text = $"발견 {Bestiary.DiscoveredCount} / {entries.Length}";

            var current = entries[selected];
            bool discovered = Bestiary.IsDiscovered(current);
            int kills = Bestiary.Kills(current.key);
            SetPortrait(portrait, current, discovered, SilhouetteColor);
            numberName.text = $"<size={Mathf.RoundToInt(18f * PxToCanvas)}>No.{selected + 1:00}</size>   {(discovered ? current.key : "???")}";
            RefreshDrops(current, discovered);

            if (!discovered)
            {
                centerName.text = "???";
                dangerText.text = "위험도  ◇◇◇◇◇";
                killText.text = "처치 수  0";
                kindText.text = "???";
                habitatText.text = current.habitat;
                sizeText.text = "???";
                pageHint.text = "";
                descriptionText.text = "아직 만나 본 적 없는 몬스터야. 어떤 아이인지 너무 궁금해! 만나면 꼭 이야기해 줘~\n" +
                                       $"<color=#8C7A86>소문으로는 {current.habitat}에서 나온대.</color>";
                descriptionText.color = DimInkColor;
                return;
            }

            centerName.text = current.key;
            dangerText.text = $"위험도  <color={DangerColor}>{Diamonds(current.danger)}</color>";
            killText.text = $"처치 수  <b>{kills}</b>";
            kindText.text = current.kind;
            habitatText.text = current.habitat;
            sizeText.text = current.size;
            descriptionText.color = InkColor;

            bool secretOpen = Bestiary.IsSecretUnlocked(current);
            pageHint.text = showSecretPage ? "◀ 설명" : (secretOpen ? "<color=#7A3F86>♥ 비밀 메모 ▶</color>" : "비밀 메모 ▶");
            if (!showSecretPage)
            {
                descriptionText.text = current.description;
            }
            else
            {
                string secret = secretOpen
                    ? current.secret
                    : $"<color=#8C7A86>{current.secretKills}마리를 쓰러뜨리면 알려 줄게! (지금 {kills} / {current.secretKills})</color>";
                descriptionText.text = $"<color={SecretColor}><b>♥ 릴리의 비밀 메모</b></color>\n{secret}";
            }
        }

        // 드롭 칸: 발견한 몬스터만 아이콘과 "이름 확률"을 보여 주고, 못 잡은 몬스터는 칸마다 "???"만.
        private void RefreshDrops(BestiaryEntry entry, bool discovered)
        {
            var drops = entry.drops ?? System.Array.Empty<BestiaryDrop>();
            for (int i = 0; i < dropIcons.Length; i++)
            {
                bool has = i < drops.Length;
                var item = has ? ItemDatabase.Find(drops[i].itemId) : null;
                Sprite icon = discovered && item != null ? LoadSprite(item.iconPath) : null;
                dropIcons[i].sprite = icon;
                dropIcons[i].enabled = icon != null;
                dropLabels[i].text = !has ? "" : !discovered || item == null ? "???" : $"{item.itemName}\n<color=#8C5A3C>{drops[i].note}</color>";
                dropLabels[i].color = discovered ? InkColor : DimInkColor;
            }
        }

        private static void SetPortrait(Image image, BestiaryEntry entry, bool discovered, Color silhouette)
        {
            image.sprite = LoadPortrait(entry.portraitPath);
            image.enabled = image.sprite != null;
            image.color = discovered ? Color.white : silhouette;
        }

        private static string Diamonds(int danger)
        {
            danger = Mathf.Clamp(danger, 0, 5);
            return new string('◆', danger) + new string('◇', 5 - danger);
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> spriteCache = new();

        // 몬스터 그림: 폴더의 첫 프레임.
        private static Sprite LoadPortrait(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            if (spriteCache.TryGetValue(folder, out var cached)) return cached;
            var frames = Resources.LoadAll<Sprite>(folder);
            Sprite first = null;
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                first = frames[0];
            }
            spriteCache[folder] = first;
            return first;
        }

        // 아이템 아이콘: 한 장짜리 스프라이트(없으면 텍스처로 만들어 쓴다).
        private static Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string key = "item:" + path;
            if (spriteCache.TryGetValue(key, out var cached)) return cached;
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(path);
                if (texture != null) sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            spriteCache[key] = sprite;
            return sprite;
        }

        // ---- 그림 픽셀 좌표(왼쪽 위 x0,y0 ~ 오른쪽 아래 x1,y1)로 요소를 만드는 도우미들 ----

        private static void SetRect(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0 / ImageWidth, 1f - y1 / ImageHeight);
            rect.anchorMax = new Vector2(x1 / ImageWidth, 1f - y0 / ImageHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform CreateRect(Transform parent, string name, float x0, float y0, float x1, float y1, params System.Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            foreach (var component in components) go.AddComponent(component);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            SetRect(rect, x0, y0, x1, y1);
            return rect;
        }

        private static RectTransform CreateRawImage(Transform parent, string name, string texturePath, float x0, float y0, float x1, float y1)
        {
            var rect = CreateRect(parent, name, x0, y0, x1, y1, typeof(RawImage));
            var raw = rect.GetComponent<RawImage>();
            raw.texture = Resources.Load<Texture2D>(texturePath);
            raw.raycastTarget = false;
            return rect;
        }

        private static Image CreateImage(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var image = CreateRect(parent, name, x0, y0, x1, y1, typeof(Image)).GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(Transform parent, string name, float x0, float y0, float x1, float y1, float fontPx, Color color, TextAnchor alignment)
        {
            var text = CreateRect(parent, name, x0, y0, x1, y1, typeof(Text)).GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(fontPx * PxToCanvas));
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        // 스크롤바 손잡이를 마우스로 끌 때 세로 이동량(화면 픽셀)을 넘겨준다.
        private class ScrollThumbDrag : MonoBehaviour, IDragHandler
        {
            public System.Action<float> OnDragPixels;
            public void OnDrag(PointerEventData eventData) => OnDragPixels?.Invoke(eventData.delta.y);
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Data;

namespace Aethoria.UI
{
    // 이리아의 축복 화면(특성 트리). 세 갈래(심판/수호/율동) x 4단계 특성을 보여 주고, 레벨업으로 얻은 축복 점수를 찍는다
    // (규칙은 BlessingRules). 지금은 코드로 그린 화면이고, 그림이 생기면 아래 경로에 넣기만 하면 그 위에 얹힌다:
    //   UI/Menus/Iria_Talent       : 화면 전체 배경(1280x720 기준 16:9)
    //   UI/Talents/<TalentId 이름> : 특성 아이콘(정사각형, 예: UI/Talents/SharpScythe)
    // 조작: 방향키(또는 클릭)로 특성 고르기, Z/Enter로 한 단계 찍기, R을 두 번 눌러 골드를 내고 초기화, X/Esc로 닫기.
    public class BlessingUI : MonoBehaviour
    {
        private const float W = 1280f, H = 720f;

        private static readonly float[] ColumnX = { 250f, 640f, 1030f };
        private static readonly float[] TierY = { 215f, 318f, 421f, 526f };
        private const float SlotOffset = 86f;
        private static readonly Vector2 NodeSize = new Vector2(160f, 76f);
        private static readonly Vector2 KeystoneSize = new Vector2(190f, 82f);

        private static readonly Color PanelColor = new Color(0.035f, 0.025f, 0.06f, 0.97f);
        private static readonly Color ColumnColor = new Color(1f, 1f, 1f, 0.035f);
        private static readonly Color LockedColor = new Color(0.16f, 0.14f, 0.19f, 0.95f);
        private static readonly Color TextColor = new Color(0.92f, 0.88f, 0.95f);
        private static readonly Color DimText = new Color(0.55f, 0.5f, 0.6f);
        private static readonly Color GoodColor = new Color(0.55f, 0.95f, 0.6f);
        private static readonly Color BadColor = new Color(1f, 0.45f, 0.45f);
        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.42f);
        private const float ResetConfirmSeconds = 3f;

        private Character player;
        private CharacterMovement2D playerMovement;

        private GameObject panel;
        private Text pointsText, goldText, noticeText;
        private Text detailName, detailBranch, detailBody, detailState;
        private readonly Image[] nodeBoxes = new Image[BlessingRules.TalentCount];
        private readonly Outline[] nodeOutlines = new Outline[BlessingRules.TalentCount];
        private readonly Text[] nodeNames = new Text[BlessingRules.TalentCount];
        private readonly Text[] nodeRanks = new Text[BlessingRules.TalentCount];
        private readonly Image[] nodeIcons = new Image[BlessingRules.TalentCount];
        private readonly Sprite[] nodeSprites = new Sprite[BlessingRules.TalentCount];
        private Image detailIcon;
        private bool hasArt;
        private readonly Vector2[] nodeCenters = new Vector2[BlessingRules.TalentCount];
        private readonly Image[] branchSpentBars = new Image[3];
        private readonly Text[] branchSpentTexts = new Text[3];
        private readonly System.Collections.Generic.List<(Image line, TalentBranch branch, int tier)> connectors = new();

        private int selected;
        private int openedFrame = -1;
        private float resetConfirmUntil = -1f;
        private float noticeUntil;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize(Character character, CharacterMovement2D movement)
        {
            player = character;
            playerMovement = movement;
            BuildPanel();
        }

        // ------------------------------------------------------------------ 화면 만들기

        private void BuildPanel()
        {
            TitleScreenUI.EnsureEventSystem();

            panel = new GameObject("BlessingPanel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = PanelColor;

            var art = Resources.Load<Texture2D>("UI/Menus/Iria_Talent");
            hasArt = art != null;
            if (art != null)
            {
                var bg = CreateRect(rect, "Art", 0f, 0f, W, H, typeof(RawImage)).GetComponent<RawImage>();
                bg.texture = art;
                bg.raycastTarget = false;
            }

            // 머리말
            var title = CreateText(rect, "Title", 60f, 22f, 700f, 66f, 30, GoldColor, TextAnchor.MiddleLeft);
            title.text = "이리아의 축복";
            title.fontStyle = FontStyle.Bold;
            var sub = CreateText(rect, "Sub", 62f, 64f, 900f, 88f, 15, DimText, TextAnchor.MiddleLeft);
            sub.text = "레벨이 오를 때마다 축복 점수 1점. 갈래에 점수를 모으면 더 깊은 축복이 열린다.";
            pointsText = CreateText(rect, "Points", 820f, 22f, 1220f, 58f, 24, TextColor, TextAnchor.MiddleRight);
            pointsText.fontStyle = FontStyle.Bold;
            goldText = CreateText(rect, "Gold", 820f, 58f, 1220f, 84f, 16, GoldColor, TextAnchor.MiddleRight);

            for (int b = 0; b < 3; b++) BuildBranch(rect, (TalentBranch)b);
            foreach (var node in BlessingRules.Nodes) BuildNode(rect, node);

            // 아래 설명 칸
            var detail = CreateRect(rect, "Detail", 60f, 584f, 1220f, 676f, typeof(Image)).GetComponent<Image>();
            detail.color = hasArt ? new Color(0.02f, 0.015f, 0.03f, 0.6f) : new Color(1f, 1f, 1f, 0.05f);
            detail.raycastTarget = false;
            // 아이콘 그림이 있으면 설명 칸 왼쪽에 고른 특성 아이콘을 크게 보여 준다.
            float detailLeft = 80f;
            if (System.Array.Exists(nodeSprites, s => s != null))
            {
                detailIcon = CreateRect(rect, "DetailIcon", 68f, 590f, 148f, 670f, typeof(Image)).GetComponent<Image>();
                detailIcon.preserveAspect = true;
                detailIcon.raycastTarget = false;
                detailLeft = 162f;
            }
            detailName = CreateText(rect, "DetailName", detailLeft, 590f, 640f, 620f, 20, TextColor, TextAnchor.MiddleLeft);
            detailName.fontStyle = FontStyle.Bold;
            detailBranch = CreateText(rect, "DetailBranch", 640f, 590f, 1200f, 620f, 15, DimText, TextAnchor.MiddleRight);
            detailBody = CreateText(rect, "DetailBody", detailLeft, 620f, 1200f, 646f, 16, TextColor, TextAnchor.MiddleLeft);
            detailState = CreateText(rect, "DetailState", detailLeft, 646f, 1200f, 672f, 15, GoodColor, TextAnchor.MiddleLeft);

            noticeText = CreateText(rect, "Notice", 60f, 682f, 900f, 708f, 15, GoodColor, TextAnchor.MiddleLeft);
            var help = CreateText(rect, "Help", 560f, 682f, 1220f, 708f, 14, DimText, TextAnchor.MiddleRight);
            help.text = "방향키 고르기 · Z 찍기 · R 초기화(골드) · X 닫기";

            panel.AddComponent<PanelFadeIn>(); // 켜질 때 공통 연출 + 여는 소리
            panel.SetActive(false);
        }

        private void BuildBranch(RectTransform root, TalentBranch branch)
        {
            int b = (int)branch;
            float cx = ColumnX[b];
            Color c = BlessingRules.BranchColors[b];

            var column = CreateRect(root, "Column" + b, cx - 180f, 100f, cx + 180f, 572f, typeof(Image)).GetComponent<Image>();
            // 배경 그림이 있으면 밝게 덮지 않고 갈래 색이 살짝 도는 어두운 반투명 판으로 깐다(뒤 감실 빛이 비치게).
            column.color = hasArt ? new Color(c.r * 0.08f, c.g * 0.08f, c.b * 0.08f, 0.55f) : ColumnColor;
            column.raycastTarget = false;

            var name = CreateText(root, "Branch" + b, cx - 170f, 104f, cx + 170f, 136f, 24, c, TextAnchor.MiddleCenter);
            name.text = BlessingRules.BranchNames[b];
            name.fontStyle = FontStyle.Bold;
            var desc = CreateText(root, "BranchDesc" + b, cx - 170f, 134f, cx + 170f, 154f, 13, DimText, TextAnchor.MiddleCenter);
            desc.text = BlessingRules.BranchDescs[b];

            // 이 갈래에 쓴 점수 막대(단계 해금 눈금 3·6·9)
            var barBg = CreateRect(root, "BarBg" + b, cx - 130f, 162f, cx + 130f, 168f, typeof(Image)).GetComponent<Image>();
            barBg.color = new Color(1f, 1f, 1f, 0.1f);
            barBg.raycastTarget = false;
            var bar = CreateRect(root, "Bar" + b, cx - 130f, 162f, cx + 130f, 168f, typeof(Image)).GetComponent<Image>();
            bar.color = c;
            bar.raycastTarget = false;
            branchSpentBars[b] = bar;
            for (int t = 1; t < BlessingRules.TierRequirement.Length; t++)
            {
                float x = cx - 130f + 260f * BlessingRules.TierRequirement[t] / 11f;
                var tick = CreateRect(root, "Tick" + b + t, x - 1f, 159f, x + 1f, 171f, typeof(Image)).GetComponent<Image>();
                tick.color = new Color(1f, 1f, 1f, 0.45f);
                tick.raycastTarget = false;
            }
            branchSpentTexts[b] = CreateText(root, "Spent" + b, cx - 170f, 170f, cx + 170f, 188f, 12, DimText, TextAnchor.MiddleCenter);

            // 단계 사이 연결선(아래 단계 → 위 단계, 열리면 갈래 색으로 밝아진다)
            for (int t = 1; t < TierY.Length; t++)
            {
                float y0 = TierY[t - 1] + NodeSize.y / 2f, y1 = TierY[t] - NodeSize.y / 2f;
                var line = CreateRect(root, "Link" + b + t, cx - 2f, y0, cx + 2f, y1, typeof(Image)).GetComponent<Image>();
                line.raycastTarget = false;
                connectors.Add((line, branch, t));
            }
        }

        private void BuildNode(RectTransform root, TalentNode node)
        {
            int i = (int)node.Id;
            float cx = ColumnX[(int)node.Branch] + (node.Slot < 0 ? 0f : (node.Slot == 0 ? -SlotOffset : SlotOffset));
            float cy = TierY[node.Tier];
            Vector2 size = node.IsKeystone ? KeystoneSize : NodeSize;
            nodeCenters[i] = new Vector2(cx, cy);

            var box = CreateRect(root, "Node_" + node.Id, cx - size.x / 2f, cy - size.y / 2f, cx + size.x / 2f, cy + size.y / 2f,
                typeof(Image), typeof(Button), typeof(Outline));
            nodeBoxes[i] = box.GetComponent<Image>();
            nodeOutlines[i] = box.GetComponent<Outline>();
            nodeOutlines[i].effectDistance = new Vector2(3f, -3f);
            var button = box.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            int index = i;
            button.onClick.AddListener(() =>
            {
                // 고른 특성을 한 번 더 누르면 찍는다(처음 누르면 고르기만).
                if (selected == index) TryRaise();
                else Select(index);
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null) es.SetSelectedGameObject(null);
            });

            // 아이콘 그림이 있으면 왼쪽에 얹고 글자를 오른쪽으로 민다.
            var iconSprite = Resources.Load<Sprite>("UI/Talents/" + node.Id);
            nodeSprites[i] = iconSprite;
            float textLeft = cx - size.x / 2f + 8f;
            if (iconSprite != null)
            {
                float s = size.y - 18f;
                var icon = CreateRect(root, "Icon_" + node.Id, cx - size.x / 2f + 8f, cy - s / 2f, cx - size.x / 2f + 8f + s, cy + s / 2f, typeof(Image)).GetComponent<Image>();
                icon.sprite = iconSprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                nodeIcons[i] = icon;
                textLeft += s + 6f;
            }

            nodeNames[i] = CreateText(root, "Name_" + node.Id, textLeft, cy - size.y / 2f + 6f, cx + size.x / 2f - 6f, cy + 4f,
                node.IsKeystone ? 17 : 16, TextColor, iconSprite != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            nodeNames[i].text = node.IsKeystone ? "◆ " + node.Name : node.Name;
            nodeNames[i].fontStyle = FontStyle.Bold;
            nodeRanks[i] = CreateText(root, "Rank_" + node.Id, textLeft, cy + 4f, cx + size.x / 2f - 6f, cy + size.y / 2f - 6f,
                13, DimText, iconSprite != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
        }

        // ------------------------------------------------------------------ 열고 닫기

        public void Show()
        {
            openedFrame = Time.frameCount;
            resetConfirmUntil = -1f;

            // 예전 축복(골드로 기도) 세이브라 골드를 돌려줬으면 처음 한 번 알려 준다.
            if (player.BlessingRefundNotice > 0)
            {
                Notice($"축복이 특성으로 바뀌어, 예전 축복에 바친 {player.BlessingRefundNotice:N0}골드를 돌려받았어요", GoodColor, 8f);
                player.BlessingRefundNotice = 0;
            }
            else
            {
                Notice("", GoodColor, 0f);
            }

            Refresh();
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            if (panel.activeSelf) UiSfx.Close();
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        // ------------------------------------------------------------------ 동작

        private void Select(int index)
        {
            if (Mathf.Clamp(index, 0, BlessingRules.TalentCount - 1) != selected) UiSfx.Move();
            selected = Mathf.Clamp(index, 0, BlessingRules.TalentCount - 1);
            resetConfirmUntil = -1f;
            Refresh();
        }

        private void TryRaise()
        {
            var id = (TalentId)selected;
            if (!player.CanRaiseTalent(id, out string reason))
            {
                Notice(reason, BadColor, 2.5f);
                Refresh();
                return;
            }
            player.RaiseTalent(id);
            var node = BlessingRules.Node(id);
            Notice($"{node.Name} {player.TalentRank(id)}/{node.MaxRank} — 이리아의 축복이 깃들었다", GoodColor, 2.5f);
            SfxPlayer.Play(SfxPlayer.LevelUp);
            Refresh();
        }

        // R 한 번: 비용 안내, 3초 안에 한 번 더: 골드를 내고 초기화.
        private void TryReset()
        {
            int spent = player.TalentPointsSpent;
            if (spent == 0)
            {
                Notice("아직 찍은 축복이 없어요", DimText, 2f);
                return;
            }
            int cost = BlessingRules.ResetCost(spent);
            if (Time.unscaledTime > resetConfirmUntil)
            {
                resetConfirmUntil = Time.unscaledTime + ResetConfirmSeconds;
                Notice($"R을 한 번 더 누르면 {cost:N0}골드를 내고 축복 {spent}점을 모두 되돌려요", GoldColor, ResetConfirmSeconds);
                return;
            }
            resetConfirmUntil = -1f;
            if (!player.TrySpendGold(cost))
            {
                Notice($"골드가 모자라요 ({cost:N0}골드 필요)", BadColor, 2.5f);
                return;
            }
            player.ResetTalents();
            Notice($"축복을 모두 되돌렸어요 — 점수 {spent}점을 다시 찍을 수 있어요", GoodColor, 3f);
            Refresh();
        }

        private void Notice(string message, Color color, float seconds)
        {
            noticeText.text = message;
            noticeText.color = color;
            noticeUntil = Time.unscaledTime + seconds;
        }

        // 방향키: 그 방향에 있는 특성 중 가장 가까운 것(옆으로 많이 벗어난 것은 덜 가깝게 친다).
        private void Move(Vector2 dir)
        {
            Vector2 from = nodeCenters[selected];
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < nodeCenters.Length; i++)
            {
                if (i == selected) continue;
                Vector2 d = nodeCenters[i] - from;
                d.y = -d.y; // 화면 좌표는 아래로 갈수록 y가 크다
                float along = Vector2.Dot(d, dir);
                if (along <= 1f) continue;
                float side = Mathf.Abs(dir.x != 0f ? d.y : d.x);
                float score = along + side * 2.5f;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            if (best >= 0) Select(best);
        }

        private void Update()
        {
            if (!IsOpen) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (noticeText.text.Length > 0 && Time.unscaledTime > noticeUntil) noticeText.text = "";

            // 연 그 프레임의 입력(이리아 대화의 숫자키 등)이 같은 프레임에 다시 소비되지 않게 한다.
            if (Time.frameCount == openedFrame) return;

            if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame) { Hide(); return; }
            if (keyboard.upArrowKey.wasPressedThisFrame) { Move(Vector2.up); return; }
            if (keyboard.downArrowKey.wasPressedThisFrame) { Move(Vector2.down); return; }
            if (keyboard.leftArrowKey.wasPressedThisFrame) { Move(Vector2.left); return; }
            if (keyboard.rightArrowKey.wasPressedThisFrame) { Move(Vector2.right); return; }
            if (keyboard.rKey.wasPressedThisFrame) { TryReset(); return; }
            if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                TryRaise();
        }

        // ------------------------------------------------------------------ 다시 그리기

        private void Refresh()
        {
            int available = player.TalentPointsAvailable;
            pointsText.text = $"남은 축복 점수  <color=#{Hex(available > 0 ? GoodColor : DimText)}>{available}</color>";
            goldText.text = $"보유 {player.Gold:N0}골드   ·   초기화 {BlessingRules.ResetCost(player.TalentPointsSpent):N0}골드";

            for (int b = 0; b < 3; b++)
            {
                int spent = player.TalentPointsSpentIn((TalentBranch)b);
                var rt = (RectTransform)branchSpentBars[b].transform;
                float cx = ColumnX[b];
                float x1 = cx - 130f + 260f * Mathf.Clamp01(spent / 11f);
                rt.anchorMax = new Vector2(x1 / W, rt.anchorMax.y);
                branchSpentTexts[b].text = $"{spent} / 11점";
            }

            foreach (var (line, branch, tier) in connectors)
            {
                bool open = player.TalentPointsSpentIn(branch) >= BlessingRules.TierRequirement[tier];
                Color c = BlessingRules.BranchColors[(int)branch];
                line.color = open ? new Color(c.r, c.g, c.b, 0.85f) : new Color(1f, 1f, 1f, 0.12f);
            }

            foreach (var node in BlessingRules.Nodes)
            {
                int i = (int)node.Id;
                int rank = player.TalentRank(node.Id);
                Color c = BlessingRules.BranchColors[(int)node.Branch];
                bool tierOpen = player.TalentPointsSpentIn(node.Branch) >= BlessingRules.TierRequirement[node.Tier];
                bool rivalTaken = node.Rival.HasValue && player.TalentRank(node.Rival.Value) > 0;
                bool maxed = rank >= node.MaxRank;

                Color box;
                if (rank > 0) box = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.97f);
                else if (rivalTaken || !tierOpen) box = LockedColor;
                else box = new Color(c.r * 0.22f, c.g * 0.22f, c.b * 0.22f, 0.97f);
                nodeBoxes[i].color = box;

                bool isSelected = i == selected;
                nodeOutlines[i].effectColor = isSelected ? Color.white : (maxed ? GoldColor : new Color(c.r, c.g, c.b, rank > 0 ? 0.9f : 0.25f));
                nodeOutlines[i].effectDistance = isSelected ? new Vector2(4f, -4f) : new Vector2(2f, -2f);

                nodeNames[i].color = (rivalTaken || !tierOpen) && rank == 0 ? DimText : TextColor;
                // 찍은 칸은 바탕이 밝아서(특히 수호 금빛) 단계 글자도 밝게 쓴다.
                nodeRanks[i].color = rank > 0 ? new Color(1f, 1f, 1f, 0.9f) : (rivalTaken || !tierOpen ? DimText : new Color(0.78f, 0.75f, 0.82f));
                if (nodeIcons[i] != null) nodeIcons[i].color = (rivalTaken || !tierOpen) && rank == 0 ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : Color.white;
                if (rivalTaken) nodeRanks[i].text = "갈림길 잠김";
                else if (!tierOpen) nodeRanks[i].text = $"{BlessingRules.TierRequirement[node.Tier]}점 필요";
                else nodeRanks[i].text = maxed ? $"<color=#{Hex(GoldColor)}>{rank}/{node.MaxRank} 최대</color>" : $"{rank}/{node.MaxRank}";
            }

            RefreshDetail();
        }

        private void RefreshDetail()
        {
            var id = (TalentId)selected;
            var node = BlessingRules.Node(id);
            int rank = player.TalentRank(id);
            Color c = BlessingRules.BranchColors[(int)node.Branch];

            detailName.text = (node.IsKeystone ? "◆ " : "") + node.Name + $"   <size=15><color=#{Hex(DimText)}>{rank}/{node.MaxRank}</color></size>";
            detailName.color = c;
            if (detailIcon != null)
            {
                detailIcon.sprite = nodeSprites[selected];
                detailIcon.enabled = nodeSprites[selected] != null;
            }
            string kind = node.IsKeystone ? "핵심 특성" : (node.Rival.HasValue ? "갈림길 — 둘 중 하나만" : "특성");
            detailBranch.text = $"{BlessingRules.BranchNames[(int)node.Branch]} {node.Tier + 1}단계 · {kind}";

            if (node.PerRank > 0f)
            {
                string now = rank > 0 ? BlessingRules.Describe(node, rank) : "없음";
                detailBody.text = rank < node.MaxRank
                    ? $"지금: {now}   <color=#{Hex(GoodColor)}>→ 다음: {BlessingRules.Describe(node, rank + 1)}</color>"
                    : $"지금: {now} (최대)";
            }
            else
            {
                detailBody.text = node.Desc;
            }

            if (player.CanRaiseTalent(id, out string reason))
            {
                detailState.text = "Z를 누르면 축복 점수 1점으로 찍어요";
                detailState.color = GoodColor;
            }
            else
            {
                detailState.text = rank >= node.MaxRank ? "다 찍었어요" : reason;
                detailState.color = rank >= node.MaxRank ? GoldColor : BadColor;
            }
        }

        private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // ---- 1280x720 화면 좌표(왼쪽 위 x0,y0 ~ 오른쪽 아래 x1,y1)로 요소를 만드는 도우미들 ----

        private static RectTransform CreateRect(Transform parent, string name, float x0, float y0, float x1, float y1, params System.Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            foreach (var t in components) go.AddComponent(t);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0 / W, 1f - y1 / H);
            rect.anchorMax = new Vector2(x1 / W, 1f - y0 / H);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Text CreateText(Transform parent, string name, float x0, float y0, float x1, float y1, int fontSize, Color color, TextAnchor alignment)
        {
            var text = CreateRect(parent, name, x0, y0, x1, y1, typeof(Text)).GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }
    }
}

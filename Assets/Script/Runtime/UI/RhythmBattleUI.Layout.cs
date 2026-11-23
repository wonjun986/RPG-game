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
    // 화면 구성: 패널/내 턴 프레임/행동·스킬 선택 창 만들기와 UI 생성 도우미.
    public partial class RhythmBattleUI
    {
        // ---- 화면 구성 ----

        private void BuildPanel()
        {
            panel = new GameObject("RhythmBattlePanel", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            // 전투 중에는 뒤쪽 월드가 아예 안 보이도록 완전히 가린다(반투명으로 비치지 않게 알파 1).
            // 전투가 시작되는 순간엔 알파 0에서 시작해서 암전 연출로 서서히 1까지 올라간다(Begin 참고).
            dimBackdrop = panel.GetComponent<Image>();
            dimBackdrop.color = new Color(0.03f, 0.02f, 0.05f, 1f);
            dimBackdrop.raycastTarget = true;

            // 전투 화면은 배경 그림(1672x941) 비율 그대로, 캔버스 높이 720 기준 크기(PxToCanvas)로 고정해 가운데에 둔다.
            // 예전에는 화면 전체에 늘려 깔아서, 16:9가 아닌 화면(16:10 전체화면, 가로로 긴 창 등)에서는 틀/레인은 늘어나는데
            // 날아오는 노드는 고정 배율(PxToCanvas)로 자리를 잡아 레인/판정 원과 어긋났다. 이제 화면에 맞춰 통째로 확대/축소한다(FitContentToScreen).
            contentRoot = new GameObject("Content", typeof(RectTransform));
            var contentRect = (RectTransform)contentRoot.transform;
            contentRect.SetParent(rect, false);
            contentRect.anchorMin = contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(ImageWidth, ImageHeight) * PxToCanvas;
            contentRect.anchoredPosition = Vector2.zero;

            // 최종보스(루나벨)전에서만 전투 틀 뒤에 깔리는 배경(절망의 언덕). 틀 가운데가 투명해서 그 사이로 비친다.
            // 노드/초상화가 묻히지 않게 어둡게 깔고, 화면 비율이 달라도 빈틈 없이 덮는다(EnvelopeParent).
            var bgGO = new GameObject("BossBackground", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            var bgRect = (RectTransform)bgGO.transform;
            bgRect.SetParent(contentRect, false);
            battleBackground = bgGO.GetComponent<RawImage>();
            battleBackground.raycastTarget = false;
            battleBackground.color = BattleBackgroundTint;
            battleBackground.texture = Resources.Load<Texture2D>(LunabelBattleBackgroundPath);
            var bgFitter = bgGO.GetComponent<AspectRatioFitter>();
            bgFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            if (battleBackground.texture != null)
                bgFitter.aspectRatio = (float)battleBackground.texture.width / battleBackground.texture.height;
            bgGO.SetActive(false);

            // 배경 위, 두 레인 뒤에 까는 어두운 띠. 붉은 배경(특히 아래 레인 뒤의 용암 절벽)에 노드가 묻히지 않게 한다.
            // 위아래 가장자리는 부드럽게 흐려지는 세로 그라데이션이라 띠 경계가 줄무늬처럼 보이지 않는다.
            laneBackdrop = CreateRawImage(contentRect, "LaneBackdrop", LaneBackdropX0, LaneBackdropY0, LaneBackdropX1, LaneBackdropY1);
            laneBackdrop.texture = CreateLaneBackdropTexture();
            laneBackdrop.color = new Color(1f, 1f, 1f, LaneBackdropAlpha);
            laneBackdrop.gameObject.SetActive(false);

            var frame = new GameObject("Frame", typeof(RectTransform), typeof(RawImage));
            var frameRect = (RectTransform)frame.transform;
            frameRect.SetParent(contentRect, false);
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            frame.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/Battle/RhythmBattleFrame");

            var monsterBar = BuildHpBar(contentRect, "MonsterBar", 950f, 60f, 1520f, 150f,
                new Color(0.8f, 0.1f, 0.15f), new Color(1f, 0.85f, 0.4f), "");
            monsterHpFill = monsterBar.Fill;
            monsterNameLabel = monsterBar.NameLabel;
            monsterHpText = monsterBar.ValueLabel;

            var playerBar = BuildHpBar(contentRect, "PlayerBar", 150f, 60f, 650f, 150f,
                new Color(0.25f, 0.55f, 0.95f), new Color(0.85f, 0.9f, 1f), "플레이어");
            playerHpFill = playerBar.Fill;
            playerHpText = playerBar.ValueLabel;
            // 플레이어 체력바 바로 아래의 필살 게이지.
            var gaugeBar = BuildHpBar(contentRect, "SkillGauge", 150f, 155f, 560f, 225f,
                SkillNoteColor, SkillNoteColor, "필살 게이지");
            skillGaugeFill = gaugeBar.Fill;
            skillGaugeText = gaugeBar.ValueLabel;

            playerPortrait = CreateImage(contentRect, "PlayerPortrait", 270f, 250f, 530f, 510f);
            playerPortrait.preserveAspect = true;

            monsterStage = CreateRect(contentRect, "MonsterStage", 1080f, 230f, 1380f, 530f, typeof(RectTransform));
            monsterPortrait = new GameObject("MonsterPortrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            monsterPortrait.raycastTarget = false;
            var monsterPortraitRect = monsterPortrait.rectTransform;
            monsterPortraitRect.SetParent(monsterStage, false);
            // 칸의 아래쪽 가운데에 발밑(스프라이트 기준점과 같은 아래 중앙)을 붙이고, 크기는 프레임마다 SetMonsterFrame이 정한다.
            monsterPortraitRect.anchorMin = new Vector2(0.5f, 0f);
            monsterPortraitRect.anchorMax = new Vector2(0.5f, 0f);
            monsterPortraitRect.pivot = new Vector2(0.5f, 0f);
            monsterPortraitRect.anchoredPosition = Vector2.zero;
            // 몬스터 원화는 기본이 오른쪽을 보는 방향이라(월드의 flipX 관례와 동일), 왼쪽의
            // 플레이어를 바라보도록 좌우로 뒤집는다.
            monsterPortraitRect.localScale = new Vector3(-1f, 1f, 1f);

            // 스킬 마무리 연출 동안 몬스터 위에 겹쳐 반복 재생하는 이펙트(몬스터 초상화보다 살짝 크게).
            skillEffectImage = CreateImage(contentRect, "SkillEffect", 1030f, 190f, 1430f, 570f);
            skillEffectImage.preserveAspect = true;
            skillEffectImage.gameObject.SetActive(false);

            // 일반 공격 베기 이펙트는 몬스터 몸통 위에 겹친다.
            basicSlashImage = CreateImage(contentRect, "BasicSlash", 1060f, 230f, 1400f, 560f);
            basicSlashImage.preserveAspect = true;
            basicSlashImage.gameObject.SetActive(false);

            // 데미지 숫자는 몬스터 칸 위쪽 가장자리에서 떠오른다(이펙트보다 위에 그려지게 뒤에 만든다).
            damageNumberLayer = CreateRect(contentRect, "DamageNumbers", 1080f, 230f, 1380f, 530f, typeof(RectTransform));
            // 루나벨에게 맞은 피해 숫자는 플레이어 초상화 칸 위쪽에서 떠오른다.
            playerDamageNumberLayer = CreateRect(contentRect, "PlayerDamageNumbers", 270f, 250f, 530f, 510f, typeof(RectTransform));

            phaseBannerText = CreateText(contentRect, "PhaseBanner", 600f, 160f, 1070f, 230f, 42f, Color.white, TextAnchor.MiddleCenter);
            phaseBannerText.fontStyle = FontStyle.Bold;

            // 그림마다 가로세로 비율이 달라(My Turn 2171x724, Enemy Turn 1862x845) 상자를 넉넉히 잡고
            // preserveAspect로 안쪽에 맞춰 띄운다 — 어느 쪽이 떠도 찌그러지거나 잘리지 않는다.
            turnBannerImage = CreateImage(contentRect, "TurnBanner", 336f, 77f, 1336f, 443f);
            turnBannerImage.preserveAspect = true;
            turnBannerImage.gameObject.SetActive(false);
            myTurnBannerSprite = Resources.Load<Sprite>("UI/Battle/MyTurnBanner");
            enemyTurnBannerSprite = Resources.Load<Sprite>("UI/Battle/EnemyTurnBanner");

            resultText = CreateText(contentRect, "ResultText", 550f, 380f, 1120f, 460f, 40f, Color.white, TextAnchor.MiddleCenter);
            resultText.fontStyle = FontStyle.Bold;

            judgmentImage = CreateImage(contentRect, "JudgmentLogo", 640f, 335f, 1030f, 505f);
            judgmentImage.preserveAspect = true;
            judgmentImage.gameObject.SetActive(false);

            for (int i = 0; i < HoldBarPoolSize; i++)
            {
                var bar = CreateMovableImage(contentRect, "HoldBar" + i, 1f);
                bar.rectTransform.pivot = new Vector2(0f, 0.5f);
                bar.gameObject.SetActive(false);
                holdBarPool[i] = bar;
            }

            var nodeSprite = Resources.Load<Sprite>("UI/Battle/NodeDiamond");
            defaultNoteSprite = nodeSprite;
            for (int i = 0; i < NotePoolSize; i++)
            {
                var note = CreateMovableImage(contentRect, "Note" + i, NoteSize);
                note.sprite = nodeSprite;
                // 배경이 깔리는 전투(루나벨전)에서 붉은 노드가 붉은 배경에 묻히지 않게 어두운 테두리를 두른다.
                var outline = note.gameObject.AddComponent<Outline>();
                outline.effectColor = NoteOutlineColor;
                outline.effectDistance = new Vector2(3f, -3f);
                note.gameObject.SetActive(false);
                notePool[i] = note;
            }
            for (int i = 0; i < 2; i++)
            {
                float cx = NodeCenter[i].x;
                float cy = NodeCenter[i].y;
                var exText = CreateText(contentRect, "Exclaim" + i, cx - 36f, cy - 128f, cx + 36f, cy - 40f, 54f, Color.white, TextAnchor.MiddleCenter);
                exText.fontStyle = FontStyle.Bold;
                exText.text = "!";
                exText.gameObject.SetActive(false);
                exclaim[i] = exText;
            }

            nodeBurstImage = CreateMovableImage(contentRect, "NodeBurst", 220f);
            nodeBurstImage.raycastTarget = false;
            nodeBurstImage.gameObject.SetActive(false);

            // 보스 필살기 검기. 원화는 오른쪽으로 굴러가는 모양이라, 왼쪽의 플레이어 쪽으로 날아가게 좌우로 뒤집는다.
            swordWaveImage = CreateMovableImage(contentRect, "BossSwordWave", 1f);
            swordWaveImage.rectTransform.sizeDelta = new Vector2(SwordWaveWidth, SwordWaveHeight) * PxToCanvas;
            swordWaveImage.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            swordWaveImage.gameObject.SetActive(false);

            // 루나벨의 피의 손. 원화는 갈퀴가 오른쪽을 향해 있어서, 왼쪽의 플레이어 쪽으로 날아가게 좌우로 뒤집는다.
            Vector2 bloodHandSize = bloodHandFrames.Length > 0
                ? new Vector2(BloodHandHeight * bloodHandFrames[0].rect.width / bloodHandFrames[0].rect.height, BloodHandHeight)
                : new Vector2(BloodHandHeight * 0.6f, BloodHandHeight);
            for (int i = 0; i < BloodHandPoolSize; i++)
            {
                var hand = CreateMovableImage(contentRect, "BloodHand" + i, 1f);
                hand.rectTransform.sizeDelta = bloodHandSize * PxToCanvas;
                hand.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                hand.gameObject.SetActive(false);
                bloodHandPool[i] = hand;
            }

            // 루나벨 2페이즈 필살기의 핏빛 구체. 크기는 날아가는 동안 BloodOrbRoutine이 정한다.
            bloodOrbImage = CreateMovableImage(contentRect, "BloodOrb", 1f);
            // 필살기의 피의 기둥. 플레이어 발밑에 바닥 가운데를 붙이고, 크기는 프레임마다 SetBloodPillarFrame이 정한다.
            bloodPillarImage = CreateMovableImage(contentRect, "BloodPillar", 1f);
            bloodPillarImage.rectTransform.pivot = new Vector2(0.5f, 0f);
            bloodPillarImage.rectTransform.anchoredPosition = ImageToCanvasPos(BloodPillarX, BloodPillarBottomY);
            bloodPillarImage.gameObject.SetActive(false);

            // 모래룡 황금 브레스. 빔 그림(512x512 칸, 오른쪽 위 출발점 → 왼쪽 아래 충돌점 대각선)의 출발점을 피벗으로 두고,
            // 모래룡 입(충전 구슬)에서 플레이어 가슴까지 닿게 회전/크기를 맞춘다(FireSandBeam).
            sandBeamImage = CreateMovableImage(contentRect, "SandDragonBeam", 1f);
            sandBeamImage.rectTransform.pivot = new Vector2(SandBeamOriginPx.x / SandBeamCellPx, 1f - SandBeamOriginPx.y / SandBeamCellPx);
            sandBeamImage.gameObject.SetActive(false);
            bloodOrbImage.gameObject.SetActive(false);

            BuildMyTurnFrame(contentRect);

            // "BATTLE!!" 슬램 연출. 암전 직후, 전투 UI(contentRoot)가 드러나기 전에 잠깐 재생된다.
            // 프레임 한 장이 세로로 긴 비율(307:512)이라 그에 맞춰 박스도 세로로 길게 잡는다.
            battleSplashImage = CreateImage(rect, "BattleSplash", 656f, 150f, 1016f, 750f);
            battleSplashImage.preserveAspect = true;
            battleSplashImage.gameObject.SetActive(false);
            lunabelSplashImage = CreateImage(rect, "LunabelBattleSplash", 336f, 230f, 1336f, 711f);
            lunabelSplashImage.preserveAspect = true;
            lunabelSplashImage.gameObject.SetActive(false);

            BuildLunabelNameCard(rect);

            // 궁극기 각성용 암전 판. 맨 마지막에 만들어 다른 모든 요소 위에 그려지고, 평소엔 완전히 투명.
            var flashGO = new GameObject("UltimateFlash", typeof(RectTransform), typeof(Image));
            var flashRect = (RectTransform)flashGO.transform;
            flashRect.SetParent(rect, false);
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            ultimateFlashOverlay = flashGO.GetComponent<Image>();
            ultimateFlashOverlay.color = new Color(0f, 0f, 0f, 0f);
            ultimateFlashOverlay.raycastTarget = false;

            // 궁극기 각성 때 EyeFlash 프레임을 화면 가득 보여줄 전용 이미지. 암전 판 바로 위라 암전된
            // 검은 배경 위에서 깜빡인다. 세로가 긴 그림이라 preserveAspect로 두면 화면보다 좁아 양옆에
            // 여백이 남는데, 그 여백도 뒤의 암전 판 덕분에 검게 메워져 자연스럽게 화면을 꽉 채운 것처럼 보인다.
            var eyeFullGO = new GameObject("UltimateEyeFullscreen", typeof(RectTransform), typeof(Image));
            var eyeFullRect = (RectTransform)eyeFullGO.transform;
            eyeFullRect.SetParent(rect, false);
            eyeFullRect.anchorMin = Vector2.zero;
            eyeFullRect.anchorMax = Vector2.one;
            eyeFullRect.offsetMin = Vector2.zero;
            eyeFullRect.offsetMax = Vector2.zero;
            ultimateEyeFullscreenImage = eyeFullGO.GetComponent<Image>();
            ultimateEyeFullscreenImage.preserveAspect = true;
            ultimateEyeFullscreenImage.raycastTarget = false;
            ultimateEyeFullscreenImage.gameObject.SetActive(false);

            panel.SetActive(false);
        }

        // 내 턴 동안 뜨는 테두리 창(UI/BattleMyTurn, 1448x1086). 위쪽 가로 띠(캐릭터 오른쪽)에 제목("나의 턴"/"스킬")을,
        // 아래 큰 칸(대략 x 200~1370, y 465~940)에 행동 선택 창 또는 스킬 목록을 띄운다.
        private const float MyTurnImageWidth = 1448f;
        private const float MyTurnImageHeight = 1086f;
        private const float MyTurnMainCenterX = 785f;
        private const float MyTurnMainCenterY = 702f;

        private void BuildMyTurnFrame(Transform parent)
        {
            myTurnFrame = new GameObject("MyTurnFrame", typeof(RectTransform), typeof(RawImage));
            var frameRect = (RectTransform)myTurnFrame.transform;
            frameRect.SetParent(parent, false);
            frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = new Vector2(960f, 720f); // 기준 해상도 높이에 꽉 차게, 그림 비율(약 4:3) 그대로.
            frameRect.anchoredPosition = Vector2.zero;
            var bg = myTurnFrame.GetComponent<RawImage>();
            bg.texture = Resources.Load<Texture2D>("UI/Battle/BattleMyTurn");
            bg.raycastTarget = false;

            myTurnTitleText = CreateText(frameRect, "Title", 0f, 0f, 0f, 0f, 30f, SkillNoteColor, TextAnchor.MiddleCenter);
            SetMyTurnAnchors(myTurnTitleText.rectTransform, 540f, 290f, 1320f, 420f);
            myTurnTitleText.fontSize = 36;
            myTurnTitleText.fontStyle = FontStyle.Bold;

            BuildActionSelectPanel(frameRect);
            BuildSkillSelectPanel(frameRect);

            myTurnFrame.SetActive(false);
        }

        // 행동 선택 창. UI/BattleActionSelect 그림 안에 공격/스킬/아이템/도주 4칸 아이콘+글자가 이미 그려져 있어서,
        // 고른 칸 아래에 Select_Effect 빛줄기만 옮겨가며 띄운다. 내 턴 테두리 창의 아래 큰 칸 한가운데에 놓는다
        // (위아래 장식 가시는 칸 테두리에 살짝 걸친다).
        private void BuildActionSelectPanel(RectTransform parent)
        {
            actionSelectPanel = new GameObject("ActionSelect", typeof(RectTransform));
            var panelRect = (RectTransform)actionSelectPanel.transform;
            panelRect.SetParent(parent, false);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(MyTurnMainCenterX / MyTurnImageWidth, 1f - MyTurnMainCenterY / MyTurnImageHeight);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(724f, 543f); // UI/BattleActionSelect(1448x1086)의 절반 크기.
            panelRect.anchoredPosition = Vector2.zero;

            var bgGO = new GameObject("BG", typeof(RectTransform), typeof(RawImage));
            var bgRect = (RectTransform)bgGO.transform;
            bgRect.SetParent(panelRect, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGO.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/Battle/BattleActionSelect");

            var cursorGO = new GameObject("Cursor", typeof(RectTransform), typeof(RawImage));
            actionCursor = (RectTransform)cursorGO.transform;
            actionCursor.SetParent(panelRect, false);
            actionCursor.pivot = new Vector2(0.5f, 0.5f);
            actionCursor.sizeDelta = new Vector2(120f, 16f);
            actionCursor.anchorMin = actionCursor.anchorMax = new Vector2(ActionSlotX[0], 0.30f);
            actionCursorImage = cursorGO.GetComponent<RawImage>();
            actionCursorImage.texture = Resources.Load<Texture2D>("UI/Common/Select_Effect");
            actionCursorImage.raycastTarget = false;

            var lockGO = new GameObject("FleeLock", typeof(RectTransform), typeof(RawImage));
            var lockRect = (RectTransform)lockGO.transform;
            lockRect.SetParent(panelRect, false);
            lockRect.anchorMin = new Vector2(FleeLockX0 / ActionSelectImageWidth, 1f - FleeLockY1 / ActionSelectImageHeight);
            lockRect.anchorMax = new Vector2(FleeLockX1 / ActionSelectImageWidth, 1f - FleeLockY0 / ActionSelectImageHeight);
            lockRect.offsetMin = Vector2.zero;
            lockRect.offsetMax = Vector2.zero;
            fleeLockImage = lockGO.GetComponent<RawImage>();
            fleeLockImage.raycastTarget = false;
            for (int i = 0; i < FleeLockFrameCount; i++) fleeLockFrames[i] = Resources.Load<Texture2D>(FleeLockPath + (i + 1));
            lockGO.SetActive(false);
            // 선택 빛줄기는 잠금 그림 위로 보이게 한다.
            actionCursor.SetAsLastSibling();

            // 안내 문구(포션 개수, 사용 불가 이유 등)는 테두리 창의 큰 칸 아래쪽에 띄운다. 행동 선택 창과 같이 켜지고
            // 꺼지도록 창의 자식으로 두되, 위치는 테두리 창 기준으로 잡는다.
            var msgGO = new GameObject("Message", typeof(RectTransform), typeof(Text), typeof(Outline));
            var msgRect = (RectTransform)msgGO.transform;
            msgRect.SetParent(panelRect, false);
            msgRect.anchorMin = msgRect.anchorMax = new Vector2(0.5f, 0.5f);
            msgRect.pivot = new Vector2(0.5f, 0.5f);
            msgRect.sizeDelta = new Vector2(700f, 36f);
            msgRect.anchoredPosition = new Vector2(0f, -(905f - MyTurnMainCenterY) * (720f / MyTurnImageHeight));
            msgGO.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            actionMessageText = msgGO.GetComponent<Text>();
            actionMessageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            actionMessageText.fontSize = 22;
            actionMessageText.fontStyle = FontStyle.Bold;
            actionMessageText.alignment = TextAnchor.MiddleCenter;
            actionMessageText.color = MissColor;
            actionMessageText.raycastTarget = false;

            actionSelectPanel.SetActive(false);
        }

        // 스킬 목록. 내 턴 테두리 창 전체를 덮는 빈 판이라 좌표는 UI/BattleMyTurn 그림 픽셀 기준 그대로 쓴다.
        // 행동 선택 창 대신 아래 큰 칸에 스킬 목록/안내 문구/조작 안내를 글자로 그린다(제목은 테두리 창이 "스킬"로 바꿔 보여준다).
        private void BuildSkillSelectPanel(RectTransform parent)
        {
            skillSelectPanel = new GameObject("SkillSelect", typeof(RectTransform));
            var panelRect = (RectTransform)skillSelectPanel.transform;
            panelRect.SetParent(parent, false);
            SetRelativeAnchors(panelRect, 0f, 0f, 1f, 1f);

            // 아래 큰 칸(대략 x 200~1370, y 465~940) 안에 다섯 줄. 왼쪽은 캐릭터 옷자락이 걸쳐 있어 조금 띄운다.
            const float rowTop = 485f;
            const float rowHeight = 64f;
            for (int i = 0; i < Skills.Length; i++)
            {
                var row = CreateText(panelRect, "Skill" + i, 0f, 0f, 0f, 0f, 26f, Color.white, TextAnchor.MiddleLeft);
                float top = rowTop + i * rowHeight;
                SetMyTurnAnchors(row.rectTransform, 300f, top, 1330f, top + rowHeight);
                row.fontSize = 24;
                row.fontStyle = FontStyle.Bold;
                skillRowTexts[i] = row;
            }

            skillMessageText = CreateText(panelRect, "Message", 0f, 0f, 0f, 0f, 22f, MissColor, TextAnchor.MiddleCenter);
            SetMyTurnAnchors(skillMessageText.rectTransform, 220f, 810f, 1350f, 865f);
            skillMessageText.fontSize = 20;
            skillMessageText.fontStyle = FontStyle.Bold;

            var hint = CreateText(panelRect, "Hint", 0f, 0f, 0f, 0f, 18f, new Color(0.75f, 0.75f, 0.8f), TextAnchor.MiddleCenter);
            SetMyTurnAnchors(hint.rectTransform, 220f, 870f, 1350f, 925f);
            hint.fontSize = 17;
            hint.text = "↑↓ 선택   Z 사용   X 뒤로";

            skillSelectPanel.SetActive(false);
        }

        // UI/BattleMyTurn 그림 속 픽셀 사각형(왼쪽 위 기준)을 스킬 창 안의 앵커로 바꾼다.
        private static void SetMyTurnAnchors(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            SetRelativeAnchors(rect, x0 / MyTurnImageWidth, 1f - y1 / MyTurnImageHeight, x1 / MyTurnImageWidth, 1f - y0 / MyTurnImageHeight);
        }

        // CreateText 등은 배경 그림 픽셀 좌표 기준이라, 작은 창 안쪽 요소는 만든 뒤 창 기준 0~1 비율로 다시 잡는다.
        private static void SetRelativeAnchors(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private HpBarRefs BuildHpBar(Transform parent, string name, float x0, float y0, float x1, float y1, Color barColor, Color nameColor, string nameText)
        {
            var rect = CreateRect(parent, name, x0, y0, x1, y1, typeof(RectTransform), typeof(Image));
            rect.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            rect.GetComponent<Image>().raycastTarget = false;

            var nameGO = new GameObject("Name", typeof(RectTransform), typeof(Text));
            var nameRect = (RectTransform)nameGO.transform;
            nameRect.SetParent(rect, false);
            nameRect.anchorMin = new Vector2(0f, 0.55f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            var nameLabel = nameGO.GetComponent<Text>();
            nameLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameLabel.fontSize = Mathf.RoundToInt(22f * PxToCanvas);
            nameLabel.fontStyle = FontStyle.Bold;
            nameLabel.alignment = TextAnchor.MiddleCenter;
            nameLabel.color = nameColor;
            nameLabel.text = nameText;
            nameLabel.raycastTarget = false;

            var fillBG = new GameObject("FillBG", typeof(RectTransform), typeof(Image));
            var fillBGRect = (RectTransform)fillBG.transform;
            fillBGRect.SetParent(rect, false);
            fillBGRect.anchorMin = new Vector2(0f, 0f);
            fillBGRect.anchorMax = new Vector2(1f, 0.5f);
            fillBGRect.offsetMin = new Vector2(6f, 4f);
            fillBGRect.offsetMax = new Vector2(-6f, 0f);
            fillBG.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.1f, 0.9f);
            fillBG.GetComponent<Image>().raycastTarget = false;

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGO.transform;
            fillRect.SetParent(fillBGRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            var fill = fillGO.GetComponent<Image>();
            fill.color = barColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            var valueGO = new GameObject("Value", typeof(RectTransform), typeof(Text));
            var valueRect = (RectTransform)valueGO.transform;
            valueRect.SetParent(fillBGRect, false);
            valueRect.anchorMin = Vector2.zero;
            valueRect.anchorMax = Vector2.one;
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;
            var valueLabel = valueGO.GetComponent<Text>();
            valueLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueLabel.fontSize = Mathf.RoundToInt(16f * PxToCanvas);
            valueLabel.fontStyle = FontStyle.Bold;
            valueLabel.alignment = TextAnchor.MiddleCenter;
            valueLabel.color = Color.white;
            valueLabel.raycastTarget = false;

            return new HpBarRefs(fill, nameLabel, valueLabel);
        }

        // ---- 그림 픽셀 좌표(왼쪽 위 x0,y0 ~ 오른쪽 아래 x1,y1)로 요소를 만드는 도우미들 ----

        private static RectTransform CreateRect(Transform parent, string name, float x0, float y0, float x1, float y1, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0 / ImageWidth, 1f - y1 / ImageHeight);
            rect.anchorMax = new Vector2(x1 / ImageWidth, 1f - y0 / ImageHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RawImage CreateRawImage(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var image = CreateRect(parent, name, x0, y0, x1, y1, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateImage(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var image = CreateRect(parent, name, x0, y0, x1, y1, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        // 고정된 두 변 사이를 늘려 채우는 CreateImage와 달리, 날아오는 노드처럼 매 프레임
        // anchoredPosition을 직접 바꿔 움직여야 하는 요소용 — 왼쪽위 한 점에 고정 크기로 붙고
        // 중심 기준(pivot 0.5,0.5)으로 움직인다.
        private static Image CreateMovableImage(Transform parent, string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        // 그림 픽셀 좌표(좌상단 기준)를 CreateMovableImage로 만든 요소의 anchoredPosition(좌상단 anchor,
        // 캔버스 단위, 아래로 갈수록 y가 음수)으로 바꾼다.
        private static Sprite[] LoadSortedSprites(string path)
        {
            var sprites = Resources.LoadAll<Sprite>(path);
            System.Array.Sort(sprites, (x, y) => string.CompareOrdinal(x.name, y.name));
            return sprites;
        }

        private static Vector2 ImageToCanvasPos(float ximg, float yimg)
        {
            return new Vector2(ximg * PxToCanvas, -(yimg * PxToCanvas));
        }

        private static Text CreateText(Transform parent, string name, float x0, float y0, float x1, float y1, float fontPx, Color color, TextAnchor alignment)
        {
            var text = CreateRect(parent, name, x0, y0, x1, y1, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(fontPx * PxToCanvas));
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}

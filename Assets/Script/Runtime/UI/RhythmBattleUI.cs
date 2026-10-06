using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.UI
{
    // 몬스터가 플레이어에게 근접 접촉하면(MonsterAI/BossAI/BossSlimeAI의 "사거리 안" 분기에서 호출)
    // 실시간 전투 대신 이 화면으로 전환해서 턴제 리듬 전투를 진행한다. 배경(UI/RhythmBattleFrame)
    // 아래쪽에 그려진 두 레인(위/아래) 중 하나가 "!"와 함께 오른쪽에서 왼쪽으로 날아오는 노드로 예고되고,
    // 판정점에 도착하는 타이밍에 맞는 키(위 레인=Z, 아래 레인=X)를 눌러야 한다 — 적 턴이면 막기,
    // 내 턴이면 공격이 성공한다. 보스는 한 "공격"에 노드가 2~5개 연달아 빠르게 오는 콤보도 섞인다.
    // 적 턴(고정 4회) → 내 턴(고정 4회)을 몬스터나 플레이어가 쓰러질 때까지 번갈아 반복한다.
    // 데미지 계산/치명타/보상(경험치·골드·드랍·퀘스트)은 전부 기존 Character.DealDamage / Monster.TakeDamage
    // 파이프라인을 그대로 통과시켜서, 승리 시 별도 처리 없이 기존 OnDied/AnyDied 훅이 다 처리해준다.
    // 패배(플레이어 체력 0)도 마찬가지로 Character.OnDied → DeathReturnUI가 그대로 이어받는다.
    public class RhythmBattleUI : MonoBehaviour
    {
        // 배경 그림(UI/RhythmBattleFrame, 1672x941) 픽셀 좌표계. 좌상단이 원점이고 아래로 갈수록 y가 커진다.
        private const float ImageWidth = 1672f;
        private const float ImageHeight = 941f;
        private const float PxToCanvas = 720f / ImageHeight;

        private const int LaneUp = 0;
        private const int LaneDown = 1;

        // 적 턴 한 "공격"마다 어떤 레인이 몇 번 연달아 오는지. 일반 몬스터는 항상 단발이다.
        private static readonly int[][] NormalAttackPatterns = { new[] { LaneUp }, new[] { LaneDown } };
        // 보스는 1~5개를 레인까지 완전히 무작위로 뽑는다 — 번갈아(위-아래-위)만 나오는 게 아니라
        // 같은 레인이 그대로 연달아(위-위-위-위-위 같은 5연타까지) 나오는 것도 섞여서 예측하기 어렵다.
        private const int BossComboMinLength = 1;
        private const int BossComboMaxLength = 5;
        // 그림에서 직접 잰 두 노드(판정점, 레인 트랙의 왼쪽 끝)의 중심 좌표.
        private static readonly Vector2[] NodeCenter = { new Vector2(301f, 664f), new Vector2(301f, 802f) };
        // 트랙이 오른쪽 장식 기둥에 닿기 전까지(약 x=1430) 이어지므로, 그 안쪽에서 노트가 출발한다.
        private const float NoteSpawnX = 1420f;
        private const float NoteSize = 80f; // 캔버스 단위(1280x720 기준).

        private const int AttacksPerTurn = 4;

        // 난이도: 일반 몬스터 / 보스. 몬스터별 전용 채보는 범위 밖이라 이 두 단계만 둔다.
        private const float NormalTelegraphDuration = 1.0f;
        private const float NormalBlockWindow = 0.18f;
        private const float NormalPerfectWindow = 0.1f;
        private const float NormalGoodWindow = 0.22f;
        private const float BossTelegraphDuration = 0.75f;
        private const float BossBlockWindow = 0.14f;
        private const float BossPerfectWindow = 0.08f;
        private const float BossGoodWindow = 0.18f;

        private const float PhaseBannerDuration = 0.9f;
        private const float BetweenHitsPause = 0.35f;
        // 공격 모션(0.5초)이 타격 순간까지 진행된 뒤에 데미지가 들어가게 한다(일격사여도 모션이 먼저 보이게).
        private const float AttackImpactDelay = 0.25f;

        // 콤보 패턴(한 "공격" 안에 노드가 여러 개)의 2번째부터는 훨씬 빠르고 텀 없이 온다 —
        // 처음 한 번만 평소처럼 예고하고, 그 다음은 "파바박" 연타하듯 빠르게 몰아친다.
        // (RunLanePrompt가 맞히는 즉시 끝나므로 실제 체감 간격은 이보다 더 짧다.)
        private const float ComboFollowupTelegraph = 0.2f;
        private const float ComboFollowupPause = 0.02f;

        private const float FleeCooldown = 1.5f;
        private const float FleeKnockbackDistance = 1.5f;

        // 접촉 → 암전 → "BATTLE!!" 연출 → 실제 전투 UI가 드러나는 도입부 타이밍.
        private const float BlackoutFadeDuration = 0.2f;
        private const float BattleSplashFrameInterval = 0.07f;
        private const float PostSplashPause = 0.15f;

        private static readonly Color PerfectColor = new Color(0.55f, 1f, 0.6f);
        private static readonly Color GoodColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color MissColor = new Color(1f, 0.45f, 0.45f);
        private static readonly Color EnemyNoteColor = new Color(1f, 0.35f, 0.35f, 0.95f);
        private static readonly Color PlayerNoteColor = new Color(0.75f, 0.45f, 1f, 0.95f);

        // 행동 선택 창(UI/BattleActionSelect)의 "공격/스킬/아이템/도주" 4칸 아이콘 중심 x(패널 안쪽 0~1 비율).
        private const int ActionAttack = 0;
        private const int ActionSkill = 1;
        private const int ActionItem = 2;
        private const int ActionFlee = 3;
        private static readonly float[] ActionSlotX = { 0.21875f, 0.40625f, 0.59375f, 0.78125f };

        // 노드를 성공적으로 맞혔을 때(막기 성공/공격 적중 모두) 그 자리에서 터지는 연출의 프레임 수/속도.
        private const float NodeBurstFrameInterval = 0.04f;

        private readonly struct HpBarRefs
        {
            public readonly Image Fill;
            public readonly Text NameLabel;
            public readonly Text ValueLabel;
            public HpBarRefs(Image fill, Text nameLabel, Text valueLabel)
            {
                Fill = fill;
                NameLabel = nameLabel;
                ValueLabel = valueLabel;
            }
        }

        private GameObject panel;
        private Image dimBackdrop;
        private GameObject contentRoot; // 프레임/체력바/초상화/레인 — 암전+BATTLE 연출이 끝난 뒤에 드러난다.
        private Image battleSplashImage;
        private Sprite[] battleSplashFrames = System.Array.Empty<Sprite>();
        private Image playerPortrait;
        private Image monsterPortrait;
        private Image monsterHpFill;
        private Text monsterNameLabel;
        private Text monsterHpText;
        private Image playerHpFill;
        private Text playerHpText;
        private Text phaseBannerText;
        private Text resultText;
        private readonly Image[] noteImages = new Image[2];
        private readonly Text[] exclaim = new Text[2];

        // 내 턴 시작 시 한 번 뜨는 행동 선택 창(공격/스킬/아이템/도주 중 고르고 Z/Enter로 확정).
        // 스킬/아이템은 아직 구현이 없어 고르면 안내만 뜨고 선택 창이 계속 열려 있는다.
        // 도주를 고르면 Esc와 같은 도망 처리(쿨다운+넉백)로 이어진다.
        private GameObject actionSelectPanel;
        private RectTransform actionCursor;
        private RawImage actionCursorImage;
        private Text actionMessageText;
        private int actionSelectIndex;
        private bool actionSelectFlee;

        // 노드 적중 시 터지는 연출(공격 성공/막기 성공 공통으로 재사용).
        private Image nodeBurstImage;
        private Sprite[] nodeBurstFrames = System.Array.Empty<Sprite>();

        // 플레이어 대기 애니메이션(왼쪽 초상화). 월드의 WalkSpriteAnimator와 같은 경로/속도(4fps)를 쓰되,
        // 전투 화면 전용으로 따로 돌린다 — 실제 플레이어가 그 순간 어떤 포즈로 멈췄는지와 무관하게
        // 항상 매끈하게 반복 재생된다.
        private const string PlayerIdlePath = "Art/Necrosia/Idle";
        private const float IdleFrameInterval = 1f / 4f;
        private Sprite[] playerIdleFrames = System.Array.Empty<Sprite>();
        private int idleFrameIndex;
        private float idleFrameTimer;

        // 공격 노드를 맞혔을 때 잠깐 재생하는 Z 공격 모션(월드의 AttackSpriteAnimator와 같은 소스/길이).
        // 재생 중에는 대기 애니메이션 쪽 Update 루프를 잠깐 쉬게 한다.
        private const string PlayerAttackPath = "Art/Necrosia/Attack";
        private const float PlayerAttackAnimDuration = 0.5f;
        private Sprite[] playerAttackFrames = System.Array.Empty<Sprite>();
        private bool isPlayingAttackAnim;
        private Coroutine attackAnimRoutine;

        private Monster monster;
        private Character player;
        private CharacterMovement2D playerMovement;
        private Coroutine turnRoutine;

        private bool isBossBattle;
        private float telegraphDuration;
        private float blockWindow;
        private float perfectWindow;
        private float goodWindow;

        private bool promptResolved;
        private float promptResolveDiff;

        // 도망친 몬스터는 잠깐 재접촉해도 다시 전투가 걸리지 않게 한다(안 그러면 쿨다운이 끝나자마자
        // 같은 자리에서 또 접촉해 전투가 반복된다). 동시에 한 마리만 기억하면 충분하다(전투는 한 번에 하나).
        private Monster lastMonster;
        private float cooldownUntil;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Initialize()
        {
            playerIdleFrames = Resources.LoadAll<Sprite>(PlayerIdlePath);
            System.Array.Sort(playerIdleFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            playerAttackFrames = Resources.LoadAll<Sprite>(PlayerAttackPath);
            System.Array.Sort(playerAttackFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            battleSplashFrames = Resources.LoadAll<Sprite>("Art/Effects/BattleSplash");
            System.Array.Sort(battleSplashFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            nodeBurstFrames = Resources.LoadAll<Sprite>("Art/Effects/NodeBurst");
            System.Array.Sort(nodeBurstFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            BuildPanel();
        }

        public bool TryBegin(Monster targetMonster, Character targetPlayer)
        {
            if (IsOpen) return false;
            if (targetMonster == null || targetMonster.IsDead) return false;
            if (targetPlayer == null || targetPlayer.IsDead) return false;
            if (targetMonster == lastMonster && Time.unscaledTime < cooldownUntil) return false;

            Begin(targetMonster, targetPlayer);
            return true;
        }

        private void Begin(Monster targetMonster, Character targetPlayer)
        {
            monster = targetMonster;
            player = targetPlayer;
            playerMovement = player.GetComponent<CharacterMovement2D>();

            isBossBattle = monster.GetComponent<BossAI>() != null || monster.GetComponent<BossSlimeAI>() != null;
            telegraphDuration = isBossBattle ? BossTelegraphDuration : NormalTelegraphDuration;
            blockWindow = isBossBattle ? BossBlockWindow : NormalBlockWindow;
            perfectWindow = isBossBattle ? BossPerfectWindow : NormalPerfectWindow;
            goodWindow = isBossBattle ? BossGoodWindow : NormalGoodWindow;

            monsterNameLabel.text = monster.Data != null ? monster.Data.monsterName : "몬스터";
            var monsterSprite = monster.GetComponent<SpriteRenderer>();
            monsterPortrait.sprite = monsterSprite != null ? monsterSprite.sprite : null;
            monsterPortrait.enabled = monsterPortrait.sprite != null;

            // 플레이어는 왼쪽, 몬스터는 오른쪽에서 서로 마주 본다. 대기 애니메이션은 매 프레임
            // Update에서 돌려주고, 여기서는 새 전투가 시작할 때마다 첫 프레임으로 되돌리기만 한다.
            idleFrameIndex = 0;
            idleFrameTimer = 0f;
            if (playerIdleFrames.Length > 0)
            {
                playerPortrait.sprite = playerIdleFrames[0];
                playerPortrait.enabled = true;
            }

            phaseBannerText.text = "";
            resultText.text = "";
            for (int i = 0; i < 2; i++)
            {
                noteImages[i].gameObject.SetActive(false);
                exclaim[i].gameObject.SetActive(false);
            }

            actionSelectPanel.SetActive(false);
            actionSelectFlee = false;
            if (attackAnimRoutine != null)
            {
                StopCoroutine(attackAnimRoutine);
                attackAnimRoutine = null;
            }
            isPlayingAttackAnim = false;

            monster.OnDied += HandleMonsterDied;
            player.OnDied += HandlePlayerDied;

            RefreshHpBars();

            // 접촉한 그 순간 바로 월드를 멈추고(몬스터가 더 때리지 못하게), 암전 → BATTLE 연출이
            // 끝난 뒤에야 프레임/체력바/레인 등 실제 전투 UI를 드러낸다.
            contentRoot.SetActive(false);
            battleSplashImage.gameObject.SetActive(false);
            dimBackdrop.color = new Color(dimBackdrop.color.r, dimBackdrop.color.g, dimBackdrop.color.b, 0f);

            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;

            turnRoutine = StartCoroutine(IntroThenEnemyTurn());
        }

        // 암전(알파 0→1) → "BATTLE!!" 슬램 애니메이션 재생 → 전투 UI 공개 → 적 턴 시작, 순서로 진행한다.
        private IEnumerator IntroThenEnemyTurn()
        {
            Color baseColor = dimBackdrop.color;
            float t = 0f;
            while (t < BlackoutFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                dimBackdrop.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(t / BlackoutFadeDuration));
                yield return null;
            }
            dimBackdrop.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);

            if (battleSplashFrames.Length > 0)
            {
                battleSplashImage.gameObject.SetActive(true);
                foreach (var frame in battleSplashFrames)
                {
                    battleSplashImage.sprite = frame;
                    yield return new WaitForSecondsRealtime(BattleSplashFrameInterval);
                }
                battleSplashImage.gameObject.SetActive(false);
            }

            yield return new WaitForSecondsRealtime(PostSplashPause);

            if (BattleEnded()) yield break;
            contentRoot.SetActive(true);
            turnRoutine = StartCoroutine(EnemyTurn());
        }

        private void HandleMonsterDied(Monster _) => EndBattle(fled: false);
        private void HandlePlayerDied(Character _) => EndBattle(fled: false);

        private void EndBattle(bool fled)
        {
            if (!IsOpen) return;

            if (turnRoutine != null)
            {
                StopCoroutine(turnRoutine);
                turnRoutine = null;
            }

            if (fled && monster != null && player != null)
            {
                lastMonster = monster;
                cooldownUntil = Time.unscaledTime + FleeCooldown;

                float dir = Mathf.Sign(player.transform.position.x - monster.transform.position.x);
                if (Mathf.Approximately(dir, 0f)) dir = 1f;
                var body = player.GetComponent<Rigidbody2D>();
                if (body != null) body.position += new Vector2(dir * FleeKnockbackDistance, 0f);
            }

            if (monster != null) monster.OnDied -= HandleMonsterDied;
            if (player != null) player.OnDied -= HandlePlayerDied;

            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);

            monster = null;
            player = null;
            playerMovement = null;
        }

        // Esc 처리와 플레이어 대기 애니메이션만 여기서 돈다. 나머지 입력(↑/↓ 판정)은
        // RunLanePrompt 코루틴이 직접 읽는다.
        private void Update()
        {
            if (!IsOpen) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                EndBattle(fled: true);
                return;
            }

            if (!isPlayingAttackAnim && playerIdleFrames.Length > 1)
            {
                idleFrameTimer += Time.unscaledDeltaTime;
                if (idleFrameTimer >= IdleFrameInterval)
                {
                    idleFrameTimer -= IdleFrameInterval;
                    idleFrameIndex = (idleFrameIndex + 1) % playerIdleFrames.Length;
                    playerPortrait.sprite = playerIdleFrames[idleFrameIndex];
                }
            }
        }

        private bool BattleEnded() => !IsOpen || monster == null || monster.IsDead || player == null || player.IsDead;

        private IEnumerator EnemyTurn()
        {
            phaseBannerText.color = MissColor;
            phaseBannerText.text = "적의 턴!";
            yield return new WaitForSecondsRealtime(PhaseBannerDuration);
            phaseBannerText.text = "";

            for (int i = 0; i < AttacksPerTurn; i++)
            {
                if (BattleEnded()) yield break;

                int[] pattern = isBossBattle
                    ? GenerateBossPattern()
                    : NormalAttackPatterns[Random.Range(0, NormalAttackPatterns.Length)];
                for (int noteIndex = 0; noteIndex < pattern.Length; noteIndex++)
                {
                    if (BattleEnded()) yield break;

                    // 패턴의 첫 노드만 평소 속도로 예고하고, 콤보로 이어지는 다음 노드부터는
                    // 훨씬 빠르고 텀 없이 몰아쳐서 "따따딱" 연타하는 느낌을 준다.
                    bool isComboFollowup = noteIndex > 0;
                    float thisTelegraph = isComboFollowup ? ComboFollowupTelegraph : telegraphDuration;

                    yield return RunLanePrompt(pattern[noteIndex], EnemyNoteColor, thisTelegraph, blockWindow);
                    if (BattleEnded()) yield break;

                    if (promptResolved)
                    {
                        ShowResult("막기 성공!", GoodColor);
                    }
                    else
                    {
                        float damage = CombatMath.PhysicalDamage(monster.Attack, player.Stats.defense);
                        player.TakeDamage(damage, monster.transform.position);
                        ShowResult("피격!", MissColor);
                        if (BattleEnded()) yield break;
                    }
                    RefreshHpBars();

                    bool isLastInPattern = noteIndex == pattern.Length - 1;
                    yield return new WaitForSecondsRealtime(isLastInPattern ? BetweenHitsPause : ComboFollowupPause);
                }
            }

            if (BattleEnded()) yield break;
            turnRoutine = StartCoroutine(PlayerTurn());
        }

        // 길이(1~3)와 레인을 전부 독립적으로 무작위로 뽑는다. 그래서 위-아래로 번갈아 나오는 패턴뿐 아니라
        // 위-위-위처럼 같은 레인이 그대로 연달아 나오는 패턴도 똑같은 확률로 나온다.
        private static int[] GenerateBossPattern()
        {
            int length = Random.Range(BossComboMinLength, BossComboMaxLength + 1);
            var pattern = new int[length];
            for (int i = 0; i < length; i++)
            {
                pattern[i] = Random.Range(0, 2);
            }
            return pattern;
        }

        private IEnumerator PlayerTurn()
        {
            phaseBannerText.color = PlayerNoteColor;
            phaseBannerText.text = "나의 턴!";
            yield return new WaitForSecondsRealtime(PhaseBannerDuration);
            phaseBannerText.text = "";

            if (BattleEnded()) yield break;
            yield return ShowActionSelect();
            if (BattleEnded()) yield break;

            if (actionSelectFlee)
            {
                EndBattle(fled: true);
                yield break;
            }

            for (int i = 0; i < AttacksPerTurn; i++)
            {
                if (BattleEnded()) yield break;

                int lane = Random.Range(0, 2);
                yield return RunLanePrompt(lane, PlayerNoteColor, telegraphDuration, goodWindow);
                if (BattleEnded()) yield break;

                if (promptResolved)
                {
                    bool perfect = promptResolveDiff <= perfectWindow;
                    TriggerPlayerAttackAnim();
                    // 모션이 타격 지점까지 진행된 뒤에 데미지를 적용한다 — 안 그러면 일격사일 때 공격
                    // 모션이 뜨기도 전에 전투 화면이 바로 닫혀서 몬스터가 이유 없이 사라지는 것처럼 보였다.
                    yield return new WaitForSecondsRealtime(AttackImpactDelay);
                    if (BattleEnded()) yield break;
                    player.DealDamage(monster, perfect ? 1f : 0.5f, isSkill: false);
                    ShowResult(perfect ? "Perfect!" : "Good", perfect ? PerfectColor : GoodColor);
                    if (BattleEnded()) yield break;
                }
                else
                {
                    ShowResult("Miss", MissColor);
                }
                RefreshHpBars();
                yield return new WaitForSecondsRealtime(BetweenHitsPause);
            }

            if (BattleEnded()) yield break;
            turnRoutine = StartCoroutine(EnemyTurn());
        }

        // 내 턴 시작 시 한 번, 공격/스킬/아이템/도주 중 고르게 한다. ←/→로 선택을 옮기고 Z/Enter로
        // 확정한다(Esc는 Update에서 전역으로 처리되는 도망과 같이 동작하므로 여기서 따로 다루지 않는다).
        // 스킬/아이템은 아직 구현이 없어 고르면 안내만 뜨고 선택 창이 그대로 열려 있는다.
        private IEnumerator ShowActionSelect()
        {
            actionSelectIndex = ActionAttack;
            actionMessageText.text = "";
            RefreshActionCursor();
            actionSelectPanel.SetActive(true);

            bool confirmed = false;
            while (!confirmed)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.leftArrowKey.wasPressedThisFrame)
                    {
                        actionSelectIndex = (actionSelectIndex + 3) % 4;
                        actionMessageText.text = "";
                        RefreshActionCursor();
                    }
                    else if (keyboard.rightArrowKey.wasPressedThisFrame)
                    {
                        actionSelectIndex = (actionSelectIndex + 1) % 4;
                        actionMessageText.text = "";
                        RefreshActionCursor();
                    }
                    else if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    {
                        if (actionSelectIndex == ActionSkill || actionSelectIndex == ActionItem)
                        {
                            actionMessageText.text = "아직 준비 중이에요";
                        }
                        else
                        {
                            confirmed = true;
                        }
                    }
                }
                yield return null;
            }

            actionSelectPanel.SetActive(false);
            actionSelectFlee = actionSelectIndex == ActionFlee;
        }

        private void RefreshActionCursor()
        {
            actionCursor.anchorMin = actionCursor.anchorMax = new Vector2(ActionSlotX[actionSelectIndex], 0.30f);
        }

        // 공격 노드를 맞혔을 때 왼쪽 초상화에 Z 공격 모션을 한 번 재생한다. 턴 진행을 막지 않도록
        // StartCoroutine으로 띄워두기만 하고 끝날 때까지 기다리지 않는다.
        private void TriggerPlayerAttackAnim()
        {
            if (playerAttackFrames.Length == 0) return;
            if (attackAnimRoutine != null) StopCoroutine(attackAnimRoutine);
            attackAnimRoutine = StartCoroutine(PlayPlayerAttackAnimRoutine());
        }

        private IEnumerator PlayPlayerAttackAnimRoutine()
        {
            isPlayingAttackAnim = true;
            float frameDuration = PlayerAttackAnimDuration / playerAttackFrames.Length;
            foreach (var frame in playerAttackFrames)
            {
                playerPortrait.sprite = frame;
                yield return new WaitForSecondsRealtime(frameDuration);
            }
            isPlayingAttackAnim = false;
            idleFrameTimer = 0f; // 애니메이션이 끝나면 바로 다음 프레임부터 대기 애니메이션이 매끈하게 이어진다.
            attackAnimRoutine = null;
        }

        // 레인 하나를 예고(느낌표 + 오른쪽에서 왼쪽으로 날아오는 노드)하고, telegraph 시점
        // (노드가 판정점에 도착하는 순간)을 기준으로 ±acceptWindow 안에 맞는 키가 눌리면
        // 성공(promptResolved=true)으로 끝난다. 시간이 다 지나도록 못 누르면 실패로 남는다.
        // 위쪽 노드는 Z, 아래쪽 노드는 X로 판정한다.
        private IEnumerator RunLanePrompt(int lane, Color noteColor, float telegraph, float acceptWindow)
        {
            promptResolved = false;
            promptResolveDiff = float.MaxValue;
            resultText.text = "";

            var note = noteImages[lane];
            note.color = noteColor;
            note.gameObject.SetActive(true);
            exclaim[lane].gameObject.SetActive(true);

            float laneY = NodeCenter[lane].y;
            float nodeX = NodeCenter[lane].x;

            float totalDuration = telegraph + acceptWindow;
            float t = 0f;
            while (t < totalDuration)
            {
                t += Time.unscaledDeltaTime;
                float travelT = Mathf.Clamp01(t / telegraph);
                float x = Mathf.LerpUnclamped(NoteSpawnX, nodeX, travelT);
                note.rectTransform.anchoredPosition = ImageToCanvasPos(x, laneY);

                var keyboard = Keyboard.current;
                bool pressed = keyboard != null && (lane == LaneUp
                    ? keyboard.zKey.wasPressedThisFrame
                    : keyboard.xKey.wasPressedThisFrame);
                if (pressed)
                {
                    float diff = Mathf.Abs(t - telegraph);
                    if (diff <= acceptWindow)
                    {
                        promptResolved = true;
                        promptResolveDiff = diff;
                        PlayNodeBurst(lane);
                        break; // 맞히면 남은 대기시간을 기다리지 않고 바로 다음 노드로 — 콤보가 진짜 연타로 느껴지게.
                    }
                }
                yield return null;
            }

            note.gameObject.SetActive(false);
            exclaim[lane].gameObject.SetActive(false);
        }

        private void ShowResult(string text, Color color)
        {
            resultText.text = text;
            resultText.color = color;
        }

        // 노드를 제때 맞혔을 때(막기 성공/공격 적중 공통) 그 노드 자리에서 한 번 터지는 연출.
        // 턴 진행을 막지 않도록 fire-and-forget으로 띄운다.
        private void PlayNodeBurst(int lane)
        {
            if (nodeBurstFrames.Length == 0) return;
            StartCoroutine(PlayNodeBurstRoutine(lane));
        }

        private IEnumerator PlayNodeBurstRoutine(int lane)
        {
            nodeBurstImage.rectTransform.anchoredPosition = ImageToCanvasPos(NodeCenter[lane].x, NodeCenter[lane].y);
            nodeBurstImage.gameObject.SetActive(true);
            foreach (var frame in nodeBurstFrames)
            {
                nodeBurstImage.sprite = frame;
                yield return new WaitForSecondsRealtime(NodeBurstFrameInterval);
            }
            nodeBurstImage.gameObject.SetActive(false);
        }

        private void RefreshHpBars()
        {
            if (monster != null)
            {
                float ratio = monster.MaxHp > 0f ? Mathf.Clamp01(monster.CurrentHp / monster.MaxHp) : 0f;
                monsterHpFill.fillAmount = ratio;
                monsterHpText.text = $"{Mathf.CeilToInt(monster.CurrentHp)} / {Mathf.CeilToInt(monster.MaxHp)}";
            }
            if (player != null)
            {
                float ratio = player.MaxHp > 0f ? Mathf.Clamp01(player.CurrentHp / player.MaxHp) : 0f;
                playerHpFill.fillAmount = ratio;
                playerHpText.text = $"{Mathf.CeilToInt(player.CurrentHp)} / {Mathf.CeilToInt(player.MaxHp)}";
            }
        }

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

            contentRoot = new GameObject("Content", typeof(RectTransform));
            var contentRect = (RectTransform)contentRoot.transform;
            contentRect.SetParent(rect, false);
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var frame = new GameObject("Frame", typeof(RectTransform), typeof(RawImage));
            var frameRect = (RectTransform)frame.transform;
            frameRect.SetParent(contentRect, false);
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            frame.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/RhythmBattleFrame");

            var monsterBar = BuildHpBar(contentRect, "MonsterBar", 950f, 60f, 1520f, 150f,
                new Color(0.8f, 0.1f, 0.15f), new Color(1f, 0.85f, 0.4f), "");
            monsterHpFill = monsterBar.Fill;
            monsterNameLabel = monsterBar.NameLabel;
            monsterHpText = monsterBar.ValueLabel;

            var playerBar = BuildHpBar(contentRect, "PlayerBar", 150f, 60f, 650f, 150f,
                new Color(0.25f, 0.55f, 0.95f), new Color(0.85f, 0.9f, 1f), "플레이어");
            playerHpFill = playerBar.Fill;
            playerHpText = playerBar.ValueLabel;

            playerPortrait = CreateImage(contentRect, "PlayerPortrait", 270f, 250f, 530f, 510f);
            playerPortrait.preserveAspect = true;

            monsterPortrait = CreateImage(contentRect, "MonsterPortrait", 1080f, 230f, 1380f, 530f);
            monsterPortrait.preserveAspect = true;
            // 몬스터 원화는 기본이 오른쪽을 보는 방향이라(월드의 flipX 관례와 동일), 왼쪽의
            // 플레이어를 바라보도록 좌우로 뒤집는다.
            monsterPortrait.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            phaseBannerText = CreateText(contentRect, "PhaseBanner", 600f, 160f, 1070f, 230f, 42f, Color.white, TextAnchor.MiddleCenter);
            phaseBannerText.fontStyle = FontStyle.Bold;

            resultText = CreateText(contentRect, "ResultText", 550f, 380f, 1120f, 460f, 40f, Color.white, TextAnchor.MiddleCenter);
            resultText.fontStyle = FontStyle.Bold;

            var nodeSprite = Resources.Load<Sprite>("UI/NodeDiamond");
            for (int i = 0; i < 2; i++)
            {
                var note = CreateMovableImage(contentRect, "Note" + i, NoteSize);
                note.sprite = nodeSprite;
                note.gameObject.SetActive(false);
                noteImages[i] = note;

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

            BuildActionSelectPanel(contentRect);

            // "BATTLE!!" 슬램 연출. 암전 직후, 전투 UI(contentRoot)가 드러나기 전에 잠깐 재생된다.
            // 프레임 한 장이 세로로 긴 비율(307:512)이라 그에 맞춰 박스도 세로로 길게 잡는다.
            battleSplashImage = CreateImage(rect, "BattleSplash", 656f, 150f, 1016f, 750f);
            battleSplashImage.preserveAspect = true;
            battleSplashImage.gameObject.SetActive(false);

            panel.SetActive(false);
        }

        // 내 턴 시작 시 뜨는 행동 선택 창. UI/BattleActionSelect 그림 안에 공격/스킬/아이템/도주
        // 4칸 아이콘+글자가 이미 그려져 있어서, 고른 칸 아래에 Select_Effect 빛줄기만 옮겨가며 띄운다.
        private void BuildActionSelectPanel(Transform parent)
        {
            actionSelectPanel = new GameObject("ActionSelect", typeof(RectTransform));
            var panelRect = (RectTransform)actionSelectPanel.transform;
            panelRect.SetParent(parent, false);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(860f, 645f); // UI/BattleActionSelect(1448x1086, 약 4:3)와 같은 비율.
            panelRect.anchoredPosition = Vector2.zero;

            var bgGO = new GameObject("BG", typeof(RectTransform), typeof(RawImage));
            var bgRect = (RectTransform)bgGO.transform;
            bgRect.SetParent(panelRect, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGO.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/BattleActionSelect");

            var cursorGO = new GameObject("Cursor", typeof(RectTransform), typeof(RawImage));
            actionCursor = (RectTransform)cursorGO.transform;
            actionCursor.SetParent(panelRect, false);
            actionCursor.pivot = new Vector2(0.5f, 0.5f);
            actionCursor.sizeDelta = new Vector2(150f, 20f);
            actionCursor.anchorMin = actionCursor.anchorMax = new Vector2(ActionSlotX[0], 0.30f);
            actionCursorImage = cursorGO.GetComponent<RawImage>();
            actionCursorImage.texture = Resources.Load<Texture2D>("UI/Select_Effect");
            actionCursorImage.raycastTarget = false;

            var msgGO = new GameObject("Message", typeof(RectTransform), typeof(Text));
            var msgRect = (RectTransform)msgGO.transform;
            msgRect.SetParent(panelRect, false);
            msgRect.anchorMin = new Vector2(0.1f, 0.12f);
            msgRect.anchorMax = new Vector2(0.9f, 0.24f);
            msgRect.offsetMin = Vector2.zero;
            msgRect.offsetMax = Vector2.zero;
            actionMessageText = msgGO.GetComponent<Text>();
            actionMessageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            actionMessageText.fontSize = 22;
            actionMessageText.fontStyle = FontStyle.Bold;
            actionMessageText.alignment = TextAnchor.MiddleCenter;
            actionMessageText.color = MissColor;
            actionMessageText.raycastTarget = false;

            actionSelectPanel.SetActive(false);
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

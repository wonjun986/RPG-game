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
    // 보스 필살기: 알리아스 심연의 검기, 루나벨 진홍의 심장/혈류 흡수/피의 회오리, 모래룡 브레스/꼬리 폭발, 도주 차단, 검기.
    public partial class RhythmBattleUI
    {
        // ---- 보스 필살기 ----
        // 검을 들어올림(한 번) → 기운 모으기(반복 유지)하는 동안 "기운 흩트리기" 채보가 흐른다. 플레이어가 노드를 쳐서
        // 기운을 흩트리는데, 노드의 BossSpecialCancelRatio 이상을 맞히면 보스가 주춤하며 필살기가 캔슬되고,
        // 그보다 많이 놓치면 모은 기운을 기본 공격 모션과 함께 검기로 날려 큰 피해를 준다.
        private const string BossSpecialName = "심연의 검기";
        // 두 번째 필살기(체력 20% 이하)는 페이크 노드가 섞인 더 어려운 채보로 나온다.
        private static readonly string[] BossSpecialChargeCharts =
        {
            "UDUD-u====-DDUU-UDU-d====--UUDD-DUDU---",
            "UDUD-A-u====-BDUU-UDAU-d====-A-UUDD-DBDU-UDUD---",
        };
        private const float BossSpecialCancelRatio = 0.7f;
        // 검기 피해 = 평소 노드 한 타(몬스터 공격력 기준) x 이 배수.
        private const float BossSpecialWaveDamageMultiplier = 5f;
        private const float BossSpecialRaiseInterval = 0.14f;
        private const float BossSpecialChargeInterval = 0.1f;
        // 채보가 시작되기 전, 기운을 모으는 모습을 잠깐 먼저 보여 준다.
        private const float BossSpecialChargeLeadIn = 0.6f;
        private const float BossSpecialStaggerDuration = 1.0f;
        private const float BossSpecialStaggerShake = 14f; // 캔버스 단위
        private const float BossSpecialWaveTravelTime = 0.55f;
        private const float BossSpecialAfterWavePause = 0.6f;
        // 공격 모션이 휘두르는 프레임(3번째)쯤에 검기가 출발한다.
        private const float BossSpecialWaveLaunchDelay = MonsterAttackFrameInterval * 2f;
        private const float SwordWaveBurstInterval = 0.08f;
        private const float SwordWaveFrameInterval = 0.06f;
        // 검기 크기/경로(배경 그림 픽셀 좌표). 몬스터 칸(1080~1380) 앞에서 출발해 플레이어 초상화(270~530)까지 바닥을 타고 간다.
        private const float SwordWaveWidth = 300f;
        private const float SwordWaveHeight = 250f;
        private const float SwordWaveStartX = 1150f;
        private const float SwordWaveEndX = 420f;
        private const float SwordWaveBottomY = 525f;

        private Sprite[] bossSpecialRaiseFrames = System.Array.Empty<Sprite>();
        private Sprite[] bossSpecialChargeFrames = System.Array.Empty<Sprite>();
        private Sprite[] swordWaveFrames = System.Array.Empty<Sprite>();
        private Image swordWaveImage;
        private Coroutine swordWaveRoutine;
        private Coroutine monsterStaggerRoutine;

        private IEnumerator BossSpecialMove(int specialIndex)
        {
            phaseBannerText.color = EnemyNoteColor;
            phaseBannerText.text = "알리아스가 검을 들어올린다...";
            PlayMonsterOneShot(bossSpecialRaiseFrames, BossSpecialRaiseInterval, restart: true);
            yield return new WaitForSecondsRealtime(BossSpecialRaiseInterval * bossSpecialRaiseFrames.Length);
            if (BattleEnded()) yield break;

            phaseBannerText.text = "어둠의 기운이 모여든다! 노드를 맞혀 흩트려라!";
            monsterHoldFrames = bossSpecialChargeFrames;
            monsterHoldInterval = BossSpecialChargeInterval;
            monsterOneShot = null;
            monsterFrameIndex = 0;
            monsterFrameTimer = 0f;
            SetMonsterFrame(monsterHoldFrames[0]);
            yield return new WaitForSecondsRealtime(BossSpecialChargeLeadIn);
            if (BattleEnded()) yield break;

            int hits = 0;
            var notes = BuildChartNotes(BossSpecialChargeCharts[Mathf.Clamp(specialIndex, 0, BossSpecialChargeCharts.Length - 1)]);
            yield return RunChart(notes, SkillNoteColor, okWindow, (note, hit, diff) =>
            {
                if (hit)
                {
                    hits++;
                    TriggerPlayerAttackAnim();
                    ShowResult("기운을 흩트렸다!", GoodColor);
                }
                else
                {
                    ShowResult("놓쳤다...", MissColor);
                }
            });
            if (BattleEnded()) yield break;

            float hitRatio = notes.Count > 0 ? (float)hits / notes.Count : 1f;
            if (hitRatio >= BossSpecialCancelRatio)
            {
                // 캔슬: 기운이 흩어지고 보스가 휘청인다.
                monsterHoldFrames = null;
                monsterOneShot = null;
                monsterFrameIndex = 0;
                monsterFrameTimer = 0f;
                SetMonsterFrame(monsterIdleFrames[0]);
                phaseBannerText.text = $"알리아스가 주춤했다! 필살기 캔슬! ({hits}/{notes.Count})";
                ShowResult("캔슬 성공!", GoodColor);
                PlayMonsterStagger();
                yield return new WaitForSecondsRealtime(BossSpecialStaggerDuration);
                phaseBannerText.text = "";
                yield break;
            }

            // 실패: 모은 기운을 검기로 날린다.
            phaseBannerText.text = $"{BossSpecialName}! ({hits}/{notes.Count})";
            monsterHoldFrames = null;
            yield return FireSwordWaveAtPlayer(BossSpecialWaveDamageMultiplier, $"{BossSpecialName}에 베였다!");
        }

        // 기본 공격 모션과 함께 검기를 날려, 닿는 순간 막을 기회 없이 (노드 한 타 x damageMultiplier) 피해를 준다.
        // 필살기 실패와 보스전 도주 차단이 같이 쓴다. 끝나면 배너를 지운다.
        private IEnumerator FireSwordWaveAtPlayer(float damageMultiplier, string hitMessage)
        {
            PlayMonsterOneShot(monsterAttackFrames, MonsterAttackFrameInterval, restart: true);
            yield return new WaitForSecondsRealtime(BossSpecialWaveLaunchDelay);
            if (BattleEnded()) yield break;

            LaunchSwordWave(BossSpecialWaveTravelTime);
            yield return new WaitForSecondsRealtime(BossSpecialWaveTravelTime);
            if (BattleEnded()) yield break;

            float damage = CombatMath.PhysicalDamage(monster.Attack, player.Stats.defense) * damageMultiplier;
            player.TakeDamage(damage, monster.transform.position, hitReaction: false);
            TriggerPlayerHitAnim();
            ShowResult($"{hitMessage} -{damage:0}", MissColor);
            RefreshHpBars();
            yield return new WaitForSecondsRealtime(BossSpecialAfterWavePause);
            phaseBannerText.text = "";
        }

        // ---- 루나벨 2페이즈 필살기 ----
        // 하늘로 손을 뻗어 마법진을 여는 모션(Special 7프레임)을 한 번 재생 → 머리 위에 핏빛 구체를 모으는 모션(Charge
        // 8프레임)을 반복하는 동안 "기운 흩트리기" 채보가 흐른다. 캔슬은 없고, 놓친 노드 비율만큼 위력이 실린다:
        // 하나라도 놓치면 구체를 던지는 모션(Throw 5프레임)을 끝까지 재생한 뒤 구체를 던진다. 구체는 플레이어 발밑에
        // 떨어져 스며들고, 그 자리에서 피의 기둥이 솟구친 뒤(BloodPillarRise 한 번) 회오리치며(BloodPillarTornado 반복)
        // 여러 번 나눠 피해를 준다. 총 피해 = 최대 위력(LunabelSpecialPillarDamageMultiplier) x 놓친 노드 / 전체 노드.
        // 전부 맞히면 구체가 완전히 흩어져 피해 없이 끝난다.
        private const string LunabelSpecialName = "진홍의 심장";
        // 두 번째 필살기(2페이즈 체력 25% 이하)는 페이크 노드가 섞인 더 어려운 채보로 나온다.
        private static readonly string[] LunabelSpecialChargeCharts =
        {
            "DUDU-u====-UUDD-DUD-d====--DDUU-UDUD---",
            "DUDU-B-d====-AUDD-DUBD-u====-B-DDUU-UADU-DUDU---",
        };
        private const float LunabelSpecialRaiseInterval = 0.14f;
        private const float LunabelSpecialChargeInterval = 0.1f;
        private const float LunabelSpecialChargeLeadIn = 0.6f;
        private const float LunabelThrowFrameInterval = 0.1f;

        // 구체 경로(배경 그림 픽셀 좌표). 던지기 마지막 프레임에서 루나벨이 내뻗은 손끝에서 출발해, 낮은 포물선을 그리며
        // 플레이어 초상화(270~530) 발밑에 떨어진다. 떨어지면 작아지며 땅으로 스며든다.
        private const float BloodOrbStartX = 1100f;
        private const float BloodOrbStartY = 375f;
        private const float BloodOrbEndX = 400f;
        private const float BloodOrbEndY = 500f;
        private const float BloodOrbArcHeight = 70f;
        private const float BloodOrbStartSize = 80f;
        private const float BloodOrbEndSize = 150f;
        private const float BloodOrbTravelTime = 0.55f;
        private const float BloodOrbSinkTime = 0.2f;
        private const float BloodOrbSpinSpeed = 300f; // 도/초

        // 피의 기둥: 플레이어 발밑(배경 그림 픽셀 좌표)을 바닥 가운데로 솟구친다. 원화 1px을 그림 0.65px로 그려서
        // 다 솟구친 기둥(원화 키 약 575px)이 플레이어 초상화(키 260)를 넉넉히 집어삼키게 한다.
        private const float BloodPillarX = 400f;
        private const float BloodPillarBottomY = 515f;
        private const float BloodPillarScale = 0.65f;
        private const float BloodPillarRiseInterval = 0.07f;
        private const float BloodPillarTornadoInterval = 0.07f;
        private const float BloodPillarTornadoDuration = 1.6f;
        private const float BloodPillarFadeTime = 0.25f;
        // 회오리 피해: 노드 한 타(몬스터 공격력 기준) x LunabelSpecialPillarDamageMultiplier를 LunabelSpecialPillarHits번에 나눠 준다.
        private const int LunabelSpecialPillarHits = 8;
        private const float LunabelSpecialPillarDamageMultiplier = 6f;
        private const float LunabelSpecialAfterPause = 0.5f;

        private Sprite[] lunabelSpecialFrames = System.Array.Empty<Sprite>();
        private Sprite[] lunabelSpecialChargeFrames = System.Array.Empty<Sprite>();
        private Sprite[] lunabelThrowFrames = System.Array.Empty<Sprite>();
        private Sprite[] bloodPillarRiseFrames = System.Array.Empty<Sprite>();
        private Sprite[] bloodPillarTornadoFrames = System.Array.Empty<Sprite>();
        private Sprite bloodOrbSprite;
        private Image bloodOrbImage;
        private Image bloodPillarImage;
        private Coroutine bloodOrbRoutine;

        private IEnumerator LunabelSpecialMove(int specialIndex)
        {
            phaseBannerText.color = EnragedNoteColor;
            phaseBannerText.text = "루나벨이 하늘로 손을 뻗는다...";
            PlayMonsterOneShot(lunabelSpecialFrames, LunabelSpecialRaiseInterval, restart: true);
            yield return new WaitForSecondsRealtime(LunabelSpecialRaiseInterval * lunabelSpecialFrames.Length);
            if (BattleEnded()) yield break;

            phaseBannerText.text = "핏빛 구체가 부풀어 오른다! 노드를 맞혀 흩트려라!";
            monsterHoldFrames = lunabelSpecialChargeFrames;
            monsterHoldInterval = LunabelSpecialChargeInterval;
            monsterOneShot = null;
            monsterFrameIndex = 0;
            monsterFrameTimer = 0f;
            SetMonsterFrame(monsterHoldFrames[0]);
            yield return new WaitForSecondsRealtime(LunabelSpecialChargeLeadIn);
            if (BattleEnded()) yield break;

            int hits = 0;
            var notes = BuildChartNotes(LunabelSpecialChargeCharts[Mathf.Clamp(specialIndex, 0, LunabelSpecialChargeCharts.Length - 1)]);
            yield return RunChart(notes, SkillNoteColor, okWindow, (note, hit, diff) =>
            {
                if (hit)
                {
                    hits++;
                    TriggerPlayerAttackAnim();
                    ShowResult("기운을 흩트렸다!", GoodColor);
                }
                else
                {
                    ShowResult("놓쳤다...", MissColor);
                }
            });
            if (BattleEnded()) yield break;

            int misses = notes.Count - hits;
            if (misses <= 0)
            {
                ReturnMonsterToIdle();
                phaseBannerText.text = $"구체가 완전히 흩어졌다! ({hits}/{notes.Count})";
                ShowResult("완벽 방어!", GoodColor);
                PlayMonsterStagger();
                yield return new WaitForSecondsRealtime(BossSpecialStaggerDuration);
                phaseBannerText.text = "";
                yield break;
            }

            // 놓친 만큼 남은 구체를 던진다: 던지기 모션을 끝까지 재생하고, 마지막 자세(손을 내뻗은 모습)로 멈춘 채 던진다.
            float missRatio = (float)misses / notes.Count;
            phaseBannerText.text = $"{LunabelSpecialName}! 놓친 기운 {misses}/{notes.Count} — 위력 {missRatio * 100f:0}%";
            if (lunabelThrowFrames.Length > 0)
            {
                monsterHoldFrames = new[] { lunabelThrowFrames[lunabelThrowFrames.Length - 1] };
                monsterHoldInterval = 1f;
                PlayMonsterOneShot(lunabelThrowFrames, LunabelThrowFrameInterval, restart: true);
                yield return new WaitForSecondsRealtime(LunabelThrowFrameInterval * lunabelThrowFrames.Length);
                if (BattleEnded()) yield break;
            }

            LaunchBloodOrb();
            yield return new WaitForSecondsRealtime(BloodOrbTravelTime + BloodOrbSinkTime);
            ReturnMonsterToIdle();
            if (BattleEnded()) yield break;

            yield return BloodPillar(LunabelSpecialPillarHits, LunabelSpecialPillarDamageMultiplier * missRatio, LunabelSpecialName,
                bloodPillarRiseFrames, bloodPillarTornadoFrames, BloodPillarTornadoInterval);
            if (BattleEnded()) yield break;
            yield return new WaitForSecondsRealtime(LunabelSpecialAfterPause);
            phaseBannerText.text = "";
        }

        // ---- 황금 모래룡: 황금 브레스 ----
        // 입에 금빛 기운을 모으는 모션(에너지 충전 8프레임)을 한 번 재생 → 마지막 두 프레임(커진 구슬)을 반복하며 차징하는
        // 동안 "기운 흩트리기" 채보가 흐른다(알리아스/루나벨 필살기와 같은 규칙: 맞힌 노드는 구슬을 흩트린다).
        // 하나라도 놓치면 놓친 비율만큼의 위력으로 입에서 플레이어에게 황금 빔(6프레임)을 쏜다. 빔이 닿아 있는 동안 여러 번 나눠 맞는다.
        // 전부 맞히면 구슬이 흩어져 피해 없이 끝난다.
        private const string SandChargePath = "Art/Monsters/SandDragon/Charge";
        private const string SandBeamPath = "Art/Effects/SandDragonBeam";
        private const string SandDragonBeamName = "황금 브레스";
        private const int SandDragonBeamEveryTurns = 3;
        private static readonly string[] SandDragonBeamCharts =
        {
            "UDU-DUD-u===-DDUU-UD-d===---",
            "DDU-UDD-UU-d====-UDUD-DU---",
            "U-UD-DU-u===-DUDU-UUDD-D---",
        };
        private const float SandDragonChargeInterval = 0.12f;
        private const float SandDragonChargeHoldInterval = 0.1f;
        private const float SandDragonChargeLeadIn = 0.5f;
        private const float SandDragonBeamFrameInterval = 0.06f;
        private const float SandDragonBeamDuration = 1.2f;
        private const int SandDragonBeamHits = 4;
        private const float SandDragonBeamDamageMultiplier = 6f;
        // 빔 칸 그림(512x512)의 출발점(오른쪽 위 별)과 충돌점(왼쪽 아래) 픽셀 — 칸 왼쪽 위 기준.
        private const float SandBeamCellPx = 512f;
        private static readonly Vector2 SandBeamOriginPx = new Vector2(475f, 24f);
        private static readonly Vector2 SandBeamEndPx = new Vector2(21f, 451f);
        // 충전 프레임(758x409 캔버스)에서 구슬이 피벗(아래 가운데)보다 이만큼(원화 px) 오른쪽/위에 있다.
        private static readonly Vector2 SandChargeOrbFromPivotPx = new Vector2(133f, 237f);
        // 빔이 닿는 곳: 플레이어 초상화 가슴께(배경 그림 px).
        private const float SandBeamTargetX = 430f;
        private const float SandBeamTargetY = 400f;
        private Sprite[] sandChargeFrames = System.Array.Empty<Sprite>();
        private Sprite[] sandBeamFrames = System.Array.Empty<Sprite>();
        private Image sandBeamImage;

        private IEnumerator SandDragonBeam()
        {
            phaseBannerText.color = EnemyNoteColor;
            phaseBannerText.text = "모래룡이 입을 벌린다... 금빛 기운이 모여든다!";
            PlayMonsterOneShot(sandChargeFrames, SandDragonChargeInterval, restart: true);
            yield return new WaitForSecondsRealtime(SandDragonChargeInterval * sandChargeFrames.Length);
            if (BattleEnded()) yield break;

            // 차징: 구슬이 가장 커진 마지막 두 프레임을 반복.
            phaseBannerText.text = "금빛 구슬이 부풀어 오른다! 노드를 맞혀 흩트려라!";
            int holdCount = Mathf.Min(2, sandChargeFrames.Length);
            var hold = new Sprite[holdCount];
            System.Array.Copy(sandChargeFrames, sandChargeFrames.Length - holdCount, hold, 0, holdCount);
            monsterHoldFrames = hold;
            monsterHoldInterval = SandDragonChargeHoldInterval;
            monsterOneShot = null;
            monsterFrameIndex = 0;
            monsterFrameTimer = 0f;
            SetMonsterFrame(monsterHoldFrames[0]);
            yield return new WaitForSecondsRealtime(SandDragonChargeLeadIn);
            if (BattleEnded()) yield break;

            int hits = 0;
            var notes = BuildChartNotes(SandDragonBeamCharts[Random.Range(0, SandDragonBeamCharts.Length)]);
            yield return RunChart(notes, SkillNoteColor, okWindow, (note, hit, diff) =>
            {
                if (hit)
                {
                    hits++;
                    TriggerPlayerAttackAnim();
                    ShowResult("기운을 흩트렸다!", GoodColor);
                }
                else
                {
                    ShowResult("놓쳤다...", MissColor);
                }
            });
            if (BattleEnded()) yield break;

            int misses = notes.Count - hits;
            if (misses <= 0)
            {
                ReturnMonsterToIdle();
                phaseBannerText.text = $"금빛 구슬이 흩어졌다! ({hits}/{notes.Count})";
                ShowResult("완벽 방어!", GoodColor);
                PlayMonsterStagger();
                yield return new WaitForSecondsRealtime(BossSpecialStaggerDuration);
                phaseBannerText.text = "";
                yield break;
            }

            float missRatio = (float)misses / notes.Count;
            phaseBannerText.text = $"{SandDragonBeamName}! 놓친 기운 {misses}/{notes.Count} — 위력 {missRatio * 100f:0}%";
            // 쏘는 동안은 구슬을 문 마지막 충전 프레임으로 멈춰 있는다.
            monsterHoldFrames = new[] { sandChargeFrames[sandChargeFrames.Length - 1] };
            monsterHoldInterval = 1f;
            SetMonsterFrame(monsterHoldFrames[0]);
            yield return FireSandBeam(missRatio);
            ReturnMonsterToIdle();
            if (BattleEnded()) yield break;
            phaseBannerText.text = "";
        }

        private IEnumerator FireSandBeam(float missRatio)
        {
            float hitDamage = CombatMath.PhysicalDamage(monster.Attack, player.Stats.defense)
                * SandDragonBeamDamageMultiplier * missRatio / SandDragonBeamHits;

            // 출발점 = 모래룡 초상화 피벗(아래 가운데) + 구슬 위치. 초상화는 좌우로 뒤집혀 있어(왼쪽을 본다) x는 빼 준다.
            var rect = sandBeamImage.rectTransform;
            var parent = (RectTransform)rect.parent;
            var portrait = monsterPortrait.rectTransform;
            float pxToUi = portrait.rect.height / Mathf.Max(1f, monsterPortrait.sprite.rect.height);
            Vector2 pivotLocal = parent.InverseTransformPoint(portrait.position);
            Vector2 origin = pivotLocal + new Vector2(-SandChargeOrbFromPivotPx.x * pxToUi, SandChargeOrbFromPivotPx.y * pxToUi);
            // ImageToCanvasPos는 부모 왼쪽 위 기준 좌표라, 부모 중심 기준 로컬 좌표로 옮긴다.
            Vector2 target = ImageToCanvasPos(SandBeamTargetX, SandBeamTargetY) + new Vector2(parent.rect.xMin, parent.rect.yMax);

            // 그림 속 빔 방향/길이에 맞춰 회전과 배율을 정한다.
            Vector2 artDir = new Vector2(SandBeamEndPx.x - SandBeamOriginPx.x, -(SandBeamEndPx.y - SandBeamOriginPx.y));
            Vector2 wantDir = target - origin;
            float scale = wantDir.magnitude / artDir.magnitude;
            float angle = Vector2.SignedAngle(artDir, wantDir);

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = origin;
            rect.sizeDelta = new Vector2(SandBeamCellPx, SandBeamCellPx) * scale;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            sandBeamImage.color = Color.white;
            sandBeamImage.gameObject.SetActive(true);
            rect.SetAsLastSibling();

            float hitInterval = SandDragonBeamDuration / SandDragonBeamHits;
            int dealt = 0;
            float nextHit = SandDragonBeamFrameInterval * 2f; // 빔이 굵어지는 3번째 프레임부터 맞는다
            float total = SandDragonBeamDuration + nextHit;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                int step = (int)(t / SandDragonBeamFrameInterval);
                int frame = step < sandBeamFrames.Length
                    ? step
                    : sandBeamFrames.Length - 3 + step % 3; // 다 굵어진 뒤엔 마지막 세 프레임을 번갈아 돌려 일렁이게
                sandBeamImage.sprite = sandBeamFrames[Mathf.Clamp(frame, 0, sandBeamFrames.Length - 1)];
                if (dealt < SandDragonBeamHits && t >= nextHit)
                {
                    dealt++;
                    nextHit += hitInterval;
                    DealBloodPillarHit(hitDamage, SandDragonBeamName);
                    TriggerPlayerHitAnim();
                    if (BattleEnded()) break;
                }
                yield return null;
            }
            for (float f = 0f; f < 0.25f; f += Time.unscaledDeltaTime)
            {
                sandBeamImage.color = new Color(1f, 1f, 1f, 1f - f / 0.25f);
                yield return null;
            }
            sandBeamImage.gameObject.SetActive(false);
            rect.localRotation = Quaternion.identity;
        }

        // ---- 황금 모래룡: 꼬리 대지 폭발 ----
        // 모래룡이 몸을 들어 땅을 내리치는 모션(대지 강타 6프레임)과 함께 "충격" 노드가 흐른다. 놓친 노드 비율만큼 땅속으로
        // 충격이 퍼져, 플레이어 발밑에서 금빛 꼬리가 솟구친다(꼬리 대지 폭발 4프레임 — 루나벨 피의 기둥과 같은 자리/방식으로
        // 솟은 뒤 여러 번 나눠 피해). 전부 막으면 꼬리가 빗나가 피해가 없다.
        private const string SandTailPath = "Art/Effects/SandTailBurst";
        private const string SandDragonTailName = "꼬리 대지 폭발";
        private const int SandDragonTailEveryTurns = 2;
        private static readonly string[] SandDragonTailCharts =
        {
            "DD-UU-D-U-DDUU---",
            "U-DD-UU-D-DUDU---",
            "DDD-U-U-DD-UUU---",
        };
        private const float SandDragonSlamFrameInterval = 0.12f;
        private const int SandDragonTailHits = 3;
        private const float SandDragonTailDamageMultiplier = 5f;
        private const float SandDragonTailRiseInterval = 0.09f;
        private Sprite[] sandTailFrames = System.Array.Empty<Sprite>();
        private bool isSandDragonBattle;
        private int sandDragonEnemyTurns;

        private IEnumerator SandDragonTailStrike()
        {
            phaseBannerText.color = EnemyNoteColor;
            phaseBannerText.text = "모래룡이 몸을 치켜든다... 땅이 울린다! 노드를 막아 충격을 흘려라!";
            PlayMonsterOneShot(monsterAttackFrames, SandDragonSlamFrameInterval, restart: true);
            yield return new WaitForSecondsRealtime(SandDragonSlamFrameInterval * Mathf.Max(1, monsterAttackFrames.Length) * 0.5f);
            if (BattleEnded()) yield break;

            int blocked = 0;
            var notes = BuildChartNotes(SandDragonTailCharts[Random.Range(0, SandDragonTailCharts.Length)]);
            yield return RunChart(notes, EnemyNoteColor, blockWindow, (note, hit, diff) =>
            {
                if (hit)
                {
                    blocked++;
                    TriggerPlayerGuardAnim();
                    ShowResult("충격을 흘렸다!", GoodColor);
                }
                else
                {
                    ShowResult("땅이 갈라진다...", MissColor);
                }
            });
            if (BattleEnded()) yield break;

            int misses = notes.Count - blocked;
            if (misses <= 0)
            {
                ReturnMonsterToIdle();
                phaseBannerText.text = $"충격을 전부 흘렸다! 꼬리가 빗나갔다! ({blocked}/{notes.Count})";
                ShowResult("완벽 방어!", GoodColor);
                yield return new WaitForSecondsRealtime(BossSpecialStaggerDuration);
                phaseBannerText.text = "";
                yield break;
            }

            float missRatio = (float)misses / notes.Count;
            phaseBannerText.text = $"{SandDragonTailName}! 놓친 충격 {misses}/{notes.Count} — 위력 {missRatio * 100f:0}%";
            PlayMonsterOneShot(monsterAttackFrames, SandDragonSlamFrameInterval, restart: true);
            // 앞 프레임들은 모래가 소용돌이치다 꼬리가 솟는 장면(한 번), 마지막 프레임(다 솟은 꼬리)에 머물며 나눠 때린다.
            var rise = new Sprite[Mathf.Max(0, sandTailFrames.Length - 1)];
            System.Array.Copy(sandTailFrames, rise, rise.Length);
            var hold = new[] { sandTailFrames[sandTailFrames.Length - 1] };
            yield return BloodPillar(SandDragonTailHits, SandDragonTailDamageMultiplier * missRatio, SandDragonTailName,
                rise, hold, SandDragonTailRiseInterval);
            if (BattleEnded()) yield break;
            phaseBannerText.text = "";
        }

        private void ReturnMonsterToIdle()
        {
            monsterHoldFrames = null;
            monsterOneShot = null;
            monsterFrameIndex = 0;
            monsterFrameTimer = 0f;
            SetMonsterFrame(monsterIdleFrames[0]);
        }

        // 피의 기둥이 솟구친 뒤 회오리치는 동안, 일정한 간격으로 나눠 피해를 준다. 끝나면 서서히 사라진다.
        // 피해 = 노드 한 타(몬스터 공격력 기준) x totalMultiplier를 hits번에 나눈 값. 필살기(피의 기둥: rise 한 번 → loop 반복)와
        // 1페이즈 피의 회오리(rise 없이 회오리 loop만, 처음에 살짝 페이드 인)가 같이 쓴다.
        private IEnumerator BloodPillar(int hits, float totalMultiplier, string attackName,
            Sprite[] riseFrames, Sprite[] loopFrames, float loopInterval)
        {
            float hitDamage = CombatMath.PhysicalDamage(monster.Attack, player.Stats.defense) * totalMultiplier / hits;
            if (riseFrames.Length == 0 && loopFrames.Length == 0)
            {
                // 이펙트가 없으면 피해만 한 번에 준다.
                DealBloodPillarHit(hitDamage * hits, attackName);
                yield break;
            }

            bloodPillarImage.color = riseFrames.Length == 0 ? new Color(1f, 1f, 1f, 0f) : Color.white;
            bloodPillarImage.gameObject.SetActive(true);
            foreach (var riseFrame in riseFrames)
            {
                SetBloodPillarFrame(riseFrame);
                yield return new WaitForSecondsRealtime(BloodPillarRiseInterval);
                if (BattleEnded()) yield break;
            }

            var loop = loopFrames.Length > 0 ? loopFrames : new[] { riseFrames[riseFrames.Length - 1] };
            float hitInterval = BloodPillarTornadoDuration / hits;
            int dealt = 0;
            int frame = 0;
            float frameTimer = 0f;
            SetBloodPillarFrame(loop[0]);
            for (float t = 0f; t < BloodPillarTornadoDuration || dealt < hits; t += Time.unscaledDeltaTime)
            {
                if (riseFrames.Length == 0) bloodPillarImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / BloodVortexFadeInTime));
                if (dealt < hits && t >= dealt * hitInterval)
                {
                    dealt++;
                    DealBloodPillarHit(hitDamage, attackName);
                    if (BattleEnded()) yield break;
                }
                frameTimer += Time.unscaledDeltaTime;
                if (frameTimer >= loopInterval)
                {
                    frameTimer -= loopInterval;
                    frame = (frame + 1) % loop.Length;
                    SetBloodPillarFrame(loop[frame]);
                }
                yield return null;
            }

            for (float f = 0f; f < BloodPillarFadeTime; f += Time.unscaledDeltaTime)
            {
                bloodPillarImage.color = new Color(1f, 1f, 1f, 1f - f / BloodPillarFadeTime);
                yield return null;
            }
            bloodPillarImage.gameObject.SetActive(false);
        }

        private void DealBloodPillarHit(float damage, string attackName)
        {
            HurtPlayer(damage);
            ShowResult($"{attackName}! -{damage:0}", MissColor);
            RefreshHpBars();
        }

        // 몬스터가 플레이어에게 피해를 준다(막을 수 없는 연타라 피격 무적 없이). 루나벨전이면 플레이어 머리 위에
        // 루나벨 전용 핏빛 숫자(UI/DamageDigits/lunabel_N)를 띄운다.
        private void HurtPlayer(float damage)
        {
            player.TakeDamage(damage, monster.transform.position, hitReaction: false);
            TriggerPlayerHitAnim();
            if (isLunabelBattle && lunabelDigits[0] != null)
                ShowDamageNumber(playerDamageNumberLayer, lunabelDigits, LunabelDigitHeight, damage, false);
        }

        // ---- 루나벨 1페이즈: 혈류 흡수 → 피의 회오리 ----
        // 1페이즈 적 턴을 세어 LunabelAbsorbEveryNthTurn번째 턴마다(3, 6, 9...) 피를 빨아들이는 모션(Absorb 8프레임)과 함께
        // 최대 체력의 LunabelAbsorbHealRatio만큼 회복하고 피가 들끓는 상태(BloodCharged)가 된다(그 턴은 노드 패턴 없음).
        // 그 뒤 루나벨의 노드를 LunabelBloodTornadoMissThreshold개 놓치면(여러 턴에 걸쳐 쌓여도 된다) 그 적 턴 끝에
        // 플레이어 발밑에서 피의 회오리(Lubel_Skill1)가 휘몰아치며 여러 번 나눠 피해를 준다(막을 수 없음).
        // 아직 들끓는 중에 다시 흡수하면 회복만 하고, 그때까지 쌓인 놓친 노드 수는 그대로 이어진다.
        private const string LunabelBloodTornadoName = "피의 회오리";
        private const int LunabelBloodTornadoMissThreshold = 5;
        private const float BloodVortexFrameInterval = 0.08f;
        private const float BloodVortexFadeInTime = 0.2f;
        private Sprite[] bloodVortexFrames = System.Array.Empty<Sprite>();
        private const int LunabelAbsorbEveryNthTurn = 3;
        private int lunabelPhaseOneTurns; // 이번 전투에서 루나벨이 1페이즈로 친 적 턴 수(흡수 턴 포함)
        private const float LunabelAbsorbHealRatio = 0.12f;
        private const float LunabelAbsorbFrameInterval = 0.15f;
        private const int LunabelAbsorbHealFrame = 5; // 핏줄기가 가장 크게 휘감기는 6번째 프레임에서 회복한다
        private const float LunabelAbsorbAfterPause = 0.6f;
        private const float LunabelBloodTornadoWarnPause = 1.0f;
        private const int LunabelBloodTornadoHits = 6;
        private const float LunabelBloodTornadoDamageMultiplier = 3f;
        private Sprite[] lunabelAbsorbFrames = System.Array.Empty<Sprite>();

        // 1페이즈 적 턴마다 한 번 부른다(턴 수를 센다). 3번째 턴마다 true.
        private bool ShouldLunabelAbsorb()
        {
            lunabelPhaseOneTurns++;
            return lunabelAbsorbFrames.Length > 0 && lunabelPhaseOneTurns % LunabelAbsorbEveryNthTurn == 0;
        }

        private IEnumerator LunabelBloodAbsorb(FinalBossAI finalBoss)
        {
            phaseBannerText.color = EnemyNoteColor;
            phaseBannerText.text = "루나벨이 주위의 피를 빨아들인다...";
            PlayMonsterOneShot(lunabelAbsorbFrames, LunabelAbsorbFrameInterval, restart: true);
            int healFrame = Mathf.Min(LunabelAbsorbHealFrame, lunabelAbsorbFrames.Length - 1);
            yield return new WaitForSecondsRealtime(LunabelAbsorbFrameInterval * healFrame);
            if (BattleEnded()) yield break;

            float before = monster.CurrentHp;
            monster.Heal(monster.MaxHp * LunabelAbsorbHealRatio);
            RefreshHpBars();
            ShowResult($"혈류 흡수! 체력 +{monster.CurrentHp - before:0}", EnemyNoteColor);
            // 이미 들끓는 중이면 쌓인 놓친 노드 수는 그대로 이어 간다.
            if (!finalBoss.BloodCharged) finalBoss.BloodMisses = 0;
            finalBoss.BloodCharged = true;
            yield return new WaitForSecondsRealtime(LunabelAbsorbFrameInterval * (lunabelAbsorbFrames.Length - healFrame));
            if (BattleEnded()) yield break;

            phaseBannerText.text = $"루나벨의 몸에 피가 들끓는다! 노드를 {LunabelBloodTornadoMissThreshold}개 놓치면 피의 회오리가 터진다!";
            yield return new WaitForSecondsRealtime(LunabelAbsorbAfterPause);
            phaseBannerText.text = "";
        }

        private IEnumerator LunabelBloodTornado()
        {
            phaseBannerText.color = EnemyNoteColor;
            phaseBannerText.text = "흘린 피가 공명한다... 빨아들인 피가 발밑에서 끓어오른다...!";
            PlayMonsterOneShot(monsterAttackFrames, MonsterAttackFrameInterval, restart: true);
            yield return new WaitForSecondsRealtime(LunabelBloodTornadoWarnPause);
            if (BattleEnded()) yield break;

            phaseBannerText.text = $"{LunabelBloodTornadoName}!";
            yield return BloodPillar(LunabelBloodTornadoHits, LunabelBloodTornadoDamageMultiplier, LunabelBloodTornadoName,
                System.Array.Empty<Sprite>(), bloodVortexFrames.Length > 0 ? bloodVortexFrames : bloodPillarTornadoFrames,
                bloodVortexFrames.Length > 0 ? BloodVortexFrameInterval : BloodPillarTornadoInterval);
            phaseBannerText.text = "";
        }

        private void SetBloodPillarFrame(Sprite sprite)
        {
            bloodPillarImage.sprite = sprite;
            bloodPillarImage.rectTransform.sizeDelta = sprite.rect.size * BloodPillarScale * PxToCanvas;
        }

        private void LaunchBloodOrb()
        {
            if (bloodOrbSprite == null) return;
            StopBloodOrb();
            bloodOrbRoutine = StartCoroutine(BloodOrbRoutine());
        }

        // 새 전투가 시작될 때 지난 전투의 구체/피의 기둥이 남아 있지 않게 치운다.
        private void StopBloodOrb()
        {
            if (bloodOrbRoutine != null)
            {
                StopCoroutine(bloodOrbRoutine);
                bloodOrbRoutine = null;
            }
            if (bloodOrbImage != null) bloodOrbImage.gameObject.SetActive(false);
            if (bloodPillarImage != null) bloodPillarImage.gameObject.SetActive(false);
            if (sandBeamImage != null) sandBeamImage.gameObject.SetActive(false);
        }

        private IEnumerator BloodOrbRoutine()
        {
            var rect = bloodOrbImage.rectTransform;
            bloodOrbImage.sprite = bloodOrbSprite;
            bloodOrbImage.color = Color.white;
            bloodOrbImage.gameObject.SetActive(true);

            for (float t = 0f; t < BloodOrbTravelTime; t += Time.unscaledDeltaTime)
            {
                float k = t / BloodOrbTravelTime;
                float arc = 4f * k * (1f - k) * BloodOrbArcHeight;
                rect.anchoredPosition = ImageToCanvasPos(Mathf.Lerp(BloodOrbStartX, BloodOrbEndX, k),
                    Mathf.Lerp(BloodOrbStartY, BloodOrbEndY, k) - arc);
                float size = Mathf.Lerp(BloodOrbStartSize, BloodOrbEndSize, k) * PxToCanvas;
                rect.sizeDelta = new Vector2(size, size);
                rect.localRotation = Quaternion.Euler(0f, 0f, t * BloodOrbSpinSpeed);
                yield return null;
            }

            // 발밑에 닿으면 납작하게 눌리며 땅으로 스며든다.
            rect.anchoredPosition = ImageToCanvasPos(BloodOrbEndX, BloodOrbEndY);
            rect.localRotation = Quaternion.identity;
            float endSize = BloodOrbEndSize * PxToCanvas;
            for (float f = 0f; f < BloodOrbSinkTime; f += Time.unscaledDeltaTime)
            {
                float k = f / BloodOrbSinkTime;
                rect.sizeDelta = new Vector2(endSize * (1f + 0.3f * k), endSize * (1f - 0.8f * k));
                bloodOrbImage.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }
            bloodOrbImage.gameObject.SetActive(false);
            bloodOrbRoutine = null;
        }

        // ---- 보스전 도주 차단 ----
        // 보스 기사전에서 도주를 고르면 도망치지 못하고, 보스가 "도망칠 수 없다"며 검기를 날려 바로 피해를 준다.
        // 그 턴은 그대로 끝나고 적 턴으로 넘어간다.
        private const float FleeBlockedLinePause = 0.9f;
        private const float FleeBlockedWaveDamageMultiplier = 2f;

        // 리듬 대결(미니게임)에서는 도주가 곧 그만두기라 막지 않는다.
        private bool BossBlocksFlee => isBossBattle && !isMinigame && monster.GetComponent<BossAI>() != null && swordWaveFrames.Length > 0;

        private IEnumerator BossBlockFlee()
        {
            phaseBannerText.color = EnemyNoteColor;
            phaseBannerText.text = "알리아스: \"어딜 도망치려 하느냐! 도망칠 수 없다!\"";
            ShowResult("도망칠 수 없다!", MissColor);
            yield return new WaitForSecondsRealtime(FleeBlockedLinePause);
            if (BattleEnded()) yield break;

            yield return FireSwordWaveAtPlayer(FleeBlockedWaveDamageMultiplier, "검기에 베였다!");
        }

        // 필살기를 캔슬당한 보스가 휘청인다: 좌우로 흔들리며 몇 번 번쩍인다.
        private void PlayMonsterStagger()
        {
            if (!monsterPortrait.enabled) return;
            if (monsterStaggerRoutine != null) StopCoroutine(monsterStaggerRoutine);
            monsterStaggerRoutine = StartCoroutine(MonsterStaggerRoutine());
        }

        private IEnumerator MonsterStaggerRoutine()
        {
            var rect = monsterPortrait.rectTransform;
            const int shakes = 8;
            float step = BossSpecialStaggerDuration * 0.6f / shakes;
            for (int i = 0; i < shakes; i++)
            {
                float falloff = 1f - (float)i / shakes;
                rect.anchoredPosition = new Vector2((i % 2 == 0 ? 1f : -1f) * BossSpecialStaggerShake * falloff, 0f);
                monsterPortrait.color = i % 2 == 0 ? MonsterHitFlashColor : Color.white;
                yield return new WaitForSecondsRealtime(step);
            }
            rect.anchoredPosition = Vector2.zero;
            monsterPortrait.color = Color.white;
            monsterStaggerRoutine = null;
        }

        private void LaunchSwordWave(float travelTime)
        {
            if (swordWaveFrames.Length == 0) return;
            StopSwordWave();
            swordWaveRoutine = StartCoroutine(SwordWaveRoutine(travelTime));
        }

        private void StopSwordWave()
        {
            if (swordWaveRoutine != null)
            {
                StopCoroutine(swordWaveRoutine);
                swordWaveRoutine = null;
            }
            if (swordWaveImage != null) swordWaveImage.gameObject.SetActive(false);
        }

        private IEnumerator SwordWaveRoutine(float travelTime)
        {
            var rect = swordWaveImage.rectTransform;
            float centerY = SwordWaveBottomY - SwordWaveHeight * 0.5f;
            swordWaveImage.color = Color.white;
            swordWaveImage.sprite = swordWaveFrames[0];
            swordWaveImage.gameObject.SetActive(true);

            float t = 0f;
            float frameTimer = 0f;
            int frame = 0;
            while (t < travelTime)
            {
                rect.anchoredPosition = ImageToCanvasPos(Mathf.Lerp(SwordWaveStartX, SwordWaveEndX, t / travelTime), centerY);

                // 첫 프레임(솟구침)을 잠깐 보여 준 뒤, 나머지 프레임(굴러가는 검기)을 반복한다.
                frameTimer += Time.unscaledDeltaTime;
                float interval = frame == 0 ? SwordWaveBurstInterval : SwordWaveFrameInterval;
                if (frameTimer >= interval)
                {
                    frameTimer -= interval;
                    frame = swordWaveFrames.Length > 1 ? 1 + (frame % (swordWaveFrames.Length - 1)) : 0;
                    swordWaveImage.sprite = swordWaveFrames[frame];
                }

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // 플레이어에게 닿으면 짧게 사라진다.
            const float fade = 0.15f;
            for (float f = 0f; f < fade; f += Time.unscaledDeltaTime)
            {
                swordWaveImage.color = new Color(1f, 1f, 1f, 1f - f / fade);
                yield return null;
            }
            swordWaveImage.gameObject.SetActive(false);
            swordWaveRoutine = null;
        }
    }
}

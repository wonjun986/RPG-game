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
    // 적 턴: 턴 배너, 일반/보스 패턴 턴, 루나벨 2페이즈 전환, 피의 손.
    public partial class RhythmBattleUI
    {
        // 턴 시작마다 그 턴의 로고 배너를 잠깐 띄웠다 지운다.
        private IEnumerator ShowTurnBanner(Sprite sprite)
        {
            turnBannerImage.sprite = sprite;
            turnBannerImage.gameObject.SetActive(sprite != null);
            yield return new WaitForSecondsRealtime(PhaseBannerDuration);
            turnBannerImage.gameObject.SetActive(false);
        }

        private IEnumerator EnemyTurn()
        {
            yield return ShowTurnBanner(enemyTurnBannerSprite);

            if (BattleEnded()) yield break;

            var bossAI = monster.GetComponent<BossAI>();
            if (bossAI != null && bossAI.ShouldUseSpecialMove && bossSpecialRaiseFrames.Length > 0 && bossSpecialChargeFrames.Length > 0)
            {
                int specialIndex = bossAI.SpecialMovesUsed;
                bossAI.MarkSpecialMoveUsed();
                yield return BossSpecialMove(specialIndex);
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(PlayerTurn());
                yield break;
            }

            var finalBoss = monster.GetComponent<FinalBossAI>();
            if (finalBoss != null && finalBoss.ShouldUseSpecialMove
                && lunabelSpecialFrames.Length > 0 && lunabelSpecialChargeFrames.Length > 0)
            {
                int specialIndex = finalBoss.SpecialMovesUsed;
                finalBoss.MarkSpecialMoveUsed();
                yield return LunabelSpecialMove(specialIndex);
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(PlayerTurn());
                yield break;
            }

            if (finalBoss != null && finalBoss.ShouldStartPhaseTwo)
            {
                // 2페이즈 변신 연출 뒤, 같은 적 턴에 곧바로 2페이즈 패턴을 친다.
                yield return LunabelPhaseTwo(finalBoss);
                if (BattleEnded()) yield break;
            }
            else if (finalBoss != null && !finalBoss.PhaseTwoStarted && ShouldLunabelAbsorb())
            {
                // 1페이즈 전용: 3번째 적 턴마다 피를 흡수해 체력을 채우는 턴. 이후 루나벨의 노드를 일정 개수 놓치면 피의 회오리가 터진다.
                yield return LunabelBloodAbsorb(finalBoss);
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(PlayerTurn());
                yield break;
            }

            // 황금 모래룡: 3번째 적 턴마다 황금 브레스(충전 → 차징 → 발사), 그 밖의 짝수 턴엔 대지 강타 → 꼬리 대지 폭발.
            if (isSandDragonBattle) sandDragonEnemyTurns++;
            if (isSandDragonBattle && sandChargeFrames.Length > 0 && sandBeamFrames.Length > 0
                && sandDragonEnemyTurns % SandDragonBeamEveryTurns == 0)
            {
                yield return SandDragonBeam();
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(PlayerTurn());
                yield break;
            }
            if (isSandDragonBattle && sandTailFrames.Length > 0 && sandDragonEnemyTurns % SandDragonTailEveryTurns == 0)
            {
                yield return SandDragonTailStrike();
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(PlayerTurn());
                yield break;
            }

            if (isBossBattle)
            {
                yield return BossPatternTurn();
                if (BattleEnded()) yield break;
                // 피가 들끓는 동안 놓친 노드가 문턱에 닿았으면 이 적 턴 끝에 곧바로 피의 회오리가 터진다.
                if (finalBoss != null && finalBoss.BloodCharged && finalBoss.BloodMisses >= LunabelBloodTornadoMissThreshold)
                {
                    finalBoss.BloodCharged = false;
                    finalBoss.BloodMisses = 0;
                    yield return new WaitForSecondsRealtime(AfterChartPause);
                    if (BattleEnded()) yield break;
                    yield return LunabelBloodTornado();
                    if (BattleEnded()) yield break;
                }
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(PlayerTurn());
                yield break;
            }

            var notes = BuildChartNotes(PickChart());
            yield return RunChart(notes, EnemyNoteColor, blockWindow, (note, hit, diff) =>
            {
                PlayMonsterOneShot(monsterAttackFrames, MonsterAttackFrameInterval, restart: false);
                if (hit)
                {
                    TriggerPlayerGuardAnim();
                    ShowResult("막기 성공!", GoodColor);
                }
                else
                {
                    // 연타 중 연속 피격이 피격 무적에 묻히지 않도록 hitReaction 없이 맞는다.
                    // 몬스터 공격은 GOOD(1.0) 고정: (몬스터 공격력 - 내 방어력) x 0.7 + 5, 랜덤 -1~+1.
                    float damage = CombatMath.PhysicalDamage(monster.Attack, player.Stats.defense);
                    player.TakeDamage(damage, monster.transform.position, hitReaction: false);
                    TriggerPlayerHitAnim();
                    ShowResult($"피격! -{damage:0}", MissColor);
                }
                RefreshHpBars();
            });

            if (BattleEnded()) yield break;
            yield return new WaitForSecondsRealtime(AfterChartPause);
            if (BattleEnded()) yield break;
            turnRoutine = StartCoroutine(PlayerTurn());
        }

        // 루나벨은 2페이즈로 각성한 뒤로 계속 분노 상태다(흡혈로 체력이 절반 위로 다시 차도 풀리지 않는다).
        private bool BossEnraged
        {
            get
            {
                if (monster == null) return false;
                var finalBoss = monster.GetComponent<FinalBossAI>();
                if (finalBoss != null) return finalBoss.PhaseTwoStarted;
                return monster.CurrentHp <= monster.MaxHp * BossEnrageHealthFraction;
            }
        }

        // ---- 루나벨 2페이즈 ----
        // 체력이 절반 이하로 처음 내려간 뒤의 적 턴: 대사 → 변신(Phase2 9프레임, 마지막 프레임에서 잠깐 멈춤) → 체력을 모두
        // 채우고 "2페이즈" 배너. 이후로는 공중에 뜬 2페이즈 대기 모션(Phase2Idle)으로 서 있고, 분노 상태(노드가 더 빠르고 아프며, 분노 전용 패턴과
        // 연계 공격)로 싸운다.
        private const float LunabelTransformFrameInterval = 0.1f; // 변신이 18장이 되어 장당 시간을 줄였다(약 1.8초)
        private const float LunabelPhaseLinePause = 1.3f;
        private const float LunabelTransformHold = 0.5f;
        private const float LunabelPhaseBannerPause = 1.4f;
        private Sprite[] lunabelPhaseTwoFrames = System.Array.Empty<Sprite>();
        private Sprite[] lunabelPhaseTwoIdleFrames = System.Array.Empty<Sprite>();
        private Sprite lunabelBaseIdleSprite;
        // 맞는 모션은 1페이즈(변신 전) 모습으로 그려져 있어서, 변신 전에만 쓴다.
        private Sprite[] lunabelHitFrames = System.Array.Empty<Sprite>();

        private IEnumerator LunabelPhaseTwo(FinalBossAI finalBoss)
        {
            phaseBannerText.color = EnemyNoteColor;
            phaseBannerText.text = "루나벨: \"후후... 그날 밤보다는 제법이네. 하지만 결말은 똑같아. 네 피, 전부 받아 갈게.\"";
            yield return new WaitForSecondsRealtime(LunabelPhaseLinePause);
            if (BattleEnded()) yield break;

            // 2페이즈 대기 모션이 없으면 변신 마지막 프레임(뿔과 날개가 돋은 모습)으로 서 있는다.
            Sprite[] phaseTwoIdle = lunabelPhaseTwoIdleFrames.Length > 0 ? lunabelPhaseTwoIdleFrames
                : lunabelPhaseTwoFrames.Length > 0 ? new[] { lunabelPhaseTwoFrames[lunabelPhaseTwoFrames.Length - 1] }
                : System.Array.Empty<Sprite>();
            if (lunabelPhaseTwoFrames.Length > 0)
            {
                phaseBannerText.text = "루나벨의 몸에서 붉은 기운이 터져 나온다...";
                // 변신 프레임을 한 번 재생한 뒤, 끝나면 2페이즈 대기 모션으로 넘어간다.
                monsterIdleFrames = phaseTwoIdle;
                monsterHitFrames = System.Array.Empty<Sprite>();
                PlayMonsterOneShot(lunabelPhaseTwoFrames, LunabelTransformFrameInterval, restart: true);
                yield return new WaitForSecondsRealtime(LunabelTransformFrameInterval * lunabelPhaseTwoFrames.Length + LunabelTransformHold);
                if (BattleEnded()) yield break;
            }

            finalBoss.EnterPhaseTwo(phaseTwoIdle);
            if (lunabelPhaseTwoAttackFrames.Length > 0) monsterAttackFrames = lunabelPhaseTwoAttackFrames;
            RefreshHpBars();
            phaseBannerText.color = EnragedNoteColor;
            phaseBannerText.text = "2페이즈 - 피의 여왕 루나벨이 각성했다! 체력을 모두 회복했다!";
            ShowResult("2페이즈!", EnragedNoteColor);
            yield return new WaitForSecondsRealtime(LunabelPhaseBannerPause);
            phaseBannerText.text = "";
        }

        // 패턴 하나를 치고, 분노 상태면 확률적으로 다른 패턴 하나를 곧바로 이어 친다.
        private IEnumerator BossPatternTurn()
        {
            yield return RunBossPattern(PickBossPattern(), combo: false);
            if (BattleEnded()) yield break;

            if (BossEnraged && Random.value < BossComboChance)
            {
                yield return new WaitForSecondsRealtime(BossComboPause);
                if (BattleEnded()) yield break;
                yield return RunBossPattern(PickBossPattern(), combo: true);
            }
        }

        private BossPattern PickBossPattern()
        {
            bool enraged = BossEnraged;
            var candidates = new List<BossPattern>();
            foreach (var pattern in BossPatterns)
            {
                if (pattern.EnragedOnly && !enraged) continue;
                // 루나벨은 노드가 붉은 안개 속으로 사라지는 패턴(그림자 손아귀)을 쓰지 않는다.
                if (pattern.Vanish && isLunabelBattle) continue;
                // 모래룡은 흡혈하는 존재가 아니라서 흡혈 참격을 쓰지 않는다(대신 황금 브레스가 있다).
                if (pattern.Drain && isSandDragonBattle) continue;
                if (pattern == lastBossPattern) continue;
                candidates.Add(pattern);
            }
            var picked = candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : BossPatterns[0];
            lastBossPattern = picked;
            return picked;
        }

        private IEnumerator RunBossPattern(BossPattern pattern, bool combo)
        {
            bool enraged = BossEnraged;
            string prefix = combo ? "연계! " : enraged ? "[분노] " : "";
            phaseBannerText.color = enraged ? EnragedNoteColor : EnemyNoteColor;
            bool blood = isLunabelBattle && pattern.BloodName != null;
            phaseBannerText.text = $"{prefix}{(blood ? pattern.BloodName : pattern.Name)} - {(blood ? pattern.BloodHint : pattern.Hint)}";
            var chargedBoss = isLunabelBattle ? monster.GetComponent<FinalBossAI>() : null;
            if (chargedBoss != null && chargedBoss.BloodCharged)
                phaseBannerText.text += $"  [피의 공명 {chargedBoss.BloodMisses}/{LunabelBloodTornadoMissThreshold}]";
            yield return new WaitForSecondsRealtime(BossPatternAnnounceDuration);
            if (BattleEnded()) yield break;

            float baseDamage = isLunabelBattle && pattern.BloodDamageMultiplier > 0f ? pattern.BloodDamageMultiplier : pattern.DamageMultiplier;
            float damageScale = baseDamage * (enraged ? BossEnrageDamageScale : 1f);
            float approachScale = isLunabelBattle && pattern.BloodApproachScale > 0f ? pattern.BloodApproachScale : pattern.ApproachScale;
            float stepScale = isLunabelBattle && pattern.BloodStepScale > 0f ? pattern.BloodStepScale : pattern.StepScale;
            telegraphDuration = baseTelegraphDuration * approachScale * (enraged ? BossEnrageApproachScale : 1f);
            stepDuration = baseStepDuration * stepScale;
            vanishNotes = pattern.Vanish;
            useLunabelNotes = isLunabelBattle && lunabelTapSprite != null;

            var notes = BuildChartNotes(pattern.Charts[Random.Range(0, pattern.Charts.Length)]);
            // 루나벨이 피를 흡수해 들끓는 동안에는 놓친 노드를 센다(문턱에 닿으면 적 턴 끝에 피의 회오리).
            var bloodBoss = chargedBoss;
            yield return RunChart(notes, enraged ? EnragedNoteColor : EnemyNoteColor, blockWindow, (note, hit, diff) =>
            {
                if (!isLunabelBattle) PlayMonsterOneShot(monsterAttackFrames, MonsterAttackFrameInterval, restart: false);
                if (hit)
                {
                    TriggerPlayerGuardAnim();
                    ShowResult("막기 성공!", GoodColor);
                }
                else
                {
                    float damage = CombatMath.PhysicalDamage(monster.Attack, player.Stats.defense) * damageScale;
                    HurtPlayer(damage);
                    string bloodGauge = "";
                    if (bloodBoss != null && bloodBoss.BloodCharged)
                    {
                        bloodBoss.BloodMisses = Mathf.Min(LunabelBloodTornadoMissThreshold, bloodBoss.BloodMisses + 1);
                        bloodGauge = bloodBoss.BloodMisses >= LunabelBloodTornadoMissThreshold
                            ? " 피의 공명 가득!"
                            : $" 피의 공명 {bloodBoss.BloodMisses}/{LunabelBloodTornadoMissThreshold}";
                    }
                    if (pattern.Drain)
                    {
                        monster.Heal(damage * BossDrainRatio);
                        ShowResult($"피격! -{damage:0} (보스 흡혈 +{damage * BossDrainRatio:0}){bloodGauge}", MissColor);
                    }
                    else
                    {
                        ShowResult($"피격! -{damage:0}{bloodGauge}", MissColor);
                    }
                }
                RefreshHpBars();
            }, isLunabelBattle ? LaunchBloodHand : null, BloodHandTravelTime);

            telegraphDuration = baseTelegraphDuration;
            stepDuration = baseStepDuration;
            vanishNotes = false;
            useLunabelNotes = false;
            phaseBannerText.text = "";
        }

        // 루나벨이 손을 뻗고(공격 모션), 피의 손 하나가 BloodHandTravelTime에 걸쳐 플레이어에게 날아간다.
        private void LaunchBloodHand(ChartNote note)
        {
            PlayMonsterOneShot(monsterAttackFrames, MonsterAttackFrameInterval, restart: true);
            if (bloodHandFrames.Length == 0) return;
            for (int i = 0; i < BloodHandPoolSize; i++)
            {
                if (bloodHandPool[i].gameObject.activeSelf) continue;
                bloodHandRoutines[i] = StartCoroutine(BloodHandRoutine(i));
                return;
            }
        }

        private IEnumerator BloodHandRoutine(int index)
        {
            var image = bloodHandPool[index];
            var rect = image.rectTransform;
            image.color = Color.white;
            image.sprite = bloodHandFrames[0];
            image.gameObject.SetActive(true);

            float t = 0f;
            float frameTimer = 0f;
            int frame = 0;
            while (t < BloodHandTravelTime)
            {
                rect.anchoredPosition = ImageToCanvasPos(Mathf.Lerp(BloodHandStartX, BloodHandEndX, t / BloodHandTravelTime), BloodHandCenterY);
                frameTimer += Time.unscaledDeltaTime;
                if (frameTimer >= BloodHandFrameInterval)
                {
                    frameTimer -= BloodHandFrameInterval;
                    frame = (frame + 1) % bloodHandFrames.Length;
                    image.sprite = bloodHandFrames[frame];
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // 플레이어에게 닿으면 짧게 사라진다(막았든 맞았든 결과는 판정 쪽에서 보여 준다).
            for (float f = 0f; f < BloodHandFadeTime; f += Time.unscaledDeltaTime)
            {
                image.color = new Color(1f, 1f, 1f, 1f - f / BloodHandFadeTime);
                yield return null;
            }
            image.gameObject.SetActive(false);
            bloodHandRoutines[index] = null;
        }

        private void StopBloodHands()
        {
            for (int i = 0; i < BloodHandPoolSize; i++)
            {
                if (bloodHandRoutines[i] != null)
                {
                    StopCoroutine(bloodHandRoutines[i]);
                    bloodHandRoutines[i] = null;
                }
                if (bloodHandPool[i] != null) bloodHandPool[i].gameObject.SetActive(false);
            }
        }
    }
}

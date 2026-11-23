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
    // 내 턴: 공격/스킬 턴, 행동·스킬 선택 창, 도주 잠금, 네크로시아 모션, 궁극기 각성.
    public partial class RhythmBattleUI
    {
        private static float AverageNoteCount(string[] charts)
        {
            int total = 0;
            foreach (var chart in charts)
            {
                foreach (char c in chart)
                {
                    if (c == 'U' || c == 'D' || c == 'u' || c == 'd' || c == 'A' || c == 'B') total++;
                }
            }
            return charts.Length > 0 ? (float)total / charts.Length : 1f;
        }

        private string PickChart()
        {
            var charts = isBossBattle ? BossCharts : NormalCharts;
            return charts[Random.Range(0, charts.Length)];
        }

        // 채보 문자열을 "몇 초에 어느 레인" 목록으로 바꾼다. 첫 노드가 화면 오른쪽 끝에서 출발해
        // 판정점까지 날아올 시간(telegraphDuration)을 앞에 깔아 둔다. 절반 확률로 위/아래를 뒤집는다.
        private List<ChartNote> BuildChartNotes(string chart)
        {
            bool mirror = Random.value < 0.5f;
            var notes = new List<ChartNote>();
            for (int i = 0; i < chart.Length; i++)
            {
                int lane;
                char c = chart[i];
                bool feint = c == 'A' || c == 'B';
                if (c == 'U' || c == 'u' || c == 'B') lane = LaneUp;
                else if (c == 'D' || c == 'd' || c == 'A') lane = LaneDown;
                else continue;
                if (mirror) lane = 1 - lane;
                float hitTime = telegraphDuration + i * stepDuration;
                if (feint)
                {
                    notes.Add(new ChartNote
                    {
                        Lane = lane, ShownLane = 1 - lane, HitTime = hitTime, EndTime = hitTime,
                        SwitchTime = hitTime - telegraphDuration * FeintSwitchLeadFraction,
                    });
                    continue;
                }
                int holdSteps = 0;
                if (c == 'u' || c == 'd')
                {
                    while (i + holdSteps + 1 < chart.Length && chart[i + holdSteps + 1] == '=') holdSteps++;
                }
                notes.Add(new ChartNote { Lane = lane, ShownLane = lane, HitTime = hitTime, EndTime = hitTime + holdSteps * stepDuration });
            }
            return notes;
        }

        private IEnumerator PlayerTurn()
        {
            yield return ShowTurnBanner(myTurnBannerSprite);

            if (BattleEnded()) yield break;
            if (isPractice)
            {
                skillGauge = MaxSkillGauge;
                RefreshHpBars();
            }
            // 튜토리얼 몬스터: 체력이 절반 이하면 필살 게이지를 채워 주고 스킬로 마무리하라고 알려 준다.
            bool tutorialFinish = monster.GetComponent<TutorialSkillFinish>() != null
                && monster.CurrentHp <= monster.MaxHp * TutorialSkillFinish.HintHealthFraction;
            if (tutorialFinish)
            {
                skillGauge = MaxSkillGauge;
                RefreshHpBars();
                phaseBannerText.color = SkillNoteColor;
                phaseBannerText.text = "필살 게이지가 가득 찼다! [스킬]을 골라 마무리하자!";
            }
            yield return ShowActionSelect();
            if (tutorialFinish) phaseBannerText.text = "";
            if (BattleEnded()) yield break;

            if (actionSelectFlee)
            {
                if (BossBlocksFlee)
                {
                    yield return BossBlockFlee();
                    if (BattleEnded()) yield break;
                    yield return new WaitForSecondsRealtime(AfterChartPause);
                    if (BattleEnded()) yield break;
                    turnRoutine = StartCoroutine(EnemyTurn());
                    yield break;
                }
                EndBattle(fled: true);
                yield break;
            }

            if (actionSelectPotion)
            {
                // 포션은 선택 창에서 이미 마셨다. 회복량을 잠깐 보여주고 공격 없이 적 턴으로 넘긴다.
                yield return new WaitForSecondsRealtime(PotionResultDuration);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(EnemyTurn());
                yield break;
            }

            // 스킬을 골랐으면 필살 게이지는 선택 창에서 이미 썼다. 채보 → 마무리 연출 순서로 따로 진행한다.
            if (chosenSkill >= 0)
            {
                int skillIndex = chosenSkill;
                chosenSkill = -1;
                yield return SkillTurn(skillIndex);
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
                turnRoutine = StartCoroutine(EnemyTurn());
                yield break;
            }

            var pattern = PickPlayerPattern();
            phaseBannerText.color = PlayerNoteColor;
            phaseBannerText.text = $"{pattern.Name} - {pattern.Hint}";
            yield return new WaitForSecondsRealtime(PlayerPatternAnnounceDuration);
            if (BattleEnded()) yield break;

            var baseCharts = isBossBattle ? BossCharts : NormalCharts;
            var patternCharts = pattern.Charts ?? baseCharts;
            float perNote = pattern.Power * AverageNoteCount(baseCharts) / AverageNoteCount(patternCharts);
            telegraphDuration = baseTelegraphDuration * pattern.ApproachScale;
            stepDuration = baseStepDuration * pattern.StepScale;
            var notes = BuildChartNotes(patternCharts[Random.Range(0, patternCharts.Length)]);
            int combo = 0;
            bool fullCombo = true;

            yield return RunChart(notes, PlayerNoteColor, okWindow, (note, hit, diff) =>
            {
                if (hit)
                {
                    combo++;
                    // 축복 특성 박자 유지: 콤보 보너스 상한이 올라간다.
                    float comboScale = 1f + Mathf.Min(player.TalentComboCap(MaxComboBonus), (combo - 1) * ComboBonusPerHit);
                    phaseBannerText.text = combo >= 2 ? $"{pattern.Name}  {combo} COMBO" : pattern.Name;
                    float rhythm;
                    float gauge;
                    if (diff <= perfectWindow)
                    {
                        rhythm = player.TalentPerfectMultiplier(PerfectMultiplier); // 축복 특성 완벽주의
                        gauge = GaugePerPerfect;
                        ShowJudgment(judgmentPerfect);
                    }
                    else if (diff <= goodWindow)
                    {
                        rhythm = GoodMultiplier;
                        gauge = GaugePerGood;
                        ShowJudgment(judgmentExcellent);
                    }
                    else
                    {
                        rhythm = OkMultiplier;
                        gauge = GaugePerOk;
                        ShowJudgment(judgmentGood);
                    }
                    TriggerPlayerAttackAnim();
                    // 채보는 멈추지 않고 계속 흘러가야 하므로 기다리지 않고 따로 띄운다.
                    StartCoroutine(DealDamageAfterImpact(rhythm * perNote * comboScale, false, battleSerial));
                    skillGauge = Mathf.Min(MaxSkillGauge, skillGauge + gauge * player.TalentGaugeMultiplier); // 축복 특성 고동
                    RefreshHpBars();
                }
                else
                {
                    // 놓쳐도 MISS 배수(0.3)만큼은 들어간다. 모션 없이 바로 넣는다. 콤보는 끊긴다.
                    combo = 0;
                    fullCombo = false;
                    phaseBannerText.text = pattern.Name;
                    ShowJudgment(judgmentMiss);
                    nextHitIsMiss = true;
                    player.DealDamage(monster, MissMultiplier * perNote, false);
                    nextHitIsMiss = false;
                    RefreshHpBars();
                }
            });

            telegraphDuration = baseTelegraphDuration;
            stepDuration = baseStepDuration;
            if (BattleEnded()) yield break;
            // 마지막 노드의 지연 데미지까지 들어간 뒤에 넘어간다.
            yield return new WaitForSecondsRealtime(Mathf.Max(AfterChartPause, AttackImpactDelay));
            if (BattleEnded()) yield break;

            if (fullCombo && notes.Count > 0)
            {
                phaseBannerText.text = $"풀 콤보! 마무리 일격 ({notes.Count} COMBO)";
                TriggerPlayerAttackAnim();
                // 축복 특성 죽음의 선율: 마무리 일격이 더 세지고 필살 게이지가 찬다.
                bool melody = player.HasTalent(TalentId.DeathMelody);
                float finisher = PerfectMultiplier * perNote * FullComboFinisherHits * (1f + MaxComboBonus);
                if (melody)
                {
                    finisher *= 1f + BlessingRules.DeathMelodyFinisherBonus;
                    skillGauge = Mathf.Min(MaxSkillGauge, skillGauge + BlessingRules.DeathMelodyGauge);
                    phaseBannerText.text = $"죽음의 선율! 마무리 일격 ({notes.Count} COMBO)";
                }
                yield return DealDamageAfterImpact(finisher, false, battleSerial);
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(AfterChartPause);
                if (BattleEnded()) yield break;
            }
            phaseBannerText.text = "";
            turnRoutine = StartCoroutine(EnemyTurn());
        }

        private PlayerPattern PickPlayerPattern()
        {
            var candidates = new List<PlayerPattern>();
            foreach (var pattern in PlayerPatterns)
            {
                if (pattern != lastPlayerPattern) candidates.Add(pattern);
            }
            var picked = candidates[Random.Range(0, candidates.Count)];
            lastPlayerPattern = picked;
            return picked;
        }

        // 스킬 턴: 1) 스킬 전용 채보를 친다 — 판정만 하고 데미지는 아직 넣지 않는다(놓친 노드는 0).
        // 2) 채보가 끝나면 시전 모션 + 스킬 이펙트 마무리 연출을 틀고, 맞힌 노드 수만큼 연출 시간에 걸쳐
        //    연타를 넣는다. 노드당 위력 = 일반 공격 한 턴(평균 노드 수 x 1타)의 Power배를 채보 노드 수로 나눈 값
        //    x 그 노드의 판정 배수 — 그래서 많이, 정확히 맞힐수록 총 데미지가 비례해서 커진다.
        private IEnumerator SkillTurn(int skillIndex)
        {
            var skill = Skills[skillIndex];
            var notes = BuildChartNotes(skill.Chart);
            float perNodeMultiplier = skill.Power * AverageNoteCount(isBossBattle ? BossCharts : NormalCharts) / notes.Count;
            var hitMultipliers = new List<float>();

            phaseBannerText.color = SkillNoteColor;
            phaseBannerText.text = skill.Name + " — 준비!";

            yield return RunChart(notes, SkillNoteColor, okWindow, (note, hit, diff) =>
            {
                if (!hit)
                {
                    ShowJudgment(judgmentMiss);
                    return;
                }
                float rhythm;
                if (diff <= perfectWindow)
                {
                    rhythm = player.TalentPerfectMultiplier(PerfectMultiplier); // 축복 특성 완벽주의
                    ShowJudgment(judgmentPerfect);
                }
                else if (diff <= goodWindow)
                {
                    rhythm = GoodMultiplier;
                    ShowJudgment(judgmentExcellent);
                }
                else
                {
                    rhythm = OkMultiplier;
                    ShowJudgment(judgmentGood);
                }
                hitMultipliers.Add(rhythm * perNodeMultiplier);
            });

            if (BattleEnded()) yield break;

            // 튜토리얼 몬스터는 스킬을 쓰면 반드시 맞고 쓰러진다: 노드를 다 놓쳐도 실패 없이 한 타는 들어간다.
            bool tutorialFinish = monster.GetComponent<TutorialSkillFinish>() != null;
            if (tutorialFinish && hitMultipliers.Count == 0) hitMultipliers.Add(OkMultiplier * perNodeMultiplier);

            if (hitMultipliers.Count == 0)
            {
                phaseBannerText.text = "";
                ShowResult(skill.Name + " 실패...", MissColor);
                yield return new WaitForSecondsRealtime(SkillFailPause);
                yield break;
            }

            // 궁극기는 채보를 다 친 뒤, 시전 모션이 나오기 전에 화면이 암전됐다가 눈을 번쩍 뜨며
            // 깜빡이는 연출을 한 번 먼저 보여준다.
            if (skill.IsUltimate) yield return PlayUltimateAwakening();
            if (BattleEnded()) yield break;

            // 마무리 연출
            phaseBannerText.text = $"{skill.Name}! ({hitMultipliers.Count}/{notes.Count})";
            PlayPlayerOneShot(skillCastFrames[skillIndex], skill.FinisherDuration, Color.white, skill.CastCharacterHeightPx);
            yield return new WaitForSecondsRealtime(SkillFinisherLeadIn);
            if (BattleEnded()) yield break;
            StartSkillEffect(skillIndex);

            float hitSpan = Mathf.Max(0.1f, skill.FinisherDuration - SkillFinisherLeadIn);
            float interval = hitSpan / hitMultipliers.Count;
            foreach (var multiplier in hitMultipliers)
            {
                player.DealDamage(monster, multiplier, true);
                RefreshHpBars();
                if (BattleEnded()) yield break;
                yield return new WaitForSecondsRealtime(interval);
                if (BattleEnded()) yield break;
            }

            // 튜토리얼 몬스터: 연타를 다 맞는 동안은 체력 1로 버티게 두었다가(SurviveLethalHit), 마지막 타가 끝나면
            // 남은 체력을 한 번에 깎아 확정으로 쓰러뜨린다 — 피해량이 모자라도, 첫 타에 연출이 끊기지도 않게.
            if (tutorialFinish && !monster.IsDead)
            {
                monster.SurviveLethalHit = false;
                monster.TakeDamage(monster.CurrentHp, true);
                RefreshHpBars();
                if (BattleEnded()) yield break;
            }

            yield return new WaitForSecondsRealtime(SkillFinisherTail);
            StopSkillEffect();
            phaseBannerText.text = "";
        }

        // 모션이 타격 지점까지 진행된 뒤에 데미지를 적용한다 — 안 그러면 일격사일 때 공격
        // 모션이 뜨기도 전에 전투 화면이 바로 닫혀서 몬스터가 이유 없이 사라지는 것처럼 보였다.
        private IEnumerator DealDamageAfterImpact(float attackMultiplier, bool isSkill, int serial)
        {
            yield return new WaitForSecondsRealtime(AttackImpactDelay);
            if (serial != battleSerial || BattleEnded()) yield break;
            if (!isSkill) PlayBasicSlash(); // 스킬은 채보 내내 스킬 이펙트가 따로 돈다
            player.DealDamage(monster, attackMultiplier, isSkill);
            RefreshHpBars();
        }

        // 내 턴 시작 시 한 번, 공격/스킬/아이템/도주 중 고르게 한다. ←/→로 선택을 옮기고 Z/Enter로
        // 확정한다. 도망은 여기서 도주를 골라야만 할 수 있다.
        // 스킬을 고르면 스킬 목록이 뜨고, 거기서 취소하면 이 창으로 돌아온다.
        // 아이템을 고르면 회복 포션을 바로 마시고 확정된다. 포션이 없거나 체력이 가득하면 안내만 뜨고 창이 그대로 열려 있는다.
        private IEnumerator ShowActionSelect()
        {
            chosenSkill = -1;
            actionSelectPotion = false;
            actionSelectIndex = ActionAttack;
            actionMessageText.text = "";
            RefreshActionCursor();
            myTurnTitleText.text = "";
            myTurnFrame.SetActive(true);
            actionSelectPanel.SetActive(true);
            ShowFleeLock();

            bool confirmed = false;
            while (!confirmed)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.leftArrowKey.wasPressedThisFrame)
                    {
                        actionSelectIndex = (actionSelectIndex + 3) % 4;
                        actionMessageText.text = ActionHint();
                        RefreshActionCursor();
                    }
                    else if (keyboard.rightArrowKey.wasPressedThisFrame)
                    {
                        actionSelectIndex = (actionSelectIndex + 1) % 4;
                        actionMessageText.text = ActionHint();
                        RefreshActionCursor();
                    }
                    else if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    {
                        if (actionSelectIndex == ActionItem)
                        {
                            string refusal = TryDrinkPotion();
                            if (refusal == null) confirmed = true;
                            else actionMessageText.text = refusal;
                        }
                        else if (actionSelectIndex == ActionFlee && fleeLocked)
                        {
                            actionMessageText.text = "루나벨의 핏빛 사슬이 도주를 막고 있다!";
                        }
                        else if (actionSelectIndex == ActionSkill)
                        {
                            actionSelectPanel.SetActive(false);
                            yield return ShowSkillSelect();
                            if (BattleEnded()) yield break;
                            if (chosenSkill >= 0)
                            {
                                confirmed = true;
                            }
                            else
                            {
                                actionMessageText.text = "";
                                myTurnTitleText.text = "";
                                actionSelectPanel.SetActive(true);
                            }
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
            myTurnFrame.SetActive(false);
            actionSelectFlee = actionSelectIndex == ActionFlee;
        }

        // 커서가 옮겨 간 칸의 안내 문구(아이템 = 포션 개수, 잠긴 도주 = 사용 불가).
        private string ActionHint()
        {
            if (actionSelectIndex == ActionItem) return PotionHint();
            if (actionSelectIndex == ActionFlee && fleeLocked) return "이 싸움에서는 도망칠 수 없다.";
            return "";
        }

        // 도주가 잠긴 전투면 도주 칸 위에 잠금 그림을 띄운다. 그 전투의 첫 행동 선택에서만 사슬이 감기는 애니메이션을 튼다.
        private void ShowFleeLock()
        {
            bool show = fleeLocked && fleeLockFrames[FleeLockFrameCount - 1] != null;
            fleeLockImage.gameObject.SetActive(show);
            if (!show) return;
            if (fleeLockAnimated)
            {
                fleeLockImage.texture = fleeLockFrames[FleeLockFrameCount - 1];
                return;
            }
            fleeLockAnimated = true;
            StartCoroutine(FleeLockRoutine());
        }

        private IEnumerator PlayLunabelSplash()
        {
            lunabelSplashImage.gameObject.SetActive(true);
            foreach (var frame in lunabelSplashFrames)
            {
                lunabelSplashImage.sprite = frame;
                yield return new WaitForSecondsRealtime(LunabelSplashFrameInterval);
            }
            yield return new WaitForSecondsRealtime(LunabelSplashHold);
            lunabelSplashImage.gameObject.SetActive(false);
        }

        // 스킬 목록. 확정하면 필살 게이지를 쓰고 chosenSkill에 담아 돌아가고, 취소하면 chosenSkill=-1로 돌아간다.
        // 게이지가 모자란 스킬은 회색으로 보이고 골라도 안내만 뜬다.
        private IEnumerator ShowSkillSelect()
        {
            chosenSkill = -1;
            skillMessageText.text = "";
            RefreshSkillRows();
            myTurnTitleText.text = "스킬";
            skillSelectPanel.SetActive(true);

            // 행동 선택 창을 확정한 Z가 같은 프레임에 여기서도 눌린 걸로 읽히지 않게 한 프레임 넘긴다.
            yield return null;

            while (true)
            {
                if (BattleEnded()) yield break;
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.upArrowKey.wasPressedThisFrame)
                    {
                        skillSelectIndex = (skillSelectIndex + Skills.Length - 1) % Skills.Length;
                        skillMessageText.text = "";
                        RefreshSkillRows();
                    }
                    else if (keyboard.downArrowKey.wasPressedThisFrame)
                    {
                        skillSelectIndex = (skillSelectIndex + 1) % Skills.Length;
                        skillMessageText.text = "";
                        RefreshSkillRows();
                    }
                    else if (keyboard.xKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
                    {
                        break;
                    }
                    else if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    {
                        if (skillGauge >= Skills[skillSelectIndex].GaugeCost)
                        {
                            skillGauge -= Skills[skillSelectIndex].GaugeCost;
                            chosenSkill = skillSelectIndex;
                            RefreshHpBars();
                            break;
                        }
                        skillMessageText.text = "필살 게이지가 부족해요";
                    }
                }
                yield return null;
            }

            skillSelectPanel.SetActive(false);
            // 스킬 목록을 닫은 키가 바로 행동 선택 창(또는 채보)에서 다시 읽히지 않게 한 프레임 넘긴다.
            yield return null;
        }

        private void RefreshSkillRows()
        {
            for (int i = 0; i < Skills.Length; i++)
            {
                var skill = Skills[i];
                bool selected = i == skillSelectIndex;
                bool affordable = skillGauge >= skill.GaugeCost;
                skillRowTexts[i].text = $"{(selected ? "> " : "   ")}[{skill.Key}] {skill.Name}   게이지 {skill.GaugeCost:0}";
                skillRowTexts[i].color = !affordable ? new Color(0.5f, 0.5f, 0.55f)
                    : selected ? SkillNoteColor : Color.white;
            }
        }

        private void StartSkillEffect(int skillIndex)
        {
            StopSkillEffect();
            var frames = skillEffectFrames[skillIndex];
            if (frames == null || frames.Length == 0) return;
            skillEffectRoutine = StartCoroutine(SkillEffectRoutine(frames));
        }

        private void StopSkillEffect()
        {
            if (skillEffectRoutine != null)
            {
                StopCoroutine(skillEffectRoutine);
                skillEffectRoutine = null;
            }
            if (skillEffectImage != null) skillEffectImage.gameObject.SetActive(false);
        }

        private IEnumerator SkillEffectRoutine(Sprite[] frames)
        {
            skillEffectImage.gameObject.SetActive(true);
            int index = 0;
            while (true)
            {
                skillEffectImage.sprite = frames[index];
                index = (index + 1) % frames.Length;
                yield return new WaitForSecondsRealtime(SkillEffectFrameInterval);
            }
        }

        private Inventory PlayerInventory => player != null ? player.GetComponent<Inventory>() : null;

        private string PotionHint()
        {
            var inventory = PlayerInventory;
            int count = inventory != null ? inventory.GetCount(ItemDatabase.Potion.id) : 0;
            return $"{ItemDatabase.Potion.itemName} {count}개 (체력 +{Mathf.RoundToInt(ItemDatabase.Potion.healAmount)}, 턴 종료)";
        }

        // 포션을 하나 마셔 체력을 회복한다. 마셨으면 null, 못 마시면 그 이유(선택 창에 띄울 안내)를 돌려준다.
        private string TryDrinkPotion()
        {
            if (isMinigame) return "리듬 대결에서는 아이템을 쓸 수 없어요";
            var potion = ItemDatabase.Potion;
            var inventory = PlayerInventory;
            if (inventory == null || inventory.GetCount(potion.id) <= 0) return "포션이 없어요";
            if (player.CurrentHp >= player.MaxHp) return "체력이 이미 가득 찼어요";
            if (!inventory.TryRemoveItem(potion, 1)) return "포션이 없어요";

            float before = player.CurrentHp;
            player.Heal(potion.healAmount * player.TalentPotionMultiplier); // 축복 특성 성수
            float healed = player.CurrentHp - before;
            actionSelectPotion = true;
            ShowResult($"체력 +{Mathf.RoundToInt(healed)}", PerfectColor);
            RefreshHpBars();
            return null;
        }

        private void RefreshActionCursor()
        {
            actionCursor.anchorMin = actionCursor.anchorMax = new Vector2(ActionSlotX[actionSelectIndex], 0.30f);
        }

        // 공격 노드를 맞혔을 때 왼쪽 초상화에 Z 공격 모션을 한 번 재생한다. 턴 진행을 막지 않도록
        // StartCoroutine으로 띄워두기만 하고 끝날 때까지 기다리지 않는다.
        private void TriggerPlayerAttackAnim() => PlayPlayerOneShot(playerAttackFrames, PlayerAttackAnimDuration, Color.white);

        // 적 턴에 노드를 놓쳐 맞을 때마다 피격 모션을 처음부터 다시 재생한다(연타로 맞으면 계속 휘청인다).
        private void TriggerPlayerHitAnim() => PlayPlayerOneShot(playerHitFrames, PlayerHitAnimDuration, PlayerHitTint);

        // 적 턴에 노드를 막을 때마다 가드 모션을 처음부터 다시 재생한다.
        private void TriggerPlayerGuardAnim() => PlayPlayerOneShot(playerGuardFrames, PlayerGuardAnimDuration, Color.white);

        private void PlayPlayerOneShot(Sprite[] frames, float duration, Color tint, float characterHeightPx = 0f)
        {
            if (frames.Length == 0) return;
            if (attackAnimRoutine != null) StopCoroutine(attackAnimRoutine);
            RestorePlayerPortraitLayout();
            attackAnimRoutine = StartCoroutine(PlayPlayerOneShotRoutine(frames, duration, tint, characterHeightPx));
        }

        private IEnumerator PlayPlayerOneShotRoutine(Sprite[] frames, float duration, Color tint, float characterHeightPx)
        {
            isPlayingAttackAnim = true;
            playerPortrait.color = tint;
            float frameDuration = duration / frames.Length;
            foreach (var frame in frames)
            {
                playerPortrait.sprite = frame;
                if (characterHeightPx > 0f) ApplyLargeCastLayout(frame, characterHeightPx);
                yield return new WaitForSecondsRealtime(frameDuration);
            }
            RestorePlayerPortraitLayout();
            playerPortrait.color = Color.white;
            isPlayingAttackAnim = false;
            idleFrameTimer = 0f; // 애니메이션이 끝나면 바로 다음 프레임부터 대기 애니메이션이 매끈하게 이어진다.
            attackAnimRoutine = null;
        }

        // 큰 시전 모션(필살기)용 배치: 초상화 칸 아래쪽 가운데에 발을 붙이고, 캐릭터 키(characterHeightPx)가
        // 칸 높이(= 대기 모션 키)가 되는 배율로 캔버스 전체를 그린다. 낫/검기는 칸 밖으로 넘쳐 보인다.
        private bool playerPortraitLayoutSaved;
        private bool playerPortraitLayoutChanged;
        private Vector2 playerPortraitAnchorMin, playerPortraitAnchorMax, playerPortraitPivot, playerPortraitOffsetMin, playerPortraitOffsetMax;

        private void ApplyLargeCastLayout(Sprite frame, float characterHeightPx)
        {
            var rect = playerPortrait.rectTransform;
            if (!playerPortraitLayoutSaved)
            {
                playerPortraitAnchorMin = rect.anchorMin;
                playerPortraitAnchorMax = rect.anchorMax;
                playerPortraitPivot = rect.pivot;
                playerPortraitOffsetMin = rect.offsetMin;
                playerPortraitOffsetMax = rect.offsetMax;
                playerPortraitLayoutSaved = true;
            }

            float boxHeight = playerPortraitLayoutChanged ? playerPortraitBoxHeight : rect.rect.height;
            playerPortraitBoxHeight = boxHeight;
            playerPortraitLayoutChanged = true;

            float scale = boxHeight / characterHeightPx;
            var bottomCenter = new Vector2((playerPortraitAnchorMin.x + playerPortraitAnchorMax.x) * 0.5f, playerPortraitAnchorMin.y);
            playerPortrait.preserveAspect = false;
            rect.anchorMin = bottomCenter;
            rect.anchorMax = bottomCenter;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = frame.rect.size * scale;
        }

        private float playerPortraitBoxHeight;

        private void RestorePlayerPortraitLayout()
        {
            if (!playerPortraitLayoutChanged) return;
            var rect = playerPortrait.rectTransform;
            rect.anchorMin = playerPortraitAnchorMin;
            rect.anchorMax = playerPortraitAnchorMax;
            rect.pivot = playerPortraitPivot;
            rect.offsetMin = playerPortraitOffsetMin;
            rect.offsetMax = playerPortraitOffsetMax;
            playerPortrait.preserveAspect = true;
            playerPortraitLayoutChanged = false;
        }

        // 궁극기 각성: 화면이 검게 암전된 뒤, EyeFlash 프레임을 화면 가득 보여주며 몇 번 깜빡인다
        // (검은 화면 ↔ 눈 뜨는 그림이 번갈아 뜨고, 깜빡일 때마다 더 떠진 눈이 드러나게). 마지막엔 다 뜬 눈을
        // 화면 가득 잠깐 붙잡아 보여준 뒤, 암전을 걷으며 원래 전투 화면(작은 초상화)으로 돌아간다.
        private const float UltimateBlackoutDuration = 0.2f;
        private const int UltimateFlickerPulses = 4;
        private const float UltimateFlickerPulseDuration = 0.1f;
        private const float UltimateAwakeningHold = 0.4f;

        private IEnumerator PlayUltimateAwakening()
        {
            yield return FadeUltimateOverlay(0f, 1f, UltimateBlackoutDuration);

            ultimateEyeFullscreenImage.sprite = eyeFlashFrames[0];
            ultimateEyeFullscreenImage.gameObject.SetActive(true);

            for (int i = 1; i <= UltimateFlickerPulses; i++)
            {
                int frameIndex = Mathf.Min(eyeFlashFrames.Length - 1, i * eyeFlashFrames.Length / UltimateFlickerPulses);
                ultimateEyeFullscreenImage.sprite = eyeFlashFrames[frameIndex];
                ultimateEyeFullscreenImage.gameObject.SetActive(true);
                yield return new WaitForSecondsRealtime(UltimateFlickerPulseDuration * 0.5f);
                if (i < UltimateFlickerPulses)
                {
                    ultimateEyeFullscreenImage.gameObject.SetActive(false);
                    yield return new WaitForSecondsRealtime(UltimateFlickerPulseDuration * 0.5f);
                }
            }

            yield return new WaitForSecondsRealtime(UltimateAwakeningHold);

            // 작은 초상화도 눈을 다 뜬 프레임으로 맞춰 둬서, 화면 가득 찬 그림을 치우고 이어지는
            // 시전 모션으로 넘어갈 때 눈이 도로 감긴 것처럼 보이지 않게 한다.
            ultimateEyeFullscreenImage.gameObject.SetActive(false);
            playerPortrait.color = Color.white;
            playerPortrait.sprite = eyeFlashFrames[eyeFlashFrames.Length - 1];

            yield return FadeUltimateOverlay(1f, 0f, UltimateBlackoutDuration);
        }

        private IEnumerator FadeUltimateOverlay(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                ultimateFlashOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            ultimateFlashOverlay.color = new Color(0f, 0f, 0f, to);
        }
    }
}

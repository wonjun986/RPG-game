using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Characters;
using Aethoria.Monsters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 황금 모래룡 등장 연출(마지막 폐허 맵). 모래룡은 처음엔 땅속에 숨어 있어 아무것도 보이지 않는다.
    // 네크로시아가 일정 거리까지 걸어오면: 조작을 막고 땅이 울린다 → 카메라가 빈 땅을 클로즈업 → 모래가 터지며
    // 모래룡이 땅 밑에서 솟아오른다(지하 분출 8프레임, Emerge) → 포효(포효 6프레임, Roar — 입을 크게 벌리는 순간
    // 화면이 크게 흔들리고 입에서 모래빛 충격파 고리가 연달아 퍼지며 화면이 번쩍인다 + "크아아아앙!!") →
    // 카메라가 네크로시아를 클로즈업 → 네크로시아 대사 → 모래룡이 땅을 내려찍어 출구(뒤쪽 포탈) 밑에서 꼬리를 솟구쳐 출구를
    // 부순다(카메라가 출구로 넘어가 보여 줌) → 다시 네크로시아 클로즈업 → 대사 → 화면이 원래대로 돌아오며 모래룡을 향해 대시 → 전투 시작.
    // 이 연출을 이미 본 세이브(두 번째 만남부터)는 Esc로 건너뛰면 모래룡이 바로 나타나고(출구는 부서진 채로) 곧장 전투가 열린다.
    public class SandDragonIntroCutscene : MonoBehaviour
    {
        // 모래룡과 이만큼 가까워지면 연출이 시작된다.
        private const float TriggerDistance = 10f;

        private const float RumbleDuration = 1.0f;
        private const float RumbleMagnitude = 0.06f;
        private const float CameraPanSmoothTime = 0.5f;
        // 모래룡을 두 배로 키운 뒤로는 솟아오르는 모습(키 약 7유닛)이 다 들어오도록, 기본 화면(4)보다 살짝 넓게 잡는다.
        // 카메라는 바닥 높이를 화면에서 그대로 두므로 넓히면 위쪽이 더 보인다.
        private const float DragonCloseUpSize = 5.2f;
        private const float PlayerCloseUpSize = 2.2f;
        private const float ZoomDuration = 0.7f;

        // 땅 밑에서 솟아오르기(Emerge 8프레임). 모래가 터지는 동안 땅도 같이 흔들린다.
        private const string EmergePath = "Art/Monsters/SandDragon/Emerge";
        private const float EmergeDuration = 1.6f;
        private const float EmergeShakeMagnitude = 0.12f;

        // 포효(Roar 6프레임). 입을 크게 벌리는 4번째 프레임쯤(RoarImpactTiming)에 충격파가 터진다.
        // 전투 중 포효(SandDragonAI.RoarRoutine)도 같은 그림을 쓰고, 입을 벌리는 순간 BattleRoarImpact로 효과만 받아 띄운다.
        private const string RoarPath = SandDragonAI.RoarPath;
        private const float BattleRoarShakeDuration = 1.0f;
        private const float BattleRoarShakeMagnitude = 0.3f;
        private const float RoarDuration = 1.3f;
        private const float RoarImpactTiming = 0.45f;
        private const float RoarHold = 1.4f;             // 충격 뒤 흔들림이 가라앉을 때까지
        private const float RoarShakeDuration = 1.6f;
        private const float RoarShakeMagnitude = 0.55f;   // 땅울림(0.06)의 약 9배 — 화면이 크게 요동친다
        private const float RoarZoomPunch = 0.82f;        // 충격 순간 클로즈업을 이 비율까지 확 당겼다가 되돌린다
        private const int ShockwaveRings = 4;
        private const float ShockwaveInterval = 0.14f;
        private const float ShockwaveDuration = 0.75f;
        private const float ShockwaveMaxSize = 11f;       // 고리 지름(유닛)
        private static readonly Color ShockwaveColor = new Color(1f, 0.86f, 0.55f, 0.85f);
        private const float FlashAlpha = 0.45f;
        private const float FlashDuration = 0.35f;
        private const string RoarText = "크아아아앙!!";

        private const float DashDurationPerUnit = 0.03f;
        private const float BattleGap = 4.0f; // 모래룡 몸 앞에서 멈추는 거리

        private static readonly string[] NecrosiaLines =
        {
            "...땅 밑에서 깨어난 건가. 폐허의 오래된 기록을 지켜 온 수호룡.",
            "미안하지만 비켜 줘야겠다. 그 문 너머의 기록, 내가 확인해야 하니까.",
        };

        // 대사가 끝나면 모래룡이 땅을 내려찍어, 네크로시아가 들어온 출구(뒤쪽 포탈) 밑에서 꼬리를 솟구쳐 출구를 부순다.
        // 카메라가 출구로 넘어가 부서지는 모습을 보여 주고, 네크로시아에게 돌아와 한마디 한 뒤 전투가 열린다.
        private const float ExitWarnTime = 0.9f;           // 출구 밑 모래가 들썩이는 시간(그 사이 카메라가 출구로 넘어간다)
        private const float ExitCameraSmoothTime = 0.3f;
        private const float ExitBreakHold = 1.3f;          // 부서진 뒤 잠깐 보여 주는 시간
        private const float ExitShatterDuration = 0.35f;
        private const float ExitShakeDuration = 0.6f;
        private const float ExitShakeMagnitude = 0.3f;

        private static readonly string[] AfterExitLines =
        {
            "...퇴로를 끊었나.",
            "상관없다. 어차피 물러설 생각은 없었으니까.",
        };

        private Portal exitPortal;
        private bool exitBroken;

        // 보스 맵의 뒤쪽 포탈(유일한 출구). 모래룡을 만든 뒤에 생기므로 GameBootstrap이 따로 알려 준다.
        public void SetExitPortal(Portal portal) => exitPortal = portal;
        private const string PlayerNextHint = "   <color=#B98CE8>[Z]</color>";
        private const string PlayerSkipHint = "   <color=#B98CE8>[Esc] 스킵</color>";

        private SandDragonAI dragon;
        private Monster dragonMonster;
        private SpriteRenderer dragonRenderer;
        private MonsterAttackSpriteAnimator dragonAttack;
        private Character player;
        private CharacterMovement2D playerMovement;
        private NpcDialogueUI dialogueUI;
        private CameraFollow cameraFollow;
        private System.Action onReveal;
        private System.Action onShown;
        private bool skippable;
        private bool started;
        private bool revealed;
        private bool finished;

        private Vector3 restPosition;

        public void Initialize(SandDragonAI dragonAI, Character targetPlayer, NpcDialogueUI dialogue,
            bool skippable, System.Action onShown, System.Action onReveal)
        {
            dragon = dragonAI;
            dragonMonster = dragonAI.GetComponent<Monster>();
            dragonRenderer = dragonAI.GetComponent<SpriteRenderer>();
            dragonAttack = dragonAI.GetComponent<MonsterAttackSpriteAnimator>();
            player = targetPlayer;
            playerMovement = player != null ? player.GetComponent<CharacterMovement2D>() : null;
            dialogueUI = dialogue;
            this.skippable = skippable;
            this.onShown = onShown;
            this.onReveal = onReveal;

            if (dragonAttack != null)
            {
                dragonAttack.AddClip("Emerge", EmergePath);
                dragonAttack.AddClip("Roar", RoarPath);
            }

            dragon.OnRoarImpact += BattleRoarImpact;
            dragon.HoldForIntro(true);
            restPosition = dragon.transform.position;
            SetDragonVisible(false);
        }

        private void Start()
        {
            var cam = Camera.main;
            cameraFollow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            StartCoroutine(HideHealthBarOnceBuilt());
        }

        // 머리 위 체력바(MonsterHealthBar)는 Start에서 만들어져 Initialize 때는 아직 없다. 한 프레임 뒤에 다시 숨긴다.
        private IEnumerator HideHealthBarOnceBuilt()
        {
            yield return null;
            if (!revealed) SetDragonVisible(dragonRenderer != null && dragonRenderer.enabled, showHealthBar: false);
        }

        private void Update()
        {
            if (started || player == null || dragon == null || player.IsDead) return;
            if (Mathf.Abs(dragon.transform.position.x - player.transform.position.x) > TriggerDistance) return;
            started = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (playerMovement != null)
            {
                playerMovement.SetCutsceneLocked(true);
                playerMovement.Face(Vector2.right);
            }
            onShown?.Invoke();

            // 1) 땅이 울린다(아직 아무것도 없다).
            if (cameraFollow != null) cameraFollow.Shake(RumbleDuration, RumbleMagnitude);
            yield return Wait(RumbleDuration * 0.6f);
            if (skipped) { yield return SkipToBattle(); yield break; }

            // 2) 빈 땅 클로즈업 → 모래가 터지며 모래룡이 솟아오른다.
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(dragon.transform, CameraPanSmoothTime);
                cameraFollow.ZoomTo(DragonCloseUpSize, ZoomDuration);
            }
            yield return Wait(ZoomDuration + 0.2f);
            if (skipped) { yield return SkipToBattle(); yield break; }

            SetDragonVisible(true, showHealthBar: false);
            if (dragonAttack != null) dragonAttack.PlayClip("Emerge", EmergeDuration);
            if (cameraFollow != null) cameraFollow.Shake(EmergeDuration, EmergeShakeMagnitude);
            yield return Wait(EmergeDuration);
            if (skipped) { yield return SkipToBattle(); yield break; }

            // 3) 포효: 입을 크게 벌리는 순간 충격파.
            if (dragonAttack != null) dragonAttack.PlayClip("Roar", RoarDuration);
            yield return Wait(RoarDuration * RoarImpactTiming);
            if (skipped) { yield return SkipToBattle(); yield break; }
            RoarImpact();
            var roar = CreateRoarText();
            yield return Wait(RoarHold);
            if (roar != null) Destroy(roar);
            Reveal();
            if (skipped) { yield return SkipToBattle(); yield break; }

            // 4) 네크로시아 클로즈업 → 대사.
            if (cameraFollow != null && player != null)
            {
                cameraFollow.SetTarget(player.transform, CameraPanSmoothTime);
                cameraFollow.ZoomTo(PlayerCloseUpSize, ZoomDuration);
            }
            yield return Wait(ZoomDuration + 0.2f);
            if (skipped) { yield return SkipToBattle(); yield break; }

            yield return Monologue(NecrosiaLines);
            if (skipped) { yield return SkipToBattle(); yield break; }

            // 5) 모래룡이 꼬리로 출구를 부순다 → 다시 네크로시아 클로즈업 → 대사.
            if (exitPortal != null && dragon != null)
            {
                yield return SmashExit();
                if (skipped) { yield return SkipToBattle(); yield break; }

                if (cameraFollow != null && player != null)
                {
                    cameraFollow.SetTarget(player.transform, CameraPanSmoothTime);
                    cameraFollow.ZoomTo(PlayerCloseUpSize, ZoomDuration);
                }
                yield return Wait(ZoomDuration + 0.2f);
                if (skipped) { yield return SkipToBattle(); yield break; }

                yield return Monologue(AfterExitLines);
                if (skipped) { yield return SkipToBattle(); yield break; }
            }

            // 6) 화면을 되돌리며 모래룡을 향해 대시 → 전투.
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(player.transform, 0.12f);
                cameraFollow.ResetZoom(0.35f);
            }
            yield return DashIntoBattle();
        }

        private bool skipped;

        private IEnumerator Monologue(string[] lines)
        {
            foreach (var line in lines)
            {
                dialogueUI?.ShowNecrosia(line + (skippable ? PlayerSkipHint : "") + PlayerNextHint);
                yield return null; // 대사가 바뀐 그 프레임의 입력으로 바로 넘어가지 않게
                while (!AdvancePressed())
                {
                    if (SkipPressed()) { skipped = true; break; }
                    yield return null;
                }
                if (skipped) break;
            }
            dialogueUI?.Hide();
        }

        // 화면을 넓혀 모래룡을 비춘 채 땅을 내려찍게 하고, 앞발이 닿는 순간 카메라를 출구로 넘긴다.
        // 출구 밑 모래가 들썩이다 꼬리가 솟는 순간 출구가 부서진다.
        private IEnumerator SmashExit()
        {
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(dragon.transform, CameraPanSmoothTime);
                cameraFollow.ResetZoom(ZoomDuration);
            }
            yield return Wait(ZoomDuration);
            if (skipped) yield break;

            Vector2 exitGround = new Vector2(ExitVisualX(), dragon.transform.position.y);
            float facing = exitGround.x < dragon.transform.position.x ? -1f : 1f;
            yield return Wait(dragon.PlayCutsceneSlam(facing));
            if (skipped) yield break;

            if (cameraFollow != null)
            {
                cameraFollow.Shake(0.4f, 0.15f);
                cameraFollow.SetTarget(exitPortal.transform, ExitCameraSmoothTime);
            }
            dragon.PlayCutsceneTail(exitGround, facing, ExitWarnTime, () => BreakExit(instant: false));
            while (!exitBroken && !skipped)
            {
                if (SkipPressed()) skipped = true;
                yield return null;
            }
            if (skipped) yield break;
            yield return Wait(ExitBreakHold);
        }

        // 포탈 그림(테두리/소용돌이)은 콜라이더보다 맵 안쪽으로 물려 그려지므로, 그림 가운데에 꼬리를 세운다.
        private float ExitVisualX()
        {
            var renderers = exitPortal.GetComponentsInChildren<SpriteRenderer>();
            if (renderers.Length == 0) return exitPortal.transform.position.x;
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds.center.x;
        }

        // 출구를 부순다: 더는 밟아도 넘어가지 않게 판정을 끄고, 모래빛 충격파와 함께 오그라들며 사라진다.
        // instant면(건너뛰기) 효과 없이 바로 없앤다. 두 번 불려도 안전하다.
        private void BreakExit(bool instant)
        {
            if (exitBroken || exitPortal == null) { exitBroken = true; return; }
            exitBroken = true;
            foreach (var c in exitPortal.GetComponentsInChildren<Collider2D>()) c.enabled = false;
            var portalGO = exitPortal.gameObject;
            if (instant)
            {
                Destroy(portalGO);
                return;
            }

            var renderers = portalGO.GetComponentsInChildren<SpriteRenderer>();
            Vector3 center = portalGO.transform.position;
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                center = bounds.center;
            }
            if (cameraFollow != null) cameraFollow.Shake(ExitShakeDuration, ExitShakeMagnitude);
            ShockwaveBurst.Play(this, center, portalGO.transform.parent, new ShockwaveBurst.Settings
            {
                rings = 3,
                interval = 0.1f,
                duration = 0.6f,
                maxSize = 6f,
                squash = 0.8f,
                ringColor = ShockwaveColor,
                flashColor = new Color(1f, 0.9f, 0.65f, 0.3f),
                flashDuration = 0.25f,
                sortingOrder = 55,
            });
            StartCoroutine(ShatterPortal(portalGO, renderers));
        }

        private IEnumerator ShatterPortal(GameObject portalGO, SpriteRenderer[] renderers)
        {
            Vector3 startScale = portalGO.transform.localScale;
            var startColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) startColors[i] = renderers[i].color;
            for (float t = 0f; t < ExitShatterDuration && portalGO != null; t += Time.deltaTime)
            {
                float k = t / ExitShatterDuration;
                // 옆으로 살짝 부풀었다가 위아래로 찌그러지며 오그라든다.
                portalGO.transform.localScale = new Vector3(startScale.x * (1f + 0.25f * Mathf.Sin(k * Mathf.PI)) * (1f - k * 0.6f),
                    startScale.y * (1f - k), startScale.z);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    Color c = startColors[i];
                    renderers[i].color = new Color(c.r, c.g, c.b, c.a * (1f - k));
                }
                yield return null;
            }
            if (portalGO != null) Destroy(portalGO);
        }

        private IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !skipped; t += Time.deltaTime)
            {
                if (SkipPressed()) skipped = true;
                yield return null;
            }
        }

        private IEnumerator DashIntoBattle()
        {
            if (player == null || dragon == null) { FinishAndBegin(); yield break; }

            if (playerMovement != null) playerMovement.Face(Vector2.right);
            float distance = dragon.transform.position.x - BattleGap - player.transform.position.x;
            var dash = player.GetComponent<CharacterSkillDash>();
            if (dash != null && distance > 0.5f)
            {
                float duration = Mathf.Clamp(distance * DashDurationPerUnit, 0.2f, 0.45f);
                var routine = dash.PlayCutsceneDash(distance, duration);
                if (routine != null) yield return routine;
            }
            FinishAndBegin();
        }

        // 건너뛰기: 남은 연출을 정리하고 모래룡을 제자리에 드러낸 뒤 바로 대시해서 전투를 연다.
        private IEnumerator SkipToBattle()
        {
            dialogueUI?.Hide();
            if (dragonAttack != null) dragonAttack.Stop();
            Reveal();
            BreakExit(instant: true); // 건너뛰어도 출구는 부서진 채로 전투가 열린다
            if (cameraFollow != null && player != null)
            {
                cameraFollow.SetTarget(player.transform, 0.12f);
                cameraFollow.ResetZoom(0.25f);
            }
            yield return DashIntoBattle();
        }

        private void Reveal()
        {
            if (revealed) return;
            revealed = true;
            SetDragonVisible(true, showHealthBar: true);
            onReveal?.Invoke();
        }

        private void FinishAndBegin()
        {
            Finish();
            if (dragon != null) dragon.HoldForIntro(false);
        }

        // ---- 보이기/가리기 ----

        private void SetDragonVisible(bool visible, bool showHealthBar = false)
        {
            if (dragonRenderer != null) dragonRenderer.enabled = visible;
            foreach (Transform child in dragon.transform)
            {
                if (child.name.StartsWith("HealthBar")) child.gameObject.SetActive(visible && showHealthBar);
            }
        }

        // 포효 충격: 화면을 크게 흔들고, 클로즈업을 확 당겼다 되돌리고, 화면을 모래빛으로 번쩍이고,
        // 입에서 충격파 고리를 연달아 퍼뜨린다.
        private void RoarImpact()
        {
            if (cameraFollow != null)
            {
                cameraFollow.Shake(RoarShakeDuration, RoarShakeMagnitude);
                StartCoroutine(ZoomPunch());
            }
            Shockwaves();
        }

        // 전투 중 포효: 클로즈업은 하지 않고, 화면을 흔들고 번쩍이고 충격파 고리와 "크아아아앙!!"만 띄운다.
        private void BattleRoarImpact()
        {
            if (cameraFollow != null) cameraFollow.Shake(BattleRoarShakeDuration, BattleRoarShakeMagnitude);
            Shockwaves();
            StartCoroutine(FadeRoarText(CreateRoarText()));
        }

        private IEnumerator FadeRoarText(GameObject textGO)
        {
            var text = textGO.GetComponent<TextMesh>();
            Color color = text.color;
            const float duration = 1.2f;
            for (float t = 0f; t < duration && textGO != null; t += Time.deltaTime)
            {
                text.color = new Color(color.r, color.g, color.b, 1f - t / duration);
                textGO.transform.position += Vector3.up * (0.6f * Time.deltaTime);
                yield return null;
            }
            if (textGO != null) Destroy(textGO);
        }

        private IEnumerator ZoomPunch()
        {
            cameraFollow.ZoomTo(DragonCloseUpSize * RoarZoomPunch, 0.06f);
            yield return new WaitForSeconds(0.12f);
            cameraFollow.ZoomTo(DragonCloseUpSize, 0.5f);
        }

        // 모래룡 입 위치(원화가 오른쪽을 보므로, 뒤집혀 있으면 왼쪽). 머리는 피벗에서 약 3유닛 옆, 4유닛 위에 있다.
        private Vector3 MouthPosition()
        {
            float facing = dragonRenderer != null && dragonRenderer.flipX ? -1f : 1f;
            return dragon.transform.position + new Vector3(facing * 3f, 4f, 0f);
        }

        // 입에서 모래빛 충격파 고리를 연달아 퍼뜨리고 화면을 모래빛으로 번쩍인다(ShockwaveBurst).
        private void Shockwaves()
        {
            ShockwaveBurst.Play(this, MouthPosition(), dragon.transform.parent, new ShockwaveBurst.Settings
            {
                rings = ShockwaveRings,
                interval = ShockwaveInterval,
                duration = ShockwaveDuration,
                maxSize = ShockwaveMaxSize,
                squash = 0.8f,
                ringColor = ShockwaveColor,
                flashColor = new Color(1f, 0.9f, 0.65f, FlashAlpha),
                flashDuration = FlashDuration,
                sortingOrder = 55,
            });
        }

        private GameObject CreateRoarText()
        {
            var go = new GameObject("SandDragonRoar");
            go.transform.SetParent(dragon.transform.parent, false);
            go.transform.position = dragon.transform.position + new Vector3(0f, 6.4f, 0f);
            var text = go.AddComponent<TextMesh>();
            text.text = RoarText;
            text.characterSize = 0.16f;
            text.fontSize = 80;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1f, 0.82f, 0.35f);
            go.GetComponent<MeshRenderer>().sortingOrder = 60;
            return go;
        }

        // ---- 입력 ----

        private bool SkipPressed()
        {
            var keyboard = Keyboard.current;
            return skippable && keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        private static bool AdvancePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame;
        }

        // 조작과 카메라를 원래대로 돌려준다. 두 번 불려도 안전하다.
        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (playerMovement != null) playerMovement.SetCutsceneLocked(false);
            if (cameraFollow != null)
            {
                if (player != null) cameraFollow.SetTarget(player.transform, CameraPanSmoothTime);
                cameraFollow.ResetSmoothTime();
                cameraFollow.ResetZoom(0.3f);
            }
        }

        // 연출 도중 스테이지가 다시 지어지면(포탈/사망 등) 막아 둔 것들을 풀고 나간다.
        private void OnDestroy()
        {
            if (!started || finished) return;
            dialogueUI?.Hide();
            Finish();
        }
    }
}

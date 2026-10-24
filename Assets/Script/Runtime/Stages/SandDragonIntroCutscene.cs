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
    // 카메라가 네크로시아를 클로즈업 → 네크로시아 대사 → 화면이 원래대로 돌아오며 네크로시아가 모래룡을 향해 대시 → 리듬전투.
    // 이 연출을 이미 본 세이브(두 번째 만남부터)는 Esc로 건너뛰면 모래룡이 바로 나타나고 곧장 전투가 열린다.
    public class SandDragonIntroCutscene : MonoBehaviour
    {
        // 모래룡과 이만큼 가까워지면 연출이 시작된다.
        private const float TriggerDistance = 10f;

        private const float RumbleDuration = 1.0f;
        private const float RumbleMagnitude = 0.06f;
        private const float CameraPanSmoothTime = 0.5f;
        private const float DragonCloseUpSize = 2.6f;
        private const float PlayerCloseUpSize = 2.2f;
        private const float ZoomDuration = 0.7f;

        // 땅 밑에서 솟아오르기(Emerge 8프레임). 모래가 터지는 동안 땅도 같이 흔들린다.
        private const string EmergePath = "Art/Monsters/SandDragon/Emerge";
        private const float EmergeDuration = 1.6f;
        private const float EmergeShakeMagnitude = 0.12f;

        // 포효(Roar 6프레임). 입을 크게 벌리는 4번째 프레임쯤(RoarImpactTiming)에 충격파가 터진다.
        private const string RoarPath = "Art/Monsters/SandDragon/Roar";
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
        private const float BattleGap = 2.2f; // 모래룡 몸 앞에서 멈추는 거리

        private static readonly string[] NecrosiaLines =
        {
            "...땅 밑에서 깨어난 건가. 폐허의 오래된 기록을 지켜 온 수호룡.",
            "미안하지만 비켜 줘야겠다. 그 문 너머의 기록, 내가 확인해야 하니까.",
        };
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
        private static Sprite ringSprite;

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
                playerMovement.SetInputLocked(true);
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

            foreach (var line in NecrosiaLines)
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
            if (skipped) { yield return SkipToBattle(); yield break; }

            // 5) 화면을 되돌리며 모래룡을 향해 대시 → 전투.
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(player.transform, 0.12f);
                cameraFollow.ResetZoom(0.35f);
            }
            yield return DashIntoBattle();
        }

        private bool skipped;

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
            var battle = Object.FindFirstObjectByType<RhythmBattleUI>();
            if (battle != null && dragonMonster != null && player != null) battle.TryBegin(dragonMonster, player);
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
            StartCoroutine(ScreenFlash());
            StartCoroutine(Shockwaves());
        }

        private IEnumerator ZoomPunch()
        {
            cameraFollow.ZoomTo(DragonCloseUpSize * RoarZoomPunch, 0.06f);
            yield return new WaitForSeconds(0.12f);
            cameraFollow.ZoomTo(DragonCloseUpSize, 0.5f);
        }

        // 모래룡 입 위치(원화가 오른쪽을 보므로, 뒤집혀 있으면 왼쪽). 머리는 피벗에서 약 1.6유닛 옆, 2유닛 위에 있다.
        private Vector3 MouthPosition()
        {
            float facing = dragonRenderer != null && dragonRenderer.flipX ? -1f : 1f;
            return restPosition + new Vector3(facing * 1.5f, 2f, 0f);
        }

        private IEnumerator Shockwaves()
        {
            var sprite = RingSprite();
            Vector3 origin = MouthPosition();
            for (int i = 0; i < ShockwaveRings; i++)
            {
                StartCoroutine(ExpandRing(sprite, origin, i == 0 ? 1.15f : 1f));
                yield return new WaitForSeconds(ShockwaveInterval);
            }
        }

        private IEnumerator ExpandRing(Sprite sprite, Vector3 origin, float sizeScale)
        {
            var go = new GameObject("RoarShockwave");
            go.transform.SetParent(dragon.transform.parent, false);
            go.transform.position = origin;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 55;
            for (float t = 0f; t < ShockwaveDuration; t += Time.deltaTime)
            {
                float k = t / ShockwaveDuration;
                float size = Mathf.Lerp(0.6f, ShockwaveMaxSize * sizeScale, 1f - (1f - k) * (1f - k)); // 처음에 빠르게 퍼진다
                go.transform.localScale = new Vector3(size, size * 0.8f, 1f);
                renderer.color = new Color(ShockwaveColor.r, ShockwaveColor.g, ShockwaveColor.b, ShockwaveColor.a * (1f - k));
                yield return null;
            }
            Destroy(go);
        }

        // 가장자리가 부드러운 고리(지름 1유닛) 그림을 한 번만 만들어 둔다.
        private static Sprite RingSprite()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float center = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center; // 0~1
                    float band = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.12f);                      // 바깥쪽 굵은 띠
                    float inner = Mathf.Clamp01(1f - Mathf.Abs(d - 0.6f) / 0.25f) * 0.25f;              // 안쪽 옅은 일렁임
                    byte a = (byte)(Mathf.Clamp01(band + inner) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return ringSprite;
        }

        // 카메라 앞을 통째로 덮는 모래빛 번쩍임.
        private IEnumerator ScreenFlash()
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            var go = new GameObject("RoarFlash");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 1f);
            go.transform.localScale = new Vector3(40f, 40f, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            var texture = Texture2D.whiteTexture;
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            renderer.sortingOrder = 58;
            for (float t = 0f; t < FlashDuration; t += Time.deltaTime)
            {
                renderer.color = new Color(1f, 0.9f, 0.65f, FlashAlpha * (1f - t / FlashDuration));
                yield return null;
            }
            Destroy(go);
        }

        private GameObject CreateRoarText()
        {
            var go = new GameObject("SandDragonRoar");
            go.transform.SetParent(dragon.transform.parent, false);
            go.transform.position = restPosition + new Vector3(0f, 3.6f, 0f);
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
            if (playerMovement != null) playerMovement.SetInputLocked(false);
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

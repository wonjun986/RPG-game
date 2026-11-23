# Aethoria

사신 **네크로시아**가 붉은 달의 밤에 잃은 힘을 되찾아 피의 여왕 루나벨에게 맞서는 2D 횡스크롤 액션 RPG입니다.
필드에서는 실시간으로 싸우고, 몬스터와 부딪히면 두 레인 리듬 전투로 넘어갑니다.

## 실행 방법

- Unity **6000.6.0f1** (Unity 6)로 프로젝트 폴더를 엽니다. 다른 버전으로 열면 에셋을 다시 가져오거나 업그레이드를 묻습니다.
- [Assets/Scenes/SampleScene.unity](Assets/Scenes/SampleScene.unity)를 열고 Play를 누릅니다.
  씬은 비어 있어도 됩니다. 맵, 플레이어, 몬스터, UI는 모두 `GameBootstrap`이 코드로 만듭니다.

## 조작

| 키 | 동작 |
|---|---|
| ← → | 이동 |
| ↑ / ↓ | 점프(공중에서 한 번 더) / 발판 아래로 내려가기 |
| Z | 기본 공격, 대화 넘기기, 확인 |
| X | 취소 |
| A S D Q W E R | 스킬 (레벨에 맞춰 이시스에게 배움) |
| Shift | 대시 |
| F | 회복 포션 |
| I / C | 인벤토리 / 상태창 |
| Esc | 창 닫기, 필드에서는 타이틀로 돌아가기 |
| 리듬 전투 Z / X | 위 레인 / 아래 레인 |
| F1 | 관리자(테스트) 메뉴. 열린 동안 F2~F12로 명령 실행 |

## 진행 흐름

메인 퀘스트는 마을의 노아가 주고, 퀘스트를 깰수록 월드맵의 지역이 열립니다.

1. **튜토리얼 숲** → 마을 도착
2. **광활한 평야**: 슬라임 → 보스 슬라임
3. **어둠의 숲**: 기사 → 알리아스
4. **잊혀진 폐허**: 콜로서스 → 황금 모래룡
5. **푸른 바다**: 심해 정령 → 돌길 붕괴 → 추격전 → 아쿠아 2차전
6. **천상의 계단 · 신전 · 이리아의 성소**: 이리아 만나기
7. **절망의 언덕**: 최종보스 루나벨 (2페이즈)

NPC: 노아(상점, 메인 퀘스트), 이시스(스킬, 연습장), 엘리(강화), 엔스룸 마을의 릴리(도감, 리듬 대결), 성소의 이리아(축복, 마지막 퀘스트).

## 주요 시스템

- **성장**: 레벨/경험치, 스킬 7개(레벨 1~16에 배움), 장비 6부위, 세트 효과(공허의 고딕, 심연의 군주), 보스 전용 장비, 강화(강화석·수정구), 축복
- **퀘스트**: 메인 퀘스트 10개, 날마다 바뀌는 일일 퀘스트
- **전투**: 실시간 전투 + 리듬 전투, 계산식은 [DamageCalculation.md](DamageCalculation.md)
- **기타**: 몬스터 도감, 저장 슬롯 5개(종료 시 자동 저장), 보스 등장·처치 컷신, 첫 클리어 엔딩·크레딧

## 폴더 구조

```
Assets/
  Script/Runtime/
    Bootstrap/   게임 시작과 맵 구성 (GameBootstrap, BGM/효과음 재생)
    Characters/  플레이어 이동, 공격, 스킬, 인벤토리, 스킬북
    Combat/      데미지 계산, 데미지 숫자
    Data/        아이템, 몬스터, 도감, 강화/축복 규칙
    Monsters/    일반 몬스터와 보스 AI
    Quests/      메인/일일 퀘스트
    Save/        저장, 설정
    Stages/      NPC, 컷신, 포탈, 스테이지 진행
    UI/          모든 화면 (HUD, 인벤토리, 상점, 리듬 전투 등)
  Script/Editor/ 캐릭터 그림 가져오기 설정
  Resources/     코드에서 불러오는 그림, 배경, 음악
```

### GameBootstrap 파일 나눔

`GameBootstrap`은 `partial class`로 여러 파일에 나뉘어 있습니다.

| 파일 | 내용 |
|---|---|
| `GameBootstrap.cs` | 스테이지 번호, 공유 상태, 새 게임/이어하기/저장, 맵 이동, `BuildStage` |
| `GameBootstrap.Village.cs` | 마을, 튜토리얼 숲, 어둠의 숲 발판, 마을 건물, 연습장 |
| `GameBootstrap.Fields.cs` | 평야, 폐허, 푸른 바다(추격전, 아쿠아 2차전) |
| `GameBootstrap.Heaven.cs` | 엔스룸 마을, 절망의 언덕, 천상의 계단·신전·성소 |
| `GameBootstrap.MapBuilder.cs` | 소품/발판, 맵 틀·배경·벽, 카메라, 포탈 |
| `GameBootstrap.Hud.cs` | HUD와 UI 생성 |
| `GameBootstrap.Spawning.cs` | 플레이어/몬스터/보스 생성, 몬스터 데이터, 드랍 |

### RhythmBattleUI 파일 나눔

리듬 전투 화면 `RhythmBattleUI`도 `partial class`로 나뉘어 있습니다. MonoBehaviour라서 클래스 이름과 같은 `RhythmBattleUI.cs`는 꼭 남겨 둬야 합니다.

| 파일 | 내용 |
|---|---|
| `RhythmBattleUI.cs` | 채보·패턴·판정 수치, 전투 상태, 시작/종료, 도주, 몬스터 애니메이션, 데미지 숫자 |
| `RhythmBattleUI.EnemyTurn.cs` | 적 턴, 보스 패턴 턴, 루나벨 2페이즈 전환, 피의 손 |
| `RhythmBattleUI.BossSpecials.cs` | 보스 필살기(알리아스, 루나벨, 모래룡), 보스전 도주 차단 |
| `RhythmBattleUI.PlayerTurn.cs` | 내 턴, 행동·스킬 선택 창, 네크로시아 모션, 궁극기 |
| `RhythmBattleUI.LunabelIntro.cs` | 루나벨전 등장 연출, 이름 카드, 시작 대사 |
| `RhythmBattleUI.Chart.cs` | 채보 재생, 노트 이동, 판정, 롱노트 |
| `RhythmBattleUI.Layout.cs` | 전투 화면 구성과 UI 생성 도우미 |

## 기획 문서

- [DamageCalculation.md](DamageCalculation.md): 데미지 계산식
- [SoulEater_Assassin_Guide.md](SoulEater_Assassin_Guide.md): 네크로시아 캐릭터 기획
- [NeededAssets.md](NeededAssets.md): 아직 넣지 않은 그림·소리 파일 목록과 경로

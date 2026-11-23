# 넣어야 할 그림 · 소리 목록

코드는 아래 경로를 이미 찾고 있습니다. 같은 이름으로 파일을 넣기만 하면 바로 게임에 나옵니다.
경로는 모두 `Assets/Resources/` 기준이고, 확장자는 자유입니다(그림 `.png`, 소리 `.ogg`/`.wav`/`.mp3`).
파일이 없는 동안에도 게임은 문제없이 돌아갑니다.

## 보물상자 — `Art/Props/Chest/` (선택)

없으면 코드로 그린 작은 픽셀 상자가 나옵니다. 두 장을 같은 캔버스 크기로, 바닥이 그림 맨 아래에 닿게 그려 주세요.
폭 약 200px이면 캐릭터 옆에 알맞은 크기(약 0.8칸)가 됩니다.

| 파일 이름 | 그림 |
|---|---|
| `chest_closed` | 닫힌 보물상자 |
| `chest_open` | 열린 보물상자(안에 금화) |

## 엔딩 일러스트 — `UI/Ending/`

없으면 검은 화면에 문장만 나옵니다. 가로로 긴 그림을 권장합니다.

| 파일 이름 | 장면 |
|---|---|
| `ending_1` | 붉은 달이 저물고 푸른 하늘이 돌아온 언덕 |
| `ending_2` | 마을 사람들과 노아가 돌아오는 네크로시아를 맞이함 |
| `ending_3` | 천상의 신전에서 기도하는 이리아 |
| `ending_4` | 다시 벼린 낫을 든 네크로시아 |

크레딧에 들어갈 이름은 [EndingCreditsUI.cs](Assets/Script/Runtime/UI/EndingCreditsUI.cs)의 `CreditLines`에 직접 적어 주세요. 지금은 "(이름을 적어 주세요)"로 되어 있습니다.

## 배경 음악 — `Audio/Bgm/`

| 파일 이름 | 나오는 곳 | 파일이 없을 때 |
|---|---|---|
| `tutorial_forest` | 튜토리얼 숲 | `dark_forest` → 없으면 무음 |
| `dark_forest` | 어둠의 숲 1~2 | 무음 |
| `plains` | 광활한 평야 1~3 | 무음 |
| `blue_sea` | 푸른 바다 1~3 | 무음 |
| `aqua_boss` | 바다 추격전, 아쿠아 2차전 | 모래룡 보스 곡 |
| `heaven` | 천상의 계단 · 신전 · 이리아의 성소 | 무음 |
| `lunabel_boss` | 절망의 언덕 (루나벨전) | 무음 |
| `training_hall` | 이시스의 연습장 | 마을 곡 |
| `ensroom` | 엔스룸 마을 | 마을 곡 |
| `ending` | 엔딩 · 크레딧 | 마을 곡 |

여러 맵으로 이어진 지역은 맵을 넘어가도 곡이 끊기지 않고, 곡이 끝나면 처음부터 다시 나옵니다.

## 효과음 — `Audio/Sfx/`

| 파일 이름 | 나오는 때 |
|---|---|
| `necrosia_jump` | 점프, 2단 점프 |
| `necrosia_dash` | 대시 (Shift) |
| `necrosia_hurt` | 네크로시아가 맞았을 때 |
| `skill_a` | A 낫 회전 베기 |
| `skill_s` | S 어둠의 주먹 |
| `skill_d` | D 낫 부메랑 |
| `skill_w` | W 악마의 손 |
| `skill_e` | E 사슬 폭풍 |
| `skill_r` | R 죽음의 소용돌이 |
| `monster_hit` | 몬스터가 맞았을 때 |
| `level_up` | 레벨업 |
| `potion_drink` | 포션 마시기 (F) |
| `item_pickup` | 아이템 획득 (상점 구매 포함) |
| `quest_complete` | 메인 · 일일 퀘스트 보고 |
| `portal_enter` | 포탈에 들어갈 때 |
| `rhythm_hit` | 리듬 전투 노트 성공 |
| `rhythm_miss` | 리듬 전투 노트 실패 |

Q 스킬은 기존 기본 공격 소리(`necrosia_z_attack`)를 그대로 씁니다.

## 아직 코드가 없는 것

- **엔스룸 마을 NPC 추가**: NPC 그림이 정해지면 대사와 역할을 함께 만듭니다.

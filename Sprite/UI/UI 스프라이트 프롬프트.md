# UI 스프라이트 시트 프롬프트

미니맵, 포션 칸, 상호작용 말풍선, 지역 이름 장식에 쓸 그림이다. 시트 **3장**이면 끝난다. 받으면 내가 잘라서 이름을 붙이고 넣는다.

- 그림이 없어도 지금 게임은 돌아간다. 코드로 그린 임시 그림(둥근 판·픽셀 아이콘)이 대신 나온다. 그림을 넣으면 그쪽으로 바뀐다.
- 기존 팝업 판(`Assets/Resources/UI/Common/Popup/popup_panel.png`)과 같은 분위기로 맞춘다. **어두운 보라 바탕 + 금빛 세공 테두리 + 보랏빛 수정 + 초승달 장식**.
- 시트는 가로로 긴 이미지(16:9 이상)로 뽑는다. 그림끼리 겹치지 않고 사이가 넉넉히 비어 있으면 된다.
- 배경은 투명이 가장 좋다. 안 되면 **한 가지 색으로 꽉 찬 배경**이어도 괜찮다(그림자·바닥 없이).
- 넣을 곳: `Sprite/UI/Sheets/` (파일 이름은 `sheet_1.png`처럼 아무렇게나 해도 된다).

---

## 시트 U1 — 지도 아이콘 5개 (한 줄)

미니맵 방 위에 작게 붙는 아이콘이다(작은 지도에서 약 10px, 큰 지도에서 18px). **작게 줄여도 모양이 읽히도록** 굵고 단순한 실루엣이 중요하다.

| 순서 | 잘라 쓸 이름 | 넣을 곳 | 모양 |
|---|---|---|---|
| 1 | icon_key | `UI/Map/icon_key` | 금빛 열쇠 |
| 2 | icon_chest | `UI/Map/icon_chest` | 보물상자 |
| 3 | icon_elite | `UI/Map/icon_elite` | 붉은 해골(정예 몬스터) |
| 4 | icon_puzzle | `UI/Map/icon_puzzle` | 하늘색 룬 마름모(퍼즐) |
| 5 | icon_arena | `UI/Map/icon_arena` | 주황빛 교차한 두 검(전멸방) |

```
Game UI icon sprite sheet, 5 separate square icons arranged in one horizontal row with wide empty gaps between them, none overlapping, all the same size. Dark gothic fantasy RPG style, bold simple silhouettes that stay readable when shrunk to 12 pixels, thick dark outline around each icon, subtle gold rim highlight.
1) an ornate golden key with a round bow, glowing faintly;
2) a small wooden treasure chest with gold trim and a gold lock, closed;
3) a menacing red glowing skull, front view, dark eye sockets;
4) a glowing pale cyan rune crystal in a diamond shape with a small engraved glyph in the center;
5) two crossed swords with orange glowing blades, hilts at the bottom.
Flat front view, crisp edges, high contrast, transparent background, no shadow, no text, no labels, no frame.
```

---

## 시트 U2 — 판과 틀 3개 (한 줄)

가운데는 **비어 있는 어두운 판**이어야 한다(그 위에 코드가 지도·포션·글자를 얹는다). 테두리 장식은 가장자리에만 둔다.

| 순서 | 잘라 쓸 이름 | 넣을 곳 | 비율(가로:세로) | 쓰임 |
|---|---|---|---|---|
| 1 | MinimapFrame | `UI/Map/MinimapFrame` | 약 1.75 : 1 | 오른쪽 아래 미니맵 판(큰 지도도 같은 그림을 늘려 쓴다) |
| 2 | PotionSlot | `UI/HUD/PotionSlot` | 1 : 1 | HUD 오른쪽 포션 칸 |
| 3 | PromptBubble | `UI/Common/PromptBubble` | 약 3 : 1 | NPC 머리 위 "[Z] 대화하기" 말풍선 |

```
Game UI frame sprite sheet, 3 separate empty frames arranged in one horizontal row with wide empty gaps between them, none overlapping. Dark gothic fantasy RPG interface matching a dark purple panel with ornate rose-gold filigree borders, small violet crystals and a crescent moon motif.
1) a wide rectangular map frame, about 1.75 times wider than tall, semi-transparent very dark purple flat interior, thin ornate rose-gold border with small violet crystal gems at the four corners and a tiny crescent moon ornament at the top center;
2) a square item slot frame, very dark purple flat interior, thick rose-gold border with a violet crystal at each corner, slightly beveled;
3) a small horizontal rounded pill-shaped speech label, about three times wider than tall, semi-transparent dark purple flat interior, thin glowing rose-gold outline, no tail.
The interiors must be completely plain and empty so text and icons can be placed on top. All decoration stays on the border only. Flat front view, crisp edges, transparent background, no shadow, no text, no labels.
```

---

## 시트 U3 — 지역 이름 장식줄 1개

지역에 들어설 때 화면 위쪽에 뜨는 큰 이름("어둠의 숲") **바로 아래**에 깔리는 가로로 긴 장식줄이다.

| 순서 | 잘라 쓸 이름 | 넣을 곳 | 비율(가로:세로) |
|---|---|---|---|
| 1 | AreaTitleOrnament | `UI/Map/AreaTitleOrnament` | 약 13 : 1 |

```
A single long thin horizontal ornamental divider for a dark gothic fantasy RPG area title, about 13 times wider than tall. Elegant rose-gold filigree lines that taper and fade toward both ends, a small glowing violet crystal and a crescent moon in the exact center, tiny sparkles along the line. Symmetrical left and right. Flat front view, crisp edges, transparent background, no shadow, no text, no frame.
```

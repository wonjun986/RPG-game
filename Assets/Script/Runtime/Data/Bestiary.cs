using System.Collections.Generic;
using Aethoria.Save;

namespace Aethoria.Data
{
    // 적 도감 한 칸. key는 MonsterData.monsterName과 같아야 처치 수가 이 칸에 쌓인다.
    // description은 처음 잡으면, secret은 secretKills만큼 잡으면 열린다. 글은 엔스룸의 릴리가 직접 적어 둔 말투로 쓴다.
    public class BestiaryEntry
    {
        public string key;
        public string kind;         // 종류(도감 오른쪽 "종류" 칸)
        public string size;         // 크기(도감 오른쪽 "크기" 칸)
        public string habitat;
        public int danger;          // 위험도(별 1~5개)
        public string portraitPath; // 도감 그림으로 쓸 대기/걷기 프레임 폴더(첫 장을 쓴다)
        public string description;
        public int secretKills;
        public string secret;
        public BestiaryDrop[] drops = System.Array.Empty<BestiaryDrop>(); // 도감 "주요 드롭 아이템" 칸(최대 4개)
    }

    // 드롭 칸 하나: 아이템 id(ItemDatabase.Find)와 칸 아래에 붙는 짧은 설명(확률/개수).
    public readonly struct BestiaryDrop
    {
        public readonly string itemId;
        public readonly string note;
        public BestiaryDrop(string itemId, string note) { this.itemId = itemId; this.note = note; }
    }

    // 몬스터를 잡은 수(이름별). 어떤 몬스터든 쓰러지면 GameBootstrap이 Record를 부르고, 저장 파일에 함께 남긴다.
    public static class Bestiary
    {
        // 실제 드롭 규칙(GameBootstrap.HandleMonsterDiedForDrops / TryDropDevilNecklace)과 맞춘 표시용 목록.
        private static readonly BestiaryDrop[] NormalDrops = { new BestiaryDrop("enhance_stone", "25%") };
        private static readonly BestiaryDrop[] BossDrops = { new BestiaryDrop("enhance_stone", "×3"), new BestiaryDrop("crystal_orb", "×3") };
        private static readonly BestiaryDrop[] AliasDrops =
        {
            new BestiaryDrop("enhance_stone", "×3"), new BestiaryDrop("crystal_orb", "×3"), new BestiaryDrop("devil_necklace", "30%"),
        };

        public static readonly BestiaryEntry[] Entries =
        {
            new BestiaryEntry
            {
                key = "숲의 정령", kind = "정령", size = "작음", habitat = "네크로시아가 눈을 뜬 숲", danger = 1,
                portraitPath = "Art/Monsters/ForestSpirit/Walk", drops = NormalDrops,
                description = "숲에서 길 잃은 사람한테 슬그머니 다가오는 작은 정령이야. 뿔이 제법 멋있지만, 사실 겁이 엄청 많대. " +
                              "처음 싸워 보는 상대로 딱 좋아. 너무 세게 때리진 말아 줘!",
                secretKills = 1,
                secret = "숲의 정령은 쓰러뜨려도 아침이면 이슬 속에서 다시 태어난대. 그러니까 미안해하지 않아도 돼. ...그래도 가끔은 쓰다듬어 주고 싶어.",
            },
            new BestiaryEntry
            {
                key = "슬라임", kind = "슬라임", size = "작음", habitat = "광활한 평야", danger = 1,
                portraitPath = "Art/Monsters/Slime/Walk", drops = NormalDrops,
                description = "말랑말랑, 통통! 평야 어디서나 보이는 슬라임이야. 보기엔 귀여워도 밭을 다 망쳐 놔서 농부 아저씨들이 골치 아파해. " +
                              "몸이 물이라 그런지 햇빛 좋은 날엔 반짝반짝 예쁘게 빛나.",
                secretKills = 10,
                secret = "비밀인데... 붉은 달이 뜬 뒤로 슬라임 색이 아주 조금씩 탁해지고 있대. 평야 물맛이 변한 거랑 관계가 있을까? 엘리는 무슨 소리냐고 웃었지만.",
            },
            new BestiaryEntry
            {
                key = "보스 슬라임", kind = "슬라임 · 우두머리", size = "거대", habitat = "광활한 평야 끝", danger = 3,
                portraitPath = "Art/Monsters/BossSlime/Walk", drops = BossDrops,
                description = "슬라임들이 잔뜩 뭉쳐서 생긴 평야의 왕! 왕관 같은 건 없지만 덩치만큼은 진짜 왕이야. " +
                              "물방울을 뿅뿅 쏘아 대니까 옷 젖지 않게 조심해.",
                secretKills = 3,
                secret = "보스 슬라임 몸속엔 옛날 평야 사람들이 잃어버린 물건들이 둥둥 떠 있대. 반지, 단추, 그리고... 누군가의 연애편지까지! 꺼내 보고 싶지 않아?",
            },
            new BestiaryEntry
            {
                key = "기사", kind = "망령 기사", size = "보통", habitat = "어둠의 숲", danger = 2,
                portraitPath = "Art/Monsters/Knight/Walk", drops = NormalDrops,
                description = "어둠의 숲을 떠도는 갑옷 기사들이야. 말도 안 통하고, 사람만 보면 칼부터 휘둘러. " +
                              "셋이 몰려다니니까 발판 위로 폴짝 피해 다니는 게 요령이래.",
                secretKills = 10,
                secret = "저 기사들, 원래는 이 땅을 지키던 진짜 기사였대. 붉은 달이 뜬 밤 언덕으로 갔다가... 갑옷만 돌아온 거래. 이시스 언니한테는 이 얘기 하지 마. 알았지?",
            },
            new BestiaryEntry
            {
                key = "알리아스", kind = "망령 기사 · 우두머리", size = "큼", habitat = "어둠의 숲 깊은 곳", danger = 4,
                portraitPath = "Art/Monsters/BossKnight/Idle", drops = AliasDrops,
                description = "어둠의 숲 기사들의 대장이야. 하늘 높이 뛰어올라서 쾅! 내려찍는 게 장기래. " +
                              "목소리가 엄청 굵고 무서운데, 말하는 걸 들어 보면 의외로 예의 바르대.",
                secretKills = 3,
                secret = "알리아스의 투구엔 낡은 리본 하나가 묶여 있대. 누가 묶어 준 걸까... 기다리는 사람이 아직 있을지도 몰라. 그렇게 생각하면 조금 슬퍼져.",
            },
            new BestiaryEntry
            {
                key = "콜로서스", kind = "골렘", size = "거대", habitat = "잊혀진 폐허", danger = 3,
                portraitPath = "Art/Monsters/Colossus/Walk", drops = NormalDrops,
                description = "폐허를 지키는 커다란 돌거인이야. 걸을 때마다 땅이 쿵쿵 울려서 멀리서도 알 수 있어. " +
                              "한 대 한 대가 묵직하니까 정면으로 받지 말고 박자를 잘 보고 막아!",
                secretKills = 10,
                secret = "콜로서스 등에는 아주 작은 글씨가 새겨져 있대. '이 문을 지나는 자에게 평안을.' 원래는 폐허가 아니라 누군가의 집을 지키던 수문장이었나 봐.",
            },
            new BestiaryEntry
            {
                key = "황금 모래룡", kind = "용 · 우두머리", size = "거대", habitat = "잊혀진 폐허 가장 안쪽", danger = 4,
                portraitPath = "Art/Monsters/SandDragon/Walk", drops = BossDrops,
                description = "폐허 가장 안쪽 모래 속에 사는 황금빛 용이야! 비늘이 전부 금이라는 소문이 있어서 보물 사냥꾼들이 몰려갔다가... " +
                              "다들 모래투성이가 돼서 울면서 돌아왔대. 모래 물결을 타고 스르륵 다가오니까 발밑 조심!",
                secretKills = 3,
                secret = "콜로서스 등에 새겨진 '이 문을 지나는 자에게 평안을', 그 문 너머에 살던 게 바로 모래룡이래. 폐허가 아직 왕국이던 시절의 수호룡! " +
                         "백성들이 다 떠난 뒤에도 혼자 남아서 문을 지킨 거야. ...조금 외로웠을지도 몰라.",
            },
            new BestiaryEntry
            {
                key = "심해 정령", kind = "정령", size = "보통", habitat = "푸른 바다", danger = 3,
                portraitPath = "Art/Monsters/SeaSpirit/Walk", drops = NormalDrops,
                description = "푸른 바다 깊은 곳에 사는 청록빛 정령이야. 꼬리가 하늘하늘 너무 예뻐서 넋 놓고 보다가 한 대 맞기 딱 좋아. " +
                              "요즘 바다가 거칠어진 게 얘네가 화가 나서래.",
                secretKills = 10,
                secret = "물속 폐허에서 들린다는 노랫소리, 사실 심해 정령들이 부르는 자장가래. 잠들지 못하는 옛 도시 사람들을 재워 주려고. ...나도 한 번만 들어 보고 싶다.",
            },
            new BestiaryEntry
            {
                key = "루나벨", kind = "흡혈귀 · 여왕", size = "보통", habitat = "절망의 언덕", danger = 5,
                portraitPath = "Art/Monsters/FinalBoss/Idle", drops = BossDrops,
                description = "붉은 달의 주인, 피의 여왕 루나벨. ...이 페이지는 손이 떨려서 글씨가 삐뚤빼뚤해. " +
                              "상대의 박자를 흐트러뜨려서 무너뜨린대. 네가 이 페이지를 펼쳤다는 건... 정말 해냈구나!",
                secretKills = 1,
                secret = "이리아 님한테 몰래 들었는데, 루나벨도 아주 옛날엔 달의 노래를 지키던 아이였대. 언제부터 노래가 피 냄새로 바뀐 걸까. " +
                         "그래도 이제 하늘이 다시 파래졌으니까... 이 이야기는 여기서 끝! 축제 준비하러 가자!",
            },
        };

        private static readonly Dictionary<string, int> kills = new();

        // 몬스터 이름이 바뀌었을 때 예전 저장 파일의 처치 수를 새 이름으로 옮긴다(옛 이름 → 새 이름).
        private static readonly Dictionary<string, string> RenamedKeys = new()
        {
            { "보스 기사", "알리아스" },
        };

        // 관리자 모드(AdminUI): 처치 기록과 상관없이 모든 칸과 비밀 메모를 보여 준다. 저장되는 처치 수는 건드리지 않는다.
        public static bool AdminUnlockAll { get; set; }

        public static int Kills(string key) => kills.TryGetValue(key, out int count) ? count : 0;
        public static bool IsDiscovered(BestiaryEntry entry) => AdminUnlockAll || Kills(entry.key) > 0;
        public static bool IsSecretUnlocked(BestiaryEntry entry) => AdminUnlockAll || Kills(entry.key) >= entry.secretKills;

        public static int DiscoveredCount
        {
            get
            {
                int count = 0;
                foreach (var entry in Entries) if (IsDiscovered(entry)) count++;
                return count;
            }
        }

        public static void Record(string monsterName)
        {
            if (string.IsNullOrEmpty(monsterName)) return;
            kills[monsterName] = Kills(monsterName) + 1;
        }

        // 저장 파일에는 itemId 자리에 몬스터 이름, count 자리에 처치 수를 넣는다(ItemSaveEntry 재사용).
        public static List<ItemSaveEntry> ToSaveEntries()
        {
            var list = new List<ItemSaveEntry>();
            foreach (var pair in kills) list.Add(new ItemSaveEntry { itemId = pair.Key, count = pair.Value });
            return list;
        }

        // 새 게임이거나 도감이 없던 예전 저장 파일이면 entries가 비어 있어 처음부터 시작한다.
        public static void Restore(List<ItemSaveEntry> entries)
        {
            kills.Clear();
            if (entries == null) return;
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.itemId) || entry.count <= 0) continue;
                string key = RenamedKeys.TryGetValue(entry.itemId, out var renamed) ? renamed : entry.itemId;
                kills[key] = Kills(key) + entry.count;
            }
        }
    }
}

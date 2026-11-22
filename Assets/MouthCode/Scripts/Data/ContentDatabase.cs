using System;
using System.Collections.Generic;
using UnityEngine;

namespace MouthCode
{
    [Serializable]
    public class GlyphDef
    {
        [Tooltip("이 글자의 소리 (한글 1음절). 같은 소리 = 같은 글자")]
        public string sound;
        [Tooltip("이 날부터 등장")]
        public int unlockDay = 1;
        [Tooltip("비워두면 shapeSeed로 임시 도형을 자동 생성")]
        public Sprite sprite;
        public int shapeSeed;
    }

    [Serializable]
    public class WordDef
    {
        [Tooltip("한글로 적는다. 음절 하나 = 글자 하나. 모든 음절의 글자가 해금되어야 등장")]
        public string text;
        [Tooltip("글자가 다 해금되어도 이 날 전에는 안 나옴")]
        public int minDay = 1;
    }

    public enum SfxPreset { RisingSweep, FallingWhoop, Beeps, Trill, Fart, Boing }

    [Serializable]
    public class SfxDef
    {
        public string displayName;
        [Tooltip("이지 모드 발음 가이드 (예: 뾰오옹↗)")]
        public string guide;
        public int unlockDay = 2;
        [Tooltip("비워두면 tempSynth로 임시 합성음을 쓴다. 넣을 경우 Import 설정 Load Type = Decompress On Load")]
        public AudioClip clip;
        public SfxPreset tempSynth;
        [Tooltip("비워두면 shapeSeed로 임시 아이콘 생성")]
        public Sprite icon;
        public int shapeSeed;
        [Tooltip("하루 결과 화면에 나오는 이 효과음의 해독 내용")]
        public string meaning;
    }

    /// <summary>하루 결과 화면의 "해독 내용". 같은 암호문은 항상 같은 뜻이 된다.</summary>
    [Serializable]
    public class MeaningLines
    {
        [Tooltip("암호문 해독 내용. {place} {item} {num}은 아래 목록에서 채운다. 한 줄 30자 안팎으로")]
        [TextArea] public string[] lines;
        [Tooltip("{place} 자리 (뒤에 '에', '에서'가 붙어도 자연스러운 말)")]
        public string[] places;
        [Tooltip("{item} 자리 (뒤에 숫자+개/상자가 붙어도 자연스러운 말)")]
        public string[] items;
    }

    [Serializable]
    public class HqLines
    {
        [TextArea] public string[] perfect;
        [TextArea] public string[] good;
        [TextArea] public string[] bad;
        [TextArea] public string[] timeout;
        [TextArea] public string[] noSpeech;
        [TextArea] public string[] dayPass;
        [TextArea] public string[] gameOver;
        [TextArea] public string[] sfxGood;
        [TextArea] public string[] sfxBad;
        [Tooltip("틀렸을 때, 본부가 실제로 들은 말을 인용하는 대사. {heard} = 들린 말, {target} = 원문")]
        [TextArea] public string[] heardBad;
        [Tooltip("통과했지만 완벽하진 않을 때 들은 말을 인용하는 대사. {heard}, {target} 사용 가능")]
        [TextArea] public string[] heardGood;
        [Tooltip("브리핑 화면의 사령관 한마디. 첫 줄은 1일차 전용, 나머지는 무작위")]
        [TextArea] public string[] briefing;
    }

    [Serializable]
    public class RankDef
    {
        [Tooltip("이 날에 도달하면 이 계급")]
        public int day = 1;
        public string name;
    }

    /// <summary>
    /// 콘텐츠(암호 글자, 단어, 효과음, 본부 대사, 계급). 코드 수정 없이 Inspector에서 교체한다.
    /// 에셋 위치: Assets/MouthCode/Resources/ContentDatabase.asset
    /// </summary>
    [CreateAssetMenu(fileName = "ContentDatabase", menuName = "MouthCode/Content Database")]
    public class ContentDatabase : ScriptableObject
    {
        public List<GlyphDef> glyphs = DefaultContent.Glyphs();
        public List<WordDef> words = DefaultContent.Words();
        public List<SfxDef> sfx = DefaultContent.Sfx();
        public HqLines hq = DefaultContent.Hq();
        [Tooltip("날짜별 계급 (진급하면 브리핑에서 알림)")]
        public List<RankDef> ranks = DefaultContent.Ranks();
        [Tooltip("하루 결과 화면의 암호문 해독 내용")]
        public MeaningLines meanings = DefaultContent.Meanings();

        /// <summary>암호문의 해독 내용. 같은 암호문은 언제나 같은 뜻 (문자열 해시로 고른다).</summary>
        public string MeaningFor(MessageSpec m)
        {
            if (m == null) return "";
            if (m.isSfx) return m.sfx != null && !string.IsNullOrEmpty(m.sfx.meaning) ? m.sfx.meaning : "(효과음 신호. 뜻은 본부만 안다)";
            var src = meanings != null && meanings.lines != null && meanings.lines.Length > 0 ? meanings : DefaultContent.Meanings();
            uint h = 2166136261;
            foreach (var c in m.text) { h ^= c; h *= 16777619; }
            string s = src.lines[h % (uint)src.lines.Length];
            if (src.places != null && src.places.Length > 0) s = s.Replace("{place}", src.places[(h >> 7) % (uint)src.places.Length]);
            if (src.items != null && src.items.Length > 0) s = s.Replace("{item}", src.items[(h >> 13) % (uint)src.items.Length]);
            return s.Replace("{num}", (2 + (h >> 19) % 15).ToString());
        }

        /// <summary>그 날의 계급 이름.</summary>
        public string RankFor(int day)
        {
            string r = ranks != null && ranks.Count > 0 ? ranks[0].name : "";
            if (ranks == null) return r;
            foreach (var k in ranks) if (k != null && day >= k.day) r = k.name;
            return r;
        }

        /// <summary>그 날 새로 진급했는지.</summary>
        public bool PromotedOn(int day) => day > 1 && RankFor(day) != RankFor(day - 1);
    }

    /// <summary>임시 콘텐츠. 기획 확정 전까지 사용.</summary>
    public static class DefaultContent
    {
        public static List<RankDef> Ranks()
        {
            var table = new (int, string)[]
            {
                (1, "훈련병"), (2, "이등병"), (3, "일병"), (5, "상병"), (7, "병장"),
                (10, "하사"), (13, "중사"), (16, "상사"), (20, "소위"), (25, "중위"),
                (30, "대위"), (40, "소령"), (50, "전설의 입무전병"),
            };
            var list = new List<RankDef>();
            foreach (var (d, n) in table) list.Add(new RankDef { day = d, name = n });
            return list;
        }

        public static List<GlyphDef> Glyphs()
        {
            // (소리, 해금일) — 초반엔 하루 0~1개만, 후반에 몰아서 여러 개.
            // 난이도는 주로 메시지 길이로 올린다 (GameConfig.length*). 효과음 해금일과 겹치지 않게 했다.
            var table = new (string, int)[]
            {
                ("뿌", 1), ("르", 1), ("딱", 1),
                ("깡", 2),
                ("삐", 4),
                ("뽀", 5),
                ("쀼", 7), ("퐁", 7),
                ("냥", 9), ("쿵", 9),
                ("뿡", 11), ("찍", 11), ("꽥", 11),
                ("뀨", 13), ("랄", 13), ("촥", 13),
                ("뭉", 15), ("끼", 15), ("똥", 15), ("휘", 15),
                ("꿀", 17), ("짹", 17), ("푸", 17), ("덩", 17),
                ("쀅", 19), ("롱", 19),
            };
            var list = new List<GlyphDef>();
            for (int i = 0; i < table.Length; i++)
                list.Add(new GlyphDef { sound = table[i].Item1, unlockDay = table[i].Item2, shapeSeed = 1000 + i * 7919 });
            return list;
        }

        public static List<WordDef> Words()
        {
            var texts = new[]
            {
                "뿌르딱", "딱딱", "뿌뿌", "르르딱", "르뿌", "뿌르", "뿌딱", "딱뿌", "르딱", "뿌뿌딱", "딱뿌르",
                "깡깡", "깡뿌", "딱깡", "뿌깡", "르깡", "깡딱", "뿌르깡",
                "삐삐", "뿌삐", "삐딱", "삐뿌르", "르삐",
                "쀼", "쀼르딱", "쀼쀼", "깡쀼",
                "뽀꽥", "꽥꽥", "뽀삐", "뽀뽀", "꽥딱", "르뽀",
                "뀨뀨", "퐁퐁", "뀨르", "퐁딱", "삐퐁", "뽀뀨",
                "뿡뿡", "냥냥", "뿡뿌딱", "냥꽥", "뿡깡",
                "쿵딱", "찍찍", "쿵쿵", "찍퐁", "쿵냥",
                "랄랄", "촥촥", "랄뿌", "촥딱", "랄랄삐",
                "뭉뭉", "끼끼", "뭉끼", "끼냥", "뭉뿌",
                "똥꽥", "휘휘", "똥똥", "휘뽀", "똥깡",
                "꿀꿀", "짹짹", "꿀딱", "짹삐", "꿀뀨",
                "푸덩", "덩덩", "푸르르", "덩쿵",
                "쀅쀅", "롱뽀", "쀅뽀", "롱뿌",
            };
            var list = new List<WordDef>();
            foreach (var t in texts) list.Add(new WordDef { text = t, minDay = 1 });
            return list;
        }

        public static List<SfxDef> Sfx()
        {
            return new List<SfxDef>
            {
                // 4일마다 하나씩 (3, 6, 10, 14, 18, 22일차)
                new SfxDef { displayName = "뾰옹", guide = "뾰오오옹↗ 점점 높게", unlockDay = 3, tempSynth = SfxPreset.RisingSweep, shapeSeed = 501, meaning = "상승 신호. 전원 천장을 볼 것. 이유는 없다." },
                new SfxDef { displayName = "뿌웅", guide = "뿌우우웅↘ 점점 낮게", unlockDay = 6, tempSynth = SfxPreset.FallingWhoop, shapeSeed = 502, meaning = "하강 신호. 사령관이 계단을 내려오는 중." },
                new SfxDef { displayName = "삐삐삐", guide = "삐! 삐! 삐! 짧게 세 번", unlockDay = 10, tempSynth = SfxPreset.Beeps, shapeSeed = 503, meaning = "3단계 경보. 화장실 앞에 줄 서기 시작." },
                new SfxDef { displayName = "부르르", guide = "부르르르르 입술을 떨면서", unlockDay = 14, tempSynth = SfxPreset.Trill, shapeSeed = 504, meaning = "한파 경보. 내복 두 겹 착용." },
                new SfxDef { displayName = "방귀", guide = "뿌지직 낮고 지저분하게", unlockDay = 18, tempSynth = SfxPreset.Fart, shapeSeed = 505, meaning = "(해독 불가. 해독병이 창문을 열었다)" },
                new SfxDef { displayName = "띠요옹", guide = "띠요오옹~ 떨리게", unlockDay = 22, tempSynth = SfxPreset.Boing, shapeSeed = 506, meaning = "용수철 보급 완료. 어디에 쓰는지는 묻지 말 것." },
            };
        }

        public static MeaningLines Meanings()
        {
            return new MeaningLines
            {
                lines = new[]
                {
                    "{place}에 {item} {num}상자 도착. 아무도 열지 말 것.",
                    "{place} {item} 재고 이상 없음. 반복한다, 이상 없음.",
                    "{item} {num}개 분실. 범인은 {place} 쪽으로 도주.",
                    "오늘 밤 {place} 집합. 준비물은 {item}.",
                    "적군이 {item}에 관심을 보인다. 전부 숨길 것.",
                    "{place} 근무자에게 알림. 노래 좀 그만 불러라.",
                    "{place}에서 수상한 {item} 발견. 맛은 괜찮았다.",
                    "작전명 '{item}' 개시. 이유는 아무도 모른다.",
                    "{place} 문 고장. 당분간 창문으로 다닐 것.",
                    "{item} 보급 지연. 대신 {num}일간 건빵만 나간다.",
                    "{place} 청소 당번이 사라졌다. {item}만 남아 있었다.",
                    "{item} 몰래 먹은 사람 자수할 것. {place}에서 기다린다.",
                    "오늘 저녁은 카레다. 반복한다, 카레다.",
                    "적 사령관 생일 확인. 케이크는 보내지 말 것.",
                    "고양이가 무전기 위에서 잔다. 깨우지 말 것.",
                    "암호책 37쪽에 커피 쏟음. 37쪽은 당분간 쓰지 말 것.",
                    "비둘기 2호 귀환. 1호는 아직 소식 없음.",
                    "내일 아침 점호 10분 연기. 사령관 늦잠 예정.",
                    "본부 와이파이 비밀번호 바뀜. 아무도 모름.",
                    "정찰병 보고. 적도 우리만큼 졸려 보인다.",
                    "다음 주 체육대회. 줄다리기 밧줄 확보 바람.",
                    "사령관 콧수염 정비 완료. 사기 상승.",
                    "이 메시지는 아무 의미 없음. 그냥 심심했다.",
                    "적 암호 해독 성공. 내용은 '배고프다'.",
                    "휴지 보급 긴급 요청. 매우, 매우 긴급.",
                    "어젯밤 들린 소리는 유령이 아니라 보일러였다.",
                    "감자밭에 수상한 발자국. 토끼로 추정.",
                    "작전 성공하면 치킨. 실패하면 반성문 3장.",
                    "적군 무전에서 우리 암호로 만든 노래가 들린다.",
                    "적 탐지망 강화 중. 송신은 짧고 정확하게.",
                    "본부 전원 이상 무. 사령관 기분은 이상 있음.",
                    "새 통신병 칭찬함. 사령관이 직접 한 말 아님.",
                },
                places = new[]
                {
                    "3번 창고", "취사장", "북쪽 초소", "사령관실", "세탁실", "연병장",
                    "매점", "의무실", "지하 벙커", "비밀 통로", "정비고", "옥상 안테나",
                },
                items = new[]
                {
                    "감자", "고무 오리", "건빵", "양말", "우산", "라면 스프",
                    "사령관 가발", "군화 끈", "비상 초콜릿", "휴지", "망원경", "수박",
                },
            };
        }

        /// <summary>
        /// 본부 사령관 대사. 말투: 부하에게 하는 반말, 짧게 끊고, 무뚝뚝한데 웃긴 사람.
        /// 화면에서 사령관 사진 옆에 나오므로 1인칭으로 말한다 ("사령관이 ~했다" 같은 3인칭 금지).
        /// </summary>
        public static HqLines Hq()
        {
            return new HqLines
            {
                perfect = new[]
                {
                    "본부: 완벽하다. 받아 적던 해독병이 감동해서 울었다.",
                    "본부: 자네 혹시 이 암호 원어민인가?",
                    "본부: 깔끔하다. 박수 한 번 쳐 주지. 짝.",
                    "본부: 이 정도면 적도 받아 적었겠군. 농담이다.",
                },
                good = new[]
                {
                    "본부: 수신 양호. 계속해.",
                    "본부: 대충 알아들었다. 대충.",
                    "본부: 좋아. 침이 좀 튄 것 같은데 넘어가지.",
                    "본부: 통과. 혀는 좀 풀고 와라.",
                },
                bad = new[]
                {
                    "본부: 방금 그거 재채기였나?",
                    "본부: 해독해 보니 '감자 세 박스 보내라'다. 이게 맞나?",
                    "본부: 해독병이 웃다가 의자에서 떨어졌다. 다시 해 봐.",
                    "본부: 적도 못 알아듣겠군. 문제는 우리도 못 알아들었다는 거다.",
                },
                timeout = new[]
                {
                    "본부: 응답이 없다. 그사이 적이 신호를 잡았다.",
                    "본부: 너무 조용해서 졸았나 했다. 적은 안 졸았다.",
                },
                noSpeech = new[]
                {
                    "본부: 숨소리만 들린다. 무섭다.",
                    "본부: 버튼만 누르면 뭐 하나. 입을 써, 입을.",
                    "본부: 마이크는 입 앞에 대는 거다. 코 말고.",
                },
                dayPass = new[]
                {
                    "본부: 오늘 작전 끝. 퇴근해라. 내일 보자.",
                    "본부: 수고했다. 간식은 없다.",
                    "본부: 잘했다. 미리 말해 두는데, 내일 암호는 더 길다.",
                },
                gameOver = new[]
                {
                    "본부: 기지 위치가 털렸다. 자네는 내일부터 감자 농장 근무다.",
                    "본부: 통신 두절. 다음 통신병 들어와.",
                    "본부: 적군이 우리 암호를 노래방 18번으로 쓰기 시작했다. 수고했다.",
                },
                sfxGood = new[]
                {
                    "본부: 소리 흉내 합격. 다들 진짜인 줄 알고 돌아봤다.",
                    "본부: 그 소리 어디서 배웠나. 아니, 안 물어보겠다.",
                },
                sfxBad = new[]
                {
                    "본부: 방금 무슨 소리였나? 아니, 대답하지 마.",
                    "본부: 효과음 불일치. 기지 고양이가 놀라서 도망갔다.",
                    "본부: 그 소리 듣고 적군이 구급차를 불렀다.",
                },
                heardBad = new[]
                {
                    "본부: '{heard}'? 우리 암호책에 그런 말은 없다.",
                    "본부: 해독 결과 '{heard}'. 내 고개가 45도로 꺾였다.",
                    "본부: '{heard}'라니. 점심 메뉴를 보낸 건가?",
                    "본부: 기다린 건 '{target}', 도착한 건 '{heard}'.",
                },
                heardGood = new[]
                {
                    "본부: '{heard}'로 들렸다. 비슷하니까 봐준다.",
                    "본부: '{heard}'. 아슬아슬하다. 오늘만 넘어간다.",
                },
                briefing = new[]
                {
                    "본부: 신입, 잘 왔다. 암호 장비가 고장 나서 오늘부터 자네 입이 무전기다.",
                    "본부: 오늘도 적이 엿듣고 있다. 또박또박, 빠르게.",
                    "본부: 어제보다 길다. 혀 꼬이면 기지도 꼬인다.",
                    "본부: 암호표 잘 봐 둬라. 적은 안 기다려 준다.",
                    "본부: 어제 송신 잘 들었다. 웃음 참느라 혼났다.",
                    "본부: 물 한 잔 마시고 시작해. 입이 마르면 암호도 마른다.",
                },
            };
        }
    }
}

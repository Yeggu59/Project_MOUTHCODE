using System;
using System.Collections.Generic;
using System.Text;

namespace MouthCode
{
    /// <summary>
    /// 한글 음절을 자모(초성·중성·종성)로 쪼개 편집 거리로 발음 정확도를 계산한다.
    /// 비슷한 소리(ㅂ/ㅃ/ㅍ, ㅐ/ㅔ, 받침 대표음 등)는 부분 점수를 준다.
    /// </summary>
    public static class Hangul
    {
        const int SBase = 0xAC00;
        const int SLast = 0xD7A3;

        // 초성: ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ
        static readonly int[] ChoGroup = { 0, 0, 2, 3, 3, 5, 6, 7, 7, 9, 9, 11, 12, 12, 12, 0, 3, 7, 18 };
        // 중성: ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ  (ㅐ=ㅔ, ㅒ=ㅖ, ㅙ=ㅚ=ㅞ)
        static readonly int[] JungGroup = { 0, 1, 2, 3, 4, 1, 6, 3, 8, 9, 10, 10, 12, 13, 14, 10, 16, 17, 18, 19, 20 };
        // 종성 대표음 7개: ㄱ ㄴ ㄷ ㄹ ㅁ ㅂ ㅇ
        static readonly int[] JongGroup =
        {
            0, 1, 1, 1, 4, 4, 4, 7, 8, 1, 16, 8, 8, 8, 17, 8,
            16, 17, 17, 7, 7, 21, 7, 7, 1, 7, 17, 7,
        };

        static readonly string ChoChars = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";

        public static bool IsSyllable(char c) => c >= SBase && c <= SLast;

        /// <summary>한글 음절만 남긴다 (공백·문장부호·영문·숫자 제거).</summary>
        public static string KeepSyllables(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
                if (IsSyllable(c)) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>토큰 = kind*100 + index. kind 0 초성, 1 중성, 2 종성(있을 때만).</summary>
        public static List<int> ToJamo(string s)
        {
            var list = new List<int>(s.Length * 3);
            foreach (var c in s)
            {
                if (!IsSyllable(c)) continue;
                int idx = c - SBase;
                int cho = idx / 588;
                int jung = (idx % 588) / 28;
                int jong = idx % 28;
                list.Add(cho);
                list.Add(100 + jung);
                if (jong > 0) list.Add(200 + jong);
            }
            return list;
        }

        public static int SyllableCount(string s) => KeepSyllables(s).Length;

        /// <summary>
        /// 두 음절이 이어질 때 소리가 바뀌는지 (자음동화). 예: 깡+르 → [깡느], 딱+냥 → [땅냥].
        /// 이런 조합은 적힌 대로 읽기 어렵고 음성인식도 바뀐 소리로 받아써서 불공평하다 → 단어 자동 생성 시 피한다.
        /// </summary>
        public static bool ChangesSound(char prev, char next)
        {
            if (!IsSyllable(prev) || !IsSyllable(next)) return false;
            int jong = (prev - SBase) % 28;
            int cho = (next - SBase) / 588;
            if (jong == 0) return false;
            int jg = JongGroup[jong];
            if (cho == 5 && jg != 8) return true;                       // 받침(ㄹ 제외) + ㄹ → ㄴ
            if ((cho == 2 || cho == 6) && (jg == 1 || jg == 7 || jg == 17)) return true; // ㄱ·ㄷ·ㅂ + ㄴ·ㅁ → 비음화
            if (cho == 2 && jg == 8) return true;                       // ㄹ + ㄴ → ㄹㄹ
            return false;
        }

        public static bool HasSoundChange(string word)
        {
            var s = KeepSyllables(word);
            for (int i = 0; i + 1 < s.Length; i++)
                if (ChangesSound(s[i], s[i + 1])) return true;
            return false;
        }

        /// <summary>된소리 초성 ㄲ ㄸ ㅃ ㅆ ㅉ</summary>
        static bool IsTense(int cho) => cho == 1 || cho == 4 || cho == 8 || cho == 10 || cho == 13;

        static float SubCost(int a, int b)
        {
            if (a == b) return 0f;
            int ka = a / 100, kb = b / 100;
            if (ka != kb) return 1f;
            int ia = a % 100, ib = b % 100;
            switch (ka)
            {
                // 같은 계열 자음(ㅂ/ㅃ/ㅍ 등): 음성인식이 된소리를 예사소리·거센소리로 자주 받아써서
                // 된소리가 낀 혼동은 거의 정답으로 본다 (사용자 QA: 된소리 판정이 너무 빡빡함)
                case 0: return ChoGroup[ia] != ChoGroup[ib] ? 1f : (IsTense(ia) || IsTense(ib)) ? 0.15f : 0.4f;
                case 1: return JungGroup[ia] == JungGroup[ib] ? 0.15f : 1f;
                default: return JongGroup[ia] == JongGroup[ib] ? 0.1f : 1f;
            }
        }

        static float InsDelCost(int token) => token >= 200 ? 0.7f : 1f; // 받침 누락은 조금 덜 아프게

        /// <summary>원문에 없는 소리를 더 말한 경우의 비용 배율 (빠뜨린 것보다 덜 아프게).</summary>
        const float ExtraWeight = 0.5f;

        /// <summary>자모 단위 가중 편집 거리. a = 원문, b = 수신.</summary>
        public static float Distance(List<int> a, List<int> b)
        {
            int n = a.Count, m = b.Count;
            var prev = new float[m + 1];
            var cur = new float[m + 1];
            prev[0] = 0;
            for (int j = 1; j <= m; j++) prev[j] = prev[j - 1] + InsDelCost(b[j - 1]) * ExtraWeight;
            for (int i = 1; i <= n; i++)
            {
                cur[0] = prev[0] + InsDelCost(a[i - 1]);
                for (int j = 1; j <= m; j++)
                {
                    float del = prev[j] + InsDelCost(a[i - 1]);
                    float ins = cur[j - 1] + InsDelCost(b[j - 1]) * ExtraWeight;
                    float sub = prev[j - 1] + SubCost(a[i - 1], b[j - 1]);
                    cur[j] = Math.Min(sub, Math.Min(del, ins));
                }
                var t = prev; prev = cur; cur = t;
            }
            return prev[m];
        }

        /// <summary>0~1 정확도. target 기준으로 정규화.</summary>
        public static float Similarity(string target, string heard)
        {
            var t = ToJamo(KeepSyllables(target));
            var h = ToJamo(KeepSyllables(heard));
            if (t.Count == 0) return h.Count == 0 ? 1f : 0f;
            if (h.Count == 0) return 0f;
            float total = 0f;
            foreach (var tok in t) total += InsDelCost(tok);
            float d = Distance(t, h);
            float sim = 1f - d / total;
            return sim < 0f ? 0f : (sim > 1f ? 1f : sim);
        }

        /// <summary>
        /// 원문 음절마다 어떤 음절로 들렸는지 정렬한다 (글자별 맞음/틀림 표시용).
        /// scores[i] = 원문 i번째 음절의 정확도(0~1), heardFor[i] = 대응된 수신 음절 ("" = 누락).
        /// </summary>
        public static void AlignSyllables(string target, string heard, out float[] scores, out string[] heardFor)
        {
            var t = KeepSyllables(target);
            var h = KeepSyllables(heard);
            int n = t.Length, m = h.Length;
            scores = new float[n];
            heardFor = new string[n];
            for (int i = 0; i < n; i++) heardFor[i] = "";
            if (n == 0) return;

            var sim = new float[n, m];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++)
                    sim[i, j] = Similarity(t[i].ToString(), h[j].ToString());

            var D = new float[n + 1, m + 1];
            var move = new byte[n + 1, m + 1]; // 0 diag, 1 del(target), 2 ins(heard)
            for (int i = 1; i <= n; i++) { D[i, 0] = i; move[i, 0] = 1; }
            for (int j = 1; j <= m; j++) { D[0, j] = j * 0.6f; move[0, j] = 2; }
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                {
                    float diag = D[i - 1, j - 1] + (1f - sim[i - 1, j - 1]);
                    float del = D[i - 1, j] + 1f;
                    float ins = D[i, j - 1] + 0.6f;
                    if (diag <= del && diag <= ins) { D[i, j] = diag; move[i, j] = 0; }
                    else if (del <= ins) { D[i, j] = del; move[i, j] = 1; }
                    else { D[i, j] = ins; move[i, j] = 2; }
                }
            int a = n, b = m;
            while (a > 0 || b > 0)
            {
                byte mv = move[a, b];
                if (a > 0 && b > 0 && mv == 0)
                {
                    scores[a - 1] = sim[a - 1, b - 1];
                    heardFor[a - 1] = h[b - 1].ToString();
                    a--; b--;
                }
                else if (a > 0 && (mv == 1 || b == 0)) { a--; }
                else { b--; }
            }
        }

        /// <summary>초성 문자열 (디버그/연출용).</summary>
        public static string Initials(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                if (!IsSyllable(c)) { sb.Append(c); continue; }
                sb.Append(ChoChars[(c - SBase) / 588]);
            }
            return sb.ToString();
        }
    }
}

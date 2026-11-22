using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MouthCode
{
    public class MessageSpec
    {
        public bool isSfx;
        public List<List<GlyphDef>> words = new List<List<GlyphDef>>(); // 단어별 글자
        public string text = "";   // "뿌르딱 깡깡"
        public SfxDef sfx;
        public float timeLimit;
        public int GlyphCount => words.Sum(w => w.Count);
    }

    public class DayPlan
    {
        public int day;
        public List<GlyphDef> unlockedGlyphs = new List<GlyphDef>();
        public List<GlyphDef> newGlyphs = new List<GlyphDef>();
        public List<SfxDef> unlockedSfx = new List<SfxDef>();
        public List<SfxDef> newSfx = new List<SfxDef>();
        public List<MessageSpec> messages = new List<MessageSpec>();
        public List<string> vocabulary = new List<string>(); // Whisper 인식 힌트
    }

    /// <summary>
    /// 하루치 메시지를 만든다. 난이도는 주로 메시지 길이(글자 수)로 올린다.
    /// 새 글자는 콘텐츠 일정대로 천천히 늘고, 다 나온 뒤에도 길이·시간 압박으로 무한히 이어진다.
    /// </summary>
    public static class DayPlanner
    {
        public static DayPlan Build(int day, GameConfig cfg, ContentDatabase db, int seed, System.Func<SfxDef, float> sfxDuration)
        {
            var rng = new System.Random(seed);
            var plan = new DayPlan { day = day };

            plan.unlockedGlyphs = db.glyphs.Where(g => g != null && !string.IsNullOrWhiteSpace(g.sound) && g.unlockDay <= day).ToList();
            plan.newGlyphs = plan.unlockedGlyphs.Where(g => g.unlockDay == day).ToList();
            plan.unlockedSfx = db.sfx.Where(s => s != null && s.unlockDay <= day).ToList();
            plan.newSfx = plan.unlockedSfx.Where(s => s.unlockDay == day).ToList();

            if (plan.unlockedGlyphs.Count == 0)
            {
                Debug.LogError("[MouthCode] 해금된 글자가 없습니다. ContentDatabase의 glyphs를 확인하세요.");
                plan.unlockedGlyphs.Add(new GlyphDef { sound = "아", unlockDay = 1, shapeSeed = 1 });
            }

            var bySound = new Dictionary<char, GlyphDef>();
            foreach (var g in plan.unlockedGlyphs)
            {
                var s = Hangul.KeepSyllables(g.sound);
                if (s.Length > 0 && !bySound.ContainsKey(s[0])) bySound[s[0]] = g;
            }

            // 사용 가능한 단어
            var pool = new List<List<GlyphDef>>();
            foreach (var w in db.words)
            {
                if (w == null || string.IsNullOrWhiteSpace(w.text) || w.minDay > day) continue;
                var gl = ToGlyphs(w.text, bySound);
                if (gl != null && gl.Count > 0) pool.Add(gl);
            }
            while (pool.Count < 6) pool.Add(RandomWord(rng, plan.unlockedGlyphs, null, 3));
            var newPool = pool.Where(w => w.Any(g => plan.newGlyphs.Contains(g))).ToList();

            plan.vocabulary = pool.Select(Text).Distinct().Take(40).ToList();

            int count = cfg.MessagesForDay(day);
            float baseLen = cfg.LengthForDay(day);
            int maxWord = Mathf.Max(1, cfg.wordMaxGlyphs);
            float mult = cfg.TimeMultiplier(day);

            // 메시지 종류 결정 (첫 메시지는 항상 글자, 효과음은 하루 메시지의 sfxMaxShare 비율까지, 최소 1개 자리)
            var pendingSfx = new Queue<SfxDef>(plan.newSfx);
            int maxSfx = Mathf.Max(1, Mathf.FloorToInt(count * cfg.sfxMaxShare));
            int sfxCount = 0;
            var pendingGlyphs = new List<GlyphDef>(plan.newGlyphs);

            for (int i = 0; i < count; i++)
            {
                bool makeSfx = false;
                SfxDef sfx = null;
                if (i > 0 && sfxCount < maxSfx && plan.unlockedSfx.Count > 0)
                {
                    if (pendingSfx.Count > 0) { sfx = pendingSfx.Dequeue(); makeSfx = true; }
                    else if (rng.NextDouble() < cfg.sfxMessageChance) { sfx = plan.unlockedSfx[rng.Next(plan.unlockedSfx.Count)]; makeSfx = true; }
                }

                var msg = new MessageSpec();
                if (makeSfx)
                {
                    sfxCount++;
                    msg.isSfx = true;
                    msg.sfx = sfx;
                    msg.text = sfx.displayName;
                    float dur = sfxDuration != null ? sfxDuration(sfx) : 1f;
                    msg.timeLimit = cfg.SfxTimeLimit(dur, day);
                }
                else
                {
                    // 이 메시지의 글자 수: 그날 평균 ± 흔들림
                    int length = Mathf.RoundToInt(baseLen + (float)(rng.NextDouble() * 2 - 1) * cfg.lengthJitter);
                    length = Mathf.Clamp(length, 2, Mathf.Max(2, cfg.maxGlyphsPerMessage));

                    // 오늘 새 글자가 꼭 등장하도록 먼저 넣는다
                    if (pendingGlyphs.Count > 0)
                    {
                        var target = pendingGlyphs[0];
                        var withNew = pool.Where(w => w.Contains(target) && w.Count <= Mathf.Min(length, maxWord)).ToList();
                        var word = withNew.Count > 0 ? withNew[rng.Next(withNew.Count)] : RandomWord(rng, plan.unlockedGlyphs, target, Mathf.Min(length, maxWord));
                        msg.words.Add(word);
                        pendingGlyphs.RemoveAll(g => word.Contains(g));
                    }
                    // 남은 글자 수만큼 단어를 채운다 (새 글자가 든 단어를 자주 섞어 익히게 한다)
                    int guard = 0;
                    while (msg.GlyphCount < length && guard++ < 60)
                    {
                        int remain = Mathf.Min(length - msg.GlyphCount, maxWord);
                        var source = newPool.Count > 0 && rng.NextDouble() < cfg.newGlyphBias ? newPool : pool;
                        var fits = source.Where(w => w.Count <= remain).ToList();
                        var word = fits.Count > 0 && rng.NextDouble() < 0.75 ? fits[rng.Next(fits.Count)] : RandomWord(rng, plan.unlockedGlyphs, null, remain);
                        msg.words.Add(word);
                    }
                    Finish(msg, cfg, mult);
                }
                plan.messages.Add(msg);
            }

            // 오늘 새 효과음인데 자리가 없어서 빠졌다면 마지막 메시지를 효과음으로 바꾼다
            if (pendingSfx.Count > 0 && plan.messages.Count > 1)
            {
                var sfx = pendingSfx.Dequeue();
                float dur = sfxDuration != null ? sfxDuration(sfx) : 1f;
                var last = plan.messages[plan.messages.Count - 1];
                if (!last.isSfx) pendingGlyphs.AddRange(last.words.SelectMany(w => w).Where(g => g.unlockDay == day).Distinct());
                plan.messages[plan.messages.Count - 1] = new MessageSpec { isSfx = true, sfx = sfx, text = sfx.displayName, timeLimit = cfg.SfxTimeLimit(dur, day) };
            }

            // 새 글자가 아직 한 번도 안 나왔다면 마지막 글자 메시지에 붙인다
            var lastGlyphMsg = plan.messages.LastOrDefault(m => !m.isSfx);
            if (lastGlyphMsg != null)
            {
                foreach (var g in pendingGlyphs.Distinct().ToList())
                {
                    if (plan.messages.Any(m => !m.isSfx && m.words.Any(w => w.Contains(g)))) continue;
                    lastGlyphMsg.words.Add(new List<GlyphDef> { g });
                }
                Finish(lastGlyphMsg, cfg, mult);
            }
            return plan;
        }

        static void Finish(MessageSpec msg, GameConfig cfg, float mult)
        {
            msg.text = string.Join(" ", msg.words.Select(Text));
            msg.timeLimit = (cfg.timeBase + cfg.timePerGlyph * msg.GlyphCount) * mult;
        }

        public static string Text(List<GlyphDef> word) => string.Concat(word.Select(g => Hangul.KeepSyllables(g.sound)));

        static List<GlyphDef> ToGlyphs(string text, Dictionary<char, GlyphDef> bySound)
        {
            var list = new List<GlyphDef>();
            foreach (var c in text)
            {
                if (!Hangul.IsSyllable(c)) continue;
                if (!bySound.TryGetValue(c, out var g)) return null;
                list.Add(g);
            }
            return list;
        }

        /// <summary>해금된 글자로 maxLen 이하 단어를 만든다 (가능하면 2글자 이상).</summary>
        static List<GlyphDef> RandomWord(System.Random rng, List<GlyphDef> glyphs, GlyphDef mustInclude, int maxLen)
        {
            List<GlyphDef> w = null;
            maxLen = Mathf.Max(1, maxLen);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int len = maxLen >= 2 ? 2 + rng.Next(maxLen - 1) : 1;
                w = new List<GlyphDef>();
                for (int i = 0; i < len; i++) w.Add(glyphs[rng.Next(glyphs.Count)]);
                if (mustInclude != null) w[rng.Next(w.Count)] = mustInclude;
                if (!Hangul.HasSoundChange(Text(w))) break; // 자음동화 생기는 조합은 다시 뽑는다
            }
            return w;
        }
    }
}

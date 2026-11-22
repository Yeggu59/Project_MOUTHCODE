using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Whisper;
using Whisper.Native;

namespace MouthCode.EditorTools
{
    /// <summary>
    /// 개발용 자가 점검 (판정 로직 보정에 사용).
    /// Unity -batchmode -projectPath . -executeMethod MouthCode.EditorTools.MouthCodeSelfTest.Run
    ///       -mouthcodeOut &lt;결과 폴더&gt; [-mouthcodeWavDir &lt;wav 폴더, 파일명 = 원문.wav&gt;]
    /// </summary>
    public static class MouthCodeSelfTest
    {
        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        [MenuItem("MouthCode/Dev/Run Self Test", priority = 100)]
        public static void RunFromMenu() => RunInternal(Path.Combine(Application.temporaryCachePath, "selftest"), null, false);

        public static void Run()
        {
            string outDir = Arg("-mouthcodeOut") ?? Path.Combine(Application.temporaryCachePath, "selftest");
            RunInternal(outDir, Arg("-mouthcodeWavDir"), true);
        }

        static void RunInternal(string outDir, string wavDir, bool exit)
        {
            Directory.CreateDirectory(outDir);
            var sb = new StringBuilder();
            int code = 0;
            try
            {
                HangulTests(sb);
                PlannerTests(sb);
                SfxTests(sb, outDir);
                GlyphSheet(outDir);
                var sfxVoiceDir = Arg("-mouthcodeSfxVoiceDir");
                SfxWeightSweep(sb, sfxVoiceDir);
                if (!string.IsNullOrEmpty(sfxVoiceDir)) SfxVoiceTests(sb, sfxVoiceDir);
                if (!string.IsNullOrEmpty(wavDir)) WhisperTests(sb, wavDir);
            }
            catch (Exception e)
            {
                sb.AppendLine("EXCEPTION: " + e);
                code = 1;
            }
            File.WriteAllText(Path.Combine(outDir, "selftest.txt"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[SelfTest]\n" + sb);
            if (exit) EditorApplication.Exit(code);
        }

        static void HangulTests(StringBuilder sb)
        {
            sb.AppendLine("== Hangul similarity ==");
            var cases = new (string t, string h)[]
            {
                ("뿌르딱 깡깡 쀼", "뿌르딱 깡깡 쀼"),
                ("뿌르딱 깡깡 쀼", "뿌르딱, 깡깡, 쀼."),
                ("뿌르딱 깡깡 쀼", "부르딱 강강 뷰"),
                ("뿌르딱 깡깡 쀼", "뿌르다 깡까 쀼"),
                ("뿌르딱 깡깡 쀼", "푸르다 강가"),
                ("뿌르딱 깡깡 쀼", "감사합니다"),
                ("뿌르딱 깡깡 쀼", "뿌르딱 깡깡 쀼 뿌르딱 깡깡 쀼"),
                ("뿌르딱 깡깡 쀼", ""),
                ("꽥꽥", "깩깩"),
                ("냥냥", "냥냥냥"),
            };
            foreach (var (t, h) in cases)
            {
                Hangul.AlignSyllables(t, h, out var scores, out var heardFor);
                sb.AppendLine($"  '{t}' vs '{h}' => {Hangul.Similarity(t, h):0.000}   per-syl [{string.Join(",", scores.Select(s => s.ToString("0.00")))}] heard [{string.Join(",", heardFor)}]");
            }
        }

        static void PlannerTests(StringBuilder sb)
        {
            sb.AppendLine("== DayPlanner ==");
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/MouthCode/Resources/ContentDatabase.asset") ?? ScriptableObject.CreateInstance<ContentDatabase>();
            var risky = db.words.Where(w => Hangul.HasSoundChange(w.text)).Select(w => w.text).ToList();
            sb.AppendLine("  words with sound change (자음동화): " + (risky.Count == 0 ? "none" : string.Join(", ", risky)));
            foreach (int day in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 15, 20, 30 })
            {
                var plan = DayPlanner.Build(day, cfg, db, 12345 + day, d => 1f);
                var lens = plan.messages.Where(m => !m.isSfx).Select(m => m.GlyphCount).ToList();
                sb.AppendLine($"  DAY {day}: glyphs {plan.unlockedGlyphs.Count} (+{plan.newGlyphs.Count} {string.Join("", plan.newGlyphs.Select(g => g.sound))}), sfx {plan.unlockedSfx.Count} (+{plan.newSfx.Count}), messages {plan.messages.Count}, length {(lens.Count > 0 ? lens.Min() + "~" + lens.Max() : "-")}");
                foreach (var m in plan.messages)
                    sb.AppendLine($"     {(m.isSfx ? "[SFX] " + m.sfx.displayName : m.text),-30} limit {m.timeLimit:0.0}s");
                foreach (var g in plan.newGlyphs)
                    if (!plan.messages.Any(m => !m.isSfx && m.words.Any(w => w.Contains(g))))
                        sb.AppendLine($"     !! new glyph {g.sound} never appears");
            }
        }

        // 사람이 흉내 낸 소리를 대략 흉내 내는 합성 (성대 펄스 + 포먼트 강조)
        static float[] Voice(int rate, float dur, Func<float, float> f0, Func<float, float> amp, float noise = 0f, int seed = 1)
        {
            int n = (int)(dur * rate);
            var s = new float[n];
            var rng = new System.Random(seed);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float f = f0(t);
                ph += 2 * Math.PI * f / rate;
                float v = 0;
                for (int k = 1; k <= 24; k++)
                {
                    float fk = k * f;
                    if (fk > rate / 2f) break;
                    float formant = 1f + 3f * Mathf.Exp(-Mathf.Pow((fk - 650f) / 250f, 2)) + 1.5f * Mathf.Exp(-Mathf.Pow((fk - 1300f) / 300f, 2));
                    v += Mathf.Sin(k * (float)ph) / k * formant;
                }
                v += ((float)rng.NextDouble() * 2 - 1) * noise;
                s[i] = v * amp(t);
            }
            return AudioUtil.Normalize(s, 0.5f, 100f);
        }

        static float Env(float t, float dur) => Mathf.Clamp01(t / 0.04f) * Mathf.Clamp01((dur - t) / 0.1f);

        static void SfxTests(StringBuilder sb, string outDir)
        {
            sb.AppendLine("== SFX judge distances (rows = imitation, cols = reference) ==");
            const int R = AudioUtil.JudgeRate;
            var presets = (SfxPreset[])Enum.GetValues(typeof(SfxPreset));
            var refs = presets.Select(p => AudioUtil.Trim(SfxSynthesizer.Generate(p, R), R, 0.002f, 0.02f)).ToArray();

            var imit = new Dictionary<SfxPreset, float[]>
            {
                [SfxPreset.RisingSweep] = Voice(R, 1.0f, t => 140f * Mathf.Pow(2.6f, t / 1.0f), t => Env(t, 1.0f)),
                [SfxPreset.FallingWhoop] = Voice(R, 1.1f, t => 300f * Mathf.Pow(0.35f, t / 1.1f), t => Env(t, 1.1f)),
                [SfxPreset.Beeps] = Voice(R, 0.75f, t => 320f, t => (t % 0.27f) < 0.15f ? 1f : 0f),
                [SfxPreset.Trill] = Voice(R, 1.3f, t => 120f, t => Env(t, 1.3f) * (0.3f + 0.7f * Mathf.Max(0, Mathf.Sin(2 * Mathf.PI * 25f * t))), 0.05f),
                [SfxPreset.Fart] = Voice(R, 0.9f, t => 95f + 20f * Mathf.Sin(2 * Mathf.PI * 7f * t), t => Env(t, 0.9f), 0.4f, 7),
                [SfxPreset.Boing] = Voice(R, 1.1f, t => 210f * (1f + 0.2f * Mathf.Sin(2 * Mathf.PI * 8f * t) * Mathf.Exp(-1.5f * t)), t => Mathf.Exp(-1.2f * t) * Mathf.Clamp01(t / 0.02f)),
            };
            sb.Append("".PadRight(14));
            foreach (var p in presets) sb.Append(p.ToString().PadLeft(13));
            sb.AppendLine();
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            var diag = new List<float>();
            var off = new List<float>();
            foreach (var row in presets)
            {
                sb.Append(("imit:" + row).PadRight(14));
                var trimmed = AudioUtil.Trim(imit[row], R, cfg.silenceRms, 0.02f);
                for (int c = 0; c < presets.Length; c++)
                {
                    float d = SfxJudge.Distance(refs[c], trimmed);
                    (row == presets[c] ? diag : off).Add(d);
                    sb.Append(d.ToString("0.000").PadLeft(13));
                }
                sb.AppendLine();
            }
            sb.AppendLine($"  imitation→own ref: mean {diag.Average():0.000}, max {diag.Max():0.000}");
            sb.AppendLine($"  imitation→other ref: mean {off.Average():0.000}, min {off.Min():0.000}");
            sb.AppendLine($"  current thresholds good {cfg.sfxDistanceGood} / bad {cfg.sfxDistanceBad}");
            foreach (var row in presets)
            {
                var r = SfxJudge.Judge(SfxSynthesizer.Generate(row, R), imit[row], cfg);
                sb.AppendLine($"  Judge({row}) imitation accuracy = {r.accuracy:0.00} (d={r.distance:0.000})");
            }
            // 자기 자신 / 늘린 것 / 잡음
            var noise = new float[R];
            var rng = new System.Random(3);
            for (int i = 0; i < noise.Length; i++) noise[i] = ((float)rng.NextDouble() * 2 - 1) * 0.3f;
            sb.AppendLine($"  self distance RisingSweep: {SfxJudge.Distance(refs[0], refs[0]):0.000}");
            sb.AppendLine($"  white noise vs RisingSweep: {SfxJudge.Distance(refs[0], noise):0.000}");

            for (int i = 0; i < presets.Length; i++)
            {
                var tex = Spectrogram.Build(refs[i], R, 320, 110);
                File.WriteAllBytes(Path.Combine(outDir, $"spec_{presets[i]}.png"), tex.EncodeToPNG());
                var tex2 = Spectrogram.Build(AudioUtil.Trim(imit[presets[i]], R, 0.01f), R, 320, 110);
                File.WriteAllBytes(Path.Combine(outDir, $"spec_{presets[i]}_imit.png"), tex2.EncodeToPNG());
            }
        }

        static void GlyphSheet(string outDir)
        {
            var db = ScriptableObject.CreateInstance<ContentDatabase>();
            int n = db.glyphs.Count + db.sfx.Count;
            const int S = 128;
            int cols = 8;
            int rows = (n + cols - 1) / cols;
            var sheet = new Texture2D(cols * S, rows * S, TextureFormat.RGBA32, false);
            var bg = new Color[sheet.width * sheet.height];
            for (int i = 0; i < bg.Length; i++) bg[i] = new Color(0.035f, 0.06f, 0.045f);
            sheet.SetPixels(bg);
            int k = 0;
            foreach (var spr in db.glyphs.Select(g => GlyphArt.Get(g)).Concat(db.sfx.Select(s => GlyphArt.Get(s))))
            {
                var px = spr.texture.GetPixels();
                int x0 = (k % cols) * S, y0 = (rows - 1 - k / cols) * S;
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        var c = px[y * S + x];
                        var b = sheet.GetPixel(x0 + x, y0 + y);
                        sheet.SetPixel(x0 + x, y0 + y, Color.Lerp(b, new Color(c.r, c.g, c.b, 1), c.a));
                    }
                k++;
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(outDir, "glyph_sheet.png"), sheet.EncodeToPNG());
        }

        // ───────────── Whisper ─────────────

        static float[] ReadWav(string path, out int rate)
        {
            var bytes = File.ReadAllBytes(path);
            rate = BitConverter.ToInt32(bytes, 24);
            int channels = BitConverter.ToInt16(bytes, 22);
            int bits = BitConverter.ToInt16(bytes, 34);
            int pos = 12;
            while (pos + 8 <= bytes.Length)
            {
                string id = Encoding.ASCII.GetString(bytes, pos, 4);
                int size = BitConverter.ToInt32(bytes, pos + 4);
                if (id == "data")
                {
                    int n = size / (bits / 8);
                    var s = new float[n];
                    for (int i = 0; i < n; i++)
                        s[i] = bits == 16 ? BitConverter.ToInt16(bytes, pos + 8 + i * 2) / 32768f : (bytes[pos + 8 + i] - 128) / 128f;
                    return AudioUtil.Mixdown(s, channels);
                }
                pos += 8 + size;
            }
            throw new Exception("no data chunk: " + path);
        }

        static float[] AddNoise(float[] s, float snrDb, int seed)
        {
            var rng = new System.Random(seed);
            float rms = AudioUtil.Rms(s, 0, s.Length);
            float nAmp = rms / Mathf.Pow(10f, snrDb / 20f) * 1.7f; // 균등분포 RMS 보정
            var r = new float[s.Length];
            float lp = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float w = ((float)rng.NextDouble() * 2 - 1) * nAmp;
                lp += (w - lp) * 0.3f; // 웅성거림 비슷하게 약간 저역 쪽으로
                r[i] = s[i] + lp * 1.6f;
            }
            return r;
        }

        static void WhisperTests(StringBuilder sb, string wavDir)
        {
            sb.AppendLine("== Whisper (게임과 같은 판정 코어 JudgeCore 사용) ==");
            string model = Path.Combine(Application.streamingAssetsPath, "Whisper/ggml-base.bin");
            var db = ScriptableObject.CreateInstance<ContentDatabase>();
            var setups = new (string tag, bool gpu, int ctx)[] { ("GPU", true, 0), ("CPU", false, 0) };
            var rng = new System.Random(1);

            // 원문별 녹음 (파일명 = 원문, 밑줄 = 공백, "__" 뒤는 실제로 한 말)
            var clips = new List<(string name, string target, float[] s)>();
            foreach (var file in Directory.GetFiles(wavDir, "*.wav").OrderBy(f => f))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                string target = name.Split(new[] { "__" }, StringSplitOptions.None)[0].Replace("_", " ");
                var raw = ReadWav(file, out int rate);
                var s16 = AudioUtil.Resample(raw, rate, AudioUtil.JudgeRate);
                clips.Add((name, target, s16));
            }

            foreach (var (tag, gpu, ctx) in setups)
            {
                var cfg = ScriptableObject.CreateInstance<GameConfig>();
                cfg.whisperUseGpu = gpu;
                cfg.whisperAudioCtx = ctx;
                cfg.whisperTimeBudget = 2.5f;
                var cp = WhisperContextParams.GetDefaultParams();
                cp.UseGpu = gpu;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var w = WhisperWrapper.InitFromFile(model, cp);
                if (w == null) { sb.AppendLine($"  [{tag}] model load FAILED"); continue; }
                long load = sw.ElapsedMilliseconds;
                sw.Restart();
                w.GetText(new float[32000], AudioUtil.JudgeRate, 1, SpeechJudge.MakeParams(cfg, ""));
                sb.AppendLine($"  [{tag}] load {load} ms, warm-up {sw.ElapsedMilliseconds} ms");

                var accs = new List<float>();
                var times = new List<float>();
                foreach (var (name, target, s16) in clips)
                {
                    var variants = tag == "GPU"
                        ? new (string v, float[] s)[]
                        {
                            ("orig", s16),
                            ("noise15dB", AddNoise(s16, 15f, 3)),
                            ("noise8dB", AddNoise(s16, 8f, 4)),
                            ("fast1.15", AudioUtil.Resample(s16, AudioUtil.JudgeRate, (int)(AudioUtil.JudgeRate / 1.15f))),
                            ("slow0.88", AudioUtil.Resample(s16, AudioUtil.JudgeRate, (int)(AudioUtil.JudgeRate / 0.88f))),
                        }
                        : new (string v, float[] s)[] { ("orig", s16) };
                    foreach (var (vtag, vs) in variants)
                    {
                        var trimmed = AudioUtil.Trim(vs, AudioUtil.JudgeRate, cfg.silenceRms);
                        var plan = DayPlanner.Build(3, cfg, db, 1, d => 1f);
                        var syll = db.glyphs.Where(g => g.unlockDay <= 10).Select(g => g.sound).ToList();
                        var v = SpeechJudge.JudgeCore(w, trimmed, target, plan.vocabulary, syll, cfg, rng);
                        if (!name.Contains("__")) { accs.Add(v.accuracy); times.Add(v.seconds); }
                        sb.AppendLine($"    {vtag,-9} {name}: heard='{v.heard}' acc={v.accuracy:0.00} passes={v.passes} {v.seconds * 1000:0} ms");
                    }
                }
                sb.AppendLine($"  [{tag}] 정답 발화 평균 정확도 {accs.Average():0.00}, 평균 판정 시간 {times.Average() * 1000:0} ms, 최대 {times.Max() * 1000:0} ms");
            }
        }

        static Dictionary<SfxPreset, float[]> SyntheticImitations(int R) => new Dictionary<SfxPreset, float[]>
        {
            [SfxPreset.RisingSweep] = Voice(R, 1.0f, t => 140f * Mathf.Pow(2.6f, t / 1.0f), t => Env(t, 1.0f)),
            [SfxPreset.FallingWhoop] = Voice(R, 1.1f, t => 300f * Mathf.Pow(0.35f, t / 1.1f), t => Env(t, 1.1f)),
            [SfxPreset.Beeps] = Voice(R, 0.75f, t => 320f, t => (t % 0.27f) < 0.15f ? 1f : 0f),
            [SfxPreset.Trill] = Voice(R, 1.3f, t => 120f, t => Env(t, 1.3f) * (0.3f + 0.7f * Mathf.Max(0, Mathf.Sin(2 * Mathf.PI * 25f * t))), 0.05f),
            [SfxPreset.Fart] = Voice(R, 0.9f, t => 95f + 20f * Mathf.Sin(2 * Mathf.PI * 7f * t), t => Env(t, 0.9f), 0.4f, 7),
            [SfxPreset.Boing] = Voice(R, 1.1f, t => 210f * (1f + 0.2f * Mathf.Sin(2 * Mathf.PI * 8f * t) * Mathf.Exp(-1.5f * t)), t => Mathf.Exp(-1.2f * t) * Mathf.Clamp01(t / 0.02f)),
        };

        /// <summary>효과음 판정 특징 가중치 비교: 자기 원본과 가깝고 다른 원본과 멀수록 좋다.</summary>
        static void SfxWeightSweep(StringBuilder sb, string voiceDir)
        {
            sb.AppendLine("== SFX feature weight sweep ==");
            const int R = AudioUtil.JudgeRate;
            var presets = (SfxPreset[])Enum.GetValues(typeof(SfxPreset));
            var refs = presets.Select(p => AudioUtil.Trim(SfxSynthesizer.Generate(p, R), R, 0.002f, 0.02f)).ToArray();
            var synth = SyntheticImitations(R);
            var sets = new List<(string tag, Dictionary<SfxPreset, float[]> data)> { ("synthVoice", synth.ToDictionary(k => k.Key, k => AudioUtil.Trim(k.Value, R, 0.012f, 0.02f))) };
            if (!string.IsNullOrEmpty(voiceDir))
            {
                var tts = new Dictionary<SfxPreset, float[]>();
                foreach (var p in presets)
                {
                    string f = Path.Combine(voiceDir, p + ".wav");
                    if (File.Exists(f)) tts[p] = AudioUtil.Trim(AudioUtil.Resample(ReadWav(f, out int rate), rate, R), R, 0.012f, 0.02f);
                }
                sets.Add(("ttsVoice", tts));
            }
            var saved = (e: SfxJudge.EnergyWeight, c: SfxJudge.CentroidWeight, p: SfxJudge.PitchWeight, v: SfxJudge.VoicedWeight, d: SfxJudge.DurationWeight);
            var configs = new (string name, float e, float c, float p, float v, float d)[]
            {
                ("old", 1f, 0.6f, 1f, 0.25f, 0.3f),
                ("lowCent", 1f, 0.3f, 1f, 0.25f, 0.3f),
                ("noCent", 1f, 0f, 1.2f, 0.25f, 0.3f),
                ("pitchHeavy", 0.8f, 0.3f, 1.6f, 0.2f, 0.3f),
                ("energyHeavy", 1.4f, 0.3f, 1f, 0.25f, 0.4f),
            };
            foreach (var cfgW in configs)
            {
                SfxJudge.EnergyWeight = cfgW.e; SfxJudge.CentroidWeight = cfgW.c; SfxJudge.PitchWeight = cfgW.p;
                SfxJudge.VoicedWeight = cfgW.v; SfxJudge.DurationWeight = cfgW.d;
                foreach (var (tag, data) in sets)
                {
                    var own = new List<float>(); var other = new List<float>(); int ranked = 0;
                    foreach (var row in data.Keys)
                    {
                        var ds = presets.Select((p, i) => SfxJudge.Distance(refs[i], data[row])).ToArray();
                        int idx = Array.IndexOf(presets, row);
                        own.Add(ds[idx]);
                        for (int i = 0; i < ds.Length; i++) if (i != idx) other.Add(ds[i]);
                        if (ds[idx] <= ds.Min() + 1e-6f) ranked++;
                    }
                    sb.AppendLine($"  {cfgW.name,-12} {tag,-10} own {own.Average():0.000} (max {own.Max():0.00})  other {other.Average():0.000} (min {other.Min():0.00})  own-closest {ranked}/{data.Count}");
                }
            }
            SfxJudge.EnergyWeight = saved.e; SfxJudge.CentroidWeight = saved.c; SfxJudge.PitchWeight = saved.p; SfxJudge.VoicedWeight = saved.v; SfxJudge.DurationWeight = saved.d;
        }

        /// <summary>효과음을 사람 목소리(TTS)로 흉내 낸 녹음으로 효과음 판정 기준값을 점검한다.</summary>
        static void SfxVoiceTests(StringBuilder sb, string dir)
        {
            sb.AppendLine("== SFX judge with TTS voice imitations (rows = 흉내, cols = 원본) ==");
            const int R = AudioUtil.JudgeRate;
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            var presets = (SfxPreset[])Enum.GetValues(typeof(SfxPreset));
            var refs = presets.Select(p => AudioUtil.Trim(SfxSynthesizer.Generate(p, R), R, 0.002f, 0.02f)).ToArray();
            sb.Append("".PadRight(14));
            foreach (var p in presets) sb.Append(p.ToString().PadLeft(13));
            sb.AppendLine();
            var own = new List<float>();
            var other = new List<float>();
            foreach (var row in presets)
            {
                string f = Path.Combine(dir, row + ".wav");
                if (!File.Exists(f)) continue;
                var s = AudioUtil.Resample(ReadWav(f, out int rate), rate, R);
                var t = AudioUtil.Trim(s, R, cfg.silenceRms, 0.02f);
                sb.Append(("voice:" + row).PadRight(14));
                for (int c = 0; c < presets.Length; c++)
                {
                    float d = SfxJudge.Distance(refs[c], t);
                    (row == presets[c] ? own : other).Add(d);
                    sb.Append(d.ToString("0.000").PadLeft(13));
                }
                sb.AppendLine();
            }
            if (own.Count > 0)
            {
                sb.AppendLine($"  voice→own ref: mean {own.Average():0.000} [{string.Join(", ", own.Select(x => x.ToString("0.00")))}]");
                sb.AppendLine($"  voice→other ref: mean {other.Average():0.000}, min {other.Min():0.000}");
                foreach (var row in presets)
                {
                    string f = Path.Combine(dir, row + ".wav");
                    if (!File.Exists(f)) continue;
                    var s = AudioUtil.Resample(ReadWav(f, out int rate), rate, R);
                    var r = SfxJudge.Judge(SfxSynthesizer.Generate(row, R), s, cfg);
                    sb.AppendLine($"  Judge({row}) TTS voice accuracy = {r.accuracy:0.00} (d={r.distance:0.000})");
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MouthCode
{
    /// <summary>UI·무전기 효과음 (전부 코드로 합성). 실제 사운드가 생기면 여기만 바꾸면 된다.</summary>
    public static class UiSound
    {
        const int Rate = 22050;
        static AudioSource src;
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        static readonly System.Random Rng = new System.Random(12345);

        public static float Volume = 0.55f;

        public static void Init(GameObject host)
        {
            if (src != null) return;
            src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
        }

        public static void Click() => Play("click", Make("click", () => Tone(0.035f, t => 1800f, 0.4f, 0.002f, 0.02f)), 0.35f);
        public static void Tick() => Play("tick", Make("tick", () => Tone(0.05f, t => 1400f, 0.5f, 0.002f, 0.03f)), 0.5f);
        public static void PttOn() => Play("ptton", Make("ptton", PttOnClip), 0.6f);
        public static void PttOff() => Play("pttoff", Make("pttoff", RogerBeep), 0.6f);
        public static void Good() => Play("good", Make("good", Arp), 0.6f);
        public static void Bad() => Play("bad", Make("bad", WahWah), 0.6f);
        public static void Alarm() => Play("alarm", Make("alarm", Siren), 0.55f);
        public static void Stamp() => Play("stamp", Make("stamp", Thump), 0.9f);
        public static void Blip() => Play("blip", Make("blip", () => Tone(0.025f, t => 2400f, 0.25f, 0.001f, 0.015f)), 0.25f);
        public static void Static() => Play("static", Make("static", () => Noise(0.35f, 0.5f)), 0.35f);
        /// <summary>문서를 책상에 내려놓는 소리.</summary>
        public static void Paper() => Play("paper", Make("paper", PaperSlide), 0.55f);
        /// <summary>버튼 위에 마우스를 올렸을 때 아주 작은 딸깍.</summary>
        public static void Hover()
        {
            if (Time.unscaledTime - lastHover < 0.05f) return;
            lastHover = Time.unscaledTime;
            Play("hover", Make("hover", () => Tone(0.018f, t => 3200f, 0.18f, 0.001f, 0.012f)), 0.18f);
        }
        static float lastHover = -1f;

        static void Play(string key, AudioClip clip, float vol)
        {
            if (src == null || clip == null) return;
            src.PlayOneShot(clip, vol * Volume);
        }

        static AudioClip Make(string key, Func<float[]> gen)
        {
            if (Clips.TryGetValue(key, out var c) && c != null) return c;
            var data = gen();
            c = AudioClip.Create("ui_" + key, Math.Max(1, data.Length), 1, Rate, false);
            c.SetData(data, 0);
            Clips[key] = c;
            return c;
        }

        static float Env(float t, float dur, float a, float r)
        {
            if (t < a) return t / a;
            if (t > dur - r) return Mathf.Max(0, (dur - t) / r);
            return 1f;
        }

        static float[] Tone(float dur, Func<float, float> freq, float amp, float a, float r, bool square = false)
        {
            int n = (int)(dur * Rate);
            var s = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                ph += 2 * Math.PI * freq(t) / Rate;
                float v = Mathf.Sin((float)ph);
                if (square) v = Mathf.Sign(v) * 0.6f;
                s[i] = v * amp * Env(t, dur, a, r);
            }
            return s;
        }

        static float[] Noise(float dur, float amp)
        {
            int n = (int)(dur * Rate);
            var s = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float w = (float)Rng.NextDouble() * 2 - 1;
                lp += (w - lp) * 0.5f;
                s[i] = lp * amp * Env(t, dur, 0.01f, 0.1f);
            }
            return s;
        }

        static float[] Concat(params float[][] parts)
        {
            int n = 0; foreach (var p in parts) n += p.Length;
            var r = new float[n]; int o = 0;
            foreach (var p in parts) { Array.Copy(p, 0, r, o, p.Length); o += p.Length; }
            return r;
        }

        static float[] Mix(float[] a, float[] b)
        {
            var r = new float[Math.Max(a.Length, b.Length)];
            for (int i = 0; i < r.Length; i++) r[i] = (i < a.Length ? a[i] : 0) + (i < b.Length ? b[i] : 0);
            return r;
        }

        static float[] PttOnClip() => Mix(Noise(0.09f, 0.35f), Tone(0.09f, t => 600f + 4000f * t, 0.25f, 0.003f, 0.03f));
        static float[] RogerBeep() => Concat(Tone(0.07f, t => 1250f, 0.4f, 0.003f, 0.01f), Tone(0.08f, t => 900f, 0.4f, 0.003f, 0.02f), Noise(0.12f, 0.25f));
        static float[] Arp() => Concat(Tone(0.08f, t => 523f, 0.35f, 0.005f, 0.02f, true), Tone(0.08f, t => 659f, 0.35f, 0.005f, 0.02f, true), Tone(0.2f, t => 784f, 0.35f, 0.005f, 0.12f, true));
        static float[] WahWah() => Concat(Tone(0.22f, t => 330f - 40f * t, 0.35f, 0.01f, 0.05f, true), Tone(0.45f, t => 262f - 60f * t, 0.35f, 0.01f, 0.2f, true));
        static float[] Siren() => Tone(0.8f, t => (Mathf.Repeat(t, 0.2f) < 0.1f) ? 880f : 660f, 0.35f, 0.01f, 0.08f, true);
        static float[] PaperSlide()
        {
            int n = (int)(0.3f * Rate);
            var s = new float[n];
            float lp = 0, prev = 0, crackle = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float w = (float)(Rng.NextDouble() * 2 - 1);
                lp += (w - lp) * 0.4f;              // 저역 통과
                float hp = lp - prev; prev = lp;    // 고역 통과 → 종이가 쓸리는 '샥'
                if (Rng.NextDouble() < 0.003) crackle = 1f;
                crackle *= 0.993f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.55f));
                s[i] = hp * (1.4f + crackle * 2.2f) * env * 0.55f;
            }
            return s;
        }

        static float[] Thump() => Mix(Tone(0.25f, t => 110f - 200f * t, 0.9f, 0.002f, 0.2f), Noise(0.06f, 0.6f));

        static float Midi(float m) => 440f * Mathf.Pow(2f, (m - 69f) / 12f);

        /// <summary>
        /// 배경 음악 (코드로 작곡한 임시 곡). 단조 첩보물 느낌 + 우스운 미끄럼 소리. 8마디 루프.
        /// 송신 화면에서는 GameRoot가 꺼서 마이크에 섞이지 않게 한다.
        /// </summary>
        public static AudioClip Bgm()
        {
            if (Clips.TryGetValue("bgm", out var c) && c != null) return c;
            const float bpm = 132f;
            float beat = 60f / bpm;
            int bars = 8;
            int n = (int)(bars * 4 * beat * Rate);
            var s = new float[n];

            void Note(float startBeat, float lenBeats, float m0, float m1, float amp, int wave)
            {
                int i0 = (int)(startBeat * beat * Rate);
                int len = (int)(lenBeats * beat * Rate);
                double ph = 0;
                for (int i = 0; i < len && i0 + i < n; i++)
                {
                    float t = (float)i / Rate;
                    float u = (float)i / len;
                    ph += 2 * Math.PI * Midi(Mathf.Lerp(m0, m1, u)) / Rate;
                    float x = (float)(ph % (2 * Math.PI)) / (2f * Mathf.PI);
                    float v = wave == 0 ? (x < 0.5f ? 0.5f : -0.5f)            // 사각파 (멜로디)
                            : wave == 1 ? (4f * Mathf.Abs(x - 0.5f) - 1f)       // 삼각파 (베이스)
                            : Mathf.Sin((float)ph);
                    float env = Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((len - i) / (0.03f * Rate)) * Mathf.Exp(-t * (wave == 0 ? 6f : 3f));
                    s[i0 + i] += v * amp * env;
                }
            }

            void Hat(float startBeat, float amp)
            {
                int i0 = (int)(startBeat * beat * Rate);
                int len = (int)(0.03f * Rate);
                for (int i = 0; i < len && i0 + i < n; i++)
                    s[i0 + i] += ((float)Rng.NextDouble() * 2 - 1) * amp * (1f - (float)i / len);
            }

            // 코드 진행: Am F G E | Am F G E (베이스 근음)
            int[] roots = { 45, 41, 43, 40, 45, 41, 43, 40 };
            int[] pattern = { 0, 0, 12, 0, 7, 0, 12, 10 };
            for (int b = 0; b < bars; b++)
            {
                for (int k = 0; k < 8; k++)
                    Note(b * 4 + k * 0.5f, 0.45f, roots[b] + pattern[k], roots[b] + pattern[k], 0.32f, 1);
                for (int k = 0; k < 4; k++) Hat(b * 4 + k + 0.5f, 0.06f);
                Note(b * 4, 0.4f, 33, 28, 0.35f, 2); // 쿵
            }

            // 멜로디 (시작 박, 길이, 음) — 짧게 끊어 치는 첩보물 느낌
            var mel = new (float at, float len, int m)[]
            {
                (0, .25f, 76), (.5f, .25f, 76), (1, .5f, 72), (2, .25f, 74), (2.5f, .25f, 76), (3, .5f, 69),
                (4, .25f, 77), (4.5f, .25f, 77), (5, .5f, 72), (6, .25f, 76), (6.5f, .25f, 74), (7, .5f, 72),
                (8, .25f, 74), (8.5f, .25f, 74), (9, .5f, 71), (10, .25f, 79), (10.5f, .25f, 77), (11, .5f, 74),
                (12, .75f, 76), (13, .25f, 80), (13.5f, .25f, 76), (14, .25f, 71),
                (16, .25f, 76), (16.5f, .25f, 76), (17, .5f, 72), (18, .25f, 74), (18.5f, .25f, 76), (19, .5f, 81),
                (20, .25f, 77), (20.5f, .25f, 77), (21, .5f, 72), (22, .25f, 76), (22.5f, .25f, 74), (23, .5f, 72),
                (24, .25f, 74), (24.5f, .25f, 74), (25, .5f, 71), (26, .25f, 79), (26.5f, .25f, 77), (27, .5f, 74),
                (28, .5f, 76), (28.5f, .5f, 75), (29, 1f, 76),
            };
            foreach (var (at, len, m) in mel) Note(at, len, m, m, 0.16f, 0);
            // 우스운 미끄럼 (뾰오옹↘ / 뿌우웅↗)
            Note(15f, 0.9f, 83, 64, 0.14f, 2);
            Note(30.5f, 1.4f, 57, 81, 0.12f, 2);

            var outS = AudioUtil.Normalize(s, 0.6f, 4f);
            c = AudioClip.Create("ui_bgm", n, 1, Rate, false);
            c.SetData(outS, 0);
            Clips["bgm"] = c;
            return c;
        }

        /// <summary>통신실 배경음 (낮은 험 + 희미한 잡음). 루프용.</summary>
        public static AudioClip Ambience()
        {
            if (Clips.TryGetValue("amb", out var c) && c != null) return c;
            float dur = 4f;
            int n = (int)(dur * Rate);
            var s = new float[n];
            var rng = new System.Random(9);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float hum = Mathf.Sin(2 * Mathf.PI * 60f * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 120f * t) * 0.25f;
                float w = (float)rng.NextDouble() * 2 - 1;
                lp += (w - lp) * 0.08f;
                float crackle = rng.NextDouble() < 0.0004 ? (float)(rng.NextDouble() * 2 - 1) : 0f;
                s[i] = hum * 0.25f + lp * 0.6f + crackle * 0.5f;
            }
            c = AudioClip.Create("ui_amb", n, 1, Rate, false);
            c.SetData(s, 0);
            Clips["amb"] = c;
            return c;
        }
    }
}

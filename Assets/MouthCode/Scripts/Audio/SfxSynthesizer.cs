using System;
using UnityEngine;

namespace MouthCode
{
    /// <summary>임시 효과음 합성기. 실제 효과음이 들어오면 SfxDef.clip으로 교체된다.</summary>
    public static class SfxSynthesizer
    {
        public static float[] Generate(SfxPreset preset, int rate)
        {
            float[] s;
            switch (preset)
            {
                case SfxPreset.RisingSweep: s = Sweep(rate, 0.9f, 260f, 1100f, 0.35f); break;
                case SfxPreset.FallingWhoop: s = Sweep(rate, 1.0f, 650f, 130f, 0.5f); break;
                case SfxPreset.Beeps: s = Beeps(rate); break;
                case SfxPreset.Trill: s = Trill(rate); break;
                case SfxPreset.Fart: s = Fart(rate); break;
                case SfxPreset.Boing: s = Boing(rate); break;
                default: s = Sweep(rate, 0.8f, 400f, 800f, 0.3f); break;
            }
            return AudioUtil.Normalize(s, 0.8f, 100f);
        }

        static float Env(float t, float dur, float attack = 0.03f, float release = 0.12f)
        {
            if (t < attack) return t / attack;
            if (t > dur - release) return Mathf.Max(0f, (dur - t) / release);
            return 1f;
        }

        static float Harm(float phase, float h2, float h3)
        {
            return Mathf.Sin(phase) + h2 * Mathf.Sin(2f * phase) + h3 * Mathf.Sin(3f * phase);
        }

        static float[] Sweep(int rate, float dur, float f0, float f1, float brightness)
        {
            int n = (int)(dur * rate);
            var s = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float u = t / dur;
                float f = f0 * Mathf.Pow(f1 / f0, u);
                phase += 2 * Math.PI * f / rate;
                s[i] = Harm((float)phase, brightness, brightness * 0.5f) * Env(t, dur);
            }
            return s;
        }

        static float[] Beeps(int rate)
        {
            float on = 0.13f, off = 0.1f;
            float dur = on * 3 + off * 2;
            int n = (int)(dur * rate);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float cycle = t % (on + off);
                if (cycle > on) continue;
                float e = Env(cycle, on, 0.008f, 0.02f);
                s[i] = Harm(2f * Mathf.PI * 1050f * t, 0.25f, 0.1f) * e;
            }
            return s;
        }

        static float[] Trill(int rate)
        {
            float dur = 1.2f;
            int n = (int)(dur * rate);
            var s = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float f = 170f + 8f * Mathf.Sin(2f * Mathf.PI * 3f * t);
                phase += 2 * Math.PI * f / rate;
                float saw = 0f;
                for (int k = 1; k <= 7; k++) saw += Mathf.Sin(k * (float)phase) / k;
                float trem = 0.35f + 0.65f * Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 24f * t));
                s[i] = saw * trem * Env(t, dur, 0.04f, 0.2f);
            }
            return s;
        }

        static float[] Fart(int rate)
        {
            float dur = 0.85f;
            int n = (int)(dur * rate);
            var s = new float[n];
            var rng = new System.Random(77);
            double phase = 0;
            float flutter = 1f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float f = 80f + 28f * Mathf.Sin(2f * Mathf.PI * 6.5f * t) - 25f * t;
                phase += 2 * Math.PI * f / rate;
                float pulse = 0f;
                for (int k = 1; k <= 12; k++) pulse += Mathf.Sin(k * (float)phase) / Mathf.Sqrt(k);
                if (i % (rate / 60) == 0) flutter = 0.55f + 0.45f * (float)rng.NextDouble();
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.35f;
                s[i] = (pulse * 0.4f + noise) * flutter * Env(t, dur, 0.02f, 0.25f);
            }
            return s;
        }

        static float[] Boing(int rate)
        {
            float dur = 1.0f;
            int n = (int)(dur * rate);
            var s = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float rise = Mathf.Clamp01(t / 0.12f);
                float baseF = Mathf.Lerp(260f, 430f, rise);
                float vib = 1f + 0.22f * Mathf.Sin(2f * Mathf.PI * 9f * t) * Mathf.Exp(-1.8f * t);
                float f = baseF * vib;
                phase += 2 * Math.PI * f / rate;
                s[i] = Harm((float)phase, 0.3f, 0.12f) * Mathf.Exp(-1.6f * t) * Env(t, dur, 0.01f, 0.1f);
            }
            return s;
        }
    }
}

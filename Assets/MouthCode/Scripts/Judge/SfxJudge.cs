using System;
using System.Collections.Generic;
using UnityEngine;

namespace MouthCode
{
    /// <summary>
    /// 효과음 따라 하기 판정.
    /// 음량 곡선 + 밝기(스펙트럼 중심) 곡선 + 음높이 곡선을 뽑아 DTW로 모양을 비교하고 길이 비율을 더한다.
    /// 음색은 보지 않는다 → "비슷한 리듬과 높낮이로 흉내 냈는가".
    /// </summary>
    public static class SfxJudge
    {
        const int N = 512;
        const int Hop = 256;
        const int Rate = AudioUtil.JudgeRate;

        // 특징 가중치 (자가 점검에서 여러 값을 비교해 정함)
        public static float EnergyWeight = 1f;
        public static float CentroidWeight = 0f;   // 목소리는 모음(포먼트)에 따라 밝기가 달라 합성음과 비교가 안 됨 → 끔
        public static float PitchWeight = 1.2f;
        public static float VoicedWeight = 0.25f;
        public static float DurationWeight = 0.3f;

        public struct Result
        {
            public float accuracy;
            public float distance;
            public bool noSound;
        }

        public static Result Judge(float[] reference, float[] player, GameConfig cfg, float silenceRms = -1f)
        {
            var r = new Result();
            var refT = AudioUtil.Trim(reference, Rate, 0.002f, 0.02f);
            var plT = AudioUtil.Trim(player, Rate, silenceRms > 0f ? silenceRms : cfg.silenceRms, 0.02f);
            if (plT.Length < Rate * 0.12f)
            {
                r.noSound = true;
                r.accuracy = 0f;
                r.distance = 99f;
                return r;
            }
            r.distance = Distance(refT, plT, cfg.sfxLengthTolerance);
            float raw = Mathf.Clamp01((cfg.sfxDistanceBad - r.distance) / Mathf.Max(0.01f, cfg.sfxDistanceBad - cfg.sfxDistanceGood));
            // 흉내는 원래 어렵다: 소리를 낸 것 자체에 기본 점수를 주고, 그 위를 거리로 채운다
            r.accuracy = Mathf.Lerp(cfg.sfxMinAccuracy, 1f, raw);
            return r;
        }

        /// <summary>이미 잘라낸 두 신호의 모양 거리 (0 = 동일). 길이가 tolerance배 안쪽이면 길이 감점 없음.</summary>
        public static float Distance(float[] a, float[] b, float lengthTolerance = 1f)
        {
            var fa = Features(a);
            var fb = Features(b);
            float d = Dtw(fa, fb);
            float ratio = Mathf.Max(1e-3f, (float)b.Length / Mathf.Max(1, a.Length));
            d += DurationWeight * Mathf.Max(0f, Mathf.Abs(Mathf.Log(ratio)) - Mathf.Log(Mathf.Max(1f, lengthTolerance)));
            return d;
        }

        public static List<float[]> Features(float[] s)
        {
            int frames = Math.Max(1, (s.Length - N) / Hop + 1);
            var dbs = new float[frames];
            var cent = new float[frames];
            var pitch = new float[frames];
            var voiced = new bool[frames];
            var re = new float[N];
            var im = new float[N];
            var mag = new float[N / 2];
            int minLag = Rate / 1000, maxLag = Rate / 70;
            float maxDb = -999f;

            for (int f = 0; f < frames; f++)
            {
                int off = f * Hop;
                float rms = AudioUtil.Rms(s, off, N);
                dbs[f] = 20f * Mathf.Log10(rms + 1e-6f);
                if (dbs[f] > maxDb) maxDb = dbs[f];

                Fft.Magnitudes(s, off, N, re, im, mag);
                double num = 0, den = 0;
                int lo = Mathf.Max(1, (int)(60f * N / Rate)), hi = Mathf.Min(mag.Length - 1, (int)(6000f * N / Rate));
                for (int k = lo; k <= hi; k++)
                {
                    double fr = (double)k * Rate / N;
                    num += fr * mag[k];
                    den += mag[k];
                }
                cent[f] = den > 1e-9 ? (float)(num / den) : 500f;

                // 자기상관 음높이 (정규화)
                float best = 0f; int bestLag = 0;
                int len = N - maxLag;
                if (off + N <= s.Length && len > 32)
                {
                    for (int lag = minLag; lag <= maxLag; lag++)
                    {
                        double xy = 0, xx = 0, yy = 0;
                        for (int i = 0; i < len; i += 2)
                        {
                            float x = s[off + i], y = s[off + i + lag];
                            xy += x * y; xx += x * x; yy += y * y;
                        }
                        if (xx < 1e-9 || yy < 1e-9) continue;
                        float c = (float)(xy / Math.Sqrt(xx * yy));
                        if (c > best) { best = c; bestLag = lag; }
                    }
                }
                voiced[f] = best > 0.55f && bestLag > 0;
                pitch[f] = voiced[f] ? (float)Rate / bestLag : 0f;
            }

            // 정규화: 음량은 피크 기준 0~1, 밝기·음높이는 자기 평균/중앙값 기준 옥타브 차이 (절대 음높이는 안 봄)
            var e = new float[frames];
            for (int f = 0; f < frames; f++) e[f] = Mathf.Clamp01((dbs[f] - maxDb + 40f) / 40f);

            double cSum = 0; int cN = 0;
            for (int f = 0; f < frames; f++) if (e[f] > 0.35f) { cSum += Math.Log(cent[f], 2); cN++; }
            float cMean = cN > 0 ? (float)(cSum / cN) : 9f;

            var vp = new List<float>();
            for (int f = 0; f < frames; f++) if (voiced[f] && e[f] > 0.3f) vp.Add(Mathf.Log(pitch[f], 2));
            vp.Sort();
            float pMed = vp.Count > 0 ? vp[vp.Count / 2] : 0f;
            float voicedRatio = frames > 0 ? (float)vp.Count / frames : 0f;

            var feats = new List<float[]>(frames);
            for (int f = 0; f < frames; f++)
            {
                float c = Mathf.Clamp(Mathf.Log(cent[f], 2) - cMean, -2f, 2f) * (e[f] > 0.2f ? 1f : 0.3f);
                float p = (voiced[f] && e[f] > 0.3f && voicedRatio > 0.2f) ? Mathf.Clamp(Mathf.Log(pitch[f], 2) - pMed, -1.5f, 1.5f) : 0f;
                feats.Add(new[] { e[f] * EnergyWeight, c * CentroidWeight, p * PitchWeight, (voiced[f] ? 1f : 0f) * VoicedWeight });
            }
            return feats;
        }

        static float Dtw(List<float[]> a, List<float[]> b)
        {
            int n = a.Count, m = b.Count;
            if (n == 0 || m == 0) return 99f;
            var D = new float[n + 1, m + 1];
            var L = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) for (int j = 0; j <= m; j++) D[i, j] = float.PositiveInfinity;
            D[0, 0] = 0;
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    float c = Dist(a[i - 1], b[j - 1]);
                    float best = D[i - 1, j - 1]; int bl = L[i - 1, j - 1];
                    if (D[i - 1, j] < best) { best = D[i - 1, j]; bl = L[i - 1, j]; }
                    if (D[i, j - 1] < best) { best = D[i, j - 1]; bl = L[i, j - 1]; }
                    D[i, j] = best + c;
                    L[i, j] = bl + 1;
                }
            }
            return D[n, m] / Mathf.Max(1, L[n, m]);
        }

        static float Dist(float[] x, float[] y)
        {
            float s = 0;
            for (int k = 0; k < x.Length; k++) { float d = x[k] - y[k]; s += d * d; }
            return Mathf.Sqrt(s);
        }
    }
}

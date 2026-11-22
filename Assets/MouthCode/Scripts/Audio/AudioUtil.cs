using System;
using System.Collections.Generic;
using UnityEngine;

namespace MouthCode
{
    public static class AudioUtil
    {
        public const int JudgeRate = 16000;

        public static float Rms(float[] s, int start, int len)
        {
            if (s == null || len <= 0) return 0f;
            int end = Math.Min(s.Length, start + len);
            double sum = 0;
            int n = 0;
            for (int i = Math.Max(0, start); i < end; i++) { sum += s[i] * s[i]; n++; }
            return n == 0 ? 0f : (float)Math.Sqrt(sum / n);
        }

        public static float Peak(float[] s)
        {
            float p = 0f;
            if (s == null) return p;
            foreach (var v in s) { float a = Math.Abs(v); if (a > p) p = a; }
            return p;
        }

        /// <summary>
        /// 리샘플링. 올릴 때는 선형 보간, 내릴 때는 구간 평균(간단한 저역 통과)으로
        /// 48kHz 마이크 → 16kHz 변환 시 고음이 음성 대역으로 접혀 들어오는 것(앨리어싱)을 줄인다.
        /// </summary>
        public static float[] Resample(float[] src, int srcRate, int dstRate)
        {
            if (src == null || src.Length == 0) return Array.Empty<float>();
            if (srcRate == dstRate) return (float[])src.Clone();
            int n = (int)((long)src.Length * dstRate / srcRate);
            var dst = new float[n];
            double step = (double)srcRate / dstRate;
            if (step > 1.0)
            {
                for (int i = 0; i < n; i++)
                {
                    int a = (int)(i * step);
                    int b = Math.Min(src.Length, Math.Max(a + 1, (int)((i + 1) * step)));
                    float sum = 0f;
                    for (int k = a; k < b; k++) sum += src[k];
                    dst[i] = sum / (b - a);
                }
                return dst;
            }
            for (int i = 0; i < n; i++)
            {
                double pos = i * step;
                int i0 = (int)pos;
                int i1 = Math.Min(i0 + 1, src.Length - 1);
                float t = (float)(pos - i0);
                dst[i] = src[i0] * (1f - t) + src[i1] * t;
            }
            return dst;
        }

        public static float[] Mixdown(float[] interleaved, int channels)
        {
            if (channels <= 1) return interleaved;
            int n = interleaved.Length / channels;
            var mono = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += interleaved[i * channels + c];
                mono[i] = sum / channels;
            }
            return mono;
        }

        /// <summary>앞뒤 무음을 잘라낸다. 소리가 전혀 없으면 빈 배열.</summary>
        public static float[] Trim(float[] s, int rate, float minRms, float padSeconds = 0.08f)
        {
            if (s == null || s.Length == 0) return Array.Empty<float>();
            int frame = Math.Max(1, rate / 50); // 20ms
            int frames = s.Length / frame;
            if (frames == 0) return Rms(s, 0, s.Length) >= minRms ? s : Array.Empty<float>();
            var rms = new float[frames];
            float peak = 0f;
            for (int f = 0; f < frames; f++)
            {
                rms[f] = Rms(s, f * frame, frame);
                if (rms[f] > peak) peak = rms[f];
            }
            float th = Math.Max(minRms, peak * 0.08f);
            int first = -1, last = -1;
            for (int f = 0; f < frames; f++) if (rms[f] >= th) { first = f; break; }
            for (int f = frames - 1; f >= 0; f--) if (rms[f] >= th) { last = f; break; }
            if (first < 0) return Array.Empty<float>();
            int pad = (int)(padSeconds * rate);
            int start = Math.Max(0, first * frame - pad);
            int end = Math.Min(s.Length, (last + 1) * frame + pad);
            var r = new float[end - start];
            Array.Copy(s, start, r, 0, r.Length);
            return r;
        }

        /// <summary>앞뒤에 무음을 붙여 최소 길이를 맞춘다 (Whisper는 1초 미만 입력을 무시함).</summary>
        public static float[] PadForWhisper(float[] s, int rate, float leadSeconds = 0.3f, float minSeconds = 1.6f)
        {
            int lead = (int)(leadSeconds * rate);
            int total = Math.Max(s.Length + lead * 2, (int)(minSeconds * rate));
            var r = new float[total];
            Array.Copy(s, 0, r, lead, s.Length);
            return r;
        }

        /// <summary>피크를 target으로 맞춘다 (작게 녹음된 목소리 보정).</summary>
        public static float[] Normalize(float[] s, float target = 0.7f, float maxGain = 12f)
        {
            float p = Peak(s);
            if (p < 1e-5f) return s;
            float g = Math.Min(maxGain, target / p);
            var r = new float[s.Length];
            for (int i = 0; i < s.Length; i++) r[i] = s[i] * g;
            return r;
        }

        public static AudioClip ToClip(float[] s, int rate, string name)
        {
            int len = Math.Max(1, s.Length);
            var clip = AudioClip.Create(name, len, 1, rate, false);
            if (s.Length > 0) clip.SetData(s, 0);
            return clip;
        }

        /// <summary>AudioClip → 16kHz 모노. 실패하면 null (Load Type이 Streaming이면 실패).</summary>
        public static float[] ClipToJudgeRate(AudioClip clip)
        {
            if (clip == null) return null;
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) return null;
            return Resample(Mixdown(data, clip.channels), clip.frequency, JudgeRate);
        }

        /// <summary>
        /// 음량 봉우리로 음절 수를 대충 센다. 음성인식 모델이 없을 때의 간이 판정용.
        /// </summary>
        public static int EstimateSyllables(float[] s, int rate)
        {
            int frame = rate / 100; // 10ms
            int frames = s.Length / frame;
            if (frames < 3) return 0;
            var env = new float[frames];
            for (int f = 0; f < frames; f++) env[f] = Rms(s, f * frame, frame);
            // 평활
            var sm = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                float sum = 0; int n = 0;
                for (int k = -3; k <= 3; k++) { int j = f + k; if (j >= 0 && j < frames) { sum += env[j]; n++; } }
                sm[f] = sum / n;
            }
            float peak = 0; foreach (var v in sm) peak = Math.Max(peak, v);
            if (peak <= 0) return 0;
            float th = peak * 0.25f;
            int count = 0;
            bool inSyl = false;
            float localMax = 0;
            int lastPeakFrame = -100;
            for (int f = 0; f < frames; f++)
            {
                if (!inSyl && sm[f] > th && f - lastPeakFrame > 8)
                {
                    inSyl = true; localMax = sm[f];
                }
                else if (inSyl)
                {
                    localMax = Math.Max(localMax, sm[f]);
                    if (sm[f] < localMax * 0.6f) { inSyl = false; count++; lastPeakFrame = f; }
                }
            }
            if (inSyl) count++;
            return count;
        }
    }

    public static class Fft
    {
        static readonly Dictionary<int, float[]> HannCache = new Dictionary<int, float[]>();

        public static float[] Hann(int n)
        {
            if (HannCache.TryGetValue(n, out var w)) return w;
            w = new float[n];
            for (int i = 0; i < n; i++) w[i] = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * i / (n - 1));
            HannCache[n] = w;
            return w;
        }

        /// <summary>제자리 radix-2 FFT. 길이는 2의 거듭제곱.</summary>
        public static void Transform(float[] re, float[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                float wr = (float)Math.Cos(ang), wi = (float)Math.Sin(ang);
                int half = len >> 1;
                for (int i = 0; i < n; i += len)
                {
                    float cr = 1f, ci = 0f;
                    for (int k = 0; k < half; k++)
                    {
                        int a = i + k, b = a + half;
                        float xr = re[b] * cr - im[b] * ci;
                        float xi = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - xr; im[b] = im[a] - xi;
                        re[a] += xr; im[a] += xi;
                        float ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = ncr;
                    }
                }
            }
        }

        /// <summary>s[offset..offset+n) 구간의 진폭 스펙트럼 (길이 n/2). 범위를 넘으면 0으로 채움.</summary>
        public static void Magnitudes(float[] s, int offset, int n, float[] re, float[] im, float[] mag)
        {
            var w = Hann(n);
            for (int i = 0; i < n; i++)
            {
                int k = offset + i;
                re[i] = (k >= 0 && k < s.Length ? s[k] : 0f) * w[i];
                im[i] = 0f;
            }
            Transform(re, im);
            int h = n / 2;
            for (int i = 0; i < h; i++) mag[i] = Mathf.Sqrt(re[i] * re[i] + im[i] * im[i]);
        }
    }
}

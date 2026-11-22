using UnityEngine;

namespace MouthCode
{
    /// <summary>무전기 오실로스코프: 마이크 파형을 실시간으로 그린다.</summary>
    public class Oscilloscope
    {
        public readonly Texture2D Texture;
        readonly int w, h;
        readonly Color32[] px;
        readonly float[] buf = new float[1024];
        static readonly Color32 Clear = new Color32(4, 12, 8, 255);
        static readonly Color32 Grid = new Color32(20, 50, 32, 255);

        public Oscilloscope(int width = 320, int height = 48)
        {
            w = width; h = height;
            px = new Color32[w * h];
            Texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Oscilloscope" };
        }

        public void Draw(MicRecorder mic, Color32 lineColor, float gain = 4f)
        {
            for (int i = 0; i < px.Length; i++) px[i] = Clear;
            int mid = h / 2;
            for (int x = 0; x < w; x += 16) for (int y = 0; y < h; y++) px[y * w + x] = Grid;
            for (int x = 0; x < w; x++) px[mid * w + x] = Grid;

            bool ok = mic != null && mic.ReadLatest(buf);
            int prevY = mid;
            for (int x = 0; x < w; x++)
            {
                float v = ok ? buf[x * buf.Length / w] * gain : 0f;
                int y = Mathf.Clamp(mid + Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * (h / 2 - 2)), 1, h - 2);
                int y0 = Mathf.Min(prevY, y), y1 = Mathf.Max(prevY, y);
                for (int yy = y0; yy <= y1; yy++) px[yy * w + x] = lineColor;
                prevY = y;
            }
            Texture.SetPixels32(px);
            Texture.Apply(false);
        }
    }

    /// <summary>마이크 입력을 실시간으로 그리는 스크롤 스펙트로그램 (이지 모드 효과음 따라 하기용).</summary>
    public class LiveSpectrogram
    {
        const int N = 512;
        public readonly Texture2D Texture;
        readonly int w, h;
        readonly float[] buf = new float[N], re = new float[N], im = new float[N], mag = new float[N / 2];
        readonly Color32[] column;
        int col;

        public LiveSpectrogram(int width = 200, int height = 96)
        {
            w = width; h = height;
            Texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                name = "LiveSpectrogram",
            };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Spectrogram.RampAt(0f);
            Texture.SetPixels32(px);
            Texture.Apply();
            column = new Color32[h];
        }

        /// <summary>가장 최근 소리로 한 칸 그린다. 돌려주는 값 = RawImage.uvRect.x (스크롤 위치).</summary>
        public float Push(MicRecorder mic, bool live)
        {
            if (live && mic != null && mic.ReadLatest(buf))
            {
                Fft.Magnitudes(buf, 0, N, re, im, mag);
                int rate = Mathf.Max(8000, mic.SampleRate);
                float maxF = Mathf.Min(6000f, rate * 0.5f - 1f);
                for (int y = 0; y < h; y++)
                {
                    float f = 80f * Mathf.Pow(maxF / 80f, y / (float)(h - 1));
                    float b = f / rate * N;
                    int b0 = Mathf.Clamp((int)b, 0, mag.Length - 1);
                    int b1 = Mathf.Min(b0 + 1, mag.Length - 1);
                    float m = Mathf.Lerp(mag[b0], mag[b1], b - b0);
                    float db = 20f * Mathf.Log10(m + 1e-6f);
                    column[y] = Spectrogram.RampAt(Mathf.InverseLerp(-12f, 38f, db));
                }
            }
            else
            {
                for (int y = 0; y < h; y++) column[y] = Spectrogram.RampAt(0f);
            }
            Texture.SetPixels32(col, 0, 1, h, column);
            Texture.Apply(false);
            col = (col + 1) % w;
            return col / (float)w;
        }
    }

    /// <summary>오디오 → 스펙트로그램 텍스처 (가로 = 시간, 세로 = 로그 주파수).</summary>
    public static class Spectrogram
    {
        const int N = 512;

        static readonly Color32[] Ramp = BuildRamp();

        public static Color32 RampAt(float v) => Ramp[Mathf.Clamp((int)(v * 255f), 0, 255)];

        static Color32[] BuildRamp()
        {
            var keys = new[]
            {
                (0.00f, new Color(0.03f, 0.06f, 0.05f)),
                (0.35f, new Color(0.02f, 0.30f, 0.20f)),
                (0.60f, new Color(0.15f, 0.75f, 0.35f)),
                (0.82f, new Color(0.80f, 0.95f, 0.30f)),
                (1.00f, new Color(1.00f, 1.00f, 0.75f)),
            };
            var ramp = new Color32[256];
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                int k = 0;
                while (k < keys.Length - 2 && t > keys[k + 1].Item1) k++;
                float u = Mathf.InverseLerp(keys[k].Item1, keys[k + 1].Item1, t);
                ramp[i] = Color.Lerp(keys[k].Item2, keys[k + 1].Item2, u);
            }
            return ramp;
        }

        public static Texture2D Build(float[] s, int rate, int width = 256, int height = 96,
                                      float minFreq = 80f, float maxFreq = 6000f, float rangeDb = 55f)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Spectrogram",
            };
            var px = new Color32[width * height];
            if (s == null || s.Length == 0)
            {
                for (int i = 0; i < px.Length; i++) px[i] = Ramp[0];
                tex.SetPixels32(px);
                tex.Apply();
                return tex;
            }

            var re = new float[N];
            var im = new float[N];
            var mag = new float[N / 2];
            var db = new float[width * height];
            float maxDb = -999f;
            float span = Mathf.Max(0, s.Length - N);
            maxFreq = Mathf.Min(maxFreq, rate * 0.5f - 1f);

            // 행마다 사용할 주파수 bin (로그 간격)
            var bins = new float[height];
            for (int y = 0; y < height; y++)
            {
                float f = minFreq * Mathf.Pow(maxFreq / minFreq, y / (float)(height - 1));
                bins[y] = f / rate * N;
            }

            for (int x = 0; x < width; x++)
            {
                int off = (int)(span * x / Mathf.Max(1, width - 1));
                Fft.Magnitudes(s, off, N, re, im, mag);
                for (int y = 0; y < height; y++)
                {
                    float b = bins[y];
                    int b0 = Mathf.Clamp((int)b, 0, mag.Length - 1);
                    int b1 = Mathf.Min(b0 + 1, mag.Length - 1);
                    float m = Mathf.Lerp(mag[b0], mag[b1], b - b0);
                    float d = 20f * Mathf.Log10(m + 1e-6f);
                    db[y * width + x] = d;
                    if (d > maxDb) maxDb = d;
                }
            }

            float floor = maxDb - rangeDb;
            for (int i = 0; i < db.Length; i++)
            {
                float v = Mathf.Clamp01((db[i] - floor) / rangeDb);
                px[i] = Ramp[(int)(v * 255f)];
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}

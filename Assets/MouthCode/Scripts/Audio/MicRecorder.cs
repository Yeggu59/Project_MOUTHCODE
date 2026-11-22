using System;
using UnityEngine;

namespace MouthCode
{
    /// <summary>
    /// 마이크를 링 버퍼로 계속 켜두고, 무전기 버튼을 누른 구간만 잘라 쓴다.
    /// 누르는 순간 Microphone.Start를 하면 말 앞부분이 잘리기 때문.
    /// </summary>
    public class MicRecorder : MonoBehaviour
    {
        const int RingSeconds = 24;
        const int LevelWindow = 1024;
        const string PrefKey = "mouthcode.mic";

        AudioClip ring;
        int ringSamples;
        int startPos;
        float recordStart;
        float preRoll;
        readonly float[] levelBuf = new float[LevelWindow];
        float lastRetry = -10f;
        string device; // null = 시스템 기본

        public bool HasDevice => Microphone.devices.Length > 0;
        public bool Active => ring != null && Microphone.IsRecording(device);
        public bool IsRecording { get; private set; }
        public int SampleRate { get; private set; }
        public float Level { get; private set; }          // RMS
        public float LevelNormalized => Mathf.Clamp01(Mathf.Sqrt(Level) * 2.2f);
        public float RecordedSeconds => IsRecording ? Time.unscaledTime - recordStart : 0f;
        public string DeviceName => device ?? "시스템 기본 마이크";

        void Awake()
        {
            var saved = PlayerPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(saved) && Array.IndexOf(Microphone.devices, saved) >= 0) device = saved;
        }

        /// <summary>다음 마이크로 바꾼다 (시스템 기본 → 장치1 → 장치2 … → 시스템 기본).</summary>
        public void NextDevice()
        {
            var list = Microphone.devices;
            int idx = device == null ? -1 : Array.IndexOf(list, device);
            idx++;
            End();
            device = idx >= list.Length ? null : list[idx];
            PlayerPrefs.SetString(PrefKey, device ?? "");
            Begin();
        }

        public bool Begin()
        {
            if (Active) return true;
            if (!HasDevice) return false;
            if (device != null && Array.IndexOf(Microphone.devices, device) < 0) device = null;
            try
            {
                Microphone.GetDeviceCaps(device, out int min, out int max);
                int rate = 16000;
                if (min != 0 || max != 0) rate = Mathf.Clamp(16000, Mathf.Max(1, min), Mathf.Max(min, max));
                ring = Microphone.Start(device, true, RingSeconds, rate);
                if (ring == null) return false;
                SampleRate = ring.frequency;
                ringSamples = ring.samples;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MouthCode] 마이크 시작 실패: " + e.Message);
                ring = null;
                return false;
            }
        }

        void End()
        {
            IsRecording = false;
            if (ring != null) Microphone.End(device);
            ring = null;
            Level = 0f;
        }

        void Update()
        {
            if (!Active)
            {
                Level = 0f;
                if (Time.unscaledTime - lastRetry > 2f) { lastRetry = Time.unscaledTime; Begin(); }
                return;
            }
            int pos = Microphone.GetPosition(device);
            if (pos <= 0 && !IsRecording) { Level = 0f; return; }
            int off = pos - LevelWindow;
            if (off < 0) off += ringSamples;
            ring.GetData(levelBuf, off);
            double sum = 0;
            for (int i = 0; i < LevelWindow; i++) sum += levelBuf[i] * levelBuf[i];
            float rms = (float)Math.Sqrt(sum / LevelWindow);
            Level = Mathf.Max(rms, Level * 0.85f); // 바늘이 천천히 떨어지게

            // 주변 소음 추적: 조용해지면 빨리 내려가고, 시끄러우면 아주 천천히 올라간다 (말소리는 거의 반영 안 됨)
            if (!IsRecording && rms > 0f)
                NoiseFloor = Mathf.Lerp(NoiseFloor, rms, rms < NoiseFloor ? 0.2f : 0.0006f);
        }

        /// <summary>주변 소음 크기(RMS) 추정치.</summary>
        public float NoiseFloor { get; private set; } = 0.004f;

        /// <summary>판정에서 "소리 없음"으로 볼 기준. 시끄러운 곳에서는 자동으로 올라간다.</summary>
        public float SilenceThreshold(float configured) => Mathf.Max(configured, NoiseFloor * 2.5f);

        /// <summary>0 = 조용함, 1 = 보통, 2 = 시끄러움</summary>
        public int NoiseLevel => NoiseFloor < 0.006f ? 0 : NoiseFloor < 0.02f ? 1 : 2;

        /// <summary>가장 최근 buf.Length 샘플(모노)을 읽는다.</summary>
        public bool ReadLatest(float[] buf)
        {
            if (!Active || ring.channels != 1 || buf.Length >= ringSamples) return false;
            int pos = Microphone.GetPosition(device);
            int off = pos - buf.Length;
            if (off < 0) off += ringSamples;
            return ring.GetData(buf, off);
        }

        public void StartRecording(float preRollSeconds)
        {
            if (!Active) Begin();
            if (!Active) return;
            preRoll = Mathf.Max(0f, preRollSeconds);
            int pos = Microphone.GetPosition(device);
            int pre = (int)(preRoll * SampleRate);
            startPos = ((pos - pre) % ringSamples + ringSamples) % ringSamples;
            recordStart = Time.unscaledTime;
            IsRecording = true;
        }

        /// <summary>녹음 종료. 16kHz 모노 샘플을 돌려준다.</summary>
        public float[] StopRecording(float maxSeconds)
        {
            if (!IsRecording) return Array.Empty<float>();
            IsRecording = false;
            if (!Active) return Array.Empty<float>();
            int end = Microphone.GetPosition(device);
            int len = ((end - startPos) % ringSamples + ringSamples) % ringSamples;
            int cap = (int)((maxSeconds + preRoll) * SampleRate);
            len = Mathf.Clamp(len, 0, Mathf.Min(cap, ringSamples - SampleRate));
            if (len <= 0) return Array.Empty<float>();
            int ch = Mathf.Max(1, ring.channels);
            var data = new float[len * ch];
            ring.GetData(data, startPos); // 끝을 넘으면 자동으로 처음부터 이어 읽음
            var mono = AudioUtil.Mixdown(data, ch);
            return AudioUtil.Resample(mono, SampleRate, AudioUtil.JudgeRate);
        }

        void OnDestroy() => End();
    }
}

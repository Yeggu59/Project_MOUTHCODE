using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Whisper;
using Whisper.Native;
using Debug = UnityEngine.Debug;

namespace MouthCode
{
    public class SpeechVerdict
    {
        public float accuracy;
        public string heard = "";      // 인식 원문
        public string heardClean = ""; // 한글 음절만
        public bool noSpeech;
        public bool usedWhisper;
        public float seconds;          // 판정에 걸린 시간
        public int passes;             // 받아쓰기 횟수
    }

    /// <summary>
    /// 암호 발음 판정. Whisper(오프라인)로 한국어 받아쓰기 → 자모 편집 거리.
    /// 모델이 없거나 로드 실패면 음절 수만 비교하는 간이 판정으로 대체한다.
    /// </summary>
    public class SpeechJudge : MonoBehaviour
    {
        public enum Status { NoModel, Loading, Ready, Failed }

        WhisperWrapper whisper;
        GameConfig cfg;
        bool gpuRequested;
        float warmupSeconds = -1f;
        readonly System.Random rng = new System.Random();

        public Status State { get; private set; } = Status.NoModel;

        public string StatusText
        {
            get
            {
                switch (State)
                {
                    case Status.Loading: return "음성 판독기 켜는 중…";
                    case Status.Ready:
                        return warmupSeconds < 0 ? "음성 판독기 예열 중…"
                            : $"음성 판독기 준비 완료 ({(gpuRequested ? "GPU" : gpuDisabledByCrash ? "CPU, 지난번 GPU 오류로 전환" : "CPU")})";
                    case Status.Failed: return "음성 판독기 고장. 글자 수만 세는 간이 판정으로 진행";
                    default: return $"음성 판독기 없음 (StreamingAssets/{cfg?.whisperModelPath}). 간이 판정으로 진행";
                }
            }
        }

        public void Init(GameConfig config)
        {
            cfg = config;
            string full = Path.Combine(Application.streamingAssetsPath, cfg.whisperModelPath);
            if (!File.Exists(full))
            {
                State = Status.NoModel;
                Debug.LogWarning("[MouthCode] Whisper 모델 파일 없음: " + full + "  → 메뉴 MouthCode > Download Whisper Model");
                return;
            }
            State = Status.Loading;
            Load(full);
        }

        const string GpuInitKey = "mouthcode.gpuInitInProgress";
        const string GpuDisabledKey = "mouthcode.gpuDisabled";
        bool gpuDisabledByCrash;

        async void Load(string path)
        {
            try
            {
                // GPU 드라이버 문제로 지난번 실행이 GPU 초기화 도중 죽었다면(표시가 남아 있으면) 그 뒤로는 CPU만 쓴다.
                // 정상 종료(OnApplicationQuit)에서는 표시를 지우므로 일찍 끈 경우는 해당 없음.
                // 명령줄 -cpu 로 CPU 강제, -gpu 로 다시 GPU 허용.
                var args = Environment.GetCommandLineArgs();
                if (PlayerPrefs.GetInt(GpuInitKey, 0) == 1)
                {
                    PlayerPrefs.SetInt(GpuDisabledKey, 1);
                    PlayerPrefs.SetInt(GpuInitKey, 0);
                    Debug.LogWarning("[MouthCode] 지난 실행이 GPU 초기화 중 비정상 종료됨 → 이 PC에서는 CPU 사용 (-gpu 로 해제)");
                }
                if (Array.IndexOf(args, "-gpu") >= 0) PlayerPrefs.SetInt(GpuDisabledKey, 0);
                gpuDisabledByCrash = PlayerPrefs.GetInt(GpuDisabledKey, 0) == 1;
                bool forceCpu = Array.IndexOf(args, "-cpu") >= 0;
                gpuRequested = cfg.whisperUseGpu && !gpuDisabledByCrash && !forceCpu;
                var cp = WhisperContextParams.GetDefaultParams();
                cp.UseGpu = gpuRequested;
                // 첫 추론(예열)까지 무사히 끝나야 GPU가 정상인 것으로 보고 표시를 지운다
                PlayerPrefs.SetInt(GpuInitKey, gpuRequested ? 1 : 0);
                PlayerPrefs.Save();
                whisper = await WhisperWrapper.InitFromFileAsync(path, cp);
                if (whisper == null && gpuRequested)
                {
                    Debug.LogWarning("[MouthCode] GPU로 Whisper 로드 실패 → CPU로 재시도");
                    gpuRequested = false;
                    cp.UseGpu = false;
                    whisper = await WhisperWrapper.InitFromFileAsync(path, cp);
                }
                if (whisper == null)
                {
                    // 모델 파일 문제 등 GPU와 무관한 실패: "GPU 초기화 중" 표시를 남기지 않는다
                    PlayerPrefs.SetInt(GpuInitKey, 0);
                    PlayerPrefs.Save();
                    State = Status.Failed;
                    return;
                }
                State = Status.Ready;

                // 첫 판정이 느리지 않도록 무음으로 한 번 돌려둔다 (GPU는 첫 실행 때 셰이더 준비로 오래 걸림)
                var sw = Stopwatch.StartNew();
                var silence = new float[AudioUtil.JudgeRate * 2];
                var w = whisper;
                var p = MakeParams(cfg, "");
                await Task.Run(() => w.GetText(silence, AudioUtil.JudgeRate, 1, p));
                warmupSeconds = (float)sw.Elapsed.TotalSeconds;
                PlayerPrefs.SetInt(GpuInitKey, 0);
                PlayerPrefs.Save();
                Debug.Log($"[MouthCode] Whisper 준비 완료 (GPU 요청 {gpuRequested}, 예열 {warmupSeconds:0.00}s)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MouthCode] Whisper 로드 실패: " + e);
                PlayerPrefs.SetInt(GpuInitKey, 0);
                PlayerPrefs.Save();
                State = whisper != null ? Status.Ready : Status.Failed;
            }
        }

        void OnApplicationQuit()
        {
            // 정상 종료면 "GPU 초기화 중" 표시를 지운다 (비정상 종료와 구분)
            if (PlayerPrefs.GetInt(GpuInitKey, 0) == 1) { PlayerPrefs.SetInt(GpuInitKey, 0); PlayerPrefs.Save(); }
        }

        void OnDestroy()
        {
            whisper = null; // 네이티브 컨텍스트는 래퍼 파이널라이저가 정리
        }

        public static WhisperParams MakeParams(GameConfig cfg, string prompt, bool beam = false)
        {
            var p = WhisperParams.GetDefaultParams(beam ? WhisperSamplingStrategy.WHISPER_SAMPLING_BEAM_SEARCH : WhisperSamplingStrategy.WHISPER_SAMPLING_GREEDY);
            p.Language = cfg.whisperLanguage;
            p.Translate = false;
            p.NoContext = true;
            p.SingleSegment = true;
            p.InitialPrompt = prompt;
            p.AudioCtx = cfg.whisperAudioCtx;
            TweakNative(p, cfg.whisperMaxTokens, 0f);
            return p;
        }

        static readonly System.Reflection.FieldInfo ParamField =
            typeof(WhisperParams).GetField("_param", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        static readonly System.Reflection.FieldInfo MaxTokensField =
            typeof(WhisperNativeParams).GetField("max_tokens", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        static readonly System.Reflection.FieldInfo TempIncField =
            typeof(WhisperNativeParams).GetField("temperature_inc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        static bool tweakWarned;

        /// <summary>
        /// 패키지가 열어주지 않는 디코딩 옵션을 바꾼다.
        /// - max_tokens: 짧은 암호문인데 "pppp…"처럼 끝없이 반복하는 경우를 끊는다.
        /// - temperature_inc = 0: 실패 시 온도를 올려 다시 푸는 재시도를 끈다 (이게 2~4초 지연의 원인). 힌트를 바꿔 여러 번 푸므로 필요 없음.
        /// 실패해도 기본값으로 동작한다.
        /// </summary>
        static void TweakNative(WhisperParams p, int maxTokens, float temperatureInc)
        {
            try
            {
                if (ParamField == null || MaxTokensField == null || TempIncField == null) throw new MissingFieldException("WhisperNativeParams");
                object boxed = ParamField.GetValue(p);
                if (maxTokens > 0) MaxTokensField.SetValue(boxed, maxTokens);
                TempIncField.SetValue(boxed, temperatureInc);
                ParamField.SetValue(p, boxed);
            }
            catch (Exception e)
            {
                if (!tweakWarned) { tweakWarned = true; Debug.LogWarning("[MouthCode] Whisper 디코딩 옵션 조정 실패(기본값 사용): " + e.Message); }
            }
        }

        static List<string> Shuffled(IList<string> src, System.Random rng)
        {
            var list = new List<string>(src);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        /// <summary>
        /// 인식 결과가 힌트 문자열의 일부를 그대로 베낀 것인지 (Whisper의 알려진 버릇).
        /// 3음절 이상이 힌트 안에 이어진 그대로 있고 원문과 다르면 베낀 것으로 본다.
        /// </summary>
        public static bool IsPromptEcho(string heardClean, string prompt, string targetClean)
        {
            if (heardClean.Length < 3 || heardClean == targetClean) return false;
            // 원문에 들어 있는 조각이면 실제로 그 부분만 말했을 수 있으므로 베낀 것으로 보지 않는다
            if (targetClean.Contains(heardClean)) return false;
            var p = Hangul.KeepSyllables(prompt);
            if (p.EndsWith(heardClean, StringComparison.Ordinal)) return true;
            // 음절 목록 힌트("뿌 르 딱 …")에서 연속 3음절 이상이 그대로 나온 경우
            var spaced = string.Join(" ", heardClean.ToCharArray());
            return prompt.Contains(spaced);
        }

        /// <summary>
        /// 판정 핵심 (스레드 어디서든 호출 가능, 게임과 자가 점검이 같이 쓴다).
        /// 힌트를 바꿔가며 여러 번 받아쓰고 가장 잘 맞은 결과를 쓴다.
        /// 힌트는 뜻 없는 음절 인식을 돕지만 가끔 엉뚱한 쪽으로 끌고 가기 때문.
        /// 엉뚱한 말을 하면 어떤 힌트로도 점수가 낮게 나오는 것을 확인함 → 판정이 후해지지 않음.
        /// </summary>
        public static SpeechVerdict JudgeCore(WhisperWrapper w, float[] trimmed16k, string target,
                                              IList<string> vocabulary, IList<string> syllables,
                                              GameConfig cfg, System.Random rng)
        {
            var v = new SpeechVerdict { usedWhisper = true, accuracy = -1f };
            var sw = Stopwatch.StartNew();
            // 앞쪽이 효과가 큰 순서. CPU에서는 시간 예산 때문에 보통 3번째쯤에서 끝나고,
            // GPU(한 번에 수십 ms)에서는 순서를 섞은 힌트와 빔 서치까지 더 시도한다.
            var passes = new List<(string prompt, bool beam)>();
            bool hasVocab = cfg.whisperVocabularyHint && vocabulary != null && vocabulary.Count > 0;
            bool hasSyll = cfg.whisperVocabularyHint && syllables != null && syllables.Count > 0;
            for (int round = 0; round < 2; round++)
            {
                if (hasVocab) passes.Add((string.Join(" ", Shuffled(vocabulary, rng)), false));
                if (hasSyll) passes.Add((string.Join(" ", Shuffled(syllables, rng)), false));
                passes.Add(("", round == 1));
            }

            var input = AudioUtil.PadForWhisper(AudioUtil.Normalize(trimmed16k), AudioUtil.JudgeRate);
            string targetClean = Hangul.KeepSyllables(target);
            foreach (var (prompt, beam) in passes)
            {
                var res = w.GetText(input, AudioUtil.JudgeRate, 1, MakeParams(cfg, prompt, beam));
                v.passes++;
                string heard = res?.Result?.Trim() ?? "";
                string clean = Hangul.KeepSyllables(heard);
                if (prompt.Length > 0 && IsPromptEcho(clean, prompt, targetClean))
                {
                    Debug.Log($"[MouthCode] 힌트를 그대로 따라 쓴 결과 버림: '{heard}'");
                    continue;
                }
                float acc = Hangul.Similarity(target, clean);
                if (acc > v.accuracy)
                {
                    v.accuracy = acc;
                    v.heard = heard;
                    v.heardClean = clean;
                }
                if (v.accuracy >= 0.97f) break;
                // 느린 PC에서 판정이 길어지지 않게: 시간 예산을 넘기면 지금까지의 최선으로 끝낸다
                if (sw.Elapsed.TotalSeconds > cfg.whisperTimeBudget && v.accuracy >= 0f) break;
            }
            v.accuracy = Mathf.Max(0f, v.accuracy);
            v.seconds = (float)sw.Elapsed.TotalSeconds;
            return v;
        }

        public async Task<SpeechVerdict> Judge(float[] samples16k, string target, IList<string> vocabulary, IList<string> syllables, float silenceRms = -1f)
        {
            var trimmed = AudioUtil.Trim(samples16k, AudioUtil.JudgeRate, silenceRms > 0f ? silenceRms : cfg.silenceRms);
            if (trimmed.Length < AudioUtil.JudgeRate * 0.15f)
                return new SpeechVerdict { noSpeech = true };

            // 로딩 중이면 잠깐 기다린다 (최대 15초)
            float wait = 0f;
            while (State == Status.Loading && wait < 15f)
            {
                await Task.Delay(100);
                wait += 0.1f;
            }

            if (State == Status.Ready && whisper != null)
            {
                try
                {
                    var w = whisper;
                    var c = cfg;
                    var r = rng;
                    var vocab = vocabulary != null ? new List<string>(vocabulary) : null;
                    var syll = syllables != null ? new List<string>(syllables) : null;
                    return await Task.Run(() => JudgeCore(w, trimmed, target, vocab, syll, c, r));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[MouthCode] Whisper 판정 실패, 간이 판정으로 대체: " + e.Message);
                }
            }

            // 간이 판정: 음절 수 비교
            var v = new SpeechVerdict();
            int est = AudioUtil.EstimateSyllables(trimmed, AudioUtil.JudgeRate);
            int want = Mathf.Max(1, Hangul.SyllableCount(target));
            v.accuracy = Mathf.Clamp01(1f - Mathf.Abs(est - want) / (float)want);
            v.heard = $"(간이 판정: {est}글자 들림, 원문 {want}글자)";
            return v;
        }
    }
}

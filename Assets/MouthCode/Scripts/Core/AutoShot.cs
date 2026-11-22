using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MouthCode
{
    /// <summary>
    /// 개발용 자동 플레이 + 스크린샷. 일반 실행에는 영향 없음 (명령줄 인자가 있을 때만 GameRoot가 붙인다).
    ///   MouthCode.exe -mouthcodeShots &lt;폴더&gt; [-mouthcodeDay N] [-mouthcodeHard] [-mouthcodeMaxDays N]
    ///                 [-mouthcodeVoiceDir &lt;단어별 wav 폴더&gt;]   ← 있으면 마이크 대신 이 녹음으로 송신
    /// </summary>
    public class AutoShot : MonoBehaviour
    {
        string dir;
        string voiceDir;
        GameRoot root;
        int shotIndex;
        readonly StringBuilder log = new StringBuilder();

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static bool Flag(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        IEnumerator Start()
        {
            root = GetComponent<GameRoot>();
            dir = Arg("-mouthcodeShots") ?? Path.Combine(Application.persistentDataPath, "shots");
            Directory.CreateDirectory(dir);
            if (int.TryParse(Arg("-mouthcodeDay"), out int d)) root.debugStartDay = d;
            int maxDays = int.TryParse(Arg("-mouthcodeMaxDays"), out int md) ? md : 3;
            voiceDir = Arg("-mouthcodeVoiceDir");
            if (!string.IsNullOrEmpty(voiceDir)) root.debugVoice = FakeVoice;
            bool hardMode = Flag("-mouthcodeHard");

            // 음성인식 모델 로딩 대기
            float wait = 0;
            while (root.Speech.State == SpeechJudge.Status.Loading && wait < 30f) { wait += Time.unscaledDeltaTime; yield return null; }
            yield return Real(2f);
            yield return Shot("title");

            if (Flag("-mouthcodeCover"))
            {
                // 스토어 표지 그림 (GameRoot.Cover.cs)
                root.SendMessage("ShowCoverForTest");
                yield return Real(2.5f);
                yield return Shot("cover");
                Application.Quit();
                yield break;
            }

            if (Flag("-mouthcodeScoreboardOnly"))
            {
                // 스코어보드만 열어 본다 (기록을 보내지 않는 읽기 전용 확인)
                root.SendMessage("ShowScoreboardForTest");
                yield return Real(8f);
                yield return Shot("scoreboard");
                Application.Quit();
                yield break;
            }

            if (Flag("-mouthcodePractice"))
            {
                // 무전 연습 화면 (음성 폴더가 있어야 자동 송신됨)
                root.SendMessage("StartPracticeForTest");
                for (int i = 0; i < 2; i++)
                {
                    float w = 0;
                    while (!root.DebugPhase.StartsWith("result") && w < 30f) { w += Time.unscaledDeltaTime; yield return null; }
                    yield return Real(2.6f);
                    yield return Shot($"practice_result{i}");
                    root.debugAdvance = true;
                    yield return Real(0.6f);
                }
                Application.Quit();
                yield break;
            }

            if (hardMode) root.SendMessage("StartRunHardForTest");
            else root.debugAdvance = true;

            string lastKey = "";
            float elapsed = 0;
            while (elapsed < 900f)
            {
                string key = $"{root.CurrentScreenName}:{root.DebugPhase}:{root.Day}";
                if (key != lastKey)
                {
                    yield return Real(root.DebugPhase.StartsWith("result") ? 2.6f : root.CurrentScreenName == "DayResult" ? 7f : 1.6f);
                    elapsed += 2f;
                    string again = $"{root.CurrentScreenName}:{root.DebugPhase}:{root.Day}";
                    if (again != key) continue;
                    lastKey = key;
                    string screen = root.CurrentScreenName;
                    if (screen == "Transmit" && root.DebugPhase.StartsWith("transmit") && root.debugVoice != null) continue; // 곧 자동 송신
                    yield return Shot($"d{root.Day}_{screen}_{root.DebugPhase}");
                    if (screen == "Scoreboard") break;
                    if (screen == "Briefing" && root.Day > root.debugStartDay + maxDays - 1)
                    {
                        log.AppendLine($"max days reached ({maxDays})");
                        break;
                    }
                    if (screen == "Transmit" && root.DebugPhase.StartsWith("transmit")) continue; // 시간 초과 대기
                    root.debugAdvance = true;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            File.WriteAllText(Path.Combine(dir, "autoshot.txt"), log.ToString(), new UTF8Encoding(false));
            yield return Real(0.5f);
            Application.Quit();
        }

        static IEnumerator Real(float s)
        {
            float t = 0;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(dir, $"{shotIndex++:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            log.AppendLine("shot " + path);
            yield return null;
        }

        // ── 가짜 목소리: 단어별 TTS wav를 이어 붙인다 ──

        float[] FakeVoice(MessageSpec m)
        {
            const int R = AudioUtil.JudgeRate;
            var outS = new List<float>();
            void Silence(float s) { for (int i = 0; i < (int)(s * R); i++) outS.Add(0f); }
            Silence(0.3f);
            if (m.isSfx)
            {
                var s = SfxSynthesizer.Generate(m.sfx.tempSynth, R);
                var rng = new System.Random(5);
                foreach (var v in s) outS.Add(v * 0.6f + ((float)rng.NextDouble() * 2 - 1) * 0.01f);
            }
            else
            {
                foreach (var word in m.words)
                {
                    string w = DayPlanner.Text(word);
                    var clip = Load(w);
                    if (clip == null)
                    {
                        foreach (var g in word) { var c = Load(g.sound); if (c != null) outS.AddRange(c); }
                    }
                    else outS.AddRange(clip);
                    Silence(0.15f);
                }
            }
            Silence(0.3f);
            log.AppendLine($"fake voice for '{m.text}': {outS.Count / (float)R:0.00}s");
            return outS.ToArray();
        }

        float[] Load(string name)
        {
            string path = Path.Combine(voiceDir, name + ".wav");
            if (!File.Exists(path)) return null;
            var bytes = File.ReadAllBytes(path);
            int rate = BitConverter.ToInt32(bytes, 24);
            int channels = BitConverter.ToInt16(bytes, 22);
            int pos = 12;
            while (pos + 8 <= bytes.Length)
            {
                string id = Encoding.ASCII.GetString(bytes, pos, 4);
                int size = BitConverter.ToInt32(bytes, pos + 4);
                if (id == "data")
                {
                    int n = size / 2;
                    var s = new float[n];
                    for (int i = 0; i < n; i++) s[i] = BitConverter.ToInt16(bytes, pos + 8 + i * 2) / 32768f;
                    var mono = AudioUtil.Mixdown(s, channels);
                    return AudioUtil.Trim(AudioUtil.Resample(mono, rate, AudioUtil.JudgeRate), AudioUtil.JudgeRate, 0.005f, 0.03f);
                }
                pos += 8 + size;
            }
            return null;
        }
    }
}

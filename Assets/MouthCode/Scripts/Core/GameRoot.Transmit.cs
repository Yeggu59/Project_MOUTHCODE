using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MouthCode
{
    public partial class GameRoot
    {
        static string KeyName(KeyCode k) => k == KeyCode.Space ? "Space" : k.ToString();

        /// <summary>단어 순서를 지키며 한 줄에 maxPerLine 글자 이하가 되도록, 가능한 한 고르게 두 줄로 나눈다.</summary>
        static List<List<List<GlyphDef>>> SplitLines(List<List<GlyphDef>> words, int maxPerLine)
        {
            int total = words.Sum(w => w.Count);
            if (total <= maxPerLine || words.Count < 2) return new List<List<List<GlyphDef>>> { words };
            int best = 1, bestScore = int.MaxValue, left = 0;
            for (int i = 1; i < words.Count; i++)
            {
                left += words[i - 1].Count;
                int score = Mathf.Max(left, total - left);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return new List<List<List<GlyphDef>>> { words.GetRange(0, best), words.GetRange(best, words.Count - best) };
        }

        /// <summary>무전기 표시등 + 글자 (PWR / RX / TX).</summary>
        static Led LabeledLed(Transform parent, string label, Color c, bool on)
        {
            var h = UIKit.HBox(parent, 6, TextAnchor.MiddleLeft);
            var led = Led.Create(h.transform, 20, c, on);
            UIKit.Label(h.transform, label, 24, UIKit.MetalDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            return led;
        }

        /// <summary>메시지 1개 송신: 표시 → 무전(push-to-talk) → 판정 → 결과.</summary>
        IEnumerator Transmit(DayPlan plan, int index, List<MessageResult> results, MessageResult res)
        {
            var msg = plan.messages[index];
            var root = NewScreen("Transmit");
            string right = results.Count > 0
                ? $"오늘 평균 {Pct(results.Average(r => r.accuracy))} (기준 {Pct(config.passThreshold)})   ·   총점 {totalScore:N0}"
                : $"통과 기준 {Pct(config.passThreshold)}   ·   총점 {totalScore:N0}";
            if (practice) Header(root, "무전 연습", "시간 제한·점수 없음   ·   [Esc] 타이틀로", UIKit.Accent);
            else Header(root, $"DAY {day}   ·   메시지 {index + 1} / {plan.messages.Count}" + (hard ? "   ·   하드" : ""), right);

            // ═════════ 무전기 본체 ═════════
            var dev = UIKit.Device(root, 14, 26);
            UIKit.Stretch((RectTransform)dev.transform, 30, 106, 30, 18);

            // ── 윗줄: 이름판 · 채널 · 오늘 송신 진행 · 표시등 ──
            var top = UIKit.HBox(dev.transform, 16, TextAnchor.MiddleLeft);
            UIKit.Plate(top.transform, "MC-26  야전 무전기");
            UIKit.Label(top.transform, practice ? "CH-00 ▸ 훈련용 회선" : $"CH-{(day * 7 + 3) % 100:00} ▸ 본부 비밀 회선", 24, UIKit.MetalDim, TextAnchor.MiddleLeft);
            UIKit.Flex(top.transform);
            Led currentPip = null;
            if (!practice && plan.messages.Count > 1)
            {
                UIKit.Label(top.transform, "오늘 송신", 24, UIKit.MetalDim);
                for (int i = 0; i < plan.messages.Count; i++)
                {
                    if (i < results.Count) Led.Create(top.transform, 18, AccColor(results[i].accuracy, config.passThreshold), true);
                    else if (i == index) currentPip = Led.Create(top.transform, 18, UIKit.Accent, true);
                    else Led.Create(top.transform, 18, UIKit.Dim, false);
                }
                UIKit.Spacer(top.transform, 20, 10);
            }
            LabeledLed(top.transform, "PWR", UIKit.Good, true);
            var rx = LabeledLed(top.transform, "RX", UIKit.Accent, false);
            var tx = LabeledLed(top.transform, "TX", UIKit.Danger, false);

            // ── CRT 화면: 수신된 암호문 ──
            var card = UIKit.Crt(dev.transform, msg.isSfx ? 16 : 8, 24, TextAnchor.MiddleCenter);
            UIKit.Size(card, -1, -1, -1, 1); // 남는 세로 공간은 화면이 차지 (결과 쪽지가 나오면 다시 줄어듦)
            UIKit.Label(card.transform, msg.isSfx ? "> 수신: 효과음 암호" : $"> 수신: 암호문 {msg.GlyphCount}글자", 24, UIKit.Dim, TextAnchor.MiddleLeft);
            var cells = new List<(Image icon, Text label, GlyphDef g)>();
            SfxAsset sfxA = null;
            LiveSpectrogram live = null;
            RawImage liveRaw = null;
            Text liveLabel = null;
            if (!msg.isSfx)
            {
                // 긴 메시지는 단어 단위로 두 줄에 나눠서 글자가 너무 작아지지 않게 한다
                var lines = SplitLines(msg.words, 9);
                int lineGlyphs = lines.Max(l => l.Sum(w => w.Count));
                int lineWords = lines.Max(l => l.Count);
                float size = Mathf.Clamp(1560f / (lineGlyphs + lineWords * 0.45f) - 14f, 70f, lines.Count > 1 ? 118f : 190f);
                int font = Mathf.RoundToInt(Mathf.Clamp(size * 0.32f, 26f, 54f));
                var rows = UIKit.VBox(card.transform, 4, TextAnchor.MiddleCenter, false);
                UIKit.Size(rows, -1, -1, -1, 1); // 안내 줄은 화면 위에 두고, 글자 줄은 남는 높이 가운데에 오게
                foreach (var glyphLine in lines)
                {
                    // 단어 사이는 넓게 띄우고, 단어마다 밑줄을 그어 끊어 읽는 곳을 보여 준다
                    var row = UIKit.HBox(rows.transform, size * 0.4f, TextAnchor.MiddleCenter);
                    foreach (var word in glyphLine)
                    {
                        var wordBox = UIKit.VBox(row.transform, 2, TextAnchor.UpperCenter, false);
                        var glyphRow = UIKit.HBox(wordBox.transform, 0, TextAnchor.MiddleCenter);
                        foreach (var g in word)
                        {
                            var (icon, label) = GlyphCell(glyphRow.transform, g, size, hard ? "" : g.sound, font, UIKit.Accent);
                            UIKit.Size(label, size + 6, font * 1.35f);
                            cells.Add((icon, label, g));
                        }
                        var underline = UIKit.Box(wordBox.transform, new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.28f), "Underline");
                        UIKit.Size(underline, word.Count * (size + 6) - 8, 3);
                    }
                }
            }
            else
            {
                sfxA = GetSfx(msg.sfx);
                var row = UIKit.HBox(card.transform, 34, TextAnchor.MiddleCenter);
                UIKit.Size(row, -1, -1, -1, 1);
                UIKit.Icon(row.transform, sfxA.icon, 170);
                if (!hard)
                {
                    var info = UIKit.VBox(row.transform, 8, TextAnchor.MiddleLeft, false);
                    UIKit.Title(info.transform, msg.sfx.displayName, 56, UIKit.Accent, TextAnchor.MiddleLeft);
                    UIKit.Label(info.transform, msg.sfx.guide, 24, UIKit.Ink, TextAnchor.MiddleLeft);
                    var sa = sfxA;
                    UIKit.CrtButton(info.transform, "▶ 다시 듣기", () => { if (!mic.IsRecording) PlaySfx(sa); }, 220, 56, 24);
                    var specCol = UIKit.VBox(row.transform, 4, TextAnchor.MiddleCenter, false);
                    UIKit.Label(specCol.transform, "원본 소리", 24, UIKit.Dim);
                    UIKit.Raw(specCol.transform, sfxA.spectrogram, 400, 150);
                    var liveCol = UIKit.VBox(row.transform, 4, TextAnchor.MiddleCenter, false);
                    liveLabel = UIKit.Label(liveCol.transform, "내 소리 (실시간)", 24, UIKit.Dim);
                    live = new LiveSpectrogram(200, 96);
                    Track(live.Texture);
                    liveRaw = UIKit.Raw(liveCol.transform, live.Texture, 400, 150);
                }
                else
                {
                    UIKit.Label(row.transform, "하드 모드라 소리는 안 들려준다.\n브리핑에서 들은 소리를 떠올려서 흉내 낼 것.", 36, UIKit.Ink, TextAnchor.MiddleLeft);
                }
            }

            // ── 타이머: 적 탐지까지 남은 시간 ──
            var timerRow = UIKit.HBox(dev.transform, 16, TextAnchor.MiddleCenter);
            UIKit.Size(UIKit.Label(timerRow.transform, "적 탐지까지", 24, UIKit.MetalText, TextAnchor.MiddleRight, FontStyle.Bold), 160, 40);
            var timerBar = SegBar.Create(timerRow.transform, 48, 1300, 30, 4, new Color(0, 0, 0, 0.45f));
            var lcd = UIKit.Crt(timerRow.transform, 0, 4, TextAnchor.MiddleCenter);
            UIKit.Size(lcd, 190, 56);
            var timerText = UIKit.Label(lcd.transform, "", 36, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (practice) timerRow.gameObject.SetActive(false);

            // ── 조작부: 송신 버튼 + 상태 화면 ──
            var deck = UIKit.HBox(dev.transform, 44, TextAnchor.MiddleCenter);
            var pttCol = UIKit.VBox(deck.transform, 8, TextAnchor.MiddleCenter, false);
            var pttBox = UIKit.Rect("PTT", pttCol.transform);
            UIKit.Size(pttBox, 210, 210);
            var pttGlow = UIKit.Box(pttBox, new Color(1f, 0.25f, 0.15f, 0f), "Glow");
            pttGlow.sprite = UISkin.Glow;
            UIKit.Anchor(pttGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380, 380));
            var ptt = UIKit.Box(pttBox, Color.white, "Dome");
            ptt.sprite = UISkin.Dome;
            ptt.raycastTarget = true;
            UIKit.Stretch(ptt.rectTransform);
            var hold = ptt.gameObject.AddComponent<HoldButton>();
            var pttLabel = UIKit.Title(ptt.transform, "PTT", 44, new Color(1f, 0.93f, 0.88f, 0.85f));
            UIKit.Stretch(pttLabel.rectTransform);
            var pttCaption = UIKit.Label(pttCol.transform, "", 24, UIKit.MetalText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(pttCaption, 340, 30);

            var status = UIKit.Crt(deck.transform, 6, 22, TextAnchor.MiddleLeft);
            UIKit.Size(status, 1180, 250);
            var statusText = UIKit.Label(status.transform, "", 36, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Size(statusText, -1, 48);
            var scope = new Oscilloscope(290, 40);
            Track(scope.Texture);
            var scopeRt = UIKit.Rect("Scope", status.transform);
            var scopeRaw = scopeRt.gameObject.AddComponent<RawImage>();
            scopeRaw.texture = scope.Texture;
            scopeRaw.raycastTarget = false;
            UIKit.Size(scopeRaw, -1, 86);
            var meterRow = UIKit.HBox(status.transform, 14, TextAnchor.MiddleLeft);
            UIKit.Label(meterRow.transform, "입력", 24, UIKit.Dim);
            var vu = SegBar.Create(meterRow.transform, 32, 640, 18, 3, new Color(1f, 1f, 1f, 0.06f));
            var recTime = UIKit.Label(meterRow.transform, "", 24, UIKit.Dim, TextAnchor.MiddleLeft);
            UIKit.Size(recTime, 420, 30);

            // 화면이 켜지고 → 암호 글자가 하나씩 수신되는 연출 (제한시간은 다 뜬 뒤부터)
            UiSound.Static();
            StartCoroutine(CrtOn(card.transform));
            foreach (var c in cells) c.icon.transform.parent.localScale = Vector3.zero;
            foreach (var c in cells)
            {
                StartCoroutine(Punch(c.icon.transform.parent, 0f, 0.12f));
                UiSound.Blip();
                rx.Set(UIKit.Accent, 1f);
                yield return Wait(0.03f);
                rx.Set(UIKit.Accent, 0.2f);
                yield return Wait(0.03f);
            }
            rx.Set(UIKit.Accent, 0f);
            if (msg.isSfx && !hard) PlaySfx(sfxA);

            // ── 송신 루프 ──
            // 첫날 첫 메시지는 처음 하는 사람을 위해 시간을 조금 더 주고 설명을 구체적으로 한다
            bool firstEver = !practice && day == 1 && index == 0;
            float limit = Mathf.Max(1f, msg.timeLimit + (firstEver ? 3f : 0f));
            float timeLeft = limit;
            bool done = false, timedOut = false;
            float[] rec = null;
            bool keyArmed = !Input.GetKey(config.talkKey);
            int lastTick = -1;
            float t = 0;
            string waitHint = firstEver && !msg.isSfx
                ? $"<color={Hex(UIKit.Accent)}>[{KeyName(config.talkKey)}] 누른 채로 {(hard ? "암호를 읽고" : $"「{msg.text}」 말하고")}, 다 말하면 떼기</color>"
                : "> 대기 중_";

            DebugPhase = "transmit-" + index;
            while (!done)
            {
                t += Time.unscaledDeltaTime;
                if (debugVoice != null && t > 1.2f)
                {
                    rec = debugVoice(msg);
                    done = true;
                    UiSound.PttOff();
                    break;
                }
                if (!Input.GetKey(config.talkKey)) keyArmed = true;
                bool pressing = (keyArmed && Input.GetKey(config.talkKey)) || hold.Held;

                if (!mic.IsRecording)
                {
                    if (pressing && mic.Active)
                    {
                        sfxSource.Stop();
                        mic.StartRecording(config.preRollSeconds);
                    }
                }
                else if (!pressing || mic.RecordedSeconds >= config.maxRecordSeconds)
                {
                    float held = mic.RecordedSeconds;
                    var data = mic.StopRecording(config.maxRecordSeconds);
                    if (held >= config.minPressSeconds)
                    {
                        rec = data;
                        done = true;
                        UiSound.PttOff();
                    }
                    else
                    {
                        waitHint = $"<color={Hex(UIKit.Accent)}>너무 짧다. 다 말할 때까지 누르고 있을 것</color>";
                    }
                }

                if (!done && !practice)
                {
                    timeLeft -= Time.unscaledDeltaTime;
                    if (timeLeft <= 0f)
                    {
                        timeLeft = 0f;
                        timedOut = true;
                        done = true;
                        if (mic.IsRecording) rec = mic.StopRecording(config.maxRecordSeconds);
                        UiSound.Alarm();
                    }
                }

                float k = timeLeft / limit;
                var tc = k > 0.5f ? UIKit.Good : Color.Lerp(UIKit.Danger, UIKit.Accent, k * 2f);
                timerBar.Set(k, _ => tc);
                bool blink = k < 0.3f && Mathf.Repeat(t, 0.5f) < 0.22f;
                timerText.text = $"{timeLeft:0.0}초";
                timerText.color = blink ? new Color(tc.r, tc.g, tc.b, 0.35f) : tc;
                int sec = Mathf.CeilToInt(timeLeft);
                if (!practice && sec <= 3 && sec != lastTick && timeLeft > 0f && !mic.IsRecording) { lastTick = sec; UiSound.Tick(); }

                bool recNow = mic.IsRecording;
                ptt.sprite = recNow ? UISkin.DomeLit : UISkin.Dome;
                pttGlow.color = new Color(1f, 0.25f, 0.15f, recNow ? 0.5f + 0.3f * Mathf.Sin(t * 10f) : 0f);
                pttBox.localScale = Vector3.one * (recNow || hold.Held ? 0.96f : 1f);
                pttLabel.text = recNow ? "ON AIR" : "PTT";
                pttCaption.text = !mic.Active ? "마이크 없음" : recNow ? "떼면 전송" : $"[{KeyName(config.talkKey)}] 누르고 말하기";
                tx.Set(UIKit.Danger, recNow ? 1f : 0f);
                if (currentPip != null) currentPip.Set(UIKit.Accent, 0.55f + 0.45f * Mathf.Sin(t * 5f));
                statusText.text = recNow ? $"<color={Hex(UIKit.Danger)}>●</color> 송신 중… 또박또박, 크게"
                    : mic.Active ? waitHint : $"<color={Hex(UIKit.Danger)}>마이크를 찾을 수 없다</color>";
                // 오실로스코프: 녹음 중엔 빨갛게, 너무 크면(클리핑) 노랗게
                Color32 scopeColor = mic.LevelNormalized > 0.95f ? new Color32(255, 214, 77, 255)
                    : recNow ? new Color32(255, 110, 90, 255) : new Color32(115, 255, 128, 255);
                scope.Draw(mic, scopeColor);
                vu.Set(mic.LevelNormalized, f => f < 0.6f ? UIKit.Good : f < 0.85f ? UIKit.Accent : UIKit.Danger);
                if (live != null)
                {
                    liveRaw.uvRect = new Rect(live.Push(mic, mic.Active), 0, 1, 1);
                    liveLabel.color = recNow ? UIKit.Danger : UIKit.Dim;
                    liveLabel.text = recNow ? "● 내 소리 (녹음 중)" : "내 소리 (실시간)";
                }
                recTime.text = recNow ? $"녹음 {mic.RecordedSeconds:0.0}초 / 최대 {config.maxRecordSeconds:0}초" : "";
                yield return null;
            }

            // ── 판정 (상태 화면에 "해독 중", RX 깜빡임) ──
            ptt.sprite = UISkin.Dome;
            pttGlow.color = new Color(1f, 0.25f, 0.15f, 0f);
            pttBox.localScale = Vector3.one;
            pttLabel.text = "PTT";
            pttCaption.text = "본부 응답 대기";
            tx.Set(UIKit.Danger, 0f);
            recTime.text = "";
            vu.Set(0f, _ => UIKit.Good);
            res.recording = rec;
            res.timedOut = timedOut;
            bool hasAudio = rec != null && rec.Length > AudioUtil.JudgeRate / 10;
            res.hadAudio = hasAudio;
            float[] perGlyph = null;
            string[] heardFor = null;

            if (!hasAudio)
            {
                res.accuracy = 0f;
                res.noSpeech = !timedOut;
            }
            else if (msg.isSfx)
            {
                statusText.text = "> 본부 분석 중…";
                rx.Set(UIKit.Accent, 1f);
                yield return null;
                var sr = SfxJudge.Judge(sfxA.judge, rec, config, mic.SilenceThreshold(config.silenceRms));
                res.accuracy = sr.accuracy;
                res.noSpeech = sr.noSound;
                Debug.Log($"[MouthCode] 효과음 '{msg.sfx.displayName}' 거리={sr.distance:0.000} 정확도={sr.accuracy:0.00}");
            }
            else
            {
                var task = speech.Judge(rec, msg.text, plan.vocabulary, plan.unlockedGlyphs.Select(g => g.sound).ToList(), mic.SilenceThreshold(config.silenceRms));
                float jt = 0;
                while (!task.IsCompleted)
                {
                    jt += Time.unscaledDeltaTime;
                    statusText.text = "> 본부 해독 중" + new string('.', 1 + ((int)(jt * 4f)) % 3);
                    rx.Set(UIKit.Accent, Mathf.Repeat(jt, 0.3f) < 0.15f ? 1f : 0.15f);
                    yield return null;
                }
                if (task.IsFaulted || task.Result == null)
                {
                    if (task.Exception != null) Debug.LogException(task.Exception);
                    res.accuracy = 0f;
                }
                else
                {
                    var v = task.Result;
                    res.accuracy = v.accuracy;
                    res.heard = v.heard;
                    res.noSpeech = v.noSpeech;
                    res.usedWhisper = v.usedWhisper;
                    if (v.usedWhisper) Hangul.AlignSyllables(msg.text, v.heardClean, out perGlyph, out heardFor);
                    Debug.Log($"[MouthCode] 원문='{msg.text}' 수신='{v.heard}' 정확도={v.accuracy:0.00} ({v.seconds:0.00}s)");
                }
            }
            rx.Set(UIKit.Accent, 0f);
            res.score = Mathf.RoundToInt(res.accuracy * 100f) * config.scorePerPercent * day;
            bool good = res.accuracy >= config.passThreshold;
            if (currentPip != null) currentPip.Set(AccColor(res.accuracy, config.passThreshold), 1f);

            // 연속 완벽 보너스: 2연속부터 +20%씩 (최대 +100%)
            int streakBonus = 0;
            if (!practice)
            {
                perfectStreak = res.accuracy >= config.perfectThreshold ? perfectStreak + 1 : 0;
                if (perfectStreak >= 2)
                {
                    streakBonus = Mathf.RoundToInt(res.score * Mathf.Min(1f, 0.2f * (perfectStreak - 1)));
                    res.score += streakBonus;
                }
            }

            // 글자별 결과 표시 (CRT 화면 위)
            for (int i = 0; i < cells.Count; i++)
            {
                float s = perGlyph != null && i < perGlyph.Length ? perGlyph[i] : res.accuracy;
                cells[i].icon.color = s >= 0.75f ? Color.white : Color.Lerp(new Color(1f, 0.25f, 0.2f), Color.white, s * 0.6f);
                string target = cells[i].g.sound;
                string heardSyl = heardFor != null && i < heardFor.Length ? heardFor[i] : null;
                if (heardSyl == null)
                    cells[i].label.text = $"<color={Hex(AccColor(s, config.passThreshold))}>{target}</color>";
                else if (heardSyl == target)
                    cells[i].label.text = $"<color={Hex(UIKit.Good)}>{target}</color>";
                else
                    cells[i].label.text = $"<color={Hex(UIKit.Dim)}>{target}</color><size={UIKit.PixelSize(Mathf.Max(24, cells[i].label.fontSize * 2 / 3))}><color={Hex(UIKit.Danger)}>→{(heardSyl == "" ? "?" : heardSyl)}</color></size>";
            }

            // ═════════ 결과: 무전기에 끼워진 수신 확인서 ═════════
            deck.gameObject.SetActive(false);
            var slip = UIKit.Paper(dev.transform, 10, 26, TextAnchor.MiddleCenter);
            slip.gameObject.AddComponent<CanvasGroup>(); // 아래 페이드 인용
            StartCoroutine(FadeIn(slip.GetComponent<CanvasGroup>(), 0.15f));
            var slipRt = (RectTransform)slip.transform;
            UIKit.Tape(slipRt, new Vector2(0.12f, 1f), Vector2.zero, 130, -5f);
            UIKit.Tape(slipRt, new Vector2(0.88f, 1f), Vector2.zero, 130, 4f);
            UiSound.Paper();

            string verdictText = !hasAudio ? (timedOut ? "적에게 탈취됨" : "무음 송신")
                : res.noSpeech ? "무음 송신"
                : good ? (res.accuracy >= config.perfectThreshold ? "완벽 수신" : "수신 양호")
                : "정보 손상";

            var slipRow = UIKit.HBox(slip.transform, 30, TextAnchor.MiddleCenter);
            var left = UIKit.VBox(slipRow.transform, 4, TextAnchor.MiddleCenter, false);
            UIKit.Size(left, 330, -1);
            var pctText = UIKit.Title(left.transform, "0%", 112, AccInk(res.accuracy, config.passThreshold));
            UIKit.Size(pctText, 330, 116);
            var (verdict, _) = UIKit.Stamp(left.transform, verdictText, 44, good ? UIKit.PGreen : UIKit.PRed, -6f);
            verdict.localScale = Vector3.zero;
            var scoreText = UIKit.Label(left.transform, "", 24, UIKit.PInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(scoreText, 330, 58);
            UIKit.Size(UIKit.Box(slipRow.transform, new Color(UIKit.PInk.r, UIKit.PInk.g, UIKit.PInk.b, 0.3f), "Divider"), 2, 230);

            var rightCol = UIKit.VBox(slipRow.transform, 18, TextAnchor.MiddleLeft, false);
            UIKit.Size(rightCol, 1200, -1);
            var sb = new StringBuilder();
            var rng = new System.Random(index * 31 + day);
            if (!msg.isSfx)
            {
                sb.Append($"원문          <color={Hex(UIKit.PBlue)}>{msg.text}</color>");
                if (hasAudio && !res.noSpeech)
                {
                    string heard = res.usedWhisper ? (string.IsNullOrWhiteSpace(res.heard) ? "(알아들을 수 없는 소리)" : res.heard) : res.heard;
                    sb.Append($"\n본부 수신    {heard}");
                }
                // 실제로 깨진 글자가 있을 때만 "손상된 정보" 줄을 보여 준다
                string damaged = res.accuracy < 0.999f ? Glitch(msg.text, res.accuracy, rng) : msg.text;
                if (damaged != msg.text)
                    sb.Append($"\n손상된 정보  <color={Hex(UIKit.PRed)}>{damaged}</color>");
            }
            else
            {
                sb.Append($"효과음   <color={Hex(UIKit.PRed)}>{msg.sfx.displayName}</color>   <color={Hex(UIKit.PDim)}>{msg.sfx.guide}</color>");
            }
            if (timedOut && hasAudio) sb.Append($"\n<color={Hex(UIKit.PRed)}>시간 초과. 말하던 중간에 끊겨서 송신됐다</color>");
            var infoText = UIKit.Label(rightCol.transform, sb.ToString(), 24, UIKit.PInk, TextAnchor.MiddleLeft);
            UIKit.Size(infoText, 1200, -1);

            // 이지 모드 효과음: 실시간 칸을 내 송신 전체 스펙트럼으로 바꿔 원본과 나란히 비교
            if (live != null && hasAudio)
            {
                var trimmed = AudioUtil.Trim(rec, AudioUtil.JudgeRate, config.silenceRms, 0.05f);
                liveRaw.texture = Track(Spectrogram.Build(trimmed.Length > 0 ? trimmed : rec, AudioUtil.JudgeRate, 320, 110));
                liveRaw.uvRect = new Rect(0, 0, 1, 1);
                liveLabel.text = "내 송신";
                liveLabel.color = UIKit.Dim;
            }

            var (hqLabel, hqFace) = HqRow(rightCol.transform, 24, 84);
            var voiceNote = UIKit.Label(rightCol.transform, "", 24, UIKit.PDim, TextAnchor.MiddleLeft);
            UIKit.Size(voiceNote, 1200, 30);

            var btnRow = UIKit.HBox(slip.transform, 24, TextAnchor.MiddleCenter);
            bool clicked = false;
            if (hasAudio)
            {
                var r2 = rec;
                UIKit.InkButton(btnRow.transform, "▶ 내 송신 다시 듣기", () => PlayVoice(r2, 1f), 340, 64, 24);
            }
            if (practice)
            {
                UIKit.InkButton(btnRow.transform, "다시 연습 ▶   (Enter)", () => clicked = true, 420, 64, 24, true);
                UIKit.InkButton(btnRow.transform, "타이틀로", () => { practiceExit = true; clicked = true; }, 240, 64, 24);
            }
            else
                UIKit.InkButton(btnRow.transform, index + 1 < plan.messages.Count ? "다음 메시지 ▶   (Enter)" : "오늘 결과 ▶   (Enter)", () => clicked = true, 460, 64, 24, true);

            // 연출: 퍼센트 카운트업 → 도장 → 본부 대사 → (실패 시) 피치 올린 내 목소리
            float e = 0;
            while (e < 0.55f && !debugAdvance)
            {
                e += Time.unscaledDeltaTime;
                pctText.text = Pct(Mathf.Lerp(0f, res.accuracy, Mathf.SmoothStep(0, 1, e / 0.55f)));
                if (Time.frameCount % 3 == 0) UiSound.Tick();
                yield return null;
            }
            pctText.text = Pct(res.accuracy);
            UiSound.Stamp();
            yield return Punch(verdict, 2.2f, 0.18f);
            scoreText.text = practice ? ""
                : streakBonus > 0 ? $"+{res.score:N0}점\n<color={Hex(UIKit.PGreen)}>연속 완벽 ×{perfectStreak}!</color>"
                : $"+{res.score:N0}점";
            if (streakBonus > 0) StartCoroutine(Punch(scoreText.transform, 1.5f, 0.2f));
            if (good) UiSound.Good();
            else
            {
                UiSound.Bad();
                StartCoroutine(Shake(root, 0.35f, 14f));
            }

            var hq = content.hq;
            string line = !hasAudio ? Pick(timedOut ? hq.timeout : hq.noSpeech)
                : res.noSpeech ? Pick(hq.noSpeech)
                : msg.isSfx ? Pick(good ? hq.sfxGood : hq.sfxBad)
                : Pick(res.accuracy >= config.perfectThreshold ? hq.perfect : good ? hq.good : hq.bad);
            // 실제로 들린 말을 인용하는 대사 (반반 확률, 들린 말이 원문과 다를 때만)
            string heardShort = res.heard?.Trim().Trim('.', '!', '?', ',') ?? "";
            if (!msg.isSfx && res.usedWhisper && heardShort.Length > 0 && heardShort.Length <= 24
                && Hangul.KeepSyllables(heardShort) != Hangul.KeepSyllables(msg.text)
                && res.accuracy < config.perfectThreshold && Random.value < 0.55f)
            {
                var pool = good ? hq.heardGood : hq.heardBad;
                if (pool != null && pool.Length > 0)
                    line = Pick(pool).Replace("{heard}", heardShort).Replace("{target}", msg.text);
            }
            TypewriteAsync(hqLabel, line, hqFace);

            if (!good && hasAudio && !res.noSpeech && config.failPlaybackPitch > 1.01f)
            {
                yield return Wait(0.7f);
                voiceNote.text = $"▶ 본부가 방금 송신을 다시 틀어 보는 중 (×{config.failPlaybackPitch:0.0})";
                PlayVoice(rec, config.failPlaybackPitch);
            }

            DebugPhase = "result-" + index;
            yield return WaitForContinue(() => clicked, 0.5f);
            voiceSource.Stop();
            sfxSource.Stop();
        }
    }
}

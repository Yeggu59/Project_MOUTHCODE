using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace MouthCode
{
    public partial class GameRoot
    {
        // ───────────────────────── 타이틀 ─────────────────────────

        public void ShowTitle() => Run(TitleRoutine());

        IEnumerator TitleRoutine()
        {
            onTitle = true;
            practice = false;
            var root = NewScreen("Title");

            // 책상 위를 떠다니는 암호 글자 (무전 잡음처럼 희미하게)
            var deco = UIKit.Rect("Deco", root);
            UIKit.Stretch(deco);
            var floaters = new List<(RectTransform rt, Vector2 basePos, float speed, float phase)>();
            if (content.glyphs.Count > 0)
            {
                var rng = new System.Random();
                for (int i = 0; i < 14; i++)
                {
                    var g = content.glyphs[rng.Next(content.glyphs.Count)];
                    var img = UIKit.Icon(deco, GlyphArt.Get(g), 100);
                    float size = rng.Next(70, 150);
                    img.rectTransform.sizeDelta = new Vector2(size, size);
                    img.color = new Color(1, 1, 1, 0.05f + 0.06f * (float)rng.NextDouble());
                    var pos = new Vector2(rng.Next(-920, 920), rng.Next(-330, 500));
                    floaters.Add((img.rectTransform, pos, 0.3f + (float)rng.NextDouble() * 0.6f, (float)rng.NextDouble() * 6f));
                }
            }

            // ── 기밀 서류철 ──
            var doc = UIKit.Paper(root, 10, 52, TextAnchor.UpperLeft);
            doc.childForceExpandWidth = false;
            var docRt = (RectTransform)doc.transform;
            UIKit.Anchor(docRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230, 60), new Vector2(1080, 780));
            docRt.gameObject.AddComponent<SlideIn>();

            // 서류철 탭
            var tab = UIKit.Box(docRt, UIKit.PaperCol * 0.94f, "Tab", true);
            UIKit.Ignore(tab);
            UIKit.Anchor(tab.rectTransform, new Vector2(0, 1), new Vector2(0, 0), new Vector2(40, -10), new Vector2(330, 56));
            tab.transform.SetSiblingIndex(1);
            var tabText = UIKit.Label(tab.transform, "작전명 : 마우스코드", 24, UIKit.PDim, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(tabText.rectTransform, 0, 0, 0, 10);

            UIKit.Label(doc.transform, "국방 통신 사령부   ·   문서번호 MC-2611-07   ·   배포: 통신병 1명", 24, UIKit.PDim, TextAnchor.MiddleLeft);
            UIKit.Spacer(doc.transform, 10, 4);
            UIKit.Title(doc.transform, "마우스코드", 156, UIKit.PInk, TextAnchor.MiddleLeft);
            var en = UIKit.Title(doc.transform, "M O U T H   C O D E", 48, UIKit.PRed, TextAnchor.MiddleLeft);
            UIKit.Spacer(doc.transform, 10, 4);
            UIKit.Label(doc.transform, "암호 장비가 고장 났다. 이제 암호는 입으로 보낸다.", 24, UIKit.PInk, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Spacer(doc.transform, 10, 8);
            UIKit.Rule(doc.transform, UIKit.PInk, 3);
            UIKit.Spacer(doc.transform, 10, 6);
            UIKit.Label(doc.transform, "■ 작전 선택", 24, UIKit.PInk, TextAnchor.MiddleLeft, FontStyle.Bold);
            // 두 모드 버튼은 왼쪽 정렬로 ▶와 설명 위치를 위아래로 맞춘다
            void ModeButton(string mode, string desc, bool hardMode)
            {
                var b = UIKit.InkButton(doc.transform, $"▶   {mode}        <size=24>{desc}</size>", () => StartRun(hardMode), 860, 84, 36, !hardMode);
                var label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                UIKit.Stretch(label.rectTransform, 60, 4, 12, 4);
            }
            ModeButton("이지 모드", "발음 가이드 O", false);
            ModeButton("하드 모드", "발음 가이드 X", true);
            var small = UIKit.HBox(doc.transform, 16, TextAnchor.MiddleLeft);
            UIKit.InkButton(small.transform, "무전 연습", StartPractice, 276, 64, 24);
            UIKit.InkButton(small.transform, "스코어보드", () => ShowScoreboard(null, false), 276, 64, 24);
            UIKit.InkButton(small.transform, "종료", Quit, 276, 64, 24);
            UIKit.Size(UIKit.Rect("Fill", doc.transform), -1, 0, -1, 1);
            var burn = UIKit.Label(doc.transform, "※ 본 문서는 열람 후 즉시 씹어 삼킬 것.", 24, UIKit.PDim, TextAnchor.MiddleRight);
            UIKit.Size(burn, -1, 30, 1);

            var (stamp, _) = UIKit.Stamp(docRt, "1급 기밀\n<size=26>TOP SECRET</size>", 60, UIKit.PRed, 9f, true);
            stamp.anchorMin = stamp.anchorMax = new Vector2(1, 1);
            stamp.pivot = new Vector2(0.5f, 0.5f);
            stamp.anchoredPosition = new Vector2(-170, -150);

            // ── 사령관 사진 ──
            var face = UIKit.Polaroid(root, HqPortrait.Closed, "본부 사령관 (실물)", 250, 4f);
            var faceCard = (RectTransform)face.transform.parent.parent;
            UIKit.Anchor(faceCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560, 230), new Vector2(274, 306));

            // ── 노란 메모지: 조작법 ──
            var note = UIKit.Paper(root, 6, 26, TextAnchor.UpperLeft, new Color(1f, 0.89f, 0.42f));
            var noteRt = (RectTransform)note.transform;
            UIKit.Anchor(noteRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(580, -150), new Vector2(380, 250));
            UIKit.Tape(noteRt, new Vector2(0.5f, 1f), new Vector2(0, 0), 110, -3f);
            UIKit.Label(note.transform, "조작법 메모", 24, UIKit.PRed, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Label(note.transform,
                $"[{KeyName(config.talkKey)}] 누르고 말하기\n[Enter] 다음으로\n[Esc] 타이틀로\n[M] 음악   [F11] 전체 화면",
                24, UIKit.PBlue, TextAnchor.UpperLeft);

            // ── 하단 장비 점검판 ──
            var eq = UIKit.Device(root, 8, 18, TextAnchor.MiddleLeft);
            UIKit.Anchor((RectTransform)eq.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(1760, 124));
            var eqRow1 = UIKit.HBox(eq.transform, 16, TextAnchor.MiddleLeft);
            UIKit.Plate(eqRow1.transform, "장비 점검");
            var micLed = Led.Create(eqRow1.transform, 20, UIKit.Good);
            var micLabel = UIKit.Label(eqRow1.transform, "", 24, UIKit.MetalText, TextAnchor.MiddleLeft);
            UIKit.Flex(eqRow1.transform);
            UIKit.Label(eqRow1.transform, "입력", 24, UIKit.MetalDim);
            var vu = SegBar.Create(eqRow1.transform, 28, 380, 20, 3, new Color(0, 0, 0, 0.4f));
            if (Microphone.devices.Length > 1)
                UIKit.KeyButton(eqRow1.transform, "마이크 변경", () => mic.NextDevice(), 180, 44, 24);
            var eqRow2 = UIKit.HBox(eq.transform, 16, TextAnchor.MiddleLeft);
            UIKit.Spacer(eqRow2.transform, 140, 10);
            var aiLed = Led.Create(eqRow2.transform, 20, UIKit.Accent);
            var whisperLabel = UIKit.Label(eqRow2.transform, "", 24, UIKit.MetalDim, TextAnchor.MiddleLeft);
            UIKit.Flex(eqRow2.transform);
            UIKit.Label(eqRow2.transform, "26 HAJE FALL Game Jam", 24, UIKit.MetalDim, TextAnchor.MiddleRight);

            UiSound.Paper();
            float t = 0, glitchUntil = 0, nextGlitch = 1.5f;
            const string enText = "M O U T H   C O D E";
            while (true)
            {
                t += Time.unscaledDeltaTime;

                if (!mic.HasDevice)
                {
                    micLabel.text = $"<color={Hex(UIKit.Danger)}>마이크가 연결되지 않았다</color>";
                    micLed.Set(UIKit.Danger, 0.5f + 0.5f * Mathf.Sin(t * 8f));
                }
                else if (!mic.Active)
                {
                    micLabel.text = "마이크 연결 중…";
                    micLed.Set(UIKit.Accent, 0.6f);
                }
                else
                {
                    string noise = mic.NoiseLevel == 0 ? "조용함" : mic.NoiseLevel == 1 ? "보통"
                        : $"<color={Hex(UIKit.Danger)}>시끄러움 (판정이 흔들릴 수 있음)</color>";
                    micLabel.text = $"마이크: {mic.DeviceName}      주변 소음: {noise}      말해 보고 입력 막대가 움직이면 OK";
                    micLed.Set(UIKit.Good, 0.75f + 0.25f * mic.LevelNormalized);
                }
                vu.Set(mic.LevelNormalized, f => f < 0.6f ? UIKit.Good : f < 0.85f ? UIKit.Accent : UIKit.Danger);

                whisperLabel.text = speech.StatusText;
                var aiColor = speech.State == SpeechJudge.Status.Ready ? UIKit.Good
                    : speech.State == SpeechJudge.Status.Loading ? UIKit.Accent : UIKit.Danger;
                aiLed.Set(aiColor, speech.State == SpeechJudge.Status.Loading ? 0.5f + 0.5f * Mathf.Sin(t * 6f) : 1f);

                if (t > nextGlitch)
                {
                    en.text = Glitch(enText, 0.55f, new System.Random());
                    glitchUntil = t + 0.09f;
                    nextGlitch = t + Random.Range(1.2f, 3.5f);
                    if (Random.value < 0.5f) UiSound.Blip();
                }
                else if (t > glitchUntil) en.text = enText;

                foreach (var f in floaters)
                {
                    f.rt.anchoredPosition = f.basePos + new Vector2(Mathf.Sin(t * f.speed + f.phase) * 24f, Mathf.Cos(t * f.speed * 0.8f + f.phase) * 16f);
                    f.rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * f.speed * 0.6f + f.phase) * 14f);
                }

                if (debugAdvance) { debugAdvance = false; StartRun(false); yield break; }
                yield return null;
            }
        }

        /// <summary>테스트용: 이 날부터 시작 (명령줄 -mouthcodeDay N)</summary>
        [System.NonSerialized] public int debugStartDay = 1;

        void StartRun(bool hardMode)
        {
            hard = hardMode;
            day = Mathf.Max(1, debugStartDay);
            totalScore = 0;
            runSeed = Random.Range(1, int.MaxValue / 2);
            runAccuracies.Clear();
            perfectStreak = 0;
            bestStreak = 0;
            worst = null;
            Run(RunRoutine());
        }

        int perfectStreak;
        int bestStreak;
        /// <summary>이번 작전에서 가장 엉망이었던 글자 송신 (게임 오버 화면 "명장면" 재생용)</summary>
        MessageResult worst;

        // ───────────────────────── 무전 연습 ─────────────────────────

        bool practice;
        bool practiceExit;

        void StartPractice()
        {
            hard = false;
            Run(PracticeRoutine());
        }

        void StartPracticeForTest() => StartPractice();

        /// <summary>시간 제한·점수 없이 송신을 연습한다. 1~3일차 글자로 문장을 뽑는다.</summary>
        IEnumerator PracticeRoutine()
        {
            onTitle = true; // ESC 한 번에 나갈 수 있게
            practice = true;
            var rng = new System.Random();
            while (true)
            {
                day = 1 + rng.Next(3);
                var full = DayPlanner.Build(day, config, content, rng.Next(), d => GetSfx(d).clip.length);
                var glyphMsgs = full.messages.Where(m => !m.isSfx).ToList();
                var plan = new DayPlan
                {
                    day = day,
                    unlockedGlyphs = full.unlockedGlyphs,
                    vocabulary = full.vocabulary,
                    messages = new List<MessageSpec> { glyphMsgs[rng.Next(glyphMsgs.Count)] },
                };
                practiceExit = false;
                var r = new MessageResult { spec = plan.messages[0] };
                yield return Transmit(plan, 0, new List<MessageResult>(), r);
                if (practiceExit) break;
            }
            practice = false;
            ShowTitle();
        }

        IEnumerator RunRoutine()
        {
            practice = false;
            onTitle = false;
            while (true)
            {
                var plan = DayPlanner.Build(day, config, content, runSeed + day * 7919, d => GetSfx(d).clip.length);
                yield return Briefing(plan);

                var results = new List<MessageResult>();
                for (int i = 0; i < plan.messages.Count; i++)
                {
                    var r = new MessageResult { spec = plan.messages[i] };
                    yield return Transmit(plan, i, results, r);
                    results.Add(r);
                    runAccuracies.Add(r.accuracy);
                    totalScore += r.score;
                    bestStreak = Mathf.Max(bestStreak, perfectStreak);
                    // 소리를 내긴 했는데 가장 엉망이었던 글자 송신을 기억 (녹음 하나만 보관)
                    bool funny = !r.spec.isSfx && !r.noSpeech && r.recording != null && r.recording.Length > AudioUtil.JudgeRate / 4;
                    if (funny && (worst == null || r.accuracy < worst.accuracy))
                    {
                        if (worst != null) worst.recording = null;
                        worst = r;
                    }
                    else r.recording = null;
                }

                float avg = results.Count > 0 ? results.Average(x => x.accuracy) : 0f;
                bool passed = avg >= config.passThreshold - 1e-4f;
                yield return DayResult(results, avg, passed);
                if (!passed) break;
                day++;
            }
            yield return GameOver();
        }

        // ───────────────────────── 브리핑 ─────────────────────────

        /// <summary>CRT 화면용 글자 칸 (녹색 글자 + 소리).</summary>
        (Image icon, Text label) GlyphCell(Transform parent, GlyphDef g, float size, string label, int fontSize, Color labelColor)
        {
            var cell = UIKit.VBox(parent, 2, TextAnchor.UpperCenter, false);
            var icon = UIKit.Icon(cell.transform, GlyphArt.Get(g), size);
            var text = UIKit.Label(cell.transform, label, fontSize, labelColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(text, size + 20, fontSize * 1.35f);
            return (icon, text);
        }

        /// <summary>문서에 테이프로 붙인 암호 글자 카드 (검은 잉크).</summary>
        (Image icon, Text label) PaperGlyphCard(Transform parent, GlyphDef g, float size, string label, int fontSize, float tapeAngle)
        {
            var card = UIKit.Card(parent, 0, 10, TextAnchor.MiddleCenter, true, tapeAngle);
            var icon = UIKit.Icon(card.transform, GlyphArt.Get(g, true), size);
            var text = UIKit.Label(card.transform, label, fontSize, UIKit.PInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(text, size, fontSize * 1.25f);
            return (icon, text);
        }

        IEnumerator Briefing(DayPlan plan)
        {
            var root = NewScreen("Briefing");
            string rank = content.RankFor(day);
            bool promoted = content.PromotedOn(day);
            Header(root, $"DAY {day}   작전 브리핑", $"{rank}   ·   " + (hard ? "하드 모드" : "이지 모드") + $"   ·   총점 {totalScore:N0}");

            var doc = Document(root, 150, 10, 40, true); // 내용 높이에 맞춘 종이
            doc.padding.bottom = 48;
            var docRt = (RectTransform)doc.transform;
            DocTitle(doc.transform, "작전 브리핑",
                $"문서번호 MC-{day:000}-{runSeed % 9000 + 1000}      수신: {rank}      {(hard ? "하드" : "이지")} 모드",
                $"DAY {day}", UIKit.PRed);

            RectTransform promoStamp = null;
            if (promoted)
            {
                (promoStamp, _) = UIKit.Stamp(docRt, $"진급!  <size=40>{rank}</size>", 60, UIKit.PRed, -8f, true);
                promoStamp.anchorMin = promoStamp.anchorMax = new Vector2(1, 1);
                promoStamp.pivot = new Vector2(0.5f, 0.5f);
                promoStamp.anchoredPosition = new Vector2(-520, -78);
                promoStamp.localScale = Vector3.zero;
            }

            string red = Hex(UIKit.PRed);
            var glyphLens = plan.messages.Where(m => !m.isSfx).Select(m => m.GlyphCount).ToList();
            string lenText = glyphLens.Count == 0 ? "" : glyphLens.Min() == glyphLens.Max() ? $"{glyphLens.Min()}글자" : $"{glyphLens.Min()}~{glyphLens.Max()}글자";
            UIKit.Label(doc.transform,
                $"오늘 송신 <color={red}>{plan.messages.Count}건</color>      암호 길이 <color={red}>{lenText}</color>      통과 기준: 평균 <color={red}>{Pct(config.passThreshold)}</color> 이상",
                24, UIKit.PInk, TextAnchor.MiddleLeft);

            // 자리가 넉넉한 날엔 사령관 한마디
            int codebookCells = plan.unlockedGlyphs.Count + plan.unlockedSfx.Count(s => s.unlockDay < day);
            (Text text, Image face) hqBrief = (null, null);
            string briefLine = null;
            // 종이가 화면을 넘지 않을 때만 사령관 한마디를 넣는다 (대략적인 높이 계산)
            // 암호표: 12개 이하는 크게 한 줄, 20개 이하는 작게 한 줄, 그 이상은 두 줄로 고르게
            bool bigCodebook = codebookCells <= 12;
            int codebookRows = codebookCells <= 20 ? 1 : 2;
            int codebookCols = Mathf.Max(1, (codebookCells + codebookRows - 1) / codebookRows);
            float estimate = 360f
                + (day == 1 ? 160f : 0f)
                + (plan.newGlyphs.Count > 0 ? 270f : 0f)
                + (plan.newSfx.Count > 0 ? 40f + 140f * plan.newSfx.Count : 0f)
                + (day > 1 ? 40f + codebookRows * (bigCodebook ? 136f : 96f) : 0f);
            if (estimate + 110f <= 930f)
            {
                var lines = content.hq.briefing != null && content.hq.briefing.Length > 0 ? content.hq.briefing : DefaultContent.Hq().briefing;
                briefLine = day == 1 || lines.Length < 2 ? lines[0] : lines[1 + Random.Range(0, lines.Length - 1)];
                if (promoted) briefLine = $"본부: {rank} 진급 축하한다. 계급장은 없다. 예산이 없다.";
                hqBrief = HqRow(doc.transform, 24, 72);
            }

            if (day == 1)
            {
                var how = UIKit.InkBox(doc.transform, 6, 20);
                UIKit.Label(how.transform, "[ 작전 수칙 ]", 24, UIKit.PRed, TextAnchor.MiddleLeft, FontStyle.Bold);
                UIKit.Label(how.transform,
                    "1.  암호 글자를 보고 정해진 소리로 읽는다. 글자마다 소리가 하나씩 있다.\n" +
                    $"2.  <color={red}>[{KeyName(config.talkKey)}]를 누른 채로 말하고, 다 말했으면 뗀다.</color> 빨간 버튼을 마우스로 눌러도 된다.\n" +
                    "3.  시간이 다 되면 적에게 들킨다. 송신은 메시지마다 한 번뿐이다.",
                    24, UIKit.PInk, TextAnchor.MiddleLeft);
            }

            var newCells = new List<Transform>();
            if (plan.newGlyphs.Count > 0)
            {
                DocSection(doc.transform, $"새 암호 {plan.newGlyphs.Count}개");
                var grid = UIKit.Grid(doc.transform, new Vector2(164, 196), new Vector2(32, 14), Mathf.Clamp(plan.newGlyphs.Count, 1, 8));
                grid.padding = new RectOffset(0, 0, 12, 2);
                var rng = new System.Random(day);
                foreach (var g in plan.newGlyphs)
                {
                    var (icon, _) = PaperGlyphCard(grid.transform, g, 112, g.sound, 36, rng.Next(-8, 9));
                    newCells.Add(icon.transform.parent);
                    icon.transform.parent.localScale = Vector3.zero;
                }
            }
            else if (plan.newSfx.Count == 0)
            {
                UIKit.Label(doc.transform, "※ 오늘은 새 암호가 없다. 대신 어제보다 길다.", 24, UIKit.PRed, TextAnchor.MiddleLeft);
            }

            if (plan.newSfx.Count > 0)
            {
                DocSection(doc.transform, "새 효과음 (이 그림이 나오면 소리로 흉내 낸다)");
                foreach (var d in plan.newSfx)
                {
                    var a = GetSfx(d);
                    var row = UIKit.HBox(doc.transform, 28, TextAnchor.MiddleLeft);
                    row.padding = new RectOffset(10, 0, 6, 6);
                    var card = UIKit.Card(row.transform, 0, 8, TextAnchor.MiddleCenter, true, 4f);
                    UIKit.Icon(card.transform, GlyphArt.Get(d, true), 110);
                    var info = UIKit.VBox(row.transform, 6, TextAnchor.MiddleLeft, false);
                    UIKit.Title(info.transform, d.displayName, 52, UIKit.PRed, TextAnchor.MiddleLeft);
                    UIKit.Label(info.transform, d.guide, 24, UIKit.PInk, TextAnchor.MiddleLeft);
                    UIKit.Size(info, 420, -1);
                    var sa = a;
                    UIKit.InkButton(row.transform, "▶ 듣기", () => PlaySfx(sa), 160, 60, 24);
                    UIKit.Raw(row.transform, a.spectrogram, 360, 120);
                }
                PlaySfx(GetSfx(plan.newSfx[0]));
            }

            if (day > 1)
            {
                DocSection(doc.transform,
                    hard ? "암호표 (하드 모드는 송신할 때 안 보인다. 지금 외워 둘 것)" : "암호표",
                    hard ? UIKit.PRed : UIKit.PInk);
                // 암호가 적을 땐 크게 보여 준다
                float gap = bigCodebook ? 14 : 4;
                float cw = bigCodebook ? 110 : Mathf.Min(78f, Mathf.Floor((1530f - gap * (codebookCols - 1)) / codebookCols));
                float icon = bigCodebook ? 84 : Mathf.Min(58f, cw - 18f);
                var grid = UIKit.Grid(doc.transform, new Vector2(cw, icon + 40), new Vector2(gap, 2), codebookCols);
                grid.padding = new RectOffset(0, 0, 6, 0);
                foreach (var g in plan.unlockedGlyphs)
                {
                    var cell = UIKit.VBox(grid.transform, 0, TextAnchor.UpperCenter, false);
                    UIKit.Icon(cell.transform, GlyphArt.Get(g, true), icon);
                    UIKit.Size(UIKit.Label(cell.transform, g.sound, bigCodebook ? 36 : 24, UIKit.PInk, TextAnchor.MiddleCenter, FontStyle.Bold), cw, 38);
                }
                foreach (var d in plan.unlockedSfx.Where(s => s.unlockDay < day))
                {
                    var cell = UIKit.VBox(grid.transform, 0, TextAnchor.UpperCenter, false);
                    UIKit.Icon(cell.transform, GlyphArt.Get(d, true), icon);
                    UIKit.Size(UIKit.Label(cell.transform, d.displayName, 24, UIKit.PRed, TextAnchor.MiddleCenter, FontStyle.Bold), cw, 38);
                }
            }

            UIKit.Spacer(doc.transform, 10, 8);
            bool clicked = false;
            var btn = UIKit.InkButton(doc.transform, "송신 시작 ▶   (Enter)", () => clicked = true, 520, 80, 36, true);
            WrapCentered(btn.GetComponent<RectTransform>(), 80); // 폭을 늘리는 세로 레이아웃 안에서 버튼 폭 고정용 래퍼

            UiSound.Static();
            if (hqBrief.text != null) TypewriteAsync(hqBrief.text, briefLine, hqBrief.face);
            if (promoStamp != null)
            {
                yield return Wait(0.35f);
                UiSound.Stamp();
                UiSound.Good();
                yield return Punch(promoStamp, 2.4f, 0.22f);
            }
            // 새 글자 카드가 하나씩 붙는다
            yield return Wait(0.25f);
            foreach (var c in newCells)
            {
                StartCoroutine(Punch(c, 0f, 0.18f));
                UiSound.Stamp();
                yield return Wait(0.22f);
            }
            foreach (var c in newCells) if (c != null) c.localScale = Vector3.one;
            yield return WaitForContinue(() => clicked, 0.4f);
            sfxSource.Stop();
        }

        /// <summary>폭을 늘리는 세로 레이아웃 안에서 요소를 가운데 고정폭으로 두기 위한 래퍼.</summary>
        static void WrapCentered(RectTransform rt, float height)
        {
            var parent = rt.parent;
            int index = rt.GetSiblingIndex();
            var wrap = UIKit.HBox(parent, 0, TextAnchor.MiddleCenter);
            wrap.transform.SetSiblingIndex(index);
            UIKit.Size(wrap, -1, height);
            rt.SetParent(wrap.transform, false);
        }

        // ───────────────────────── 하루 결과 ─────────────────────────

        IEnumerator DayResult(List<MessageResult> results, float avg, bool passed)
        {
            var root = NewScreen("DayResult");
            Header(root, $"DAY {day}   작전 결과", (hard ? "하드 모드" : "이지 모드") + $"   ·   총점 {totalScore:N0}", passed ? UIKit.Good : UIKit.Danger);

            var doc = Document(root, 150, 8, 40, true);
            DocTitle(doc.transform, "작전 결과 보고서",
                $"송신 {results.Count}건      통과 기준: 평균 {Pct(config.passThreshold)} 이상",
                $"DAY {day}", UIKit.PRed);

            // 표: 번호 | 송신 암호 | 해독 내용(본부가 받아 적은 뜻) | 정확도
            float[] w = { 60, 470, 830, 120 };
            var head = UIKit.HBox(doc.transform, 18, TextAnchor.MiddleLeft);
            string[] heads = { "번호", "송신 암호", "해독 내용", "정확도" };
            for (int i = 0; i < heads.Length; i++)
                UIKit.Size(UIKit.Label(head.transform, heads[i], 24, UIKit.PDim, i == 3 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, FontStyle.Bold), w[i], 32);
            UIKit.Rule(doc.transform, new Color(UIKit.PInk.r, UIKit.PInk.g, UIKit.PInk.b, 0.6f), 2);

            var rows = new List<(CanvasGroup group, Text meaning, string full)>();
            var glitchRng = new System.Random(day * 977 + results.Count);
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var row = UIKit.HBox(doc.transform, 18, TextAnchor.MiddleLeft);
                UIKit.Size(UIKit.Label(row.transform, $"#{i + 1}", 24, UIKit.PDim, TextAnchor.MiddleLeft), w[0], 40);
                string code = r.spec.isSfx ? $"효과음 「{r.spec.sfx.displayName}」" : r.spec.text;
                UIKit.Size(UIKit.Label(row.transform, code, 24, UIKit.PInk, TextAnchor.MiddleLeft), w[1], 40);

                // 해독 내용: 잘 보냈으면 그대로, 망쳤으면 그만큼 깨져서 도착한다
                string meaning = content.MeaningFor(r.spec);
                Color mColor = UIKit.PBlue;
                if (!r.hadAudio) { meaning = r.timedOut ? "(적이 가로챘다. 내용 없음)" : "(아무 소리도 안 왔다)"; mColor = UIKit.PRed; }
                else if (r.noSpeech) { meaning = "(숨소리만 왔다)"; mColor = UIKit.PRed; }
                else if (r.accuracy < config.perfectThreshold)
                {
                    meaning = Glitch(meaning, Mathf.Clamp01(r.accuracy + 0.1f), glitchRng);
                    if (r.accuracy < config.passThreshold) mColor = UIKit.PRed;
                }
                var mText = UIKit.Label(row.transform, "", 24, mColor, TextAnchor.MiddleLeft);
                mText.supportRichText = false;
                UIKit.Size(mText, w[2], 40);
                UIKit.Size(UIKit.Label(row.transform, Pct(r.accuracy), 36, AccInk(r.accuracy, config.passThreshold), TextAnchor.MiddleRight, FontStyle.Bold), w[3], 40);
                // 자리는 미리 잡아 두고 하나씩 보이게 (종이 크기가 중간에 안 바뀌게)
                var group = row.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                rows.Add((group, mText, meaning));
            }
            UIKit.Rule(doc.transform, UIKit.PInk, 3);

            var avgRow = UIKit.HBox(doc.transform, 28, TextAnchor.MiddleLeft);
            UIKit.Label(avgRow.transform, "하루 평균", 36, UIKit.PInk, TextAnchor.MiddleLeft, FontStyle.Bold);
            var avgText = UIKit.Title(avgRow.transform, "0%", 100, AccInk(avg, config.passThreshold));
            UIKit.Size(avgText, 270, 110);
            UIKit.Label(avgRow.transform, $"기준 {Pct(config.passThreshold)}", 24, UIKit.PDim);
            UIKit.Flex(avgRow.transform);
            var (stamp, _) = UIKit.Stamp(avgRow.transform, passed ? "통과" : "실패", 76, passed ? UIKit.PGreen : UIKit.PRed, -10f);
            stamp.localScale = Vector3.zero;
            UIKit.Spacer(avgRow.transform, 40, 10);

            var (hq, hqFace) = HqRow(doc.transform, 24, 96);

            bool clicked = false;
            var btn = UIKit.InkButton(doc.transform, passed ? "다음 날로 ▶   (Enter)" : "결과 보기 ▶   (Enter)", () => clicked = true, 520, 80, 36, true);
            WrapCentered(btn.GetComponent<RectTransform>(), 80);
            // 자리는 미리 잡아 두고 안 보이게 (나중에 켜도 종이 크기가 바뀌지 않게)
            var btnGroup = btn.transform.parent.gameObject.AddComponent<CanvasGroup>();
            btnGroup.alpha = 0f;
            btnGroup.blocksRaycasts = false;

            // 연출: 줄 하나씩 나타나며 해독 내용이 타자로 찍힘 (Enter·Space·클릭이면 바로 다 보여 줌)
            //       → 평균 카운트업 → 도장 → 본부 대사
            bool skip = false;
            bool SkipPressed() => debugAdvance || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                                  || Input.GetKeyDown(config.talkKey) || Input.GetMouseButtonDown(0);
            foreach (var row in rows)
            {
                row.group.alpha = 1f;
                if (skip) { row.meaning.text = row.full; continue; }
                UiSound.Blip();
                float shown = 0;
                int lastShown = 0;
                while (shown < row.full.Length)
                {
                    if (SkipPressed()) { skip = true; break; }
                    shown += Time.unscaledDeltaTime * 55f;
                    int n = Mathf.Min(row.full.Length, (int)shown);
                    if (n != lastShown)
                    {
                        row.meaning.text = row.full.Substring(0, n);
                        if (n / 3 != lastShown / 3) UiSound.Blip();
                        lastShown = n;
                    }
                    yield return null;
                }
                row.meaning.text = row.full;
                if (!skip) yield return Wait(0.12f);
            }
            float e = 0;
            while (e < 0.8f && !debugAdvance)
            {
                e += Time.unscaledDeltaTime;
                avgText.text = Pct(Mathf.Lerp(0, avg, Mathf.SmoothStep(0, 1, e / 0.8f)));
                if (Time.frameCount % 4 == 0) UiSound.Tick();
                yield return null;
            }
            avgText.text = Pct(avg);
            yield return Wait(0.2f);
            UiSound.Stamp();
            yield return Punch(stamp, 2.2f, 0.2f);
            if (passed) UiSound.Good(); else UiSound.Bad();
            TypewriteAsync(hq, Pick(passed ? content.hq.dayPass : content.hq.gameOver), hqFace);
            btnGroup.alpha = 1f;
            btnGroup.blocksRaycasts = true;
            yield return WaitForContinue(() => clicked, 0.5f);
        }

        // ───────────────────────── 게임 오버 ─────────────────────────

        IEnumerator GameOver()
        {
            var root = NewScreen("GameOver");
            Header(root, "통신 두절", hard ? "하드 모드" : "이지 모드", UIKit.Danger);

            var doc = Document(root, 250, 14, 44, true);
            doc.padding.top = 70;
            UIKit.HazardStrip((RectTransform)doc.transform, 28, true);

            var titleRow = UIKit.HBox(doc.transform, 36, TextAnchor.MiddleCenter);
            var go = UIKit.Title(titleRow.transform, "GAME OVER", 140, UIKit.PRed);
            var (failStamp, _) = UIKit.Stamp(titleRow.transform, "작전 실패", 52, UIKit.PRed, -12f);
            failStamp.localScale = Vector3.zero;
            UIKit.Label(doc.transform, $"DAY {day}, 기지 위치가 들통났다.      최종 계급: <color={Hex(UIKit.PRed)}>{content.RankFor(day)}</color>", 36, UIKit.PInk);

            float avgAll = runAccuracies.Count > 0 ? runAccuracies.Average() : 0f;
            var tiles = UIKit.HBox(doc.transform, 22, TextAnchor.MiddleCenter);
            void Tile(string caption, string value, Color valueColor)
            {
                var box = UIKit.InkBox(tiles.transform, 0, 12, TextAnchor.MiddleCenter);
                UIKit.Size(box, 300, 124);
                UIKit.Label(box.transform, caption, 24, UIKit.PDim);
                UIKit.Title(box.transform, value, 56, valueColor);
            }
            Tile("버틴 날", $"{day - 1}일", UIKit.PInk);
            Tile("총점", $"{totalScore:N0}", UIKit.PInk);
            Tile("평균 정확도", Pct(avgAll), AccInk(avgAll, config.passThreshold));
            if (bestStreak >= 2) Tile("최고 연속 완벽", $"{bestStreak}회", UIKit.PGreen);

            // 명장면: 이번 작전에서 가장 엉망이었던 송신을 피치 올려 다시 들려준다
            var highlight = worst != null && worst.recording != null ? worst : null;
            if (highlight != null)
            {
                var hl = UIKit.InkBox(doc.transform, 10, 20, TextAnchor.UpperLeft, new Color(UIKit.PRed.r, UIKit.PRed.g, UIKit.PRed.b, 0.8f));
                UIKit.Label(hl.transform, "■ 증거물 1호: 이번 작전 최악의 송신 (감청 녹음)", 24, UIKit.PRed, TextAnchor.MiddleLeft, FontStyle.Bold);
                string heard = string.IsNullOrWhiteSpace(highlight.heard) ? "(알아들을 수 없는 소리)" : highlight.heard;
                var hlText = UIKit.Label(hl.transform,
                    $"원문 「<color={Hex(UIKit.PBlue)}>{highlight.spec.text}</color>」     →     본부 수신 「<color={Hex(UIKit.PRed)}>{heard}</color>」     ({Pct(highlight.accuracy)})",
                    24, UIKit.PInk, TextAnchor.MiddleLeft);
                UIKit.Size(hlText, -1, 34);
                var hlRow = UIKit.HBox(hl.transform, 16, TextAnchor.MiddleLeft);
                UIKit.Size(hlRow, -1, 60);
                var rec = highlight.recording;
                UIKit.InkButton(hlRow.transform, $"▶ 다시 듣기 (×{config.failPlaybackPitch:0.0})", () => PlayVoice(rec, config.failPlaybackPitch), 330, 58, 24);
                UIKit.InkButton(hlRow.transform, "▶ 원래 속도로", () => PlayVoice(rec, 1f), 250, 58, 24);
            }

            UIKit.Spacer(doc.transform, 10, 6);
            var sig = UIKit.HBox(doc.transform, 20, TextAnchor.MiddleCenter);
            UIKit.Label(sig.transform, "통신병 서명 :", 36, UIKit.PInk, TextAnchor.MiddleRight, FontStyle.Bold);
            var input = UIKit.PaperInput(sig.transform, PlayerPrefs.GetString("mouthcode.name", "무명 통신병"), 520, 70, 36);

            var buttons = UIKit.HBox(doc.transform, 24, TextAnchor.MiddleCenter);
            bool save = false, skip = false;
            UIKit.InkButton(buttons.transform, "기록 저장 ▶   (Enter)", () => save = true, 460, 76, 36, true);
            UIKit.InkButton(buttons.transform, "저장 안 하고 타이틀로", () => skip = true, 420, 76, 24);

            UiSound.Bad();
            yield return Punch(go.transform, 1.6f, 0.3f);
            UiSound.Stamp();
            yield return Punch(failStamp, 2.4f, 0.2f);
            input.ActivateInputField();
            if (highlight != null && config.failPlaybackPitch > 1.01f)
            {
                yield return Wait(0.8f);
                PlayVoice(highlight.recording, config.failPlaybackPitch);
            }

            float t0 = Time.unscaledTime;
            while (!save && !skip)
            {
                if (Time.unscaledTime - t0 > 0.5f && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) save = true;
                if (debugAdvance) { debugAdvance = false; save = true; }
                yield return null;
            }

            if (skip) { ShowTitle(); yield break; }

            string name = string.IsNullOrWhiteSpace(input.text) ? "무명 통신병" : input.text.Trim();
            PlayerPrefs.SetString("mouthcode.name", name);
            PlayerPrefs.Save();
            var entry = new ScoreEntry
            {
                id = System.Guid.NewGuid().ToString("N").Substring(0, 12),
                name = name,
                day = day,
                score = totalScore,
                accuracy = avgAll,
                hard = hard,
                rank = content.RankFor(day),
                date = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            var list = Scoreboard.Add(entry);
            ShowScoreboard(list, true, entry);
        }

        // ───────────────────────── 스코어보드 ─────────────────────────

        /// <param name="submitted">방금 끝난 판의 기록 (온라인 서버로 보낸다)</param>
        void ShowScoreboard(List<ScoreEntry> list, bool afterRun, ScoreEntry submitted = null)
            => Run(ScoreboardRoutine(list ?? Scoreboard.Load(), afterRun, submitted));

        void ShowScoreboardForTest() => ShowScoreboard(null, false);

        /// <summary>다른 PC에서 온 이름이 글자 꾸밈 태그로 화면을 망치지 않게.</summary>
        static string SafeName(string n, int max = 12)
        {
            n = string.IsNullOrWhiteSpace(n) ? (max == 12 ? "무명 통신병" : "") : n.Trim();
            if (n.Length > max) n = n.Substring(0, max);
            return n.Replace("<", "‹").Replace(">", "›");
        }

        IEnumerator ScoreboardRoutine(List<ScoreEntry> local, bool afterRun, ScoreEntry submitted)
        {
            onTitle = true; // ESC 확인 없이 나가도 됨
            var root = NewScreen("Scoreboard");
            bool online = OnlineScoreboard.Enabled(config);
            Header(root, "스코어보드", online ? "전체 순위 (온라인)  ·  이 PC 기록" : "이 PC의 기록", UIKit.Accent);

            var doc = Document(root, 150, 6, 40);
            DocTitle(doc.transform, "명예의 통신병 명부", "상위 10명      보존 기한: 영구", "TOP 10", UIKit.PRed);
            var status = UIKit.Label(doc.transform, "", 24, UIKit.PDim, TextAnchor.MiddleLeft);
            UIKit.Size(status, -1, 30);

            float[] widths = { 90, 340, 330, 210, 160, 110, 250 };
            void Row(Transform parent, string[] cells, Color color, bool bold, bool highlight, Color? firstColor = null)
            {
                var row = UIKit.HBox(parent, 0, TextAnchor.MiddleCenter);
                if (highlight)
                {
                    var hl = UIKit.Box(row.transform, UIKit.Highlighter, "Highlighter");
                    UIKit.Ignore(hl);
                    UIKit.Stretch(hl.rectTransform, -10, 2, -10, 2);
                }
                for (int i = 0; i < cells.Length; i++)
                    UIKit.Size(UIKit.Label(row.transform, cells[i], 24, i == 0 && firstColor.HasValue ? firstColor.Value : color,
                        TextAnchor.MiddleCenter, bold ? FontStyle.Bold : FontStyle.Normal), widths[i], 42);
            }
            Row(doc.transform, new[] { "순위", "이름", "도달 · 계급", "점수", "정확도", "모드", "날짜" }, UIKit.PDim, true, false);
            UIKit.Rule(doc.transform, new Color(UIKit.PInk.r, UIKit.PInk.g, UIKit.PInk.b, 0.6f), 2);
            var table = UIKit.VBox(doc.transform, 6, TextAnchor.UpperCenter);

            var blank = new Color(UIKit.PDim.r, UIKit.PDim.g, UIKit.PDim.b, 0.45f);
            void Fill(List<ScoreEntry> list, string newId)
            {
                if (table == null) return;
                foreach (Transform c in table.transform) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
                for (int i = 0; i < 10; i++)
                {
                    if (i >= list.Count)
                    {
                        // 빈 칸도 명부 양식처럼 줄을 그어 둔다
                        Row(table.transform, new[] { (i + 1).ToString(), i == 0 ? "아직 기록 없음" : "", "", "", "", "", "" }, blank, false, false);
                        UIKit.Rule(table.transform, new Color(UIKit.PInk.r, UIKit.PInk.g, UIKit.PInk.b, 0.2f), 2, true);
                        continue;
                    }
                    var e = list[i];
                    bool isNew = e.isNew || (newId != null && e.id == newId);
                    Row(table.transform, new[]
                    {
                        i < 3 ? $"★ {i + 1}" : (i + 1).ToString(),
                        SafeName(e.name) + (isNew ? $"  <color={Hex(UIKit.PRed)}>◀ NEW</color>" : ""),
                        string.IsNullOrEmpty(e.rank) ? $"DAY {e.day}" : $"DAY {e.day} · {SafeName(e.rank, 10)}",
                        e.score.ToString("N0"),
                        Pct(e.accuracy),
                        e.hard ? "하드" : "이지",
                        SafeName(e.date, 16),
                    }, i < 3 || isNew ? UIKit.PInk : UIKit.PDim, isNew || i < 3, isNew, i < 3 ? UIKit.PRed : (Color?)null);
                    UIKit.Rule(table.transform, new Color(UIKit.PInk.r, UIKit.PInk.g, UIKit.PInk.b, 0.25f), 2, true);
                }
            }

            UIKit.Size(UIKit.Rect("Fill", doc.transform), -1, 0, -1, 1);
            var buttons = UIKit.HBox(doc.transform, 24, TextAnchor.MiddleCenter);
            bool toTitle = false, again = false;
            UIKit.InkButton(buttons.transform, "타이틀로   (Enter)", () => toTitle = true, 420, 76, 36, true);
            if (afterRun) UIKit.InkButton(buttons.transform, "같은 모드로 다시", () => again = true, 420, 76, 36);

            // ── 온라인: 이 PC 기록을 먼저 보여 주고, 서버 응답이 오면 전체 순위로 바꾼다 ──
            OnlineScoreboard.Result net = null;
            bool syncing = online, viewOnline = online;
            Text toggleText = null;
            void Refresh()
            {
                if (table == null) return;
                string red = Hex(UIKit.PRed), green = Hex(UIKit.PGreen);
                if (viewOnline && net != null && net.ok)
                {
                    Fill(net.entries, submitted?.id);
                    string mine = net.myPlace > 0 ? $"      내 순위: {net.myPlace}위" : "";
                    string warn = string.IsNullOrEmpty(net.error) ? "" : $"   <color={red}>({net.error})</color>";
                    status.text = $"<color={green}>●</color> 전체 순위 (모든 PC, {net.total}건){mine}{warn}";
                }
                else
                {
                    Fill(local, null);
                    status.text = !online ? "○ 이 PC의 기록"
                        : syncing ? "◌ 본부 서버 연결 중… 우선 이 PC 기록부터 보여 준다"
                        : net != null && !net.ok ? $"<color={red}>○ 서버에 연결하지 못했다. 이 PC 기록만 보여 준다.</color>" + (submitted != null ? "   (이번 기록은 다음 접속 때 보낸다)" : "")
                        : "○ 이 PC의 기록";
                }
                if (toggleText != null)
                {
                    toggleText.text = viewOnline ? "이 PC 기록 보기" : "전체 순위 보기";
                    // 서버 순위를 받아 온 뒤에만 전환 버튼을 보여 준다
                    toggleText.transform.parent.gameObject.SetActive(net != null && net.ok);
                }
            }
            if (online)
            {
                var toggle = UIKit.InkButton(buttons.transform, "", () => { if (net != null && net.ok) { viewOnline = !viewOnline; Refresh(); } }, 300, 76, 24);
                toggleText = toggle.GetComponentInChildren<Text>();
                StartCoroutine(OnlineScoreboard.Sync(config, submitted, 10, r =>
                {
                    syncing = false;
                    net = r;
                    if (!r.ok) viewOnline = false;
                    Refresh();
                }));
            }
            Refresh();

            yield return WaitForContinue(() => toTitle || again, 0.5f);
            if (again) StartRun(hard);
            else ShowTitle();
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace MouthCode
{
    public class MessageResult
    {
        public MessageSpec spec;
        public float accuracy;
        public string heard = "";
        public bool timedOut;
        public bool noSpeech;
        public bool usedWhisper;
        public bool hadAudio;
        public float[] recording;
        public int score;
    }

    public class SfxAsset
    {
        public AudioClip clip;
        public float[] judge;          // 16kHz 판정용
        public Texture2D spectrogram;
        public Sprite icon;
    }

    /// <summary>
    /// 게임 전체 흐름. 씬에는 이 컴포넌트 하나만 있으면 된다 (UI는 전부 코드로 생성).
    /// 화면 구성은 GameRoot.Screens.cs, 송신 화면은 GameRoot.Transmit.cs.
    /// </summary>
    public partial class GameRoot : MonoBehaviour
    {
        [Tooltip("비워두면 Resources/GameConfig")]
        public GameConfig config;
        [Tooltip("비워두면 Resources/ContentDatabase")]
        public ContentDatabase content;

        MicRecorder mic;
        SpeechJudge speech;
        AudioSource sfxSource;
        AudioSource voiceSource;
        AudioSource ambience;
        AudioSource bgm;
        bool musicMuted;
        Canvas canvas;
        RectTransform screen;
        Text toast;
        float toastUntil;
        Coroutine flow;
        bool onTitle;
        float escArmedUntil;
        readonly Dictionary<SfxDef, SfxAsset> sfxCache = new Dictionary<SfxDef, SfxAsset>();

        // 진행 상태
        bool hard;
        int day;
        int totalScore;
        int runSeed;
        readonly List<float> runAccuracies = new List<float>();

        /// <summary>자동 테스트용: true가 되면 대기 중인 화면을 넘긴다.</summary>
        [NonSerialized] public bool debugAdvance;

        public static GameRoot Instance { get; private set; }
        public MicRecorder Mic => mic;
        public SpeechJudge Speech => speech;
        public string CurrentScreenName => screen != null ? screen.name : "";

        // ── 자동 테스트용 (AutoShot) ──
        /// <summary>설정되면 송신 화면에서 마이크 대신 이 소리를 송신한다.</summary>
        [NonSerialized] public Func<MessageSpec, float[]> debugVoice;
        public string DebugPhase { get; private set; } = "";
        public int Day => day;

        void StartRunHardForTest() => StartRun(true);

        void Awake()
        {
            Instance = this;
            if (config == null) config = Resources.Load<GameConfig>("GameConfig");
            if (config == null) config = ScriptableObject.CreateInstance<GameConfig>();
            if (content == null) content = Resources.Load<ContentDatabase>("ContentDatabase");
            if (content == null) content = ScriptableObject.CreateInstance<ContentDatabase>();

            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            UIKit.Init();
            EnsureCameraAndEvents();
            BuildCanvas();

            UiSound.Init(gameObject);
            sfxSource = NewSource();
            voiceSource = NewSource();
            ambience = NewSource();
            ambience.clip = UiSound.Ambience();
            ambience.loop = true;
            ambience.volume = 0.05f;
            ambience.Play();
            bgm = NewSource();
            bgm.clip = UiSound.Bgm();
            bgm.loop = true;
            bgm.volume = 0f;
            bgm.Play();
            musicMuted = PlayerPrefs.GetInt("mouthcode.musicMuted", 0) == 1;

            mic = gameObject.AddComponent<MicRecorder>();
            mic.Begin();
            speech = gameObject.AddComponent<SpeechJudge>();
            speech.Init(config);

            // 온라인 스코어보드: 테스트용 주소 덮어쓰기. 자동 테스트는 주소를 직접 주지 않으면 실제 서버에 올리지 않는다.
            var args = Environment.GetCommandLineArgs();
            int urlArg = Array.IndexOf(args, "-mouthcodeScoreboardUrl");
            if (urlArg >= 0 && urlArg + 1 < args.Length) OnlineScoreboard.OverrideUrl = args[urlArg + 1];
            if (Array.IndexOf(args, "-mouthcodeShots") >= 0)
            {
                OnlineScoreboard.Disabled = urlArg < 0;
                gameObject.AddComponent<AutoShot>();
            }
        }

        AudioSource NewSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        void Start() => ShowTitle();

        void Update()
        {
            if (toast != null)
            {
                float a = Mathf.Clamp01((toastUntil - Time.unscaledTime) * 2f);
                var c = toast.color; c.a = a; toast.color = c;
                if (toastBg != null) toastBg.color = new Color(0.06f, 0.06f, 0.05f, 0.92f * a);
            }
            if (alarmLight != null)
            {
                float target = CurrentScreenName == "GameOver" ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.2f) : 0f;
                var ac = alarmLight.color;
                ac.a = Mathf.MoveTowards(ac.a, target * 0.8f, Time.unscaledDeltaTime * 2f);
                alarmLight.color = ac;
            }

            // 입력창에 글자를 치는 중이면 단축키를 쓰지 않는다
            bool typing = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                          && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;
            if (!typing && Input.GetKeyDown(KeyCode.M))
            {
                musicMuted = !musicMuted;
                PlayerPrefs.SetInt("mouthcode.musicMuted", musicMuted ? 1 : 0);
                Toast(musicMuted ? "음악 끔 (M)" : "음악 켬 (M)", 1.2f);
            }
            if (Input.GetKeyDown(KeyCode.F11))
            {
                if (Screen.fullScreen) Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
                else Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
            }

            if (Input.GetKeyDown(KeyCode.Escape) && practice)
            {
                ShowTitle();
            }
            else if (Input.GetKeyDown(KeyCode.Escape) && !onTitle && !typing && CurrentScreenName != "GameOver")
            {
                if (Time.unscaledTime < escArmedUntil)
                {
                    escArmedUntil = 0;
                    ShowTitle();
                }
                else
                {
                    escArmedUntil = Time.unscaledTime + 2f;
                    Toast("ESC를 한 번 더 누르면 타이틀로 (이번 판 기록은 사라진다)");
                }
            }

            // 녹음 중엔 배경음을 끈다 (마이크에 섞이지 않게). 음악은 송신 화면 전체에서 끈다.
            if (ambience != null)
            {
                float target = mic != null && mic.IsRecording ? 0f : 0.05f;
                ambience.volume = Mathf.MoveTowards(ambience.volume, target, Time.unscaledDeltaTime * 0.6f);
            }
            if (bgm != null)
            {
                bool quiet = musicMuted || CurrentScreenName == "Transmit" || (mic != null && mic.IsRecording);
                float target = quiet ? 0f : config.musicVolume;
                bgm.volume = Mathf.MoveTowards(bgm.volume, target, Time.unscaledDeltaTime * (quiet ? 1.5f : 0.25f));
            }
        }

        // ───────────────────────── 흐름 제어 ─────────────────────────

        void Run(IEnumerator routine)
        {
            if (flow != null) StopCoroutine(flow);
            if (mic != null && mic.IsRecording) mic.StopRecording(0.1f);
            if (sfxSource != null) sfxSource.Stop();
            if (voiceSource != null) voiceSource.Stop();
            flow = StartCoroutine(Inline(routine));
        }

        /// <summary>
        /// 중첩 IEnumerator를 한 코루틴 안에서 직접 돌린다.
        /// (Unity의 yield return IEnumerator는 별도 코루틴이 되어 StopCoroutine으로 같이 안 멈춤)
        /// </summary>
        static IEnumerator Inline(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                bool moved;
                try { moved = top.MoveNext(); }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    stack.Pop();
                    // 녹음 중에 화면이 터졌다면 녹음을 끊어 다음 메시지가 저절로 송신되지 않게 한다
                    var gr = Instance;
                    if (gr != null && gr.mic != null && gr.mic.IsRecording) gr.mic.StopRecording(0.1f);
                    continue;
                }
                if (!moved) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return top.Current;
            }
        }

        IEnumerator WaitForContinue(Func<bool> clicked, float minDelay = 0.4f)
        {
            float t0 = Time.unscaledTime;
            bool keyArmed = !Input.GetKey(config.talkKey);
            while (true)
            {
                if (debugAdvance) { debugAdvance = false; yield break; }
                if (!Input.GetKey(config.talkKey)) keyArmed = true;
                bool ready = Time.unscaledTime - t0 > minDelay;
                if (ready && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                              (keyArmed && Input.GetKeyDown(config.talkKey))))
                {
                    UiSound.Click();
                    yield break;
                }
                if (clicked != null && clicked()) yield break;
                yield return null;
            }
        }

        IEnumerator Wait(float seconds)
        {
            float t = 0;
            while (t < seconds)
            {
                if (debugAdvance) yield break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // ───────────────────────── 화면 공통 ─────────────────────────

        void EnsureCameraAndEvents()
        {
            if (Camera.main == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                var cam = go.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = UIKit.Bg;
                cam.orthographic = true;
            }
            if (FindAnyObjectByType<AudioListener>() == null) Camera.main.gameObject.AddComponent<AudioListener>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        static Canvas MakeCanvas(string name, Transform parent, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // 창 비율이 16:9가 아니어도(16:10, 4:3, 울트라와이드, 창 크기 조절) 1920x1080 화면이 통째로 들어오게 넓힌다
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return c;
        }

        RawImage alarmLight;
        Image toastBg;

        void BuildCanvas()
        {
            canvas = MakeCanvas("Canvas", transform, 0);
            canvas.pixelPerfect = true; // 픽셀체를 화면 픽셀에 맞춰 또렷하게
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            // 배경: 어두운 책상 + 스탠드 조명 (+ 게임 오버 때 붉은 경광등)
            var bg = UIKit.Box(canvas.transform, UIKit.Bg, "Background");
            UIKit.Stretch(bg.rectTransform);
            UIKit.LayerTile(bg.rectTransform, UISkin.DeskTex, 256, 256, Color.white, "Desk");
            var lamp = UIKit.Rect("Lamp", bg.rectTransform);
            UIKit.Stretch(lamp);
            var lampRaw = lamp.gameObject.AddComponent<RawImage>();
            lampRaw.texture = UISkin.Lamp;
            lampRaw.raycastTarget = false;
            var alarm = UIKit.Rect("Alarm", bg.rectTransform);
            UIKit.Stretch(alarm);
            alarmLight = alarm.gameObject.AddComponent<RawImage>();
            alarmLight.texture = UISkin.Lamp;
            alarmLight.color = new Color(1f, 0.1f, 0.05f, 0f);
            alarmLight.raycastTarget = false;

            // 가장자리 어둡게 (입력은 막지 않음)
            var overlay = MakeCanvas("Overlay", transform, 100);
            var vig = UIKit.Rect("Vignette", overlay.transform);
            UIKit.Stretch(vig);
            var vigRaw = vig.gameObject.AddComponent<RawImage>();
            vigRaw.texture = VignetteTexture();
            vigRaw.raycastTarget = false;

            // 알림: 검은 띠
            toastBg = UIKit.Box(overlay.transform, new Color(0.06f, 0.06f, 0.05f, 0f), "Toast", true);
            UIKit.Anchor(toastBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1500, 64));
            toast = UIKit.Label(toastBg.transform, "", 24, UIKit.Accent);
            UIKit.Stretch(toast.rectTransform, 20, 0, 20, 0);
            var tc = toast.color; tc.a = 0; toast.color = tc;
        }

        static Texture2D VignetteTexture()
        {
            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S * 2 - 1, dy = (y + 0.5f) / S * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                    px[y * S + x] = new Color(0, 0, 0, Mathf.SmoothStep(0f, 0.55f, Mathf.InverseLerp(0.8f, 1.6f, d)));
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        readonly List<UnityEngine.Object> screenGarbage = new List<UnityEngine.Object>();

        /// <summary>화면 전용으로 만든 텍스처/클립을 등록해 화면이 바뀔 때 정리한다.</summary>
        T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null) screenGarbage.Add(obj);
            return obj;
        }

        RectTransform NewScreen(string name)
        {
            DebugPhase = "";
            foreach (var o in screenGarbage) if (o != null) Destroy(o);
            screenGarbage.Clear();
            if (screen != null) Destroy(screen.gameObject);
            screen = UIKit.Rect(name, canvas.transform);
            UIKit.Stretch(screen);
            return screen;
        }

        /// <summary>화면 위 금속 상태 띠: LED + 왼쪽 제목 + 오른쪽 정보.</summary>
        Led Header(RectTransform root, string left, string right, Color? led = null)
        {
            var bar = UIKit.Rect("Header", root);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(0.5f, 1);
            bar.anchoredPosition = new Vector2(0, -6); bar.sizeDelta = new Vector2(-24, 84);
            UIKit.Stretch(UIKit.Layer(bar, UISkin.Shadow, new Color(0, 0, 0, 0.6f), true, "Shadow").rectTransform, -14, -4, -14, -24);
            UIKit.Layer(bar, UISkin.Metal, UIKit.MetalTint, true, "Metal");
            var brushed = UIKit.LayerTile(bar, UISkin.Brushed, 256, 64, Color.white, "Brushed");
            UIKit.Stretch(brushed.rectTransform, 8, 8, 8, 8);
            var row = UIKit.HBox(bar, 16, TextAnchor.MiddleLeft);
            UIKit.Stretch((RectTransform)row.transform, 48, 4, 48, 4);
            var l = Led.Create(row.transform, 22, led ?? UIKit.Good);
            UIKit.Label(row.transform, left, 36, UIKit.MetalText, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Flex(row.transform);
            UIKit.Label(row.transform, right, 24, UIKit.MetalDim, TextAnchor.MiddleRight);
            return l;
        }

        /// <summary>책상 위에 놓인 문서 한 장 (머리띠 아래 영역에 펼침).</summary>
        /// <param name="fit">true면 내용 높이에 맞춘 종이를 머리띠 아래 가운데에 둔다</param>
        VerticalLayoutGroup Document(RectTransform root, float sideMargin, float spacing = 12, int pad = 44, bool fit = false)
        {
            var doc = UIKit.Paper(root, spacing, pad);
            var rt = (RectTransform)doc.transform;
            if (fit)
            {
                rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(1, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(-2 * sideMargin, 0);
                rt.anchoredPosition = new Vector2(0, -42);
                rt.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else UIKit.Stretch(rt, sideMargin, 110, sideMargin, 26);
            rt.gameObject.AddComponent<SlideIn>();
            UiSound.Paper();
            return doc;
        }

        /// <summary>문서 제목 줄: 큰 제목 + 문서 번호, 오른쪽에 붉은 표시. 아래 이중선.</summary>
        void DocTitle(Transform doc, string title, string docLine, string right, Color rightColor)
        {
            var row = UIKit.HBox(doc, 24, TextAnchor.MiddleLeft);
            var left = UIKit.VBox(row.transform, 4, TextAnchor.MiddleLeft, false);
            UIKit.Title(left.transform, title, 64, UIKit.PInk, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(docLine)) UIKit.Label(left.transform, docLine, 24, UIKit.PDim, TextAnchor.MiddleLeft);
            UIKit.Flex(row.transform);
            if (!string.IsNullOrEmpty(right)) UIKit.Title(row.transform, right, 64, rightColor, TextAnchor.MiddleRight);
            var rules = UIKit.VBox(doc, 3, TextAnchor.UpperCenter);
            UIKit.Rule(rules.transform, UIKit.PInk, 4);
            UIKit.Rule(rules.transform, UIKit.PInk, 1);
        }

        /// <summary>문서 안 소제목: ■ 제목 + 옅은 점선.</summary>
        void DocSection(Transform doc, string title, Color? color = null)
        {
            var box = UIKit.VBox(doc, 4, TextAnchor.UpperLeft);
            UIKit.Label(box.transform, "■ " + title, 24, color ?? UIKit.PInk, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Rule(box.transform, new Color(UIKit.PInk.r, UIKit.PInk.g, UIKit.PInk.b, 0.4f), 2, true);
        }

        public void Toast(string msg, float seconds = 2.5f)
        {
            if (toast == null) return;
            toast.text = msg;
            toastUntil = Time.unscaledTime + seconds;
        }

        IEnumerator Typewrite(Text t, string full, Image portrait = null, float cps = 38f)
        {
            if (t == null || full == null) yield break;
            if (portrait != null)
            {
                // 사진 옆 이름표가 이미 "본부 사령관"이므로 대사 앞의 "본부:"는 뺀다
                var trimmed = full.TrimStart();
                if (trimmed.StartsWith("본부:") || trimmed.StartsWith("본부 :")) full = trimmed.Substring(trimmed.IndexOf(':') + 1).TrimStart();
                // 초상 옆 대사칸 폭을 완성된 문장 길이에 맞춰 두어, 타이핑 중에 초상이 움직이지 않게 한다
                t.text = full;
                var le = t.GetComponent<LayoutElement>();
                if (le != null) le.preferredWidth = le.minWidth = Mathf.Min(1000f, t.preferredWidth + 8f);
            }
            t.text = "";
            float shown = 0, mouthT = 0;
            int last = 0;
            bool open = false;
            while (last < full.Length)
            {
                if (t == null) yield break;
                shown += Time.unscaledDeltaTime * cps;
                int n = Mathf.Min(full.Length, Mathf.Max(last, (int)shown));
                if (n != last)
                {
                    t.text = full.Substring(0, n);
                    if (n % 3 == 0) UiSound.Blip();
                    last = n;
                }
                if (portrait != null)
                {
                    mouthT += Time.unscaledDeltaTime;
                    if (mouthT > 0.09f)
                    {
                        mouthT = 0;
                        // 공백·문장부호에서는 입을 닫는다
                        char c = full[Mathf.Clamp(last - 1, 0, full.Length - 1)];
                        open = !open && !char.IsWhiteSpace(c) && !char.IsPunctuation(c);
                        portrait.sprite = open ? HqPortrait.Open : HqPortrait.Closed;
                    }
                }
                yield return null;
            }
            if (portrait != null) portrait.sprite = HqPortrait.Closed;
        }

        void TypewriteAsync(Text t, string full, Image portrait = null) => StartCoroutine(Typewrite(t, full, portrait));

        /// <summary>사령관 사진(테이프로 붙인 사진) + 대사 한 줄. 종이 위에서 쓴다.</summary>
        (Text text, Image portrait) HqRow(Transform parent, int fontSize = 24, float portraitSize = 84)
        {
            var row = UIKit.HBox(parent, 24, TextAnchor.MiddleLeft);
            var img = UIKit.Polaroid(row.transform, HqPortrait.Closed, "", portraitSize, -4f);
            var bubble = UIKit.VBox(row.transform, 4, TextAnchor.MiddleLeft, false);
            UIKit.Label(bubble.transform, "본부 사령관 :", 24, UIKit.PRed, TextAnchor.MiddleLeft, FontStyle.Bold);
            var t = UIKit.Label(bubble.transform, "", fontSize, UIKit.PInk, TextAnchor.MiddleLeft);
            UIKit.Size(t, 10, Mathf.Max(fontSize * 1.4f, portraitSize - 34));
            return (t, img);
        }

        IEnumerator FadeIn(CanvasGroup g, float dur)
        {
            float e = 0;
            while (e < dur && g != null)
            {
                e += Time.unscaledDeltaTime;
                g.alpha = Mathf.Clamp01(e / dur);
                yield return null;
            }
            if (g != null) g.alpha = 1f;
        }

        /// <summary>CRT 화면이 켜지는 연출: 가는 가로줄에서 위아래로 펼쳐진다.</summary>
        IEnumerator CrtOn(Transform t, float dur = 0.16f)
        {
            float e = 0;
            while (e < dur && t != null)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / dur);
                t.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, Mathf.Sqrt(k)), Mathf.Lerp(0.02f, 1f, k * k), 1f);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        IEnumerator Shake(RectTransform rt, float dur, float magnitude)
        {
            float e = 0;
            while (e < dur && rt != null)
            {
                e += Time.unscaledDeltaTime;
                float m = magnitude * (1f - e / dur);
                rt.anchoredPosition = new Vector2(Random.Range(-m, m), Random.Range(-m, m));
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = Vector2.zero;
        }

        IEnumerator Punch(Transform t, float from = 1.8f, float dur = 0.22f)
        {
            float e = 0;
            while (e < dur && t != null)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(from, 1f, e / dur);
                t.localScale = new Vector3(k, k, 1);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        // ───────────────────────── 공통 데이터 ─────────────────────────

        SfxAsset GetSfx(SfxDef d)
        {
            if (sfxCache.TryGetValue(d, out var a)) return a;
            a = new SfxAsset();
            if (d.clip != null)
            {
                a.clip = d.clip;
                a.judge = AudioUtil.ClipToJudgeRate(d.clip);
                if (a.judge == null)
                    Debug.LogWarning($"[MouthCode] 효과음 '{d.displayName}' 데이터를 읽을 수 없음. Load Type을 Decompress On Load로 바꾸세요. 임시 합성음으로 판정합니다.");
            }
            if (a.clip == null) a.clip = AudioUtil.ToClip(SfxSynthesizer.Generate(d.tempSynth, 22050), 22050, d.displayName);
            if (a.judge == null) a.judge = SfxSynthesizer.Generate(d.tempSynth, AudioUtil.JudgeRate);
            a.spectrogram = Spectrogram.Build(AudioUtil.Trim(a.judge, AudioUtil.JudgeRate, 0.002f, 0.05f), AudioUtil.JudgeRate, 320, 110);
            a.icon = GlyphArt.Get(d);
            sfxCache[d] = a;
            return a;
        }

        void PlaySfx(SfxAsset a)
        {
            if (a?.clip == null) return;
            sfxSource.Stop();
            sfxSource.clip = a.clip;
            sfxSource.pitch = 1f;
            sfxSource.Play();
        }

        void PlayVoice(float[] rec, float pitch)
        {
            if (rec == null || rec.Length == 0) return;
            var trimmed = AudioUtil.Trim(rec, AudioUtil.JudgeRate, config.silenceRms * 0.5f);
            if (trimmed.Length == 0) trimmed = rec;
            voiceSource.Stop();
            if (voiceSource.clip != null) Destroy(voiceSource.clip);
            voiceSource.clip = AudioUtil.ToClip(AudioUtil.Normalize(trimmed, 0.8f), AudioUtil.JudgeRate, "voice");
            voiceSource.pitch = pitch;
            voiceSource.Play();
        }

        string Pick(string[] lines) => lines == null || lines.Length == 0 ? "" : lines[Random.Range(0, lines.Length)];

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        static Color AccColor(float acc, float threshold) =>
            acc >= threshold ? UIKit.Good : Color.Lerp(UIKit.Danger, UIKit.Accent, Mathf.InverseLerp(0.2f, threshold, acc));

        /// <summary>종이 위에서 읽히는 정확도 색 (진한 녹색 / 황토 / 빨강).</summary>
        static Color AccInk(float acc, float threshold) =>
            acc >= threshold ? UIKit.PGreen : Color.Lerp(UIKit.PRed, UIKit.PAmber, Mathf.InverseLerp(0.2f, threshold, acc));

        static string Glitch(string text, float acc, System.Random rng)
        {
            const string junk = "▒#@%&?*※◈▣";
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (c == ' ') { sb.Append(c); continue; }
                sb.Append(rng.NextDouble() < (1 - acc) * 0.95 ? junk[rng.Next(junk.Length)] : c);
            }
            return sb.ToString();
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

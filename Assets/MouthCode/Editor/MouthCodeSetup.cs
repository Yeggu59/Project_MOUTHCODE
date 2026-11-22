using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;

namespace MouthCode.EditorTools
{
    /// <summary>
    /// 프로젝트 초기 세팅 / 모델 다운로드 / 빌드 메뉴.
    /// 배치 모드: Unity -batchmode -projectPath . -executeMethod MouthCode.EditorTools.MouthCodeSetup.SetupBatch -quit
    /// </summary>
    public static class MouthCodeSetup
    {
        const string Root = "Assets/MouthCode";
        const string ResDir = Root + "/Resources";
        const string SceneDir = Root + "/Scenes";
        public const string ScenePath = SceneDir + "/Main.unity";

        [MenuItem("MouthCode/Setup Project (에셋·씬·빌드 설정)", priority = 0)]
        public static void Setup()
        {
            Directory.CreateDirectory(ResDir);
            Directory.CreateDirectory(SceneDir);
            AssetDatabase.Refresh();

            var cfg = LoadOrCreate<GameConfig>(ResDir + "/GameConfig.asset");
            var db = LoadOrCreate<ContentDatabase>(ResDir + "/ContentDatabase.asset");

            if (!File.Exists(ScenePath)) CreateScene(cfg, db);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "MOUTH CODE";
            PlayerSettings.companyName = "HAJE Jam";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            EnsureIcon();

            AssetDatabase.SaveAssets();
            Debug.Log("[MouthCode] 셋업 완료: " + ScenePath);
        }

        public static void SetupBatch()
        {
            Setup();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        const string IconPath = Root + "/Art/AppIcon.png";

        /// <summary>실행 파일 아이콘: 본부 사령관 초상 (없을 때만 생성, 이미 있으면 그 그림을 쓴다 → 교체 가능).</summary>
        static void EnsureIcon()
        {
            if (!File.Exists(IconPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
                const int S = 256;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
                var face = HqPortrait.Closed.texture;
                var bg = new Color(0.05f, 0.12f, 0.08f);
                var gold = new Color(1f, 0.84f, 0.3f);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        // 둥근 사각 + 금색 테두리
                        float cx = Mathf.Abs(x + 0.5f - S / 2f) - (S / 2f - 40f), cy = Mathf.Abs(y + 0.5f - S / 2f) - (S / 2f - 40f);
                        float d = new Vector2(Mathf.Max(cx, 0), Mathf.Max(cy, 0)).magnitude + Mathf.Min(Mathf.Max(cx, cy), 0) - 32f;
                        float inside = Mathf.Clamp01(-d + 0.5f);
                        float border = Mathf.Clamp01(6f - Mathf.Abs(d + 6f));
                        var c = Color.Lerp(bg, gold, border);
                        c.a = Mathf.Max(inside, border);
                        var f = face.GetPixelBilinear((x - 24f) / (S - 48f), (y - 16f) / (S - 48f));
                        if (x >= 24 && x < S - 24 && y >= 16 && y < S - 32) c = Color.Lerp(c, new Color(f.r, f.g, f.b, c.a), f.a * inside);
                        tex.SetPixel(x, y, c);
                    }
                tex.Apply();
                File.WriteAllBytes(IconPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(IconPath);
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
                PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log("[MouthCode] 생성: " + path);
            return asset;
        }

        static void CreateScene(GameConfig cfg, ContentDatabase db)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.035f, 0.06f, 0.045f);
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();

            var rootGo = new GameObject("GameRoot");
            var root = rootGo.AddComponent<GameRoot>();
            root.config = cfg;
            root.content = db;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        [MenuItem("MouthCode/Dev/콘텐츠를 임시 기본값으로 덮어쓰기", priority = 110)]
        public static void ResetContentMenu()
        {
            if (!EditorUtility.DisplayDialog("MOUTH CODE", "ContentDatabase의 글자·단어·효과음·대사를 코드의 임시 기본값으로 덮어씁니다.\n직접 넣은 스프라이트/효과음 연결도 사라집니다. 계속할까요?", "덮어쓰기", "취소"))
                return;
            ResetContent();
        }

        /// <summary>배치 모드용. 에셋 GUID는 유지하고 내용만 기본값으로 바꾼다.</summary>
        public static void ResetContent()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(ResDir + "/ContentDatabase.asset");
            if (db == null) { Setup(); return; }
            var fresh = ScriptableObject.CreateInstance<ContentDatabase>();
            db.glyphs = fresh.glyphs;
            db.words = fresh.words;
            db.sfx = fresh.sfx;
            db.hq = fresh.hq;
            db.ranks = fresh.ranks;
            db.meanings = fresh.meanings;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log("[MouthCode] ContentDatabase를 임시 기본값으로 초기화");
        }

        // ───────────── Whisper 모델 다운로드 ─────────────

        static UnityWebRequest download;

        [MenuItem("MouthCode/Download Whisper Model", priority = 20)]
        public static void DownloadModel()
        {
            if (download != null) { Debug.Log("[MouthCode] 이미 다운로드 중"); return; }
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ResDir + "/GameConfig.asset") ?? ScriptableObject.CreateInstance<GameConfig>();
            string dest = Path.Combine(Application.streamingAssetsPath, cfg.whisperModelPath);
            if (File.Exists(dest) && !EditorUtility.DisplayDialog("MOUTH CODE", "모델 파일이 이미 있습니다. 다시 받을까요?\n" + dest, "다시 받기", "취소"))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            string tmp = dest + ".download";
            download = new UnityWebRequest(cfg.whisperModelUrl, UnityWebRequest.kHttpVerbGET)
            {
                downloadHandler = new DownloadHandlerFile(tmp) { removeFileOnAbort = true },
            };
            download.SendWebRequest();
            EditorApplication.update += Poll;

            void Poll()
            {
                if (download == null) { EditorApplication.update -= Poll; return; }
                if (!download.isDone)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Whisper 모델 다운로드", cfg.whisperModelUrl, download.downloadProgress))
                    {
                        download.Abort();
                    }
                    return;
                }
                EditorApplication.update -= Poll;
                EditorUtility.ClearProgressBar();
                if (download.result == UnityWebRequest.Result.Success)
                {
                    download.Dispose();
                    download = null;
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(tmp, dest);
                    AssetDatabase.Refresh();
                    Debug.Log("[MouthCode] 모델 다운로드 완료: " + dest);
                }
                else
                {
                    Debug.LogError("[MouthCode] 모델 다운로드 실패: " + download.error);
                    download.Dispose();
                    download = null;
                }
            }
        }

        // ───────────── 빌드 ─────────────

        [MenuItem("MouthCode/Build Windows (Builds/Windows)", priority = 40)]
        public static void BuildWindows()
        {
            Setup();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/MouthCode.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[MouthCode] 빌드 결과: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)}MB, 에러 {report.summary.totalErrors}");
            // 폰트 라이선스(OFL)는 배포물에 같이 넣어야 한다
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded && System.IO.Directory.Exists(Root + "/Art/Licenses"))
            {
                const string dst = "Builds/Windows/Licenses";
                System.IO.Directory.CreateDirectory(dst);
                foreach (var f in System.IO.Directory.GetFiles(Root + "/Art/Licenses", "*.txt"))
                    System.IO.File.Copy(f, System.IO.Path.Combine(dst, System.IO.Path.GetFileName(f)), true);
            }
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}

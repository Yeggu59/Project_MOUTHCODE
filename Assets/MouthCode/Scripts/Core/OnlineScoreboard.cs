using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MouthCode
{
    /// <summary>
    /// 온라인 스코어보드 (구글 시트 + Apps Script 웹 앱).
    /// 서버 코드와 설치 방법: 저장소의 Tools/scoreboard_apps_script.gs
    /// 주소가 비어 있거나 연결이 안 되면 로컬 스코어보드만 쓴다. 못 보낸 기록은 모아 뒀다가 다음 연결 때 보낸다.
    /// </summary>
    public static class OnlineScoreboard
    {
        [Serializable]
        class Response
        {
            public bool ok;
            public int total;
            public int myPlace;
            public string error;
            public List<ScoreEntry> entries;
        }

        [Serializable]
        class PendingFile { public List<ScoreEntry> entries = new List<ScoreEntry>(); }

        /// <summary>테스트용 덮어쓰기 (명령줄 -mouthcodeScoreboardUrl)</summary>
        public static string OverrideUrl;

        public static string Url(GameConfig cfg)
        {
            if (!string.IsNullOrWhiteSpace(OverrideUrl)) return OverrideUrl.Trim();
            return cfg != null && !string.IsNullOrWhiteSpace(cfg.scoreboardUrl) ? cfg.scoreboardUrl.Trim() : "";
        }

        /// <summary>자동 테스트 중에는 실제 서버에 기록을 올리지 않는다.</summary>
        public static bool Disabled;

        public static bool Enabled(GameConfig cfg) => !Disabled && Url(cfg).Length > 0;

        public class Result
        {
            public bool ok;
            public string error;
            public int total;
            /// <summary>보낸 기록의 전체 순위 (모르면 0)</summary>
            public int myPlace;
            public List<ScoreEntry> entries = new List<ScoreEntry>();
        }

        const int TimeoutSeconds = 10;

        /// <summary>못 보낸 기록을 먼저 보내고(있다면) 새 기록을 보낸 뒤, 전체 순위를 받아온다.</summary>
        public static IEnumerator Sync(GameConfig cfg, ScoreEntry submit, int limit, Action<Result> done)
        {
            string url = Url(cfg);
            var result = new Result();
            if (!Enabled(cfg)) { result.error = "주소 없음"; done?.Invoke(result); yield break; }

            var queue = LoadPending();
            if (submit != null && !queue.Exists(e => e.id == submit.id)) queue.Add(submit);
            SavePending(queue); // 보내는 도중 게임을 꺼도 기록이 남게 먼저 적어 둔다 (성공하면 지운다)

            // 보낼 기록이 있으면 하나씩 보낸다 (POST 응답에도 순위가 같이 온다)
            Result last = null;
            while (queue.Count > 0)
            {
                Result r = null;
                yield return Send(url, JsonUtility.ToJson(queue[0]), limit, x => r = x);
                if (r == null || !r.ok) { last = r; break; }
                queue.RemoveAt(0);
                last = r;
            }
            SavePending(queue);

            if (last == null || !last.ok)
            {
                // 보낼 게 없었거나 보내기에 실패했으면 순위만 다시 받아 본다
                Result r = null;
                yield return Send(url, null, limit, x => r = x, submit?.id);
                if (r != null && r.ok && last != null && !last.ok) r.error = "기록 전송 실패 (다음에 다시 보냄): " + last.error;
                last = r ?? last;
            }
            done?.Invoke(last ?? new Result { error = "응답 없음" });
        }

        /// <summary>
        /// 서버에 한 번 요청한다. submit이 있으면 기록 추가, 없으면 순위 조회.
        /// Apps Script 웹 앱은 응답을 302로 다른 주소에 넘겨주는데, POST는 따라가는 방식이 환경마다 달라서 전부 GET으로 보낸다.
        /// </summary>
        static IEnumerator Send(string url, string submit, int limit, Action<Result> done, string id = null)
        {
            string method = submit != null ? "submit" : "get";
            var sb = new StringBuilder(url);
            sb.Append(url.Contains("?") ? "&" : "?").Append("limit=").Append(limit);
            if (!string.IsNullOrEmpty(id)) sb.Append("&id=").Append(UnityWebRequest.EscapeURL(id));
            if (submit != null) sb.Append("&submit=").Append(UnityWebRequest.EscapeURL(submit));
            using (var req = UnityWebRequest.Get(sb.ToString()))
            {
                req.timeout = TimeoutSeconds;
                req.redirectLimit = 5; // Apps Script는 결과를 다른 주소로 넘겨서 준다 (302)
                yield return req.SendWebRequest();

                var result = new Result();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    result.error = req.error;
                }
                else
                {
                    try
                    {
                        var text = req.downloadHandler.text;
                        var r = JsonUtility.FromJson<Response>(text);
                        if (r == null || !r.ok || r.entries == null)
                            result.error = r != null && !string.IsNullOrEmpty(r.error) ? r.error
                                : text.TrimStart().StartsWith("<") ? "웹 앱 접근 권한 확인 필요 (모든 사용자 허용)" : "응답 형식 오류";
                        else
                        {
                            result.ok = true;
                            result.total = r.total;
                            result.myPlace = r.myPlace;
                            result.entries = r.entries;
                        }
                    }
                    catch (Exception e) { result.error = "응답 해석 실패: " + e.Message; }
                }
                if (!result.ok) Debug.LogWarning($"[MouthCode] 온라인 스코어보드 {method} 실패: {result.error}");
                done(result);
            }
        }

        // ── 못 보낸 기록 보관 (persistentDataPath/scoreboard_pending.json) ──

        static string PendingPath => Path.Combine(Application.persistentDataPath, "scoreboard_pending.json");

        static List<ScoreEntry> LoadPending()
        {
            try
            {
                if (!File.Exists(PendingPath)) return new List<ScoreEntry>();
                return JsonUtility.FromJson<PendingFile>(File.ReadAllText(PendingPath, Encoding.UTF8))?.entries ?? new List<ScoreEntry>();
            }
            catch { return new List<ScoreEntry>(); }
        }

        static void SavePending(List<ScoreEntry> list)
        {
            try
            {
                if (list.Count == 0) { if (File.Exists(PendingPath)) File.Delete(PendingPath); return; }
                // 너무 오래 쌓이지 않게 최근 50개까지만
                if (list.Count > 50) list = list.GetRange(list.Count - 50, 50);
                File.WriteAllText(PendingPath, JsonUtility.ToJson(new PendingFile { entries = list }), Encoding.UTF8);
            }
            catch (Exception e) { Debug.LogWarning("[MouthCode] 미전송 기록 저장 실패: " + e.Message); }
        }
    }
}

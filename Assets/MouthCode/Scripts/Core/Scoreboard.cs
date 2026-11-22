using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MouthCode
{
    [Serializable]
    public class ScoreEntry
    {
        public string id;        // 기록 고유값 (온라인 순위에서 "내 기록" 찾기용)
        public string name;
        public int day;          // 도달한 날 (게임 오버된 날)
        public int score;
        public float accuracy;   // 전체 평균 정확도 0~1
        public bool hard;
        public string rank;      // 최종 계급
        public string date;
        [NonSerialized] public bool isNew;
    }

    /// <summary>로컬 스코어보드. persistentDataPath/scoreboard.json</summary>
    public static class Scoreboard
    {
        public const int MaxEntries = 20;

        [Serializable]
        class FileData { public List<ScoreEntry> entries = new List<ScoreEntry>(); }

        static string FilePath => Path.Combine(Application.persistentDataPath, "scoreboard.json");

        public static List<ScoreEntry> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<ScoreEntry>();
                var data = JsonUtility.FromJson<FileData>(File.ReadAllText(FilePath, Encoding.UTF8));
                return data?.entries ?? new List<ScoreEntry>();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MouthCode] 스코어보드 읽기 실패: " + e.Message);
                return new List<ScoreEntry>();
            }
        }

        /// <summary>기록 추가 후 정렬된 목록을 돌려준다.</summary>
        public static List<ScoreEntry> Add(ScoreEntry entry)
        {
            var list = Load();
            entry.isNew = true;
            list.Add(entry);
            list = list.OrderByDescending(e => e.score).ThenByDescending(e => e.day).Take(MaxEntries).ToList();
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(new FileData { entries = list }, true), Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MouthCode] 스코어보드 저장 실패: " + e.Message);
            }
            return list;
        }
    }
}

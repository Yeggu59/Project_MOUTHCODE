using UnityEngine;

namespace MouthCode
{
    /// <summary>
    /// 밸런스 수치 모음. 코드 수정 없이 Inspector에서 바꾼다.
    /// 에셋 위치: Assets/MouthCode/Resources/GameConfig.asset
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "MouthCode/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("통과 기준 (하루 평균 정확도)")]
        [Range(0f, 1f)] public float passThreshold = 0.5f;

        [Header("하루 메시지 수 = start + (day-1)/everyDays * step (최대 max)")]
        public int messagesStart = 3;
        public int messagesStep = 1;
        public int messagesEveryDays = 3;
        public int messagesMax = 6;

        [Header("메시지 길이(글자 수) = start + perDay x (day-1), 최대 max")]
        [Tooltip("1일차 메시지 글자 수")]
        public float lengthStart = 2f;
        [Tooltip("하루마다 늘어나는 글자 수. 난이도는 새 글자보다 이 길이로 올린다")]
        public float lengthPerDay = 0.8f;
        [Tooltip("메시지 길이 상한")]
        public int lengthMax = 14;
        [Tooltip("메시지마다 길이가 이만큼 위아래로 흔들린다")]
        public float lengthJitter = 0.8f;
        [Tooltip("단어 하나의 최대 글자 수 (긴 메시지는 단어 여러 개로 끊어서 보여 준다)")]
        public int wordMaxGlyphs = 3;
        [Tooltip("새 글자가 나온 날, 단어를 고를 때 새 글자가 든 단어를 고를 확률 (익히라고 자주 보여 줌)")]
        [Range(0f, 1f)] public float newGlyphBias = 0.35f;
        [Tooltip("메시지 1개에 들어갈 수 있는 최대 글자 수 (화면 한계)")]
        public int maxGlyphsPerMessage = 16;

        [Header("제한시간 (메시지 1개당, 초)")]
        [Tooltip("글자 메시지 제한시간 = (base + perGlyph x 글자수) x 일차 배율")]
        public float timeBase = 4f;
        public float timePerGlyph = 0.8f;
        [Tooltip("하루마다 제한시간 배율이 이만큼 줄어든다")]
        public float timeShrinkPerDay = 0.03f;
        [Tooltip("제한시간 배율의 하한")]
        public float timeMinMultiplier = 0.6f;
        [Tooltip("효과음 메시지 제한시간 = 효과음 길이 + extra x 일차 배율")]
        public float sfxTimeExtra = 5f;

        [Header("효과음 메시지")]
        [Tooltip("해금된 효과음이 있을 때, 메시지 하나가 효과음일 확률")]
        [Range(0f, 1f)] public float sfxMessageChance = 0.3f;
        [Tooltip("하루 메시지 중 효과음 비율 상한")]
        [Range(0f, 1f)] public float sfxMaxShare = 0.2f;

        public float SfxTimeLimit(float sfxSeconds, int day) => sfxSeconds + sfxTimeExtra * TimeMultiplier(day);

        [Header("녹음")]
        public KeyCode talkKey = KeyCode.Space;
        public float maxRecordSeconds = 10f;
        [Tooltip("말 시작이 잘리지 않도록 키를 누르기 전 소리를 이만큼 포함 (초)")]
        public float preRollSeconds = 0.25f;
        [Tooltip("이보다 작은 소리(RMS)는 무음으로 본다")]
        public float silenceRms = 0.012f;
        [Tooltip("이보다 짧게 누른 건 실수로 보고 무시한다 (초)")]
        public float minPressSeconds = 0.18f;

        [Header("음성 인식 (Whisper)")]
        [Tooltip("StreamingAssets 기준 경로")]
        public string whisperModelPath = "Whisper/ggml-base.bin";
        public string whisperModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin";
        public string whisperLanguage = "ko";
        [Tooltip("그날 해금된 단어를 인식 힌트(initial prompt)로 넣는다")]
        public bool whisperVocabularyHint = true;
        [Tooltip("GPU(Vulkan)를 먼저 시도한다. 실패하면 자동으로 CPU")]
        public bool whisperUseGpu = true;
        [Tooltip("Whisper 인코더 길이 (0 = 기본 30초). 짧게 하면 CPU에서 빨라지지만 정확도가 떨어질 수 있음")]
        public int whisperAudioCtx = 0;
        [Tooltip("받아쓰기 여러 번 할 때 이 시간(초)을 넘기면 지금까지의 최선으로 끝낸다")]
        public float whisperTimeBudget = 2.5f;
        [Tooltip("한 번 받아쓸 때 최대 토큰 수. 같은 소리를 끝없이 반복하는 오작동을 끊는다")]
        public int whisperMaxTokens = 40;

        [Header("효과음 판정 환산 (사용자 QA: 후하게)")]
        [Tooltip("모양 거리가 이 값 이하면 100%. 자체 테스트: 같은 소리 합성 흉내 ≈ 0.12~0.23, TTS로 읽은 흉내 ≈ 0.29~0.65")]
        public float sfxDistanceGood = 0.40f;
        [Tooltip("모양 거리가 이 값 이상이면 최저 점수. 자체 테스트: 다른 소리 흉내 평균 ≈ 0.46")]
        public float sfxDistanceBad = 1.0f;
        [Tooltip("소리를 제대로 냈으면(너무 짧지 않으면) 최소 이만큼은 준다")]
        [Range(0f, 1f)] public float sfxMinAccuracy = 0.3f;
        [Tooltip("원본과 길이가 이 배율 안쪽이면 길이 차이로 감점하지 않는다")]
        public float sfxLengthTolerance = 1.6f;

        [Header("점수")]
        [Tooltip("메시지 점수 = 정확도(0~100) x 이 값 x 일차")]
        public int scorePerPercent = 10;

        [Header("온라인 스코어보드 (구글 시트)")]
        [Tooltip("구글 Apps Script 웹 앱 주소 (https://script.google.com/macros/s/.../exec). 비워 두면 이 PC에만 저장한다. 설치 방법: Tools/scoreboard_apps_script.gs")]
        public string scoreboardUrl = "";

        [Header("연출")]
        [Tooltip("배경 음악 음량 (0 = 끔). 송신 화면에서는 자동으로 꺼진다")]
        [Range(0f, 1f)] public float musicVolume = 0.14f;
        [Tooltip("실패 시 내 목소리를 이 배율로 재생 (1 이하 = 끔)")]
        public float failPlaybackPitch = 1.8f;
        [Tooltip("이 정확도 이상이면 '완벽' 대사")]
        [Range(0f, 1f)] public float perfectThreshold = 0.95f;

        public int MessagesForDay(int day) =>
            Mathf.Max(1, Mathf.Min(messagesMax, messagesStart + (day - 1) / Mathf.Max(1, messagesEveryDays) * messagesStep));

        /// <summary>그날 메시지의 평균 글자 수.</summary>
        public float LengthForDay(int day) => Mathf.Min(lengthMax, lengthStart + lengthPerDay * (day - 1));

        public float TimeMultiplier(int day) =>
            Mathf.Max(timeMinMultiplier, 1f - timeShrinkPerDay * (day - 1));
    }
}

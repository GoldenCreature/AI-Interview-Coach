using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJS
{
    // 면접 직종 선택용 열거형
    // Inspector에서 드롭다운으로 자동 표시됨
    public enum JobCategory
    {
        IT개발자,
        마케팅,
        디자인,
        영업,
        금융
    }

    // Intensive: 직무 기반 심화 면접 (엄격한 면접관)
    // Casual: 일상적 대화 면접 (친절한 면접관)
    public enum InterviewerType
    {
        Intensive,  // 직무 기반 심화 면접
        Casual      // 일상적 대화 면접
    }

    public class InterviewManager : SingletonBase<InterviewManager>
    {
        // -----------------------------------------------
        // C# 이벤트 정의
        // -----------------------------------------------

        // STT 결과 텍스트가 준비됐을 때 발생
        // 구독자: FillerWordDetector, GeminiManager
        public static event Action<string> OnTranscriptReceived;

        // Gemini 응답이 준비됐을 때 발생
        // 구독자: TextToSpeechManager
        public static event Action<string> OnGeminiResponseReceived;

        // 면접이 시작될 때 발생
        // 구독자: GeminiManager (프롬프트 주입 + 첫 질문)
        public static event Action<JobCategory, InterviewerType> OnInterviewStarted;

        // 면접이 종료될 때 발생
        // 구독자: GeminiManager (종합 평가), DBManager (저장), UIManager (결과 화면)
        public static event Action<InterviewResultData> OnInterviewEnded;

        // -----------------------------------------------
        // 면접 진행 중 데이터 누적
        // -----------------------------------------------

        // 현재 면접이 진행 중인지 여부
        public bool IsInterviewActive { get; private set; }

        // 선택된 직종 (InterviewSetup 씬에서 설정)
        public JobCategory SelectedJob { get; private set; }

        // 선택된 면접관 유형
        public InterviewerType SelectedInterviewerType { get; private set; }

        // 태도 점수 (신모세 MediaPipe에서 설정)
        private int _attitudeScore;

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("[InterviewManager] 초기화 완료");
        }

        private void OnEnable()
        {
            // Interview Room 씬 진입 감지
            // 씬 로드 완료 시 OnSceneLoaded 자동 호출
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // -----------------------------------------------
        // 씬 로드 완료 시 자동 호출
        // Interview Room 씬 진입 시 면접 자동 시작
        // -----------------------------------------------
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Interview Room 씬 진입 시 자동으로 면접 시작
            // Interviewer 씬에서 설정한 직종/유형 그대로 사용
            if (scene.name == "Interview Room")
            {
                Debug.Log("[InterviewManager] Interview Room 진입 감지 → 면접 자동 시작");
                StartInterview();
            }
        }

        // -----------------------------------------------
        // 면접 제어 함수
        // -----------------------------------------------

        // 직종 선택 (InterviewSetup 씬에서 호출)
        public void SetJob(JobCategory job)
        {
            SelectedJob = job;
            Debug.Log($"[InterviewManager] 직종 설정: {job}");
        }

        // 면접관 유형 선택
        public void SetInterviewerType(InterviewerType type)
        {
            SelectedInterviewerType = type;
            Debug.Log($"[InterviewManager] 면접관 유형 설정: {type}");
        }

        // 면접 시작 (면접 씬 로드 후 호출)
        public void StartInterview()
        {
            if (IsInterviewActive)
            {
                Debug.LogWarning("[InterviewManager] 이미 면접이 진행 중입니다.");
                return;
            }

            IsInterviewActive = true;
            _attitudeScore = 0;

            Debug.Log("[InterviewManager] 면접 시작");
            OnInterviewStarted?.Invoke(SelectedJob, SelectedInterviewerType);
        }

        // 면접 종료 (종료 버튼에서 호출)
        public void EndInterview()
        {
            if (!IsInterviewActive)
            {
                Debug.LogWarning("[InterviewManager] 진행 중인 면접이 없습니다.");
                return;
            }

            IsInterviewActive = false;

            // 결과 데이터 취합
            InterviewResultData resultData = new InterviewResultData
            {
                Job = SelectedJob,
                InterviewerType = SelectedInterviewerType,
                AttitudeScore = _attitudeScore
            };

            Debug.Log("[InterviewManager] 면접 종료 → 결과 데이터 전송");
            OnInterviewEnded?.Invoke(resultData);
        }

        // -----------------------------------------------
        // 이벤트 발생 함수 (외부 매니저에서 호출)
        // -----------------------------------------------

        // STT 결과 전달 (SpeechToTextManager에서 호출)
        public static void NotifyTranscriptReceived(string transcript)
        {
            OnTranscriptReceived?.Invoke(transcript);
        }

        // Gemini 응답 전달 (GeminiManager에서 호출)
        public static void NotifyGeminiResponseReceived(string response)
        {
            OnGeminiResponseReceived?.Invoke(response);
        }

        // 태도 점수 설정 (신모세 MediaPipeManager에서 호출)
        public void SetAttitudeScore(int score)
        {
            _attitudeScore = score;
            Debug.Log($"[InterviewManager] 태도 점수 설정: {score}");
        }
    }

    // -----------------------------------------------
    // 면접 종료 시 전달할 결과 데이터 묶음
    // DBManager, UIManager, GeminiManager가 이걸 받아서 처리
    // -----------------------------------------------
    [Serializable]
    public class InterviewResultData
    {
        public JobCategory Job;              // 직종
        public InterviewerType InterviewerType; // 면접관 유형
        public int AttitudeScore;            // 태도 점수 (MediaPipe)
        // 말버릇 카운트는 FillerWordDetector에서 직접 꺼냄
        // 대화 기록은 GeminiManager의 chatHistory에서 직접 꺼냄
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HJS
{
    public enum JobCategory
    {
        IT개발자,
        마케팅,
        디자인,
        영업,
        금융
    }
    public class InterviewManager : SingletonBase<InterviewManager>
    {
        // -----------------------------------------------
        // C# 이벤트 정의
        // 각 매니저들이 이 이벤트를 구독해서 동작함
        // -----------------------------------------------

        // STT 결과 텍스트가 준비됐을 때 발생
        // 구독자: FillerWordDetector, GeminiManager
        public static event Action<string> OnTranscriptReceived;

        // Gemini 응답이 준비됐을 때 발생
        // 구독자: TextToSpeechManager
        public static event Action<string> OnGeminiResponseReceived;

        // 면접이 시작될 때 발생
        // 구독자: GeminiManager (프롬프트 주입 + 첫 질문)
        public static event Action<JobCategory> OnInterviewStarted;

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

        // 태도 점수 (신모세 MediaPipe에서 설정)
        private int _attitudeScore;

        // -----------------------------------------------
        // 면접 제어 함수
        // -----------------------------------------------

        // 직종 선택 (InterviewSetup 씬에서 호출)
        public void SetJob(JobCategory job)
        {
            SelectedJob = job;
            Debug.Log($"[InterviewManager] 직종 설정: {job}");
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
            OnInterviewStarted?.Invoke(SelectedJob);
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
        public JobCategory Job;          // 직종
        public int AttitudeScore;        // 태도 점수 (MediaPipe)
        // 말버릇 카운트는 FillerWordDetector에서 직접 꺼냄
        // 대화 기록은 GeminiManager의 chatHistory에서 직접 꺼냄
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

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
        public static event Action<string> OnTranscriptReceived;

        // Gemini 응답이 준비됐을 때 발생
        public static event Action<string> OnGeminiResponseReceived;

        // 면접이 시작될 때 발생
        // 구독자: GeminiManager (프롬프트 주입 + 첫 질문)
        public static event Action<JobCategory, InterviewerType> OnInterviewStarted;

        // 면접이 종료될 때 발생
        public static event Action<InterviewResultData> OnInterviewEnded;

        // -----------------------------------------------
        // 면접 진행 중 데이터
        // -----------------------------------------------

        public bool IsInterviewActive { get; private set; }
        public JobCategory SelectedJob { get; private set; }

        // 선택된 면접관 유형
        public InterviewerType SelectedInterviewerType { get; private set; }

        private int _attitudeScore;

        // -----------------------------------------------
        // 면접 제어 함수
        // -----------------------------------------------

        // 직종 선택
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

        // 면접 시작
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

        // 면접 종료
        public void EndInterview()
        {
            if (!IsInterviewActive)
            {
                Debug.LogWarning("[InterviewManager] 진행 중인 면접이 없습니다.");
                return;
            }

            IsInterviewActive = false;

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
        // 이벤트 발생 함수
        // -----------------------------------------------

        public static void NotifyTranscriptReceived(string transcript)
        {
            OnTranscriptReceived?.Invoke(transcript);
        }

        public static void NotifyGeminiResponseReceived(string response)
        {
            OnGeminiResponseReceived?.Invoke(response);
        }

        public void SetAttitudeScore(int score)
        {
            _attitudeScore = score;
            Debug.Log($"[InterviewManager] 태도 점수 설정: {score}");
        }
    }

    // 면접 종료 시 전달할 결과 데이터
    [Serializable]
    public class InterviewResultData
    {
        public JobCategory Job;
        public InterviewerType InterviewerType;
        public int AttitudeScore;
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using InterviewDb.Core;
using InterviewDb.Models;

namespace HJS
{
    public class InterviewResultSaver : MonoBehaviour
    {
        // 면접 시작 시간 기록
        private string _startTime;

        // Interview_Session 저장 후 받아오는 session_id
        // Session_Result 저장 시 FK로 사용
        private int _currentSessionId = -1;

        private void OnEnable()
        {
            // 면접 시작 이벤트 구독 → 시작 시간 기록
            InterviewManager.OnInterviewStarted += HandleInterviewStarted;

            // 면접 종료 이벤트 구독 → Interview_Session 저장
            InterviewManager.OnInterviewEnded += HandleInterviewEnded;

            // 평가 완료 이벤트 구독 → Session_Result 저장
            InterviewManager.OnEvaluationReceived += HandleEvaluationReceived;
        }

        private void OnDisable()
        {
            InterviewManager.OnInterviewStarted -= HandleInterviewStarted;
            InterviewManager.OnInterviewEnded -= HandleInterviewEnded;
            InterviewManager.OnEvaluationReceived -= HandleEvaluationReceived;
        }

        // -----------------------------------------------
        // 면접 시작 시 시작 시간 기록
        // -----------------------------------------------
        private void HandleInterviewStarted(JobCategory job, InterviewerType type)
        {
            _startTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _currentSessionId = -1;
            Debug.Log("[InterviewResultSaver] 면접 시작 시간 기록 완료");
        }

        // -----------------------------------------------
        // 면접 종료 시 Interview_Session 저장
        // conversation_log, start_time, end_time, job_category 포함
        // -----------------------------------------------
        private void HandleInterviewEnded(InterviewResultData resultData)
        {
            if (MainThreadDbDispatcher.Instance == null)
            {
                Debug.LogError("[InterviewResultSaver] MainThreadDbDispatcher가 없습니다!");
                return;
            }

            string endTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string startTime = _startTime;
            string job = resultData.Job.ToString();
            string conversationLog = BuildConversationLog();

            MainThreadDbDispatcher.Instance.Enqueue(conn =>
            {
                var session = new InterviewSession
                {
                    JobCategory = job,
                    SessionStatus = "Completed",
                    //StartTime = startTime,
                    EndTime = endTime,
                    ConversationLog = conversationLog
                };

                conn.Insert(session);
                _currentSessionId = session.SessionId;

                Debug.Log($"[InterviewResultSaver] Interview_Session 저장 완료" +
                          $" (session_id: {_currentSessionId})");
            });
        }

        // -----------------------------------------------
        // Gemini 평가 완료 시 Session_Result 저장
        // 음성/내용 점수 저장
        // TODO: [신모세] 태도 점수 MediaPipe 연동 후 score_attitude 추가
        // TODO: [이재혁] voice_result, voice_improvement,
        //                content_result, content_improvement 컬럼 추가 후 저장
        // -----------------------------------------------
        private void HandleEvaluationReceived(string evaluationText)
        {
            if (MainThreadDbDispatcher.Instance == null)
            {
                Debug.LogError("[InterviewResultSaver] MainThreadDbDispatcher가 없습니다!");
                return;
            }

            // InterviewManager에 임시 보관된 파싱 결과 꺼내기
            var resultData = InterviewManager.Instance.EvaluationResult;
            if (resultData == null)
            {
                Debug.LogWarning("[InterviewResultSaver] 평가 결과 데이터가 없습니다.");
                return;
            }

            int sessionId = _currentSessionId;

            MainThreadDbDispatcher.Instance.Enqueue(conn =>
            {
                // session_id가 아직 없으면 저장 불가
                // (Gemini 응답 전에 Interview_Session이 저장 완료되어야 함)
                if (sessionId == -1)
                {
                    Debug.LogWarning("[InterviewResultSaver] session_id 미확보. " +
                                     "Session_Result 저장 보류.");
                    return;
                }

                // SessionResult → SessionResultHardened 교체
                // 새 컬럼 구조에 맞게 각 영역별로 분리 저장
                var result = new SessionResultHardened
                {
                    SessionId = sessionId,

                    // 음성 영역
                    ScoreAudio = resultData.VoiceScore,
                    EvalAudioText = resultData.VoiceResult,
                    AdviceAudioText = resultData.VoiceImprovement,

                    // 내용 영역
                    ScoreContent = resultData.ContentScore,
                    EvalContentText = resultData.ContentResult,
                    AdviceContentText = resultData.ContentImprovement,

                    // 태도 점수 → TODO: [신모세] MediaPipe 연동 후 추가
                    ScoreAttitude = null,

                    // total_score → TODO: [신모세] SetTotalScore() 호출로 채워짐
                    TotalScore = null,

                    // 공용 총평/개선가이드 → 현재 미사용
                    SummaryText = null,
                    AdviceText = null,

                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Version = 1
                };

                conn.Insert(result);
                Debug.Log($"[InterviewResultSaver] Session_Result 저장 완료\n" +
                  $"session_id: {sessionId}\n" +
                  $"음성 점수: {resultData.VoiceScore}/5\n" +
                  $"음성 평가결과: {resultData.VoiceResult}\n" +
                  $"음성 개선사항: {resultData.VoiceImprovement}\n" +
                  $"내용 점수: {resultData.ContentScore}/5\n" +
                  $"내용 평가결과: {resultData.ContentResult}\n" +
                  $"내용 개선사항: {resultData.ContentImprovement}");
            });
        }

        // -----------------------------------------------
        // chatHistory를 DB 저장용 JSON 문자열로 변환
        // DB 트리거에서 speaker 값을 "AI"/"User"로만 허용
        // -----------------------------------------------
        private string BuildConversationLog()
        {
            if (UnityAndGeminiV3.Instance == null) return "[]";

            var history = UnityAndGeminiV3.Instance.chatHistory;
            if (history == null || history.Length == 0) return "[]";

            var jsonParts = new List<string>();

            foreach (var content in history)
            {
                // 시스템 프롬프트 제외
                if (content.role == "user" &&
                    content.parts[0].text.Contains("면접관입니다"))
                    continue;

                if (content.role == "model" &&
                    content.parts[0].text == "네, 면접관 역할을 시작하겠습니다.")
                    continue;

                if (content.role == "user" &&
                    content.parts[0].text == "면접을 시작해주세요.")
                    continue;

                // DB 트리거 speaker 허용값: "AI" / "User"
                string speaker = content.role == "user" ? "User" : "AI";
                string text = content.parts[0].text;

                // 질문 번호 태그 제거
                if (text.Contains("[현재") && text.Contains("번 질문에 대한 답변]"))
                {
                    int tagEnd = text.IndexOf('\n');
                    if (tagEnd != -1)
                        text = text.Substring(tagEnd + 1).Trim();
                }

                // [면접종료] 태그 제거
                text = text.Replace("[면접종료]", "").Trim();

                // JSON 특수문자 이스케이프
                string escapedText = EscapeJson(text);

                jsonParts.Add(
                    $"{{\"speaker\":\"{speaker}\"," +
                    $"\"text\":\"{escapedText}\"}}");
            }

            return $"[{string.Join(",", jsonParts)}]";
        }

        // -----------------------------------------------
        // JSON 문자열 이스케이프 처리
        // -----------------------------------------------
        private string EscapeJson(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
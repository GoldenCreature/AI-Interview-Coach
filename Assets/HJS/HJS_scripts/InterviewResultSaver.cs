using System;
using System.Collections.Generic;
using UnityEngine;
using InterviewDb;

namespace HJS
{
    public class InterviewResultSaver : SingletonBase<InterviewResultSaver>
    {

        protected override void Awake()
        {
            base.Awake(); // SingletonBase의 DontDestroyOnLoad 처리
            Debug.Log("[InterviewResultSaver] 초기화 완료");
        }

        private void OnEnable()
        {
            // 면접 시작 이벤트 구독 → 세션 생성
            InterviewManager.OnInterviewStarted += HandleInterviewStarted;

            // 평가 완료 이벤트 구독 → DB 저장
            InterviewManager.OnEvaluationReceived += HandleEvaluationReceived;
        }

        private void OnDisable()
        {
            InterviewManager.OnInterviewStarted -= HandleInterviewStarted;
            InterviewManager.OnEvaluationReceived -= HandleEvaluationReceived;
        }

        // -----------------------------------------------
        // 면접 시작 시 호출
        // DB에 세션 생성 → session_id 확보
        // -----------------------------------------------
        private void HandleInterviewStarted(JobCategory job, InterviewerType type)
        {
            if (InterviewDbManager.Instance == null)
            {
                Debug.LogError("[InterviewResultSaver] InterviewDbManager가 없습니다!");
                return;
            }


            // DB에 세션 생성 → session_id 자동 확보
            // job_category에 직종 + 면접관 유형 함께 저장
            int sessionId = InterviewDbManager.Instance.StartSession(
                job.ToString(),
                type.ToString()
            );

            Debug.Log($"[InterviewResultSaver] 세션 생성 완료 (session_id: {sessionId})");
        }

        // -----------------------------------------------
        // Gemini 평가 완료 시 호출
        // 파싱된 평가 결과 + 대화 기록 → DB 저장
        // -----------------------------------------------
        private void HandleEvaluationReceived(string evaluationText)
        {
            if (InterviewDbManager.Instance == null)
            {
                Debug.LogError("[InterviewResultSaver] InterviewDbManager가 없습니다!");
                return;
            }

            // InterviewManager에 임시 보관된 파싱 결과 꺼내기
            var resultData = InterviewManager.Instance.EvaluationResult;
            if (resultData == null)
            {
                Debug.LogWarning("[InterviewResultSaver] 평가 결과 데이터가 없습니다.");
                return;
            }


            // 대화 기록 JSON 변환
            string conversationLogJson = BuildConversationLog();

            // InterviewDbManager 통로로 DB 저장
            bool success = InterviewDbManager.Instance.SaveInterviewResult(
                sessionId: InterviewDbManager.Instance.CurrentSessionId,
                scoreAudio: resultData.VoiceScore,
                evalAudioText: resultData.VoiceResult,
                adviceAudioText: resultData.VoiceImprovement,
                scoreContent: resultData.ContentScore,
                evalContentText: resultData.ContentResult,
                adviceContentText: resultData.ContentImprovement,
                conversationLogJson: conversationLogJson
            );

            if (success)
            {
                Debug.Log($"[InterviewResultSaver] DB 저장 완료\n" +
                          $"session_id: {InterviewDbManager.Instance.CurrentSessionId}\n" +
                          $"음성 점수: {resultData.VoiceScore}/5\n" +
                          $"음성 평가결과: {resultData.VoiceResult}\n" +
                          $"음성 개선사항: {resultData.VoiceImprovement}\n" +
                          $"내용 점수: {resultData.ContentScore}/5\n" +
                          $"내용 평가결과: {resultData.ContentResult}\n" +
                          $"내용 개선사항: {resultData.ContentImprovement}");
            }
            else
            {
                Debug.LogWarning("[InterviewResultSaver] DB 저장 실패!");
            }
        }

        // -----------------------------------------------
        // chatHistory → JSON 문자열 변환
        // DB 트리거: json_valid() + speaker "AI"/"User" 검증
        // -----------------------------------------------
        private string BuildConversationLog()
        {
            if (UnityAndGeminiV3.Instance == null) return null;

            var history = UnityAndGeminiV3.Instance.chatHistory;
            if (history == null || history.Length == 0) return null;

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

            // 대화 내용이 없으면 null 반환 (빈 JSON 배열 트리거 오류 방지)
            if (jsonParts.Count == 0) return null;

            return $"[{string.Join(",", jsonParts)}]";
        }

        // -----------------------------------------------
        // JSON 특수문자 이스케이프 처리
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
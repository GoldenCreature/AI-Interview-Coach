// ============================================================
// DummyResultInjector.cs
// ------------------------------------------------------------
// Gemini API 키가 없어서 실제 면접 흐름(InterviewManager)이 막혀 있을 때,
// "면접이 끝났다"고 가정한 더미 데이터를 InterviewDbManager에 직접 넣어서
// DB 저장/조회 파이프라인만 따로 검증하기 위한 도구.
//
// ⚠ InterviewManager/Gemini/웹캠을 전혀 거치지 않고 InterviewDbManager만
//   직접 호출하므로, 씬이나 프리팹 설정과 무관하게 아무 빈 오브젝트에
//   붙여서 어느 씬에서든 실행할 수 있음. (InterviewDbManager.Instance는
//   씬에 없으면 자동으로 생성되도록 이미 만들어져 있음)
//
// 사용법:
//   1) 아무 빈 GameObject에 이 스크립트를 붙임 (테스트용 씬이어도 됨).
//   2) Play 모드에서 Inspector 우클릭 → "더미 면접 결과 주입" 실행.
//   3) Console에서 session_id와 저장/조회 결과 로그를 확인.
//   4) DB Browser for SQLite로 InterviewDatabase.db를 열어
//      Interview_Session / Session_Result에 실제로 값이 들어갔는지
//      눈으로 다시 한번 확인하면 됨.
// ============================================================
using System;
using UnityEngine;
using InterviewDb;

namespace InterviewDb.Testing
{
    public class DummyResultInjector : MonoBehaviour
    {
        [Header("주입할 더미 값 (Inspector에서 수정 가능)")]
        public string jobCategory = "마케팅";
        public string interviewType = "일반";
        public double scoreAudio = 4.0;
        public double scoreContent = 4.5;
        public double scoreAttitude = 4.2;

        [ContextMenu("더미 면접 결과 주입")]
        public void InjectDummyResult()
        {
            var db = InterviewDbManager.Instance;

            // 1) 세션 시작 — Gemini/웹캠 전혀 거치지 않음
            int sessionId = db.StartSession(jobCategory, interviewType);
            Debug.Log($"[DummyResultInjector] 세션 생성됨: session_id={sessionId}");

            // 2) 음성/내용 결과 저장 (한종수 팀장님이 넣게 될 것과 동일한 통로)
            string conversationLog =
                "[{\"speaker\":\"AI\",\"text\":\"자기소개 부탁드립니다.\",\"timestamp\":\"00:00:05\"}," +
                "{\"speaker\":\"User\",\"text\":\"안녕하세요, 저는...\",\"timestamp\":\"00:00:12\"}]";

            bool ok1 = db.SaveInterviewResult(
                sessionId,
                scoreAudio, "발음이 명확하고 목소리 톤이 안정적입니다.", "말 속도를 조금 늦춰보세요.",
                scoreContent, "직무 이해도가 높고 답변이 논리적입니다.", "구체적인 사례를 하나 더 들어보세요.",
                conversationLog);

            // 3) 태도(표정) 결과 저장 (신모세 님이 넣게 될 것과 동일한 통로)
            bool ok2 = db.SaveFaceEvaluation(sessionId, scoreAttitude, "표정 변화가 자연스럽고 시선 처리가 안정적입니다.");

            // 4) 종합 점수 저장 (계산 담당 트리거나 모세님의 스크립트를 거치지 않기 떄문에 지금은 사람이 직접 계산해서 넣어야 함)
            double total = Math.Round((scoreAudio + scoreContent + scoreAttitude) / 3.0, 2);
            bool ok3 = db.SetTotalScore(sessionId, total);

            Debug.Log($"[DummyResultInjector] 저장 결과 — 음성/내용:{ok1}, 태도:{ok2}, 종합점수:{ok3}");

            // 5) 곧바로 다시 읽어서 실제로 잘 들어갔는지 그 자리에서 확인
            var report = db.GetLatestSessionReport();
            if (report != null)
            {
                Debug.Log(
                    $"[DummyResultInjector] 조회 확인 — session_id={report.SessionId}, " +
                    $"음성={report.ScoreAudio}, 내용={report.ScoreContent}, 태도={report.ScoreAttitude}, " +
                    $"종합={report.TotalScore}, 종료시각={report.EndTime}");
            }
            else
            {
                Debug.LogWarning("[DummyResultInjector] 방금 저장한 세션을 다시 조회하지 못했습니다.");
            }
        }
    }
}

// ============================================================
// FaceEvaluationIntegrationTest.cs
// ------------------------------------------------------------
// InterviewResultRepository.SaveFaceEvaluation이 실제로 기존 Session_Result와
// 잘 맞물리는지 확인하는 통합 테스트. 세 가지 상황을 모두 확인.
//
//   (a) 음성/내용 분석이 먼저 끝나 Session_Result 행이 있는 상태에서
//       표정 분석 결과가 나중에 UPDATE로 반영되는 경우
//   (b) 표정 분석이 가장 먼저 끝나 Session_Result 행이 아직 없는 상태에서
//       INSERT로 새로 생기는 경우
//   (c) 실제 MediaPipe 콜백처럼 백그라운드 스레드에서 호출해도
//       MainThreadDbDispatcher를 거치면 안전한지
//
// ⚠ 이번 수정: total_score는 더 이상 자동 계산되지 않으므로, (a)에서
//    SetTotalScore를 직접 호출하는 과정까지 함께 확인.
// ⚠ (c)는 씬에 MainThreadDbDispatcher가 붙은 오브젝트가 이미 있어야
//    동작합니다. 없으면 자동으로 건너뜀.
// ⚠ (c)는 백그라운드 스레드 → 큐 → 다음 Update()에서 처리되므로,
//    로그가 (a)/(b)보다 한 프레임 정도 늦게 찍힐 수 있습니다. 정상.
// ============================================================
using System;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using UnityEngine;
using InterviewDb.Core;
using InterviewDb.Models;

namespace InterviewDb.Testing
{
    public class FaceEvaluationIntegrationTest : MonoBehaviour
    {
        [Tooltip("이 테스트 전용 DB 파일 경로")]
        public string databasePath = "";

        [ContextMenu("Run Face Evaluation Integration Test")]
        public void RunTest()
        {
            string path = string.IsNullOrEmpty(databasePath)
                ? Path.Combine(Application.persistentDataPath, "interview_face_eval_test.db")
                : databasePath;

            using (var conn = new SQLiteConnection(path))
            {
                SchemaBootstrapHardened.ApplySchema(conn);

                Case_A_ExistingResult_ThenFaceUpdate(conn);
                Case_B_FaceFirst_ThenInsert(conn);
            }

            Case_C_ViaBackgroundThreadAndDispatcher();
        }

        private InterviewSession NewSession(string job)
        {
            return new InterviewSession
            {
                JobCategory = job,
                SessionStatus = "Completed",
                StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                EndTime = DateTime.Now.AddMinutes(10).ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        // (a) 음성/내용 분석이 먼저 끝난 상태 → 표정 분석이 나중에 UPDATE
        private void Case_A_ExistingResult_ThenFaceUpdate(SQLiteConnection conn)
        {
            var session = NewSession("IT개발자");
            conn.Insert(session);

            // 음성+내용 분석 모듈이 먼저 결과를 저장했다고 가정 (아직 태도 점수는 없음)
            conn.Insert(new SessionResultHardened
            {
                SessionId = session.SessionId,
                ScoreAudio = 4.0,
                ScoreContent = 4.0,
                SummaryText = "답변이 논리적입니다.",
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Version = 1
            });

            // 표정 분석 모듈이 나중에 도착 (팀원 스크린샷 예시값 그대로 사용)
            InterviewResultRepository.SaveFaceEvaluation(conn, session.SessionId, 4.5, "미소가 아주 자연스럽습니다.");

            var result = conn.Get<SessionResultHardened>(session.SessionId);
            bool attitudeOk = result.ScoreAttitude.HasValue && Math.Abs(result.ScoreAttitude.Value - 4.5) < 0.0001;
            bool adviceOk = result.AdviceText != null && result.AdviceText.Contains("미소가 아주 자연스럽습니다");

            // 이번 수정: total_score는 자동 계산되지 않으므로, 3개 영역이 다 모이면
            // 누군가(표정 분석 담당자)가 SetTotalScore를 직접 호출해야 함
            bool totalStillNullBeforeSet = !result.TotalScore.HasValue;
            InterviewResultRepository.SetTotalScore(conn, session.SessionId, Math.Round((4.0 + 4.0 + 4.5) / 3.0, 2));
            var afterSet = conn.Get<SessionResultHardened>(session.SessionId);
            bool totalOk = afterSet.TotalScore.HasValue &&
                Math.Abs(afterSet.TotalScore.Value - Math.Round((4.0 + 4.0 + 4.5) / 3.0, 2)) < 0.0001;

            Debug.Log(attitudeOk && adviceOk && totalStillNullBeforeSet && totalOk
                ? $"[PASS] (a) 표정 점수 UPDATE 성공, total_score는 자동 계산되지 않다가 SetTotalScore 호출 후 {afterSet.TotalScore}로 반영됨, advice_text=\"{result.AdviceText}\""
                : $"[FAIL] (a) score_attitude={result.ScoreAttitude}, advice_text={result.AdviceText}, total_score(호출 전)={result.TotalScore}, total_score(호출 후)={afterSet.TotalScore}");
        }

        // (b) 표정 분석이 가장 먼저 끝난 상태 → Session_Result가 없어서 새로 INSERT
        private void Case_B_FaceFirst_ThenInsert(SQLiteConnection conn)
        {
            var session = NewSession("디자인");
            conn.Insert(session);

            // 아직 아무 결과도 없는 상태에서 표정 분석이 가장 먼저 끝남
            InterviewResultRepository.SaveFaceEvaluation(conn, session.SessionId, 3.5, "긴장한 표정이 자주 보였습니다.");

            var result = conn.Get<SessionResultHardened>(session.SessionId);
            bool created = result != null && result.ScoreAttitude.HasValue && Math.Abs(result.ScoreAttitude.Value - 3.5) < 0.0001;
            bool totalStillNull = result != null && !result.TotalScore.HasValue; // audio/content가 없으니 total은 아직 NULL이어야 정상

            Debug.Log(created && totalStillNull
                ? "[PASS] (b) 결과 행이 없던 세션에도 새로 INSERT됨, 다른 점수 없으니 total_score는 아직 NULL"
                : $"[FAIL] (b) score_attitude={result?.ScoreAttitude}, total_score={result?.TotalScore}");
        }

        // (c) 백그라운드 스레드에서 호출해도 안전한지 — MainThreadDbDispatcher 경유
        private void Case_C_ViaBackgroundThreadAndDispatcher()
        {
            if (MainThreadDbDispatcher.Instance == null)
            {
                Debug.LogWarning("[SKIP] (c) 씬에 MainThreadDbDispatcher가 없어 이 케이스는 건너뜁니다.");
                return;
            }

            Task.Run(() =>
            {
                // 실제 MediaPipe 콜백이 백그라운드 스레드에서 오는 상황을 흉내
                var session = new InterviewSession
                {
                    JobCategory = "영업",
                    SessionStatus = "Completed",
                    StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                MainThreadDbDispatcher.Instance.Enqueue(conn =>
                {
                    conn.Insert(session);
                    InterviewResultRepository.SaveFaceEvaluation(conn, session.SessionId, 4.0, "표정 변화가 자연스럽습니다.");
                    Debug.Log($"[PASS] (c) 백그라운드 스레드 → Enqueue → 메인 스레드에서 안전하게 저장됨 (session_id={session.SessionId})");
                });
            });
        }
    }
}

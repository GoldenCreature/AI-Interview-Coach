// ============================================================
// SchemaCrudTester.cs
// ------------------------------------------------------------
// 목적: 원본 스키마(App_Setting / Interview_Session / Session_Result /
//       View_Session_Report)를 전혀 수정하지 않고, gilzoide(unity-sqlite-net)
//       ORM/쿼리만으로 각 테이블과 뷰(조인)의 CRUD 및 무결성을 검증.
//
// 사용법:
//   1) 빈 GameObject를 하나 만들고 이 컴포넌트를 붙임.
//   2) Play 시 자동 실행되거나, Inspector에서 컴포넌트명 우클릭 →
//      "Run CRUD Test"로 수동 실행할 수 있음.
//   3) Console 창에서 [PASS]/[FAIL] 로그를 확인.
//
// 주의: 기본적으로 트랜잭션을 롤백하므로 실제 DB에는 테스트 데이터가
//       남지 않음. (rollbackAfterTest = false 로 바꾸면 커밋되어 남음)
// ============================================================
using System;
using System.Collections.Generic;
using System.IO;
using SQLite;
using UnityEngine;
using InterviewDb.Core;
using InterviewDb.Models;

namespace InterviewDb.Testing
{
    public class SchemaCrudTester : MonoBehaviour
    {
        [Tooltip("테스트용 DB 파일 경로. 비워두면 persistentDataPath에 별도 테스트 DB를 생성합니다.")]
        public string databasePath = "";

        [Tooltip("체크 해제 시 테스트 데이터가 실제로 DB에 커밋됩니다 (기본: 롤백하여 흔적 없음).")]
        public bool rollbackAfterTest = true;

        [Tooltip("Play 시 자동으로 테스트를 실행할지 여부")]
        public bool runOnStart = true;

        private readonly List<string> _log = new List<string>();

        private void Start()
        {
            if (runOnStart) RunCrudTest();
        }

        [ContextMenu("Run CRUD Test")]
        public void RunCrudTest()
        {
            _log.Clear();
            string path = ResolvePath();

            using (var conn = new SQLiteConnection(path))
            {
                // 원본 DDL을 그대로 실행 (이미 있으면 IF NOT EXISTS로 스킵)
                SchemaBootstrap.ApplySchema(conn);

                conn.BeginTransaction();
                try
                {
                    TestAppSettingCrud(conn);
                    int sessionId = TestInterviewSessionCrud(conn);
                    TestSessionResultCrud(conn, sessionId);
                    TestViewSessionReportJoin(conn, sessionId);
                    TestCascadeDelete(conn);

                    _log.Add("[SUCCESS] 모든 테이블/뷰 CRUD 및 조인 무결성 테스트 통과");
                }
                catch (Exception ex)
                {
                    _log.Add($"[FAIL] 테스트 중단: {ex.Message}");
                }
                finally
                {
                    if (rollbackAfterTest) conn.Rollback();
                    else conn.Commit();
                }
            }

            Debug.Log(string.Join("\n", _log));
        }

        private string ResolvePath()
        {
            return string.IsNullOrEmpty(databasePath)
                ? Path.Combine(Application.persistentDataPath, "interview_crud_test.db")
                : databasePath;
        }

        // ── ① App_Setting: Read → Update (싱글턴이라 Create/Delete는 테스트 대상 아님) ──
        private void TestAppSettingCrud(SQLiteConnection conn)
        {
            var setting = conn.Get<AppSetting>(1);
            Assert(setting != null, "App_Setting: 싱글턴 행(setting_id=1) 조회");

            setting.VolumeMaster = 0.42;
            setting.DeviceInput = "TestMic";
            setting.Resolution = "2560x1440";
            conn.Update(setting);

            var reread = conn.Get<AppSetting>(1);
            Assert(Math.Abs(reread.VolumeMaster - 0.42) < 0.0001, "App_Setting: volume_master 갱신 확인");
            Assert(reread.DeviceInput == "TestMic", "App_Setting: device_input 갱신 확인");
            Assert(reread.Resolution == "2560x1440", "App_Setting: resolution 갱신 확인");
        }

        // ── ② Interview_Session: Create → Read → Update(면접 종료 처리) ──
        private int TestInterviewSessionCrud(SQLiteConnection conn)
        {
            var session = new InterviewSession
            {
                JobCategory = "IT개발자",
                SessionStatus = "In-Progress",
                //StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            conn.Insert(session);
            Assert(session.SessionId > 0, "Interview_Session: INSERT 후 AUTOINCREMENT session_id 발급");

            var fetched = conn.Get<InterviewSession>(session.SessionId);
            Assert(fetched.JobCategory == "IT개발자", "Interview_Session: job_category 저장값 확인");

            // ④ 면접 종료 시나리오: end_time / session_status / conversation_log 일괄 업데이트
            string log = "[{\"speaker\":\"AI\",\"text\":\"자기소개 부탁드립니다.\",\"timestamp\":\"00:00:05\"}," +
                         "{\"speaker\":\"User\",\"text\":\"안녕하세요, 저는...\",\"timestamp\":\"00:00:12\"}]";
            fetched.EndTime = DateTime.Now.AddMinutes(15).ToString("yyyy-MM-dd HH:mm:ss");
            fetched.SessionStatus = "Completed";
            fetched.ConversationLog = log;
            conn.Update(fetched);

            var reread = conn.Get<InterviewSession>(session.SessionId);
            Assert(reread.SessionStatus == "Completed", "Interview_Session: session_status 갱신 확인");
            Assert(reread.ConversationLog == log, "Interview_Session: conversation_log JSON 저장 확인");

            return session.SessionId;
        }

        // ── ③ Session_Result: Create(FK) → Read → Update, FK 무결성 확인 ──
        private void TestSessionResultCrud(SQLiteConnection conn, int sessionId)
        {
            var result = new SessionResult
            {
                SessionId = sessionId,
                ScoreAudio = 4.0,
                ScoreContent = 3.5,
                ScoreAttitude = 4.5,
                TotalScore = Math.Round((4.0 + 3.5 + 4.5) / 3.0, 2),
                SummaryText = "전반적으로 답변이 논리적이며 전달력이 좋습니다.",
                AdviceText = "시선 처리를 조금 더 안정적으로 유지해보세요.",
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            conn.Insert(result);

            var fetched = conn.Get<SessionResult>(sessionId);
            Assert(fetched != null, "Session_Result: INSERT 후 조회 확인");

            fetched.ScoreAudio = 4.5;
            fetched.TotalScore = Math.Round((4.5 + 3.5 + 4.5) / 3.0, 2);
            conn.Update(fetched);

            var reread = conn.Get<SessionResult>(sessionId);
            Assert(Math.Abs(reread.ScoreAudio.Value - 4.5) < 0.0001, "Session_Result: score_audio 갱신 확인");

            bool fkBlocked = false;
            try
            {
                conn.Insert(new SessionResult { SessionId = -9999, TotalScore = 1.0 });
            }
            catch (SQLiteException)
            {
                fkBlocked = true;
            }
            Assert(fkBlocked, "Session_Result: 존재하지 않는 session_id INSERT 시 FK 제약으로 차단");
        }

        // ── ④ View_Session_Report: 조인 결과 정합성(정상 케이스 + LEFT JOIN NULL 케이스) ──
        private void TestViewSessionReportJoin(SQLiteConnection conn, int sessionId)
        {
            var rows = conn.Query<SessionReportRow>(
                "SELECT * FROM View_Session_Report WHERE session_id = ?", sessionId);
            Assert(rows.Count == 1, "View_Session_Report: session_id로 1건 조회");

            var row = rows[0];
            Assert(row.JobCategory == "IT개발자", "View_Session_Report: Interview_Session 조인 컬럼 확인");
            Assert(row.TotalScore.HasValue, "View_Session_Report: Session_Result 조인 컬럼 확인");
            Assert(row.DurationSeconds.HasValue && row.DurationSeconds.Value > 0,
                "View_Session_Report: duration_seconds 계산 확인");
            Assert(!string.IsNullOrEmpty(row.ConversationLog), "View_Session_Report: conversation_log 패스스루 확인");

            // Session_Result가 없는 세션 → LEFT JOIN으로 NULL 컬럼과 함께 1건 조회되어야 함
            var orphan = new InterviewSession
            {
                JobCategory = "디자인",
                SessionStatus = "Aborted",
                //StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            conn.Insert(orphan);

            var orphanRows = conn.Query<SessionReportRow>(
                "SELECT * FROM View_Session_Report WHERE session_id = ?", orphan.SessionId);
            Assert(orphanRows.Count == 1, "View_Session_Report: 결과 없는 세션도 LEFT JOIN으로 조회됨");
            Assert(!orphanRows[0].TotalScore.HasValue,
                "View_Session_Report: Session_Result 없는 세션은 total_score가 NULL");
        }

        // ── ⑤ 삭제 연쇄(CASCADE): Interview_Session 삭제 시 Session_Result 동반 삭제 확인 ──
        private void TestCascadeDelete(SQLiteConnection conn)
        {
            var temp = new InterviewSession
            {
                JobCategory = "영업",
                SessionStatus = "Completed",
                //StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                EndTime = DateTime.Now.AddMinutes(10).ToString("yyyy-MM-dd HH:mm:ss")
            };
            conn.Insert(temp);
            conn.Insert(new SessionResult
            {
                SessionId = temp.SessionId,
                TotalScore = 3.0,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") // NOT NULL 컬럼 — ORM Insert는 SQL DEFAULT를 타지 않으므로 반드시 명시
            });

            conn.Delete<InterviewSession>(temp.SessionId);

            var orphanResult = conn.Find<SessionResult>(temp.SessionId);
            Assert(orphanResult == null, "CASCADE: Interview_Session 삭제 시 Session_Result 동반 삭제 확인");
        }

        private void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            _log.Add("[PASS] " + message);
        }
    }
}

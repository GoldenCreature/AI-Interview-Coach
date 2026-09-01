// ============================================================
// SchemaIntegrityDefenseTest.cs
// ------------------------------------------------------------
// SchemaAnomalyDemo.cs와 정확히 같은 7가지 공격을 강화된 스키마
// (SchemaBootstrapHardened)에 대해 다시 시도해서, 이번엔 DB 자체가
// 실제로 막아주는지 검증.
//
//   [방어 성공] = 공격이 차단됨 / 데이터가 안전하게 유지됨 (원하는 결과)
//   [방어 실패] = 공격이 그대로 먹힘 (스키마를 더 보강해야 함)
//
// ⚠ 반드시 SchemaAnomalyDemo/SchemaCrudTester와는 다른, 이 스크립트
//    전용의 새 DB 파일에서 실행해야 함. (기존 파일에는 CHECK/트리거가
//    소급 적용되지 않습니다)
// ============================================================
using System;
using System.IO;
using System.Collections.Generic;
using SQLite;
using UnityEngine;
using InterviewDb.Core;
using InterviewDb.Models;

namespace InterviewDb.Testing
{
    public class SchemaIntegrityDefenseTest : MonoBehaviour
    {
        [Tooltip("강화된 스키마 전용 DB 파일 경로. 기존 테스트 DB와 반드시 다른 파일이어야 합니다.")]
        public string databasePath = "";

        private readonly List<string> _log = new List<string>();

        [ContextMenu("Run Defense Test")]
        public void RunDefenseTest()
        {
            _log.Clear();
            string path = ResolvePath();

            using (var conn = new SQLiteConnection(path))
            {
                SchemaBootstrapHardened.ApplySchema(conn);

                Demo1_SingletonInsertAttempt(conn);
                Demo2_UpdateAnomaly_AutoRecalculated(conn);
                Demo3_DomainAnomaly_InvalidStatus(conn);
                Demo4_TemporalAnomaly_EndBeforeStart(conn);
                Demo5_1NFAnomaly_InvalidLog(conn);
                Demo6_ReferentialAnomaly_TriggerGuard(path);
                Demo7_LostUpdate_NaiveVsOptimistic(path);
            }

            Debug.Log(string.Join("\n", _log));
        }

        private string ResolvePath()
        {
            return string.IsNullOrEmpty(databasePath)
                ? Path.Combine(Application.persistentDataPath, "interview_hardened_test.db")
                : databasePath;
        }

        private InterviewSession NewSession(string job, string lang, string status, bool withEndTime)
        {
            var s = new InterviewSession
            {
                JobCategory = job,
                InterviewLang = lang,
                SessionStatus = status,
                StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            if (withEndTime) s.EndTime = DateTime.Now.AddMinutes(10).ToString("yyyy-MM-dd HH:mm:ss");
            return s;
        }

        // ① 싱글턴 삽입 시도 — 기존과 동일하게 여전히 막혀야 함 (회귀 확인)
        private void Demo1_SingletonInsertAttempt(SQLiteConnection conn)
        {
            try
            {
                conn.Execute("INSERT INTO App_Setting (volume_master) VALUES (0.9);");
                _log.Add("[방어 실패] App_Setting 싱글턴 제약이 뚫림");
            }
            catch (SQLiteException)
            {
                _log.Add("[방어 성공] App_Setting 싱글턴 삽입이 CHECK로 차단됨");
            }
        }

        // ② 갱신 이상 — score_audio만 갱신해도 트리거가 total_score를 자동 재계산하는지 확인
        private void Demo2_UpdateAnomaly_AutoRecalculated(SQLiteConnection conn)
        {
            var session = NewSession("마케팅", "KO", "Completed", withEndTime: true);
            conn.Insert(session);
            conn.Insert(new SessionResultHardened
            {
                SessionId = session.SessionId,
                ScoreAudio = 3.0,
                ScoreContent = 3.0,
                ScoreAttitude = 3.0,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                // total_score는 일부러 채우지 않음 — AFTER INSERT 트리거가 자동 계산해야 함
            });

            var afterInsert = conn.Get<SessionResultHardened>(session.SessionId);
            bool insertOk = afterInsert.TotalScore.HasValue && Math.Abs(afterInsert.TotalScore.Value - 3.0) < 0.0001;

            // score_audio만 갱신 (예전엔 total_score가 방치되던 지점)
            conn.Execute("UPDATE Session_Result SET score_audio = 5.0 WHERE session_id = ?", session.SessionId);

            var afterUpdate = conn.Get<SessionResultHardened>(session.SessionId);
            double expected = Math.Round((5.0 + 3.0 + 3.0) / 3.0, 2);
            bool updateOk = afterUpdate.TotalScore.HasValue && Math.Abs(afterUpdate.TotalScore.Value - expected) < 0.0001;

            if (insertOk && updateOk)
                _log.Add($"[방어 성공] score_audio만 갱신해도 트리거가 total_score를 {afterUpdate.TotalScore.Value}로 자동 재계산함");
            else
                _log.Add($"[방어 실패] total_score={afterUpdate.TotalScore}, 기대값={expected}");
        }

        // ③ 도메인 이상 — 정의되지 않은 session_status는 CHECK로 거부되어야 함
        private void Demo3_DomainAnomaly_InvalidStatus(SQLiteConnection conn)
        {
            try
            {
                conn.Insert(NewSession("금융", "KO", "완전히_잘못된_상태값", withEndTime: false));
                _log.Add("[방어 실패] 정의되지 않은 session_status 값이 그대로 저장됨");
            }
            catch (SQLiteException)
            {
                _log.Add("[방어 성공] session_status CHECK 제약이 잘못된 값을 차단함");
            }
        }

        // ④ 시간 순서 이상 — end_time < start_time은 CHECK로 거부되어야 함
        private void Demo4_TemporalAnomaly_EndBeforeStart(SQLiteConnection conn)
        {
            try
            {
                conn.Insert(new InterviewSession
                {
                    JobCategory = "IT개발자",
                    InterviewLang = "EN",
                    SessionStatus = "Completed",
                    StartTime = "2026-07-31 15:00:00",
                    EndTime = "2026-07-31 14:00:00"
                });
                _log.Add("[방어 실패] end_time < start_time 인 행이 그대로 저장됨");
            }
            catch (SQLiteException)
            {
                _log.Add("[방어 성공] end_time >= start_time CHECK 제약이 시간 역전을 차단함");
            }
        }

        // ⑤ 1NF성 이상 — 깨진 JSON과 정의되지 않은 speaker 값 모두 트리거/CHECK로 거부되어야 함
        private void Demo5_1NFAnomaly_InvalidLog(SQLiteConnection conn)
        {
            // 5-1) 문법이 깨진 JSON
            try
            {
                var s1 = NewSession("디자인", "KO", "Completed", withEndTime: true);
                s1.ConversationLog = "[{\"speaker\":\"Robot\",\"text\":\"질문입니다\",";
                conn.Insert(s1);
                _log.Add("[방어 실패] 문법이 깨진 conversation_log가 그대로 저장됨");
            }
            catch (SQLiteException)
            {
                _log.Add("[방어 성공] json_valid() CHECK가 깨진 JSON을 차단함");
            }

            // 5-2) 문법은 맞지만 정의되지 않은 speaker 값("Robot")
            try
            {
                var s2 = NewSession("디자인", "KO", "Completed", withEndTime: true);
                s2.ConversationLog = "[{\"speaker\":\"Robot\",\"text\":\"질문입니다\",\"timestamp\":\"00:00:01\"}]";
                conn.Insert(s2);
                _log.Add("[방어 실패] 정의되지 않은 speaker 값이 그대로 저장됨");
            }
            catch (SQLiteException)
            {
                _log.Add("[방어 성공] speaker 검증 트리거가 정의되지 않은 값을 차단함");
            }
        }

        // ⑥ 참조 무결성 이상 — pragma foreign_keys가 꺼진 연결에서도 트리거는 항상 작동해야 함
        private void Demo6_ReferentialAnomaly_TriggerGuard(string path)
        {
            using (var rawConn = new SQLiteConnection(path)) // ApplySchema(=pragma ON)를 거치지 않은 생짜 연결
            {
                try
                {
                    rawConn.Execute("INSERT INTO Session_Result (session_id, total_score, created_at) VALUES (-1, 1.0, datetime('now'));");
                    _log.Add("[방어 실패] foreign_keys pragma가 꺼진 연결에서 고아 Session_Result 행이 생성됨");
                    rawConn.Execute("DELETE FROM Session_Result WHERE session_id = -1;");
                }
                catch (SQLiteException)
                {
                    _log.Add("[방어 성공] BEFORE INSERT 트리거가 pragma 상태와 무관하게 존재하지 않는 session_id를 차단함");
                }
            }
        }

        // ⑦ 갱신 손실 — (a) 낙관적 동시성 제어 없이 쓰면 여전히 취약함 vs (b) version 체크를 쓰면 방어됨
        private void Demo7_LostUpdate_NaiveVsOptimistic(string path)
        {
            // (a) 나쁜 예: version을 확인하지 않고 그냥 덮어쓰기 — 스키마만으로는 못 막음
            using (var connA = new SQLiteConnection(path))
            using (var connB = new SQLiteConnection(path))
            {
                var session = NewSession("IT개발자", "KO", "Completed", withEndTime: true);
                connA.Insert(session);
                connA.Insert(new SessionResultHardened
                {
                    SessionId = session.SessionId,
                    TotalScore = 3.0,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });

                var readA = connA.Get<SessionResultHardened>(session.SessionId);
                var readB = connB.Get<SessionResultHardened>(session.SessionId);

                connA.Execute("UPDATE Session_Result SET total_score = ? WHERE session_id = ?",
                    readA.TotalScore.Value + 0.5, session.SessionId); // 3.5
                connB.Execute("UPDATE Session_Result SET total_score = ? WHERE session_id = ?",
                    readB.TotalScore.Value + 1.0, session.SessionId); // 4.0 (A의 갱신을 덮어씀)

                var finalNaive = connA.Get<SessionResultHardened>(session.SessionId);
                if (Math.Abs(finalNaive.TotalScore.Value - 4.0) < 0.0001)
                {
                    _log.Add("[방어 실패(예상된 결과)] version 체크 없이 UPDATE하면 스키마가 있어도 여전히 갱신 손실이 발생함 " +
                              "→ 반드시 애플리케이션 쿼리가 WHERE version = ? 를 사용해야 함");
                }
            }

            // (b) 올바른 예: WHERE version = ? 로 낙관적 동시성 제어
            using (var connA = new SQLiteConnection(path))
            using (var connB = new SQLiteConnection(path))
            {
                var session = NewSession("IT개발자", "KO", "Completed", withEndTime: true);
                connA.Insert(session);
                connA.Insert(new SessionResultHardened
                {
                    SessionId = session.SessionId,
                    TotalScore = 3.0,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Version = 1 // ORM Insert는 SQL DEFAULT를 타지 않으므로 명시적으로 지정 (안 하면 0으로 저장됨)
                });

                var readA = connA.Get<SessionResultHardened>(session.SessionId); // version=1
                var readB = connB.Get<SessionResultHardened>(session.SessionId); // version=1 (동시에 읽었다고 가정)

                int rowsA = connA.Execute(
                    "UPDATE Session_Result SET total_score = ?, version = version + 1 WHERE session_id = ? AND version = ?",
                    readA.TotalScore.Value + 0.5, session.SessionId, readA.Version); // 성공 → version 2

                int rowsB = connB.Execute(
                    "UPDATE Session_Result SET total_score = ?, version = version + 1 WHERE session_id = ? AND version = ?",
                    readB.TotalScore.Value + 1.0, session.SessionId, readB.Version); // 실패해야 함 (이미 version이 2라서 조건 불일치)

                if (rowsA == 1 && rowsB == 0)
                {
                    var final = connA.Get<SessionResultHardened>(session.SessionId);
                    _log.Add($"[방어 성공] WHERE version=? 낙관적 동시성 제어로 B의 뒤늦은 갱신이 거부됨 " +
                              $"(최종 total_score={final.TotalScore}, version={final.Version}) → A의 갱신 손실 없음");
                }
                else
                {
                    _log.Add($"[방어 실패] 예상과 다르게 rowsA={rowsA}, rowsB={rowsB}");
                }
            }
        }
    }
}

// ============================================================
// SchemaAnomalyDemo.cs
// ------------------------------------------------------------
// 목적: 스키마 자체는 전혀 수정하지 않고, "쿼리 사용 패턴"만으로 실제 발생할 수
//       있는 DB 이상현상(anomaly)들을 gilzoide(unity-sqlite-net) 위에서 재현합니다.
//       Console에 [이상현상 발생]이 뜨면 실제로 재현된 것이고,
//       [정상 차단]이 뜨면 스키마 제약이 정상적으로 막아준 것입니다.
//
// 재현하는 이상현상 목록:
//   ① 싱글턴 삽입 시도       — App_Setting 2번째 행 삽입 (정상적으로는 차단되어야 함, 대조군)
//   ② 갱신 이상(Update)      — score_audio만 갱신하고 total_score(파생값)는 방치
//   ③ 도메인 무결성 위반     — session_status에 정의되지 않은 값 저장
//   ④ 시간 순서 위반         — end_time < start_time → duration_seconds 음수로 연쇄
//   ⑤ 1NF성 이상             — conversation_log(JSON 문자열)의 형식/값 미검증
//   ⑥ 참조 무결성 이상       — PRAGMA foreign_keys가 연결 단위 설정임을 이용한 고아 행 생성
//   ⑦ 갱신 손실(Lost Update) — 두 연결이 같은 행을 동시에 읽고-계산-써서 한쪽 갱신이 소실
//
// ⚠ 주의: 이 스크립트는 "의도적으로" 데이터 무결성을 깨뜨립니다.
//         반드시 실제 서비스 DB가 아닌 별도의 데모 전용 DB 파일에서만 실행하세요.
// ============================================================
using System;
using System.Linq;
using System.IO;
using SQLite;
using UnityEngine;
using InterviewDb.Core;
using InterviewDb.Models;

namespace InterviewDb.Testing
{
    public class SchemaAnomalyDemo : MonoBehaviour
    {
        [Tooltip("데모 전용 DB 파일 경로. 절대 실제 서비스 DB를 지정하지 마세요.")]
        public string databasePath = "";

        [ContextMenu("Run Anomaly Demos")]
        public void RunAnomalyDemos()
        {
            string path = ResolvePath();

            using (var conn = new SQLiteConnection(path))
            {
                SchemaBootstrap.ApplySchema(conn);

                Demo1_SingletonInsertAttempt(conn);
                Demo2_UpdateAnomaly_StaleTotalScore(conn);
                Demo3_DomainAnomaly_InvalidStatus(conn);
                Demo4_TemporalAnomaly_EndBeforeStart(conn);
                Demo5_1NFAnomaly_MalformedJsonLog(conn);
                Demo6_ReferentialAnomaly_ForeignKeysOffByDefault(path);
                Demo7_LostUpdateAnomaly(path);
            }
        }

        private string ResolvePath()
        {
            return string.IsNullOrEmpty(databasePath)
                ? Path.Combine(Application.persistentDataPath, "interview_anomaly_demo.db")
                : databasePath;
        }

        // 데모①: App_Setting 싱글턴 제약 우회 시도 (정상적으로는 차단되는 대조군 케이스)
        private void Demo1_SingletonInsertAttempt(SQLiteConnection conn)
        {
            try
            {
                // setting_id를 지정하지 않고 INSERT → SQLite가 새 rowid(보통 2)를 자동 채번
                // → CHECK(setting_id = 1) 위반으로 거부되는 것이 정상
                conn.Execute("INSERT INTO App_Setting (volume_master) VALUES (0.9);");
                Debug.LogWarning("[이상현상 발생] App_Setting에 2번째 행이 삽입됨 — 싱글턴 제약이 깨졌습니다.");
            }
            catch (SQLiteException ex)
            {
                Debug.Log($"[정상 차단] App_Setting 싱글턴 제약(CHECK)이 정상 동작함: {ex.Message}");
            }
        }

        // 데모②: 갱신 이상(Update Anomaly) — score_audio만 갱신하고 total_score(파생값)는 방치
        private void Demo2_UpdateAnomaly_StaleTotalScore(SQLiteConnection conn)
        {
            var session = NewSession("마케팅", "KO", "Completed", withEndTime: true);
            conn.Insert(session);
            conn.Insert(new SessionResult
            {
                SessionId = session.SessionId,
                ScoreAudio = 3.0,
                ScoreContent = 3.0,
                ScoreAttitude = 3.0,
                TotalScore = 3.0,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") // NOT NULL 컬럼 — ORM Insert는 SQL DEFAULT를 타지 않으므로 반드시 명시
            });

            // 실무에서 흔한 실수: 점수 하나만 수정하고 total_score 재계산을 빠뜨림
            conn.Execute("UPDATE Session_Result SET score_audio = 5.0 WHERE session_id = ?", session.SessionId);

            var result = conn.Get<SessionResult>(session.SessionId);
            double expected = Math.Round(
                (result.ScoreAudio.Value + result.ScoreContent.Value + result.ScoreAttitude.Value) / 3.0, 2);

            if (Math.Abs(result.TotalScore.Value - expected) > 0.0001)
            {
                Debug.LogWarning(
                    $"[이상현상 발생: 갱신 이상] 저장된 total_score={result.TotalScore.Value}, " +
                    $"실제 평균={expected} → DB에 트리거/CHECK가 없어 파생값이 방치됨");
            }
        }

        // 데모③: 도메인 이상 — session_status에 정의되지 않은 값도 제약 없이 저장됨
        private void Demo3_DomainAnomaly_InvalidStatus(SQLiteConnection conn)
        {
            var session = NewSession("금융", "KO", "완전히_잘못된_상태값", withEndTime: false);
            conn.Insert(session);

            var reread = conn.Get<InterviewSession>(session.SessionId);
            if (reread.SessionStatus == "완전히_잘못된_상태값")
            {
                Debug.LogWarning(
                    "[이상현상 발생: 도메인 무결성 위반] session_status에 " +
                    "'In-Progress'/'Completed'/'Aborted' 외의 값이 제약 없이 저장됨 " +
                    "(CHECK(session_status IN (...)) 부재)");
            }
        }

        // 데모④: end_time이 start_time보다 빠른 값도 저장 가능 → duration_seconds 음수로 연쇄됨
        private void Demo4_TemporalAnomaly_EndBeforeStart(SQLiteConnection conn)
        {
            var session = new InterviewSession
            {
                JobCategory = "IT개발자",
                InterviewLang = "EN",
                SessionStatus = "Completed",
                StartTime = "2026-07-31 15:00:00",
                EndTime = "2026-07-31 14:00:00"
            };
            conn.Insert(session);

            var row = conn.Query<SessionReportRow>(
                "SELECT * FROM View_Session_Report WHERE session_id = ?", session.SessionId).FirstOrDefault();

            if (row != null && row.DurationSeconds.HasValue && row.DurationSeconds.Value < 0)
            {
                Debug.LogWarning(
                    $"[이상현상 발생: 시간 순서 위반] duration_seconds={row.DurationSeconds.Value} (음수) " +
                    "→ end_time >= start_time 제약 부재로 인한 파생 이상현상");
            }
        }

        // 데모⑤: conversation_log는 JSON 배열 문자열이라 DB가 형식/값 검증을 전혀 못함 (1NF성 이상)
        private void Demo5_1NFAnomaly_MalformedJsonLog(SQLiteConnection conn)
        {
            var session = NewSession("디자인", "KO", "Completed", withEndTime: true);
            // 괄호가 닫히지 않은 깨진 JSON + 정의되지 않은 speaker("Robot") 값
            session.ConversationLog = "[{\"speaker\":\"Robot\",\"text\":\"질문입니다\",";
            conn.Insert(session);

            var reread = conn.Get<InterviewSession>(session.SessionId);
            Debug.LogWarning(
                "[이상현상 발생: 비정규화 컬럼 검증 불가] conversation_log에 문법이 깨졌거나 " +
                $"정의되지 않은 speaker 값이 제약 없이 그대로 저장됨: {reread.ConversationLog}");
        }

        // 데모⑥: PRAGMA foreign_keys는 '연결 단위' 설정 — 다시 켜주지 않으면 FK가 조용히 무시됨
        //         (원본 DDL 맨 앞에 PRAGMA foreign_keys = ON; 이 있지만, 이는 그 pragma를 실행한
        //          "그 연결"에만 적용되고 DB 파일에 영구 저장되는 옵션이 아닙니다)
        private void Demo6_ReferentialAnomaly_ForeignKeysOffByDefault(string path)
        {
            // ApplySchema를 거치지 않은 '생짜' 연결 → foreign_keys 기본값(OFF)인 상태
            using (var rawConn = new SQLiteConnection(path))
            {
                bool orphanCreated;
                try
                {
                    rawConn.Execute("INSERT INTO Session_Result (session_id, total_score) VALUES (-1, 1.0);");
                    orphanCreated = true;
                }
                catch (SQLiteException)
                {
                    orphanCreated = false;
                }

                if (orphanCreated)
                {
                    Debug.LogWarning(
                        "[이상현상 발생: 참조 무결성 위반] 이 연결에서 foreign_keys가 꺼져 있어 " +
                        "존재하지 않는 session_id(-1)를 참조하는 고아(orphan) Session_Result 행이 생성됨");
                    rawConn.Execute("DELETE FROM Session_Result WHERE session_id = -1;");
                }
                else
                {
                    Debug.Log("[정상 차단] FK 제약이 정상 동작하여 참조 무결성 위반이 차단됨");
                }
            }
        }

        // 데모⑦: 갱신 손실(Lost Update) — 두 연결이 같은 행을 "읽기→계산→쓰기"로 다룸
        //         (실제 멀티스레드 경합이 아니라, 그 인터리빙 패턴을 순차 코드로 재현한 것입니다)
        private void Demo7_LostUpdateAnomaly(string path)
        {
            using (var connA = new SQLiteConnection(path))
            using (var connB = new SQLiteConnection(path))
            {
                var session = NewSession("IT개발자", "KO", "Completed", withEndTime: true);
                connA.Insert(session);
                connA.Insert(new SessionResult
                {
                    SessionId = session.SessionId,
                    TotalScore = 3.0,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") // NOT NULL 컬럼 — ORM Insert는 SQL DEFAULT를 타지 않으므로 반드시 명시
                });

                // A, B가 동시에 같은 값(3.0)을 읽었다고 가정
                var readA = connA.Get<SessionResult>(session.SessionId);
                var readB = connB.Get<SessionResult>(session.SessionId);

                readA.TotalScore += 0.5; // A는 +0.5 보정
                readB.TotalScore += 1.0; // B는 +1.0 보정 (서로 다른 계산)

                connA.Update(readA); // 3.5로 반영
                connB.Update(readB); // 4.0으로 덮어씀 → A의 갱신이 사라짐

                var final = connA.Get<SessionResult>(session.SessionId);
                if (Math.Abs(final.TotalScore.Value - 3.5) < 0.0001)
                {
                    Debug.LogWarning(
                        $"[이상현상 발생: 갱신 손실(Lost Update)] 최종 total_score={final.TotalScore.Value} " +
                        "→ A의 갱신(+0.5)이 B의 갱신(+1.0)에 덮어써져 사라짐");
                }
            }
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
    }
}

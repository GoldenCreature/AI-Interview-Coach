// ============================================================
// SchemaBootstrapHardened.cs
// ------------------------------------------------------------
// 원본 스키마에 최소한의 CHECK 제약과 트리거를
// 추가하여, DB 자체가 이상현상을 능동적으로 막아주도록 강화한 버전.
//
// 기존 SchemaBootstrap.cs(원본, 무수정)는 그대로 남겨두고, 이 파일은 별도의
// "스키마"로 독립된 다른 DB 파일에 적용하는 것을 전제로 함.
// ⚠ 기존에 만들어진 .db 파일에는 CREATE TABLE IF NOT EXISTS가 적용되지
//   않으므로(테이블이 이미 있으면 그냥 스킵됨),  새 DB 파일 경로에서
//   테스트해야 함. 기존 파일을 재사용하면 옛 테이블 구조가 그대로 남아
//   CHECK/트리거가 전혀 적용되지 않게 됨.
//
// 추가된 방어 장치 요약:
//   ① App_Setting 싱글턴          — (기존 유지) CHECK(setting_id = 1)
//   ② 갱신 이상(total_score)      — AFTER INSERT/UPDATE 트리거로 자동 재계산
//   ③ 도메인 무결성(session_status) — CHECK(session_status IN (...))
//   ④ 시간 순서(end_time)          — CHECK(end_time IS NULL OR end_time >= start_time)
//   ⑤ 1NF성 이상(conversation_log) — CHECK(json_valid(...)) + BEFORE 트리거로 speaker 값 검증
//   ⑥ 참조 무결성(FK pragma 무관)  — BEFORE INSERT 트리거로 존재 여부 직접 검사
//                                    (연결의 foreign_keys pragma 상태와 무관하게 항상 작동)
//                                    + AFTER DELETE 트리거로 수동 CASCADE(역시 pragma와 무관)
//   ⑦ 갱신 손실(Lost Update)      — version 컬럼(낙관적 동시성 제어). 단, 앱 쿼리가
//                                    WHERE version = ? 를 실제로 사용해야 방어됨
//                                    (스키마만으로는 100% 손실을 막을 수는 없음 — SchemaIntegrityDefenseTest.cs 참고)
// ============================================================
using SQLite;

namespace InterviewDb.Core
{
    public static class SchemaBootstrapHardened
    {
        private static readonly string[] SchemaStatements = new string[]
        {
            "PRAGMA foreign_keys = ON;",
            "PRAGMA journal_mode = WAL;",

            // ── App_Setting: 기존과 동일 (이미 CHECK로 보호됨) ──
            @"CREATE TABLE IF NOT EXISTS App_Setting (
    setting_id      INTEGER PRIMARY KEY DEFAULT 1,
    volume_master   REAL    NOT NULL DEFAULT 1.0,
    device_input    TEXT    NOT NULL DEFAULT 'Default',
    device_output   TEXT    NOT NULL DEFAULT 'Default',
    resolution      TEXT    NOT NULL DEFAULT '1920x1080',
    is_fullscreen   INTEGER NOT NULL DEFAULT 1,
    CHECK (setting_id = 1)
);",

            "INSERT OR IGNORE INTO App_Setting (setting_id) VALUES (1);",

            // ── Interview_Session: session_status / end_time / conversation_log에 CHECK 추가 ──
            @"CREATE TABLE IF NOT EXISTS Interview_Session (
    session_id        INTEGER PRIMARY KEY AUTOINCREMENT,
    job_category       TEXT    NOT NULL DEFAULT 'IT',
    interview_lang      TEXT    NOT NULL DEFAULT 'KO',
    session_status     TEXT    NOT NULL DEFAULT 'Completed'
                        CHECK (session_status IN ('In-Progress', 'Completed', 'Aborted')),
    start_time         TEXT    NOT NULL DEFAULT (datetime('now', 'localtime')),
    end_time           TEXT    NULL
                        CHECK (end_time IS NULL OR end_time >= start_time),
    conversation_log   TEXT    NULL
                        CHECK (conversation_log IS NULL OR json_valid(conversation_log))
);",

            @"CREATE INDEX IF NOT EXISTS idx_session_time
ON Interview_Session(start_time DESC);",

            // ── Session_Result: version 컬럼(낙관적 동시성 제어용) 추가 ──
            @"CREATE TABLE IF NOT EXISTS Session_Result (
    session_id     INTEGER PRIMARY KEY,
    score_audio    REAL    NULL,
    score_content  REAL    NULL,
    score_attitude REAL    NULL,
    total_score    REAL    NULL,
    summary_text   TEXT    NULL,
    advice_text    TEXT    NULL,
    created_at     TEXT    NOT NULL DEFAULT (datetime('now', 'localtime')),
    version        INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (session_id) REFERENCES Interview_Session(session_id) ON DELETE CASCADE
);",

            "DROP VIEW IF EXISTS View_Session_Report;",

            @"CREATE VIEW View_Session_Report AS
SELECT
    s.session_id,
    s.job_category,
    s.interview_lang,
    s.start_time,
    s.end_time,
    CAST((julianday(s.end_time) - julianday(s.start_time)) * 86400 AS INTEGER) AS duration_seconds,
    s.conversation_log,
    r.score_audio,
    r.score_content,
    r.score_attitude,
    r.total_score,
    r.summary_text,
    r.advice_text
FROM Interview_Session s
LEFT JOIN Session_Result r ON s.session_id = r.session_id;",

            // ── 트리거 ⑥: FK를 '연결 pragma와 무관하게' 스키마 자체가 직접 검사 ──
            @"CREATE TRIGGER IF NOT EXISTS trg_session_result_fk_guard_insert
BEFORE INSERT ON Session_Result
FOR EACH ROW
WHEN NOT EXISTS (SELECT 1 FROM Interview_Session WHERE session_id = NEW.session_id)
BEGIN
    SELECT RAISE(ABORT, 'Session_Result.session_id references a non-existent Interview_Session row');
END;",

            @"CREATE TRIGGER IF NOT EXISTS trg_interview_session_cascade_delete
AFTER DELETE ON Interview_Session
FOR EACH ROW
BEGIN
    DELETE FROM Session_Result WHERE session_id = OLD.session_id;
END;",

            // ── 트리거 ②: total_score 자동 재계산 (score_* 3개가 모두 채워졌을 때만) ──
            @"CREATE TRIGGER IF NOT EXISTS trg_session_result_total_after_insert
AFTER INSERT ON Session_Result
FOR EACH ROW
WHEN NEW.score_audio IS NOT NULL AND NEW.score_content IS NOT NULL AND NEW.score_attitude IS NOT NULL
BEGIN
    UPDATE Session_Result
    SET total_score = ROUND((NEW.score_audio + NEW.score_content + NEW.score_attitude) / 3.0, 2)
    WHERE session_id = NEW.session_id;
END;",

            @"CREATE TRIGGER IF NOT EXISTS trg_session_result_total_after_update
AFTER UPDATE OF score_audio, score_content, score_attitude ON Session_Result
FOR EACH ROW
WHEN NEW.score_audio IS NOT NULL AND NEW.score_content IS NOT NULL AND NEW.score_attitude IS NOT NULL
BEGIN
    UPDATE Session_Result
    SET total_score = ROUND((NEW.score_audio + NEW.score_content + NEW.score_attitude) / 3.0, 2)
    WHERE session_id = NEW.session_id;
END;",

            // ── 트리거 ⑦ 보조: score/텍스트가 바뀔 때마다 version 자동 증가 ──
            @"CREATE TRIGGER IF NOT EXISTS trg_session_result_version_bump
AFTER UPDATE OF score_audio, score_content, score_attitude, summary_text, advice_text ON Session_Result
FOR EACH ROW
BEGIN
    UPDATE Session_Result SET version = OLD.version + 1 WHERE session_id = NEW.session_id;
END;",

            // ── 트리거 ⑤: conversation_log의 JSON 구조 + speaker 값 검증 (INSERT) ──
            @"CREATE TRIGGER IF NOT EXISTS trg_interview_session_validate_log_insert
BEFORE INSERT ON Interview_Session
FOR EACH ROW
WHEN NEW.conversation_log IS NOT NULL
BEGIN
    SELECT RAISE(ABORT, 'conversation_log: invalid JSON syntax')
    WHERE NOT json_valid(NEW.conversation_log);

    SELECT RAISE(ABORT, 'conversation_log: contains an undefined speaker value')
    WHERE json_valid(NEW.conversation_log)
      AND EXISTS (
          SELECT 1 FROM json_each(NEW.conversation_log)
          WHERE json_extract(value, '$.speaker') NOT IN ('AI', 'User')
      );
END;",

            // ── 트리거 ⑤: conversation_log의 JSON 구조 + speaker 값 검증 (UPDATE) ──
            @"CREATE TRIGGER IF NOT EXISTS trg_interview_session_validate_log_update
BEFORE UPDATE OF conversation_log ON Interview_Session
FOR EACH ROW
WHEN NEW.conversation_log IS NOT NULL
BEGIN
    SELECT RAISE(ABORT, 'conversation_log: invalid JSON syntax')
    WHERE NOT json_valid(NEW.conversation_log);

    SELECT RAISE(ABORT, 'conversation_log: contains an undefined speaker value')
    WHERE json_valid(NEW.conversation_log)
      AND EXISTS (
          SELECT 1 FROM json_each(NEW.conversation_log)
          WHERE json_extract(value, '$.speaker') NOT IN ('AI', 'User')
      );
END;"
        };

        /// <summary>수정된 스키마를 적용. 반드시 해당 DB 파일에서만 사용.</summary>
        public static void ApplySchema(SQLiteConnection conn)
        {
            foreach (var statement in SchemaStatements)
            {
                if (statement.IndexOf("journal_mode", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    conn.ExecuteScalar<string>(statement);
                }
                else
                {
                    conn.Execute(statement);
                }
            }
        }
    }
}

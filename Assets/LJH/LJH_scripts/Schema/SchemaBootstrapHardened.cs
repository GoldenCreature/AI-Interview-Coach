// ============================================================
// SchemaBootstrapHardened.cs
// ------------------------------------------------------------
// "이번 수정" — 팀장님 피드백 반영.
//   - App_Setting 테이블 전체 제거 (더 이상 사용 안 함)
//   - Interview_Session.interview_lang 컬럼 제거 (한국어 면접만 진행)
//   - total_score 자동 계산 트리거 제거 (표정 분석 담당자가 직접 계산)
//
// 기존 SchemaBootstrap.cs(5차 원본, 무수정)는 그대로 남겨두고, 이 파일은
// 별도의 "강화된 스키마"로 완전히 독립된 DB 파일에 적용하는 것을 전제로 함.
// ⚠ 기존에 만들어진 .db 파일에는 CREATE TABLE IF NOT EXISTS가 적용되지
//   않으므로(테이블이 이미 있으면 그냥 스킵됨), 반드시 새 DB 파일 경로에서
//   테스트해야 함.
//
// 현재 방어 장치 요약:
//   ① 도메인 무결성(session_status) — CHECK(session_status IN (...))
//   ② 시간 순서(end_time)          — CHECK(end_time IS NULL OR end_time >= start_time)
//   ③ 1NF성 이상(conversation_log) — CHECK(json_valid(...)) + BEFORE 트리거로 speaker 값 검증
//   ④ 참조 무결성(FK pragma 무관)  — BEFORE INSERT 트리거로 존재 여부 직접 검사
//                                    (연결의 foreign_keys pragma 상태와 무관하게 항상 작동)
//                                    + AFTER DELETE 트리거로 수동 CASCADE(역시 pragma와 무관)
//   ⑤ 갱신 손실(Lost Update)      — version 컬럼(낙관적 동시성 제어)
//
// ⚠ total_score는 더 이상 DB가 자동 계산하지 않음. score_audio/
//   score_content/score_attitude가 다 채워져도 트리거가 동작하지 않으니,
//   반드시 InterviewResultRepository.SetTotalScore(...)로 직접 채워야 함.
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

            // ── Interview_Session: interview_lang 제거, session_status / end_time / conversation_log CHECK 유지 ──
            @"CREATE TABLE IF NOT EXISTS Interview_Session (
    session_id        INTEGER PRIMARY KEY AUTOINCREMENT,
    job_category       TEXT    NOT NULL DEFAULT 'IT',
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

            // ── Session_Result: 영역별 평가/개선사항 컬럼 추가 (저번 수정 - Result Scene 연동 규격화) ──
            // 음성/내용은 [점수 + 평가 텍스트 + 개선사항 텍스트] 3개씩 전용 컬럼.
            // 태도는 요구사항대로 단일 점수만 유지(전용 텍스트 컬럼 없음) —
            // 표정 관련 코멘트는 기존 summary_text/advice_text(공용)에 계속 누적됨.
            @"CREATE TABLE IF NOT EXISTS Session_Result (
    session_id           INTEGER PRIMARY KEY,
    score_audio           REAL    NULL,
    eval_audio_text       TEXT    NULL,
    advice_audio_text     TEXT    NULL,
    score_content         REAL    NULL,
    eval_content_text     TEXT    NULL,
    advice_content_text   TEXT    NULL,
    score_attitude        REAL    NULL,
    total_score           REAL    NULL,
    summary_text          TEXT    NULL,
    advice_text           TEXT    NULL,
    created_at            TEXT    NOT NULL DEFAULT (datetime('now', 'localtime')),
    version                INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (session_id) REFERENCES Interview_Session(session_id) ON DELETE CASCADE
);",

            "DROP VIEW IF EXISTS View_Session_Report;",

            @"CREATE VIEW View_Session_Report AS
SELECT
    s.session_id,
    s.job_category,
    s.start_time,
    s.end_time,
    CAST((julianday(s.end_time) - julianday(s.start_time)) * 86400 AS INTEGER) AS duration_seconds,
    s.conversation_log,
    r.score_audio,
    r.eval_audio_text,
    r.advice_audio_text,
    r.score_content,
    r.eval_content_text,
    r.advice_content_text,
    r.score_attitude,
    r.total_score,
    r.summary_text,
    r.advice_text
FROM Interview_Session s
LEFT JOIN Session_Result r ON s.session_id = r.session_id;",

            // ── 트리거 ④: FK를 '연결 pragma와 무관하게' 스키마 자체가 직접 검사 ──
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

            // ── total_score 자동 계산 트리거는 제거됨 (이번 수정) ──
            // 표정 분석 담당 팀원이 점수를 직접 계산해 합산하는 방식으로 변경되어,
            // DB가 임의로 total_score를 덮어쓰지 않도록 트리거를 삭제했음.
            // 이제 total_score는 InterviewResultRepository.SetTotalScore(...)로만 채워집니다.

            // ── 트리거: score/텍스트/total_score가 바뀔 때마다 version 자동 증가 ──
            @"CREATE TRIGGER IF NOT EXISTS trg_session_result_version_bump
AFTER UPDATE OF score_audio, eval_audio_text, advice_audio_text,
                 score_content, eval_content_text, advice_content_text,
                 score_attitude, total_score, summary_text, advice_text ON Session_Result
FOR EACH ROW
BEGIN
    UPDATE Session_Result SET version = OLD.version + 1 WHERE session_id = NEW.session_id;
END;",

            // ── 트리거 ③: conversation_log의 JSON 구조 + speaker 값 검증 (INSERT) ──
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

            // ── 트리거 ③: conversation_log의 JSON 구조 + speaker 값 검증 (UPDATE) ──
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

        /// <summary>강화된 스키마를 적용합니다. 반드시 전용 DB 파일에서만 사용하여야 함.</summary>
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

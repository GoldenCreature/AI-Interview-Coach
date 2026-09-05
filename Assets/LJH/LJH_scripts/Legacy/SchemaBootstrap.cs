// ============================================================
// SchemaBootstrap.cs
// ------------------------------------------------------------
// 원본 DB 명세서(이전 수정)의 DDL 내용을 "글자 하나도 바꾸지 않고" 그대로 담아
// SQLiteConnection에 실행해주는 헬퍼입니다.
//
// ⚠ SQLiteConnection.Execute()는 문자열 하나당 SQL 구문을 1개만 실행하기
//   때문에(sqlite3_prepare_v2 특성), 원본 스크립트를 여러 CREATE TABLE을
//   한 번에 실행할 수 없습니다. 그래서 원본 DDL을 "구문 단위"로만 나누어
//   배열에 담았을 뿐, 각 구문의 내용(컬럼/제약조건/기본값 등)은 원본과
//   완전히 동일합니다. (섹션 구분용 배너 주석만 이 파일의 상단 설명으로
//   대체했고, CHECK/컬럼 설명 등 의미 있는 인라인 주석은 그대로 유지했습니다.)
// ============================================================
using SQLite;

namespace InterviewDb.Core
{
    public static class SchemaBootstrap
    {
        // 원본 DDL 그대로 (내용 수정 없음) — 구문 단위로만 분리
        private static readonly string[] SchemaStatements = new string[]
        {
            // 런타임 환경 설정: 외래키 및 WAL 모드 활성화
            "PRAGMA foreign_keys = ON;",
            "PRAGMA journal_mode = WAL;",

            // 1. App_Setting (앱 환경 설정 — 1행만 존재하는 테이블)
            @"CREATE TABLE IF NOT EXISTS App_Setting (
    setting_id      INTEGER PRIMARY KEY DEFAULT 1,
    volume_master   REAL    NOT NULL DEFAULT 1.0,
    device_input    TEXT    NOT NULL DEFAULT 'Default',
    device_output   TEXT    NOT NULL DEFAULT 'Default',
    resolution      TEXT    NOT NULL DEFAULT '1920x1080',
    is_fullscreen   INTEGER NOT NULL DEFAULT 1,
    CHECK (setting_id = 1)  -- 로컬 단일 사용자 환경: 항상 1개 행만 존재하도록 강제
);",

            // 기본 설정값 초기 삽입 (앱 최초 실행 시 1회)
            "INSERT OR IGNORE INTO App_Setting (setting_id) VALUES (1);",

            // 2. Interview_Session (면접 세션 이력 테이블)
            @"CREATE TABLE IF NOT EXISTS Interview_Session (
    session_id        INTEGER PRIMARY KEY AUTOINCREMENT,
    job_category       TEXT    NOT NULL DEFAULT 'IT',
    interview_lang      TEXT    NOT NULL DEFAULT 'KO',
    session_status     TEXT    NOT NULL DEFAULT 'Completed',
    start_time         TEXT    NOT NULL DEFAULT (datetime('now', 'localtime')),
    end_time           TEXT    NULL,
    conversation_log   TEXT    NULL   -- STT/TTS 전체 대화 내용, JSON 배열 문자열
);",

            // 조회 최적화 인덱스 (최신 면접 이력 정렬)
            @"CREATE INDEX IF NOT EXISTS idx_session_time
ON Interview_Session(start_time DESC);",

            // 3. Session_Result (면접 최종 리포트 데이터 테이블)
            @"CREATE TABLE IF NOT EXISTS Session_Result (
    session_id     INTEGER PRIMARY KEY,
    score_audio    REAL    NULL,
    score_content  REAL    NULL,
    score_attitude REAL    NULL,
    total_score    REAL    NULL,
    summary_text   TEXT    NULL,
    advice_text    TEXT    NULL,
    created_at     TEXT    NOT NULL DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (session_id) REFERENCES Interview_Session(session_id) ON DELETE CASCADE
);",

            // 4. View_Session_Report (uGUI 결과 화면 조회 전용 뷰)
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
LEFT JOIN Session_Result r ON s.session_id = r.session_id;"
        };

        /// <summary>
        /// 원본 스키마를 있는 그대로 적용합니다. (IF NOT EXISTS 이므로 반복 호출해도 안전)
        /// CRUD 테스터와 이상현상 데모가 이 메서드를 공통으로 사용하여,
        /// 두 코드 모두 항상 동일한(원본 그대로의) 스키마 위에서 동작함을 보장합니다.
        /// </summary>
        public static void ApplySchema(SQLiteConnection conn)
        {
            foreach (var statement in SchemaStatements)
            {
                // ⚠ PRAGMA journal_mode = ...; 는 SET 형태로 실행해도 SQLite가
                //   변경된 저널 모드를 결과 행(row) 하나로 돌려줍니다.
                //   Execute()(ExecuteNonQuery)는 "완료(Done)"만 정상으로 받아들이고
                //   행이 돌아오면 예외("not an error")를 던지므로, 이 문장만
                //   결과 행을 받아줄 수 있는 ExecuteScalar로 실행합니다.
                //   (다른 PRAGMA/DDL 문장은 그대로 Execute 사용 — 스키마 내용은 미변경)
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

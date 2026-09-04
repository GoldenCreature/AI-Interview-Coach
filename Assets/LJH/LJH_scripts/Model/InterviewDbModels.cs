// ============================================================
// InterviewDbModels.cs
// ------------------------------------------------------------
// gilzoide/unity-sqlite-net(SQLite-net) ORM 매핑 전용 모델 클래스.
// ⚠ 이 파일은 테이블/뷰를 "새로 만들지" 않음. 이미 만들어진 스키마에
//    C# 클래스를 매핑만 하게 됨. → conn.CreateTable<T>() 를 호출하면 안됨.
//
// ⚠ App_Setting은 이번 수정으로 완전히 제거되어 이 파일에 없음.
//    (Legacy 스크립트용 사본은 Legacy/LegacyModels.cs에 별도 보관)
// ⚠ interview_lang도 이번 수정으로 제거되어 아래 클래스들에 없음.
//    (한국어 면접만 진행하기로 결정함)
// ============================================================
using SQLite;

namespace InterviewDb.Models
{
    /// <summary>Interview_Session — 면접 세션 이력 + STT/TTS 대화 로그(JSON 배열 문자열)</summary>
    [Table("Interview_Session")]
    public class InterviewSession
    {
        [PrimaryKey, AutoIncrement, Column("session_id")]
        public int SessionId { get; set; }

        [Column("job_category")]
        public string JobCategory { get; set; }

        [Column("session_status")]
        public string SessionStatus { get; set; }

        [Column("start_time")]
        public string StartTime { get; set; }

        [Column("end_time")]
        public string EndTime { get; set; }

        [Column("conversation_log")]
        public string ConversationLog { get; set; }
    }

    /// <summary>③ Session_Result — 면접 최종 결과 (Interview_Session과 1:1, session_id 공유 PK/FK)</summary>
    [Table("Session_Result")]
    public class SessionResult
    {
        [PrimaryKey, Column("session_id")]
        public int SessionId { get; set; }

        [Column("score_audio")]
        public double? ScoreAudio { get; set; }

        [Column("score_content")]
        public double? ScoreContent { get; set; }

        [Column("score_attitude")]
        public double? ScoreAttitude { get; set; }

        [Column("total_score")]
        public double? TotalScore { get; set; }

        [Column("summary_text")]
        public string SummaryText { get; set; }

        [Column("advice_text")]
        public string AdviceText { get; set; }

        [Column("created_at")]
        public string CreatedAt { get; set; }
    }

    /// <summary>④ View_Session_Report — 조회 전용 가상 뷰 (SELECT만 가능, Insert/Update/Delete 불가)</summary>
    [Table("View_Session_Report")]
    public class SessionReportRow
    {
        [Column("session_id")]
        public int SessionId { get; set; }

        [Column("job_category")]
        public string JobCategory { get; set; }

        [Column("start_time")]
        public string StartTime { get; set; }

        [Column("end_time")]
        public string EndTime { get; set; }

        [Column("duration_seconds")]
        public int? DurationSeconds { get; set; }

        [Column("conversation_log")]
        public string ConversationLog { get; set; }

        [Column("score_audio")]
        public double? ScoreAudio { get; set; }

        [Column("eval_audio_text")]
        public string EvalAudioText { get; set; }

        [Column("advice_audio_text")]
        public string AdviceAudioText { get; set; }

        [Column("score_content")]
        public double? ScoreContent { get; set; }

        [Column("eval_content_text")]
        public string EvalContentText { get; set; }

        [Column("advice_content_text")]
        public string AdviceContentText { get; set; }

        [Column("score_attitude")]
        public double? ScoreAttitude { get; set; }

        [Column("total_score")]
        public double? TotalScore { get; set; }

        [Column("summary_text")]
        public string SummaryText { get; set; }

        [Column("advice_text")]
        public string AdviceText { get; set; }
    }
}

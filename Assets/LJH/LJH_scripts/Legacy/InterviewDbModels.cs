// ============================================================
// InterviewDbModels.cs
// ------------------------------------------------------------
// gilzoide/unity-sqlite-net(SQLite-net) ORM 매핑 전용 모델 클래스.
// ⚠ 이 파일은 테이블/뷰를 "새로 만들지" 않음 . 원본 DDL(App_Setting,
//    Interview_Session, Session_Result, View_Session_Report)로
//    이미 만들어진 스키마에 C# 클래스를 매핑만 하는 역할.
//    → conn.CreateTable<T>() 를 호출하면 안됨. (원본 스키마와 다른
//       테이블이 새로 생성될 수 있음. 테이블 생성은 SchemaBootstrap.cs가
//       원본 DDL을 그대로 실행하는 방식으로만 수행.)
// ============================================================
using SQLite;

namespace InterviewDb.Models
{
    /// <summary>① App_Setting — 앱 전체 단일 설정 행 (setting_id = 1 고정)</summary>
    [Table("App_Setting")]
    public class AppSetting
    {
        [PrimaryKey, Column("setting_id")]
        public int SettingId { get; set; }

        [Column("volume_master")]
        public double VolumeMaster { get; set; }

        [Column("device_input")]
        public string DeviceInput { get; set; }

        [Column("device_output")]
        public string DeviceOutput { get; set; }

        [Column("resolution")]
        public string Resolution { get; set; }

        [Column("is_fullscreen")]
        public int IsFullscreen { get; set; } // SQLite는 BOOL 타입이 없어 0/1 INTEGER 그대로 매핑
    }

    /// <summary>② Interview_Session — 면접 세션 이력 + STT/TTS 대화 로그(JSON 배열 문자열)</summary>
    [Table("Interview_Session")]
    public class InterviewSession
    {
        [PrimaryKey, AutoIncrement, Column("session_id")]
        public int SessionId { get; set; }

        [Column("job_category")]
        public string JobCategory { get; set; }

        [Column("interview_lang")]
        public string InterviewLang { get; set; }

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

        [Column("interview_lang")]
        public string InterviewLang { get; set; }

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

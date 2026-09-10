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
//
// - SessionReportRow에 SessionStatus 추가
// - 3x3 표 태도 영역 바인딩을 위한 EvalAttitudeText, AdviceAttitudeText 지원
// ============================================================
using SQLite;

namespace InterviewDb.Models
{
    /// <summary>Interview_Session 테이블 매핑 엔티티</summary>
    [Table("Interview_Session")]
    public class InterviewSession
    {
        [PrimaryKey, AutoIncrement, Column("session_id")]
        public int SessionId { get; set; }

        [Column("job_category")]
        public string JobCategory { get; set; }

        [Column("session_status")]
        public string SessionStatus { get; set; }

        [Column("end_time")]
        public string EndTime { get; set; }

        [Column("duration_seconds")]
        public int? DurationSeconds { get; set; }

        [Column("conversation_log")]
        public string ConversationLog { get; set; }
    }

    /// <summary>Session_Result 테이블 매핑 엔티티</summary>
    [Table("Session_Result")]
    public class SessionResult
    {
        [PrimaryKey, Column("session_id")]
        public int SessionId { get; set; }

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

        [Column("created_at")]
        public string CreatedAt { get; set; }
    }

    /// <summary>
    /// View_Session_Report 가상 뷰 조회 전용 DTO
    /// 한효준 팀원의 ResultUI, FeedbackListUI 화면 바인딩 대상
    /// </summary>
    [Table("View_Session_Report")]
    public class SessionReportRow
    {
        [Column("session_id")]
        public int SessionId { get; set; }

        [Column("job_category")]
        public string JobCategory { get; set; }

        // [추가] 세션 상태 (In-Progress, Completed, Aborted)
        [Column("session_status")]
        public string SessionStatus { get; set; }

        [Column("end_time")]
        public string EndTime { get; set; }

        [Column("duration_seconds")]
        public int? DurationSeconds { get; set; }

        [Column("conversation_log")]
        public string ConversationLog { get; set; }

        // ── 5점 척도 점수 (3개 영역 + 종합) ──
        [Column("score_audio")]
        public double? ScoreAudio { get; set; }

        [Column("score_content")]
        public double? ScoreContent { get; set; }

        [Column("score_attitude")]
        public double? ScoreAttitude { get; set; }

        [Column("total_score")]
        public double? TotalScore { get; set; }

        // ── 음성 영역 피드백 (3x3 표 1행) ──
        [Column("eval_audio_text")]
        public string EvalAudioText { get; set; }

        [Column("advice_audio_text")]
        public string AdviceAudioText { get; set; }

        // ── 내용 영역 피드백 (3x3 표 2행) ──
        [Column("eval_content_text")]
        public string EvalContentText { get; set; }

        [Column("advice_content_text")]
        public string AdviceContentText { get; set; }

        // ── 태도 영역 피드백 원본 컬럼 (3x3 표 3행) ──
        [Column("summary_text")]
        public string SummaryText { get; set; }

        [Column("advice_text")]
        public string AdviceText { get; set; }

        // ── [편의 기능] 한효준 팀원 전용 프로퍼티 ──
        // summary_text와 advice_text를 태도 평가 텍스트로 바로 꺼내 쓸 수 있도록 매핑
        [Ignore]
        public string EvalAttitudeText
        {
            get => SummaryText;
            set => SummaryText = value;
        }

        [Ignore]
        public string AdviceAttitudeText
        {
            get => AdviceText;
            set => AdviceText = value;
        }
    }
}
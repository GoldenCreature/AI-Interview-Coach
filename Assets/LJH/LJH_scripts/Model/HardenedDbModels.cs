// ============================================================
// HardenedDbModels.cs
// ------------------------------------------------------------
// 강화된 스키마(SchemaBootstrapHardened) 전용 추가 모델.
// App_Setting / Interview_Session / View_Session_Report는 컬럼 구조가
// 기존과 동일하므로 InterviewDb.Models의 기존 클래스(InterviewDbModels.cs)를
// 그대로 재사용함. Session_Result만 version 컬럼이 새로 추가되어
// 별도 클래스로 매핑. (기존 SessionResult 클래스를 건드리면
// SchemaCrudTester/SchemaAnomalyDemo가 깨지므로 새 클래스로 분리)
// ============================================================
using SQLite;

namespace InterviewDb.Models
{
    [Table("Session_Result")]
    public class SessionResultHardened
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

        [Column("version")]
        public int Version { get; set; }
    }
}

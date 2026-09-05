// ============================================================
// HardenedDbModels.cs
// ------------------------------------------------------------
// 현재 수정 버전 스키마(SchemaBootstrapHardened) 전용 추가 모델.
// Interview_Session / View_Session_Report는 InterviewDb.Models의 기존
// 클래스(InterviewDbModels.cs)를 그대로 재사용. Session_Result만
// 영역별 텍스트 컬럼 + version 컬럼이 추가되어 별도 클래스로 매핑.
// (App_Setting은 이번 수정으로 완전히 제거되어 여기에서 사라짐)
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

        /// <summary>⚠ 더 이상 DB가 자동 계산하지 않음. InterviewResultRepository.SetTotalScore(...)로 직접 채워줘야 함.</summary>
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

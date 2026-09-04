using SQLite;

public class FaceScoreEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Timestamp { get; set; }

    // 상세 코멘트 (여러 줄 가능하므로 SQLite TEXT 컬럼에 그대로 저장됨)
    public string EvaluationDetail { get; set; }

    // 개선사항 (감정별 구체적인 코멘트, 총평과는 별도)
    public string ImprovementNotes { get; set; }

    // 0~5점으로 정규화된 종합 평가 점수
    public float EvaluationScore { get; set; }
}
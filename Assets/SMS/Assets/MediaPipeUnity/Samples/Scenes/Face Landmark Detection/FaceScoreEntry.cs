using SQLite;

public class FaceScoreEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Timestamp { get; set; }

    // 이번 구간(smoothing window) 점수
    public float SmileScore { get; set; }
    public float SurpriseScore { get; set; }
    public float AngryScore { get; set; }

    // 면접용 종합 평가 결과 (fass.cs의 EvaluateExpression 결과)
    // ExpressionGrade enum은 SQLite에 직접 저장할 수 없으므로 string으로 변환해서 저장.
    // (fass.cs에서 grade.ToString()으로 넘기면 됨. 예: "Excellent", "Good", "Poor" 등)
    public string EvaluationGrade { get; set; }

    // 상세 코멘트 (여러 줄 가능하므로 SQLite TEXT 컬럼에 그대로 저장됨)
    public string EvaluationDetail { get; set; }

    // 0~5점으로 정규화된 종합 평가 점수
    public float EvaluationScore { get; set; }
}
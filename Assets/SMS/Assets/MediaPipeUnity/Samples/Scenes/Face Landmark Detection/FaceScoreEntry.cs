using SQLite;

public class FaceScoreEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Timestamp { get; set; }

    public float SmileScore { get; set; }
    public float SurpriseScore { get; set; }
    public float AngryScore { get; set; }

    // [추가] 감정별 누적 합계/평균 (저장 시점까지의 누적값 스냅샷)
    public float SmileTotal { get; set; }
    public float SmileAverage { get; set; }

    public float SurpriseTotal { get; set; }
    public float SurpriseAverage { get; set; }

    public float AngryTotal { get; set; }
    public float AngryAverage { get; set; }
}
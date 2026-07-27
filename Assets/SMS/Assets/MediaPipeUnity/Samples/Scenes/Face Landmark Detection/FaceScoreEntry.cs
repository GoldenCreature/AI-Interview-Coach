using SQLite;
using System;

public class FaceScoreEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Timestamp { get; set; }
    public float SmileScore { get; set; }
    public float SurpriseScore { get; set; }
    public float AngryScore { get; set; }
}
using SQLite;
using UnityEngine;
using System;
using System.IO;

public class FaceScoreDBLogger : MonoBehaviour
{
    private SQLiteConnection db;

    [Header("저장 경로 설정")]
    [Tooltip("DB 파일을 저장할 폴더의 전체 경로. 비워두면 기본 persistentDataPath를 사용합니다. 예: D:\\FaceScoreData")]
    public string customFolderPath = "";

    [Tooltip("DB 파일명")]
    public string dbFileName = "FaceScores.db";

    void Awake()
    {
        string folderPath;

        if (!string.IsNullOrEmpty(customFolderPath))
        {
            folderPath = customFolderPath;

            // 지정한 폴더가 없으면 자동으로 생성
            if (!Directory.Exists(folderPath))
            {
                try
                {
                    Directory.CreateDirectory(folderPath);
                    Debug.Log($"[FaceScoreDBLogger] 폴더가 없어 새로 생성했습니다: {folderPath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FaceScoreDBLogger] 폴더 생성 실패: {e.Message}. 기본 경로로 대체합니다.");
                    folderPath = Application.persistentDataPath;
                }
            }
        }
        else
        {
            folderPath = Application.persistentDataPath;
        }

        string dbPath = Path.Combine(folderPath, dbFileName);
        Debug.Log($"[FaceScoreDBLogger] DB 경로: {dbPath}");

        db = new SQLiteConnection(dbPath);
        db.CreateTable<FaceScoreEntry>(); // 테이블 없으면 자동 생성
    }

    /// <summary>
    /// [수정] 감정별(기쁨/놀람/분노) 누적 합계·평균 값도 함께 저장하도록 확장
    /// </summary>
    public void SaveScoreToDB(
        float smile, float surprise, float angry,
        float smileTotal, float smileAverage,
        float surpriseTotal, float surpriseAverage,
        float angryTotal, float angryAverage)
    {
        var entry = new FaceScoreEntry
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            SmileScore = smile,
            SurpriseScore = surprise,
            AngryScore = angry,
            SmileTotal = smileTotal,
            SmileAverage = smileAverage,
            SurpriseTotal = surpriseTotal,
            SurpriseAverage = surpriseAverage,
            AngryTotal = angryTotal,
            AngryAverage = angryAverage
        };

        db.Insert(entry); // sqlite-net 내부적으로 thread-safe하게 처리됨
    }

    void OnApplicationQuit()
    {
        db?.Close();
    }
}
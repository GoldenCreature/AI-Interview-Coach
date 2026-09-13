using SQLite;
using UnityEngine;
using System;
using System.IO;

public class FaceScoreDBLogger : MonoBehaviour
{
    private SQLiteConnection db;
    private readonly object dbLock = new object();
    private bool isInitialized = false;

    // 메인 스레드에서 캐싱할 기본 persistentDataPath
    private static string defaultPersistentPath = string.Empty;
    private string cachedDbPath = string.Empty;

    [Header("저장 경로 설정")]
    [Tooltip("DB 파일을 저장할 폴더의 전체 경로. 비워두면 기본 persistentDataPath를 사용합니다. 예: D:\\FaceScoreData")]
    public string customFolderPath = "";

    [Tooltip("DB 파일명")]
    public string dbFileName = "FaceScores.db";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CacheMainThreadData()
    {
        // 씬이 로드되기 전 메인 스레드에서 persistentDataPath를 static 변수에 안전하게 미리 캐싱
        defaultPersistentPath = Application.persistentDataPath;
    }

    void Awake()
    {
        SetupPathAndInitialize();
    }

    private void SetupPathAndInitialize()
    {
        lock (dbLock)
        {
            if (isInitialized) return;

            string folderPath = string.Empty;

            // 1. 커스텀 경로가 지정된 경우
            if (!string.IsNullOrEmpty(customFolderPath))
            {
                folderPath = customFolderPath;

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
                        folderPath = defaultPersistentPath;
                    }
                }
            }
            else
            {
                // 2. 커스텀 경로가 없으면 캐싱된 persistentDataPath 사용
                folderPath = defaultPersistentPath;
            }

            // 여전히 경로를 구하지 못했으면(메인 스레드 첫 프레임 진입 전 백그라운드 호출 시)
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            cachedDbPath = Path.Combine(folderPath, dbFileName);

            try
            {
                db = new SQLiteConnection(cachedDbPath);
                db.CreateTable<FaceScoreEntry>();
                isInitialized = true;
                Debug.Log($"[FaceScoreDBLogger] DB 연결 및 테이블 생성 완료: {cachedDbPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FaceScoreDBLogger] DB 연결 초기화 실패: {e.Message}");
            }
        }
    }

    /// <summary>
    /// MediaPipe 콜백 스레드에서 호출되는 저장 함수
    /// </summary>
    public void SaveScoreToDB(DateTime timestamp, float evaluationScore, string evaluationDetail, string improvementNotes)
    {
        // 초기화가 안 되어 있다면 초기화 시도
        if (!isInitialized || db == null)
        {
            SetupPathAndInitialize();
        }

        // 경로 준비가 안 되었거나 DB 커넥션 생성 실패 시 경고 후 스킵
        if (db == null)
        {
            Debug.LogWarning("[FaceScoreDBLogger] DB 커넥션이 아직 준비되지 않아 저장을 스킵합니다.");
            return;
        }

        var entry = new FaceScoreEntry
        {
            Timestamp = timestamp.ToString("yyyy-MM-dd HH:mm"),
            EvaluationScore = evaluationScore,
            EvaluationDetail = evaluationDetail ?? "",
            ImprovementNotes = improvementNotes ?? ""
        };

        lock (dbLock)
        {
            try
            {
                db.Insert(entry);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FaceScoreDBLogger] DB Insert 실패: {e.Message}");
            }
        }
    }

    void OnApplicationQuit()
    {
        lock (dbLock)
        {
            if (db != null)
            {
                db.Close();
                db = null;
                isInitialized = false;
            }
        }
    }
}
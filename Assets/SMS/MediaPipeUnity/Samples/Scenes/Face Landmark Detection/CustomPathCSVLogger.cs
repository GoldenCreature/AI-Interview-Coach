using System;
using System.IO;
using System.Text;
using UnityEngine;

public class CustomPathCSVLogger : MonoBehaviour
{
    [Header("저장 경로 설정 (Path Settings)")]
    [Tooltip("체크하면 유니티 기본 안전 경로(persistentDataPath)를 사용합니다.")]
    public bool useDefaultPath = false;
    [Tooltip("원하는 커스텀 폴더 경로를 입력하세요.\n(예: C:/MyFaceData 또는 D:/FaceLogs)")]
    public string customFolderPath = "C:/FaceData";
    [Tooltip("저장할 CSV 파일의 이름입니다.")]
    public string fileName = "FaceScores.csv";

    private string finalFilePath;

    // 파일을 매번 열고 닫지 않고, Awake에서 한 번 열어 계속 재사용합니다.
    // MediaPipe 콜백 스레드에서 동시에 호출될 수 있으므로 lock으로 보호합니다.
    private StreamWriter persistentWriter;
    private readonly object writeLock = new object();

    void Awake()
    {
        string directoryPath = "";

        // 1. 유니티 기본 경로를 쓸지, 커스텀 경로를 쓸지 결정
        if (useDefaultPath)
        {
            directoryPath = Application.persistentDataPath;
        }
        else
        {
            directoryPath = customFolderPath;
        }

        // 혹시나 설정한 경로가 완전히 비어있다면 에러 방지를 위해 기본 경로로 대체
        if (string.IsNullOrEmpty(directoryPath))
        {
            directoryPath = Application.persistentDataPath;
            Debug.LogWarning("[CSV Logger] 커스텀 경로가 비어있어 기본 경로로 대체합니다.");
        }

        // 2. 입력한 경로의 폴더가 실제로 존재하지 않으면 자동으로 폴더 생성
        if (!Directory.Exists(directoryPath))
        {
            try
            {
                Directory.CreateDirectory(directoryPath);
                Debug.Log($"[CSV Logger] 새로운 폴더를 생성했습니다: {directoryPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CSV Logger] 폴더 생성 실패 (권한 문제 등): {e.Message}. 기본 안전 경로로 강제 전환합니다.");
                directoryPath = Application.persistentDataPath;
            }
        }

        // 3. 최종 파일 경로 완성 (폴더 경로 + 파일 이름)
        finalFilePath = Path.Combine(directoryPath, fileName);
        Debug.Log($"[CSV Logger] 최종 CSV 파일 저장 경로: {finalFilePath}");

        bool isNewFile = !File.Exists(finalFilePath);

        // 4. 파일을 한 번만 열어서 세션 내내 재사용 (AutoFlush로 매 줄 즉시 디스크 반영)
        try
        {
            // UTF-8 BOM을 명시적으로 포함시켜 Excel이 인코딩을 올바르게 인식하도록 함
            // (BOM이 없으면 한국어 Windows의 Excel이 CP949로 잘못 추측해 한글이 깨짐)
            persistentWriter = new StreamWriter(finalFilePath, append: true, new UTF8Encoding(true)) { AutoFlush = true };

            // 새 파일일 때만 헤더(Header) 작성
            // [수정] 감정별 개별 점수(Smile/Surprise/Angry)와 평가등급(EvaluationGrade)은 저장하지 않고,
            // Timestamp + EvaluationScore + EvaluationDetail만 저장
            if (isNewFile)
            {
                persistentWriter.WriteLine("Timestamp,EvaluationScore,EvaluationDetail,ImprovementNotes");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CSV Logger] 파일 오픈 실패: {e.Message}");
            persistentWriter = null;
        }
    }

    /// <summary>
    /// 외부에서 시간 + 종합 평가 점수/코멘트를 전달받아 CSV에 한 줄씩 기록하는 함수
    /// MediaPipe 콜백 스레드에서 호출될 수 있으므로 lock으로 동시 접근을 막습니다.
    /// </summary>
    public void SaveScoreToCSV(DateTime timestamp, float evaluationScore, string evaluationDetail, string improvementNotes)
    {
        if (string.IsNullOrEmpty(finalFilePath)) return;

        string timestampStr = timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");

        // CSV 규격상 콤마/줄바꿈/큰따옴표가 포함된 텍스트는 큰따옴표로 감싸고
        // 내부 큰따옴표는 두 개("")로 이스케이프해야 함. 코멘트/개선사항은 여러 줄일 수 있어 특히 중요.
        string safeDetail = EscapeCsvField(evaluationDetail);
        string safeImprovementNotes = EscapeCsvField(improvementNotes);

        string csvLine = $"{timestampStr},{evaluationScore:F1},{safeDetail},{safeImprovementNotes}";

        lock (writeLock)
        {
            if (persistentWriter == null)
            {
                Debug.LogWarning("[CSV Logger] writer가 준비되지 않아 저장을 건너뜁니다.");
                return;
            }

            try
            {
                persistentWriter.WriteLine(csvLine);
                Debug.Log($"[CSV Logger] 점수 등록 완료 -> {csvLine}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CSV Logger] 파일 쓰기 실패: {e.Message}");
            }
        }
    }

    // CSV 필드 이스케이프 처리 (콤마/줄바꿈/큰따옴표 포함 시 큰따옴표로 감싸기)
    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";

        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }

    private void OnDestroy()
    {
        CloseWriter();
    }

    private void OnApplicationQuit()
    {
        CloseWriter();
    }

    private void CloseWriter()
    {
        lock (writeLock)
        {
            if (persistentWriter != null)
            {
                try
                {
                    persistentWriter.Flush();
                    persistentWriter.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CSV Logger] writer 종료 중 예외: {e.Message}");
                }
                persistentWriter = null;
            }
        }
    }
}
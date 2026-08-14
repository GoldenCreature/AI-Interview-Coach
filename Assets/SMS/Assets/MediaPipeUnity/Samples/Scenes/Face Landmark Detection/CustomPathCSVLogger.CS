using System;
using System.IO;
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
        // 4. 파일이 없을 때만 헤더(Header) 작성
        // [수정] 감정별(기쁨/놀람/분노) 누적 합계·평균 컬럼 추가
        // [추가] 면접용 종합 평가 컬럼(등급/요약/상세) 추가
        if (!File.Exists(finalFilePath))
        {
            try
            {
                using (StreamWriter sw = File.CreateText(finalFilePath))
                {
                    sw.WriteLine("Timestamp,SmileScore,SurpriseScore,AngryScore," +
                                 "SmileTotal,SmileAverage,SurpriseTotal,SurpriseAverage,AngryTotal,AngryAverage," +
                                 "EvaluationGrade,EvaluationSummary,EvaluationDetail,EvaluationScore");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CSV Logger] 초기 파일 생성 실패: {e.Message}");
            }
        }
    }
    /// <summary>
    /// 외부에서 점수를 전달받아 CSV에 실시간으로 한 줄씩 기록하는 함수
    /// [수정] 감정별 누적 합계/평균 값도 함께 저장
    /// [추가] 면접용 종합 평가(등급/요약/상세)도 함께 저장
    /// </summary>
    public void SaveScoreToCSV(
        float smile, float surprise, float angry,
        float smileTotal, float smileAverage,
        float surpriseTotal, float surpriseAverage,
        float angryTotal, float angryAverage,
        string evaluationGrade, string evaluationSummary, string evaluationDetail,
        float evaluationScore)
    {
        if (string.IsNullOrEmpty(finalFilePath)) return;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        // CSV 규격상 콤마/줄바꿈/큰따옴표가 포함된 텍스트는 큰따옴표로 감싸고
        // 내부 큰따옴표는 두 개("")로 이스케이프해야 함. evaluationDetail은 여러 줄 코멘트라 특히 중요.
        string safeSummary = EscapeCsvField(evaluationSummary);
        string safeDetail = EscapeCsvField(evaluationDetail);

        string csvLine = $"{timestamp},{smile:F1},{surprise:F1},{angry:F1}," +
                          $"{smileTotal:F1},{smileAverage:F2}," +
                          $"{surpriseTotal:F1},{surpriseAverage:F2}," +
                          $"{angryTotal:F1},{angryAverage:F2}," +
                          $"{evaluationGrade},{safeSummary},{safeDetail},{evaluationScore:F1}";
        try
        {
            using (StreamWriter sw = File.AppendText(finalFilePath))
            {
                sw.WriteLine(csvLine);
            }
            Debug.Log($"[CSV Logger] 점수 등록 완료 -> {csvLine}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CSV Logger] 파일 쓰기 실패: {e.Message}");
        }
    }

    // [추가] CSV 필드 이스케이프 처리 (콤마/줄바꿈/큰따옴표 포함 시 큰따옴표로 감싸기)
    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";

        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }
}
using System.IO;
using System.Text;
using UnityEngine;

public class CustomPathCSVLogger : MonoBehaviour
{
    [Header("저장 경로 설정")]
    [Tooltip("C:\\Users\\User\\Desktop\\as\\ploy")]
    [SerializeField] private string customFolderPath = "C:/MyFaceData";

    [Tooltip("저장할 CSV 파일의 이름")]
    [SerializeField] private string fileName = "fess.csv";

    private string finalFilePath;

    void Awake()
    {
        // 1. 경로 자동 보정 및 결합
        ConfigurePath();

        // 2. 파일 및 폴더 준비
        InitCSVFile();
    }

    void Update()
    {
        // [테스트 꿀팁] 게임 실행 중 'O' 키를 누르면 파일이 저장된 폴더가 화면에 바로 열립니다.
        if (Input.GetKeyDown(KeyCode.O))
        {
            OpenFolderInExplorer();
        }
    }

    private void ConfigurePath()
    {
        // 슬래시(/)와 역슬래시(\) 혼용으로 인한 경로 오류 방지
        string sanitizedFolder = customFolderPath.Replace("\\", "/");

        // 입력한 경로가 비어있다면 에러 방지를 위해 기본 저장소(Application.persistentDataPath)로 대체
        if (string.IsNullOrEmpty(sanitizedFolder))
        {
            sanitizedFolder = Application.persistentDataPath;
            Debug.LogWarning("[CSV Logger] 입력된 경로가 없어 기본 안전 경로로 대체합니다.");
        }

        // 폴더 경로와 파일명을 결합하여 최종 절대 경로 완성
        finalFilePath = Path.Combine(sanitizedFolder, fileName);
    }

    private void InitCSVFile()
    {
        try
        {
            // 1. 원하는 폴더가 실제로 존재하지 않으면 자동으로 생성합니다.
            string directory = Path.GetDirectoryName(finalFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Debug.Log($"[CSV Logger] 존재하지 않는 폴더를 새로 생성했습니다: {directory}");
            }

            // 2. 파일이 없을 때만 최상단 열(헤더)을 작성합니다.
            if (!File.Exists(finalFilePath))
            {
                using (StreamWriter sw = new StreamWriter(finalFilePath, false, new UTF8Encoding(true)))
                {
                    sw.WriteLine("Timestamp,SmileScore,SurpriseScore,AngryScore");
                }
                Debug.Log($"[CSV Logger] 새로운 CSV 파일을 생성했습니다: {finalFilePath}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CSV Logger] 초기화 중 오류 발생: {e.Message}");
        }
    }

    /// <summary>
    /// 계산된 표정 점수를 설정한 커스텀 경로의 CSV 파일에 누적 기록합니다.
    /// </summary>
    public void SaveScoreToCSV(float smile, float surprise, float angry)
    {
        try
        {
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logLine = $"{timestamp},{smile:F1},{surprise:F1},{angry:F1}";

            // true 매개변수는 기존 파일 내용 뒤에 덮어쓰지 않고 추가(Append)하겠다는 의미입니다.
            using (StreamWriter sw = new StreamWriter(finalFilePath, true, new UTF8Encoding(true)))
            {
                sw.WriteLine(logLine);
            }

            Debug.Log($"[CSV Logger] 지정 경로에 데이터 기록 완료: {logLine}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CSV Logger] 데이터 쓰기 실패 (경로를 확인하세요): {e.Message}");
        }
    }

    /// <summary>
    /// 저장된 폴더를 탐색기(Windows) 또는 파인더(Mac)로 즉시 엽니다.
    /// </summary>
    public void OpenFolderInExplorer()
    {
        string folderPath = Path.GetDirectoryName(finalFilePath);
        if (Directory.Exists(folderPath))
        {
            // OS별 탐색기 실행 명령어 처리
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            System.Diagnostics.Process.Start("explorer.exe", folderPath.Replace("/", "\\"));
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            System.Diagnostics.Process.Start("open", folderPath);
#else
            Debug.Log($"현재 플랫폼에서는 폴더 열기를 지원하지 않습니다: {folderPath}");
#endif
        }
        else
        {
            Debug.LogError("폴더가 존재하지 않아 열 수 없습니다.");
        }
    }
}
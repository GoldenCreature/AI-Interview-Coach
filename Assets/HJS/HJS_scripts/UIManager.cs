using UnityEngine;
using TMPro;

// 팀원이 작성한 네임스페이스 가져오기
using PlayUI.Scripts;         // Play.cs (타이머)
using WebCamOptionUI.Scripts;       // WebCam.cs (웹캠)
using MicroPhoneUI.Scripts;   // microphoneSetting.cs (마이크)
using OptionUI.Scripts;       // VideoOption.cs (비디오 옵션)

namespace HJS
{
    public class UIManager : SingletonBase<UIManager>
    {
        [Header("--- UI 팝업 및 패널 ---")]
        [SerializeField] private GameObject settingsPopup;          // 설정 팝업 패널 (Option, Mic, Cam 통합 UI)

        [Header("--- 기능별 전용 스크립트 연결 ---")]
        [SerializeField] private Play timerController;               // Play.cs 연결
        [SerializeField] private WebCamOption webCamController;            // WebCam.cs 연결
        [SerializeField] private MicrophoneSetting micController;    // microphoneSetting.cs 연결
        [SerializeField] private VideoOption videoOption;            // VideoOption.cs 연결

        [Header("--- UI 텍스트 ---")]
        [SerializeField] private TextMeshProUGUI evaluationResultText; // 결과 화면 평가지 텍스트
        [SerializeField] private TextMeshProUGUI durationResultText;   // 결과 화면 총 진행 시간 텍스트

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("[UIManager] 초기화 완료");

            // 시작 시 설정 팝업 비활성화 (방어 코드)
            if (settingsPopup != null)
            {
                settingsPopup.SetActive(false);
            }
        }

        private void OnEnable()
        {
            // 면접 종료 이벤트 구독 (옵저버 패턴)
            InterviewManager.OnInterviewEnded += HandleInterviewEnded;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            InterviewManager.OnInterviewEnded -= HandleInterviewEnded;
        }

        // -----------------------------------------------
        // 이벤트 핸들러 (면접 종료시 자동 호출)
        // -----------------------------------------------

        private void HandleInterviewEnded(InterviewResultData resultData)
        {
            Debug.Log("[UIManager] 면접 종료 감지 → 타이머 정지 및 결과 저장");

            // 1. 타이머 정지 및 진행 시간 저장
            if (timerController != null)
            {
                timerController.PauseTimer();
                float totalTime = timerController.elapsedTime;
                PlayerPrefs.SetFloat("InterviewDuration", totalTime);
            }

            // 2. AI 평가 결과 데이터 저장 (ResultData 존재 시)
            if (resultData != null)
            {
                PlayerPrefs.SetString("LastEvaluation", resultData.ToString());
            }

            PlayerPrefs.Save();

            // 3. 결과 화면 씬 로드
            GameManager.Instance.LoadResultScene();
        }

        // -----------------------------------------------
        // UI 버튼 클릭 이벤트 함수
        // -----------------------------------------------

        // [면접 시작] 버튼 (Main 씬 또는 Interviewer 씬)
        public void OnClickInterviewStart()
        {
            Debug.Log("[UIManager] 면접 시작 버튼 클릭");
            GameManager.Instance.LoadInterviewSetupScene();
        }

        // [피드백 불러오기] 버튼 (FeedBack 씬 이동)
        public void OnClickFeedbackHistory()
        {
            Debug.Log("[UIManager] 피드백 불러오기 버튼 클릭");
            UnityEngine.SceneManagement.SceneManager.LoadScene("FeedBack");
        }

        // [설정] 버튼 (팝업 열기 + 웹캠/마이크 테스트 준비 + 타이머 정지)
        public void OnClickSettings()
        {
            Debug.Log("[UIManager] 설정 버튼 클릭 (팝업 열기)");

            if (settingsPopup != null)
            {
                settingsPopup.SetActive(true);

                // 면접 도중 설정창을 열었다면 타이머 정지
                if (timerController != null)
                {
                    timerController.PauseTimer();
                }

                // 웹캠 테스트 자동 시작 (필요 시)
                if (webCamController != null)
                {
                    webCamController.StartCamTest();
                }
            }
        }

        // [설정 닫기] 버튼 (팝업 닫기 + 웹캠 테스트 종료 + 타이머 재개)
        public void OnClickCloseSettings()
        {
            Debug.Log("[UIManager] 설정 닫기 버튼 클릭");

            if (settingsPopup != null)
            {
                // 웹캠 작동 중이면 중단
                if (webCamController != null)
                {
                    webCamController.StopCamTest();
                }

                settingsPopup.SetActive(false);

                // 타이머 재개
                if (timerController != null)
                {
                    timerController.ResumeTimer();
                }
            }
        }

        // [앱 종료] 버튼
        public void OnClickQuit()
        {
            Debug.Log("[UIManager] 종료 버튼 클릭");
            GameManager.Instance.QuitApplication();
        }

        // [면접 강제 종료] 버튼 (Play 씬 내부 UI)
        public void OnClickEndInterview()
        {
            Debug.Log("[UIManager] 면접 강제 종료 버튼 클릭");
            InterviewManager.Instance.EndInterview();
        }

        // -----------------------------------------------
        // 결과 화면 데이터 표시 함수
        // -----------------------------------------------

        public void ShowResult(string evaluationResult)
        {
            Debug.Log("[UIManager] 결과 화면 표시");

            // 1. AI 평가 내용 텍스트 출력
            if (evaluationResultText != null)
            {
                evaluationResultText.text = evaluationResult;
            }

            // 2. 저장된 면접 시간 출력 (예: 05:12)
            if (durationResultText != null)
            {
                float savedDuration = PlayerPrefs.GetFloat("InterviewDuration", 0f);
                int minutes = Mathf.FloorToInt(savedDuration / 60);
                int seconds = Mathf.FloorToInt(savedDuration % 60);
                durationResultText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }
        }
    }
}
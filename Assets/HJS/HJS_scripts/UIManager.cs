using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJS
{
    public class UIManager : SingletonBase<UIManager>
    {
        protected override void Awake()
        {
            base.Awake();
            Debug.Log("[UIManager] 초기화 완료");
        }

        private void OnEnable()
        {
            // 면접 종료 이벤트 구독 (옵저버 패턴)
            // 면접 종료 시 결과 화면으로 자동 전환
            InterviewManager.OnInterviewEnded += HandleInterviewEnded;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            InterviewManager.OnInterviewEnded -= HandleInterviewEnded;
        }

        // -----------------------------------------------
        // 이벤트 핸들러 (면접 종료 시 자동 호출)
        // 씬 전환만 담당
        // 타이머 정지 등 UI 처리는 Play.cs가 담당
        // -----------------------------------------------
        private void HandleInterviewEnded(InterviewResultData resultData)
        {
            Debug.Log("[UIManager] 면접 종료 감지 → 결과 화면 전환");
            GameManager.Instance.LoadResultScene();
        }

        // -----------------------------------------------
        // 화면 전환 함수들
        // UI 버튼 OnClick에 직접 연결 가능
        // -----------------------------------------------

        // [면접 시작] 버튼
        public void OnClickInterviewStart()
        {
            Debug.Log("[UIManager] 면접 시작 버튼 클릭");
            GameManager.Instance.LoadInterviewSetupScene();
        }

        // [피드백 불러오기] 버튼
        public void OnClickFeedbackHistory()
        {
            Debug.Log("[UIManager] 피드백 불러오기 버튼 클릭");
            GameManager.Instance.LoadFeedbackScene();
        }

        // [설정] 버튼 (타이틀에서 설정 씬으로 이동)
        public void OnClickSettings()
        {
            Debug.Log("[UIManager] 설정 버튼 클릭");
            GameManager.Instance.LoadSettingScene();
        }

        // [종료] 버튼
        public void OnClickQuit()
        {
            Debug.Log("[UIManager] 종료 버튼 클릭");
            GameManager.Instance.QuitApplication();
        }

        // [면접 강제 종료] 버튼
        public void OnClickEndInterview()
        {
            Debug.Log("[UIManager] 면접 강제 종료 버튼 클릭");
            InterviewManager.Instance.EndInterview();
        }

        // -----------------------------------------------
        // 결과 화면 표시 함수
        // Gemini 평가 결과를 Result.cs로 전달
        // -----------------------------------------------
        public void ShowResult(string evaluationResult)
        {
            Debug.Log("[UIManager] 결과 데이터 전달");
            // TODO: Result.cs 작성 후 연결
        }
    }
}
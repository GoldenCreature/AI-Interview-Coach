using UnityEngine;

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
            // 면접 종료 이벤트 구독
            // 면접이 끝나면 자동으로 결과 화면으로 전환
            InterviewManager.OnInterviewEnded += HandleInterviewEnded;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            // 오브젝트가 꺼질 때 반드시 해제해야 메모리 누수 방지
            InterviewManager.OnInterviewEnded -= HandleInterviewEnded;
        }

        // -----------------------------------------------
        // 이벤트 핸들러
        // -----------------------------------------------

        // 면접 종료 이벤트 수신 시 호출
        private void HandleInterviewEnded(InterviewResultData resultData)
        {
            Debug.Log("[UIManager] 면접 종료 감지 → 결과 화면 전환");
            // TODO: 한효준 팀원이 결과 화면 연결
            GameManager.Instance.LoadResultScene();
        }

        // -----------------------------------------------
        // 화면 전환 함수들
        // 한효준 팀원이 UI 버튼에 연결할 함수들
        // -----------------------------------------------

        // 면접 시작 버튼 → 면접관 유형 설정 화면으로 이동
        public void OnClickInterviewStart()
        {
            Debug.Log("[UIManager] 면접 시작 버튼 클릭");
            GameManager.Instance.LoadInterviewSetupScene();
        }

        // 피드백 불러오기 버튼 → 과거 기록 화면으로 이동
        public void OnClickFeedbackHistory()
        {
            Debug.Log("[UIManager] 피드백 불러오기 버튼 클릭");
            // TODO: 한효준 팀원이 피드백 기록 화면 연결
        }

        // 설정 버튼
        public void OnClickSettings()
        {
            Debug.Log("[UIManager] 설정 버튼 클릭");
            // TODO: 한효준 팀원이 설정 화면 연결
        }

        // 종료 버튼
        public void OnClickQuit()
        {
            Debug.Log("[UIManager] 종료 버튼 클릭");
            GameManager.Instance.QuitApplication();
        }

        // 면접 종료 버튼 (면접 화면에서 호출)
        public void OnClickEndInterview()
        {
            Debug.Log("[UIManager] 면접 종료 버튼 클릭");
            InterviewManager.Instance.EndInterview();
        }

        // -----------------------------------------------
        // 결과 화면 표시 함수
        // 한효준 팀원이 UI 연결 후 채워넣을 부분
        // -----------------------------------------------

        // 결과 화면 데이터 표시
        // GeminiManager에서 평가 결과 받아서 호출
        public void ShowResult(string evaluationResult)
        {
            Debug.Log("[UIManager] 결과 화면 표시");
            // TODO: 한효준 팀원이 UI 텍스트에 연결
        }
    }
}
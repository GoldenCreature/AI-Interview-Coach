using UnityEngine;
using UnityEngine.SceneManagement;

namespace HJS
{
    // 앱 전체 상태를 나타내는 열거형
    public enum AppState
    {
        Title,          // 타이틀 화면
        Loading,        // 로딩 화면
        InterviewSetup, // 면접관 유형 설정 화면
        Interview,      // 면접 진행 화면
        Result          // 결과/피드백 화면
    }

    public class GameManager : SingletonBase<GameManager>
    {
        // 현재 앱 상태
        public AppState CurrentState { get; private set; }

        // 씬 이름 상수
        // 나중에 씬 이름 바뀌면 여기만 수정하면 됨
        private const string SCENE_TITLE = "TitleScene";
        private const string SCENE_LOADING = "LoadingScene";
        private const string SCENE_INTERVIEW_SETUP = "InterviewSetupScene";
        private const string SCENE_INTERVIEW = "Interview Room";
        private const string SCENE_RESULT = "ResultScene";

        protected override void Awake()
        {
            base.Awake();
            CurrentState = AppState.Title;
            Debug.Log("[GameManager] 초기화 완료");
        }

        // -----------------------------------------------
        // 씬 전환 함수들
        // UI 버튼 OnClick에 직접 연결 가능
        // -----------------------------------------------

        // 타이틀 화면으로 이동
        public void LoadTitleScene()
        {
            ChangeState(AppState.Title);
            SceneManager.LoadScene(SCENE_TITLE);
        }

        // 면접관 유형 설정 화면으로 이동
        // 타이틀에서 면접 시작 버튼 누르면 호출
        public void LoadInterviewSetupScene()
        {
            ChangeState(AppState.InterviewSetup);
            SceneManager.LoadScene(SCENE_INTERVIEW_SETUP);
        }

        // 면접 화면으로 이동
        // 유형 설정 완료 후 호출
        public void LoadInterviewScene()
        {
            ChangeState(AppState.Interview);
            SceneManager.LoadScene(SCENE_INTERVIEW);
        }

        // 결과 화면으로 이동
        // 면접 종료 시 InterviewManager가 호출
        public void LoadResultScene()
        {
            ChangeState(AppState.Result);
            SceneManager.LoadScene(SCENE_RESULT);
        }

        // 앱 종료
        // 타이틀 화면 종료 버튼에 연결
        public void QuitApplication()
        {
            Debug.Log("[GameManager] 앱 종료");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // -----------------------------------------------
        // 상태 변경 내부 함수
        // -----------------------------------------------
        private void ChangeState(AppState newState)
        {
            Debug.Log($"[GameManager] 상태 변경: {CurrentState} → {newState}");
            CurrentState = newState;
        }
    }
}
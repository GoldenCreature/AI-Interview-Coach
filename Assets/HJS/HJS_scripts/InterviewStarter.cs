using UnityEngine;

namespace HJS
{
    // -----------------------------------------------
    // 임시 면접 시작 스크립트
    // UI 버튼 연결 전까지 씬 시작 시 자동으로 면접 시작
    // UI 연결 완료 후 이 스크립트는 삭제 예정
    // -----------------------------------------------
    public class InterviewStarter : MonoBehaviour
    {
        [Header("임시 직종 선택 (UI 연결 전까지 사용)")]
        public JobCategory selectedJob = JobCategory.IT개발자;
        [Header("임시 면접관 유형 선택 (UI 연결 전까지 사용)")]
        public InterviewerType selectedType = InterviewerType.Intensive;

        private void Start()
        {
            // 직종 설정
            InterviewManager.Instance.SetJob(selectedJob);

            // 면접 시작
            InterviewManager.Instance.StartInterview();

            Debug.Log($"[InterviewStarter] 임시 면접 시작: {selectedJob}");
        }
    }
}
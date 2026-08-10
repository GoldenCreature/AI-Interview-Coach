using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HJS;

namespace InterViewUI.Scripts
{
    public class Interviewer : MonoBehaviour
    {
        [Header("직종 선택 드롭다운")]
        [SerializeField] private TMP_Dropdown jobDropdown;

        [Header("면접관 유형 토글")]
        // Inspector에서 ToggleGroup 안의 각 Toggle 연결
        [SerializeField] private Toggle intensiveToggle;  // 직무 기반 심화 면접
        [SerializeField] private Toggle casualToggle;     // 일상적 대화 면접

        private void Start()
        {
            // 드롭다운 초기화
            // JobCategory enum에서 자동으로 옵션 가져오기
            // 나중에 직종 추가 시 enum만 수정하면 자동 반영됨
            if (jobDropdown != null)
            {
                jobDropdown.ClearOptions();
                jobDropdown.AddOptions(
                    new System.Collections.Generic.List<string>(
                        System.Enum.GetNames(typeof(JobCategory))
                    )
                );
                jobDropdown.onValueChanged.AddListener(OnJobChanged);
            }

            // 토글 리스너 연결
            if (intensiveToggle != null)
                intensiveToggle.onValueChanged.AddListener((isOn) =>
                {
                    if (isOn) OnTypeSelected(InterviewerType.Intensive);
                });

            if (casualToggle != null)
                casualToggle.onValueChanged.AddListener((isOn) =>
                {
                    if (isOn) OnTypeSelected(InterviewerType.Casual);
                });

            // 기본값 설정
            InterviewManager.Instance.SetJob(JobCategory.IT개발자);
            InterviewManager.Instance.SetInterviewerType(InterviewerType.Intensive);
        }

        // 직종 드롭다운 변경 시 호출
        // index가 JobCategory enum 순서와 일치하므로 바로 캐스팅 가능
        private void OnJobChanged(int index)
        {
            JobCategory selectedJob = (JobCategory)index;
            InterviewManager.Instance.SetJob(selectedJob);
            Debug.Log($"[Interviewer] 직종 선택: {selectedJob}");
        }

        // 면접관 유형 토글 선택 시 호출
        private void OnTypeSelected(InterviewerType type)
        {
            InterviewManager.Instance.SetInterviewerType(type);
            Debug.Log($"[Interviewer] 면접관 유형 선택: {type}");
        }

        // [면접 시작] 버튼 → 로딩 씬으로 이동
        public void PlayBtn()
        {
            // API 키 유효성 검사
            // 비어있으면 경고 후 씬 전환 막기
            if (!SettingsManager.Instance.IsApiKeysValid())
            {
                Debug.LogWarning("[Interviewer] API 키가 설정되지 않았습니다. 설정 화면에서 입력해주세요.");
                // TODO: 경고 팝업 UI 연결 요망
                return;
            }

            GameManager.Instance.LoadLoadingScene();
        }

        // [메인 화면] 버튼 → 타이틀로 이동
        public void MainBtn()
        {
            GameManager.Instance.LoadTitleScene();
        }
    }
}
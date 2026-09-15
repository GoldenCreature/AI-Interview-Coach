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

        [Header("경고 팝업 UI")]
        [SerializeField] private GameObject apiWarningPopup;   // 경고 팝업 패널 오브젝트

        [Header("--- [면접 요약 UI 연결] ---")]
        [SerializeField] private TextMeshProUGUI jobSummaryText;    // 우측 직무 텍스트 (예: IT 개발자)
        [SerializeField] private TextMeshProUGUI typeSummaryText;   // 우측 유형 텍스트 (예: 일상적 대화 면접)
        [SerializeField] private TextMeshProUGUI micKeySummaryText; // 우측 마이크 키 텍스트 (예: Space)

        private void Start()
        {
            // 1. 드롭다운 초기화 (JobCategory enum에서 가져옴)
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

            // 2. 토글 리스너 연결
            if (intensiveToggle != null)
            {
                intensiveToggle.onValueChanged.AddListener((isOn) =>
                {
                    if (isOn) OnTypeSelected(InterviewerType.Intensive);
                });
            }

            if (casualToggle != null)
            {
                casualToggle.onValueChanged.AddListener((isOn) =>
                {
                    if (isOn) OnTypeSelected(InterviewerType.Casual);
                });
            }

            // 3. 기본 선택값 및 토글 상태를 'Casual(일상적 대화 면접)'로 설정
            if (casualToggle != null)
            {
                casualToggle.isOn = true;
            }

            InterviewManager.Instance.SetJob(JobCategory.IT개발자);
            InterviewManager.Instance.SetInterviewerType(InterviewerType.Casual);

            // 4. UI 초기 요약 텍스트 전체 갱신 (직무, 유형, 마이크 키)
            UpdateSummaryUI(JobCategory.IT개발자, InterviewerType.Casual);
        }

        // 직종 드롭다운 변경 시 호출
        private void OnJobChanged(int index)
        {
            JobCategory selectedJob = (JobCategory)index;
            InterviewManager.Instance.SetJob(selectedJob);

            if (jobSummaryText != null)
            {
                jobSummaryText.text = selectedJob.ToString();
            }

            Debug.Log($"[Interviewer] 직종 선택: {selectedJob}");
        }

        // 면접관 유형 토글 선택 시 호출
        private void OnTypeSelected(InterviewerType type)
        {
            InterviewManager.Instance.SetInterviewerType(type);

            if (typeSummaryText != null)
            {
                typeSummaryText.text = GetTypeDisplayName(type);
            }

            Debug.Log($"[Interviewer] 면접관 유형 선택: {type}");
        }

        /// <summary>
        /// 면접 요약 패널의 텍스트(직무, 유형, 마이크 키)를 실시간 업데이트
        /// </summary>
        private void UpdateSummaryUI(JobCategory job, InterviewerType type)
        {
            if (jobSummaryText != null)
            {
                jobSummaryText.text = job.ToString();
            }

            if (typeSummaryText != null)
            {
                typeSummaryText.text = GetTypeDisplayName(type);
            }

            // SettingsManager의 MicKey 값을 가져와 반영
            if (micKeySummaryText != null)
            {
                string keyName = "Space"; // 기본 fallback 값

                if (SettingsManager.Instance != null)
                {
                    keyName = SettingsManager.Instance.MicKey.ToString();
                }

                micKeySummaryText.text = keyName;
            }
        }

        /// <summary>
        /// InterviewerType Enum을 한국어 표시 명칭으로 변환
        /// </summary>
        private string GetTypeDisplayName(InterviewerType type)
        {
            return type switch
            {
                InterviewerType.Intensive => "직무 기반 심화 면접",
                InterviewerType.Casual => "일상적 대화 면접",
                _ => type.ToString()
            };
        }

        // [면접 시작] 버튼 → 로딩 씬으로 이동
        public void PlayBtn()
        {
            // API 키 유효성 검사
            if (!SettingsManager.Instance.IsApiKeysValid())
            {
                Debug.LogWarning("[Interviewer] API 키가 설정되지 않았습니다. 설정 화면에서 입력해주세요.");

                if (apiWarningPopup != null)
                {
                    apiWarningPopup.SetActive(true);
                }
                return;
            }

            GameManager.Instance.LoadLoadingScene();
        }

        // [메인 화면] 버튼 → 타이틀로 이동
        public void MainBtn()
        {
            GameManager.Instance.LoadTitleScene();
        }

        public void CloseWarningPopup()
        {
            if (apiWarningPopup != null)
            {
                apiWarningPopup.SetActive(false);
            }
        }
    }
}
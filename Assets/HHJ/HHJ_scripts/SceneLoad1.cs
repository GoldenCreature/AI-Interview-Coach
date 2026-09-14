using HJS;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LoadingUI.Scripts
{
    public class SceneLoad1 : MonoBehaviour
    {
        [Header("로딩 슬라이더")]
        public Slider progressbar;
        public float loadSpeed = 0.5f;

        [Header("선택한 면접 정보 텍스트 UI")]
        [SerializeField] private TextMeshProUGUI jobText;  // 좌측 직무 텍스트 (예: IT 개발자)
        [SerializeField] private TextMeshProUGUI typeText; // 우측 면접 유형 텍스트 (예: 일상적 대화 면접)

        private void Start()
        {
            // 면접 정보 UI 텍스트 업데이트
            UpdateInterviewInfo();

            // 비동기 씬 로딩 시작
            StartCoroutine(LoadScene());
        }

        /// <summary>
        /// InterviewManager에서 선택된 직무와 면접 유형을 읽어와 텍스트를 각각 업데이트합니다.
        /// </summary>
        private void UpdateInterviewInfo()
        {
            string jobName = "IT 개발자";
            string typeName = "일상적 대화 면접";

            if (InterviewManager.Instance != null)
            {
                // 선택된 직무 가져오기
                jobName = InterviewManager.Instance.SelectedJob.ToString();

                // 선택된 면접 유형 가져오기 후 한글 명칭 변환
                typeName = GetTypeDisplayName(InterviewManager.Instance.SelectedInterviewerType);
            }

            // 각각의 TextMeshProUGUI UI에 반영
            if (jobText != null)
            {
                jobText.text = jobName;
            }

            if (typeText != null)
            {
                typeText.text = typeName;
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

        IEnumerator LoadScene()
        {
            yield return null;
            AsyncOperation operation = SceneManager.LoadSceneAsync("Interview Room");
            operation.allowSceneActivation = false;

            while (!operation.isDone)
            {
                yield return null;
                if (progressbar.value < 0.9f)
                {
                    progressbar.value = Mathf.MoveTowards(progressbar.value, 0.9f, Time.deltaTime * loadSpeed);
                }
                else if (operation.progress >= 0.9f)
                {
                    progressbar.value = Mathf.MoveTowards(progressbar.value, 1f, Time.deltaTime * loadSpeed);
                }
                if (progressbar.value >= 1f && operation.progress >= 0.9f)
                {
                    operation.allowSceneActivation = true;
                }
            }
        }
    }
}

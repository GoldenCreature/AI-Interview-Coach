using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using HJS;

namespace PlayUI.Scripts
{
    public class Play : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI timerText;

        public float elapsedTime;
        private bool isTimerRunning = true;

        [Header("--- 설정 팝업 ---")]
        [SerializeField] private GameObject settingsPopup;
        [SerializeField] private WebCamOptionUI.Scripts.WebCamOption webCamController;

        private void OnEnable()
        {
            InterviewManager.OnInterviewEnded += HandleInterviewEnded;
        }

        private void OnDisable()
        {
            InterviewManager.OnInterviewEnded -= HandleInterviewEnded;
        }

        private void HandleInterviewEnded(InterviewResultData resultData)
        {
            Debug.Log("[Play] 면접 종료 감지 → 타이머 정지 및 데이터 저장");
            PauseTimer();

            // [핵심] 서로 다른 씬이므로 FeedbackManager에 추가하여 파일 저장
            if (FeedbackManager.Instance != null)
            {
                FeedbackData newData = new FeedbackData
                {
                    dateText = System.DateTime.Now.ToString("yyyy-MM-dd"),
                    jobText = InterviewManager.Instance != null ? InterviewManager.Instance.SelectedJob.ToString() : "IT",
                    typeText = InterviewManager.Instance != null ? InterviewManager.Instance.SelectedInterviewerType.ToString() : "일상적"
                };

                FeedbackManager.Instance.AddFeedback(newData);
            }
        }

        public void OnClickSettings()
        {
            if (settingsPopup != null)
            {
                settingsPopup.SetActive(true);
                PauseTimer();

                if (webCamController != null)
                    webCamController.StartCamTest();
            }
        }

        public void OnClickCloseSettings()
        {
            if (settingsPopup != null)
            {
                if (webCamController != null)
                    webCamController.StopCamTest();

                settingsPopup.SetActive(false);
                ResumeTimer();
            }
        }

        public void ResultBtn()
        {
            InterviewManager.Instance.EndInterview();
        }

        private void Update()
        {
            if (!isTimerRunning) return;

            elapsedTime += Time.deltaTime;
            int minutes = Mathf.FloorToInt(elapsedTime / 60);
            int seconds = Mathf.FloorToInt(elapsedTime % 60);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }

        public void PauseTimer()
        {
            isTimerRunning = false;
        }

        public void ResumeTimer()
        {
            isTimerRunning = true;
        }
    }
}
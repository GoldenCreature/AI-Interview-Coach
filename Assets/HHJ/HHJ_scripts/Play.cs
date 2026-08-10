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

        private void OnEnable()
        {
            // 면접 종료 이벤트 구독
            // UIManager 대신 Play.cs가 직접
            // 타이머 정지 및 시간 저장 담당
            InterviewManager.OnInterviewEnded += HandleInterviewEnded;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            InterviewManager.OnInterviewEnded -= HandleInterviewEnded;
        }

        // -----------------------------------------------
        // 면접 종료 시 타이머만 정지
        // 시간 저장 X (Result 씬에 타이머 표시 없음)
        // -----------------------------------------------
        private void HandleInterviewEnded(InterviewResultData resultData)
        {
            Debug.Log("[Play] 면접 종료 감지 → 타이머 정지");
            PauseTimer();
        }

        // 면접 종료 버튼
        // 직접 씬 전환 대신 EndInterview() 호출
        // → OnInterviewEnded 이벤트 발생
        // → UIManager가 자동으로 씬 전환 처리
        // → Play.cs가 자동으로 타이머 정지 처리
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
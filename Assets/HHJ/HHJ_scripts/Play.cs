using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using HJS; // ← 추가

namespace PlayUI.Scripts
{
    public class Play : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI timerText;
        public float elapsedTime;
        private bool isTimerRunning = true;

        // 면접 종료 버튼
        // 직접 씬 전환 대신 EndInterview() 호출
        // → OnInterviewEnded 이벤트 발생
        // → UIManager가 자동으로 타이머 정지, 씬 전환 처리
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
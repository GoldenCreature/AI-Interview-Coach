using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace PlayUI.Scripts
{
    public class Play : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI timerText;
        public float elapsedTime;
        private bool isTimerRunning = true;

        public void ResultBtn()
        {
            SceneManager.LoadScene("Result");
        }
        private void Update()
        {
            if (!isTimerRunning) return;

            elapsedTime += Time.deltaTime;
            int minutus = Mathf.FloorToInt(elapsedTime / 60);
            int seconds = Mathf.FloorToInt(elapsedTime % 60);
            timerText.text = string.Format("{0:00}:{1:00}",minutus,seconds);
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

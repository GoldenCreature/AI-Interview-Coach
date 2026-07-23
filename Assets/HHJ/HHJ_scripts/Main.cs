using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; 

namespace MainUI.Scripts
{
    public class Main : MonoBehaviour
    {
        public void PlayBtn()
        {
            SceneManager.LoadScene("Interviewer");
        }
        public void SettingBtn()
        {
            SceneManager.LoadScene("Setting");
        }
        public void ExitBtn()
        {
            Application.Quit();
        }
        public void FeedbackBtn()
        {
            SceneManager.LoadScene("FeedBack");
        }
    }
}

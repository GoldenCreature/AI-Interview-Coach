using HJS;
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
            GameManager.Instance.LoadInterviewSetupScene();
        }
        public void SettingBtn()
        {
            GameManager.Instance.LoadSettingScene();
        }
        public void ExitBtn()
        {
            GameManager.Instance.QuitApplication();
        }
        public void FeedbackBtn()
        {
            GameManager.Instance.LoadFeedbackScene();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeedBackUI.Scripts
{
    public class FeedBack : MonoBehaviour
    {
        public void ResultBtn()
        {
            SceneManager.LoadScene("Result");
        }
        public void MainBtn()
        {
            SceneManager.LoadScene("Main");
        }
    }
}

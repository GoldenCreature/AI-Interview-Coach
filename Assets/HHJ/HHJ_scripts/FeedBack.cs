using HJS;
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
            GameManager.Instance.LoadResultScene();
        }
        public void MainBtn()
        {
            GameManager.Instance.LoadTitleScene();
        }
    }
}

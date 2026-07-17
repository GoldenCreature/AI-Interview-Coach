using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Interviewer : MonoBehaviour
{
    public void MainBtn()
    {
        SceneManager.LoadScene("Main");
    }
    public void PlayBtn()
    {
        SceneManager.LoadScene("Loading1");
    }
}

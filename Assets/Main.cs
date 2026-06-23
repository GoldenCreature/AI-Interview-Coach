using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // 씬을 불러오기 위해 필요한 네임스페이스

public class Main : MonoBehaviour
{
  public void PlayBtn()
    {
        SceneManager.LoadScene("Loading"); // 다음 씬 불러오기
    }
 public void SettingBtn()
    {
        SceneManager.LoadScene("Setting"); // 다음 씬 불러오기
    }
 public void ExitBtn()
    {
        Application.Quit();
    }
}

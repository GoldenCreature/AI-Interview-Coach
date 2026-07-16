using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneLoad : MonoBehaviour
{
    public Slider progressbar; 
    public float loadSpeed = 0.5f; 

    private void Start()
    {
        StartCoroutine(LoadScene());
    }
    IEnumerator LoadScene()
    {
        yield return null; 
        AsyncOperation operation = SceneManager.LoadSceneAsync("Interviewer"); 
        operation.allowSceneActivation = false; 

        while (!operation.isDone)
        {
            yield return null; 
            if (progressbar.value < 0.9f)  
            {
                progressbar.value = Mathf.MoveTowards(progressbar.value, 0.9f, Time.deltaTime*loadSpeed); 
            }
            else if (operation.progress >= 0.9f) 
            {
                progressbar.value = Mathf.MoveTowards(progressbar.value, 1f, Time.deltaTime*loadSpeed); 
            }
            if (progressbar.value >= 1f && operation.progress >= 0.9f) 
            {
                operation.allowSceneActivation = true; 
            }
        }
    }
}

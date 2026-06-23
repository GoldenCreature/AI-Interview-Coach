using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneLoad : MonoBehaviour
{
    public Slider progressbar; // 유니티 UI Slider 연동 (로딩바로 사용)
    public float loadSpeed = 0.5f; // 로딩 바가 차오르는 속도 제어 변수

    private void Start()
    {
        StartCoroutine(LoadScene()); // 게임이 시작되면 LoadScene 코루틴을 실행
    }
    IEnumerator LoadScene()
    {
        yield return null; // 1프레임 대기 (씬 전환 직후의 렉을 방지)
        AsyncOperation operation = SceneManager.LoadSceneAsync("Play"); // "Play"라는 이름의 씬을 백그라운드에서 로딩하기 시작
        operation.allowSceneActivation = false; // 실제 로딩이 끝나도 자동으로 화면이 안 넘어가게 막음

        while (!operation.isDone)
        {
            yield return null; // 매 프레임 한 번씩 쉬어줌 (안 적으면 무한 루프 걸려서 유니티 멈춤)
            if (progressbar.value < 0.9f)  // [구간 1] 로딩 바가 아직 90%(0.9) 미만일 때
            {
                progressbar.value = Mathf.MoveTowards(progressbar.value, 0.9f, Time.deltaTime*loadSpeed); // progressbar의 가치를 지정한 속도(loadSpeed)에 맞춰 0.9까지 부드럽게 채움
            }
            else if (operation.progress >= 0.9f) // [구간 2] 실제 유니티 내부 로딩이 90% 이상 완료되었을 때
            {
                progressbar.value = Mathf.MoveTowards(progressbar.value, 1f, Time.deltaTime*loadSpeed); // 로딩 바를 나머지 100%(1.0)까지 마저 부드럽게 채움
            }
            if (progressbar.value >= 1f && operation.progress >= 0.9f) // [구간 3] 로딩 바도 100% 채워졌고, 실제 데이터 로딩도 90% 이상 끝났다면!
            {
                operation.allowSceneActivation = true; // 잠금을 해제하여 "Play" 씬으로 화면을 전환시킴
            }
        }
    }
}

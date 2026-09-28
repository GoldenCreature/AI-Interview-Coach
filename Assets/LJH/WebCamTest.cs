using UnityEngine;
using System.Collections;

public class WebcamTest : MonoBehaviour
{
    private WebCamTexture webcamTexture;

    IEnumerator Start()
    {
        Debug.Log("=== WebCam Test Start ===");

        // 카메라 권한 요청
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

        Debug.Log("Camera Permission: " +
                  Application.HasUserAuthorization(UserAuthorization.WebCam));

        // Unity가 인식한 카메라 목록
        WebCamDevice[] devices = WebCamTexture.devices;

        Debug.Log("Camera Count: " + devices.Length);

        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"Camera [{i}] : {devices[i].name}");
            Debug.Log($"Front Facing : {devices[i].isFrontFacing}");
        }

        if (devices.Length == 0)
        {
            Debug.LogError("Unity에서 카메라를 하나도 찾지 못했습니다.");
            yield break;
        }

        // 첫 번째 카메라 사용
        webcamTexture = new WebCamTexture(devices[0].name, 640, 480, 30);

        webcamTexture.Play();

        Debug.Log("Selected Camera : " + devices[0].name);
        Debug.Log("Camera Playing : " + webcamTexture.isPlaying);
    }

    private void OnDestroy()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DirectWebcamTest : MonoBehaviour
{
    [SerializeField] private RawImage displayImage;

    private WebCamTexture webcam;

    private IEnumerator Start()
    {
        Debug.Log("=== DIRECT WEBCAM TEST ===");

        var devices = WebCamTexture.devices;

        Debug.Log($"Device Count = {devices.Length}");

        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"Device[{i}] = {devices[i].name}");
        }

        if (devices.Length == 0)
        {
            Debug.LogError("Unity에서 카메라를 찾지 못했습니다.");
            yield break;
        }

        // 해상도/FPS를 강제로 지정하지 않고 카메라 기본값 사용
        webcam = new WebCamTexture(devices[0].name);

        Debug.Log($"Starting Camera = {devices[0].name}");

        webcam.Play();

        float elapsed = 0f;

        while (elapsed < 10f)
        {
            Debug.Log(
                $"Playing={webcam.isPlaying}, " +
                $"Width={webcam.width}, " +
                $"Height={webcam.height}, " +
                $"Updated={webcam.didUpdateThisFrame}"
            );

            if (webcam.width > 16)
            {
                Debug.Log("=== CAMERA START SUCCESS ===");

                if (displayImage != null)
                {
                    displayImage.texture = webcam;
                    displayImage.enabled = true;
                }

                yield break;
            }

            elapsed += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        Debug.LogError("=== CAMERA START FAILED ===");
    }

    private void OnDestroy()
    {
        if (webcam != null)
        {
            webcam.Stop();
        }
    }
}
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mediapipe.Unity.Sample;

namespace WebCamControllerUI.Scripts
{
    public class WebCamController : MonoBehaviour
    {
        [Header("UI 연결")]
        [SerializeField] private RawImage webcamDisplay;
        [SerializeField] private TextMeshProUGUI warningText;

        [Tooltip("카메라 오픈 대기 최대 시간(초)")]
        [SerializeField] private float waitTimeoutSeconds = 10f;

        private void Start()
        {
            StartCoroutine(WaitForMediaPipeCameraAndDisplay());
        }

        private IEnumerator WaitForMediaPipeCameraAndDisplay()
        {
            if (WebCamTexture.devices.Length == 0)
            {
                ShowNoCameraState("웹캠이 연결되지 않았습니다.\n카메라를 연결해 주세요.");
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < waitTimeoutSeconds)
            {
                var source = ImageSourceProvider.ImageSource;

                if (source != null && source.isPlaying)
                {
                    var tex = source.GetCurrentTexture();
                    if (tex != null && tex.width > 16)
                    {
                        webcamDisplay.texture = tex;
                        webcamDisplay.gameObject.SetActive(true);
                        warningText.gameObject.SetActive(false);
                        yield break;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Debug.LogError("[WebCamController] MediaPipe 카메라가 시간 내에 열리지 않았습니다.");
            ShowNoCameraState("카메라를 불러올 수 없습니다.\n장치 상태를 확인해 주세요.");
        }

        private void ShowNoCameraState(string message)
        {
            webcamDisplay.gameObject.SetActive(false);
            warningText.gameObject.SetActive(true);
            warningText.text = message;
        }

        private void OnDisable()
        {
            // 카메라 소유자가 아니므로 Stop() 호출하지 않음
        }
    }
}
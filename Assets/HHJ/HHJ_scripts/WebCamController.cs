using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WebCamControllerUI.Scripts
{
    public class WebCamController : MonoBehaviour
    {
        [Header("UI 연결")]
        [SerializeField] private RawImage webcamDisplay;       // 카메라 화면을 띄울 RawImage
        [SerializeField] private TextMeshProUGUI warningText;   // 경고 문구 텍스트

        private WebCamTexture webcamTexture;

        private void Start()
        {
            InitializeWebCam();
        }

        public void InitializeWebCam()
        {
            // 1. 연결된 웹캠 장치 검색
            WebCamDevice[] devices = WebCamTexture.devices;

            // 2. 웹캠이 없거나 연결되지 않은 경우
            if (devices.Length == 0)
            {
                ShowNoCameraState("웹캠이 연결되지 않았습니다.\n카메라를 연결해 주세요.");
                return;
            }

            // 3. 웹캠이 연결되어 있는 경우 (첫 번째 카메라 사용)
            try
            {
                webcamTexture = new WebCamTexture(devices[0].name, 1280, 720, 30);
                webcamDisplay.texture = webcamTexture;
                webcamTexture.Play();

                // UI 상태 전환
                webcamDisplay.gameObject.SetActive(true);
                warningText.gameObject.SetActive(false);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"웹캠 연결 실패: {e.Message}");
                ShowNoCameraState("카메라를 불러올 수 없습니다.\n장치 상태를 확인해 주세요.");
            }
        }

        private void ShowNoCameraState(string message)
        {
            webcamDisplay.gameObject.SetActive(false);
            warningText.gameObject.SetActive(true);
            warningText.text = message;
        }

        private void OnDisable()
        {
            // 씬 전환이나 비활성화 시 카메라 자원 해제
            if (webcamTexture != null && webcamTexture.isPlaying)
            {
                webcamTexture.Stop();
            }
        }
    }
}

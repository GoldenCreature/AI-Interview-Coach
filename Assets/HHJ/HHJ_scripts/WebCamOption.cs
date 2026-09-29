using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mediapipe.Unity.Sample;

namespace WebCamOptionUI.Scripts
{
    public class WebCamOption : MonoBehaviour
    {
        [Header("--- UI 컴포넌트 연결 ---")]
        [SerializeField] private RawImage displayImage;
        [SerializeField] private TextMeshProUGUI statusText;

        private WebCamTexture webCamTexture;
        private Coroutine camTestCoroutine;

        /// <summary>
        /// [카메라 테스트 시작] 버튼의 OnClick에 연결할 함수
        /// </summary>
        public void StartCamTest()
        {
            if (camTestCoroutine != null)
            {
                StopCoroutine(camTestCoroutine);
            }

            camTestCoroutine = StartCoroutine(CoStartCamTest());
        }

        private IEnumerator CoStartCamTest()
        {
            if (statusText != null)
                statusText.text = "카메라를 연결하는 중...";

            // 1. 연결된 카메라 장치가 있는지 확인
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                if (statusText != null)
                    statusText.text = "연결된 카메라를 찾을 수 없습니다.";
                yield break;
            }

            // 2. WebCamTexture 생성 및 재생
            webCamTexture = new WebCamTexture(devices[0].name, 640, 480, 30);
            webCamTexture.Play();

            // 3. 텍스처 준비 대기 (최대 5초 타임아웃)
            float timeout = 5.0f;
            float elapsed = 0f;

            while (webCamTexture.width <= 16 && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (webCamTexture.width <= 16)
            {
                if (statusText != null)
                    statusText.text = "카메라 영상을 불러올 수 없습니다.";
                yield break;
            }

            // 4. 화면에 표시
            if (displayImage != null)
            {
                displayImage.texture = webCamTexture;
                displayImage.enabled = true;
            }

            if (statusText != null)
            {
                statusText.text = "화면 테스트 중입니다...";
            }
        }

        /// <summary>
        /// [카메라 테스트 종료] 버튼의 OnClick에 연결할 함수
        /// </summary>
        public void StopCamTest()
        {
            if (camTestCoroutine != null)
            {
                StopCoroutine(camTestCoroutine);
                camTestCoroutine = null;
            }

            if (webCamTexture != null)
            {
                webCamTexture.Stop();
                webCamTexture = null;
            }

            if (displayImage != null)
            {
                displayImage.texture = null;
                displayImage.enabled = false;
            }

            if (statusText != null)
            {
                statusText.text = "카메라 테스트가 종료되었습니다.";
            }
        }

        private void OnDisable()
        {
            StopCamTest();
        }
    }
}
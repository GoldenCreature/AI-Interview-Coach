using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WebCamOptionUI.Scripts
{
    public class WebCamOption : MonoBehaviour
    {
        [SerializeField] private RawImage displayImage;       
        [SerializeField] private TextMeshProUGUI statusText;

        private WebCamTexture webCamTexture;

        public void StartCamTest()
        {
            if (WebCamTexture.devices.Length == 0)
            {
                statusText.text = "연결된 카메라를 찾을 수 없습니다.";
                return;
            }

            if (webCamTexture != null && webCamTexture.isPlaying)
            {
                return;
            }

            string defaultCamName = WebCamTexture.devices[0].name;
            webCamTexture = new WebCamTexture(defaultCamName, 1280, 720, 30);

            displayImage.texture = webCamTexture;
            webCamTexture.Play();

            statusText.text = "화면 테스트 중입니다...";
        }

        public void StopCamTest()
        {
            // 1. 웹캠 객체가 존재하고 작동 중인 경우에만 중지
            if (webCamTexture != null)
            {
                if (webCamTexture.isPlaying)
                {
                    webCamTexture.Stop();
                }
                webCamTexture = null;
            }

            // 2. 화면 출력용 RawImage 초기화 (있을 때만)
            if (displayImage != null)
            {
                displayImage.texture = null;
            }

            // 3. 카메라 연결 여부/실행 상태와 상관없이 항상 종료 메시지 출력
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

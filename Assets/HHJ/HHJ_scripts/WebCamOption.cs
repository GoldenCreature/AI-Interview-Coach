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
            if (webCamTexture != null && webCamTexture.isPlaying)
            {
                webCamTexture.Stop();
                displayImage.texture = null;
                statusText.text = "카메라 테스트가 종료되었습니다.";
            }
        }
        private void OnDisable()
        {
            StopCamTest();
        }
    }
}

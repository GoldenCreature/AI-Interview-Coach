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
        [SerializeField] private RawImage displayImage;
        [SerializeField] private TextMeshProUGUI statusText;

        public void StartCamTest()
        {
<<<<<<< Updated upstream
=======
            if (camTestCoroutine != null)
            {
                StopCoroutine(camTestCoroutine);
            }

            camTestCoroutine = StartCoroutine(CoStartCamTest());
        }

        private IEnumerator CoStartCamTest()
        {
            Debug.Log("=== WebCam Test ===");
            Debug.Log($"ImageSource = {ImageSourceProvider.ImageSource}");
            Debug.Log($"SourceType = {ImageSourceProvider.CurrentSourceType}");
>>>>>>> Stashed changes
            var source = ImageSourceProvider.ImageSource;

            if (source == null || !source.isPlaying)
            {
                if (statusText != null)
                    statusText.text = "연결된 카메라를 찾을 수 없습니다.";
                return;
            }

            var tex = source.GetCurrentTexture();
            if (tex == null)
            {
                if (statusText != null)
                    statusText.text = "카메라 영상을 불러올 수 없습니다.";
                return;
            }

            if (displayImage != null)
                displayImage.texture = tex;

            if (statusText != null)
                statusText.text = "화면 테스트 중입니다...";
        }

        public void StopCamTest()
        {
            // 1. 화면 출력용 RawImage 초기화 (있을 때만)
            if (displayImage != null)
            {
                displayImage.texture = null;
            }

            // 2. 항상 종료 메시지 출력
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
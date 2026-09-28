using System.Collections;
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

        // 웹캠 테스트 Coroutine을 저장
        private Coroutine camTestCoroutine;

        public void StartCamTest()
        {
            // 이미 실행 중인 테스트가 있으면 중지
            if (camTestCoroutine != null)
            {
                StopCoroutine(camTestCoroutine);
            }

            // 웹캠 테스트 시작
            camTestCoroutine = StartCoroutine(CoStartCamTest());
        }

        private IEnumerator CoStartCamTest()
        {
            Debug.Log("=== WebCam Test ===");
            Debug.Log($"ImageSource = {ImageSourceProvider.ImageSource}");
            Debug.Log($"SourceType = {ImageSourceProvider.CurrentSourceType}");

            var source = ImageSourceProvider.ImageSource;

            // ImageSource가 없거나 카메라가 재생 중이 아니면 실패
            if (source == null || !source.isPlaying)
            {
                if (statusText != null)
                    statusText.text = "연결된 카메라를 찾을 수 없습니다.";

                yield break;
            }

            // 현재 카메라 텍스처 가져오기
            var tex = source.GetCurrentTexture();

            if (tex == null)
            {
                if (statusText != null)
                    statusText.text = "카메라 영상을 불러올 수 없습니다.";

                yield break;
            }

            // RawImage에 카메라 영상 표시
            if (displayImage != null)
            {
                displayImage.texture = tex;
                displayImage.enabled = true;
            }

            if (statusText != null)
                statusText.text = "화면 테스트 중입니다...";

            yield return null;
        }

        public void StopCamTest()
        {
            // 실행 중인 Coroutine 중지
            if (camTestCoroutine != null)
            {
                StopCoroutine(camTestCoroutine);
                camTestCoroutine = null;
            }

            // 화면 출력용 RawImage 초기화
            if (displayImage != null)
            {
                displayImage.texture = null;
                displayImage.enabled = false;
            }

            // 종료 메시지
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
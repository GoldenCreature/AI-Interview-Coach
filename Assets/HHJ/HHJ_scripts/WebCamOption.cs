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
            var source = ImageSourceProvider.ImageSource;

            if (source == null)
            {
                if (statusText != null)
                    statusText.text = "연결된 카메라를 찾을 수 없습니다.";
                yield break;
            }

            if (statusText != null)
                statusText.text = "카메라를 연결하는 중...";

            // 1. 카메라가 재생 중이 아니면 재생
            if (!source.isPlaying)
            {
                yield return source.Play();
            }

            // 2. 텍스처 준비 대기 (최대 5초 타임아웃)
            float timeout = 5.0f;
            float elapsed = 0f;

            while (source.GetCurrentTexture() == null && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 3. 텍스처 확인 및 적용
            var tex = source.GetCurrentTexture();
            if (tex == null)
            {
                if (statusText != null)
                    statusText.text = "카메라 영상을 불러올 수 없습니다.";
                yield break;
            }

            if (displayImage != null)
            {
                displayImage.texture = tex;
                displayImage.enabled = true; // 화면 켜기
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

            if (displayImage != null)
            {
                displayImage.texture = null;
                displayImage.enabled = false; // 화면 끄기 (흰 상자 안 보이게)
            }

            if (statusText != null)
            {
                statusText.text = "카메라 테스트가 종료되었습니다.";
            }
        }

        private void OnDisable()
        {
            // 설정 창이 닫히면 자동으로 테스트 종료
            StopCamTest();
        }
    }
}
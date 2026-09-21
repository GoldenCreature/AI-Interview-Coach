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

        [Header("텍스트 색상 설정")]
        [Tooltip("경고 메시지 출력 시 적용할 색상")]
        [SerializeField] private Color warningTextColor = new Color(1f, 0.3f, 0.3f, 1f); // 빨간색 계열
        [Tooltip("로딩 메시지 출력 시 적용할 색상")]
        [SerializeField] private Color loadingTextColor = Color.white; // 흰색

        [Tooltip("카메라 오픈 대기 최대 시간(초)")]
        [SerializeField] private float waitTimeoutSeconds = 10f;

        private void Start()
        {
            StartCoroutine(WaitForMediaPipeCameraAndDisplay());
        }

        private IEnumerator WaitForMediaPipeCameraAndDisplay()
        {
            // 1. 장치 연결 유무와 상관없이 무조건 '불러오는 중' 상태로 시작
            webcamDisplay.gameObject.SetActive(false);
            warningText.gameObject.SetActive(true);
            warningText.color = loadingTextColor;
            warningText.text = "카메라를 불러오는 중...";

            float elapsed = 0f;

            // 2. 지정한 시간 동안 프레임이 읽히는지 계속 대기
            while (elapsed < waitTimeoutSeconds)
            {
                var source = ImageSourceProvider.ImageSource;

                if (source != null && source.isPlaying)
                {
                    var tex = source.GetCurrentTexture();
                    if (tex != null && tex.width > 16)
                    {
                        // 카메라 연결 성공 시 화면 표시 및 텍스트 숨김
                        webcamDisplay.texture = tex;
                        webcamDisplay.gameObject.SetActive(true);
                        warningText.gameObject.SetActive(false);
                        yield break;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 3. 지정된 시간이 지난 후에도 카메라가 열리지 않으면 장치 검사 후 빨간색 경고 출력
            Debug.LogError("[WebCamController] MediaPipe 카메라가 시간 내에 열리지 않았습니다.");

            if (WebCamTexture.devices.Length == 0)
            {
                ShowNoCameraState("웹캠이 연결되지 않았습니다.\n카메라를 연결해 주세요.");
            }
            else
            {
                ShowNoCameraState("카메라를 불러올 수 없습니다.\n장치 상태를 확인해 주세요.");
            }
        }

        private void ShowNoCameraState(string message)
        {
            webcamDisplay.gameObject.SetActive(false);
            warningText.gameObject.SetActive(true);
            warningText.color = warningTextColor; // 빨간색 경고 색상 적용
            warningText.text = message;
        }

        private void OnDisable()
        {
            // 카메라 소유자가 아니므로 Stop() 호출하지 않음
        }
    }
}
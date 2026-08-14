using UnityEngine;
using TMPro;
using HJS;

namespace SettingUI.Scripts
{
    public class ApiKeySettingUI : MonoBehaviour
    {
        [Header("--- Gemini API 키 입력 ---")]
        // Inspector에서 Gemini InputField 연결
        [SerializeField] private TMP_InputField geminiApiInputField;

        [Header("--- Google Cloud API 키 입력 ---")]
        // Inspector에서 Cloud InputField 연결
        [SerializeField] private TMP_InputField googleApiInputField;

        [Header("--- 상태 텍스트 ---")]
        // 키 설정 완료 또는 오류 메시지 표시용 (선택사항)
        [SerializeField] private TextMeshProUGUI geminiStatusText;
        [SerializeField] private TextMeshProUGUI googleStatusText;

        // -----------------------------------------------
        // Gemini API 키 확인 버튼 클릭 시 호출
        // SettingUI 프리팹의 Gemini 확인 버튼 OnClick에 연결
        // -----------------------------------------------
        public void OnGeminiApiKeyConfirm()
        {
            if (geminiApiInputField == null) return;

            string key = geminiApiInputField.text.Trim();

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[ApiKeySettingUI] Gemini API 키가 비어있습니다!");
                if (geminiStatusText != null)
                {
                    geminiStatusText.text = "※ API 키를 입력해주세요!";
                    geminiStatusText.color = Color.red;
                }
                return;
            }

            // SettingsManager에 키 전달
            SettingsManager.Instance.SetGeminiApiKey(key);
            Debug.Log("[ApiKeySettingUI] Gemini API 키 설정 완료");

            // 상태 텍스트 업데이트
            if (geminiStatusText != null)
            { 
                geminiStatusText.text = "Gemini API 키 설정 완료";
                geminiStatusText.color = Color.green;
            }

            // 보안을 위해 입력 필드 초기화
            geminiApiInputField.text = "";
        }

        // -----------------------------------------------
        // Google Cloud API 키 확인 버튼 클릭 시 호출
        // SettingUI 프리팹의 Cloud 확인 버튼 OnClick에 연결
        // -----------------------------------------------
        public void OnGoogleApiKeyConfirm()
        {
            if (googleApiInputField == null) return;

            string key = googleApiInputField.text.Trim();

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[ApiKeySettingUI] Google Cloud API 키가 비어있습니다!");
                if (googleStatusText != null)
                { 
                    googleStatusText.text = "※ API 키를 입력해주세요!";
                    googleStatusText.color = Color.red;
                }
                return;
            }

            // SettingsManager에 키 전달
            SettingsManager.Instance.SetGoogleApiKey(key);
            Debug.Log("[ApiKeySettingUI] Google Cloud API 키 설정 완료");

            // 상태 텍스트 업데이트
            if (googleStatusText != null)
            { 
                googleStatusText.text = "Google Cloud API 키 설정 완료";
                googleStatusText.color = Color.green;
            }

            // 보안을 위해 입력 필드 초기화
            googleApiInputField.text = "";
        }
    }
}
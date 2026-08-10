using UnityEngine;

namespace HJS
{
    public class SettingsManager : SingletonBase<SettingsManager>
    {
        // -----------------------------------------------
        // API 키 보관
        // 메모리에만 존재 → 앱 종료 시 사라짐
        // 절대 PlayerPrefs나 파일로 저장하지 말 것
        // -----------------------------------------------

        // Gemini API 키
        public string GeminiApiKey { get; private set; } = "";

        // Google Cloud API 키 (STT + TTS 공용)
        public string GoogleApiKey { get; private set; } = "";

        // -----------------------------------------------
        // 마이크 입력 키 설정
        // 기본값: Space (스페이스바)
        // 나중에 설정 화면에서 변경 가능
        // -----------------------------------------------
        public KeyCode MicKey { get; private set; } = KeyCode.Space;

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("[SettingsManager] 초기화 완료");
        }

        // -----------------------------------------------
        // 설정 화면 UI에서 호출하는 함수들
        // -----------------------------------------------

        // Gemini API 키 설정
        // Setting 씬 UI 입력 필드 → 확인 버튼 클릭 시 호출
        public void SetGeminiApiKey(string key)
        {
            GeminiApiKey = key;
            Debug.Log("[SettingsManager] Gemini API 키 설정 완료");
        }

        // Google Cloud API 키 설정
        // Setting 씬 UI 입력 필드 → 확인 버튼 클릭 시 호출
        public void SetGoogleApiKey(string key)
        {
            GoogleApiKey = key;
            Debug.Log("[SettingsManager] Google Cloud API 키 설정 완료");
        }

        // 마이크 입력 키 설정
        // Setting 씬 UI에서 키 입력 받아서 호출
        public void SetMicKey(KeyCode key)
        {
            MicKey = key;
            Debug.Log($"[SettingsManager] 마이크 입력 키 설정 완료: {key}");
        }

        // -----------------------------------------------
        // API 키 유효성 검사
        // 면접 시작 버튼 클릭 시 호출
        // 비어있으면 false 반환 → 경고 메시지 표시
        // -----------------------------------------------
        public bool IsApiKeysValid()
        {
            if (string.IsNullOrEmpty(GeminiApiKey))
            {
                Debug.LogWarning("[SettingsManager] Gemini API 키가 비어있습니다!");
                return false;
            }

            if (string.IsNullOrEmpty(GoogleApiKey))
            {
                Debug.LogWarning("[SettingsManager] Google Cloud API 키가 비어있습니다!");
                return false;
            }

            return true;
        }
    }
}
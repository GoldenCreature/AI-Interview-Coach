using HJS;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ApplyKeySetting.Scripts
{
    public class ApplyKeySetting : MonoBehaviour
    {
        [Header("UI 연결")]
        public TextMeshProUGUI keyDisplayText;
        public Button changeBtn;
        public Button applyBtn;

        [Header("키 설정 상태")]
        public KeyCode currentApplyKey = KeyCode.Space;
        private KeyCode tempKey = KeyCode.Space;
        private bool isRebinding = false;

        private const string SAVE_KEY_NAME = "ApplyKeyCode";

        // [핵심] 키 변경 알림 static 이벤트 생성
        public static event Action OnApplyKeyChanged;

        private void Awake()
        {
            if (changeBtn != null) changeBtn.onClick.AddListener(OnClickChangeButton);
            if (applyBtn != null) applyBtn.onClick.AddListener(OnClickApplyButton);
        }

        private void OnEnable()
        {
            isRebinding = false;
            LoadMuteKey();
            UpdateUI();
        }

        private void OnGUI()
        {
            if (isRebinding)
            {
                Event e = Event.current;
                if (e.isKey && e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
                {
                    tempKey = e.keyCode;
                    keyDisplayText.text = tempKey.ToString();
                    isRebinding = false;
                }
            }
        }

        public void OnClickChangeButton()
        {
            isRebinding = true;
            keyDisplayText.text = "<color=red>키 입력 대기 중...</color>";

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void OnClickApplyButton()
        {
            currentApplyKey = tempKey;

            // PlayerPrefs에 저장 (앱 재실행 시 유지)
            SaveMuteKey();

            // SettingsManager에도 동기화
            // SpeechToTextManager가 이 값을 사용해 마이크 입력 감지
            SettingsManager.Instance.SetMicKey(currentApplyKey);

            UpdateUI();
            OnApplyKeyChanged?.Invoke();
            Debug.Log($"[음소거 키 적용 완료] 변경된 키: {currentApplyKey}");
        }

        private void SaveMuteKey()
        {
            PlayerPrefs.SetString(SAVE_KEY_NAME, currentApplyKey.ToString());
            PlayerPrefs.Save();
        }

        public void LoadMuteKey()
        {
            // PlayerPrefs에서 저장된 키 불러오기
            string savedKey = PlayerPrefs.GetString(SAVE_KEY_NAME, KeyCode.Space.ToString());

            if (Enum.TryParse(savedKey, out KeyCode loadedKey))
                currentApplyKey = loadedKey;
            else
                currentApplyKey = KeyCode.Space;

            tempKey = currentApplyKey;

            // SettingsManager에도 동기화
            // 앱 시작 시 저장된 키값이 SpeechToTextManager에 반영됨
            SettingsManager.Instance.SetMicKey(currentApplyKey);
        }

        private void UpdateUI()
        {
            if (keyDisplayText != null)
            {
                keyDisplayText.text = currentApplyKey.ToString();
            }
        }
    }
}

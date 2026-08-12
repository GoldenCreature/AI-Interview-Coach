using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace MuteKeySetting.Scripts
{
    public class MuteKeySetting : MonoBehaviour
    {
        [Header("UI 연결")]
        public TextMeshProUGUI keyDisplayText;
        public Button changeBtn;
        public Button applyBtn;

        [Header("키 설정 상태")]
        public KeyCode currentMuteKey = KeyCode.Space;
        private KeyCode tempKey = KeyCode.Space;
        private bool isRebinding = false;

        private const string SAVE_KEY_NAME = "MuteKeyCode";

        // [핵심] 키 변경 알림 static 이벤트 생성
        public static event Action OnMuteKeyChanged;

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

        // [적용] 버튼 클릭 이벤트
        public void OnClickApplyButton()
        {
            currentMuteKey = tempKey;
            SaveMuteKey();
            UpdateUI();

            // [핵심] [적용] 클릭 시 키가 변경되었다고 전국(이벤트를 듣고 있는 곳)에 방송!
            OnMuteKeyChanged?.Invoke();

            Debug.Log($"[음소거 키 저장 및 적용 완료] 변경된 키: {currentMuteKey}");
        }

        private void SaveMuteKey()
        {
            PlayerPrefs.SetString(SAVE_KEY_NAME, currentMuteKey.ToString());
            PlayerPrefs.Save();
        }

        public void LoadMuteKey()
        {
            string savedKey = PlayerPrefs.GetString(SAVE_KEY_NAME, KeyCode.Space.ToString());

            if (Enum.TryParse(savedKey, out KeyCode loadedKey))
            {
                currentMuteKey = loadedKey;
            }
            else
            {
                currentMuteKey = KeyCode.Space;
            }

            tempKey = currentMuteKey;
        }

        private void UpdateUI()
        {
            if (keyDisplayText != null)
            {
                keyDisplayText.text = currentMuteKey.ToString();
            }
        }
    }
}

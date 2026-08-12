using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MuteKeySetting.Scripts;

namespace InterviewMute.Scripts
{
    public class InterviewMuteController : MonoBehaviour
    {
        [Header("--- 마이크 UI 연결 ---")]
        [SerializeField] private Image micImage;
        [SerializeField] private Sprite micOnSprite;
        [SerializeField] private Sprite micOffSprite;

        [Header("--- 마이크 상태 ---")]
        public bool isMuted = false;

        private KeyCode muteKey = KeyCode.Space;
        private const string SAVE_KEY_NAME = "MuteKeyCode";

        private void OnEnable()
        {
            // 설정 창에서 [적용]을 누를 때 발생하는 이벤트 구독
            MuteKeySetting.Scripts.MuteKeySetting.OnMuteKeyChanged += LoadMuteKey;

            LoadMuteKey();
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            MuteKeySetting.Scripts.MuteKeySetting.OnMuteKeyChanged -= LoadMuteKey;
        }

        private void Start()
        {
            UpdateMicUI();
        }

        private void Update()
        {
            // 최신화된 음소거 키 입력 감지
            if (Input.GetKeyDown(muteKey))
            {
                ToggleMute();
            }
        }

        public void LoadMuteKey()
        {
            string savedKey = PlayerPrefs.GetString(SAVE_KEY_NAME, KeyCode.Space.ToString());

            if (System.Enum.TryParse(savedKey, out KeyCode loadedKey))
            {
                muteKey = loadedKey;
            }
            else
            {
                muteKey = KeyCode.Space;
            }

            Debug.Log($"[면접 화면] 최신 음소거 키 감지 완료: {muteKey}");
        }

        public void ToggleMute()
        {
            isMuted = !isMuted;
            UpdateMicUI();

            if (isMuted)
            {
                Debug.Log($"[면접] 마이크 음소거 ON ({muteKey} 키)");
            }
            else
            {
                Debug.Log($"[면접] 마이크 음소거 OFF ({muteKey} 키)");
            }
        }

        private void UpdateMicUI()
        {
            if (micImage == null) return;

            if (micOnSprite != null && micOffSprite != null)
            {
                micImage.sprite = isMuted ? micOffSprite : micOnSprite;
            }
        }
    }
}

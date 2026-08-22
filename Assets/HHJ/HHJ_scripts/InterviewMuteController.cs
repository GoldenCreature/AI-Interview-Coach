using System;
using UnityEngine;
using UnityEngine.UI;
using GoogleSpeechToText.Scripts;

namespace InterviewMute.Scripts
{
    public class InterviewMuteController : MonoBehaviour
    {
        [Header("--- 마이크 UI 연결 ---")]
        [SerializeField] private Image micImage;
        [SerializeField] private Sprite micOnSprite;
        [SerializeField] private Sprite micOffSprite;

        [Header("--- 마이크 상태 ---")]
        public bool isMuted = true; // 기본값: 녹음 중 아님

        private void OnEnable()
        {
            // SpeechToTextManager 녹음 이벤트 구독
            // 녹음 시작/종료 시 마이크 아이콘 자동 변경
            SpeechToTextManager.OnRecordingStarted += HandleRecordingStarted;
            SpeechToTextManager.OnRecordingStopped += HandleRecordingStopped;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            SpeechToTextManager.OnRecordingStarted -= HandleRecordingStarted;
            SpeechToTextManager.OnRecordingStopped -= HandleRecordingStopped;
        }

        private void Start()
        {
            // 시작 시 기본 아이콘 표시 (마이크 Off 상태)
            UpdateMicUI();
        }

        // -----------------------------------------------
        // 녹음 시작 시 자동 호출
        // 마이크 아이콘 On으로 변경
        // -----------------------------------------------
        private void HandleRecordingStarted()
        {
            isMuted = false;
            UpdateMicUI();
            Debug.Log("[InterviewMuteController] 마이크 ON");
        }

        // -----------------------------------------------
        // 녹음 종료 시 자동 호출
        // 마이크 아이콘 Off로 변경
        // -----------------------------------------------
        private void HandleRecordingStopped()
        {
            isMuted = true;
            UpdateMicUI();
            Debug.Log("[InterviewMuteController] 마이크 OFF");
        }

        // -----------------------------------------------
        // 마이크 아이콘 UI 업데이트
        // isMuted 상태에 따라 스프라이트 변경
        // -----------------------------------------------
        private void UpdateMicUI()
        {
            if (micImage == null) return;

            if (micOnSprite != null && micOffSprite != null)
                micImage.sprite = isMuted ? micOffSprite : micOnSprite;
        }
    }
}
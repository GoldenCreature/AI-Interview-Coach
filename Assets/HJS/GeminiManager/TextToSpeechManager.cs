using System;
using GoogleTextToSpeech.Scripts.Data;
using UnityEngine;
using GoogleTextToSpeech.Scripts;

namespace GoogleTextToSpeech.Scripts
{
    public class TextToSpeechManager : MonoBehaviour
    {
        [SerializeField] private VoiceScriptableObject voice;
        [SerializeField] private TextToSpeech text_to_speech;
        [SerializeField] private AudioSource audioSource;

        private Action<AudioClip> _audioClipReceived;
        private Action<BadRequestData> _errorReceived;
        private bool _isRequestPending = false;   // 구글에 요청했고 아직 응답을 못 받은 상태

        public bool IsPlaying => audioSource != null && audioSource.isPlaying;
        public bool IsBusy => _isRequestPending || IsPlaying;   // 요청 중이거나 재생 중

        private void OnEnable()
        {
            // Gemini 응답 이벤트 구독
            // GeminiManager가 응답을 받으면 자동으로 TTS 실행
            HJS.InterviewManager.OnGeminiResponseReceived += SendTextToGoogle;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            HJS.InterviewManager.OnGeminiResponseReceived -= SendTextToGoogle;
        }

        // -----------------------------------------------
        // 직접 호출 불필요 → 이벤트로 자동 실행됨
        // -----------------------------------------------
        public void SendTextToGoogle(string _text)
        {
            if(text_to_speech == null)
            {
                text_to_speech = FindObjectOfType<TextToSpeech>();
                Debug.Log("[TTS] TextToSpeech 컴포넌트를 찾았습니다");
            }
                
            if(text_to_speech == null)
            {
                Debug.LogError("[TTS] TextToSpeech 컴포넌트를 찾을 수 없습니다!");
                return;
            }

            _errorReceived = ErrorReceived; 
            _audioClipReceived = AudioClipReceived;
            _isRequestPending = true;
            text_to_speech.GetSpeechAudioFromGoogle(
                _text, voice, _audioClipReceived, _errorReceived);
        }

        private void ErrorReceived(BadRequestData badRequestData)
        {
            _isRequestPending = false;
            Debug.Log($"[TTS] 오류 {badRequestData.error.code}: {badRequestData.error.message}");
        }

        private void AudioClipReceived(AudioClip clip)
        {
            _isRequestPending = false;

            if (audioSource == null)
            {
                var go = GameObject.Find("genSuit");
                if (go != null)
                    audioSource = go.GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                Debug.LogError("[TTS] AudioSource를 찾을 수 없습니다!");
                return;
            }

            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play(); 
        }
    }
}
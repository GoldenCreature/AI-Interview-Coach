using HJS;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;

namespace GoogleSpeechToText.Scripts
{
    public class SpeechToTextManager : MonoBehaviour
    {
        // API 키는 SettingsManager에서 가져옴
        private string ApiKey => SettingsManager.Instance.GoogleApiKey;

        // 녹음 시작/종료 이벤트
        // 구독자: InterviewMuteController (마이크 아이콘 변경)
        public static event Action OnRecordingStarted;
        public static event Action OnRecordingStopped;

        private AudioClip clip;
        private byte[] bytes;
        private bool recording = false;

        void Update()
        {
            if (Input.GetKeyDown(SettingsManager.Instance.MicKey) && !recording)
            {
                StartRecording();
                recording = true;
            }

            if (Input.GetKeyUp(SettingsManager.Instance.MicKey) && recording)
            {
                StopRecording();
                recording = false;
            }
        }

        private void StartRecording()
        {
            clip = Microphone.Start(null, false, 60, 44100);
            recording = true;

            // 마이크 아이콘 On 알림
            OnRecordingStarted?.Invoke();
        }

        private byte[] EncodeAsWAV(float[] samples, int frequency, int channels)
        {
            using (var memoryStream = new MemoryStream(44 + samples.Length * 2))
            {
                using (var writer = new BinaryWriter(memoryStream))
                {
                    writer.Write("RIFF".ToCharArray());
                    writer.Write(36 + samples.Length * 2);
                    writer.Write("WAVE".ToCharArray());
                    writer.Write("fmt ".ToCharArray());
                    writer.Write(16);
                    writer.Write((ushort)1);
                    writer.Write((ushort)channels);
                    writer.Write(frequency);
                    writer.Write(frequency * channels * 2);
                    writer.Write((ushort)(channels * 2));
                    writer.Write((ushort)16);
                    writer.Write("data".ToCharArray());
                    writer.Write(samples.Length * 2);
                    foreach (var sample in samples)
                    {
                        writer.Write((short)(sample * short.MaxValue));
                    }
                }
                return memoryStream.ToArray();
            }
        }

        private void StopRecording()
        {
            var position = Microphone.GetPosition(null);
            Microphone.End(null);

            // 마이크 아이콘 Off 알림
            OnRecordingStopped?.Invoke();

            var samples = new float[position * clip.channels];
            clip.GetData(samples, 0);
            bytes = EncodeAsWAV(samples, clip.frequency, clip.channels);
            recording = false;

            GoogleCloudSpeechToText.SendSpeechToTextRequest(bytes, ApiKey,
                (response) =>
                {
                    Debug.Log("Speech-to-Text Response: " + response);

                    var speechResponse = JsonUtility.FromJson<SpeechToTextResponse>(response);

                    // 결과가 없는 경우 (묵음, 노이즈 등)
                    if (speechResponse == null ||
                        speechResponse.results == null ||
                        speechResponse.results.Length == 0)
                    {
                        Debug.LogWarning("[SpeechToTextManager] STT 결과 없음 (묵음 또는 노이즈)");
                        return;
                    }

                    // 대안 텍스트가 없는 경우
                    if (speechResponse.results[0].alternatives == null ||
                        speechResponse.results[0].alternatives.Length == 0)
                    {
                        Debug.LogWarning("[SpeechToTextManager] STT 대안 텍스트 없음");
                        return;
                    }

                    var transcript = speechResponse.results[0].alternatives[0].transcript;

                    // 텍스트가 비어있는 경우
                    if (string.IsNullOrEmpty(transcript))
                    {
                        Debug.LogWarning("[SpeechToTextManager] STT 변환 텍스트 비어있음");
                        return;
                    }

                    Debug.Log($"[SpeechToTextManager] STT 결과: {transcript}");
                    HJS.InterviewManager.NotifyTranscriptReceived(transcript);
                },
                (error) =>
                {
                    Debug.LogError($"[SpeechToTextManager] STT 오류: {error.error.message}");
                });
        }
    }
}
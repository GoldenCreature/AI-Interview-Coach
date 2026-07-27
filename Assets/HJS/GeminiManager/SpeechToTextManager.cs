using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace GoogleSpeechToText.Scripts
{
    public class SpeechToTextManager : MonoBehaviour
    {
        [Header("Google Cloud API Key")]
        [SerializeField] private string apiKey;

        private AudioClip clip;
        private byte[] bytes;
        private bool recording = false;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) && !recording)
            {
                StartRecording();
                recording = true;
            }

            if (Input.GetKeyUp(KeyCode.Space) && recording)
            {
                StopRecording();
                recording = false;
            }
        }

        private void StartRecording()
        {
            clip = Microphone.Start(null, false, 10, 44100);
            recording = true;
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
            var samples = new float[position * clip.channels];
            clip.GetData(samples, 0);
            bytes = EncodeAsWAV(samples, clip.frequency, clip.channels);
            recording = false;

            GoogleCloudSpeechToText.SendSpeechToTextRequest(bytes, apiKey,
                (response) =>
                {
                    Debug.Log("Speech-to-Text Response: " + response);
                    var speechResponse = JsonUtility.FromJson<SpeechToTextResponse>(response);
                    var transcript = speechResponse.results[0].alternatives[0].transcript;
                    Debug.Log($"[SpeechToTextManager] STT 결과: {transcript}");
                    // 직접 호출 대신 이벤트 발생
                    // FillerWordDetector, GeminiManager가 자동으로 반응함
                    HJS.InterviewManager.NotifyTranscriptReceived(transcript);
                },
                (error) =>
                {
                    Debug.LogError($"[SpeechToTextManager] STT 오류: {error.error.message}");
                });
        }
    }
}
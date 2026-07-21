using HJS;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GoogleSpeechToText.Scripts
{
    public class SpeechToTextManager : MonoBehaviour
    {
        // [SerializeField] private string audioUri = "gs://cloud-samples-tests/speech/brooklyn.flac"; // Audio file URI in Google Cloud Storage
        [Header("Google Cloud API Password")]
        [SerializeField] private string apiKey; // Replace with your API key
        [Header("Gemini Manager Prefab")]
        public UnityAndGeminiV3 geminiManager;

        [Header("말버릇 감지")]
        // Inspector에서 GoogleServices 프리팹의 FillerWordDetector를 드래그 연결
        public FillerWordDetector fillerDetector;

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
        
        if (Input.GetKeyUp(KeyCode.Space) && recording )
        
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

    private byte[] EncodeAsWAV(float[] samples, int frequency, int channels) {
        using (var memoryStream = new MemoryStream(44 + samples.Length * 2)) {
            using (var writer = new BinaryWriter(memoryStream)) {
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

                foreach (var sample in samples) {
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
                (response) => {
                    Debug.Log("Speech-to-Text Response: " + response);
                    // Parse the response if needed
                    var speechResponse = JsonUtility.FromJson<SpeechToTextResponse>(response);
                    var transcript = speechResponse.results[0].alternatives[0].transcript;
                    Debug.Log("Transcript: " + transcript);

                    // STT 결과를 말버릇 감지기에 먼저 전달
                    // fillerDetector가 연결되지 않았을 때 오류 방지용 null 체크 포함
                    if (fillerDetector != null)
                        fillerDetector.Analyze(transcript);

                    geminiManager.SendChat(transcript);

                },
                (error) => {
                    Debug.LogError("Error: " + error.error.message);
                });
    }

    }
}

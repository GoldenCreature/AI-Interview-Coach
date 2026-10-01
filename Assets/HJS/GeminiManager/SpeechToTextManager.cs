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

        private const int ChunkSeconds = 50;   // 구글 1회 요청 한도(약 60초)보다 짧게 나눠서 전송
        private const int MaxRecordSeconds = 180;   // 녹음 공간 크기 (최대 3분)
        private float recordStartTime;              // 녹음을 시작한 시각

        void Update()
        {
            if (!InterviewManager.Instance.IsInterviewActive) // 면접이 활성화 중이 아니라면
                return;                                       // 즉 InterViewRoom 씬이 아닐때 마이크 활성화 금지

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

            // 녹음 시간이 (녹음 공간 3분 - 5초)에 도달하면 자동으로 멈추고 전송
            if (recording && Time.realtimeSinceStartup - recordStartTime >= MaxRecordSeconds - 5)
            {
                Debug.Log("[SpeechToTextManager] 최대 녹음 시간 도달 - 자동 종료 후 전송");
                StopRecording();
                recording = false;
            }
        }

        public void StartRecording()
        {
            clip = Microphone.Start(null, false, MaxRecordSeconds, 44100);
            recordStartTime = Time.realtimeSinceStartup;   // 녹음 시작 시각 기록
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

        public void StopRecording()
        {
            var position = Microphone.GetPosition(null);
            Microphone.End(null);

            // 마이크 아이콘 Off 알림
            OnRecordingStopped?.Invoke();
            recording = false;

            // 녹음된 소리가 없으면 전송하지 않음
            if (position <= 0)
            {
                Debug.LogWarning("[SpeechToTextManager] 녹음된 소리 없음");
                return;
            }

            int channels = clip.channels;
            int frequency = clip.frequency;

            // 녹음된 부분만 꺼냄
            var samples = new float[position * channels];
            clip.GetData(samples, 0);

            // 50초 분량의 샘플 수, 조각 개수 (나머지가 있으면 1개 추가)
            int chunkSamples = ChunkSeconds * frequency * channels;
            int chunkCount = (samples.Length + chunkSamples - 1) / chunkSamples;

            var results = new string[chunkCount];   // 조각별 인식 결과 (순서 유지용)
            int remaining = chunkCount;             // 아직 응답을 못 받은 조각 수

            Debug.Log($"[SpeechToTextManager] 녹음 {position / (float)frequency:F1}초 → {chunkCount}개 조각으로 전송");

            // 조각 하나의 응답이 끝날 때마다 호출. 마지막 조각까지 끝나면 이어 붙여서 전달
            void OnChunkFinished()
            {
                remaining--;
                if (remaining > 0) return;   // 아직 기다리는 조각이 있음

                var sb = new System.Text.StringBuilder();
                foreach (var t in results)
                {
                    if (string.IsNullOrEmpty(t)) continue;
                    if (sb.Length > 0) sb.Append(" ");
                    sb.Append(t);
                }

                var transcript = sb.ToString();
                if (string.IsNullOrEmpty(transcript))
                {
                    Debug.LogWarning("[SpeechToTextManager] STT 결과 없음 (묵음 또는 노이즈)");
                    return;
                }

                Debug.Log($"[SpeechToTextManager] STT 결과: {transcript}");
                HJS.InterviewManager.NotifyTranscriptReceived(transcript);
            }

            // 조각마다 WAV로 만들어 구글에 전송
            for (int i = 0; i < chunkCount; i++)
            {
                int index = i;                                         // 콜백에서 쓸 조각 번호 복사
                int start = i * chunkSamples;                          // 이 조각의 시작 위치
                int length = Mathf.Min(chunkSamples, samples.Length - start);   // 마지막 조각은 더 짧을 수 있음

                var chunk = new float[length];
                Array.Copy(samples, start, chunk, 0, length);          // 원본에서 이 조각만 복사
                byte[] wav = EncodeAsWAV(chunk, frequency, channels);

                GoogleCloudSpeechToText.SendSpeechToTextRequest(wav, ApiKey,
                    (response) =>
                    {
                        results[index] = ExtractTranscript(response);  // 1단계에서 만든 함수 재사용
                        OnChunkFinished();
                    },
                    (error) =>
                    {
                        Debug.LogError($"[SpeechToTextManager] STT 오류(조각 {index}): {error.error.message}");
                        results[index] = "";                           // 실패한 조각은 빈 값, 나머지는 살림
                        OnChunkFinished();
                    });
            }
        }

        // 구글 응답(JSON)에서 글자만 꺼내는 함수
        // results(결과 조각)가 여러 개면 순서대로 이어 붙여서 하나의 문장으로 돌려줌
        private string ExtractTranscript(string response)
        {
            // 구글이 보낸 원본 응답을 확인용으로 출력 (기존 로그와 동일)
            Debug.Log("Speech-to-Text Response: " + response);

            // JSON 글자를 코드에서 쓸 수 있는 객체로 변환
            var speechResponse = JsonUtility.FromJson<SpeechToTextResponse>(response);

            // 결과가 아예 없으면 빈 글자 반환
            if (speechResponse == null || speechResponse.results == null) return "";

            // 여러 조각의 글자를 이어 붙이기 위한 도구
            var sb = new System.Text.StringBuilder();

            foreach (var r in speechResponse.results)   // 결과 조각을 하나씩 꺼냄
            {
                // 비어 있거나 후보가 없는 조각은 건너뜀 (null 에러 방지)
                if (r == null || r.alternatives == null || r.alternatives.Length == 0) continue;

                var t = r.alternatives[0].transcript;   // 그 조각의 가장 정확한 후보 문장
                if (string.IsNullOrEmpty(t)) continue;

                if (sb.Length > 0) sb.Append(" ");      // 조각 사이에 공백 하나
                sb.Append(t.Trim());                    // 앞뒤 공백을 지우고 이어 붙임
            }

            return sb.ToString();   // 이어 붙인 전체 글자 반환
        }
    }
}
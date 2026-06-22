using System;
using System.Collections;
using System.IO;
using GoogleTextToSpeech.Scripts.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace GoogleTextToSpeech.Scripts
{
    public class AudioConverter : MonoBehaviour
    {
        // 고유한 파일명을 저장할 변수 (요청마다 갱신됨)
        private static string currentMp3FileName = "audio.mp3";

        // Google TTS로부터 받은 Base64 인코딩된 MP3 데이터를 파일로 저장
        public static void SaveTextToMp3(AudioData audioData)
        {
            // 이전 임시 파일들 정리 (audio_로 시작하는 mp3 파일 전부 삭제)
            try
            {
                var oldFiles = Directory.GetFiles(Application.temporaryCachePath, "audio_*.mp3");
                foreach (var oldFile in oldFiles)
                {
                    File.Delete(oldFile);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("이전 임시 파일 정리 중 오류: " + e.Message);
            }

            // 매번 고유한 파일명 생성 (타임스탬프 기반)
            // 이전 요청 파일과 충돌하지 않음
            currentMp3FileName = "audio_" + DateTime.Now.Ticks + ".mp3";

            var bytes = Convert.FromBase64String(audioData.audioContent);
            File.WriteAllBytes(Application.temporaryCachePath + "/" + currentMp3FileName, bytes);
        }

        // 외부에서 호출하는 함수 - 코루틴 시작
        public void LoadClipFromMp3(Action<AudioClip> onClipLoaded)
        {
            StartCoroutine(LoadClipFromMp3Cor(onClipLoaded));
        }

        // MP3 파일을 읽어서 AudioClip으로 변환하는 코루틴
        private static IEnumerator LoadClipFromMp3Cor(Action<AudioClip> onClipLoaded)
        {
            // 저장된 MP3 파일 경로
            string filePath = Application.temporaryCachePath + "/" + currentMp3FileName;

            // 파일이 존재하는지 먼저 확인
            if (!File.Exists(filePath))
            {
                Debug.LogError("MP3 파일이 존재하지 않습니다: " + filePath);
                yield break;
            }

            // UnityWebRequest로 MP3 파일을 오디오 클립으로 로드
            // AudioType.MPEG = MP3 형식 명시
            string url = "file:///" + filePath.Replace("\\", "/");

            using (UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
            {
                // 스트리밍 방식으로 로드 (Unity 2022에서 MP3 로드 안정성 향상)
                ((DownloadHandlerAudioClip)webRequest.downloadHandler).streamAudio = false;

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    // 로드 성공 시 AudioClip 반환
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(webRequest);

                    if (clip != null && clip.length > 0)
                    {
                        Debug.Log("TTS 오디오 로드 성공. 길이: " + clip.length + "초");
                        onClipLoaded.Invoke(clip);
                    }
                    else
                    {
                        Debug.LogError("AudioClip이 비어있습니다.");
                    }
                }
                else
                {
                    Debug.LogError("MP3 로드 실패: " + webRequest.error);
                }
            }
        }
    }
}
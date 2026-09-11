using System.Collections.Generic;
using UnityEngine;

namespace HJS
{
    public class FillerWordDetector : SingletonBase<FillerWordDetector>
    {
        [Header("감지할 말버릇 목록 (Inspector에서 수정 가능)")]
        public List<string> fillerWords = new List<string>
        {
            "어", "음", "아",
            "저기", "그",
            "이제", "뭐", "좀", "일단",
            "약간", "그냥", "막", "되게", "뭔가",
            "진짜", "솔직히"
        };

        // 말버릇별 감지 횟수
        private Dictionary<string, int> fillerCount = new Dictionary<string, int>();

        // 총 말버릇 감지 횟수
        private int totalFillerCount = 0;

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("<color=#81C784>[FillerWordDetector] 초기화 완료</color>");
        }

        private void OnEnable()
        {
            InterviewManager.OnTranscriptReceived += Analyze;
        }

        private void OnDisable()
        {
            InterviewManager.OnTranscriptReceived -= Analyze;
        }

        public void Analyze(string sttText)
        {
            if (string.IsNullOrEmpty(sttText)) return;

            // STT 실제 수신 텍스트 확인용 로그
            Debug.Log($"<color=#81C784>[FillerWordDetector] STT 수신 텍스트: '{sttText}'</color>");

            foreach (string word in fillerWords)
            {
                int count = CountOccurrences(sttText, word);

                if (count > 0)
                {
                    if (fillerCount.ContainsKey(word))
                        fillerCount[word] += count;
                    else
                        fillerCount[word] = count;

                    totalFillerCount += count;

                    Debug.Log($"<color=#81C784>[FillerWordDetector] '{word}' → {count}회 (누적: {fillerCount[word]}회)</color>");
                }
            }
        }

        private int CountOccurrences(string text, string word)
        {
            int count = 0;
            int index = 0;

            while ((index = text.IndexOf(word, index)) != -1)
            {
                bool isWordStart = index == 0 || text[index - 1] == ' ';
                bool isWordEnd = index + word.Length == text.Length
                                 || text[index + word.Length] == ' ';

                if (isWordStart && isWordEnd)
                    count++;

                index += word.Length;
            }

            return count;
        }

        public Dictionary<string, int> GetFillerCount()
        {
            return fillerCount;
        }

        public int GetTotalFillerCount()
        {
            return totalFillerCount;
        }

        public void Reset()
        {
            fillerCount.Clear();
            totalFillerCount = 0;
            Debug.Log("<color=#81C784>[FillerWordDetector] 카운트 초기화 완료</color>");
        }
    }
}
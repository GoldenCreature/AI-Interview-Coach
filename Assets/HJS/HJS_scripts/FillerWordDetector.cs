using System.Collections.Generic;
using UnityEngine;

namespace HJS
{
    public class FillerWordDetector : SingletonBase<FillerWordDetector>
    {
        [Header("감지할 말버릇 목록 (Inspector에서 수정 가능)")]
        public List<string> fillerWords = new List<string>
        {
            "어", "음", "아", "이제", "그게", "그러니까",
            "사실", "뭐", "좀", "일단", "약간", "그냥",
            "진짜", "솔직히", "어떻게 보면"
        };

        // 말버릇별 감지 횟수
        private Dictionary<string, int> fillerCount = new Dictionary<string, int>();

        // 총 말버릇 감지 횟수
        private int totalFillerCount = 0;

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("[FillerWordDetector] 초기화 완료");
        }

        private void OnEnable()
        {
            // STT 결과 이벤트 구독
            // SpeechToTextManager가 이벤트 발생시키면 자동으로 Analyze() 실행
            InterviewManager.OnTranscriptReceived += Analyze;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            InterviewManager.OnTranscriptReceived -= Analyze;
        }

        // -----------------------------------------------
        // STT 결과 텍스트를 받아서 말버릇 분석
        // 이제 직접 호출 불필요 → 이벤트로 자동 실행됨
        // -----------------------------------------------
        public void Analyze(string sttText)
        {
            if (string.IsNullOrEmpty(sttText)) return;

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

                    Debug.Log($"[FillerWordDetector] '{word}' → {count}회 (누적: {fillerCount[word]}회)");
                }
            }
        }

        // -----------------------------------------------
        // 텍스트 안에서 특정 단어가 몇 번 나오는지 세는 함수
        // -----------------------------------------------
        private int CountOccurrences(string text, string word)
        {
            int count = 0;
            int index = 0;

            while ((index = text.IndexOf(word, index)) != -1)
            {
                // 앞뒤가 공백이거나 문장 경계일 때만 카운트
                // 예: "아이가" 에서 "아" → 뒤에 공백 없음 → 카운트 안 됨
                // 예: "어 그러니까" 에서 "어" → 뒤에 공백 있음 → 카운트 됨
                bool isWordStart = index == 0 || text[index - 1] == ' ';
                bool isWordEnd = index + word.Length == text.Length
                                 || text[index + word.Length] == ' ';

                if (isWordStart && isWordEnd)
                {
                    count++;
                }

                index += word.Length;
            }

            return count;
        }

        // -----------------------------------------------
        // 외부에서 꺼내 쓰는 함수들
        // DBManager, GeminiManager에서 사용
        // -----------------------------------------------
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
            Debug.Log("[FillerWordDetector] 카운트 초기화 완료");
        }
    }
}
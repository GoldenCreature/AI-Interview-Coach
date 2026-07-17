using System.Collections.Generic;
using UnityEngine;

namespace HJS 
{ 

    public class FillerWordDetector : MonoBehaviour
    {
        [Header("감지할 말버릇 목록 (Inspector에서 수정 가능)")]
        // Inspector에서 직접 추가/삭제 가능
        public List<string> fillerWords = new List<string>
        {
            "어", "음", "아", "이제", "그게", "그러니까",
            "사실", "뭐", "좀", "일단", "약간", "그냥",
            "진짜", "솔직히", "어떻게 보면"
        };

        // 말버릇별 감지 횟수를 저장하는 딕셔너리
        // 예: { "어": 3, "그냥": 2 }
        private Dictionary<string, int> fillerCount = new Dictionary<string, int>();

        // 총 말버릇 감지 횟수
        private int totalFillerCount = 0;

        // -----------------------------------------------
        // 외부에서 호출하는 함수
        // STT 결과 텍스트를 받아서 말버릇을 분석함
        // 호출 위치: SpeechToTextManager.cs의 STT 결과 받은 직후
        // -----------------------------------------------
        public void Analyze(string sttText)
        {
            if (string.IsNullOrEmpty(sttText)) return;

            foreach (string word in fillerWords)
            {
                // 텍스트 안에 말버릇 단어가 몇 번 등장하는지 카운트
                int count = CountOccurrences(sttText, word);

                if (count > 0)
                {
                    // 이미 카운트된 적 있는 단어면 누적, 처음이면 새로 추가
                    if (fillerCount.ContainsKey(word))
                        fillerCount[word] += count;
                    else
                        fillerCount[word] = count;

                    totalFillerCount += count;

                    Debug.Log($"[말버릇 감지] '{word}' → {count}회 (누적: {fillerCount[word]}회)");
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
                count++;
                index += word.Length;
            }

            return count;
        }

        // -----------------------------------------------
        // 결과 화면에서 꺼내 쓸 수 있는 접근 함수들
        // -----------------------------------------------

        // 말버릇별 횟수 전체 반환 (결과 화면, DB 저장용)
        public Dictionary<string, int> GetFillerCount()
        {
            return fillerCount;
        }

        // 총 말버릇 횟수 반환
        public int GetTotalFillerCount()
        {
            return totalFillerCount;
        }

        // 면접 다시 시작할 때 초기화
        public void Reset()
        {
            fillerCount.Clear();
            totalFillerCount = 0;
            Debug.Log("[말버릇 감지] 카운트 초기화 완료");
        }
    }
}
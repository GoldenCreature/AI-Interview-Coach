using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using GoogleTextToSpeech.Scripts;
using GoogleTextToSpeech.Scripts.Data;

// Gemini API 데이터 구조
[System.Serializable]
public class UnityAndGeminiKey { public string key; }

[System.Serializable]
public class Response { public Candidate[] candidates; }

public class ChatRequest { public Content[] contents; }

[System.Serializable]
public class Candidate { public Content content; }

[System.Serializable]
public class Content
{
    public string role;
    public Part[] parts;
}

[System.Serializable]
public class Part { public string text; }

namespace HJS
{
    public class UnityAndGeminiV3 : SingletonBase<UnityAndGeminiV3>
    {
        [Header("Gemini API 키")]
        // 절대 커밋하지 말 것
        public string apiKey;

        // 사용할 Gemini 모델 API 주소
        private string apiEndpoint =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        // 전체 대화 기록
        // Gemini는 대화 맥락을 기억 못하기 때문에
        // 매번 API 요청 시 이 배열 전체를 같이 보냄
        // public이라 외부에서 대화기록 접근 가능
        public Content[] chatHistory;

        protected override void Awake()
        {
            base.Awake();
            chatHistory = new Content[0]; // 빈 배열로 초기화
            Debug.Log("[GeminiManager] 초기화 완료");
        }

        private void OnEnable()
        {
            // 면접 시작 이벤트 구독 → 직종 프롬프트 자동 주입
            InterviewManager.OnInterviewStarted += HandleInterviewStarted;

            // STT 결과 이벤트 구독 → Gemini 전송 자동 실행
            InterviewManager.OnTranscriptReceived += HandleTranscriptReceived;

            // 면접 종료 이벤트 구독 → 종합 평가 자동 시작
            InterviewManager.OnInterviewEnded += HandleInterviewEnded;
        }

        private void OnDisable()
        {
            InterviewManager.OnInterviewStarted -= HandleInterviewStarted;
            InterviewManager.OnTranscriptReceived -= HandleTranscriptReceived;
            InterviewManager.OnInterviewEnded -= HandleInterviewEnded;
        }

        // -----------------------------------------------
        // 이벤트 핸들러
        // -----------------------------------------------

        // 면접 시작 시 직종에 맞는 프롬프트 주입
        private void HandleInterviewStarted(JobCategory job)
        {
            string prompt = GetPromptByJob(job);

            Content systemContent = new Content
            {
                role = "user",
                parts = new Part[] { new Part { text = prompt } }
            };

            Content systemAck = new Content
            {
                role = "model",
                parts = new Part[] { new Part { text = "네, 면접관 역할을 시작하겠습니다." } }
            };

            chatHistory = new Content[] { systemContent, systemAck };

            Debug.Log($"[GeminiManager] 직종 프롬프트 주입 완료: {job}");

            // 첫 질문 시작
            StartCoroutine(SendChatRequestToGemini("면접을 시작해주세요."));
        }

        // STT 결과 수신 시 Gemini로 전송
        private void HandleTranscriptReceived(string transcript)
        {
            StartCoroutine(SendChatRequestToGemini(transcript));
        }

        // 면접 종료 시 종합 평가 요청
        private void HandleInterviewEnded(InterviewResultData resultData)
        {
            StartCoroutine(SendEvaluationRequest(resultData));
        }

        // -----------------------------------------------
        // 직종별 프롬프트
        // -----------------------------------------------
        private string GetPromptByJob(JobCategory job)
        {
            switch (job)
            {
                case JobCategory.IT개발자:
                    return @"당신은 IT 기업의 신입 개발자 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 지원자가 답변하면 논리적 허점을 찾아 꼬리 질문을 1개만 하세요.
3. 친절하지 않고 엄격하고 진중한 어조를 유지하세요.
4. 한 번에 한 가지 질문만 하세요.
5. 첫 시작은 자기소개를 요청하세요.
6. 지원자가 같은 질문에 3번 이상 명확한 답변을 못하면 다음 질문으로 넘어가세요.";

                case JobCategory.마케팅:
                    return @"당신은 대기업 마케팅 부서의 신입 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 지원자의 창의성과 트렌드 감각을 평가하는 질문을 하세요.
3. 지원자가 답변하면 구체적인 사례나 근거를 요구하는 꼬리 질문을 1개만 하세요.
4. 친절하지 않고 엄격하고 진중한 어조를 유지하세요.
5. 한 번에 한 가지 질문만 하세요.
6. 첫 시작은 자기소개를 요청하세요.
7. 지원자가 같은 질문에 3번 이상 명확한 답변을 못하면 다음 질문으로 넘어가세요.";

                case JobCategory.디자인:
                    return @"당신은 디자인 회사의 신입 디자이너 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 지원자의 디자인 철학과 감각을 평가하는 질문을 하세요.
3. 지원자가 답변하면 포트폴리오나 구체적인 작업 경험을 묻는 꼬리 질문을 1개만 하세요.
4. 친절하지 않고 엄격하고 진중한 어조를 유지하세요.
5. 한 번에 한 가지 질문만 하세요.
6. 첫 시작은 자기소개를 요청하세요.
7. 지원자가 같은 질문에 3번 이상 명확한 답변을 못하면 다음 질문으로 넘어가세요.";

                case JobCategory.영업:
                    return @"당신은 영업 회사의 신입 영업직 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 지원자의 커뮤니케이션 능력과 목표 달성 의지를 평가하는 질문을 하세요.
3. 지원자가 답변하면 실제 상황에서 어떻게 행동할지 묻는 꼬리 질문을 1개만 하세요.
4. 친절하지 않고 엄격하고 진중한 어조를 유지하세요.
5. 한 번에 한 가지 질문만 하세요.
6. 첫 시작은 자기소개를 요청하세요.
7. 지원자가 같은 질문에 3번 이상 명확한 답변을 못하면 다음 질문으로 넘어가세요.";

                case JobCategory.금융:
                    return @"당신은 금융권 신입 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 지원자의 수리 능력과 금융 지식, 윤리 의식을 평가하는 질문을 하세요.
3. 지원자가 답변하면 논리적 근거를 요구하는 꼬리 질문을 1개만 하세요.
4. 친절하지 않고 엄격하고 진중한 어조를 유지하세요.
5. 한 번에 한 가지 질문만 하세요.
6. 첫 시작은 자기소개를 요청하세요.
7. 지원자가 같은 질문에 3번 이상 명확한 답변을 못하면 다음 질문으로 넘어가세요.";

                default:
                    return @"당신은 신입 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 지원자가 답변하면 꼬리 질문을 1개만 하세요.
3. 엄격하고 진중한 어조를 유지하세요.
4. 한 번에 한 가지 질문만 하세요.
5. 첫 시작은 자기소개를 요청하세요.
6. 지원자가 같은 질문에 3번 이상 명확한 답변을 못하면 다음 질문으로 넘어가세요.";
            }
        }

        // -----------------------------------------------
        // Gemini API 통신
        // -----------------------------------------------
        private IEnumerator SendChatRequestToGemini(string newMessage)
        {
            string url = $"{apiEndpoint}?key={apiKey}";

            Content userContent = new Content
            {
                role = "user",
                parts = new Part[] { new Part { text = newMessage } }
            };

            List<Content> contentsList = new List<Content>(chatHistory);
            contentsList.Add(userContent);
            chatHistory = contentsList.ToArray();

            ChatRequest chatRequest = new ChatRequest { contents = chatHistory };
            string jsonData = JsonUtility.ToJson(chatRequest);
            byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(jsonToSend);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[GeminiManager] 요청 실패: {www.error}");
                }
                else
                {
                    Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);

                    if (response.candidates.Length > 0 &&
                        response.candidates[0].content.parts.Length > 0)
                    {
                        string reply = response.candidates[0].content.parts[0].text;

                        Content botContent = new Content
                        {
                            role = "model",
                            parts = new Part[] { new Part { text = reply } }
                        };

                        contentsList.Add(botContent);
                        chatHistory = contentsList.ToArray();

                        Debug.Log($"[GeminiManager] 응답: {reply}");

                        // 직접 TTS 호출 대신 이벤트 발생
                        // TextToSpeechManager가 자동으로 반응함
                        InterviewManager.NotifyGeminiResponseReceived(reply);
                    }
                    else
                    {
                        Debug.Log("[GeminiManager] 응답 텍스트 없음.");
                    }
                }
            }
        }

        // -----------------------------------------------
        // 종합 평가 요청
        // 면접 종료 이벤트 수신 시 자동 실행
        // -----------------------------------------------
        private IEnumerator SendEvaluationRequest(InterviewResultData resultData)
        {
            string url = $"{apiEndpoint}?key={apiKey}";

            // 말버릇 카운트 텍스트 만들기
            string fillerSummary = "없음";
            if (FillerWordDetector.Instance != null)
            {
                var counts = FillerWordDetector.Instance.GetFillerCount();
                if (counts.Count > 0)
                {
                    fillerSummary = "";
                    foreach (var pair in counts)
                        fillerSummary += $"'{pair.Key}' {pair.Value}회, ";
                    fillerSummary = fillerSummary.TrimEnd(',', ' ');
                }
            }

            string evaluationPrompt =
                $"지금까지의 면접이 모두 끝났습니다.\n" +
                $"아래 기준으로 지원자를 종합 평가해주세요.\n\n" +
                $"[말버릇 감지 결과]\n{fillerSummary}\n\n" +
                $"[태도 점수]\n{resultData.AttitudeScore}점 (MediaPipe 측정)\n\n" +
                $"[평가 항목]\n" +
                $"1. 답변 내용의 충실도 (30점)\n" +
                $"2. 논리적 구성력 (30점)\n" +
                $"3. 의사소통 능력 (20점)\n" +
                $"4. 말버릇 및 화법 (20점)\n\n" +
                $"[출력 형식]\n" +
                $"총점: XX점\n" +
                $"항목별 점수: 각 항목 점수 요약\n" +
                $"강점: (2~3가지)\n" +
                $"개선점: (2~3가지)\n" +
                $"한 줄 총평: (한 문장 요약)";

            Content evaluationRequest = new Content
            {
                role = "user",
                parts = new Part[] { new Part { text = evaluationPrompt } }
            };

            List<Content> contentsList = new List<Content>(chatHistory);
            contentsList.Add(evaluationRequest);

            ChatRequest chatRequest = new ChatRequest { contents = contentsList.ToArray() };
            string jsonData = JsonUtility.ToJson(chatRequest);
            byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(jsonToSend);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[GeminiManager] 종합 평가 요청 실패: {www.error}");
                }
                else
                {
                    Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);

                    if (response.candidates.Length > 0 &&
                        response.candidates[0].content.parts.Length > 0)
                    {
                        string evaluationResult = response.candidates[0].content.parts[0].text;
                        Debug.Log("=== 종합 평가 결과 ===\n" + evaluationResult);

                        // TODO: UIManager.Instance.ShowResult(evaluationResult) 연결 예정
                    }
                    else
                    {
                        Debug.Log("[GeminiManager] 종합 평가 응답 텍스트 없음.");
                    }
                }
            }
        }
    }
}
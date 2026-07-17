using GoogleTextToSpeech.Scripts;
using GoogleTextToSpeech.Scripts.Data;
using HJS;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// Gemini API 키를 담는 데이터 구조
// 현재는 직접 사용하지 않지만 나중에 키를 JSON으로 관리할 때 쓸 수 있음
[System.Serializable]
public class UnityAndGeminiKey
{
    public string key;
}

// Gemini API 응답 전체를 담는 클래스
// API가 응답을 보낼 때 candidates 배열 안에 내용이 들어옴
[System.Serializable]
public class Response
{
    public Candidate[] candidates;
}

// Gemini에게 보내는 요청 데이터 구조
// contents 배열 안에 대화 기록 전체를 담아서 보냄
public class ChatRequest
{
    public Content[] contents;
}

// 대화 한 턴의 응답자 정보를 담는 클래스
// candidates 배열의 각 항목이 이 구조로 이루어져 있음
[System.Serializable]
public class Candidate
{
    public Content content;
}

// 대화 한 줄을 표현하는 클래스
// role : 누가 말했는지 ("user" = 사용자, "model" = AI)
// parts : 실제 텍스트 내용 배열
[System.Serializable]
public class Content
{
    public string role;
    public Part[] parts;
}

// 실제 텍스트를 담는 가장 작은 단위
// Gemini API는 텍스트를 Part 단위로 주고받음
[System.Serializable]
public class Part
{
    public string text;
}

// 면접 직종 선택용 열거형
// Inspector에서 드롭다운으로 자동 표시됨
// 나중에 UI 버튼 방식으로 전환할 때도 이 enum을 그대로 활용 가능
public enum JobCategory
{
    IT개발자,
    마케팅,
    디자인,
    영업,
    금융
}

public class UnityAndGeminiV3 : MonoBehaviour
{
    [Header("Gemini API 키")]
    // Inspector에서 직접 입력하는 Gemini API 키
    // 절대 커밋하지 말 것
    public string apiKey;

    // 사용할 Gemini 모델의 API 주소
    // 모델을 바꾸고 싶으면 URL 안의 모델명 부분만 수정하면 됨
    private string apiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

    [Header("연결된 컴포넌트")]
    // TTS 기능을 담당하는 스크립트
    // Inspector에서 GoogleServices 오브젝트를 드래그해서 연결
    [SerializeField] private TextToSpeechManager googleServices;

    // 지금까지 나눈 대화 전체를 저장하는 배열
    // Gemini는 대화 맥락을 기억 못하기 때문에
    // 매번 API 요청 시 이 배열 전체를 같이 보내야 함
    // public이라 외부에서 대화기록 접근 가능
    public Content[] chatHistory;

    [Header("면접 직종 선택")]
    // Inspector에서 드롭다운으로 직종 선택
    // 선택된 직종에 맞는 프롬프트가 Start()에서 자동 주입됨
    // 나중에 UI 버튼으로 교체 예정
    public JobCategory selectedJob = JobCategory.IT개발자;

    [Header("말버릇 감지기 연결")]
    // Inspector에서 GoogleServices 프리팹의 FillerWordDetector를 드래그 연결
    public FillerWordDetector fillerDetector;

    void Start()
    {
        // 선택된 직종에 맞는 프롬프트 가져오기
        string prompt = GetPromptByJob(selectedJob);

        // 게임 시작 시 면접관 역할을 AI에게 사전 주입
        // Gemini는 시스템 프롬프트를 직접 지원하지 않기 때문에
        // 가짜 대화 한 턴을 미리 만들어서 chatHistory에 넣는 방식으로 처리
        // "사용자가 면접관 역할을 요청했고, AI가 수락했다"는 대화가 이미 있었던 것처럼 설정

        // 사용자가 면접관 역할을 요청하는 가짜 메시지
        Content systemContent = new Content
        {
            role = "user",
            parts = new Part[]
            {
                new Part { text = prompt }
            }
        };

        // AI가 면접관 역할을 수락하는 가짜 응답
        Content systemAck = new Content
        {
            role = "model",
            parts = new Part[]
            {
                new Part { text = "네, 면접관 역할을 시작하겠습니다." }
            }
        };

        // 위 두 가짜 대화를 대화 기록의 시작점으로 설정
        chatHistory = new Content[] { systemContent, systemAck };

        Debug.Log($"면접 직종 설정 완료: {selectedJob}");
    }

    // 선택된 직종에 맞는 프롬프트를 반환하는 함수
    // 직종별로 면접관 페르소나와 평가 기준이 다르게 설정됨
    // 나중에 프롬프트 내용을 더 구체화하거나 직종을 추가할 때 이 함수만 수정하면 됨
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

    // 단순 프롬프트 전송 함수 (대화 기록 없이 단발성 질문)
    // 현재는 사용하지 않음. 나중에 필요할 때를 대비해 남겨둠
    private IEnumerator SendPromptRequestToGemini(string promptText)
    {
        string url = $"{apiEndpoint}?key={apiKey}";

        string jsonData = "{\"contents\": [{\"parts\": [{\"text\": \"{" + promptText + "}\"}]}]}";

        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(www.error);
            }
            else
            {
                Debug.Log("요청 완료!");
                Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);
                if (response.candidates.Length > 0 && response.candidates[0].content.parts.Length > 0)
                {
                    string text = response.candidates[0].content.parts[0].text;
                    Debug.Log(text);
                }
                else
                {
                    Debug.Log("응답 텍스트 없음.");
                }
            }
        }
    }

    // 외부에서 호출하는 채팅 전송 함수
    // 버튼 클릭 또는 STT 결과를 받아서 이 함수를 호출하면 됨
    public void SendChat(string userMessage)
    {
        StartCoroutine(SendChatRequestToGemini(userMessage));
    }

    // 실제 대화 기록을 포함해서 Gemini에 요청하는 핵심 함수
    private IEnumerator SendChatRequestToGemini(string newMessage)
    {
        string url = $"{apiEndpoint}?key={apiKey}";

        // 사용자 메시지를 Content 형태로 만듦
        Content userContent = new Content
        {
            role = "user",
            parts = new Part[]
            {
                new Part { text = newMessage }
            }
        };

        // 기존 대화 기록에 새 메시지 추가
        List<Content> contentsList = new List<Content>(chatHistory);
        contentsList.Add(userContent);
        chatHistory = contentsList.ToArray();

        // 대화 기록 전체를 요청 데이터로 만들어서 JSON으로 변환
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
                Debug.LogError(www.error);
            }
            else
            {
                Debug.Log("요청 완료!");
                Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);

                if (response.candidates.Length > 0 && response.candidates[0].content.parts.Length > 0)
                {
                    // AI 응답 텍스트 추출
                    string reply = response.candidates[0].content.parts[0].text;

                    // AI 응답을 Content 형태로 만들어서 대화 기록에 추가
                    Content botContent = new Content
                    {
                        role = "model",
                        parts = new Part[]
                        {
                            new Part { text = reply }
                        }
                    };

                    Debug.Log(reply);

                    // AI 응답을 TTS로 전달해서 음성으로 출력
                    googleServices.SendTextToGoogle(reply);

                    // 대화 기록 업데이트 (다음 요청 때 이 내용도 같이 전송됨)
                    contentsList.Add(botContent);
                    chatHistory = contentsList.ToArray();
                }
                else
                {
                    Debug.Log("응답 텍스트 없음.");
                }
            }
        }
    }

    // 면접 종료 버튼에 연결할 함수
    // 전체 대화 기록 + 말버릇 카운트를 Gemini에 보내서 종합 평가 요청
    public void EndInterview()
    {
        StartCoroutine(SendEvaluationRequest());
    }

    private IEnumerator SendEvaluationRequest()
    {
        string url = $"{apiEndpoint}?key={apiKey}";

        // 말버릇 카운트 텍스트 만들기
        string fillerSummary = "없음";
        if (fillerDetector != null)
        {
            var counts = fillerDetector.GetFillerCount();
            if (counts.Count > 0)
            {
                fillerSummary = "";
                foreach (var pair in counts)
                {
                    fillerSummary += $"'{pair.Key}' {pair.Value}회, ";
                }
                // 마지막 ", " 제거
                fillerSummary = fillerSummary.TrimEnd(',', ' ');
            }
        }

        // Gemini에게 보낼 종합 평가 요청 메시지
        string evaluationPrompt =
            $"지금까지의 면접이 모두 끝났습니다.\n" +
            $"아래 기준으로 지원자를 종합 평가해주세요.\n\n" +
            $"[말버릇 감지 결과]\n{fillerSummary}\n\n" +
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
            parts = new Part[]
            {
                new Part { text = evaluationPrompt }
            }
        };

        // 기존 대화 기록에 평가 요청 추가
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
                Debug.LogError("종합 평가 요청 실패: " + www.error);
            }
            else
            {
                Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);

                if (response.candidates.Length > 0 && response.candidates[0].content.parts.Length > 0)
                {
                    string evaluationResult = response.candidates[0].content.parts[0].text;
                    Debug.Log("=== 종합 평가 결과 ===\n" + evaluationResult);

                    // TODO: 나중에 결과 화면 UI로 전달하는 코드 여기에 추가
                    // 예: UIManager.Instance.ShowResult(evaluationResult);
                }
                else
                {
                    Debug.Log("종합 평가 응답 텍스트 없음.");
                }
            }
        }
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using GoogleTextToSpeech.Scripts.Data;
using GoogleTextToSpeech.Scripts;

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
    private Content[] chatHistory;


    // 임시 면접관 프롬프트 - 추후 직종별 프롬프트로 교체 예정
    [Header("면접관 시스템 프롬프트 (임시)")]
    [TextArea(5, 10)]
    public string systemPrompt = @"당신은 IT 기업의 신입 개발자 채용 면접관입니다.
    [규칙]
    1. 반드시 한국어로만 대화하세요.
    2. 지원자가 답변하면 논리적 허점을 찾아 꼬리 질문을 1개만 하세요.
    3. 친절하지 않고 엄격하고 진중한 어조를 유지하세요.
    4. 한 번에 한 가지 질문만 하세요.
    5. 첫 시작은 자기소개를 요청하세요.";


    void Start()
    {
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
                new Part { text = systemPrompt }
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
    }


    // 단순 프롬프트 전송 함수 (대화 기록 없이 단발성 질문)
    // 현재는 사용하지 않음. 나중에 필요할 때를 대비해 남겨둠
    private IEnumerator SendPromptRequestToGemini(string promptText)
    {
        string url = $"{apiEndpoint}?key={apiKey}";

        // 단순 JSON 형태로 텍스트만 전송
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
}
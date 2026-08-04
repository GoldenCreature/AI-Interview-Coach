using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using GoogleTextToSpeech.Scripts;
using GoogleTextToSpeech.Scripts.Data;

// Gemini API 키를 담는 데이터 구조
// 현재는 직접 사용하지 않지만 나중에 키를 JSON으로 관리할 때 쓸 수 있음
[System.Serializable]
public class UnityAndGeminiKey { public string key; }

// Gemini API 응답 전체를 담는 클래스
// API가 응답을 보낼 때 candidates 배열 안에 내용이 들어옴
[System.Serializable]
public class Response { public Candidate[] candidates; }

// Gemini에게 보내는 요청 데이터 구조
// contents 배열 안에 대화 기록 전체를 담아서 보냄
public class ChatRequest { public Content[] contents; }

// 대화 한 턴의 응답자 정보를 담는 클래스
[System.Serializable]
public class Candidate { public Content content; }

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
public class Part { public string text; }

namespace HJS
{
    public class UnityAndGeminiV3 : SingletonBase<UnityAndGeminiV3>
    {
        [Header("Gemini API 키")]
        // Inspector에서 직접 입력하는 Gemini API 키
        // 절대 커밋하지 말 것
        public string apiKey;

        // 사용할 Gemini 모델의 API 주소
        // 모델을 바꾸고 싶으면 URL 안의 모델명 부분만 수정하면 됨
        private string apiEndpoint =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        // 지금까지 나눈 대화 전체를 저장하는 배열
        // Gemini는 대화 맥락을 기억 못하기 때문에
        // 매번 API 요청 시 이 배열 전체를 같이 보내야 함
        // public이라 외부에서 대화기록 접근 가능
        public Content[] chatHistory;

        // 현재 진행 중인 질문 번호 추적
        // Gemini에게 몇 번째 질문에 대한 답변인지 알려주기 위해 사용
        private int _currentQuestionNumber = 0;

        protected override void Awake()
        {
            base.Awake();

            // chatHistory 빈 배열로 초기화
            // null 상태에서 접근하면 오류 발생하기 때문에 미리 초기화
            chatHistory = new Content[0];

            // API 키 누락 경고
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[GeminiManager] API 키가 비어있습니다! Inspector에서 입력해주세요.");
            }

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
        private void HandleInterviewStarted(JobCategory job, InterviewerType type)
        {
            string prompt = GetPromptByJobAndType(job, type);

            // Gemini는 시스템 프롬프트를 직접 지원하지 않기 때문에
            // 가짜 대화 한 턴을 미리 만들어서 chatHistory에 넣는 방식으로 처리
            // "사용자가 면접관 역할을 요청했고, AI가 수락했다"는 대화가 이미 있었던 것처럼 설정
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
            _currentQuestionNumber = 0; // 면접 시작 시 초기화
            Debug.Log($"[GeminiManager] 직종 프롬프트 주입 완료: {job}");

            // 첫 질문 시작
            StartCoroutine(SendChatRequestToGemini("면접을 시작해주세요."));
        }

        // STT 결과 수신 시 Gemini로 전송
        private void HandleTranscriptReceived(string transcript)
        {
            // 답변을 받을 때마다 질문 번호 증가
            _currentQuestionNumber++;

            // Gemini에게 현재 몇 번째 질문에 대한 답변인지 명시
            // 이를 통해 Gemini가 진행 순서를 인식하고 다음 단계로 넘어갈 수 있음
            string taggedTranscript =
                $"[현재 {_currentQuestionNumber}번 질문에 대한 답변]\n{transcript}";

            Debug.Log($"[GeminiManager] {_currentQuestionNumber}번 질문 답변 전송");
            StartCoroutine(SendChatRequestToGemini(taggedTranscript));
        }

        // 면접 종료 시 종합 평가 요청
        private void HandleInterviewEnded(InterviewResultData resultData)
        {
            StartCoroutine(SendEvaluationRequest(resultData));
        }

        // -----------------------------------------------
        // 직종별 프롬프트
        // 직종별로 면접관 페르소나와 평가 기준이 다르게 설정됨
        // 나중에 프롬프트 내용을 더 구체화하거나 직종을 추가할 때 이 함수만 수정하면 됨
        // -----------------------------------------------
        private string GetPromptByJobAndType(JobCategory job, InterviewerType type)
        {
            switch (job)
            {
                case JobCategory.IT개발자:
                    if (type == InterviewerType.Intensive)
                    {
                        return @"당신은 IT 기업의 신입 개발자 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 지원자가 답변하면 논리적 허점을 찾아 꼬리 질문을 1개만 하세요.
3. 친절하지 않고 엄격하고 진중한 어조를 유지하세요.
4. 한 번에 한 가지 질문만 하세요.
5. 같은 주제에 대해 최대 1회 꼬리 질문 후 반드시 다음 단계로 넘어가세요.
6. 지원자가 명확한 답변을 못하면 바로 다음 단계로 넘어가세요.
7. 아래 순서대로 정확히 10개의 질문만 진행하세요.

[면접 진행 순서 - 총 10문항]
1번: 자기소개
2번: 자기소개 꼬리 질문
3번: 지원 동기
4번: 지원 동기 꼬리 질문
5번: 프로젝트 또는 개발 경험
6번: 프로젝트 또는 개발 경험 꼬리 질문
7번: 기술 역량 (언어, 프레임워크 등)
8번: 기술 역량 꼬리 질문
9번: 입사 후 목표
10번: 마지막으로 하고 싶은 말

8. 10번 질문에 대한 답변을 받은 후 면접 마무리 멘트를 하고
   마지막 메시지 끝에 반드시 [면접종료] 태그를 출력하세요.";
                    }
                    else // Casual
                    {
                        return @"당신은 IT 기업의 신입 개발자 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 친근하고 부드러운 어조로 지원자가 편하게 말할 수 있는 분위기를 만드세요.
3. 지원자의 답변에 공감하며 자연스럽게 다음 질문으로 이어가세요.
4. 한 번에 한 가지 질문만 하세요.
5. 같은 주제에 대해 최대 1회 추가 질문 후 반드시 다음 단계로 넘어가세요.
6. 지원자가 답변하기 어려워하면 힌트를 주거나 다음 단계로 넘어가세요.
7. 아래 순서대로 정확히 10개의 질문만 진행하세요.

[면접 진행 순서 - 총 10문항]
1번: 자기소개
2번: 자기소개 관련 편안한 추가 질문
3번: 지원 동기
4번: 지원 동기 관련 편안한 추가 질문
5번: 프로젝트 또는 개발 경험
6번: 프로젝트 경험 관련 편안한 추가 질문
7번: 관심 있는 기술 또는 공부하고 있는 것
8번: 기술 관련 편안한 추가 질문
9번: 입사 후 목표
10번: 마지막으로 하고 싶은 말

8. 10번 질문에 대한 답변을 받은 후 따뜻하게 면접 마무리 멘트를 하고
   마지막 메시지 끝에 반드시 [면접종료] 태그를 출력하세요.";
                    }

                case JobCategory.마케팅:
                    if (type == InterviewerType.Intensive)
                    {
                        return @"당신은 대기업 마케팅 부서의 신입 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 지원자의 창의성과 트렌드 감각을 평가하는 질문을 하세요.
3. 지원자가 답변하면 구체적인 사례나 근거를 요구하는 꼬리 질문을 1개만 하세요.
4. 친절하지 않고 엄격하고 진중한 어조를 유지하세요.
5. 한 번에 한 가지 질문만 하세요.
6. 같은 주제에 대해 최대 1회 꼬리 질문 후 반드시 다음 단계로 넘어가세요.
7. 지원자가 명확한 답변을 못하면 바로 다음 단계로 넘어가세요.
8. 아래 순서대로 정확히 10개의 질문만 진행하세요.

[면접 진행 순서 - 총 10문항]
1번: 자기소개
2번: 자기소개 꼬리 질문
3번: 지원 동기
4번: 지원 동기 꼬리 질문
5번: 마케팅 관련 경험 또는 프로젝트
6번: 마케팅 경험 꼬리 질문
7번: 트렌드 및 시장 분석 능력
8번: 트렌드 분석 꼬리 질문
9번: 입사 후 목표
10번: 마지막으로 하고 싶은 말

9. 10번 질문에 대한 답변을 받은 후 면접 마무리 멘트를 하고
   마지막 메시지 끝에 반드시 [면접종료] 태그를 출력하세요.";
                    }
                    else // Casual
                    {
                        return @"당신은 대기업 마케팅 부서의 신입 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 친근하고 창의적인 분위기로 지원자가 자유롭게 아이디어를 말할 수 있게 하세요.
3. 지원자의 답변에 흥미를 보이며 자연스럽게 대화를 이어가세요.
4. 한 번에 한 가지 질문만 하세요.
5. 같은 주제에 대해 최대 1회 추가 질문 후 반드시 다음 단계로 넘어가세요.
6. 지원자가 답변하기 어려워하면 편안하게 다음 단계로 넘어가세요.
7. 아래 순서대로 정확히 10개의 질문만 진행하세요.

[면접 진행 순서 - 총 10문항]
1번: 자기소개
2번: 자기소개 관련 편안한 추가 질문
3번: 지원 동기
4번: 지원 동기 관련 편안한 추가 질문
5번: 관심 있는 마케팅 트렌드나 사례
6번: 트렌드 관련 편안한 추가 질문
7번: 본인만의 아이디어나 창의적 경험
8번: 아이디어 관련 편안한 추가 질문
9번: 입사 후 목표
10번: 마지막으로 하고 싶은 말

9. 10번 질문에 대한 답변을 받은 후 따뜻하게 면접 마무리 멘트를 하고
   마지막 메시지 끝에 반드시 [면접종료] 태그를 출력하세요.";
                    }

                case JobCategory.디자인:
                    if (type == InterviewerType.Intensive)
                    {
                        return @"당신은 디자인 회사의 신입 디자이너 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 지원자의 디자인 철학과 감각을 평가하는 질문을 하세요.
3. 지원자가 답변하면 포트폴리오나 구체적인 작업 경험을 묻는 꼬리 질문을 1개만 하세요.
4. 친절하지 않고 엄격하고 진중한 어조를 유지하세요.
5. 한 번에 한 가지 질문만 하세요.
6. 같은 주제에 대해 최대 1회 꼬리 질문 후 반드시 다음 단계로 넘어가세요.
7. 지원자가 명확한 답변을 못하면 바로 다음 단계로 넘어가세요.
8. 아래 순서대로 정확히 10개의 질문만 진행하세요.

[면접 진행 순서 - 총 10문항]
1번: 자기소개
2번: 자기소개 꼬리 질문
3번: 지원 동기
4번: 지원 동기 꼬리 질문
5번: 포트폴리오 또는 디자인 경험
6번: 포트폴리오 꼬리 질문
7번: 디자인 철학 및 툴 역량
8번: 디자인 철학 꼬리 질문
9번: 입사 후 목표
10번: 마지막으로 하고 싶은 말

9. 10번 질문에 대한 답변을 받은 후 면접 마무리 멘트를 하고
   마지막 메시지 끝에 반드시 [면접종료] 태그를 출력하세요.";
                    }
                    else // Casual
                    {
                        return @"당신은 디자인 회사의 신입 디자이너 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 따뜻하고 감성적인 분위기로 지원자의 디자인 감각을 자연스럽게 이끌어내세요.
3. 지원자의 답변에 공감하며 편안하게 대화를 이어가세요.
4. 한 번에 한 가지 질문만 하세요.
5. 같은 주제에 대해 최대 1회 추가 질문 후 반드시 다음 단계로 넘어가세요.
6. 지원자가 답변하기 어려워하면 편안하게 다음 단계로 넘어가세요.
7. 아래 순서대로 정확히 10개의 질문만 진행하세요.

[면접 진행 순서 - 총 10문항]
1번: 자기소개
2번: 자기소개 관련 편안한 추가 질문
3번: 지원 동기
4번: 지원 동기 관련 편안한 추가 질문
5번: 기억에 남는 디자인 작업이나 영감을 받은 경험
6번: 디자인 경험 관련 편안한 추가 질문
7번: 좋아하는 디자인 스타일이나 영향받은 작품
8번: 디자인 철학 관련 편안한 추가 질문
9번: 입사 후 목표
10번: 마지막으로 하고 싶은 말

9. 10번 질문에 대한 답변을 받은 후 따뜻하게 면접 마무리 멘트를 하고
   마지막 메시지 끝에 반드시 [면접종료] 태그를 출력하세요.";
                    }

                case JobCategory.영업:
                    if (type == InterviewerType.Intensive)
                    {
                        return @"당신은 영업 회사의 신입 영업직 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 지원자의 커뮤니케이션 능력과 목표 달성 의지를 평가하는 질문을 하세요.
3. 지원자가 답변하면 실제 상황에서 어떻게 행동할지 묻는 꼬리 질문을 1개만 하세요.
4. 친절하지 않고 엄격하고 진중한 어조를 유지하세요.
5. 한 번에 한 가지 질문만 하세요.
6. 같은 주제에 대해 최대 1회 꼬리 질문 후 반드시 다음 단계로 넘어가세요.
7. 지원자가 명확한 답변을 못하면 바로 다음 단계로 넘어가세요.
8. 아래 순서대로 정확히 10개의 질문만 진행하세요.

[면접 진행 순서 - 총 10문항]
1번: 자기소개
2번: 자기소개 꼬리 질문
3번: 지원 동기
4번: 지원 동기 꼬리 질문
5번: 영업 관련 경험 또는 대인관계 사례
6번: 영업 경험 꼬리 질문
7번: 목표 달성 의지 및 어려움 극복 사례
8번: 극복 사례 꼬리 질문
9번: 입사 후 목표
10번: 마지막으로 하고 싶은 말

9. 10번 질문에 대한 답변을 받은 후 면접 마무리 멘트를 하고
   마지막 메시지 끝에 반드시 [면접종료] 태그를 출력하세요.";
                    }
                    else // Casual
                    {
                        return @"당신은 영업 회사의 신입 영업직 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 활기차고 친근한 분위기로 지원자의 소통 능력을 자연스럽게 이끌어내세요.
3. 지원자의 답변에 긍정적으로 반응하며 편안하게 대화를 이어가세요.
4. 한 번에 한 가지 질문만 하세요.
5. 같은 주제에 대해 최대 1회 추가 질문 후 반드시 다음 단계로 넘어가세요.
6. 지원자가 답변하기 어려워하면 편안하게 다음 단계로 넘어가세요.
7. 아래 순서대로 정확히 10개의 질문만 진행하세요.

[면접 진행 순서 - 총 10문항]
1번: 자기소개
2번: 자기소개 관련 편안한 추가 질문
3번: 지원 동기
4번: 지원 동기 관련 편안한 추가 질문
5번: 사람들과 잘 어울렸던 경험이나 설득했던 경험
6번: 대인관계 경험 관련 편안한 추가 질문
7번: 어려운 상황을 극복했던 경험
8번: 극복 경험 관련 편안한 추가 질문
9번: 입사 후 목표
10번: 마지막으로 하고 싶은 말

9. 10번 질문에 대한 답변을 받은 후 따뜻하게 면접 마무리 멘트를 하고
   마지막 메시지 끝에 반드시 [면접종료] 태그를 출력하세요.";
                    }

                case JobCategory.금융:
                    if (type == InterviewerType.Intensive)
                    {
                        return @"당신은 금융권 신입 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 지원자의 수리 능력과 금융 지식, 윤리 의식을 평가하는 질문을 하세요.
3. 지원자가 답변하면 논리적 근거를 요구하는 꼬리 질문을 1개만 하세요.
4. 친절하지 않고 엄격하고 진중한 어조를 유지하세요.
5. 한 번에 한 가지 질문만 하세요.
6. 같은 주제에 대해 최대 1회 꼬리 질문 후 반드시 다음 단계로 넘어가세요.
7. 지원자가 명확한 답변을 못하면 바로 다음 단계로 넘어가세요.
8. 아래 순서대로 정확히 10개의 질문만 진행하세요.

[면접 진행 순서 - 총 10문항]
1번: 자기소개
2번: 자기소개 꼬리 질문
3번: 지원 동기
4번: 지원 동기 꼬리 질문
5번: 금융 관련 지식 또는 경험
6번: 금융 지식 꼬리 질문
7번: 윤리 의식 및 책임감 관련 질문
8번: 윤리 의식 꼬리 질문
9번: 입사 후 목표
10번: 마지막으로 하고 싶은 말

9. 10번 질문에 대한 답변을 받은 후 면접 마무리 멘트를 하고
   마지막 메시지 끝에 반드시 [면접종료] 태그를 출력하세요.";
                    }
                    else // Casual
                    {
                        return @"당신은 금융권 신입 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 차분하고 신뢰감 있는 분위기로 지원자가 편안하게 답변할 수 있게 하세요.
3. 지원자의 답변에 이해를 표하며 자연스럽게 대화를 이어가세요.
4. 한 번에 한 가지 질문만 하세요.
5. 같은 주제에 대해 최대 1회 추가 질문 후 반드시 다음 단계로 넘어가세요.
6. 지원자가 답변하기 어려워하면 편안하게 다음 단계로 넘어가세요.
7. 아래 순서대로 정확히 10개의 질문만 진행하세요.

[면접 진행 순서 - 총 10문항]
1번: 자기소개
2번: 자기소개 관련 편안한 추가 질문
3번: 지원 동기
4번: 지원 동기 관련 편안한 추가 질문
5번: 금융이나 경제에 관심 갖게 된 계기
6번: 관심 계기 관련 편안한 추가 질문
7번: 꼼꼼함이나 책임감을 발휘했던 경험
8번: 책임감 경험 관련 편안한 추가 질문
9번: 입사 후 목표
10번: 마지막으로 하고 싶은 말

9. 10번 질문에 대한 답변을 받은 후 따뜻하게 면접 마무리 멘트를 하고
   마지막 메시지 끝에 반드시 [면접종료] 태그를 출력하세요.";
                    }

                default:
                    return @"당신은 신입 채용 면접관입니다.
[규칙]
1. 반드시 한국어로만 대화하세요.
2. 지원자가 답변하면 꼬리 질문을 1개만 하세요.
3. 엄격하고 진중한 어조를 유지하세요.
4. 한 번에 한 가지 질문만 하세요.
5. 같은 주제에 대해 최대 1회 꼬리 질문 후 반드시 다음 단계로 넘어가세요.
6. 지원자가 명확한 답변을 못하면 바로 다음 단계로 넘어가세요.
7. 아래 순서대로 정확히 10개의 질문만 진행하세요.

[면접 진행 순서 - 총 10문항]
1번: 자기소개
2번: 자기소개 꼬리 질문
3번: 지원 동기
4번: 지원 동기 꼬리 질문
5번: 관련 경험
6번: 경험 꼬리 질문
7번: 역량 관련 질문
8번: 역량 꼬리 질문
9번: 입사 후 목표
10번: 마지막으로 하고 싶은 말

8. 10번 질문에 대한 답변을 받은 후 면접 마무리 멘트를 하고
   마지막 메시지 끝에 반드시 [면접종료] 태그를 출력하세요.";
            }
        }

        // -----------------------------------------------
        // Gemini API 통신
        // 대화 기록을 포함해서 Gemini에 요청하는 핵심 함수
        // -----------------------------------------------
        private IEnumerator SendChatRequestToGemini(string newMessage)
        {
            string url = $"{apiEndpoint}?key={apiKey}";

            // 사용자 메시지를 Content 형태로 만듦
            Content userContent = new Content
            {
                role = "user",
                parts = new Part[] { new Part { text = newMessage } }
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
                    Debug.LogError($"[GeminiManager] 요청 실패: {www.error}");
                }
                else
                {
                    Response response = JsonUtility.FromJson<Response>(www.downloadHandler.text);

                    // 응답 자체가 null인 경우
                    if (response == null)
                    {
                        Debug.LogError("[GeminiManager] 응답 파싱 실패 (null)");
                        yield break;
                    }

                    // candidates 배열이 없는 경우
                    if (response.candidates == null || response.candidates.Length == 0)
                    {
                        Debug.LogWarning("[GeminiManager] 응답 candidates 없음");
                        yield break;
                    }

                    // parts 배열이 없는 경우
                    if (response.candidates[0].content == null ||
                        response.candidates[0].content.parts == null ||
                        response.candidates[0].content.parts.Length == 0)
                    {
                        Debug.LogWarning("[GeminiManager] 응답 parts 없음");
                        yield break;
                    }

                    string reply = response.candidates[0].content.parts[0].text;

                    // 응답 텍스트가 비어있는 경우
                    if (string.IsNullOrEmpty(reply))
                    {
                        Debug.LogWarning("[GeminiManager] 응답 텍스트 비어있음");
                        yield break;
                    }

                    // AI 응답을 대화 기록에 추가
                    Content botContent = new Content
                    {
                        role = "model",
                        parts = new Part[] { new Part { text = reply } }
                    };

                    contentsList.Add(botContent);
                    chatHistory = contentsList.ToArray();

                    Debug.Log($"[GeminiManager] 응답: {reply}");

                    // -----------------------------------------------
                    // [면접종료] 태그 감지
                    // Gemini가 10번 질문까지 완료하면 [면접종료] 태그를 출력함
                    // 태그를 제거한 뒤 마지막 멘트만 TTS로 출력하고
                    // 일정 시간 후 면접 종료 처리
                    // -----------------------------------------------
                    if (reply.Contains("[면접종료]"))
                    {
                        // 태그 제거 후 TTS 출력
                        string cleanReply = reply.Replace("[면접종료]", "").Trim();
                        Debug.Log("[GeminiManager] 면접 종료 신호 감지");
                        InterviewManager.NotifyGeminiResponseReceived(cleanReply);

                        // TTS 출력이 끝날 시간을 고려해서 3초 후 종료
                        StartCoroutine(EndInterviewAfterDelay(3f));
                    }
                    else
                    {
                        // 일반 응답은 그대로 TTS로 전달
                        InterviewManager.NotifyGeminiResponseReceived(reply);
                    }
                }
            }
        }

        // -----------------------------------------------
        // 면접 종료 지연 처리
        // TTS 출력이 끝날 시간을 고려해서 일정 시간 후 종료
        // -----------------------------------------------
        private IEnumerator EndInterviewAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Debug.Log("[GeminiManager] 면접 자동 종료");
            InterviewManager.Instance.EndInterview();
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

            // Gemini에게 보낼 종합 평가 요청 메시지
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
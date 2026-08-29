using UnityEngine;
using TMPro;
using HJS;
using UnityEngine.UI;

namespace ResultUI.Scripts
{
    public class Result : MonoBehaviour
    {
        [Header("--- 대화 기록 ---")]
        // TODO: [한효준] 대화 기록 표시 UI 연결
        [SerializeField] private TextMeshProUGUI conversationLogText;

        [Header("--- [음성 영역] UI 연결 ---")]
        [SerializeField] private TextMeshProUGUI voiceResultText;       // 음성 평가 결과
        [SerializeField] private TextMeshProUGUI voiceImprovementText;  // 음성 개선 사항

        [Header("--- [내용 영역] UI 연결 ---")]
        [SerializeField] private TextMeshProUGUI contentResultText;     // 내용 평가 결과
        [SerializeField] private TextMeshProUGUI contentImprovementText;// 내용 개선 사항

        [Header("--- [태도 영역] UI 연결 ---")]
        [SerializeField] private TextMeshProUGUI attitudeResultText;    // 태도 평가 결과
        [SerializeField] private TextMeshProUGUI attitudeImprovementText; // 태도 개선 사항
        
        [Header("--- 평가 결과 ---")]
        [SerializeField] private TextMeshProUGUI evaluationResultText;

        [Header("--- 테스트용 입력 옵션 ---")]
        [Tooltip("체크하면 Inspector에서 입력한 점수로 막대 차트 및 UI를 테스트합니다.")]
        [SerializeField] private bool useTestData = true;

        [Header("--- 세로 막대 차트 테스트 입력 (1.0 ~ 5.0) ---")]
        [Range(1.0f, 5.0f)][SerializeField] private float testVoiceScore = 4.0f;    // 음성 점수
        [Range(1.0f, 5.0f)][SerializeField] private float testContentScore = 5.0f;  // 내용 점수
        [Range(1.0f, 5.0f)][SerializeField] private float testAttitudeScore = 3.0f; // 태도 점수

        [Header("--- [세로 막대 차트 Image 연결] ---")]
        [Tooltip("Image Type: Filled / Fill Method: Vertical / Fill Origin: Bottom 설정 필수!")]
        [SerializeField] private Image voiceBarFill;    // 음성 영역 막대 Image
        [SerializeField] private Image contentBarFill;  // 내용 영역 막대 Image
        [SerializeField] private Image attitudeBarFill; // 태도 영역 막대 Image

        private const float MAX_SCORE = 5.0f; // 만점 기준

        private void Start()
        {
            //  피드백 목록 화면에서 버튼을 클릭하고 넘어온 경우인지 확인
            if (FeedbackManager.Instance != null && FeedbackManager.Instance.CurrentSelectedFeedback != null)
            {
                // 1. 과거 기록 보기 모드
                LoadSelectedData();
            }
            else
            {
                // 2. 실시간 면접 종료 후 넘어온 경우 (기존 원본 로직)
                ShowConversationLog();
            }

            if (useTestData)
            {
                ApplyChartScores(testVoiceScore, testContentScore, testAttitudeScore);
            }

            // (이하 기존 Start 로직 동일)
            if (FeedbackManager.Instance != null && FeedbackManager.Instance.CurrentSelectedFeedback != null)
            {
                LoadSelectedData();
            }
            else
            {
                ShowConversationLog();
            }
        }

        // -----------------------------------------------
        //  [추가] Inspector에서 값 수정 시 실시간으로 차트 반영 (실행 중일 때)
        // -----------------------------------------------
        private void OnValidate()
        {
            if (useTestData && Application.isPlaying)
            {
                ApplyChartScores(testVoiceScore, testContentScore, testAttitudeScore);
            }
        }

        // -----------------------------------------------
        // [추가] 세로 막대 차트 fillAmount 적용 함수 (밑에서 위로 채워짐)
        // -----------------------------------------------
        public void ApplyChartScores(float voiceScore, float contentScore, float attitudeScore)
        {
            if (voiceBarFill != null)
                voiceBarFill.fillAmount = Mathf.Clamp01(voiceScore / MAX_SCORE);

            if (contentBarFill != null)
                contentBarFill.fillAmount = Mathf.Clamp01(contentScore / MAX_SCORE);

            if (attitudeBarFill != null)
                attitudeBarFill.fillAmount = Mathf.Clamp01(attitudeScore / MAX_SCORE);
        }

        private void OnEnable()
        {
            // 평가 결과 이벤트 구독
            InterviewManager.OnEvaluationReceived += HandleEvaluationReceived;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            InterviewManager.OnEvaluationReceived -= HandleEvaluationReceived;
        }

        // -----------------------------------------------
        //  [추가됨] 매니저에서 선택된 데이터를 불러와 임시 텍스트 표시
        // TODO: [이재혁] DB 연동 완료 후 더미 텍스트 생성 부분 삭제 후 DB 로드로 변경
        // -----------------------------------------------
        private void LoadSelectedData()
        {
            var data = FeedbackManager.Instance.CurrentSelectedFeedback;

            // 1. 대화 기록 표시
            string dummyLog = $"[면접 일자 : {data.dateText} | 직무 : {data.jobText} | 유형 : {data.typeText}]\n\n" +
                              $"[지원자]\n안녕하세요, {data.jobText} 직무에 지원한 응시자입니다.\n\n" +
                              $"[면접관]\n네, 반갑습니다. {data.typeText} 답변 위주로 면접을 진행하겠습니다.";

            if (conversationLogText != null) conversationLogText.text = dummyLog;

            // 2. 영역별 결과/개선사항 임시 데이터 설정
            SetEvaluationUI(
                vResult: $"[4/5점] 음성 톤이 안정적이며 전달력이 좋습니다.",
                vImprove: $"말을 시작할 때 약간의 습관성 불필요 추임새(‘어...’, ‘음...’) 사용을 줄이면 더 전문적으로 보입니다.",

                cResult: $"[5/5점] {data.jobText} 직무 관련 핵심 개념을 정확히 이해하고 논리적으로 답변했습니다.",
                cImprove: $"질문 의도에 잘 맞게 답변했으나, 구체적인 실제 사례나 프로젝트 경험 수치를 덧붙이면 더욱 완벽합니다.",

                aResult: $"[3/5점] 전체적으로 차분한 표정을 유지했으나, 시선 처리가 다소 불안정했습니다.",
                aImprove: $"답변 중간에 카메라(면접관 시선)를 이탈하는 횟수를 줄이고 긍정적인 미소를 유지해 보세요."
            );

            FeedbackManager.Instance.CurrentSelectedFeedback = null;

            //  [추가] 테스트 모드가 아닐 때 불러온 점수로 차트 채우기
            if (!useTestData)
            {
                ApplyChartScores(4.0f, 5.0f, 3.0f); // (나중에 DB 점수로 대체될 자리)
            }
        }

        // -----------------------------------------------
        // 평가 결과 수신 시 자동 호출
        // -----------------------------------------------
        private void HandleEvaluationReceived(string evaluationResult)
        {
            Debug.Log("[Result] 평가 결과 수신 완료");

            // 평가 결과 텍스트 표시
            if (evaluationResultText != null)
                evaluationResultText.text = evaluationResult;
        }
        // -----------------------------------------------
        // 영역별 UI 텍스트 일괄 적용 함수
        // -----------------------------------------------
        private void SetEvaluationUI(string vResult, string vImprove, string cResult, string cImprove, string aResult, string aImprove)
        {
            // 음성 영역 UI
            if (voiceResultText != null) voiceResultText.text = vResult;
            if (voiceImprovementText != null) voiceImprovementText.text = vImprove;

            // 내용 영역 UI
            if (contentResultText != null) contentResultText.text = cResult;
            if (contentImprovementText != null) contentImprovementText.text = cImprove;

            // 태도 영역 UI
            if (attitudeResultText != null) attitudeResultText.text = aResult;
            if (attitudeImprovementText != null) attitudeImprovementText.text = aImprove;
        }

        // -----------------------------------------------
        // 대화 기록 표시
        // -----------------------------------------------
        private void ShowConversationLog()
        {
            if (conversationLogText == null) return;

            if (UnityAndGeminiV3.Instance == null)
            {
                conversationLogText.text = "대화 기록이 없습니다.";
                return;
            }

            var history = UnityAndGeminiV3.Instance.chatHistory;

            if (history == null || history.Length == 0)
            {
                conversationLogText.text = "대화 기록이 없습니다.";
                return;
            }

            string log = "";
            foreach (var content in history)
            {
                if (content.role == "user" && content.parts[0].text.Contains("면접관입니다"))
                    continue;

                if (content.role == "model" && content.parts[0].text == "네, 면접관 역할을 시작하겠습니다.")
                    continue;

                if (content.role == "user" && content.parts[0].text == "면접을 시작해주세요.")
                    continue;

                string speaker = content.role == "user" ? "지원자" : "면접관";
                string text = content.parts[0].text;

                if (text.Contains("[현재") && text.Contains("번 질문에 대한 답변]"))
                {
                    int tagEnd = text.IndexOf('\n');
                    if (tagEnd != -1)
                        text = text.Substring(tagEnd + 1).Trim();
                }

                log += $"[{speaker}]\n{text}\n\n";
            }

            conversationLogText.text = string.IsNullOrEmpty(log)
                ? "대화 기록이 없습니다."
                : log;
        }

        // -----------------------------------------------
        // 버튼 함수
        // -----------------------------------------------

        // [메인 화면] 버튼
        public void MainBtn()
        {
            // 메인으로 돌아가기 전 전체 초기화
            // 새 면접 시작 시 이전 데이터 남지 않게
            InterviewManager.Instance.ResetInterview();
            GameManager.Instance.LoadTitleScene();
        }
    }
}
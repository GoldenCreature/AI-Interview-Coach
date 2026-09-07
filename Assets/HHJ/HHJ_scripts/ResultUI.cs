using HJS;
using InterviewDb;
using InterviewDb.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResultUI.Scripts
{
    public class Result : MonoBehaviour
    {
        [Header("--- 대화 기록 ---")]
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

        [Header("--- [세로 막대 차트 Image 연결] ---")]
        [Tooltip("Image Type: Filled / Fill Method: Vertical / Fill Origin: Bottom 설정 필수!")]
        [SerializeField] private Image voiceBarFill;    // 음성 영역 막대 Image
        [SerializeField] private Image contentBarFill;  // 내용 영역 막대 Image
        [SerializeField] private Image attitudeBarFill; // 태도 영역 막대 Image

        private const float MAX_SCORE = 5.0f; // 만점 기준

        private void Start()
        {
            // 피드백 목록에서 특정 결과 항목을 선택하여 클릭하고 넘어온 경우
            if (FeedbackManager.Instance != null && FeedbackManager.Instance.CurrentSelectedFeedback != null)
            {
                LoadSelectedData();
            }
            else
            {
                // 면접 종료 직후 최신 DB/캐시 결과 로드
                LoadLatestDbResult();
            }
        }

        private void OnEnable()
        {
            InterviewManager.OnEvaluationReceived += HandleEvaluationReceived;
        }

        private void OnDisable()
        {
            InterviewManager.OnEvaluationReceived -= HandleEvaluationReceived;
        }

        /// <summary>
        /// 세로 막대 차트 fillAmount 적용 함수 (0.0 ~ 1.0)
        /// </summary>
        public void ApplyChartScores(float voiceScore, float contentScore, float attitudeScore)
        {
            if (voiceBarFill != null)
                voiceBarFill.fillAmount = Mathf.Clamp01(voiceScore / MAX_SCORE);

            if (contentBarFill != null)
                contentBarFill.fillAmount = Mathf.Clamp01(contentScore / MAX_SCORE);

            if (attitudeBarFill != null)
                attitudeBarFill.fillAmount = Mathf.Clamp01(attitudeScore / MAX_SCORE);
        }

        /// <summary>
        /// InterviewDbManager에서 최신 세션 결과(SessionReportRow)를 가져와 UI에 표시
        /// </summary>
        private void LoadLatestDbResult()
        {
            if (InterviewDbManager.Instance == null)
            {
                Debug.LogWarning("[Result] InterviewDbManager 인스턴스를 찾을 수 없습니다.");
                ShowConversationLog();
                return;
            }

            SessionReportRow report = InterviewDbManager.Instance.GetLatestSessionReport();
            if (report != null)
            {
                DisplayReportData(report);
            }
            else
            {
                Debug.LogWarning("[Result] DB에 조회할 최신 면접 결과가 없습니다.");
                ShowConversationLog();
            }
        }

        /// <summary>
        /// FeedbackManager에서 선택된 항목 로드
        /// </summary>
        private void LoadSelectedData()
        {
            var selectedFeedback = FeedbackManager.Instance.CurrentSelectedFeedback;

            if (selectedFeedback == null)
            {
                Debug.LogWarning("[Result] 선택된 피드백 데이터가 없습니다.");
                return;
            }

            // 1. DB 매니저가 유효한 경우, 전체 리포트 중 선택된 정보와 일치하는 레코드 검색
            if (InterviewDbManager.Instance != null)
            {
                var allReports = InterviewDbManager.Instance.GetAllSessionReports();

                // 직무(JobCategory) 또는 ID 등을 기준으로 매칭 (일치하는 레코드가 있으면 우선 표시)
                SessionReportRow matchedReport = null;
                if (allReports != null && allReports.Count > 0)
                {
                    matchedReport = allReports.Find(r => r.JobCategory == selectedFeedback.jobText);

                    // 일치하는 항목이 없으면 가장 최근 세션 리포트 사용
                    if (matchedReport == null)
                    {
                        matchedReport = InterviewDbManager.Instance.GetLatestSessionReport();
                    }
                }

                if (matchedReport != null)
                {
                    DisplayReportData(matchedReport);
                    FeedbackManager.Instance.CurrentSelectedFeedback = null;
                    return;
                }
            }

            // 2. DB 데이터를 찾지 못한 경우 선택된 feedback 데이터 기반 텍스트 표출
            if (conversationLogText != null)
            {
                conversationLogText.text = $"[면접 일자 : {selectedFeedback.dateText} | 직무 : {selectedFeedback.jobText} | 유형 : {selectedFeedback.typeText}]\n\n" +
                                           $"[지원자]\n안녕하세요, {selectedFeedback.jobText} 직무 지원자입니다.\n\n" +
                                           $"[면접관]\n반갑습니다. 면접을 시작하겠습니다.";
            }

            // 데이터 사용 후 초기화
            FeedbackManager.Instance.CurrentSelectedFeedback = null;
        }

        /// <summary>
        /// SessionReportRow DB 모델을 UI 텍스트 및 차트에 매핑
        /// </summary>
        private void DisplayReportData(SessionReportRow report)
        {
            float vScore = (float)(report.ScoreAudio ?? 0.0);
            float cScore = (float)(report.ScoreContent ?? 0.0);
            float aScore = (float)(report.ScoreAttitude ?? 0.0);

            // 1. 영역별 UI 텍스트 설정
            SetEvaluationUI(
                vResult: $"[{vScore:F1}/5.0점] {(string.IsNullOrEmpty(report.EvalAudioText) ? "음성 평가 내용이 없습니다." : report.EvalAudioText)}",
                vImprove: string.IsNullOrEmpty(report.AdviceAudioText) ? "개선 조언이 없습니다." : report.AdviceAudioText,

                cResult: $"[{cScore:F1}/5.0점] {(string.IsNullOrEmpty(report.EvalContentText) ? "답변 내용 평가가 없습니다." : report.EvalContentText)}",
                cImprove: string.IsNullOrEmpty(report.AdviceContentText) ? "개선 조언이 없습니다." : report.AdviceContentText,

                aResult: $"[{aScore:F1}/5.0점] {(string.IsNullOrEmpty(report.SummaryText) ? "태도 평가 결과가 없습니다." : report.SummaryText)}",
                aImprove: string.IsNullOrEmpty(report.AdviceText) ? "개선 조언이 없습니다." : report.AdviceText
            );

            // 2. 종합 평가 결과 텍스트 (TotalScore 존재 시)
            if (evaluationResultText != null)
            {
                double total = report.TotalScore ?? ((vScore + cScore + aScore) / 3.0);
                evaluationResultText.text = $"최종 종합 점수: {total:F1} / 5.0";
            }

            // 3. 막대 차트 반영
            ApplyChartScores(vScore, cScore, aScore);

            // 4. 대화 기록 표시 (DB ConversationLog 우선 사용)
            if (conversationLogText != null)
            {
                if (!string.IsNullOrEmpty(report.ConversationLog))
                {
                    conversationLogText.text = report.ConversationLog;
                }
                else
                {
                    ShowConversationLog();
                }
            }
        }

        private void HandleEvaluationReceived(string evaluationResult)
        {
            Debug.Log("[Result] 평가 결과 수신 완료");

            if (evaluationResultText != null)
                evaluationResultText.text = evaluationResult;
        }

        private void SetEvaluationUI(string vResult, string vImprove, string cResult, string cImprove, string aResult, string aImprove)
        {
            if (voiceResultText != null) voiceResultText.text = vResult;
            if (voiceImprovementText != null) voiceImprovementText.text = vImprove;

            if (contentResultText != null) contentResultText.text = cResult;
            if (contentImprovementText != null) contentImprovementText.text = cImprove;

            if (attitudeResultText != null) attitudeResultText.text = aResult;
            if (attitudeImprovementText != null) attitudeImprovementText.text = aImprove;
        }

        /// <summary>
        /// Gemini 인메모리 대화 히스토리 파싱 로직 (DB ConversationLog 미존재 시 사용)
        /// </summary>
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

        public void MainBtn()
        {
            InterviewManager.Instance.ResetInterview();
            GameManager.Instance.LoadTitleScene();
        }
    }
}
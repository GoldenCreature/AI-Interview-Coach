using HJS;
using InterviewDb;
using InterviewDb.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResultUI.Scripts
{
    public class Result : MonoBehaviour
    {
        [Header("--- 대화 기록 ---")]
        [SerializeField] private TextMeshProUGUI conversationLogText;
        [SerializeField] private ScrollRect conversationScrollRect;

        [Header("--- [음성 영역] UI 연결 ---")]
        [SerializeField] private TextMeshProUGUI voiceResultText;
        [SerializeField] private TextMeshProUGUI voiceImprovementText;

        [Header("--- [내용 영역] UI 연결 ---")]
        [SerializeField] private TextMeshProUGUI contentResultText;
        [SerializeField] private TextMeshProUGUI contentImprovementText;

        [Header("--- [태도 영역] UI 연결 ---")]
        [SerializeField] private TextMeshProUGUI attitudeResultText;
        [SerializeField] private TextMeshProUGUI attitudeImprovementText;

        [Header("--- 평가 결과 ---")]
        [SerializeField] private TextMeshProUGUI evaluationResultText;

        [Header("--- [막대 차트 연결] ---")]
        [SerializeField] private Image voiceBarFill;
        [SerializeField] private Image contentBarFill;
        [SerializeField] private Image attitudeBarFill;

        [Header("--- [막대 차트 점수 텍스트 연결] ---")]
        [SerializeField] private TextMeshProUGUI voiceScoreText;
        [SerializeField] private TextMeshProUGUI contentScoreText;
        [SerializeField] private TextMeshProUGUI attitudeScoreText;

        [Header("--- [테스트용 옵션] ---")]
        [SerializeField] private bool useDummyTest = false;
        [Range(0f, 5f)][SerializeField] private float testVoiceScore = 4.2f;
        [Range(0f, 5f)][SerializeField] private float testContentScore = 3.5f;
        [Range(0f, 5f)][SerializeField] private float testAttitudeScore = 4.8f;

        private const float MAX_SCORE = 5.0f;

        private void Start()
        {
            if (FeedbackManager.Instance != null && FeedbackManager.Instance.CurrentSelectedFeedback != null)
            {
                LoadSelectedData();
            }
            else
            {
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

        private void OnValidate()
        {
            if (useDummyTest)
            {
                ApplyChartScores(testVoiceScore, testContentScore, testAttitudeScore);
            }
        }

        public void ApplyChartScores(float voiceScore, float contentScore, float attitudeScore)
        {
            if (voiceBarFill != null)
                voiceBarFill.fillAmount = Mathf.Clamp01(voiceScore / MAX_SCORE);

            if (contentBarFill != null)
                contentBarFill.fillAmount = Mathf.Clamp01(contentScore / MAX_SCORE);

            if (attitudeBarFill != null)
                attitudeBarFill.fillAmount = Mathf.Clamp01(attitudeScore / MAX_SCORE);

            if (voiceScoreText != null)
                voiceScoreText.text = $"[{voiceScore:F1}/5]";

            if (contentScoreText != null)
                contentScoreText.text = $"[{contentScore:F1}/5]";

            if (attitudeScoreText != null)
                attitudeScoreText.text = $"[{attitudeScore:F1}/5]";
        }

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

        private void LoadSelectedData()
        {
            var selectedFeedback = FeedbackManager.Instance.CurrentSelectedFeedback;

            if (selectedFeedback == null)
            {
                LoadLatestDbResult();
                return;
            }

            if (InterviewDbManager.Instance != null)
            {
                var allReports = InterviewDbManager.Instance.GetAllSessionReports();
                SessionReportRow matchedReport = allReports?.Find(r => r.SessionId == selectedFeedback.SessionId);

                if (matchedReport != null)
                {
                    DisplayReportData(matchedReport);
                }
                else
                {
                    DisplayReportData(selectedFeedback);
                }
            }
            else
            {
                DisplayReportData(selectedFeedback);
            }

            FeedbackManager.Instance.CurrentSelectedFeedback = null;
        }

        private string FormatConversationLog(string rawJson)
        {
            if (string.IsNullOrEmpty(rawJson))
                return "대화 기록이 없습니다.";

            try
            {
                var dialogueList = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(rawJson);

                if (dialogueList == null || dialogueList.Count == 0)
                {
                    return rawJson;
                }

                StringBuilder sb = new StringBuilder();

                foreach (var entry in dialogueList)
                {
                    string speaker = entry.ContainsKey("speaker") ? entry["speaker"] : "";
                    string text = entry.ContainsKey("text") ? entry["text"] : "";

                    string speakerName = "지원자";
                    if (speaker.Equals("AI", StringComparison.OrdinalIgnoreCase) ||
                        speaker.Equals("Model", StringComparison.OrdinalIgnoreCase) ||
                        speaker.Equals("Interviewer", StringComparison.OrdinalIgnoreCase))
                    {
                        speakerName = "면접관";
                    }

                    string cleanText = text.Replace("\\n", "\n").Trim();

                    sb.AppendLine($"[{speakerName}]");
                    sb.AppendLine(cleanText);
                    sb.AppendLine();
                }

                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Result] 대화 로그 JSON 파싱 오류: {ex.Message}");
                return rawJson.Replace("\\n", "\n");
            }
        }

        private void DisplayReportData(SessionReportRow report)
        {
            float vScore = (float)(report.ScoreAudio ?? 0.0);
            float cScore = (float)(report.ScoreContent ?? 0.0);
            float aScore = (float)(report.ScoreAttitude ?? 0.0);

            SetEvaluationUI(
                vResult: string.IsNullOrEmpty(report.EvalAudioText) ? "음성 평가 내용이 없습니다." : report.EvalAudioText,
                vImprove: string.IsNullOrEmpty(report.AdviceAudioText) ? "개선 조언이 없습니다." : report.AdviceAudioText,

                cResult: string.IsNullOrEmpty(report.EvalContentText) ? "답변 내용 평가가 없습니다." : report.EvalContentText,
                cImprove: string.IsNullOrEmpty(report.AdviceContentText) ? "개선 조언이 없습니다." : report.AdviceContentText,

                aResult: string.IsNullOrEmpty(report.SummaryText) ? "태도 평가 결과가 없습니다." : report.SummaryText,
                aImprove: string.IsNullOrEmpty(report.AdviceText) ? "개선 조언이 없습니다." : report.AdviceText
            );

            if (evaluationResultText != null)
            {
                double total = report.TotalScore ?? ((vScore + cScore + aScore) / 3.0);
                evaluationResultText.text = $"최종 종합 점수: {total:F1} / 5.0";
            }

            ApplyChartScores(vScore, cScore, aScore);

            if (conversationLogText != null)
            {
                if (!string.IsNullOrEmpty(report.ConversationLog))
                {
                    SetLogTextAndResetScroll(FormatConversationLog(report.ConversationLog));
                }
                else
                {
                    ShowConversationLog();
                }
            }
        }

        private void SetLogTextAndResetScroll(string text)
        {
            conversationLogText.text = text;

            if (conversationScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                conversationScrollRect.verticalNormalizedPosition = 1.0f;
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

        private void ShowConversationLog()
        {
            if (conversationLogText == null) return;

            if (UnityAndGeminiV3.Instance == null)
            {
                SetLogTextAndResetScroll("대화 기록이 없습니다.");
                return;
            }

            var history = UnityAndGeminiV3.Instance.chatHistory;

            if (history == null || history.Length == 0)
            {
                SetLogTextAndResetScroll("대화 기록이 없습니다.");
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

            SetLogTextAndResetScroll(string.IsNullOrEmpty(log) ? "대화 기록이 없습니다." : log);
        }

        public void MainBtn()
        {
            InterviewManager.Instance.ResetInterview();
            GameManager.Instance.LoadTitleScene();
        }
    }
}
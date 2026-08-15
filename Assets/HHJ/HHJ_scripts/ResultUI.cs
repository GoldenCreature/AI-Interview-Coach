using UnityEngine;
using TMPro;
using HJS;

namespace ResultUI.Scripts
{
    public class Result : MonoBehaviour
    {
        [Header("--- 대화 기록 ---")]
        // TODO: [한효준] 대화 기록 표시 UI 연결
        [SerializeField] private TextMeshProUGUI conversationLogText;

        [Header("--- 평가 결과 ---")]
        // TODO: [한효준] 평가 결과 표시 UI 연결
        [SerializeField] private TextMeshProUGUI evaluationResultText;

        //[Header("--- 로딩 표시 ---")]
        // 평가 결과 기다리는 동안 표시할 패널 (선택사항)
        // TODO: [한효준] 로딩 패널 UI 연결
        //[SerializeField] private GameObject loadingPanel;

        private void Start()
        {
            // 씬 시작 시 대화 기록 즉시 표시
            // TODO: [이재혁] DB 연동 완료 후
            //       chatHistory 직접 읽기 → DB에서 읽기로 교체
            ShowConversationLog();

            // 평가 결과는 비동기로 오기 때문에
            // 로딩 패널을 먼저 활성화
            //if (loadingPanel != null)
            //    loadingPanel.SetActive(true);
        }

        private void OnEnable()
        {
            // 평가 결과 이벤트 구독
            // GeminiManager → UIManager → InterviewManager
            // → Result.cs 순서로 전달됨
            InterviewManager.OnEvaluationReceived += HandleEvaluationReceived;
        }

        private void OnDisable()
        {
            // 이벤트 구독 해제
            InterviewManager.OnEvaluationReceived -= HandleEvaluationReceived;
        }

        // -----------------------------------------------
        // 평가 결과 수신 시 자동 호출
        // 현재: 이벤트로 직접 받는 임시 방식
        // TODO: [이재혁] DB 연동 완료 후
        //       이벤트 수신 방식 → DB에서 읽기로 교체
        // TODO: [한효준] DB 연동 후 해당 부분 UI 연결 필요
        // -----------------------------------------------
        private void HandleEvaluationReceived(string evaluationResult)
        {
            Debug.Log("[Result] 평가 결과 수신 완료");

            // 로딩 패널 비활성화
            //if (loadingPanel != null)
            //    loadingPanel.SetActive(false);

            // 평가 결과 텍스트 표시
            if (evaluationResultText != null)
                evaluationResultText.text = evaluationResult;
        }

        // -----------------------------------------------
        // 대화 기록 표시
        // 현재: GeminiManager chatHistory 직접 읽는 임시 방식
        // TODO: [이재혁] DB 연동 완료 후
        //       chatHistory 직접 읽기 → DB에서 읽기로 교체
        // TODO: [한효준] DB 연동 후 해당 부분 UI 연결 필요
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
                // 시스템 프롬프트 제외
                // 사전 주입된 면접관 역할 프롬프트 건너뜀
                if (content.role == "user" &&
                    content.parts[0].text.Contains("면접관입니다"))
                    continue;

                if (content.role == "model" &&
                    content.parts[0].text == "네, 면접관 역할을 시작하겠습니다.")
                    continue;

                // 면접 시작 트리거 메시지 제외
                if (content.role == "user" &&
                    content.parts[0].text == "면접을 시작해주세요.")
                    continue;

                string speaker = content.role == "user" ? "지원자" : "면접관";
                string text = content.parts[0].text;

                // 질문 번호 태그 제거
                // "[현재 N번 질문에 대한 답변]" 태그 제거 후 표시
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
            GameManager.Instance.LoadTitleScene();
        }

        // [다시 면접] 버튼 (선택사항)
        //public void RetryBtn()
        //{
        //    GameManager.Instance.LoadInterviewSetupScene();
        //}
    }
}
using UnityEngine;
using InterviewDb;

public class DbDirectTester : MonoBehaviour
{
    /*
    void St1art()
    {
        // 1. 세션 생성 테스트
        int sessionId = InterviewDbManager.Instance.StartSession("IT 개발자", "압박형");
        Debug.Log($"[테스트] 세션 생성 성공 - ID: {sessionId}");

        // 2. 가짜 대화 로그 JSON (트리거 검증용)
        string dummyChatJson = "[{\"speaker\":\"AI\",\"message\":\"자기소개 부탁드립니다.\"},{\"speaker\":\"User\",\"message\":\"안녕하세요 백엔드 개발자입니다.\"}]";

        // 3. 종수 팀장님 영역 (음성/내용 점수 및 결과 적재)
        InterviewDbManager.Instance.SaveInterviewResult(
            sessionId: sessionId,
            scoreAudio: 4.5,
            evalAudioText: "목소리가 또렷하고 발음이 명확합니다.",
            adviceAudioText: "말끝을 흐리지 않고 당당하게 마무리해보세요.",
            scoreContent: 4.0,
            evalContentText: "직무에 대한 기본 이해도가 높습니다.",
            adviceContentText: "구체적인 프로젝트 문제 해결 경험을 추가하면 좋겠습니다.",
            conversationLogJson: dummyChatJson,
            customDurationSeconds: 180
        );

        // 4. 모세님 영역 (태도 점수 적재)
        InterviewDbManager.Instance.SaveFaceEvaluation(
            sessionId: sessionId,
            scoreAttitude: 4.2,
            adviceAttitudeText: "면접관을 바라보는 정면 응시율이 우수합니다."
        );

        // 5. 종합 점수 입력
        InterviewDbManager.Instance.SetTotalScore(sessionId, 4.2);

        Debug.Log("✅ [테스트 완료] DB에 더미 데이터 적재가 완료되었습니다!");
    }
    */

    private void Awake()
    {
        // 씬 시작과 동시에 실제 DB에 테스트용 세션 2건 적재
        SeedSampleData();
    }

    private void SeedSampleData()
    {
        // 1번 세션 (최근 데이터)
        int id2 = InterviewDbManager.Instance.StartSession("마케팅 기획자", "일반형");
        InterviewDbManager.Instance.SaveInterviewResult(
            sessionId: id2,
            scoreAudio: 4.5, evalAudioText: "발음이 또렷합니다.", adviceAudioText: "속도를 조금 늦춰보세요.",
            scoreContent: 4.2, evalContentText: "직무 분석이 뛰어납니다.", adviceContentText: "정량적 성과를 강조하세요.",
            conversationLogJson: "[]"
        );
        InterviewDbManager.Instance.SaveFaceEvaluation(id2, 4.0, "시선 고정이 안정적입니다.");
        InterviewDbManager.Instance.SetTotalScore(id2, 4.2);

        // 2번 세션 (이전 데이터)
        int id3 = InterviewDbManager.Instance.StartSession("IT 백엔드 개발자", "압박형");
        InterviewDbManager.Instance.SaveInterviewResult(
            sessionId: id3,
            scoreAudio: 3.8, evalAudioText: "목소리가 다소 작습니다.", adviceAudioText: "자신감 있게 발성하세요.",
            scoreContent: 4.6, evalContentText: "기술적 이해도가 깊습니다.", adviceContentText: "아키텍처 근거를 제시하세요.",
            conversationLogJson: "[]"
        );
        InterviewDbManager.Instance.SaveFaceEvaluation(id3, 3.5, "시선이 자주 흔들립니다.");
        InterviewDbManager.Instance.SetTotalScore(id3, 4.0);

        Debug.Log("✅ [DbDirectTester] DB에 실제 테스트 데이터 2건 적재 완료");
    }
}

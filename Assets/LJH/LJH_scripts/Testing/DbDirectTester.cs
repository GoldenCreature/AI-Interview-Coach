using UnityEngine;
using InterviewDb;

public class DbDirectTester : MonoBehaviour
{
    void Start()
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
}
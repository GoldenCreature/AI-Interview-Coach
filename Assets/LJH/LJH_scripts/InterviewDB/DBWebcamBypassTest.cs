using UnityEngine;

public class DBWebcamBypassTest : MonoBehaviour
{
    [Tooltip("씬에 있는 FaceScoreDBLogger를 끌어다 넣으세요")]
    public FaceScoreDBLogger dbLogger;

    void Start()
    {
        if (dbLogger != null)
        {
            // 웹캠 없이 가짜 표정 데이터 강제 삽입 테스트
            dbLogger.SaveScoreToDB(85.5f, 10.2f, 2.1f, "Good", "미소가 아주 자연스럽습니다.", 4.5f);
            Debug.Log("✅ 가상 면접 더미 데이터 DB 삽입 완료!");
        }
    }
}
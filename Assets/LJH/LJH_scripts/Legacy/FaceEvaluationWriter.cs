// ============================================================
// FaceEvaluationWriter.cs
// ------------------------------------------------------------
// ⚠ 이 클래스의 실제 로직은 InterviewResultRepository.SaveFaceEvaluation로
//   옮겨졌습니다 (팀 데이터 입출력 통로를 파일 하나로 모으기 위함).
//   이 파일은 기존 호출부(FaceEvaluationWriter.Save(...))가 계속 컴파일되도록
//   남겨둔 얇은 래퍼 클래스. 새로 작성하는 코드는 이 파일 대신
//   InterviewResultRepository.SaveFaceEvaluation(...)을 직접 호출해야 함.
// ============================================================
using SQLite;

namespace InterviewDb.Core
{
    public static class FaceEvaluationWriter
    {
        /// <summary>[Deprecated] InterviewResultRepository.SaveFaceEvaluation(...)을 대신 사용하여야 함.</summary>
        public static void Save(SQLiteConnection conn, int sessionId, double evaluationScore, string evaluationDetail)
        {
            InterviewResultRepository.SaveFaceEvaluation(conn, sessionId, evaluationScore, evaluationDetail);
        }
    }
}

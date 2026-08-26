// ============================================================
// FaceEvaluationWriter.cs
// ------------------------------------------------------------
// 팀원이 만든 FaceScoreEntry(표정 분석 결과)를 별도 테이블로 만들지 않고,
// 기존 Session_Result 테이블에 흡수시키는 스키마 수정 코드.
//
//   FaceScoreEntry.EvaluationScore  → Session_Result.score_attitude
//   FaceScoreEntry.EvaluationDetail → Session_Result.advice_text ("[표정] " 라벨 붙여서 append)
//   SmileScore / SurpriseScore / AngryScore / Timestamp / Id → 저장하지 않음
//   (원래 명세서의 "최종 결과만 적재" 원칙대로, 원시 중간값은 영상 처리
//    모듈 내부에서 EvaluationScore를 계산하는 데만 쓰고 버림)
//
// ⚠ 본 코드는 스키마 SchemaBootstrapHardened.cs 를 사용한다는 전제 하에서 사용.
//   score_attitude가 바뀌면 트리거가 total_score/version을 알아서 다시 계산해줌.
//
// ⚠ 스레드 안전성: 이 클래스 자체는 스레드에 관여하지 않음. MediaPipe
//   콜백이 백그라운드 스레드에서 온다면, 이 메서드를 직접 부르지 말고
//   MainThreadDbDispatcher.Instance.Enqueue(conn => FaceEvaluationWriter.Save(...))
//   형태로 감싸서 호출하는게 좋다고 함.
// ============================================================
using System;
using SQLite;
using InterviewDb.Models;

namespace InterviewDb.Core
{
    public static class FaceEvaluationWriter
    {
        /// <summary>
        /// 표정/비언어 분석 결과를 해당 세션의 Session_Result에 반영.
        /// 이미 다른 모듈(음성/내용 분석)이 먼저 행을 만들어뒀으면 UPDATE,
        /// 이 모듈이 가장 먼저 끝났다면 새로 INSERT.
        /// </summary>
        public static void Save(SQLiteConnection conn, int sessionId, double evaluationScore, string evaluationDetail)
        {
            string labeled = string.IsNullOrWhiteSpace(evaluationDetail) ? null : $"[표정] {evaluationDetail.Trim()}";

            int rows;
            if (labeled != null)
            {
                rows = conn.Execute(
                    @"UPDATE Session_Result
                      SET score_attitude = ?,
                          advice_text = CASE
                              WHEN advice_text IS NULL OR advice_text = '' THEN ?
                              ELSE advice_text || ' ' || ?
                          END
                      WHERE session_id = ?",
                    evaluationScore, labeled, labeled, sessionId);
            }
            else
            {
                // 텍스트 코멘트가 없을 때는 advice_text를 건드리지 않음
                // (CASE 식에 NULL을 그대로 넣으면 기존 advice_text가 통째로 NULL로 덮여씀)
                rows = conn.Execute(
                    "UPDATE Session_Result SET score_attitude = ? WHERE session_id = ?",
                    evaluationScore, sessionId);
            }

            if (rows == 0)
            {
                // 아직 이 세션의 Session_Result 행이 없음 → 이 모듈이 가장 먼저 끝난 경우
                conn.Insert(new SessionResultHardened
                {
                    SessionId = sessionId,
                    ScoreAttitude = evaluationScore,
                    AdviceText = labeled,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Version = 1 // ORM Insert는 SQL DEFAULT를 타지 않으므로 명시적으로 지정
                });
            }
        }
    }
}

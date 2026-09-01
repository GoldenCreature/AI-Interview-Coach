// ============================================================
// InterviewResultRepository.cs
// ------------------------------------------------------------
// 팀원이 DB를 건드릴 때 실제로 알아야 할 건 이 파일 하나임.
// Model/Schema/Testing 폴더 내부 구조는 몰라도 되는 상태.
//
//   1) InitializeSchema(conn)   — 프로그램 시작 시 딱 한 번
//   2) SaveInterviewResult(...) — 면접 종료 후 결과 저장 시 (팀장님이 호출)
//   3) GetSessionReport(...)    — Result Scene에서 리포트 조회 시
//
// ── DB 초기화와 데이터 적재 분리 ──
//   InitializeSchema는 CREATE TABLE IF NOT EXISTS만 실행(테이블 공간
//   확보). 실제 행 데이터는 절대 여기서 만들지 않음.
//   SaveInterviewResult는 그 반대로, 스키마는 절대 건드리지 않고
//   오직 INSERT/UPDATE(트랜잭션으로 묶음)만 수행.
//
// ── 영역별 데이터 규격 ──
//   음성 : score_audio(REAL) + eval_audio_text(TEXT) + advice_audio_text(TEXT)
//   내용 : score_content(REAL) + eval_content_text(TEXT) + advice_content_text(TEXT)
//   태도 : score_attitude(REAL) 단일 점수만 (텍스트 없음 — FaceEvaluationWriter가
//          별도로 채우며, 표정 코멘트는 공용 advice_text에 누적됨)
//   세션 메타정보(직무=job_category, 언어/유형=interview_lang, 일시=start_time)는
//   Interview_Session에 이미 TEXT로 정확히 저장되고 있어 별도 작업 불필요.
//
// ⚠ 스레드 안전성: 이 파일의 메서드들은 SQLiteConnection을 직접 받게 됨.
//   Gemini API 콜백이 백그라운드 스레드에서 온다면, 직접 호출하지 말고
//   MainThreadDbDispatcher.Instance.Enqueue(conn => InterviewResultRepository.SaveInterviewResult(conn, input));
//   형태로 감싸서 호출하는 것이 좋음.
// ============================================================
using System;
using System.Linq;
using SQLite;
using InterviewDb.Models;

namespace InterviewDb.Core
{
    /// <summary>
    /// SaveInterviewResult 한 번 호출에 필요한 입력값을 모아둔 DTO.
    /// Gemini 프롬프트 파싱 결과를 이 형태로 채워서 넘겨주면 됨
    /// </summary>
    public class InterviewEvaluationInput
    {
        public int SessionId { get; set; }

        public double? ScoreAudio { get; set; }
        public string EvalAudioText { get; set; }
        public string AdviceAudioText { get; set; }

        public double? ScoreContent { get; set; }
        public string EvalContentText { get; set; }
        public string AdviceContentText { get; set; }
    }

    public static class InterviewResultRepository
    {
        /// <summary>
        /// 프로그램 시작 시 한 번만 호출해야 함. CREATE TABLE IF NOT EXISTS로
        /// 테이블 공간만 확보하며, 데이터는 만들지 않음.
        /// </summary>
        public static void InitializeSchema(SQLiteConnection conn)
        {
            SchemaBootstrapHardened.ApplySchema(conn);
        }

        /// <summary>
        /// 면접 종료 후, 음성/내용 분석 결과를 저장. (태도 점수는 FaceEvaluationWriter가 별도 처리)        /// 다른 모듈이 먼저 결과를 만들어뒀으면 UPDATE, 없으면 새로 INSERT — 순서 상관없이 안전합니다.
        /// 전체를 트랜잭션으로 묶어서, 저장 도중 실패하면 부분 반영 없이 전부 롤백.
        /// </summary>
        public static void SaveInterviewResult(SQLiteConnection conn, InterviewEvaluationInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            conn.BeginTransaction();
            try
            {
                int rows = conn.Execute(
                    @"UPDATE Session_Result
                      SET score_audio = ?, eval_audio_text = ?, advice_audio_text = ?,
                          score_content = ?, eval_content_text = ?, advice_content_text = ?
                      WHERE session_id = ?",
                    input.ScoreAudio, input.EvalAudioText, input.AdviceAudioText,
                    input.ScoreContent, input.EvalContentText, input.AdviceContentText,
                    input.SessionId);

                if (rows == 0)
                {
                    // 이 세션의 Session_Result 행이 아직 없음 → 음성/내용 분석이 가장 먼저 끝난 경우
                    conn.Insert(new SessionResultHardened
                    {
                        SessionId = input.SessionId,
                        ScoreAudio = input.ScoreAudio,
                        EvalAudioText = input.EvalAudioText,
                        AdviceAudioText = input.AdviceAudioText,
                        ScoreContent = input.ScoreContent,
                        EvalContentText = input.EvalContentText,
                        AdviceContentText = input.AdviceContentText,
                        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Version = 1
                    });
                }

                conn.Commit();
            }
            catch
            {
                conn.Rollback();
                throw; // 호출한 쪽(팀장님 코드)에서 실패를 알 수 있도록 다시 던짐
            }
        }

        /// <summary>
        /// Result Scene에서 결과 화면을 그릴 때 호출. 세션이 없거나
        /// 아직 결과가 하나도 없으면 null을 반환.
        /// </summary>
        public static SessionReportRow GetSessionReport(SQLiteConnection conn, int sessionId)
        {
            return conn.Query<SessionReportRow>(
                "SELECT * FROM View_Session_Report WHERE session_id = ?", sessionId)
                .FirstOrDefault();
        }
    }
}

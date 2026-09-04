// ============================================================
// DbSchemaReference.cs
// ------------------------------------------------------------
// ⚠ 이 파일은 실행되는 코드가 아닙니다. "테이블 구조를 찾기 어렵다"는
//   피드백에 따라, 현재 스키마의 테이블/뷰/트리거 설계를 한 파일에
//   모아둔 참고 전용 문서. 실제 스키마 생성은 Schema/SchemaBootstrapHardened.cs가
//   담당하며, 이 파일을 수정해도 DB에는 아무 영향이 없음.
//
// 데이터베이스 구조를 알고 싶을 때 이 파일 하나만 열어보면 됨.
// ============================================================

/*
================================================================================
 1. Interview_Session — 면접 세션 이력 + 대화 로그
================================================================================
 컬럼명              타입      제약 조건                                   설명
 --------------------------------------------------------------------------------
 session_id          INTEGER   PK, AUTOINCREMENT                          면접 세션 고유 번호
 job_category        TEXT      NOT NULL DEFAULT 'IT'                      선택 직종
 session_status      TEXT      NOT NULL, CHECK(In-Progress/Completed/Aborted)  세션 상태
 start_time          TEXT      NOT NULL DEFAULT (localtime)               면접 시작 일시
 end_time            TEXT      NULL, CHECK(end_time >= start_time)        면접 종료 일시
 conversation_log    TEXT      NULL, CHECK(json_valid)                    STT/TTS 전체 대화(JSON 배열)

 ⚠ interview_lang 컬럼 없음 (일단 한국어 면접만 진행하기로 하여 수정에서 제거됨)
 C# 매핑 클래스 : InterviewDb.Models.InterviewSession (Model/InterviewDbModels.cs)


================================================================================
 2. Session_Result — 면접 최종 결과 (Interview_Session과 1:1)
================================================================================
 컬럼명                  타입      제약 조건                          설명
 --------------------------------------------------------------------------------
 session_id              INTEGER   PK, FK→Interview_Session(CASCADE)  세션 ID 공유
 score_audio             REAL      NULL                               음성 점수
 eval_audio_text         TEXT      NULL                               음성 평가 결과
 advice_audio_text       TEXT      NULL                               음성 개선사항
 score_content           REAL      NULL                               내용 점수
 eval_content_text       TEXT      NULL                               내용 평가 결과
 advice_content_text     TEXT      NULL                               내용 개선사항
 score_attitude          REAL      NULL                               태도 점수 (단일 점수만, 텍스트 없음)
 total_score             REAL      NULL                               종합 점수 ※자동계산 안 됨, 직접 SetTotalScore 호출 필요
 summary_text            TEXT      NULL                               공용 총평
 advice_text             TEXT      NULL                               공용 개선 가이드 (표정 코멘트 "[표정] ..." 포함)
 created_at              TEXT      NOT NULL DEFAULT (localtime)        결과 저장 일시
 version                 INTEGER   NOT NULL DEFAULT 1                  낙관적 동시성 제어용 (자동 증가)

 ⚠ App_Setting 테이블 자체가 없음 (이번 수정에서 완전히 제거함)
 ⚠ total_score 자동 계산 트리거 없음 (표정 분석 담당자가 직접 계산해서 SetTotalScore로 저장)
 C# 매핑 클래스 : InterviewDb.Models.SessionResultHardened (Model/HardenedDbModels.cs)


================================================================================
 3. View_Session_Report — 결과 화면 조회 전용 뷰 (SELECT만 가능)
================================================================================
 Interview_Session의 (session_id, job_category, start_time, end_time)
 + 계산값 duration_seconds
 + Session_Result의 모든 컬럼(session_id 제외)
 을 LEFT JOIN 해서 보여줌. Session_Result가 아직 없는 세션도 1건으로
 조회되며, 이 경우 점수/텍스트 컬럼은 전부 NULL.

 C# 매핑 클래스 : InterviewDb.Models.SessionReportRow (Model/InterviewDbModels.cs)


================================================================================
 4. 트리거(자동 동작) 요약
================================================================================
 트리거명                                  발동 시점                    하는 일
 --------------------------------------------------------------------------------
 trg_session_result_fk_guard_insert        Session_Result INSERT 전     존재하지 않는 session_id 차단
                                                                         (연결의 foreign_keys 설정과 무관하게 항상 작동)
 trg_interview_session_cascade_delete      Interview_Session DELETE 후  연결된 Session_Result 함께 삭제
 trg_session_result_version_bump           Session_Result 주요 컬럼 UPDATE 후  version 자동 +1
 trg_interview_session_validate_log_*      conversation_log INSERT/UPDATE 전  JSON 형식 + speaker 값('AI'/'User') 검증

 ⚠ total_score를 자동 계산해주는 트리거는 없음 (8차 수정에서 제거).


================================================================================
 5. 데이터 입출력 — 이 5개 함수만 알면 됩니다 (API/InterviewResultRepository.cs)
================================================================================
 InitializeSchema(conn)                                       프로그램 시작 시 1회
 SaveInterviewResult(conn, InterviewEvaluationInput)           음성+내용 결과 저장 (팀장님)
 SaveFaceEvaluation(conn, sessionId, score, detail)            태도 결과 저장 (표정 분석 담당자)
 SetTotalScore(conn, sessionId, totalScore)                    종합 점수 저장 (3개 영역 합산하는 쪽)
 GetSessionReport(conn, sessionId)                             Result Scene 조회용
================================================================================
*/

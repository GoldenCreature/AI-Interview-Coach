// ============================================================
// DbSchemaReference.cs
// ------------------------------------------------------------
// ⚠ 이 파일은 실행되는 코드가 아님. "테이블 구조를 찾기 어렵다"는
//   피드백에 따라, 현재 스키마의 테이블/뷰/트리거 설계를 한 파일에
//   모아둔 참고 전용 문서. 실제 스키마 생성은
//   Schema/SchemaBootstrapHardened.cs가 담당하며, 이 파일을 수정해도
//   DB에는 아무 영향이 없음.
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
 end_time            TEXT      NULL                                       면접 종료 일시
 duration_seconds    INTEGER   NULL, CHECK(duration_seconds >= 0)         면접 소요 시간(초)
 conversation_log    TEXT      NULL, CHECK(json_valid)                    STT/TTS 전체 대화(JSON 배열)

 ⚠ start_time 컬럼 없음 — 시작 시각은 저장하지 않고, InterviewDbManager가
   내부적으로 경과 시간을 재서 duration_seconds에 직접 채워 넣음.
 ⚠ interview_lang 컬럼 없음 (한국어 면접만 진행하기로 하여 제거됨)
 ⚠ duration_seconds는 뷰가 계산하는 값이 아니라, 이 테이블에 직접
   저장되는 값임. (INSERT/UPDATE 시 애플리케이션이 채워 넣음).
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
 score_attitude          REAL      NULL                               태도 점수 (단일 점수만, 전용 텍스트 컬럼 없음)
 total_score             REAL      NULL                               종합 점수 ※자동계산 안 됨, 직접 SetTotalScore 호출 필요
 summary_text            TEXT      NULL                               공용 총평
 advice_text             TEXT      NULL                               공용 개선 가이드 (표정 코멘트 "[표정] ..." 포함)
 created_at              TEXT      NOT NULL DEFAULT (localtime)        결과 저장 일시
 version                 INTEGER   NOT NULL DEFAULT 1                  낙관적 동시성 제어용 (자동 증가)

 ⚠ App_Setting 테이블 자체가 없음 (완전히 제거됨)
 ⚠ total_score 자동 계산 트리거 없음 (누군가 3개 영역 점수를 합산해서 SetTotalScore로 직접 저장해야 함)
 ⚠ 태도(attitude)는 전용 평가/개선 텍스트 컬럼이 없습니다. 코멘트가 필요하면
   공용 advice_text를 같이 씁니다 (음성/내용처럼 분리된 전용 필드는 없음).
 C# 매핑 클래스 : InterviewDb.Models.SessionResultHardened (Model/HardenedDbModels.cs)


================================================================================
 3. View_Session_Report — 결과 화면 조회 전용 뷰 (SELECT만 가능)
================================================================================
 Interview_Session의 (session_id, job_category, session_status, end_time,
 duration_seconds, conversation_log) — 전부 그대로 통과 (계산 없음)
 + Session_Result의 모든 컬럼(session_id 제외, version/created_at 제외)
 을 LEFT JOIN 해서 보여줌. Session_Result가 아직 없는 세션도 1건으로
 조회되며, 이 경우 점수/텍스트 컬럼은 전부 NULL.

 ⚠ C# 클래스(SessionReportRow)에는 session_status가 아직 매핑되어 있지
   않음. — 뷰에는 있지만 읽어올 방법이 없는 상태이니, 화면에서 세션
   상태를 써야 한다면 이 클래스에 프로퍼티 추가가 먼저 필요함.
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
                                                                         (total_score 갱신도 감지 대상에 포함됨)
 trg_interview_session_validate_log_*      conversation_log INSERT/UPDATE 전  JSON 형식 + speaker 값 검증
                                                                         (대소문자 무관 'ai'/'user'/'model' 허용 —
                                                                          Gemini 응답 형식과 맞추기 위해 'model' 추가됨)

 ⚠ total_score를 자동 계산해주는 트리거는 제외함.


================================================================================
 5. 데이터 입출력 — InterviewDbManager.cs 하나만 참조하면 되는 부분
================================================================================
 실제 DB I/O는 전부 InterviewDbManager(싱글턴, namespace InterviewDb)를 통해서만
 이뤄집니다. 커넥션을 직접 열거나 관리할 필요가 없음 — 씬에 없으면
 InterviewDbManager.Instance 호출 시 자동으로 생성됨.

 InterviewDbManager.Instance.StartSession(jobCategory, interviewType)
     → 면접 시작. session_id(int) 반환. (한종수 팀장)
 InterviewDbManager.Instance.AbortSession(sessionId)
     → 면접 중단 처리 (session_status='Aborted', end_time/duration 기록)
 InterviewDbManager.Instance.SaveInterviewResult(sessionId,
     scoreAudio, evalAudioText, adviceAudioText,
     scoreContent, evalContentText, adviceContentText,
     conversationLogJson)
     → 음성+내용 결과 저장. 세션도 함께 'Completed' 처리됨. (한종수 팀장)
 InterviewDbManager.Instance.SaveFaceEvaluation(sessionId, scoreAttitude, adviceAttitudeText)
     → 태도(표정) 결과 저장. (신모세 팀원)
 InterviewDbManager.Instance.SetTotalScore(sessionId, totalScore)
     → 종합 점수 저장. 3개 영역 점수를 합산하는 쪽이 직접 계산해서 호출.
 InterviewDbManager.Instance.GetLatestSessionReport()
     → 가장 최근 세션 리포트 1건 조회 (SessionReportRow). (한효준 팀원)
 InterviewDbManager.Instance.GetAllSessionReports()
     → 전체 세션 리포트 목록 조회 (List<SessionReportRow>). (한효준 팀원)
 InterviewDbManager.Instance.DeleteSession(sessionId)
     → 세션 삭제 (Session_Result도 CASCADE로 함께 삭제됨)

 ⚠ InterviewResultRepository.cs / FaceEvaluationWriter.cs는 예전에 만든
   저수준(SQLiteConnection을 직접 받는) API. InterviewDbManager가
   내부적으로 이와 비슷한 방식을 자체 구현해 대체했으므로, 새로 작성하는
   코드에서는 이 두 파일을 직접 쓰지 말고 InterviewDbManager만 쓰면 됨.
================================================================================
*/

// ============================================================
// InterviewDbManager.cs
// ------------------------------------------------------------
// 한종수(AI/세션) · 신모세(태도/비전) · 한효준(UI/대시보드) 
// 3개 파트 연동 규격이 통합된 DB 통로 코드
// ============================================================
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using SQLite;
using InterviewDb.Models;
using InterviewDb.Core; // SchemaBootstrapHardened 네임스페이스

namespace InterviewDb.API
{
    [DisallowMultipleComponent]
    public class InterviewDbManager : MonoBehaviour
    {
        private static InterviewDbManager _instance;
        public static InterviewDbManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<InterviewDbManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[InterviewDbManager]");
                        _instance = go.AddComponent<InterviewDbManager>();
                    }
                }
                return _instance;
            }
        }

        [Header("SQLite 설정")]
        [SerializeField] private string dbFileName = "InterviewDatabase.db";
        private SQLiteConnection _connection;

        /// <summary>현재 진행 중인 세션 ID (결과 저장 시 자동 타겟팅용)</summary>
        public int CurrentSessionId { get; private set; } = -1;
        public SQLiteConnection Connection => _connection;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        /// <summary>DB 연결 생성 및 SchemaBootstrapHardened 스키마 적용</summary>
        public void Initialize()
        {
            try
            {
                string dbPath = Path.Combine(Application.persistentDataPath, dbFileName);
                _connection = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

                _connection.Execute("PRAGMA foreign_keys = ON;");
                _connection.Execute("PRAGMA journal_mode = WAL;");

                // 최신 강화 스키마 DDL 적용
                SchemaBootstrapHardened.ApplySchema(_connection);
                Debug.Log($"[InterviewDbManager] DB 초기화 완료: {dbPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InterviewDbManager] DB 초기화 오류: {ex.Message}");
            }
        }

        // ============================================================
        // [1] 한종수 팀장님 파이프라인 (InterviewManager, InterviewResultSaver)
        // ============================================================

        /// <summary>
        /// 면접 시작 시 호출: 세션을 생성하고 발급된 ID를 CurrentSessionId에 보관.
        /// (DDL 호환을 위해 직무와 면접관 유형을 job_category 컬럼에 안전하게 병합)
        /// </summary>
        public int StartSession(string jobCategory, string interviewType = "")
        {
            CurrentSessionId = -1;
            ExecuteSafe(() =>
            {
                string combinedJob = string.IsNullOrEmpty(interviewType)
                    ? jobCategory
                    : $"{jobCategory} ({interviewType})";

                string startTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                string sql = "INSERT INTO Interview_Session (job_category, session_status, start_time) VALUES (?, 'In-Progress', ?);";

                _connection.Execute(sql, combinedJob, startTime);
                CurrentSessionId = (int)SQLite3.LastInsertRowid(_connection.Handle);
                Debug.Log($"[InterviewDbManager] 면접 세션 시작 - ID: {CurrentSessionId} ({combinedJob})");
            });
            return CurrentSessionId;
        }

        /// <summary>면접 중단(나가기 버튼 등) 시 세션 상태를 'Aborted'로 마감.</summary>
        public void AbortSession(int sessionId = -1)
        {
            int targetId = sessionId > 0 ? sessionId : CurrentSessionId;
            if (targetId <= 0) return;

            ExecuteSafe(() =>
            {
                string endTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                _connection.Execute("UPDATE Interview_Session SET end_time = ?, session_status = 'Aborted' WHERE session_id = ?;", endTime, targetId);
                Debug.Log($"[InterviewDbManager] 세션 {targetId} 강제 종료 마감");
            });
        }

        /// <summary>
        /// 면접 종료 시 호출: Gemini 파싱 결과(음성/내용) 및 대화 로그를 트랜잭션으로 일괄 적재.
        /// sessionId에 -1을 넣으면 현재 활성화된 CurrentSessionId에 저장.
        /// </summary>
        public bool SaveInterviewResult(
            int sessionId,
            double? scoreAudio, string evalAudioText, string adviceAudioText,
            double? scoreContent, string evalContentText, string adviceContentText,
            string conversationLogJson)
        {
            int targetId = sessionId > 0 ? sessionId : CurrentSessionId;
            if (targetId <= 0)
            {
                Debug.LogError("[InterviewDbManager] 유효한 session_id가 없어 결과를 저장할 수 없습니다.");
                return false;
            }

            bool success = false;
            ExecuteSafe(() =>
            {
                _connection.BeginTransaction();
                try
                {
                    string endTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                    // 1) 세션 상태 Completed 및 대화 로그 저장
                    _connection.Execute(
                        "UPDATE Interview_Session SET end_time = ?, session_status = 'Completed', conversation_log = ? WHERE session_id = ?;",
                        endTime, conversationLogJson, targetId);

                    // 2) 음성/내용 결과 적재 (기존 태도 점수가 먼저 들어가 있어도 안전하게 보존)
                    string sql = @"
                        INSERT INTO Session_Result (
                            session_id, score_audio, eval_audio_text, advice_audio_text,
                            score_content, eval_content_text, advice_content_text
                        ) VALUES (?, ?, ?, ?, ?, ?, ?)
                        ON CONFLICT(session_id) DO UPDATE SET
                            score_audio = excluded.score_audio,
                            eval_audio_text = excluded.eval_audio_text,
                            advice_audio_text = excluded.advice_audio_text,
                            score_content = excluded.score_content,
                            eval_content_text = excluded.eval_content_text,
                            advice_content_text = excluded.advice_content_text;";

                    _connection.Execute(sql, targetId, scoreAudio, evalAudioText, adviceAudioText, scoreContent, evalContentText, adviceContentText);
                    _connection.Commit();
                    success = true;

                    // 총점 수동 계산 및 갱신 (트리거 제거 대응)
                    UpdateTotalScoreInternal(targetId);
                    Debug.Log($"[InterviewDbManager] 세션 {targetId} 음성/내용 적재 완료");
                }
                catch (Exception ex)
                {
                    _connection.Rollback();
                    Debug.LogError($"[InterviewDbManager] 결과 저장 트랜잭션 롤백: {ex.Message}");
                }
            });
            return success;
        }

        // ============================================================
        // [2] 신모세 팀원 파이프라인 (MediaPipe 안면/태도 분석)
        // ============================================================

        /// <summary>
        /// 미디어파이프 안면 분석 종료 시 호출: 5.0 만점 척도의 태도 점수와 피드백 텍스트를 적재.
        /// </summary>
        public bool SaveFaceEvaluation(int sessionId, double scoreAttitude, string adviceAttitudeText)
        {
            int targetId = sessionId > 0 ? sessionId : CurrentSessionId;
            if (targetId <= 0) return false;

            bool success = false;
            ExecuteSafe(() =>
            {
                try
                {
                    string sql = @"
                        INSERT INTO Session_Result (session_id, score_attitude, advice_text)
                        VALUES (?, ?, ?)
                        ON CONFLICT(session_id) DO UPDATE SET
                            score_attitude = excluded.score_attitude,
                            advice_text = excluded.advice_text;";

                    _connection.Execute(sql, targetId, scoreAttitude, adviceAttitudeText);
                    success = true;

                    // 총점 수동 계산 및 갱신
                    UpdateTotalScoreInternal(targetId);
                    Debug.Log($"[InterviewDbManager] 세션 {targetId} 태도 점수({scoreAttitude:F1}) 적재 완료");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[InterviewDbManager] 태도 데이터 저장 실패: {ex.Message}");
                }
            });
            return success;
        }

        // ============================================================
        // [3] 한효준 팀원 파이프라인 (ResultUI, FeedbackListUI)
        // ============================================================

        /// <summary>
        /// ResultUI 전용: 씬 전환 후 특정 session_id를 전달받지 못했을 때,
        /// 가장 최근에 완료된 면접 결과 리포트를 즉시 가져옴.
        /// </summary>
        public SessionReportRow GetLatestSessionReport()
        {
            SessionReportRow report = null;
            ExecuteSafe(() =>
            {
                string sql = "SELECT * FROM View_Session_Report ORDER BY session_id DESC LIMIT 1;";
                var list = _connection.Query<SessionReportRow>(sql);
                if (list != null && list.Count > 0) report = list[0];
            });
            return report;
        }

        /// <summary>특정 회차 세션 리포트 1건 조회</summary>
        public SessionReportRow GetSessionReport(int sessionId)
        {
            SessionReportRow report = null;
            ExecuteSafe(() =>
            {
                var list = _connection.Query<SessionReportRow>("SELECT * FROM View_Session_Report WHERE session_id = ? LIMIT 1;", sessionId);
                if (list != null && list.Count > 0) report = list[0];
            });
            return report;
        }

        /// <summary>마이페이지/피드백 기록실: 저장된 전체 면접 이력 조회 (최신순 정렬)</summary>
        public List<SessionReportRow> GetAllSessionReports()
        {
            List<SessionReportRow> reports = new List<SessionReportRow>();
            ExecuteSafe(() =>
            {
                reports = _connection.Query<SessionReportRow>("SELECT * FROM View_Session_Report ORDER BY start_time DESC;");
            });
            return reports;
        }

        /// <summary>마이페이지: 특정 면접 세션 삭제 (CASCADE 연쇄 삭제 적용)</summary>
        public bool DeleteSession(int sessionId)
        {
            bool success = false;
            ExecuteSafe(() =>
            {
                int affected = _connection.Execute("DELETE FROM Interview_Session WHERE session_id = ?;", sessionId);
                success = affected > 0;
                Debug.Log($"[InterviewDbManager] 세션 {sessionId} 삭제 완료");
            });
            return success;
        }

        // ============================================================
        // 내부 유틸리티: total_score 자동 수동 계산 및 스레드 디스패치
        // ============================================================

        private void UpdateTotalScoreInternal(int sessionId)
        {
            try
            {
                var rows = _connection.Query<SessionReportRow>("SELECT score_audio, score_content, score_attitude FROM Session_Result WHERE session_id = ? LIMIT 1;", sessionId);
                if (rows != null && rows.Count > 0)
                {
                    var r = rows[0];
                    double sum = 0.0;
                    int count = 0;

                    if (r.ScoreAudio.HasValue) { sum += r.ScoreAudio.Value; count++; }
                    if (r.ScoreContent.HasValue) { sum += r.ScoreContent.Value; count++; }
                    if (r.ScoreAttitude.HasValue) { sum += r.ScoreAttitude.Value; count++; }

                    if (count > 0)
                    {
                        double avg = Math.Round(sum / count, 1);
                        _connection.Execute("UPDATE Session_Result SET total_score = ? WHERE session_id = ?;", avg, sessionId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InterviewDbManager] total_score 계산 중 예외: {ex.Message}");
            }
        }

        private void ExecuteSafe(Action action)
        {
            // MainThreadDbDispatcher의 Action<SQLiteConnection> 규격 호환
            if (MainThreadDbDispatcher.Instance != null)
            {
                MainThreadDbDispatcher.Instance.Enqueue(_ => action());
            }
            else
            {
                lock (_connection)
                {
                    action?.Invoke();
                }
            }
        }

        private void OnDestroy()
        {
            if (_connection != null)
            {
                _connection.Close();
                _connection.Dispose();
                _connection = null;
            }
        }
    }
}
// ============================================================
// InterviewDbManager.cs
// ------------------------------------------------------------
// 
// 1. PRAGMA journal_mode = WAL 실행 시 ExecuteScalar<string> 적용 (Crash 방어)
// 2. SetTotalScore() 추가 및 SaveFaceEvaluation 캐시 양방향 동기화
// 3. start_time 제거, end_time 및 duration_seconds 물리 컬럼 저장 반영
// 4. 메인 스레드 즉각 동기 실행 보장 (디스패처 지연으로 인한 반환값 누락 방어)
// 5. conversation_log 빈 문자열 시 JSON 트리거 크래시 방어
//
// ============================================================
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using SQLite;
using InterviewDb.Models;
using InterviewDb.Core;

namespace InterviewDb
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

        private SQLiteConnection _connection;
        private SessionReportRow _latestCachedReport;
        private DateTime _sessionStartTime;
        private int _mainThreadId;

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
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DontDestroyOnLoad(gameObject);
            InitializeDatabase();
        }

        /// <summary>
        /// SQLite 연결 수립 및 스키마 DDL 적용
        /// </summary>
        public void InitializeDatabase()
        {
            try
            {
                string dbPath = Path.Combine(Application.persistentDataPath, "InterviewDatabase.db");
                _connection = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

                // 1) 외래키 제약조건 활성화 (반환값 없음 -> Execute)
                _connection.Execute("PRAGMA foreign_keys = ON;");

                // 2) WAL 모드 활성화 (문자열 "wal" 반환 -> ExecuteScalar<string> 필수)
                _connection.ExecuteScalar<string>("PRAGMA journal_mode = WAL;");

                // 3) SchemaBootstrapHardened 스키마 DDL 적용
                SchemaBootstrapHardened.ApplySchema(_connection);
                Debug.Log($"[InterviewDbManager] DB 초기화 완료: {dbPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InterviewDbManager] DB 초기화 실패: {ex.Message}");
            }
        }

        // ============================================================
        // 한종수 팀장 연동 통로 (면접 세션 시작 / 중단 / 음성·내용 결과 적재)
        // ============================================================

        /// <summary>
        /// 면접 시작 시 호출: 세션 레코드를 생성하고 발급된 ID를 반환 및 보관합니다.
        /// </summary>
        public int StartSession(string jobCategory, string interviewType = "")
        {
            CurrentSessionId = -1;
            _latestCachedReport = null;
            _sessionStartTime = DateTime.UtcNow;

            ExecuteSafe(() =>
            {
                string combinedJob = string.IsNullOrEmpty(interviewType) ? jobCategory : $"{jobCategory} ({interviewType})";
                string sql = "INSERT INTO Interview_Session (job_category, session_status) VALUES (?, 'In-Progress');";
                _connection.Execute(sql, combinedJob);

                CurrentSessionId = _connection.ExecuteScalar<int>("SELECT last_insert_rowid();");
                Debug.Log($"[InterviewDbManager] 세션 발급 완료 (ID: {CurrentSessionId})");
            });

            return CurrentSessionId;
        }

        /// <summary>
        /// 면접 중단(나가기 버튼 등) 시 세션 상태를 'Aborted'로 정상 마감합니다.
        /// </summary>
        public void AbortSession(int sessionId = -1)
        {
            int targetId = sessionId > 0 ? sessionId : CurrentSessionId;
            if (targetId <= 0) return;

            ExecuteSafe(() =>
            {
                string endTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                int duration = _sessionStartTime != default
                    ? (int)Math.Max(0, (DateTime.UtcNow - _sessionStartTime).TotalSeconds)
                    : 0;

                _connection.Execute("UPDATE Interview_Session SET end_time = ?, duration_seconds = ?, session_status = 'Aborted' WHERE session_id = ?;", endTime, duration, targetId);
                Debug.Log($"[InterviewDbManager] 세션 {targetId} 중단 처리 완료");
            });
        }

        /// <summary>
        /// Gemini 파싱 결과 및 대화 로그를 트랜잭션으로 일괄 영구 저장합니다.
        /// Result 씬 즉시 표출을 위해 인메모리 캐시도 함께 동기화됩니다.
        /// </summary>
        public bool SaveInterviewResult(
            int sessionId,
            double? scoreAudio, string evalAudioText, string adviceAudioText,
            double? scoreContent, string evalContentText, string adviceContentText,
            string conversationLogJson,
            int customDurationSeconds = -1)
        {
            int targetId = sessionId > 0 ? sessionId : CurrentSessionId;
            if (targetId <= 0) return false;

            string endTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            int duration = customDurationSeconds >= 0
                ? customDurationSeconds
                : (_sessionStartTime != default ? (int)Math.Max(0, (DateTime.UtcNow - _sessionStartTime).TotalSeconds) : 0);

            // JSON 빈 문자열("") 유입 시 SQLite json_valid 트리거 크래시 방어 (null 치환)
            string safeLogJson = string.IsNullOrWhiteSpace(conversationLogJson) ? null : conversationLogJson;

            // 1) Result 씬 즉시 표출용 캐시 갱신 (태도 점수가 먼저 들어와 있어도 값 보존)
            if (_latestCachedReport == null || _latestCachedReport.SessionId != targetId)
            {
                _latestCachedReport = new SessionReportRow { SessionId = targetId };
            }
            _latestCachedReport.EndTime = endTime;
            _latestCachedReport.DurationSeconds = duration;
            _latestCachedReport.ScoreAudio = scoreAudio;
            _latestCachedReport.EvalAudioText = evalAudioText;
            _latestCachedReport.AdviceAudioText = adviceAudioText;
            _latestCachedReport.ScoreContent = scoreContent;
            _latestCachedReport.EvalContentText = evalContentText;
            _latestCachedReport.AdviceContentText = adviceContentText;
            _latestCachedReport.ConversationLog = safeLogJson;

            // 2) SQLite 영구 저장 트랜잭션
            bool success = false;
            ExecuteSafe(() =>
            {
                _connection.BeginTransaction();
                try
                {
                    _connection.Execute(
                        "UPDATE Interview_Session SET end_time = ?, duration_seconds = ?, session_status = 'Completed', conversation_log = ? WHERE session_id = ?;",
                        endTime, duration, safeLogJson, targetId);

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
                    Debug.Log($"[InterviewDbManager] 세션 {targetId} 음성/내용 적재 완료");
                }
                catch (Exception ex)
                {
                    _connection.Rollback();
                    Debug.LogError($"[InterviewDbManager] 저장 롤백: {ex.Message}");
                }
            });

            return success;
        }

        // ============================================================
        // 신모세 팀원 연동 통로 (태도 점수 / 종합 점수 저장)
        // ============================================================

        /// <summary>
        /// 미디어파이프 태도 점수(0~5점) 및 개선 조언/평가 텍스트 적재
        /// </summary>
        public bool SaveFaceEvaluation(int sessionId, double scoreAttitude, string adviceAttitudeText, string evalAttitudeText = null)
        {
            int targetId = sessionId > 0 ? sessionId : CurrentSessionId;
            if (targetId <= 0) return false;

            // Result 씬 캐시 즉시 동기화
            if (_latestCachedReport == null || _latestCachedReport.SessionId != targetId)
            {
                _latestCachedReport = new SessionReportRow { SessionId = targetId };
            }
            _latestCachedReport.ScoreAttitude = scoreAttitude;
            _latestCachedReport.AdviceText = adviceAttitudeText;
            if (!string.IsNullOrEmpty(evalAttitudeText))
            {
                _latestCachedReport.SummaryText = evalAttitudeText;
            }

            bool success = false;
            ExecuteSafe(() =>
            {
                try
                {
                    string sql = @"
                        INSERT INTO Session_Result (session_id, score_attitude, advice_text, summary_text)
                        VALUES (?, ?, ?, ?)
                        ON CONFLICT(session_id) DO UPDATE SET
                            score_attitude = excluded.score_attitude,
                            advice_text = excluded.advice_text,
                            summary_text = COALESCE(excluded.summary_text, Session_Result.summary_text);";

                    _connection.Execute(sql, targetId, scoreAttitude, adviceAttitudeText, evalAttitudeText);
                    success = true;
                    Debug.Log($"[InterviewDbManager] 세션 {targetId} 태도 점수({scoreAttitude:F1}) 적재 완료");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[InterviewDbManager] 태도 데이터 저장 실패: {ex.Message}");
                }
            });

            return success;
        }

        /// <summary>
        /// 외부 모듈에서 계산된 최종 종합 점수(total_score)를 DB와 캐시에 저장
        /// </summary>
        public bool SetTotalScore(int sessionId, double totalScore)
        {
            int targetId = sessionId > 0 ? sessionId : CurrentSessionId;
            if (targetId <= 0) return false;

            if (_latestCachedReport != null && _latestCachedReport.SessionId == targetId)
            {
                _latestCachedReport.TotalScore = totalScore;
            }

            bool success = false;
            ExecuteSafe(() =>
            {
                try
                {
                    int affected = _connection.Execute("UPDATE Session_Result SET total_score = ? WHERE session_id = ?;", totalScore, targetId);
                    success = affected > 0;
                    Debug.Log($"[InterviewDbManager] 세션 {targetId} 종합 점수({totalScore:F1}) 갱신 완료");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[InterviewDbManager] 종합 점수 갱신 실패: {ex.Message}");
                }
            });

            return success;
        }

        // ============================================================
        // 한효준 팀원 연동 통로 (Result UI 표출 및 대시보드 관리)
        // ============================================================

        /// <summary>
        /// Result 씬 전용: 메모리 캐시가 있으면 즉시 반환, 없을 경우 DB 최신 회차 조회
        /// </summary>
        public SessionReportRow GetLatestSessionReport()
        {
            if (_latestCachedReport != null && _latestCachedReport.ScoreAudio.HasValue)
            {
                return _latestCachedReport;
            }

            SessionReportRow report = null;
            ExecuteSafe(() =>
            {
                var list = _connection.Query<SessionReportRow>("SELECT * FROM View_Session_Report ORDER BY session_id DESC LIMIT 1;");
                if (list != null && list.Count > 0) report = list[0];
            });

            return report ?? _latestCachedReport;
        }

        /// <summary>
        /// 마이페이지 기록실: 면접 종료 시각(end_time DESC) 기준 최신순 정렬 반환
        /// </summary>
        public List<SessionReportRow> GetAllSessionReports()
        {
            List<SessionReportRow> reports = new List<SessionReportRow>();
            ExecuteSafe(() =>
            {
                reports = _connection.Query<SessionReportRow>("SELECT * FROM View_Session_Report ORDER BY end_time DESC, session_id DESC;");
            });
            return reports;
        }

        /// <summary>
        /// 마이페이지 기록실: 특정 면접 세션 삭제 (CASCADE 연쇄 삭제 작동)
        /// </summary>
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
        // 스레드 안전성 보장 내부 실행기
        // ============================================================

        private void ExecuteSafe(Action action)
        {
            if (action == null) return;

            // 메인 스레드 호출 시 지연 없이 즉각 동기 실행 (반환값 및 ID 누락 방어)
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                lock (_connection)
                {
                    action();
                }
                return;
            }

            // 백그라운드 스레드 호출 시 MainThreadDbDispatcher 큐 경유
            if (MainThreadDbDispatcher.Instance != null)
            {
                MainThreadDbDispatcher.Instance.Enqueue(_ => action());
            }
            else
            {
                lock (_connection)
                {
                    action();
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
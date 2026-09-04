// ============================================================
// InterviewDbManager.cs
// ------------------------------------------------------------
// [버그 수정 반영본]
// - start_time 제거 / end_time 및 duration_seconds 적재 지원
// - _sessionStartTime을 통한 경과 시간 자동 산출
// - 세션 종료 시 end_time과 duration_seconds 동시 UPDATE
// - 면접 결과 리스트 end_time DESC 정렬 반영.
// 1. PRAGMA journal_mode = WAL 실행 시 ExecuteScalar<string> 적용 (Crash 방어)
// 2. SetTotalScore() 메서드 추가 및 SaveFaceEvaluation 캐시 동기화
// 3. UTF-8 인코딩 지원 및 무상태/캐시 통합
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
            InitializeDatabase();
        }

        /// <summary>
        /// SQLite 연결 및 스키마 적용
        /// </summary>
        public void InitializeDatabase()
        {
            try
            {
                string dbPath = Path.Combine(Application.persistentDataPath, "InterviewDatabase.db");
                _connection = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

                // 1) 외래키 제약 활성화 (반환값 없음 -> Execute)
                _connection.Execute("PRAGMA foreign_keys = ON;");

                // 2) [버그1 해결] WAL 모드는 "wal" 문자열을 반환하므로 ExecuteScalar<string>으로 실행해야 크래시가 안 남
                _connection.ExecuteScalar<string>("PRAGMA journal_mode = WAL;");

                // 3) 최신 스키마 DDL 적용
                SchemaBootstrapHardened.ApplySchema(_connection);
                Debug.Log($"[InterviewDbManager] DB 초기화 완료: {dbPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InterviewDbManager] DB 초기화 실패: {ex.Message}");
            }
        }

        // ============================================================
        // 한종수 팀장 연동 통로 (세션 시작 / 중단 / 음성·내용 결과 적재)
        // ============================================================

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

        public void AbortSession(int sessionId = -1)
        {
            int targetId = sessionId > 0 ? sessionId : CurrentSessionId;
            if (targetId <= 0) return;

            ExecuteSafe(() =>
            {
                string endTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                int duration = (int)Math.Max(0, (DateTime.UtcNow - _sessionStartTime).TotalSeconds);
                _connection.Execute("UPDATE Interview_Session SET end_time = ?, duration_seconds = ?, session_status = 'Aborted' WHERE session_id = ?;", endTime, duration, targetId);
                Debug.Log($"[InterviewDbManager] 세션 {targetId} 중단 처리 마감");
            });
        }

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
            int duration = customDurationSeconds >= 0 ? customDurationSeconds : (int)Math.Max(0, (DateTime.UtcNow - _sessionStartTime).TotalSeconds);

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
            _latestCachedReport.ConversationLog = conversationLogJson;

            // 2) SQLite 영구 저장
            bool success = false;
            ExecuteSafe(() =>
            {
                _connection.BeginTransaction();
                try
                {
                    _connection.Execute(
                        "UPDATE Interview_Session SET end_time = ?, duration_seconds = ?, session_status = 'Completed', conversation_log = ? WHERE session_id = ?;",
                        endTime, duration, conversationLogJson, targetId);

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
        /// [버그2 해결] 미디어파이프 태도 점수(0~5점) 및 피드백 텍스트 적재
        /// </summary>
        public bool SaveFaceEvaluation(int sessionId, double scoreAttitude, string adviceAttitudeText)
        {
            int targetId = sessionId > 0 ? sessionId : CurrentSessionId;
            if (targetId <= 0) return false;

            // Result 씬 캐시 동기화
            if (_latestCachedReport == null || _latestCachedReport.SessionId != targetId)
            {
                _latestCachedReport = new SessionReportRow { SessionId = targetId };
            }
            _latestCachedReport.ScoreAttitude = scoreAttitude;
            _latestCachedReport.AdviceText = adviceAttitudeText;

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
        /// [버그2 해결] 외부에서 계산된 종합 점수(total_score)를 DB와 캐시에 저장
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

        public List<SessionReportRow> GetAllSessionReports()
        {
            List<SessionReportRow> reports = new List<SessionReportRow>();
            ExecuteSafe(() =>
            {
                reports = _connection.Query<SessionReportRow>("SELECT * FROM View_Session_Report ORDER BY end_time DESC, session_id DESC;");
            });
            return reports;
        }

        public bool DeleteSession(int sessionId)
        {
            bool success = false;
            ExecuteSafe(() =>
            {
                int affected = _connection.Execute("DELETE FROM Interview_Session WHERE session_id = ?;", sessionId);
                success = affected > 0;
            });
            return success;
        }

        private void ExecuteSafe(Action action)
        {
            if (MainThreadDbDispatcher.Instance != null)
            {
                MainThreadDbDispatcher.Instance.Enqueue(_ => action());
            }
            else
            {
                lock (_connection) { action?.Invoke(); }
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
// ============================================================
// ThreadSafetyStressTest.cs
// ------------------------------------------------------------
// 목적: gilzoide(sqlite-net)를 실제 여러 스레드에서 동시에 두드렸을 때
//       어떤 문제가 나는지 두 가지 패턴으로 비교 관찰.
//
//   ① Run: Shared Connection  — 위험한 패턴. 하나의 SQLiteConnection
//      객체를 여러 스레드가 동시에 공유해서 사용.
//   ② Run: Connection Per Thread — 권장 패턴. 스레드마다 자기 자신의
//      SQLiteConnection을 새로 열어서(같은 파일에 대해) 사용.
//
// ⚠ 이 스크립트는 "만약 백그라운드 스레드에서 DB를 직접 건드리면 어떻게
//   되는가"를 보여줄 뿐입니다. 실제로 Gemini API 콜백이나 MediaPipe 처리
//   스레드가 DB를 직접 호출하고 있는지는 해당 모듈 코드를 봐야 알 수 있고,
//   이 스크립트로는 확인할 수 없음 — 그 부분은 팀원 확인이 필요.
// ============================================================
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using UnityEngine;
using InterviewDb.Core;
using InterviewDb.Models;

namespace InterviewDb.Testing
{
    public class ThreadSafetyStressTest : MonoBehaviour
    {
        [Tooltip("스트레스 테스트 전용 DB 파일 경로. 비워두면 persistentDataPath에 자동 생성됩니다.")]
        public string databasePath = "";

        [Tooltip("동시에 띄울 스레드(작업) 개수")]
        public int threadCount = 8;

        private readonly ConcurrentBag<string> _log = new ConcurrentBag<string>();

        [ContextMenu("Run: Shared Connection (위험한 패턴)")]
        public void RunSharedConnectionTest()
        {
            _log.Clear();
            string path = ResolvePath("interview_thread_shared.db");

            using (var sharedConn = new SQLiteConnection(path))
            {
                SchemaBootstrap.ApplySchema(sharedConn);

                var tasks = new Task[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    int idx = i;
                    tasks[i] = Task.Run(() => WorkerUsingSharedConnection(sharedConn, idx));
                }

                WaitAndCollectErrors(tasks);
            }

            Report("① 하나의 SQLiteConnection 객체를 여러 스레드가 동시에 공유");
        }

        [ContextMenu("Run: Connection Per Thread (권장 패턴)")]
        public void RunPerThreadConnectionTest()
        {
            _log.Clear();
            string path = ResolvePath("interview_thread_perconn.db");

            // 스키마는 메인 스레드에서 미리 한 번만 준비
            using (var initConn = new SQLiteConnection(path))
            {
                SchemaBootstrap.ApplySchema(initConn);
            }

            var tasks = new Task[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                int idx = i;
                tasks[i] = Task.Run(() => WorkerUsingOwnConnection(path, idx));
            }

            WaitAndCollectErrors(tasks);

            Report("② 스레드마다 자기 자신의 SQLiteConnection을 새로 열어서 사용 (같은 파일)");
        }

        private void WorkerUsingSharedConnection(SQLiteConnection conn, int idx)
        {
            try
            {
                conn.Insert(new InterviewSession
                {
                    JobCategory = $"스레드{idx}",
                    SessionStatus = "Completed",
                    StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
                _log.Add($"[스레드 {idx}] 성공 (ManagedThreadId={Thread.CurrentThread.ManagedThreadId})");
            }
            catch (Exception ex)
            {
                _log.Add($"[스레드 {idx}] 실패: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void WorkerUsingOwnConnection(string path, int idx)
        {
            try
            {
                using (var conn = new SQLiteConnection(path))
                {
                    // ⚠ PRAGMA busy_timeout = ...; 도 journal_mode와 마찬가지로
                    //   SET 할 때 적용된 값을 결과 행으로 반환하므로 Execute() 대신
                    //   ExecuteScalar로 실행해야 "not an error" 예외를 피할 수 있음
                    conn.ExecuteScalar<int>("PRAGMA busy_timeout = 3000;");

                    conn.Insert(new InterviewSession
                    {
                        JobCategory = $"스레드{idx}",
                        SessionStatus = "Completed",
                        StartTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
                _log.Add($"[스레드 {idx}] 성공 (ManagedThreadId={Thread.CurrentThread.ManagedThreadId})");
            }
            catch (Exception ex)
            {
                _log.Add($"[스레드 {idx}] 실패: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void WaitAndCollectErrors(Task[] tasks)
        {
            try
            {
                Task.WaitAll(tasks);
            }
            catch (AggregateException ae)
            {
                foreach (var inner in ae.InnerExceptions)
                    _log.Add($"[미처리 예외] {inner.GetType().Name}: {inner.Message}");
            }
        }

        private string ResolvePath(string defaultFileName)
        {
            return string.IsNullOrEmpty(databasePath)
                ? Path.Combine(Application.persistentDataPath, defaultFileName)
                : databasePath;
        }

        private void Report(string title)
        {
            int success = 0, fail = 0;
            foreach (var line in _log)
            {
                if (line.Contains("성공")) success++;
                else fail++;
            }

            Debug.Log($"=== {title} ===\n성공 {success} / 실패 {fail} (총 {threadCount})\n" +
                      string.Join("\n", _log));
        }
    }
}

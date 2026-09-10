// ============================================================
// MainThreadDbDispatcher.cs
// ------------------------------------------------------------
// 목적: 어떤 스레드(코루틴, Task, Gemini API 콜백, MediaPipe 처리 스레드 등)
//       에서 호출하든, 실제 SQLite 접근은 항상 메인 스레드 하나에서만
//       일어나도록 강제하는 큐 기반 디스패처.
//
// 사용법:
//   1) 씬에 이 컴포넌트를 붙인 GameObject를 하나만 두고 (싱글턴).
//   2) 어디서든(백그라운드 스레드 포함) 아래처럼 호출하면 됨:
//
//        MainThreadDbDispatcher.Instance.Enqueue(conn =>
//        {
//            conn.Insert(new InterviewSession { ... });
//        });
//
//      → 실제 실행은 항상 다음 Update()에서, 메인 스레드에서 일어나게 됨.
// ============================================================
using System;
using System.Collections.Concurrent;
using System.IO;
using SQLite;
using UnityEngine;

namespace InterviewDb.Core
{
    public class MainThreadDbDispatcher : MonoBehaviour
    {
        public static MainThreadDbDispatcher Instance { get; private set; }

        [Tooltip("이 디스패처가 관리할 DB 파일 경로. 비워두면 persistentDataPath/interview.db 사용")]
        public string databasePath = "";

        [Tooltip("한 프레임(Update)에서 처리할 최대 작업 개수 — 순간적으로 큐가 몰려도 프레임 스파이크 방지")]
        public int maxJobsPerFrame = 50;

        private SQLiteConnection _conn;
        private readonly ConcurrentQueue<Action<SQLiteConnection>> _queue = new ConcurrentQueue<Action<SQLiteConnection>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            string path = string.IsNullOrEmpty(databasePath)
                ? Path.Combine(Application.persistentDataPath, "interview.db")
                : databasePath;

            // 메인 스레드(Awake)에서만 연결을 생성 — 이 연결은 이후 절대 다른 스레드에서 직접 쓰지 않음
            _conn = new SQLiteConnection(path);
            SchemaBootstrapHardened.ApplySchema(_conn);
        }

        /// <summary>
        /// 어떤 스레드에서 호출해도 안전합니다(ConcurrentQueue 사용).
        /// 실제 DB 작업은 큐에만 쌓이고, 다음 Update()에서 메인 스레드로 순차 실행됨.
        /// </summary>
        public void Enqueue(Action<SQLiteConnection> dbWork)
        {
            if (dbWork == null) return;
            _queue.Enqueue(dbWork);
        }

        private void Update()
        {
            int processed = 0;
            while (processed < maxJobsPerFrame && _queue.TryDequeue(out var work))
            {
                try
                {
                    work(_conn);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MainThreadDbDispatcher] DB 작업 중 예외 발생: {ex}");
                }
                processed++;
            }
        }

        private void OnDestroy()
        {
            _conn?.Close();
        }
    }
}

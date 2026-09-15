using System.Collections.Generic;
using UnityEngine;
using InterviewDb;
using InterviewDb.Models;

public class FeedbackManager : MonoBehaviour
{
    public static FeedbackManager Instance { get; private set; }

    // Result 씬 전달용 (선택된 DB 레코드)
    public SessionReportRow CurrentSelectedFeedback { get; set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// SQLite DB에서 전체 면접 리포트 목록을 조회합니다.
    /// </summary>
    public List<SessionReportRow> GetFeedbackList()
    {
        if (InterviewDbManager.Instance != null)
        {
            return InterviewDbManager.Instance.GetAllSessionReports();
        }
        return new List<SessionReportRow>();
    }

    /// <summary>
    /// SQLite DB에서 특정 면접 세션을 연쇄 삭제(CASCADE)합니다.
    /// </summary>
    public void RemoveFeedback(SessionReportRow targetData)
    {
        if (targetData == null) return;

        if (InterviewDbManager.Instance != null)
        {
            InterviewDbManager.Instance.DeleteSession(targetData.SessionId);
        }
    }
}

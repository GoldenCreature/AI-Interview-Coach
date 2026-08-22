using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class FeedbackData
{
    public string dateText; // 날짜 (예: 2026-07-16)
    public string jobText;  // 직무 (예: IT)
    public string typeText; // 유형 (예: 일상적)
}
[Serializable]
public class FeedbackListWrapper
{
    public List<FeedbackData> list = new List<FeedbackData>();
}
public class FeedbackManager : MonoBehaviour
{
    public static FeedbackManager Instance { get; private set; }
    
    // 버튼의 데이터를 저장할 임시 변수 
    public FeedbackData CurrentSelectedFeedback { get; set; }

    private List<FeedbackData> feedbackList = new List<FeedbackData>();
    private string filePath;

    public void RemoveFeedback(FeedbackData targetData)
    {
        var list = GetFeedbackList();

        if (list.Contains(targetData))
        {
            list.Remove(targetData);
        }

        SaveFeedbacks();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 이동해도 파괴되지 않음

            // 프로그램이 종료되어도 유지되는 영구 저장 경로
            filePath = Path.Combine(Application.persistentDataPath, "FeedbackLogs.json");
            LoadFeedbacks();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void AddFeedback(FeedbackData newData)
    {
        feedbackList.Insert(0, newData); // 최신 데이터가 위에 오도록 추가
        SaveFeedbacks();
    }

    public List<FeedbackData> GetFeedbackList()
    {
        return feedbackList;
    }

    private void SaveFeedbacks()
    {
        try
        {
            FeedbackListWrapper wrapper = new FeedbackListWrapper { list = feedbackList };
            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(filePath, json);
            Debug.Log($"[FeedbackManager] 파일 저장 완료: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FeedbackManager] 저장 실패: {e.Message}");
        }
    }

    private void LoadFeedbacks()
    {
        try
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                FeedbackListWrapper wrapper = JsonUtility.FromJson<FeedbackListWrapper>(json);
                if (wrapper != null && wrapper.list != null)
                {
                    feedbackList = wrapper.list;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[FeedbackManager] 로드 실패: {e.Message}");
        }
    }
}

using HJS;
using InterviewDb;
using InterviewDb.Models;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FeedbackItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI dateText;
    [SerializeField] private TextMeshProUGUI jobTypeText;

    [SerializeField] private Button mainButton;
    [SerializeField] private Button deleteButton;

    private SessionReportRow myData;

    public void Setup(SessionReportRow data, Action onDeleteClick)
    {
        myData = data;

        if (dateText != null)
        {
            // DB의 end_time 표출 (예: 2026-09-15 14:30:00)
            dateText.text = !string.IsNullOrEmpty(myData.EndTime) ? myData.EndTime : "일시 정보 없음";
        }

        if (jobTypeText != null)
        {
            string categoryText = myData.JobCategory ?? "";

            // 영어 면접 유형 명칭을 한글로 치환
            categoryText = categoryText.Replace("Casual", "일반")
                                       .Replace("Intensive", "심화");

            jobTypeText.text = !string.IsNullOrEmpty(categoryText)
                ? $"직무/유형 : {categoryText}"
                : "직무 정보 없음";
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => onDeleteClick?.Invoke());
        }

        if (mainButton != null)
        {
            mainButton.onClick.RemoveAllListeners();
            mainButton.onClick.AddListener(GoToResultScene);
        }
    }

    private void GoToResultScene()
    {
        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.CurrentSelectedFeedback = myData;
        }

        GameManager.Instance.LoadResultScene();
    }
}

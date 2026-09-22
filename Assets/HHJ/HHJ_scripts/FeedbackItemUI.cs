using HJS;
using InterviewDb;
using InterviewDb.Models;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FeedbackItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI dateText;      // 예: 2026-07-25
    [SerializeField] private TextMeshProUGUI timeText;      // 예: 19:40
    [SerializeField] private TextMeshProUGUI jobTypeText;   // 예: IT개발자 / 일반 면접

    [SerializeField] private Button mainButton;
    [SerializeField] private Button deleteButton;

    private SessionReportRow myData;

    public void Setup(SessionReportRow data, Action onDeleteClick)
    {
        myData = data;

        // DB에 저장된 end_time 값을 변환 없이 그대로 분리하여 표출
        if (!string.IsNullOrEmpty(myData.EndTime))
        {
            string[] parts = myData.EndTime.Split(' ');
            if (parts.Length >= 2)
            {
                if (dateText != null)
                    dateText.text = parts[0]; // 날짜 (예: 2026-07-25)

                string timeStr = parts[1];
                if (timeStr.Length >= 5)
                    timeStr = timeStr.Substring(0, 5); // 초 단위(`:ss`)를 제외하고 시:분까지만 추출 (예: 19:40)

                if (timeText != null)
                    timeText.text = timeStr;
            }
            else
            {
                if (dateText != null) dateText.text = myData.EndTime;
                if (timeText != null) timeText.text = "";
            }
        }
        else
        {
            if (dateText != null) dateText.text = "일시 정보 없음";
            if (timeText != null) timeText.text = "";
        }

        // 직무 및 유형 포맷팅
        if (jobTypeText != null)
        {
            string category = myData.JobCategory ?? "";

            category = category.Replace("Casual", "일반 면접")
                               .Replace("Intensive", "심화 면접");

            jobTypeText.text = !string.IsNullOrEmpty(category) ? category : "직무 정보 없음";
        }

        // 버튼 이벤트 연결
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

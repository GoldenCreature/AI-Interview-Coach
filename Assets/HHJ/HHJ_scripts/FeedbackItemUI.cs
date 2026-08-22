using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.SceneManagement;
using HJS;

public class FeedbackItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI dateText;
    [SerializeField] private TextMeshProUGUI jobTypeText;

    [SerializeField] private Button mainButton;  
    [SerializeField] private Button deleteButton; 

    private FeedbackData myData; 

    public void Setup(FeedbackData data, Action onDeleteClick)
    {
        myData = data; // 데이터 저장

        if (dateText != null) dateText.text = myData.dateText;
        if (jobTypeText != null) jobTypeText.text = $"직무 : {myData.jobText} / 유형 : {myData.typeText}";

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

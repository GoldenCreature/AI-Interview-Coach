using HJS;
using InterviewDb;
using InterviewDb.Models;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FeedbackListUI : MonoBehaviour
{
    [Header("UI 연결")]
    public Transform contentParent;
    public GameObject feedbackItemPrefab;

    [Header("삭제 팝업 UI 연결")]
    public GameObject deletePopupPanel;  // 팝업 패널 전체
    public Button confirmDeleteBtn;      // 팝업의 [확인] 버튼
    public Button cancelDeleteBtn;       // 팝업의 [취소] 버튼

    private SessionReportRow targetDataToDelete; // DB 삭제 대기 중인 레코드 보관함

    private void Start()
    {
        // 시작할 때 팝업 무조건 숨기기
        if (deletePopupPanel != null) deletePopupPanel.SetActive(false);

        // 팝업 버튼 이벤트 연결
        if (confirmDeleteBtn != null)
            confirmDeleteBtn.onClick.AddListener(ExecuteDelete);

        if (cancelDeleteBtn != null)
            cancelDeleteBtn.onClick.AddListener(ClosePopup);

        RefreshList();
    }

    public void RefreshList()
    {
        if (contentParent == null || feedbackItemPrefab == null) return;

        foreach (Transform child in contentParent) Destroy(child.gameObject);

        if (FeedbackManager.Instance != null)
        {
            var list = FeedbackManager.Instance.GetFeedbackList();
            foreach (var data in list)
            {
                GameObject newItem = Instantiate(feedbackItemPrefab, contentParent);
                FeedbackItemUI itemUI = newItem.GetComponent<FeedbackItemUI>();

                if (itemUI != null)
                {
                    itemUI.Setup(data, () =>
                    {
                        ShowDeletePopup(data);
                    });
                }
            }
        }
    }

    // --- 팝업 관련 기능 ---

    private void ShowDeletePopup(SessionReportRow data)
    {
        targetDataToDelete = data;
        if (deletePopupPanel != null) deletePopupPanel.SetActive(true);
    }

    private void ClosePopup()
    {
        targetDataToDelete = null;
        if (deletePopupPanel != null) deletePopupPanel.SetActive(false);
    }

    private void ExecuteDelete()
    {
        if (targetDataToDelete != null)
        {
            // 1. SQLite DB에서 세션 삭제 (Interview_Session 및 연관 결과 연쇄 삭제)
            FeedbackManager.Instance.RemoveFeedback(targetDataToDelete);

            // 2. 리스트 다시 갱신
            RefreshList();
        }

        // 3. 팝업 닫기
        ClosePopup();
    }

    public void MainBtn()
    {
        GameManager.Instance.LoadTitleScene();
    }
}

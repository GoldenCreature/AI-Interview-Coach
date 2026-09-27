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
        if (contentParent == null || feedbackItemPrefab == null)
        {
            Debug.LogError("[FeedbackListUI] contentParent 또는 feedbackItemPrefab이 Inspector에 연결되지 않았습니다.");
            return;
        }

        // 1. 기존 리스트 아이템 제거
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        // 2. InterviewDbManager에서 저장된 전체 세션 리스트 조회
        if (InterviewDbManager.Instance != null)
        {
            List<SessionReportRow> list = InterviewDbManager.Instance.GetAllSessionReports();

            if (list == null || list.Count == 0)
            {
                Debug.LogWarning("[FeedbackListUI] DB에 저장된 면접 기록이 없습니다.");
                return;
            }

            // 3. 리스트 아이템 생성 및 데이터 바인딩
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

            // 4. ScrollView 레이아웃 강제 갱신 (자식 크기에 맞춰 컨텐츠 크기 자동 재계산)
            Canvas.ForceUpdateCanvases();
            var layoutGroup = contentParent.GetComponent<LayoutGroup>();
            if (layoutGroup != null)
            {
                layoutGroup.enabled = false;
                layoutGroup.enabled = true;
            }
        }
        else
        {
            Debug.LogError("[FeedbackListUI] InterviewDbManager 인스턴스를 찾을 수 없습니다.");
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
            // 1. SQLite DB에서 세션 삭제 (FOREIGN KEY CASCADE 연쇄 삭제)
            bool isDeleted = InterviewDbManager.Instance.DeleteSession(targetDataToDelete.SessionId);

            if (isDeleted)
            {
                Debug.Log($"[FeedbackListUI] 세션 {targetDataToDelete.SessionId} 삭제 성공");
            }

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

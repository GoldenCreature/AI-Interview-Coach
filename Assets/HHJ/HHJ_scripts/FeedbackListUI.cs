using HJS;
using System.Collections;
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

    private FeedbackData targetDataToDelete; // 삭제 대기 중인 데이터 보관함

    private void Start()
    {
        // 시작할 때 팝업 무조건 숨기기
        if (deletePopupPanel != null) deletePopupPanel.SetActive(false);

        // 팝업 버튼들 이벤트 미리 연결해두기
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
                    itemUI.Setup(data,
                        () => // 삭제 버튼 눌렀을 때의 동작
                        {
                            ShowDeletePopup(data);
                        }
                    );
                }
            }
        }
    }

    // --- 팝업 관련 기능 ---

    private void ShowDeletePopup(FeedbackData data)
    {
        targetDataToDelete = data; // 어떤 걸 지울지 잠시 기억해둠
        if (deletePopupPanel != null) deletePopupPanel.SetActive(true); // 팝업 켜기
    }

    private void ClosePopup()
    {
        targetDataToDelete = null; // 기억 비우기
        if (deletePopupPanel != null) deletePopupPanel.SetActive(false); // 팝업 끄기
    }

    private void ExecuteDelete()
    {
        if (targetDataToDelete != null)
        {
            // 1. 매니저에서 진짜로 삭제
            FeedbackManager.Instance.RemoveFeedback(targetDataToDelete);

            // 2. 리스트 다시 그리기 (삭제된 항목이 사라진 상태로 다시 뜸)
            RefreshList();
        }

        // 3. 팝업 닫기
        ClosePopup();
    }
}

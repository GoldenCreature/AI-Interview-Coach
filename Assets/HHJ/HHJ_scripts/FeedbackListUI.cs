using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FeedbackListUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private Transform contentParent;       // Scroll View > Viewport > Content
    [SerializeField] private GameObject feedbackItemPrefab;   // 피드백 버튼 프리팹

    private void OnEnable()
    {
        RefreshList();
    }

    public void RefreshList()
    {
        if (contentParent == null || feedbackItemPrefab == null) return;

        // 1. 기존 생성된 버튼 초기화 (중복 방지)
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        // 2. 저장된 파일 데이터에서 불러와 버튼 생성
        if (FeedbackManager.Instance != null)
        {
            var list = FeedbackManager.Instance.GetFeedbackList();
            foreach (var data in list)
            {
                GameObject newItem = Instantiate(feedbackItemPrefab, contentParent);
                FeedbackItemUI itemUI = newItem.GetComponent<FeedbackItemUI>();
                if (itemUI != null)
                {
                    itemUI.Setup(data.dateText, data.jobText, data.typeText, () =>
                    {
                        Debug.Log($"선택한 피드백: {data.dateText} [{data.jobText}/{data.typeText}]");
                    });
                }
            }
        }
    }
}

using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class FeedbackItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private Button itemButton;

    public void Setup(string date, string job, string type, System.Action onClickAction)
    {
        if (dateText != null) dateText.text = date;
        if (infoText != null) infoText.text = $"직무 : {job} / 유형 : {type}";

        itemButton.onClick.RemoveAllListeners();
        itemButton.onClick.AddListener(() => onClickAction?.Invoke());
    }
}

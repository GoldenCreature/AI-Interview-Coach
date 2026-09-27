using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HJS;
using UnityEngine.UI;

public class SoundSetting : MonoBehaviour
{
    [Header("슬라이더 UI 연결")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("버튼 UI 연결")]
    [SerializeField] private Button applyButton; // ★ [적용] 버튼 추가

    private void Start()
    {
        InitUI();
    }

    private void OnEnable()
    {
        // 설정 창이 열릴 때 항상 현재 저장된 음량으로 슬라이더 위치 초기화
        InitUI();
    }

    /// <summary>
    /// UI 초기 세팅
    /// </summary>
    private void InitUI()
    {
        if (SoundManager.Instance == null) return;

        // 1. BGM 슬라이더 세팅
        if (bgmSlider != null)
        {
            bgmSlider.minValue = 0f;
            bgmSlider.maxValue = 1f;
            // 실시간 반영(onValueChanged)을 사용하지 않고 현재 음량 값만 슬라이더에 세팅
            bgmSlider.value = SoundManager.Instance.BGMVolume;
        }

        // 2. SFX 슬라이더 세팅
        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.value = SoundManager.Instance.SFXVolume;
        }

        // 3. [적용] 버튼 클릭 이벤트 연결
        if (applyButton != null)
        {
            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(OnApplyButtonClicked);
        }
    }

    /// <summary>
    /// [적용] 버튼을 클릭했을 때만 호출되는 메서드
    /// </summary>
    private void OnApplyButtonClicked()
    {
        if (SoundManager.Instance == null) return;

        // 슬라이더의 현재 위치(값)를 SoundManager에 최종 적용
        if (bgmSlider != null)
        {
            SoundManager.Instance.SetBGMVolume(bgmSlider.value);
        }

        if (sfxSlider != null)
        {
            SoundManager.Instance.SetSFXVolume(sfxSlider.value);
        }

        Debug.Log($"[SettingUI] 음량 변경 적용 완료! BGM: {bgmSlider.value}, SFX: {sfxSlider.value}");
    }
}

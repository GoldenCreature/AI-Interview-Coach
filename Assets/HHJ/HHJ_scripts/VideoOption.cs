using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using HJS;

namespace OptionUI.Scripts
{
    public class VideoOption : MonoBehaviour
    {
        FullScreenMode screenMode;
        public TMP_Dropdown resolutionDropdown;
        List<Resolution> resolutions = new List<Resolution>();
        public int resolutionNum;
        public Toggle fullscreenBtn;

        void Start()
        {
            screenMode = Screen.fullScreenMode;
            InitUI();
        }

        void InitUI()
        {
            resolutions.Clear();

            // 현재 모니터의 화면 비율 계산 (예: 16:9 = 1.777...)
            float targetAspectRatio = (float)Screen.currentResolution.width / Screen.currentResolution.height;

            for (int i = 0; i < Screen.resolutions.Length; i++)
            {
                Resolution currentRes = Screen.resolutions[i];

                // 1. 주사율 필터링 (60Hz 부근)
                if (currentRes.refreshRateRatio.value >= 59.0 && currentRes.refreshRateRatio.value <= 61.0)
                {
                    // 2. 현재 모니터와 비율(Aspect Ratio)이 같은 해상도만 추출 (오차 범위 0.05 이내)
                    float currentAspect = (float)currentRes.width / currentRes.height;
                    if (Mathf.Abs(currentAspect - targetAspectRatio) > 0.05f)
                        continue;

                    // 3. 중복 해상도 제거
                    bool isDuplicate = false;
                    foreach (Resolution r in resolutions)
                    {
                        if (r.width == currentRes.width && r.height == currentRes.height)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }

                    if (!isDuplicate)
                    {
                        resolutions.Add(currentRes);
                    }
                }
            }

            resolutionDropdown.options.Clear();

            int optionNum = 0;
            int currentResIndex = 0;

            foreach (Resolution item in resolutions)
            {
                TMP_Dropdown.OptionData option = new TMP_Dropdown.OptionData();
                option.text = item.width + " X " + item.height;
                resolutionDropdown.options.Add(option);

                // 현재 적용 중인 해상도 위치 찾기
                if (item.width == Screen.width && item.height == Screen.height)
                {
                    currentResIndex = optionNum;
                }
                optionNum++;
            }

            resolutionDropdown.value = currentResIndex;
            resolutionDropdown.RefreshShownValue();

            fullscreenBtn.isOn = Screen.fullScreenMode.Equals(FullScreenMode.FullScreenWindow) || Screen.fullScreenMode.Equals(FullScreenMode.ExclusiveFullScreen);
        }

        public void FullScreenBtn(bool isfull)
        {
            screenMode = isfull ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        public void DropboxOptionChange(int x)
        {
            resolutionNum = x;
        }

        public void OkBtnClick()
        {
            if (resolutions.Count > 0 && resolutionNum < resolutions.Count)
            {
                Screen.SetResolution(resolutions[resolutionNum].width, resolutions[resolutionNum].height, screenMode);
            }
        }

        public void MainBtn()
        {
            GameManager.Instance.LoadTitleScene();
        }
        public void PlayBtn()
        {
            GameManager.Instance.LoadInterviewSetupScene();
        }

        public void ExitBtn()
        {
            GameManager.Instance.QuitApplication();
        }
    }
}

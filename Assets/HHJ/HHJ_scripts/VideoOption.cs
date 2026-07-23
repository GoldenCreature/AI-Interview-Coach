using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

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

            for (int i = 0; i < Screen.resolutions.Length; i++)
            {
                Resolution currentRes = Screen.resolutions[i];

                if (currentRes.refreshRateRatio.value >= 59.0 && currentRes.refreshRateRatio.value <= 61.0)
                {

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
            foreach (Resolution item in resolutions)
            {

                TMP_Dropdown.OptionData option = new TMP_Dropdown.OptionData();
                option.text = item.width + " X " + item.height;
                resolutionDropdown.options.Add(option);

                if (item.width == Screen.width && item.height == Screen.height)
                {
                    resolutionDropdown.value = optionNum;
                }
                optionNum++;
            }

            resolutionDropdown.RefreshShownValue();

            fullscreenBtn.isOn = Screen.fullScreenMode.Equals(FullScreenMode.FullScreenWindow) ? true : false;
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
            SceneManager.LoadScene("Main");
        }
        public void ExitBtn()
        {
            Application.Quit();
        }
    }
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HJS
{
    public class SoundManager : SingletonBase<SoundManager>
    {
        [Header("BGM 설정")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioClip mainBGM;
        [SerializeField] private float bgmFadeTime = 0.5f;

        [Header("SFX 설정")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip buttonClickSFX;

        [Header("볼륨 설정")]
        [Range(0f, 1f)]
        [SerializeField] private float bgmVolume = 0.5f;
        [Range(0f, 1f)]
        [SerializeField] private float sfxVolume = 1f;

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("[SoundManager] 초기화 완료");
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // -----------------------------------------------
        // 씬 전환 시 자동 호출
        // -----------------------------------------------
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            switch (scene.name)
            {
                case GameManager.SCENE_TITLE:
                    // 메인 씬: 항상 처음부터 재생
                    if (!bgmSource.isPlaying)
                        PlayBGMFromStart(mainBGM);
                    break;

                case GameManager.SCENE_INTERVIEW_SETUP:
                case GameManager.SCENE_SETTING:
                case GameManager.SCENE_FEEDBACK:
                    // 이어서 재생
                    // BGM이 꺼져있으면 처음부터 재생
                    if (!bgmSource.isPlaying)
                        PlayBGMFromStart(mainBGM);
                    break;

                case GameManager.SCENE_LOADING:
                    // 페이드 아웃
                    StartCoroutine(FadeOutBGM(bgmFadeTime));
                    break;

                case GameManager.SCENE_INTERVIEW:
                case GameManager.SCENE_RESULT:
                    // 면접/결과 씬: BGM 완전 중지
                    StopBGM();
                    break;
            }
            // 한 프레임 후 UI요소 등록
            // 씬 초기화 완료 후 등록
            StartCoroutine(RegisterUISoundsDelayed());
        }

        // -----------------------------------------------
        // BGM 처음부터 재생
        // -----------------------------------------------
        public void PlayBGMFromStart(AudioClip clip)
        {
            if (clip == null) return;

            bgmSource.clip = clip;
            bgmSource.volume = bgmVolume;
            bgmSource.loop = true;
            bgmSource.time = 0f;
            bgmSource.Play();
        }

        // -----------------------------------------------
        // BGM 완전 중지
        // -----------------------------------------------
        public void StopBGM()
        {
            bgmSource.Stop();
        }

        // -----------------------------------------------
        // BGM 페이드 아웃
        // Loading1 씬 진입 시 호출
        // -----------------------------------------------
        private IEnumerator FadeOutBGM(float fadeTime)
        {
            if (!bgmSource.isPlaying) yield break;

            float startVolume = bgmSource.volume;

            while (bgmSource.volume > 0f)
            {
                bgmSource.volume -= startVolume * Time.deltaTime / fadeTime;
                yield return null;
            }

            bgmSource.Stop();
            bgmSource.volume = startVolume;
        }

        // -----------------------------------------------
        // UI 클릭 효과음 재생
        // 원래 방식 : UI 버튼 OnClick 이벤트에 연결
        // 현재 방식 : RegisterUISoundsDelayed()를 통해 자동등록중
        // -----------------------------------------------
        public void PlayButtonClick()
        {
            if (buttonClickSFX == null) return;
            sfxSource.volume = sfxVolume;
            sfxSource.PlayOneShot(buttonClickSFX);
        }

        // -----------------------------------------------
        // BGM 볼륨 설정
        // Setting 씬에서 슬라이더로 조절 가능
        // -----------------------------------------------
        public void SetBGMVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            bgmSource.volume = bgmVolume;
        }

        // -----------------------------------------------
        // SFX 볼륨 설정
        // Setting 씬에서 슬라이더로 조절 가능
        // -----------------------------------------------
        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
        }

        // -----------------------------------------------
        // 씬 로드 시 모든 UI요소에 클릭음 자동 등록
        // 버튼마다 OnClick 연결 불필요
        // -----------------------------------------------
        private IEnumerator RegisterUISoundsDelayed()
        {
            yield return null;

            if (buttonClickSFX == null) yield break;

            // Button 등록
            Button[] buttons = FindObjectsOfType<Button>(true);
            foreach (Button btn in buttons)
                btn.onClick.AddListener(PlayButtonClick);

            // Toggle 등록 (선택 시에만 소리)
            Toggle[] toggles = FindObjectsOfType<Toggle>(true);
            foreach (Toggle tog in toggles)
                tog.onValueChanged.AddListener(
                    isOn => { if (isOn) PlayButtonClick(); });

            // TMP_Dropdown 등록 (항목 선택 시 소리)
            TMP_Dropdown[] dropdowns = FindObjectsOfType<TMP_Dropdown>(true);
            foreach (TMP_Dropdown dropdown in dropdowns)
                dropdown.onValueChanged.AddListener(
                    _ => PlayButtonClick());

            Debug.Log($"[SoundManager] 버튼 {buttons.Length}개 " +
                      $"토글 {toggles.Length}개 " +
                      $"드롭다운 {dropdowns.Length}개 클릭음 등록 완료");
        }
    }
}
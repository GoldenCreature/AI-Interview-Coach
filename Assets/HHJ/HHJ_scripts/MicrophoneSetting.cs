using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class microphoneSetting : MonoBehaviour
{
    [Header("[ 3D Modern Menu UI 연결 ]")]
    public TMP_Dropdown MicroDropdown;
    public Slider MicroSlider;
    public Slider TestSlider;
    public Button TestButton;

    [Header("[ 오디오 재생 설정 ]")]
    public AudioSource TestAudioSource;

    public string SelectedMicName { get; private set; }
    public float MicGain { get; private set; } = 1.0f;

    private TextMeshProUGUI testButtonText;
    private AudioClip micTestClip;
    private bool isTesting = false;
    private const int sampleWindow = 128;
    private int idealSampleRate = 44100;
    private float[] sampleData;          
    private float inverseSampleWindow;  
    private bool isFillRectActive = false;

    private Coroutine playCoroutine;

    void Start()
    {
        if (TestButton != null) testButtonText = TestButton.GetComponentInChildren<TextMeshProUGUI>();

        PopulateMicrophones();

        if (MicroSlider != null)
        {
            MicroSlider.minValue = 0f;
            MicroSlider.maxValue = 5f;
            MicroSlider.value = 1f;
            MicroSlider.onValueChanged.AddListener(OnGainChanged);
        }

        if (MicroDropdown != null) MicroDropdown.onValueChanged.AddListener(OnMicChanged);
        if (TestButton != null) TestButton.onClick.AddListener(ToggleMicTest);

        if (testButtonText != null) testButtonText.text = "마이크 테스트";

        if (TestSlider != null)
        {
            TestSlider.minValue = 0f;
            TestSlider.maxValue = 1f;

            if (TestSlider.fillRect != null) TestSlider.fillRect.gameObject.SetActive(false);
        }

        if (TestAudioSource == null)
        {
            TestAudioSource = GetComponent<AudioSource>();
        }

        sampleData = new float[sampleWindow];
        inverseSampleWindow = 1f / sampleWindow;
    }

    void PopulateMicrophones()
    {
        if (MicroDropdown == null) return;
        MicroDropdown.ClearOptions();

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[마이크 에러] PC에 연결된 마이크 장치를 찾을 수 없습니다!");
            MicroDropdown.AddOptions(new List<string> { "연결된 마이크 없음" });
            return;
        }

        List<string> options = new List<string>(Microphone.devices);
        MicroDropdown.AddOptions(options);
        SelectedMicName = Microphone.devices[0];

        UpdateDeviceCapabilities();
    }

    void UpdateDeviceCapabilities()
    {
        if (string.IsNullOrEmpty(SelectedMicName)) return;

        int minFreq, maxFreq;
        Microphone.GetDeviceCaps(SelectedMicName, out minFreq, out maxFreq);
        idealSampleRate = (maxFreq == 0) ? 44100 : maxFreq;
    }

    void OnMicChanged(int index)
    {
        if (Microphone.devices.Length == 0) return;

        SelectedMicName = Microphone.devices[index];
        UpdateDeviceCapabilities();

        if (isTesting)
        {
            StopMicTest();
            StartMicTest();
        }
    }

    void OnGainChanged(float value) { MicGain = value; }
    void ToggleMicTest() { if (!isTesting) StartMicTest(); else StopMicTest(); }

    void StartMicTest()
    {
        if (string.IsNullOrEmpty(SelectedMicName) || Microphone.devices.Length == 0) return;

        isTesting = true;
        if (testButtonText != null) testButtonText.text = "테스트 중지";

        micTestClip = Microphone.Start(SelectedMicName, true, 1, idealSampleRate);

        if (micTestClip == null)
        {
            Debug.LogError($"[마이크 에러] {SelectedMicName}으로 녹음을 시작하지 못했습니다.");
            StopMicTest();
            return;
        }

        if (TestAudioSource != null)
        {
            TestAudioSource.clip = micTestClip;
            TestAudioSource.loop = true;

            if (playCoroutine != null) StopCoroutine(playCoroutine);
            playCoroutine = StartCoroutine(PlayMicAudioWithDelay());
        }
    }

    IEnumerator PlayMicAudioWithDelay()
    {
        int targetPosition = idealSampleRate / 10;

        while (Microphone.GetPosition(SelectedMicName) < targetPosition)
        {
            yield return null;
        }

        if (isTesting && TestAudioSource != null)
        {
            TestAudioSource.Play();
        }
    }

    void StopMicTest()
    {
        isTesting = false;
        if (testButtonText != null) testButtonText.text = "마이크 테스트";

        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        if (TestAudioSource != null) TestAudioSource.Stop();

        if (!string.IsNullOrEmpty(SelectedMicName)) Microphone.End(SelectedMicName);

        if (TestSlider != null)
        {
            TestSlider.value = 0;
            if (TestSlider.fillRect != null) TestSlider.fillRect.gameObject.SetActive(false);
            isFillRectActive = false;
        }
    }

    void Update()
    {
        if (!isTesting || micTestClip == null || TestSlider == null) return;

        int micPosition = Microphone.GetPosition(SelectedMicName);
        if (micPosition <= 0 || micPosition < sampleWindow) return;

        micTestClip.GetData(sampleData, micPosition - sampleWindow);

        float sum = 0;
        for (int i = 0; i < sampleWindow; i++)
        {
            sum += sampleData[i] * sampleData[i];
        }

        float rms = Mathf.Sqrt(sum * inverseSampleWindow);

        float finalVolume = rms * MicGain * 100f;

        finalVolume = finalVolume > 1f ? 1f : (finalVolume < 0f ? 0f : finalVolume);

        TestSlider.value = Mathf.Lerp(TestSlider.value, finalVolume, Time.deltaTime * 15f);

        if (TestSlider.fillRect != null)
        {
            bool shouldBeActive = TestSlider.value > 0.01f;
            if (isFillRectActive != shouldBeActive)
            {
                isFillRectActive = shouldBeActive;
                TestSlider.fillRect.gameObject.SetActive(shouldBeActive);
            }
        }
    }
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!isTesting) return;

        for (int i = 0; i < data.Length; i++)
        {
            data[i] *= MicGain;
        }
    }

    void OnDisable() { if (isTesting) StopMicTest(); }
}

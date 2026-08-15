using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정창(Settings) 팝업 컨트롤러. 슬라이더 조작 시 볼륨을 즉시 동기화합니다.
/// </summary>
public class SettingsPopupUI : MonoBehaviour
{
    [Header("🔊 Audio Sliders")]
    [SerializeField] private Slider masterVolumeSlider;

    [Header("🖱️ Mouse Settings")]
    [SerializeField] private Slider mouseSensitivitySlider;

    [Header("🔘 Close Button")]
    [SerializeField] private Button closeButton;

    private const string MASTER_VOL_KEY = "SETTINGS_MASTER_VOL";
    private const string MOUSE_SENS_KEY = "SETTINGS_MOUSE_SENS";

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseSettings);

        // 🌟 1. 저장된 볼륨값 불러와서 유니티 전체 볼륨에 즉시 적용!
        float savedVolume = PlayerPrefs.GetFloat(MASTER_VOL_KEY, 1f);
        AudioListener.volume = savedVolume;

        // 2. 볼륨 슬라이더 UI 동기화
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = savedVolume;
            masterVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        // 3. 감도 슬라이더 기본 세팅
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.value = PlayerPrefs.GetFloat(MOUSE_SENS_KEY, 1.5f);
            mouseSensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value; // 슬라이더 움직이면 실시간 BGM 볼륨 조절!
        PlayerPrefs.SetFloat(MASTER_VOL_KEY, value);
    }

    private void OnSensitivityChanged(float value)
    {
        PlayerPrefs.SetFloat(MOUSE_SENS_KEY, value);
    }

    public void CloseSettings()
    {
        PlayerPrefs.Save();
        gameObject.SetActive(false);
    }
}
using UnityEngine;
using UnityEngine.UI;
using KinematicCharacterController.Examples;
using YG;
public class GameSettingsUI : MonoBehaviour
{
    [Header("UI ссылки")]
    public Button settingsButton;
    public Button closeSettingsButton;
    public Button resetSettingsButton; // ← НОВАЯ КНОПКА
    public GameObject settingsPanel;

    [Header("Ползунки")]
    public Slider sensitivitySlider;
    public Slider volumeSlider;

    // Ключи для сохранения
    private const string SENSITIVITY_KEY = "MouseSensitivity";
    private const string VOLUME_KEY = "MasterVolume";

    // Объявляем дефолтную громкость как константу
    private const float DEFAULT_VOLUME = 0.5f; // 50% — центр слайдера

    // Чувствительность — вычисляемая в зависимости от платформы
    private float DefaultSensitivity
    {
        get
        {
            if (YG2.envir.isMobile)
            {
                // Диапазон слайдера: 0.1f – 3.0f
                // 60% = 0.1 + (3.0 - 0.1) * 0.6 = 1.84 → округляем до 1.8f
                return 1.8f;
            }
            return 1.0f; // значение по умолчанию для ПК
        }
    }

    private ExampleCharacterController player;
    private ExampleCharacterCamera cameraController;

    private void Start()
    {
        player = FindFirstObjectByType<ExampleCharacterController>();
        var mainCamera = Camera.main;
        if (mainCamera != null)
            cameraController = mainCamera.GetComponent<ExampleCharacterCamera>();

        // Настройка диапазонов (если не заданы в инспекторе)
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = 0.1f;
            sensitivitySlider.maxValue = 3.0f;
            sensitivitySlider.wholeNumbers = false;
        }

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.wholeNumbers = false;
        }

        // Подписка на кнопки
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(CloseSettings);
        if (resetSettingsButton != null) resetSettingsButton.onClick.AddListener(ResetToDefaults);

        // Загрузка настроек (использует DefaultSensitivity при первом запуске)
        LoadSettings();

        // Подписка на изменения
        if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void LoadSettings()
    {
        float sens = PlayerPrefs.GetFloat(SENSITIVITY_KEY, DefaultSensitivity);
        float vol = PlayerPrefs.GetFloat(VOLUME_KEY, DEFAULT_VOLUME);

        if (sensitivitySlider != null) sensitivitySlider.value = sens;
        if (volumeSlider != null) volumeSlider.value = vol;

        ApplySensitivity(sens);
        ApplyVolume(vol);
    }

    private void OnSensitivityChanged(float value)
    {
        ApplySensitivity(value);
        PlayerPrefs.SetFloat(SENSITIVITY_KEY, value);
        PlayerPrefs.Save();
    }

    private void OnVolumeChanged(float value)
    {
        ApplyVolume(value);
        PlayerPrefs.SetFloat(VOLUME_KEY, value);
        PlayerPrefs.Save();
    }

    private void ApplySensitivity(float value)
    {
        if (player != null)
            player.mouseSensitivity = value;

        if (cameraController != null)
            cameraController.mouseSensitivity = value;
    }

    private void ApplyVolume(float value)
    {
        AudioListener.volume = value;
    }

    public void OpenSettings()
    {
        settingsPanel?.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel?.SetActive(false);
    }

    public void ResetToDefaults()
    {
        float defaultSens = DefaultSensitivity;
        float defaultVol = DEFAULT_VOLUME;

        if (sensitivitySlider != null) sensitivitySlider.value = defaultSens;
        if (volumeSlider != null) volumeSlider.value = defaultVol;

        ApplySensitivity(defaultSens);
        ApplyVolume(defaultVol);

        PlayerPrefs.SetFloat(SENSITIVITY_KEY, defaultSens);
        PlayerPrefs.SetFloat(VOLUME_KEY, defaultVol);
        PlayerPrefs.Save();

        Debug.Log("Настройки сброшены к значениям по умолчанию.");
    }
}
using UnityEngine;
using YG; // Обязательно подключите пространство имен YG2

public class StartupQualitySetter : MonoBehaviour
{
    // Укажите здесь индексы ваших уровней графики в настройках Quality (Edit → Project Settings → Quality)
    // Индексы считаются с 0 (самый нижний уровень в списке).
    [SerializeField] private int mobileQualityLevelIndex = 0; // Для мобильных
    [SerializeField] private int desktopQualityLevelIndex = 1; // Для ПК

    void Start()
    {
        SetQualityForPlatform();
    }

    private void SetQualityForPlatform()
    {
        // Самый надежный способ — использовать bool поля
        if (YG2.envir.isMobile || YG2.envir.isTablet)
        {
            Debug.Log($"Определено мобильное устройство или планшет. Устанавливаем качество (Индекс: {mobileQualityLevelIndex})");
            QualitySettings.SetQualityLevel(mobileQualityLevelIndex, true); // true — применить изменения сразу
        }
        else if (YG2.envir.isDesktop)
        {
            Debug.Log($"Определен ПК. Устанавливаем качество (Индекс: {desktopQualityLevelIndex})");
            QualitySettings.SetQualityLevel(desktopQualityLevelIndex, true);
        }
        else
        {
            // На случай, если тип не определен (например, TV) — ставим что-то среднее или ПК версию
            Debug.LogWarning($"Тип устройства не определен ({YG2.envir.deviceType}). Устанавливаем качество для ПК.");
            QualitySettings.SetQualityLevel(desktopQualityLevelIndex, true);
        }

        // Альтернативный вариант — использовать строку deviceType
        // string device = YG2.envir.deviceType;
        // if (device == "mobile" || device == "tablet") { ... }
    }
}
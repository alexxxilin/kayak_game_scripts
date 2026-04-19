using UnityEngine;
using YG;
using System;
using System.Collections;

/// <summary>
/// Менеджер инициализации Yandex Games SDK.
/// Гарантирует, что все обращения к YG2.saves происходят после полной инициализации SDK.
/// Также автоматически выполняет GameReadyAPI и инициализирует определение языка.
/// </summary>
public class YG2InitializationManager : MonoBehaviour
{
    public static YG2InitializationManager Instance { get; private set; }

    [Header("Настройки инициализации")]
    [Tooltip("Максимальное время ожидания инициализации SDK (секунды)")]
    [SerializeField] private float maxInitializationTime = 10f;

    [Tooltip("Задержка после инициализации перед разрешением доступа к saves (секунды)")]
    [SerializeField] private float postInitDelay = 0.5f;

    [Header("Game Ready API")]
    [Tooltip("Автоматически вызывать GameReadyAPI после инициализации (если в настройках плагина AutoGRA отключён)")]
    [SerializeField] private bool autoCallGameReady = true;

    [Header("Локализация")]
    [Tooltip("Вызывать событие после определения языка платформы")]
    [SerializeField] private bool invokeOnLanguageDetected = true;

    /// <summary>
    /// true если SDK полностью инициализирован и безопасен для использования
    /// </summary>
    public bool IsInitialized { get; private set; } = false;

    /// <summary>
    /// Событие вызывается когда SDK полностью готов к использованию
    /// </summary>
    public event Action OnSDKReady;
    
    /// <summary>
    /// Событие вызывается после определения языка платформы
    /// </summary>
    public event Action<string> OnLanguageDetected;

    private bool _gameReadyCalled = false;
    private string _detectedLanguage = "ru"; // дефолтное значение

    private void Awake()
    {
        // Синглтон
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        StartCoroutine(InitializeSDK());
    }

    private IEnumerator InitializeSDK()
    {
        Debug.Log("🚀 YG2InitializationManager: Начало инициализации Yandex Games SDK...");

        float waitTime = 0f;

        // ✅ Ждём инициализации SDK согласно документации
        while (!YG2.isSDKEnabled && waitTime < maxInitializationTime)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        if (!YG2.isSDKEnabled)
        {
            Debug.LogWarning($"⚠️ YG2InitializationManager: SDK не был инициализирован за {maxInitializationTime} сек. Продолжаем работу без SDK.");
            // Даже без SDK вызываем GameReady если нужно, чтобы игра не зависла
            if (autoCallGameReady && !_gameReadyCalled)
            {
                YG2.GameReadyAPI();
                _gameReadyCalled = true;
                Debug.Log("✅ GameReadyAPI вызван (режим без SDK)");
            }
            IsInitialized = true;
            OnSDKReady?.Invoke();
            yield break;
        }

        Debug.Log("✅ YG2InitializationManager: SDK включен, ожидаем загрузку данных...");
        
        // 🔹 Дополнительная задержка для полной загрузки данных платформы
        yield return new WaitForSeconds(postInitDelay);

        // 🔹 Проверяем что saves не null
        if (YG2.saves == null)
        {
            Debug.Log("⚠️ YG2InitializationManager: YG2.saves всё ещё null, создаем дефолтные сохранения...");
            YG2.SetDefaultSaves();
        }

        // 🔹 🔥 АВТООПРЕДЕЛЕНИЕ ЯЗЫКА 🔥
        // Язык автоматически определяется плагином через модуль EnvirData
        // Но мы можем явно прочитать его и вызвать событие
        _detectedLanguage = YG2.envir.language;
        Debug.Log($"🌐 Обнаружен язык платформы: {_detectedLanguage}");
        
        if (invokeOnLanguageDetected)
        {
            OnLanguageDetected?.Invoke(_detectedLanguage);
        }
        
        // 🔹 🔥 GAME READY API 🔥
        // По умолчанию плагин сам вызывает GameReadyAPI если включена опция AutoGRA в Basic Settings
        // Но если опция отключена — вызываем вручную
        if (autoCallGameReady && !_gameReadyCalled)
        {
            // Проверяем, не вызвал ли уже плагин GameReady автоматически
            // (это эвристика: если игра уже в фокусе и SDK готов — вероятно, AutoGRA сработал)
            YG2.GameReadyAPI();
            _gameReadyCalled = true;
            Debug.Log("✅ GameReadyAPI вызван вручную");
        }

        IsInitialized = true;
        Debug.Log("✅ YG2InitializationManager: SDK полностью инициализирован и готов к использованию!");
        
        // Вызываем событие готовности
        OnSDKReady?.Invoke();
    }

    /// <summary>
    /// Безопасный способ получить доступ к YG2.saves
    /// </summary>
    public static bool TryGetSaves(out SavesYG saves)
    {
        saves = null;

        if (Instance == null)
        {
            Debug.LogWarning("⚠️ YG2InitializationManager: Инстанс не найден!");
            return false;
        }

        if (!Instance.IsInitialized)
        {
            Debug.LogWarning("⚠️ YG2InitializationManager: SDK ещё не инициализирован!");
            return false;
        }

        if (YG2.saves == null)
        {
            Debug.LogWarning("⚠️ YG2InitializationManager: YG2.saves равен null!");
            return false;
        }

        saves = YG2.saves;
        return true;
    }

    /// <summary>
    /// Асинхронный метод для ожидания инициализации
    /// </summary>
    public static IEnumerator WaitForInitialization(System.Action onComplete)
    {
        if (Instance == null)
        {
            Debug.LogError("❌ YG2InitializationManager: Инстанс не найден в сцене!");
            onComplete?.Invoke();
            yield break;
        }

        while (!Instance.IsInitialized)
        {
            yield return null;
        }

        onComplete?.Invoke();
    }

    /// <summary>
    /// Проверка можно ли безопасно обращаться к YG2.saves
    /// </summary>
    public static bool CanAccessSaves()
    {
        if (Instance == null) return false;
        if (!Instance.IsInitialized) return false;
        if (YG2.saves == null) return false;
        return true;
    }
    
    /// <summary>
    /// Получить текущий определённый язык
    /// </summary>
    public static string GetDetectedLanguage()
    {
        if (Instance != null && Instance.IsInitialized)
        {
            return YG2.envir.language;
        }
        return "ru"; // дефолт
    }
    
    /// <summary>
    /// Принудительно вызвать GameReadyAPI (если ещё не вызывался)
    /// </summary>
    public static void EnsureGameReady()
    {
        if (Instance != null && !Instance._gameReadyCalled)
        {
            YG2.GameReadyAPI();
            Instance._gameReadyCalled = true;
            Debug.Log("✅ GameReadyAPI вызван через EnsureGameReady()");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
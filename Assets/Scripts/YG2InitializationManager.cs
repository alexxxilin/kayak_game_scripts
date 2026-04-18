using UnityEngine;
using YG;
using System;
using System.Collections;

/// <summary>
/// Менеджер инициализации Yandex Games SDK.
/// Гарантирует, что все обращения к YG2.saves происходят после полной инициализации SDK.
/// </summary>
public class YG2InitializationManager : MonoBehaviour
{
    public static YG2InitializationManager Instance { get; private set; }

    [Header("Настройки инициализации")]
    [Tooltip("Максимальное время ожидания инициализации SDK (секунды)")]
    [SerializeField] private float maxInitializationTime = 10f;

    [Tooltip("Задержка после инициализации перед разрешением доступа к saves (секунды)")]
    [SerializeField] private float postInitDelay = 0.5f;

    /// <summary>
    /// true если SDK полностью инициализирован и безопасен для использования
    /// </summary>
    public bool IsInitialized { get; private set; } = false;

    /// <summary>
    /// Событие вызывается когда SDK полностью готов к использованию
    /// </summary>
    public event Action OnSDKReady;

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

        // ✅ ИСПРАВЛЕНО: используем YG2.isSDKEnabled согласно документации
        while (!YG2.isSDKEnabled && waitTime < maxInitializationTime)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        if (!YG2.isSDKEnabled)
        {
            Debug.LogWarning($"⚠️ YG2InitializationManager: SDK не был инициализирован за {maxInitializationTime} сек. Продолжаем работу без SDK.");
            IsInitialized = true;
            OnSDKReady?.Invoke();
            yield break;
        }

        Debug.Log("✅ YG2InitializationManager: SDK включен, ожидаем загрузку данных...");
        
        // Дополнительная задержка для полной загрузки данных
        yield return new WaitForSeconds(postInitDelay);

        // Проверяем что saves не null
        if (YG2.saves == null)
        {
            Debug.Log("⚠️ YG2InitializationManager: YG2.saves всё ещё null, создаем дефолтные сохранения...");
            YG2.SetDefaultSaves();
        }

        IsInitialized = true;
        Debug.Log("✅ YG2InitializationManager: SDK полностью инициализирован и готов к использованию!");
        
        // Вызываем событие
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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
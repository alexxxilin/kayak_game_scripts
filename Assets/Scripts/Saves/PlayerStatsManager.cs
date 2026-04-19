using UnityEngine;
using YG;
using System.Collections;
using System.Collections.Generic;

public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance { get; private set; }

    private const string PENDING_ADS_DISABLED_KEY = "PendingAdsDisabled";
    private const string ADS_DISABLED_KEY = "AdsDisabled";
    private const string DONATE_PET_PREFIX = "DonatePet_";
    private const string SILVER_COINS_KEY = "SilverCoins";
    private const string REGULAR_COINS_KEY = "RegularCoins";
    private const string VIP_UNLOCKED_KEY = "VipUnlocked";

    [Header("Настройки")]
    [Tooltip("Сколько раз пытаться сохранить, если не получилось с первого раза")]
    public int maxSaveAttempts = 3;
    [Tooltip("Задержка между попытками сохранения (сек)")]
    public float saveAttemptDelay = 0.2f;

    private List<string> _allDonatePetKeys = new List<string>();
    private bool _sdkReady = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        YG2.onGetSDKData += OnSDKDataLoaded;
        if (YG2.isSDKEnabled)
        {
            _sdkReady = true;
            LoadAllDonatePetKeys();
            SyncWithSavesOnLoad();
            CheckAndApplyPendingAdsDisabled();
            CheckAndRestoreCoins();
        }
    }

    private void OnDestroy()
    {
        YG2.onGetSDKData -= OnSDKDataLoaded;
    }

    private void OnSDKDataLoaded()
    {
        _sdkReady = true;
        Debug.Log("📦 PlayerStatsManager: данные SDK загружены");

        LoadAllDonatePetKeys();
        SyncWithSavesOnLoad();
        CheckAndApplyPendingAdsDisabled();
        CheckAndRestoreCoins();
    }

    // === VIP METHODS ===
    
    /// <summary>
    /// Устанавливает статус разблокировки VIP-доступа с мгновенным сохранением
    /// </summary>
    public void SetVIPUnlocked(bool value)
    {
        if (!_sdkReady) return;
        
        int intValue = value ? 1 : 0;
        Debug.Log($"👑 PlayerStats: устанавливаем VIP = {value}");
        
        // Устанавливаем значение в Player Stats
        YG2.SetState(VIP_UNLOCKED_KEY, intValue);

        // Немедленно обновляем облачные сохранения для синхронизации
        if (YG2.saves != null)
        {
            YG2.saves.vipUnlocked = value;
            // Сохраняем прогресс сразу
            YG2.SaveProgress();
        }
        
        // Запускаем гарантированное сохранение с повторными попытками
        StartCoroutine(GuaranteedSaveCoroutine(VIP_UNLOCKED_KEY, intValue));
    }

    /// <summary>
    /// Возвращает статус разблокировки VIP-доступа
    /// </summary>
    public bool GetVIPUnlocked()
    {
        if (!_sdkReady) return false;
        return YG2.GetState(VIP_UNLOCKED_KEY) == 1;
    }

    /// <summary>
    /// Принудительное сохранение всех ключевых данных (вызывается после важных событий)
    /// </summary>
    public void ForceSave()
    {
        if (!_sdkReady) return;
        
        Debug.Log("💾 PlayerStats: принудительное сохранение");
        
        // Сохраняем ключевые значения
        YG2.SetState(VIP_UNLOCKED_KEY, GetVIPUnlocked() ? 1 : 0);
        YG2.SetState(SILVER_COINS_KEY, GetSilverCoins());
        YG2.SetState(REGULAR_COINS_KEY, (int)GetRegularCoins());
        YG2.SetState(ADS_DISABLED_KEY, GetAdsDisabled() ? 1 : 0);
        
        // Синхронизируем с облачными сохранениями
        if (YG2.saves != null)
        {
            YG2.saves.vipUnlocked = GetVIPUnlocked();
            YG2.saves.silverCoins = GetSilverCoins();
            YG2.saves.coinsCollected = GetRegularCoins();
            YG2.saves.adsDisabled = GetAdsDisabled();
            YG2.SaveProgress();
        }
    }

    // === ОСТАЛЬНЫЕ МЕТОДЫ ===
    
    public void SetPendingAdsDisabled(bool value)
    {
        if (!_sdkReady) return;
        int intValue = value ? 1 : 0;
        Debug.Log($"⚡ PlayerStats: устанавливаем {PENDING_ADS_DISABLED_KEY} = {intValue}");
        YG2.SetState(PENDING_ADS_DISABLED_KEY, intValue);
        StartCoroutine(GuaranteedSaveCoroutine(PENDING_ADS_DISABLED_KEY, intValue));
    }

    public bool GetPendingAdsDisabled()
    {
        if (!_sdkReady) return false;
        return YG2.GetState(PENDING_ADS_DISABLED_KEY) == 1;
    }

    public void SetAdsDisabled(bool value)
    {
        if (!_sdkReady) return;
        int intValue = value ? 1 : 0;
        YG2.SetState(ADS_DISABLED_KEY, intValue);
    }

    public bool GetAdsDisabled()
    {
        if (!_sdkReady) return false;
        return YG2.GetState(ADS_DISABLED_KEY) == 1;
    }

    public void ClearPendingAdsDisabled()
    {
        if (!_sdkReady) return;
        YG2.SetState(PENDING_ADS_DISABLED_KEY, 0);
    }

    private void CheckAndApplyPendingAdsDisabled()
    {
        if (!_sdkReady) return;
        if (GetPendingAdsDisabled() && !GetAdsDisabled())
        {
            Debug.Log("🔍 PlayerStats: найден pending-флаг! Применяем отключение рекламы");
            AutoInterstitialAd adManager = FindFirstObjectByType<AutoInterstitialAd>();
            adManager?.ApplyAdsDisabledFromStats();
            SetAdsDisabled(true);
            ClearPendingAdsDisabled();
        }
    }

    public void SetRegularCoins(long amount)
    {
        if (!_sdkReady) return;
        Debug.Log($"💰 PlayerStats: сохраняем обычные монеты = {amount}");
        YG2.SetState(REGULAR_COINS_KEY, (int)amount);
    }

    public long GetRegularCoins()
    {
        if (!_sdkReady) return 0;
        return YG2.GetState(REGULAR_COINS_KEY);
    }

    public void SetSilverCoins(int amount)
    {
        if (!_sdkReady) return;
        Debug.Log($"🥈 PlayerStats: сохраняем серебряные монеты = {amount}");
        YG2.SetState(SILVER_COINS_KEY, amount);
    }

    public int GetSilverCoins()
    {
        if (!_sdkReady) return 0;
        return YG2.GetState(SILVER_COINS_KEY);
    }

    private void CheckAndRestoreCoins()
    {
        if (!_sdkReady) return;
        var player = FindFirstObjectByType<KinematicCharacterController.Examples.ExampleCharacterController>();
        if (player == null) return;

        long savedRegularCoins = GetRegularCoins();
        if (savedRegularCoins > 0 && player.CoinsCollected < savedRegularCoins)
        {
            Debug.Log($"💰 PlayerStats: восстанавливаем обычные монеты: {savedRegularCoins}");
            player.SetCoins(savedRegularCoins);
        }

        int savedSilverCoins = GetSilverCoins();
        if (savedSilverCoins > 0 && player.SilverCoins < savedSilverCoins)
        {
            Debug.Log($"🥈 PlayerStats: восстанавливаем серебряные монеты: {savedSilverCoins}");
            player.SetSilverCoins(savedSilverCoins);
        }
    }

    public void SetDonatePetPurchased(int petIndex, int shopIndex)
    {
        if (!_sdkReady) return;
        string key = $"{DONATE_PET_PREFIX}{shopIndex}_{petIndex}";
        Debug.Log($"🐕 PlayerStats: отмечаем покупку питомца {key}");
        YG2.SetState(key, 1);

        if (!_allDonatePetKeys.Contains(key))
        {
            _allDonatePetKeys.Add(key);
        }

        StartCoroutine(GuaranteedSaveCoroutine(key, 1));
    }

    public bool IsDonatePetPurchased(int petIndex, int shopIndex)
    {
        if (!_sdkReady) return false;
        string key = $"{DONATE_PET_PREFIX}{shopIndex}_{petIndex}";
        return YG2.GetState(key) == 1;
    }

    public List<string> GetAllPurchasedDonatePets()
    {
        List<string> purchasedPets = new List<string>();
        if (!_sdkReady) return purchasedPets;

        var allStats = YG2.GetAllStats();
        foreach (var stat in allStats)
        {
            if (stat.Key.StartsWith(DONATE_PET_PREFIX) && stat.Value == 1)
            {
                purchasedPets.Add(stat.Key);
                if (!_allDonatePetKeys.Contains(stat.Key))
                {
                    _allDonatePetKeys.Add(stat.Key);
                }
            }
        }
        return purchasedPets;
    }

    private void LoadAllDonatePetKeys()
    {
        if (!_sdkReady) return;
        _allDonatePetKeys.Clear();
        var allStats = YG2.GetAllStats();
        foreach (var stat in allStats)
        {
            if (stat.Key.StartsWith(DONATE_PET_PREFIX))
            {
                _allDonatePetKeys.Add(stat.Key);
            }
        }
    }

    public void SyncWithSavesOnLoad()
    {
        if (!_sdkReady || YG2.saves == null) return;

        Debug.Log("🔄 Синхронизация: Player Stats → Cloud Saves (приоритет доната)...");

        // === VIP ===
        bool vipStats = GetVIPUnlocked();
        if (vipStats && !YG2.saves.vipUnlocked)
        {
            Debug.Log("👑 Восстанавливаем VIP из Player Stats в Cloud Saves");
            YG2.saves.vipUnlocked = true;
        }
        else if (!vipStats && YG2.saves.vipUnlocked)
        {
            SetVIPUnlocked(true);
        }

        // === Реклама ===
        if (GetAdsDisabled() && !YG2.saves.adsDisabled)
        {
            Debug.Log("✅ Восстанавливаем отключение рекламы из Player Stats в Cloud Saves");
            YG2.saves.adsDisabled = true;
            YG2.saves.pendingAdsDisabled = false;
        }
        else if (!GetAdsDisabled() && YG2.saves.adsDisabled)
        {
            SetAdsDisabled(true);
        }

        // === Монеты ===
        long statsCoins = GetRegularCoins();
        if (statsCoins > YG2.saves.coinsCollected)
        {
            Debug.Log($"✅ Восстанавливаем обычные монеты из Player Stats: {statsCoins}");
            YG2.saves.coinsCollected = statsCoins;
        }
        else if (YG2.saves.coinsCollected > statsCoins)
        {
            SetRegularCoins((long)YG2.saves.coinsCollected);
        }

        int statsSilver = GetSilverCoins();
        if (statsSilver > YG2.saves.silverCoins)
        {
            Debug.Log($"✅ Восстанавливаем серебряные монеты из Player Stats: {statsSilver}");
            YG2.saves.silverCoins = statsSilver;
        }
        else if (YG2.saves.silverCoins > statsSilver)
        {
            SetSilverCoins(YG2.saves.silverCoins);
        }

        // === Питомцы ===
        var purchasedPets = GetAllPurchasedDonatePets();
        foreach (string petKey in purchasedPets)
        {
            string[] parts = petKey.Replace("DonatePet_", "").Split('_');
            if (parts.Length == 2 && int.TryParse(parts[0], out int shopIndex) && int.TryParse(parts[1], out int petIndex))
            {
                bool existsInSaves = false;
                foreach (var pet in YG2.saves.ownedPets)
                {
                    if (pet.shopIndex == shopIndex && pet.petTypeIndex == petIndex && pet.isDonatePet)
                    {
                        existsInSaves = true;
                        break;
                    }
                }

                if (!existsInSaves)
                {
                    Debug.Log($"✅ Восстанавливаем питомца [{shopIndex}:{petIndex}] из Player Stats в Cloud Saves");
                    YG2.saves.ownedPets.Add(new PetSaveData
                    {
                        id = Random.Range(10000, 99999),
                        shopIndex = shopIndex,
                        petTypeIndex = petIndex,
                        isDonatePet = true
                    });
                }
            }
        }

        SaveManager saveManager = FindFirstObjectByType<SaveManager>();
        if (saveManager != null)
        {
            saveManager.SaveImmediately("sync_stats_to_saves");
        }
        else
        {
            YG2.SaveProgress();
        }

        Debug.Log("✅ Синхронизация завершена (приоритет у Player Stats для доната)");
    }

    public void ResetAllDonatePurchases()
    {
        Debug.Log("🧹 Сбрасываем ВСЕ донатные покупки в Player Stats...");

        SetPendingAdsDisabled(false);
        SetAdsDisabled(false);
        SetVIPUnlocked(false);
        Debug.Log("   ✅ Реклама и VIP сброшены");

        SetRegularCoins(0);
        SetSilverCoins(0);
        Debug.Log("   ✅ Монеты сброшены");

        int petCount = 0;
        foreach (string key in _allDonatePetKeys)
        {
            YG2.SetState(key, 0);
            petCount++;
        }
        Debug.Log($"   ✅ {petCount} донатных питомцев сброшено");

        _allDonatePetKeys.Clear();

        var currentStats = YG2.GetAllStats();
        Dictionary<string, int> cleanedStats = new Dictionary<string, int>();
        foreach (var stat in currentStats)
        {
            if (!stat.Key.StartsWith(DONATE_PET_PREFIX) &&
                stat.Key != PENDING_ADS_DISABLED_KEY &&
                stat.Key != ADS_DISABLED_KEY &&
                stat.Key != REGULAR_COINS_KEY &&
                stat.Key != SILVER_COINS_KEY &&
                stat.Key != VIP_UNLOCKED_KEY)
            {
                cleanedStats[stat.Key] = stat.Value;
            }
        }
        YG2.SetAllStats(cleanedStats);

        Debug.Log("✅ Все донатные покупки в Player Stats полностью сброшены!");
        StartCoroutine(ForceSaveAfterReset());
    }

    private IEnumerator ForceSaveAfterReset()
    {
        yield return new WaitForSecondsRealtime(0.2f);
        YG2.SetState(PENDING_ADS_DISABLED_KEY, 0);
        YG2.SetState(ADS_DISABLED_KEY, 0);
        YG2.SetState(REGULAR_COINS_KEY, 0);
        YG2.SetState(SILVER_COINS_KEY, 0);
        YG2.SetState(VIP_UNLOCKED_KEY, 0);
        Debug.Log("💾 Финальное сохранение после сброса");
    }

    private IEnumerator GuaranteedSaveCoroutine(string key, int value)
    {
        int attempts = 0;
        bool saved = false;

        while (attempts < maxSaveAttempts && !saved)
        {
            attempts++;
            Debug.Log($"💾 PlayerStats: попытка сохранения #{attempts} для {key}");

            YG2.SetState(key, value);
            yield return new WaitForSecondsRealtime(saveAttemptDelay);

            if (YG2.GetState(key) == value)
            {
                saved = true;
                Debug.Log($"✅ PlayerStats: ключ {key} подтвержден после попытки #{attempts}");
            }
        }

        if (!saved)
        {
            Debug.LogError($"❌ PlayerStats: не удалось сохранить {key} после {maxSaveAttempts} попыток!");
        }
    }

    [ContextMenu("Очистить все Player Stats")]
    public void ClearAllStats()
    {
        if (!_sdkReady) return;
        YG2.SetAllStats(new Dictionary<string, int>());
        Debug.Log("🧹 Все Player Stats очищены");
    }

    [ContextMenu("Логировать все Player Stats")]
    public void LogAllStats()
    {
        if (!_sdkReady) return;
        var allStats = YG2.GetAllStats();
        Debug.Log("📊 Все Player Stats:");
        foreach (var stat in allStats)
        {
            Debug.Log($"   {stat.Key}: {stat.Value}");
        }
    }
}
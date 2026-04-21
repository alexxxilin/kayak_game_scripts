using UnityEngine;
using YG;
using System;
using System.Collections;
using System.Collections.Generic;

public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance { get; private set; }

    private const string PENDING_ADS_DISABLED_KEY = "PendingAdsDisabled";
    private const string ADS_DISABLED_KEY = "AdsDisabled";
    private const string SILVER_COINS_KEY = "SilverCoins";
    private const string REGULAR_COINS_KEY = "RegularCoins";
    private const string VIP_UNLOCKED_KEY = "VipUnlocked";

    [Header("Настройки")]
    [Tooltip("Сколько раз пытаться сохранить, если не получилось с первого раза")]
    public int maxSaveAttempts = 3;
    [Tooltip("Задержка между попытками сохранения (сек)")]
    public float saveAttemptDelay = 0.2f;

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
        SyncWithSavesOnLoad();
        CheckAndApplyPendingAdsDisabled();
        CheckAndRestoreCoins();
    }

    // === VIP ===
    public void SetVIPUnlocked(bool value)
    {
        if (!_sdkReady) return;
        int intValue = value ? 1 : 0;
        YG2.SetState(VIP_UNLOCKED_KEY, intValue);
        if (YG2.saves != null)
        {
            YG2.saves.vipUnlocked = value;
            YG2.SaveProgress();
        }
        StartCoroutine(GuaranteedSaveCoroutine(VIP_UNLOCKED_KEY, intValue));
    }

    public bool GetVIPUnlocked() => _sdkReady && YG2.GetState(VIP_UNLOCKED_KEY) == 1;

    // === Монеты ===
    public void SetRegularCoins(long amount)
    {
        if (!_sdkReady) return;
        YG2.SetState(REGULAR_COINS_KEY, (int)amount);
    }

    public long GetRegularCoins() => _sdkReady ? YG2.GetState(REGULAR_COINS_KEY) : 0;

    public void SetSilverCoins(int amount)
    {
        if (!_sdkReady) return;
        YG2.SetState(SILVER_COINS_KEY, amount);
    }

    public int GetSilverCoins() => _sdkReady ? YG2.GetState(SILVER_COINS_KEY) : 0;

    // === Реклама ===
    public void SetPendingAdsDisabled(bool value)
    {
        if (!_sdkReady) return;
        YG2.SetState(PENDING_ADS_DISABLED_KEY, value ? 1 : 0);
        StartCoroutine(GuaranteedSaveCoroutine(PENDING_ADS_DISABLED_KEY, value ? 1 : 0));
    }

    public bool GetPendingAdsDisabled() => _sdkReady && YG2.GetState(PENDING_ADS_DISABLED_KEY) == 1;

    public void SetAdsDisabled(bool value)
    {
        if (!_sdkReady) return;
        YG2.SetState(ADS_DISABLED_KEY, value ? 1 : 0);
    }

    public bool GetAdsDisabled() => _sdkReady && YG2.GetState(ADS_DISABLED_KEY) == 1;

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
            var adManager = FindFirstObjectByType<AutoInterstitialAd>();
            adManager?.ApplyAdsDisabledFromStats();
            SetAdsDisabled(true);
            ClearPendingAdsDisabled();
        }
    }

    private void CheckAndRestoreCoins()
    {
        if (!_sdkReady) return;
        var player = FindFirstObjectByType<KinematicCharacterController.Examples.ExampleCharacterController>();
        if (player == null) return;

        long savedRegular = GetRegularCoins();
        if (savedRegular > 0 && player.CoinsCollected < savedRegular)
        {
            player.SetCoins(savedRegular);
        }

        int savedSilver = GetSilverCoins();
        if (savedSilver > 0 && player.SilverCoins < savedSilver)
        {
            player.SetSilverCoins(savedSilver);
        }
    }

    private void SyncWithSavesOnLoad()
    {
        if (!_sdkReady || YG2.saves == null) return;

        // Синхронизация: монеты
        long statsCoins = GetRegularCoins();
        if (statsCoins > YG2.saves.coinsCollected)
            YG2.saves.coinsCollected = statsCoins;
        else if (YG2.saves.coinsCollected > statsCoins)
            SetRegularCoins((long)YG2.saves.coinsCollected);

        int statsSilver = GetSilverCoins();
        if (statsSilver > YG2.saves.silverCoins)
            YG2.saves.silverCoins = statsSilver;
        else if (YG2.saves.silverCoins > statsSilver)
            SetSilverCoins(YG2.saves.silverCoins);

        // Синхронизация: VIP
        bool vipStats = GetVIPUnlocked();
        if (vipStats && !YG2.saves.vipUnlocked)
            YG2.saves.vipUnlocked = true;
        else if (!vipStats && YG2.saves.vipUnlocked)
            SetVIPUnlocked(true);

        // Синхронизация: реклама
        if (GetAdsDisabled() && !YG2.saves.adsDisabled)
            YG2.saves.adsDisabled = true;
        else if (!GetAdsDisabled() && YG2.saves.adsDisabled)
            SetAdsDisabled(true);

        var saveManager = FindFirstObjectByType<SaveManager>();
        if (saveManager != null)
            saveManager.SaveImmediately("sync_stats");
        else
            YG2.SaveProgress();
    }

    public void ResetAllDonatePurchases()
    {
        Debug.Log("🧹 Сброс Player Stats (без питомцев)...");
        SetPendingAdsDisabled(false);
        SetAdsDisabled(false);
        YG2.SetState(VIP_UNLOCKED_KEY, 0);
        if (YG2.saves != null) YG2.saves.vipUnlocked = false;
        SetRegularCoins(0);
        SetSilverCoins(0);
        if (YG2.saves != null)
        {
            YG2.saves.silverCoins = 0;
            YG2.saves.coinsCollected = 0;
        }

        var currentStats = YG2.GetAllStats();
        Dictionary<string, int> cleaned = new Dictionary<string, int>();
        foreach (var stat in currentStats)
        {
            if (stat.Key != PENDING_ADS_DISABLED_KEY &&
                stat.Key != ADS_DISABLED_KEY &&
                stat.Key != REGULAR_COINS_KEY &&
                stat.Key != SILVER_COINS_KEY &&
                stat.Key != VIP_UNLOCKED_KEY)
            {
                cleaned[stat.Key] = stat.Value;
            }
        }
        YG2.SetAllStats(cleaned);
        YG2.SaveProgress();
    }

    private IEnumerator GuaranteedSaveCoroutine(string key, int value)
    {
        int attempts = 0;
        bool saved = false;
        while (attempts < maxSaveAttempts && !saved)
        {
            attempts++;
            YG2.SetState(key, value);
            yield return new WaitForSecondsRealtime(saveAttemptDelay);
            if (YG2.GetState(key) == value) saved = true;
        }
        if (!saved) Debug.LogError($"❌ Не удалось сохранить {key}");
        YG2.SaveProgress();
    }

    [ContextMenu("Очистить все Player Stats")]
    public void ClearAllStats()
    {
        if (!_sdkReady) return;
        YG2.SetAllStats(new Dictionary<string, int>());
    }
}
using UnityEngine;
using YG;
using System;
using System.Collections;
using System.Collections.Generic;

public class SaveManager : MonoBehaviour
{
    [Header("Настройки автосохранения")]
    public float autoSaveInterval = 120f;
    public bool enableAutoSave = true;

    private FortuneWheel fortuneWheel;
    private KinematicCharacterController.Examples.ExampleCharacterController playerController;
    private WorldSystemManager worldSystemManager;
    private PetSystem petSystem;
    private AutoInterstitialAd autoInterstitialAd;
    private PlayerStatsManager _playerStatsManager;
    private bool isDataLoaded = false;
    private float autoSaveTimer = 0f;
    private bool _isSaving = false;
    private int _pendingSaveRequests = 0;

    private void Awake()
    {
        if (YG2.saves == null)
            YG2.saves = new SavesYG();
        
        playerController = FindFirstObjectByType<KinematicCharacterController.Examples.ExampleCharacterController>();
        fortuneWheel = FindFirstObjectByType<FortuneWheel>();
        worldSystemManager = FindFirstObjectByType<WorldSystemManager>();
        petSystem = FindFirstObjectByType<PetSystem>();
        autoInterstitialAd = FindFirstObjectByType<AutoInterstitialAd>();
        _playerStatsManager = FindFirstObjectByType<PlayerStatsManager>();
        
        isDataLoaded = false;
        Debug.Log("✅ SaveManager: инициализация в Awake, данные ещё не загружены");
    }

    private void Start()
    {
        YG2.onGetSDKData += OnSDKDataLoaded;
        if (YG2.isSDKEnabled)
        {
            OnSDKDataLoaded();
        }
    }

    private void OnDestroy()
    {
        YG2.onGetSDKData -= OnSDKDataLoaded;
    }

    private void OnSDKDataLoaded()
    {
        ApplySaveData();
        isDataLoaded = true;
        
        fortuneWheel?.OnGameLoad();
        petSystem?.OnGameLoad();
        worldSystemManager?.UpdateTeleportUIPanelsVisibility();
        
        if (playerController != null && worldSystemManager != null && isDataLoaded)
        {
            worldSystemManager.TeleportToLocation(YG2.saves.currentLocationID);
        }
        
        Debug.Log("✅ SaveManager: данные применены после загрузки SDK");
    }

    private void Update()
    {
        if (!isDataLoaded) return;
        
        if (enableAutoSave)
        {
            autoSaveTimer += Time.deltaTime;
            if (autoSaveTimer >= autoSaveInterval)
            {
                RequestSave("autosave");
                autoSaveTimer = 0f;
            }
        }
        
        if (YG2.saves != null)
        {
            YG2.saves.totalPlayTime += Time.deltaTime;
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && isDataLoaded)
        {
            RequestSave("pause", true);
        }
    }

    private void OnApplicationQuit()
    {
        if (isDataLoaded)
        {
            RequestSave("quit", true);
        }
    }

    public void SaveGameData()
    {
        SaveImmediately("legacy_call");
    }

    public void RequestSave(string reason = "unknown", bool immediate = false)
    {
        if (!isDataLoaded) return;
        
        Debug.Log($"💾 Запрос на сохранение: {reason} (immediate={immediate})");
        
        if (immediate)
        {
            PerformSave(reason);
        }
        else
        {
            _pendingSaveRequests++;
            if (_pendingSaveRequests == 1)
            {
                StartCoroutine(DelayedSave());
            }
        }
    }

    private IEnumerator DelayedSave()
    {
        yield return new WaitForSeconds(1f);
        if (_pendingSaveRequests > 0)
        {
            PerformSave("delayed");
            _pendingSaveRequests = 0;
        }
    }

    public void SaveImmediately(string reason)
    {
        RequestSave(reason, true);
    }

    private void PerformSave(string reason)
    {
        if (_isSaving)
        {
            Debug.Log("⚠️ Сохранение уже выполняется, пропускаем");
            return;
        }
        
        _isSaving = true;
        UpdateSaveData();
        YG2.SaveProgress();
        Debug.Log($"💾 Сохранение выполнено: {reason}");
        _isSaving = false;
    }

    private void UpdateSaveData()
    {
        if (YG2.saves == null) return;
        
        YG2.saves.lastSaveTimeUnix = (float)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        
        if (playerController != null)
        {
            YG2.saves.coinsCollected = playerController.CoinsCollected;
            YG2.saves.silverCoins = playerController.SilverCoins;
            YG2.saves.cursorLocked = playerController.cursorLocked;
        }
        
        if (fortuneWheel != null)
        {
            YG2.saves.fortuneWheelSpins = fortuneWheel.GetCurrentSpins();
            YG2.saves.timeUntilAdSpinAvailable = fortuneWheel.TimeUntilAdSpinAvailable;
        }
        
        if (worldSystemManager != null)
        {
            YG2.saves.currentLocationID = worldSystemManager.CurrentLocationID;
            YG2.saves.maxAchievedVirtualHeight = worldSystemManager.GetMaxAchievedVirtualHeight();
            YG2.saves.trophiesCollected = worldSystemManager.TrophiesCollected;
            
            YG2.saves.purchasedTeleportTriggers.Clear();
            foreach (var teleport in worldSystemManager.TeleportTriggers)
            {
                if (teleport.WasBought && !string.IsNullOrEmpty(teleport.TriggerID))
                {
                    YG2.saves.purchasedTeleportTriggers.Add(teleport.TriggerID);
                }
            }
            
            YG2.saves.teleportPanelUnlocked.Clear();
            foreach (var panel in worldSystemManager.TeleportUIPanels)
            {
                YG2.saves.teleportPanelUnlocked.Add(panel.WasUnlocked);
            }
        }
        
        if (petSystem != null)
        {
            petSystem.SavePetsData();
        }
        
        if (autoInterstitialAd != null)
        {
            YG2.saves.adsDisabled = autoInterstitialAd.IsAdsDisabled();
        }
        
        if (LeaderboardManager.Instance != null)
        {
            YG2.saves.ladderCompletionCount = LeaderboardManager.Instance.GetLadderCompletionCount();
        }
        
        if (_playerStatsManager != null && playerController != null)
        {
            _playerStatsManager.SetRegularCoins((long)playerController.CoinsCollected);
            _playerStatsManager.SetSilverCoins(playerController.SilverCoins);
        }
    }

public void ApplySaveData()
{
    if (YG2.saves == null) return;
    
    if (_playerStatsManager != null)
    {
        long statsCoins = _playerStatsManager.GetRegularCoins();
        if (statsCoins > 0 && (playerController == null || playerController.CoinsCollected < statsCoins))
        {
            Debug.Log($"💰 SaveManager: восстанавливаем монеты из Player Stats: {statsCoins}");
            YG2.saves.coinsCollected = statsCoins;
        }
        
        int statsSilver = _playerStatsManager.GetSilverCoins();
        if (statsSilver > 0 && (playerController == null || playerController.SilverCoins < statsSilver))
        {
            Debug.Log($"🥈 SaveManager: восстанавливаем серебро из Player Stats: {statsSilver}");
            YG2.saves.silverCoins = statsSilver;
        }
    }
    
    if (playerController != null)
    {
        playerController.SetCoins(YG2.saves.coinsCollected);
        playerController.SetSilverCoins(YG2.saves.silverCoins);
        playerController.SetCursorLocked(YG2.saves.cursorLocked);
    }
    
    if (fortuneWheel != null)
    {
        fortuneWheel.SetSpins(YG2.saves.fortuneWheelSpins);
        fortuneWheel.TimeUntilAdSpinAvailable = YG2.saves.timeUntilAdSpinAvailable;
    }
    
    if (worldSystemManager != null)
    {
        worldSystemManager.TrophiesCollected = YG2.saves.trophiesCollected;
        
        foreach (var teleport in worldSystemManager.TeleportTriggers)
        {
            if (!string.IsNullOrEmpty(teleport.TriggerID))
            {
                teleport.WasBought = YG2.saves.purchasedTeleportTriggers.Contains(teleport.TriggerID);
            }
        }
        
        var panels = worldSystemManager.TeleportUIPanels;
        var savedStates = YG2.saves.teleportPanelUnlocked;
        for (int i = 0; i < panels.Count && i < savedStates.Count; i++)
        {
            panels[i].WasUnlocked = savedStates[i];
        }
        
        worldSystemManager.CurrentLocationID = YG2.saves.currentLocationID;
    }
    
    if (LeaderboardManager.Instance != null)
    {
        LeaderboardManager.Instance.SetLadderCompletionCount(YG2.saves.ladderCompletionCount);
    }
}

    public void OnDonatePetPurchased()
    {
        SaveImmediately("donate_pet");
        Debug.Log("🎁 Сохранение после покупки донатного питомца");
    }

    // 🔥 ОБНОВЛЁННЫЙ МЕТОД: полный сброс с синхронизацией VIP и монет
    [ContextMenu("Сбросить сохранения")]
    public void ResetSaveData()
    {
        Debug.Log("🔄 Начинаем полный сброс ВСЕХ сохранений (включая донатные покупки)...");
        
        // 1. Сброс стандартных сохранений
        YG2.SetDefaultSaves();
        
        // 2. Явный сброс донатных данных в облаке
        if (YG2.saves != null)
        {
            YG2.saves.vipUnlocked = false;
            YG2.saves.silverCoins = 0;
            YG2.saves.coinsCollected = 0;
            YG2.saves.adsDisabled = false;
            YG2.saves.pendingAdsDisabled = false;
            YG2.saves.ownedPets?.Clear();
        }

        // 3. Сброс донатных покупок через PlayerStatsManager
        if (_playerStatsManager != null)
        {
            _playerStatsManager.ResetAllDonatePurchases();
            Debug.Log("✅ Player Stats (донатные покупки) сброшены");
        }
        else
        {
            _playerStatsManager = FindFirstObjectByType<PlayerStatsManager>();
            if (_playerStatsManager != null)
            {
                _playerStatsManager.ResetAllDonatePurchases();
            }
            else
            {
                Debug.LogWarning("PlayerStatsManager не найден, создаем временный");
                GameObject tempStats = new GameObject("TempPlayerStats");
                var tempManager = tempStats.AddComponent<PlayerStatsManager>();
                tempManager.ResetAllDonatePurchases();
                Destroy(tempStats);
            }
        }
        
        // 4. Принудительное сохранение в облако
        YG2.SaveProgress();
        
        // 5. Применяем данные к игровым объектам
        ApplySaveData();
        
        // 6. Обновляем UI VIP с небольшой задержкой
        var vipManager = FindFirstObjectByType<VIPManager>();
        if (vipManager != null)
        {
            StartCoroutine(RefreshVIPAfterReset(vipManager));
        }
        
        Debug.Log("🎉 Полный сброс всех сохранений выполнен!");
    }

    private IEnumerator RefreshVIPAfterReset(VIPManager vipManager)
    {
        yield return new WaitForSecondsRealtime(0.15f);
        vipManager.OnVIPStatusReset();
    }

    public void ResetAllData()
    {
        Debug.Log("🔄 Вызван сброс всех данных (включая Player Stats) из консоли");
        ResetSaveData();
    }

    public bool HasSaveData()
    {
        return YG2.saves != null && YG2.saves.lastSaveTimeUnix > 0;
    }

    public static DateTime UnixToDateTime(float unixTime)
    {
        return new DateTime(1970, 1, 1).AddSeconds(unixTime).ToLocalTime();
    }

    public static float DateTimeToUnix(DateTime dateTime)
    {
        return (float)(dateTime.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds;
    }
}
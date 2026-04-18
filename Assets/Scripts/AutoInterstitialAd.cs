using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using YG;
using YG.Utils.Pay;
using KinematicCharacterController.Examples;

public class AutoInterstitialAd : MonoBehaviour
{
    [Header("UI таймера")]
    public GameObject adCountdownPanel;
    public TextMeshProUGUI countdownText;
    
    [Header("Кнопка отключения рекламы")]
    public Button disableAdsButton;
    public TextMeshProUGUI disableAdsButtonText;
    
    [Header("Ads Disabled Marker")]
    public GameObject adsDisabledMarker;
    
    [Header("Панель получения")]
    public GameObject receivedPanel;
    public Image receivedPanelImage;
    public Sprite adsDisabledSprite;
    
    [Header("Настройки")]
    public float countdownDuration = 2f;
    public float minInterval = 60f;
    public float receivedPanelDisplayTime = 3f;

    private float _lastAdTime = 0f;
    private Coroutine _currentCoroutine = null;
    private ExampleCharacterController _characterController;
    private bool _isInitialized = false;
    private Coroutine _hideReceivedPanelCoroutine;
    private bool _isProcessingPurchase = false;
    
    // 🔥 Ссылки на менеджеры
    private SaveManager _saveManager;
    private PlayerStatsManager _playerStatsManager;

    void Start()
    {
        _characterController = FindFirstObjectByType<ExampleCharacterController>();
        _saveManager = FindFirstObjectByType<SaveManager>();
        _playerStatsManager = FindFirstObjectByType<PlayerStatsManager>();
        
        // Если нет в сцене, создаем
        if (_playerStatsManager == null)
        {
            GameObject statsGO = new GameObject("PlayerStatsManager");
            _playerStatsManager = statsGO.AddComponent<PlayerStatsManager>();
        }

        YG2.onPurchaseSuccess += OnPurchaseSuccess;
        YG2.onGetPayments += OnPaymentsLoaded;
        YG2.onGetSDKData += OnSDKDataLoaded;
        
        if (disableAdsButton != null)
            disableAdsButton.onClick.AddListener(OnDisableAdsButtonClick);

        StartCoroutine(InitializeAfterSDK());
        StartCoroutine(AdLoop());
        
        if (receivedPanel != null)
            receivedPanel.SetActive(false);
    }

    // ✅ ИСПРАВЛЕНО: ждем инициализацию SDK через YG2InitializationManager
    private IEnumerator InitializeAfterSDK()
    {
        Debug.Log("🔄 AutoInterstitialAd: Ожидание инициализации SDK...");
        
        while (!YG2InitializationManager.CanAccessSaves())
        {
            yield return null;
        }
        
        yield return new WaitForSeconds(0.5f);
        RestoreAdsDisabledState();
        _isInitialized = true;
        Debug.Log("✅ AutoInterstitialAd: инициализация завершена");
    }

    private void OnSDKDataLoaded()
    {
        Debug.Log("📦 AutoInterstitialAd: получены новые данные из SDK");
        if (YG2InitializationManager.CanAccessSaves())
        {
            RestoreAdsDisabledState();
        }
    }

    // ✅ ИСПРАВЛЕНО: безопасный доступ к YG2.saves
    private void RestoreAdsDisabledState()
    {
        if (!YG2InitializationManager.CanAccessSaves())
        {
            Debug.LogWarning("⚠️ AutoInterstitialAd: YG2.saves ещё не доступен");
            return;
        }
        
        bool adsDisabled = YG2.saves.adsDisabled;
        
        // 🔥 Также проверяем Player Stats
        if (_playerStatsManager != null && _playerStatsManager.GetAdsDisabled())
        {
            adsDisabled = true;
        }
        
        Debug.Log($"🔍 Восстановление состояния рекламы: adsDisabled = {adsDisabled}");
        
        if (adsDisabledMarker != null)
            adsDisabledMarker.SetActive(adsDisabled);
        
        if (disableAdsButton != null)
        {
            disableAdsButton.gameObject.SetActive(!adsDisabled);
            if (!adsDisabled)
            {
                UpdateDisableAdsButtonText();
            }
        }
    }

    // 🔥 Публичный метод для применения отключения из PlayerStatsManager
    public void ApplyAdsDisabledFromStats()
    {
        Debug.Log("🎯 Применяем отключение рекламы из Player Stats");
        ApplyAdsDisabled();
        ShowReceivedPanel();
        
        if (YG2InitializationManager.CanAccessSaves())
        {
            YG2.saves.adsDisabled = true;
            if (_saveManager != null)
            {
                _saveManager.SaveImmediately("stats_restore");
            }
        }
    }

    private void ApplyAdsDisabled()
    {
        if (adsDisabledMarker != null)
            adsDisabledMarker.SetActive(true);
        if (disableAdsButton != null)
            disableAdsButton.gameObject.SetActive(false);
    }

    private void ShowReceivedPanel()
    {
        if (receivedPanel != null)
        {
            receivedPanel.SetActive(true);
            if (receivedPanelImage != null && adsDisabledSprite != null)
            {
                receivedPanelImage.sprite = adsDisabledSprite;
            }
            if (_hideReceivedPanelCoroutine != null)
                StopCoroutine(_hideReceivedPanelCoroutine);
            _hideReceivedPanelCoroutine = StartCoroutine(HideReceivedPanelAfterDelay());
        }
    }

    private IEnumerator HideReceivedPanelAfterDelay()
    {
        yield return new WaitForSecondsRealtime(receivedPanelDisplayTime);
        if (receivedPanel != null)
            receivedPanel.SetActive(false);
        _hideReceivedPanelCoroutine = null;
    }

    private void OnDisableAdsButtonClick()
    {
        Debug.Log("🛒 Нажата кнопка отключения рекламы");
        YG2.BuyPayments("disable_ads");
    }

    // 🔥 ОБНОВЛЕННЫЙ МЕТОД: используем Player Stats для мгновенного сохранения
    private void OnPurchaseSuccess(string purchaseId)
    {
        if (purchaseId == "disable_ads")
        {
            if (_isProcessingPurchase)
            {
                Debug.Log("⚠️ Покупка уже обрабатывается");
                return;
            }
            
            _isProcessingPurchase = true;
            Debug.Log("✅ OnPurchaseSuccess сработал! Покупка подтверждена платформой");
            
            // Мгновенно обновляем UI
            ApplyAdsDisabled();
            ShowReceivedPanel();
            
            // 🔥 МГНОВЕННОЕ СОХРАНЕНИЕ ЧЕРЕЗ PLAYER STATS
            if (_playerStatsManager != null)
            {
                _playerStatsManager.SetPendingAdsDisabled(true);
                _playerStatsManager.SetAdsDisabled(true);
                Debug.Log("⚡ Pending-флаг мгновенно сохранен в Player Stats");
            }
            
            // Сохраняем в основное облако (для остальных данных)
            if (YG2InitializationManager.CanAccessSaves())
            {
                YG2.saves.adsDisabled = true;
                YG2.saves.pendingAdsDisabled = true;
                if (_saveManager != null)
                {
                    _saveManager.SaveImmediately("ads_disable_purchase");
                    Debug.Log("💾 Сохранение через SaveManager");
                }
                else
                {
                    YG2.SaveProgress();
                }
            }
            
            _isProcessingPurchase = false;
        }
    }

    private void OnPaymentsLoaded()
    {
        Debug.Log("💰 Данные о покупках загружены");
        CheckForUnconsumedPurchases();
        if (disableAdsButton != null && disableAdsButton.gameObject.activeSelf)
        {
            UpdateDisableAdsButtonText();
        }
    }

    private void CheckForUnconsumedPurchases()
    {
        if (YG2.purchases == null) return;
        
        foreach (var purchase in YG2.purchases)
        {
            if (purchase.id == "disable_ads" && !purchase.consumed)
            {
                Debug.Log("🔍 Найдена непотребленная покупка disable_ads! Применяем...");
                ApplyAdsDisabled();
                ShowReceivedPanel();
                
                if (YG2InitializationManager.CanAccessSaves())
                {
                    YG2.saves.adsDisabled = true;
                    if (_saveManager != null)
                    {
                        _saveManager.SaveImmediately("unconsumed_purchase");
                    }
                    else
                    {
                        YG2.SaveProgress();
                    }
                }
                break;
            }
        }
    }

    private void UpdateDisableAdsButtonText()
    {
        if (disableAdsButtonText == null) return;
        
        var purchase = YG2.PurchaseByID("disable_ads");
        if (purchase != null && !string.IsNullOrEmpty(purchase.priceCurrencyCode))
        {
            string buyText = GetLocalizedText("");
            string currency = purchase.priceCurrencyCode.ToUpper();
            disableAdsButtonText.text = $"{buyText} {purchase.priceValue} {currency}";
            disableAdsButton.interactable = true;
        }
        else
        {
            disableAdsButtonText.text = GetLocalizedText("loading");
            disableAdsButton.interactable = false;
        }
    }

    private IEnumerator AdLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (!_isInitialized) continue;
            if (IsAdsDisabled()) continue;
            
            if (Time.time - _lastAdTime >= minInterval)
            {
                ShowAdWithCountdown();
                yield return new WaitUntil(() => _currentCoroutine == null);
            }
        }
    }

    private void ShowAdWithCountdown()
    {
        if (_currentCoroutine != null || IsAdsDisabled()) return;
        _currentCoroutine = StartCoroutine(CountdownAndShowAd());
    }

    private IEnumerator CountdownAndShowAd()
    {
        if (IsAdsDisabled())
        {
            _currentCoroutine = null;
            yield break;
        }

        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        
        if (_characterController != null)
            _characterController.OnUIOrAdOpened();
        
        if (adCountdownPanel != null)
            adCountdownPanel.SetActive(true);

        float timer = countdownDuration;
        while (timer > 0)
        {
            if (IsAdsDisabled()) break;
            int sec = Mathf.CeilToInt(timer);
            string text = $"{GetLocalizedText("ad_in")} {sec}";
            if (countdownText != null) countdownText.text = text;
            yield return new WaitForSecondsRealtime(1f);
            timer -= 1f;
        }

        if (adCountdownPanel != null)
            adCountdownPanel.SetActive(false);
        if (_characterController != null)
            _characterController.OnUIOrAdClosed();
        
        Time.timeScale = originalTimeScale;

        if (!IsAdsDisabled())
        {
            Debug.Log("📺 Показ межстраничной рекламы");
            YG2.InterstitialAdvShow();
        }
        
        _lastAdTime = Time.time;
        _currentCoroutine = null;
    }

    public bool IsAdsDisabled()
    {
        // Приоритет: сначала маркер
        if (adsDisabledMarker != null && adsDisabledMarker.activeInHierarchy)
            return true;
        
        // Проверяем Player Stats (самое быстрое)
        if (_playerStatsManager != null && _playerStatsManager.GetAdsDisabled())
            return true;
        
        // Затем обычные сохранения (с проверкой)
        if (YG2InitializationManager.CanAccessSaves())
            return YG2.saves.adsDisabled;
        
        return false;
    }

    private string GetLocalizedText(string key)
    {
        bool en = YG2.lang == "en";
        return key switch
        {
            "ad_in" => en ? "Ad in:" : "Реклама через:",
            "loading" => en ? "Loading..." : "Загрузка...",
            _ => en ? key.ToUpper() : key.ToUpper()
        };
    }

    private void OnDestroy()
    {
        YG2.onPurchaseSuccess -= OnPurchaseSuccess;
        YG2.onGetPayments -= OnPaymentsLoaded;
        YG2.onGetSDKData -= OnSDKDataLoaded;
        if (disableAdsButton != null)
            disableAdsButton.onClick.RemoveListener(OnDisableAdsButtonClick);
        if (_hideReceivedPanelCoroutine != null)
            StopCoroutine(_hideReceivedPanelCoroutine);
    }

#if UNITY_EDITOR
    [ContextMenu("Сбросить отключение рекламы (тест)")]
    public void ResetAdRemovalForTesting()
    {
        if (adsDisabledMarker != null)
            adsDisabledMarker.SetActive(false);
        
        if (YG2InitializationManager.CanAccessSaves())
        {
            YG2.saves.adsDisabled = false;
            YG2.saves.pendingAdsDisabled = false;
        }
        
        // Сбрасываем Player Stats
        if (_playerStatsManager != null)
        {
            _playerStatsManager.SetPendingAdsDisabled(false);
            _playerStatsManager.SetAdsDisabled(false);
        }
        
        if (_saveManager != null)
        {
            _saveManager.SaveImmediately("test_reset");
        }
        else if (YG2InitializationManager.CanAccessSaves())
        {
            YG2.SaveProgress();
        }
        
        if (disableAdsButton != null)
        {
            disableAdsButton.gameObject.SetActive(true);
            UpdateDisableAdsButtonText();
        }
        
        Debug.Log("🔄 Тестовое сброс отключения рекламы выполнен");
    }
#endif
}
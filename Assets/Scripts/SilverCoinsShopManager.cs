using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YG;
using YG.Utils.Pay;
using System.Collections;
using System.Collections.Generic;
using KinematicCharacterController.Examples;

/// <summary>
/// Управление покупкой серебряных монет за реальные деньги (IAP).
/// 🔥 Предложения настраиваются через консоль Яндекс Игр (Payments).
/// 🔥 Покупки консумируются и мгновенно сохраняются согласно документации YG2.
/// </summary>
public class SilverCoinsShopManager : MonoBehaviour
{
    [Header("Настройки товаров (Payments)")]
    [Tooltip("ID товаров из консоли Яндекс Игр в том же порядке, что и кнопки")]
    public List<string> silverCoinProductIds = new List<string>();
    
    [Tooltip("Количество серебряных монет за каждый товар (в том же порядке)")]
    public List<int> silverCoinsAmounts = new List<int>();

    [Header("UI Кнопки покупки")]
    [Tooltip("Кнопки покупки в том же порядке, что и ID товаров")]
    public List<Button> buyButtons = new List<Button>();
    
    [Tooltip("Тексты цен для кнопок (в том же порядке)")]
    public List<TextMeshProUGUI> priceTexts = new List<TextMeshProUGUI>();
    
    [Tooltip("Тексты количества монет для кнопок (в том же порядке)")]
    public List<TextMeshProUGUI> coinsAmountTexts = new List<TextMeshProUGUI>();

    [Header("Панель магазина серебряных монет")]
    public GameObject silverShopPanel;
    public Button openSilverShopButton;
    public Button openSilverShopButton2;
    public Button closeSilverShopButton;

    [Header("Панель успешной покупки")]
    public GameObject purchaseSuccessPanel;
    public Image purchasedCoinsImage;
    public TextMeshProUGUI purchasedCoinsAmountText;
    public Button closeSuccessButton;

    [Header("Счётчик серебряных монет")]
    [Tooltip("Первый текст для отображения баланса серебряных монет")]
    public TextMeshProUGUI silverCoinsCounterText1;
    
    [Tooltip("Второй текст для отображения баланса серебряных монет")]
    public TextMeshProUGUI silverCoinsCounterText2;
    
    [Tooltip("Автоматически обновлять счётчики при изменении монет")]
    public bool autoUpdateCounters = true;
    
    [Tooltip("Интервал обновления счётчиков в секундах (если autoUpdateCounters = true)")]
    public float updateInterval = 0.5f;

    [Header("Ссылки на другие системы")]
    [Tooltip("PetSystem — для открытия магазина при нехватке монет")]
    public PetSystem petSystem;
    [Tooltip("CoinShopManager — для координации покупок")]
    public CoinShopManager coinShopManager;

    [Header("Настройки надёжности")]
    [Tooltip("Количество повторных попыток сохранения в Cloud Saves")]
    public int saveRetryCount = 3;
    [Tooltip("Задержка между попытками сохранения (сек)")]
    public float saveRetryDelay = 0.5f;

    private ExampleCharacterController _playerController;
    private PlayerStatsManager _playerStatsManager;
    private SaveManager _saveManager;
    private bool _isProcessingPurchase = false;
    private int _lastSilverCoinsValue = -1;
    private float _updateTimer = 0f;

    private void Awake()
    {
        // Подписка на события покупок
        YG2.onPurchaseSuccess += OnPurchaseSuccess;
        YG2.onGetPayments += UpdateAllPrices;
    }

    private void Start()
    {
        _playerController = FindFirstObjectByType<ExampleCharacterController>();
        _playerStatsManager = FindFirstObjectByType<PlayerStatsManager>();
        _saveManager = FindFirstObjectByType<SaveManager>();

        // Создаём PlayerStatsManager если его нет
        if (_playerStatsManager == null)
        {
            GameObject statsGO = new GameObject("PlayerStatsManager");
            _playerStatsManager = statsGO.AddComponent<PlayerStatsManager>();
        }

        SetupButtons();
        UpdateAllPrices();
        
        // 🔥 Восстанавливаем серебряные монеты из Player Stats при старте
        RestoreSilverCoinsFromStats();
        
        // 🔥 Обновляем счётчик серебряных монет
        UpdateSilverCoinsCounter();
        
        // Сохраняем текущее значение для отслеживания изменений
        if (_playerController != null)
        {
            _lastSilverCoinsValue = _playerController.SilverCoins;
        }
    }

    private void OnDestroy()
    {
        YG2.onPurchaseSuccess -= OnPurchaseSuccess;
        YG2.onGetPayments -= UpdateAllPrices;
    }

    private void Update()
    {
        // 🔥 Автоматическое обновление счётчиков при изменении монет
        if (autoUpdateCounters && _playerController != null)
        {
            _updateTimer += Time.deltaTime;
            
            if (_updateTimer >= updateInterval)
            {
                _updateTimer = 0f;
                
                int currentSilver = _playerController.SilverCoins;
                
                // Обновляем только если значение изменилось
                if (currentSilver != _lastSilverCoinsValue)
                {
                    _lastSilverCoinsValue = currentSilver;
                    UpdateSilverCoinsCounter();
                }
            }
        }
    }

    private void SetupButtons()
    {
        // Кнопки предложений
        for (int i = 0; i < buyButtons.Count; i++)
        {
            int index = i;
            if (buyButtons[index] != null)
            {
                buyButtons[index].onClick.RemoveAllListeners();
                buyButtons[index].onClick.AddListener(() => BuySilverCoinsOffer(index));
            }
        }

        // 🔥 Кнопка открытия магазина 1
        if (openSilverShopButton != null)
        {
            openSilverShopButton.onClick.RemoveAllListeners();
            openSilverShopButton.onClick.AddListener(OpenSilverShop);
        }

        // 🔥 Кнопка открытия магазина 2 (дополнительная)
        if (openSilverShopButton2 != null)
        {
            openSilverShopButton2.onClick.RemoveAllListeners();
            openSilverShopButton2.onClick.AddListener(OpenSilverShop);
        }

        // Кнопка закрытия магазина
        if (closeSilverShopButton != null)
        {
            closeSilverShopButton.onClick.RemoveAllListeners();
            closeSilverShopButton.onClick.AddListener(CloseSilverShop);
        }

        // Кнопка закрытия панели успеха
        if (closeSuccessButton != null)
        {
            closeSuccessButton.onClick.RemoveAllListeners();
            closeSuccessButton.onClick.AddListener(() => purchaseSuccessPanel.SetActive(false));
        }

        // 🔥 Интеграция с PetSystem: открываем магазин при нехватке монет
        if (petSystem != null)
        {
            petSystem.onNotEnoughSilverCoins += OpenSilverShop;
        }
    }

    // === ОТКРЫТИЕ/ЗАКРЫТИЕ ПАНЕЛИ ===
    public void OpenSilverShop()
    {
        if (silverShopPanel != null)
        {
            silverShopPanel.SetActive(true);
            _playerController?.OnUIOrAdOpened();
            UpdateAllPrices();
            UpdateSilverCoinsCounter(); // 🔥 Обновляем счётчик при открытии
        }
    }

    public void CloseSilverShop()
    {
        if (silverShopPanel != null)
        {
            silverShopPanel.SetActive(false);
            _playerController?.OnUIOrAdClosed();
        }
    }

    public void ToggleSilverShop()
    {
        if (silverShopPanel == null) return;
        if (silverShopPanel.activeSelf)
            CloseSilverShop();
        else
            OpenSilverShop();
    }

    // === ПОКУПКА СЕРЕБРЯНЫХ МОНЕТ ===
    private void BuySilverCoinsOffer(int offerIndex)
    {
        if (_isProcessingPurchase) return;
        if (offerIndex < 0 || offerIndex >= silverCoinProductIds.Count) return;

        string productId = silverCoinProductIds[offerIndex];
        Debug.Log($"🛒 Покупка серебряных монет: {productId}");
        
        _isProcessingPurchase = true;
        YG2.BuyPayments(productId);
    }

    // === ОБРАБОТКА УСПЕШНОЙ ПОКУПКИ ===
    private void OnPurchaseSuccess(string productId)
    {
        int offerIndex = silverCoinProductIds.IndexOf(productId);
        if (offerIndex == -1) return; // Это не покупка серебряных монет

        int coinsAmount = (offerIndex < silverCoinsAmounts.Count) ? silverCoinsAmounts[offerIndex] : 0;

        Debug.Log($"✅ Покупка успешна: {productId} ({coinsAmount} серебряных монет)");

        // 1. Выдаём серебряные монеты игроку
        if (_playerController != null && coinsAmount > 0)
        {
            _playerController.AddSilverCoins(coinsAmount);
            Debug.Log($"💰 Выдано {coinsAmount} серебряных монет");
        }

        // 2. 🔥 МГНОВЕННО сохраняем в Player Stats (источник правды для доната)
        if (_playerStatsManager != null && _playerController != null)
        {
            _playerStatsManager.SetSilverCoins(_playerController.SilverCoins);
            Debug.Log("💾 Серебряные монеты сохранены в Player Stats");
        }

        // 3. 🔥 Консумируем покупку согласно документации YG2
        StartCoroutine(ConsumeAndSaveWithRetry(productId, coinsAmount));

        // 4. Показываем панель успеха
        ShowPurchaseSuccess(coinsAmount);
        
        // 🔥 Обновляем счётчик серебряных монет после покупки
        UpdateSilverCoinsCounter();
    }

    // === КОНСУМИРОВАНИЕ + СОХРАНЕНИЕ С ПОВТОРНЫМИ ПОПЫТКАМИ ===
    private IEnumerator ConsumeAndSaveWithRetry(string productId, int coinsGranted)
    {
        // 🔥 Шаг 1: Консумируем покупку (обязательно по документации YG2)
        Debug.Log("🔄 Консумирование покупки...");
        YG2.ConsumePurchaseByID(productId, true);
        Debug.Log($"✅ Покупка {productId} отправлена на консумирование");

        // 🔥 Шаг 2: Сохраняем в Cloud Saves с повторными попытками
        for (int attempt = 1; attempt <= saveRetryCount; attempt++)
        {
            Debug.Log($"💾 Cloud Saves: попытка #{attempt} (silver_purchase)");
            
            // Обновляем SavesYG из PlayerStatsManager для синхронизации
            if (YG2.saves != null && _playerStatsManager != null)
            {
                YG2.saves.silverCoins = _playerStatsManager.GetSilverCoins();
            }
            
            // Сохраняем через SaveManager или напрямую
            if (_saveManager != null)
            {
                _saveManager.SaveImmediately("silver_iap_purchase");
            }
            else
            {
                YG2.SaveProgress();
            }
            
            yield return new WaitForSecondsRealtime(saveRetryDelay);
        }
        
        Debug.Log($"✅ Silver IAP: консумирование и сохранение завершено");
        _isProcessingPurchase = false;
    }

    // === ОТОБРАЖЕНИЕ УСПЕШНОЙ ПОКУПКИ ===
    private void ShowPurchaseSuccess(int coinsAmount)
    {
        if (purchaseSuccessPanel != null && purchasedCoinsAmountText != null)
        {
            purchasedCoinsAmountText.text = $"+{coinsAmount}";
            purchaseSuccessPanel.SetActive(true);
            _playerController?.OnUIOrAdOpened();
        }
    }

    // === ОБНОВЛЕНИЕ ЦЕН И КОЛИЧЕСТВА МОНЕТ ИЗ КАТАЛОГА ===
    private void UpdateAllPrices()
    {
        for (int i = 0; i < buyButtons.Count; i++)
        {
            if (i >= silverCoinProductIds.Count) break;

            string productId = silverCoinProductIds[i];
            
            // === Обновление цены ===
            TextMeshProUGUI priceText = (i < priceTexts.Count && priceTexts[i] != null) ? priceTexts[i] : buyButtons[i]?.GetComponentInChildren<TextMeshProUGUI>();

            if (priceText != null)
            {
                var purchase = YG2.PurchaseByID(productId);
                if (purchase != null && !string.IsNullOrEmpty(purchase.priceCurrencyCode))
                {
                    string currencyCode = purchase.priceCurrencyCode.ToUpper();
                    priceText.text = $"{purchase.priceValue} {currencyCode}";
                    if (buyButtons[i] != null) 
                        buyButtons[i].interactable = true;
                }
                else
                {
                    priceText.text = "Загрузка...";
                    if (buyButtons[i] != null) 
                        buyButtons[i].interactable = false;
                }
            }
            
            // === Обновление количества монет ===
            if (i < coinsAmountTexts.Count && coinsAmountTexts[i] != null)
            {
                int amount = (i < silverCoinsAmounts.Count) ? silverCoinsAmounts[i] : 0;
                coinsAmountTexts[i].text = $"+{amount}";
            }
        }
    }

    // === 🔥 НОВЫЙ МЕТОД: Обновление счётчика серебряных монет (2 текста) ===
    public void UpdateSilverCoinsCounter()
    {
        if (_playerController == null) return;
        
        int currentSilver = _playerController.SilverCoins;
        string formattedValue = FormatNumber(currentSilver);
        
        // Обновляем первый текст, если назначен
        if (silverCoinsCounterText1 != null)
        {
            silverCoinsCounterText1.text = formattedValue;
        }
        
        // Обновляем второй текст, если назначен
        if (silverCoinsCounterText2 != null)
        {
            silverCoinsCounterText2.text = formattedValue;
        }
        
        // Обновляем последнее известное значение
        _lastSilverCoinsValue = currentSilver;
    }
    
    // === 🔥 Вспомогательный метод: форматирование числа (сокращение только от 1 миллиона) ===
    private string FormatNumber(int number)
    {
        if (number >= 1_000_000_000)
            return (number / 1_000_000_000f).ToString("0.##") + "B";
        else if (number >= 1_000_000)
            return (number / 1_000_000f).ToString("0.##") + "M";
        else
            return number.ToString(); // Без сокращения для значений меньше 1 миллиона
    }

    // === ВОССТАНОВЛЕНИЕ СЕРЕБРЯНЫХ МОНЕТ ИЗ PLAYER STATS ===
    private void RestoreSilverCoinsFromStats()
    {
        if (_playerStatsManager == null || _playerController == null) return;

        int savedSilver = _playerStatsManager.GetSilverCoins();
        int currentSilver = _playerController.SilverCoins;

        if (savedSilver > currentSilver)
        {
            Debug.Log($"🔄 Восстанавливаем серебряные монеты из Player Stats: {savedSilver}");
            _playerController.SetSilverCoins(savedSilver);
            
            // Синхронизируем с SavesYG
            if (YG2.saves != null)
            {
                YG2.saves.silverCoins = savedSilver;
            }
        }
    }

    // === ПУБЛИЧНЫЙ МЕТОД: ОТКРЫТЬ МАГАЗИН ПРИ НЕХВАТКЕ МОНЕТ ===
    public void OpenSilverShopForInsufficientFunds()
    {
        OpenSilverShop();
    }

    // === СОБЫТИЕ ДЛЯ ИНТЕГРАЦИИ С PETSYSTEM ===
    public System.Action onNotEnoughSilverCoins;

    // === ПРОВЕРКА ДОСТАТОЧНОСТИ МОНЕТ ===
    public bool HasEnoughSilverCoins(int requiredAmount)
    {
        return _playerController != null && _playerController.SilverCoins >= requiredAmount;
    }

    // === ФОРСИРОВАННОЕ СОХРАНЕНИЕ (при сворачивании игры) ===
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Debug.Log("⚠️ Игра свёрнута — форсируем сохранение серебряных монет");
            ForceSaveSilverCoins();
        }
    }

    private void ForceSaveSilverCoins()
    {
        if (_playerController == null || _playerStatsManager == null) return;
        
        // Сохраняем в Player Stats
        _playerStatsManager.SetSilverCoins(_playerController.SilverCoins);
        
        // Синхронизируем с SavesYG
        if (YG2.saves != null)
        {
            YG2.saves.silverCoins = _playerStatsManager.GetSilverCoins();
        }
        
        // Сохраняем в Cloud
        if (_saveManager != null)
        {
            _saveManager.SaveImmediately("force_save_silver_pause");
        }
        else
        {
            YG2.SaveProgress();
        }
        
        // 🔥 Обновляем счётчик после сохранения
        UpdateSilverCoinsCounter();
    }

    [ContextMenu("Логировать товары")]
    public void LogProducts()
    {
        Debug.Log("📋 Товары серебряных монет:");
        for (int i = 0; i < silverCoinProductIds.Count; i++)
        {
            string id = silverCoinProductIds[i];
            int amount = (i < silverCoinsAmounts.Count) ? silverCoinsAmounts[i] : 0;
            Debug.Log($"   - ID: {id}, Amount: {amount}");
        }
    }
    
    [ContextMenu("Обновить счётчик монет")]
    public void RefreshCoinsCounter()
    {
        UpdateSilverCoinsCounter();
        Debug.Log("🔄 Счётчик серебряных монет обновлён");
    }
}
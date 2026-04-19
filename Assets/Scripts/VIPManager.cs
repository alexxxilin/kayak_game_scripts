using UnityEngine;
using UnityEngine.UI;
using YG;
using YG.Utils.Pay;
using System.Collections.Generic;
using TMPro;

public class VIPManager : MonoBehaviour
{
    [Header("Настройки покупки")]
    [SerializeField] private string vipPurchaseId = "vip_access";
    
    [Tooltip("Автоматически консумировать необработанные покупки при старте (требуется для Яндекс Игр)")]
    [SerializeField] private bool autoConsumeOnStart = true;

    [Header("UI")]
    [SerializeField] private GameObject vipPanel;
    [SerializeField] private Button purchaseButton;
    
    [Tooltip("Текст для отображения цены товара (автоматически заполняется)")]
    [SerializeField] private TextMeshProUGUI priceDisplayText;
    
    [Tooltip("Текст-заглушка, пока цена загружается")]
    [SerializeField] private string loadingPriceText = "Загрузка...";

    [Header("Триггер-зоны")]
    [Tooltip("Коллайдеры, при входе в которые показывается панель")]
    [SerializeField] private List<Collider> triggerZones;
    
    [Tooltip("Объекты, которые нужно скрыть после покупки VIP")]
    [SerializeField] private List<GameObject> triggerObjectsToHide;

    private bool vipUnlocked = false;
    private Transform player;
    private Purchase _vipPurchaseInfo;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        StartCoroutine(InitializeVIP());
        
        // Подписываем кнопку на покупку и убеждаемся, что она активна
        if (purchaseButton != null)
        {
            purchaseButton.onClick.AddListener(BuyVIP);
            purchaseButton.interactable = true; // Гарантируем, что кнопка активна
        }
    }

    private void OnEnable() 
    {
        YG2.onPurchaseSuccess += OnPurchaseSuccess;
        YG2.onGetPayments += UpdatePriceDisplay;
    }
    
    private void OnDisable() 
    {
        YG2.onPurchaseSuccess -= OnPurchaseSuccess;
        YG2.onGetPayments -= UpdatePriceDisplay;
    }

    private System.Collections.IEnumerator InitializeVIP()
    {
        // 🔥 ШАГ 1: Ждём инициализации SDK
        while (!YG2InitializationManager.CanAccessSaves())
        {
            yield return null;
        }

        // 🔥 ШАГ 2: Консумирование необработанных покупок
        if (autoConsumeOnStart && YG2.isSDKEnabled)
        {
            Debug.Log($"🔄 VIPManager: проверка необработанных покупок для {vipPurchaseId}...");
            
            var pendingPurchase = YG2.PurchaseByID(vipPurchaseId);
            
            if (pendingPurchase != null)
            {
                Debug.Log($"📦 Purchase info: consumed={pendingPurchase.consumed}, price={pendingPurchase.priceValue} {pendingPurchase.priceCurrencyCode}");
            }
            
            if (pendingPurchase != null && !pendingPurchase.consumed)
            {
                Debug.Log("✅ Найдена необработанная покупка, консумируем...");
                YG2.ConsumePurchaseByID(vipPurchaseId, onPurchaseSuccess: true);
            }
            else
            {
                Debug.Log("ℹ️ Необработанных покупок не найдено или товар уже консумирован");
            }
        }

        // 🔥 ШАГ 3: Загружаем статус VIP из сохранений
        vipUnlocked = YG2.saves.vipUnlocked;

        if (vipUnlocked)
        {
            if (vipPanel != null) vipPanel.SetActive(false);
            HideTriggerObjects();
            Debug.Log("✅ VIP уже разблокирован, панель скрыта");
        }
        
        // 🔥 ШАГ 4: Обновляем отображение цены
        UpdatePriceDisplay();
        
        Debug.Log($"✅ VIPManager: инициализация завершена, VIP = {vipUnlocked}");
    }

    // 🔥 МЕТОД: Обновление отображения цены (полностью как в PetSystem)
    private void UpdatePriceDisplay()
    {
        if (priceDisplayText == null) return;
        
        // Получаем информацию о товаре из каталога
        _vipPurchaseInfo = YG2.PurchaseByID(vipPurchaseId);
        
        // Проверяем, что каталог загрузился и у товара есть код валюты
        if (_vipPurchaseInfo != null && !string.IsNullOrEmpty(_vipPurchaseInfo.priceCurrencyCode))
        {
            // Отображаем код валюты: RUB, USD, YAN и т.д. (без символов, чтобы не было квадратиков)
            string currencyCode = _vipPurchaseInfo.priceCurrencyCode.ToUpper();
            priceDisplayText.text = $"{_vipPurchaseInfo.priceValue} {currencyCode}";
            priceDisplayText.gameObject.SetActive(true);
            
            // Кнопка активна ТОЛЬКО если:
            // 1. VIP ещё не разблокирован
            // 2. Товар ещё не консумирован
            if (purchaseButton != null)
            {
                bool isUnlocked = vipUnlocked;
                bool isConsumed = _vipPurchaseInfo.consumed;
                
                purchaseButton.interactable = !isUnlocked && !isConsumed;
                
                Debug.Log($"💡 VIPManager: Цена загружена. Interactable={!isUnlocked && !isConsumed} (VIP:{isUnlocked}, Consumed:{isConsumed})");
            }
        }
        else
        {
            // Пока каталог не загрузился — показываем заглушку и блокируем кнопку
            priceDisplayText.text = loadingPriceText;
            if (purchaseButton != null) 
            {
                purchaseButton.interactable = false;
                Debug.Log("⏳ VIPManager: Каталог покупок ещё не загружен. Кнопка заблокирована.");
            }
        }
    }

    private void Update()
    {
        if (vipUnlocked || player == null || vipPanel == null) return;

        bool insideAnyZone = false;
        foreach (var zone in triggerZones)
        {
            if (zone != null && zone.bounds.Contains(player.position))
            {
                insideAnyZone = true;
                break;
            }
        }

        if (insideAnyZone && !vipPanel.activeSelf)
            vipPanel.SetActive(true);
        else if (!insideAnyZone && vipPanel.activeSelf)
            vipPanel.SetActive(false);
    }

    private void BuyVIP()
    {
        if (vipUnlocked)
        {
            Debug.Log("VIP уже разблокирован");
            return;
        }
        
        // Если товар уже консумирован, но статус VIP не обновился — форсируем обновление
        if (_vipPurchaseInfo != null && _vipPurchaseInfo.consumed)
        {
            Debug.LogWarning("Товар уже был приобретён, но статус не обновлён. Обновляем...");
            OnPurchaseSuccess(vipPurchaseId);
            return;
        }
        
        if (!YG2.isSDKEnabled)
        {
            Debug.LogWarning("SDK ещё не инициализирован, покупка невозможна");
            return;
        }
        
        Debug.Log($"🛒 Покупка VIP: {vipPurchaseId}");
        YG2.BuyPayments(vipPurchaseId);
    }

    // 🔥 ОБРАБОТКА УСПЕШНОЙ ПОКУПКИ
    private void OnPurchaseSuccess(string purchaseId)
    {
        if (purchaseId != vipPurchaseId) return;
        
        Debug.Log("✅ Покупка подтверждена, выдаём вознаграждение...");
        
        vipUnlocked = true;
        if (vipPanel != null) vipPanel.SetActive(false);
        HideTriggerObjects();

        // 🔥 Приоритет: PlayerStatsManager для мгновенного сохранения
        if (PlayerStatsManager.Instance != null)
        {
            PlayerStatsManager.Instance.SetVIPUnlocked(true);
            PlayerStatsManager.Instance.ForceSave();
        }
        else
        {
            YG2.saves.vipUnlocked = true;
            YG2.SaveProgress();
        }

        // Активируем все VIP-горки
        var vipLadders = FindObjectsOfType<LadderZone>();
        foreach (var ladder in vipLadders)
            if (ladder.IsVIP) ladder.SetVIPAccess(true);
            
        // 🔥 Обновляем UI цены (кнопка станет неактивной)
        UpdatePriceDisplay();
        
        Debug.Log("🎉 VIP активирован!");
    }

    private void HideTriggerObjects()
    {
        foreach (var obj in triggerObjectsToHide)
        {
            if (obj != null) obj.SetActive(false);
        }
    }
}
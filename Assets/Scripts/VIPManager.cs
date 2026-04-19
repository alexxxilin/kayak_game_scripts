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
        
        if (purchaseButton != null)
            purchaseButton.onClick.AddListener(BuyVIP);
            
        // 🔥 Подписка на событие получения каталога покупок для обновления цены
        YG2.onGetPayments += UpdatePriceDisplay;
    }

    private void OnDestroy()
    {
        YG2.onGetPayments -= UpdatePriceDisplay;
    }

    private System.Collections.IEnumerator InitializeVIP()
    {
        // 🔥 ШАГ 1: Ждём инициализации SDK
        while (!YG2InitializationManager.CanAccessSaves())
        {
            yield return null;
        }

        // 🔥 ШАГ 2: Консумирование необработанных покупок (ТРЕБОВАНИЕ ЯНДЕКС ИГР)
        if (autoConsumeOnStart && YG2.isSDKEnabled)
        {
            Debug.Log("🔄 VIPManager: проверка необработанных покупок...");
            
            // Проверяем, есть ли необработанная покупка именно для этого товара
            var pendingPurchase = YG2.PurchaseByID(vipPurchaseId);
            if (pendingPurchase != null && !pendingPurchase.consumed)
            {
                Debug.Log("✅ Найдена необработанная покупка, консумируем...");
                // Консумируем с автоматическим вызовом onPurchaseSuccess
                YG2.ConsumePurchaseByID(vipPurchaseId, onPurchaseSuccess: true);
            }
            else
            {
                // Если нет конкретной покупки — можно консумировать все (опционально)
                // YG2.ConsumePurchases(onPurchaseSuccess: true);
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
        
        // 🔥 ШАГ 4: Обновляем отображение цены (если каталог уже загружен)
        UpdatePriceDisplay();
        
        Debug.Log($"✅ VIPManager: инициализация завершена, VIP = {vipUnlocked}");
    }

    // 🔥 МЕТОД: Обновление отображения цены с автоматическим определением валюты
    private void UpdatePriceDisplay()
    {
        if (priceDisplayText == null) return;
        
        // Получаем информацию о товаре
        _vipPurchaseInfo = YG2.PurchaseByID(vipPurchaseId);
        
        if (_vipPurchaseInfo != null && !string.IsNullOrEmpty(_vipPurchaseInfo.priceCurrencyCode))
        {
            // Формируем строку цены: "99 ₽" или "1.99 $"
            string currencySymbol = GetCurrencySymbol(_vipPurchaseInfo.priceCurrencyCode);
            priceDisplayText.text = $"{_vipPurchaseInfo.priceValue} {currencySymbol}";
            priceDisplayText.gameObject.SetActive(true);
            
            // Блокируем кнопку, если товар уже консумирован (куплен)
            if (purchaseButton != null)
            {
                purchaseButton.interactable = !_vipPurchaseInfo.consumed && !vipUnlocked;
            }
        }
        else
        {
            // Пока цена не загрузилась — показываем заглушку
            priceDisplayText.text = loadingPriceText;
            if (purchaseButton != null) purchaseButton.interactable = false;
        }
    }

    // 🔥 ВСПОМОГАТЕЛЬНЫЙ МЕТОД: Преобразование кода валюты в символ
    private string GetCurrencySymbol(string currencyCode)
    {
        if (string.IsNullOrEmpty(currencyCode)) return "";
        
        return currencyCode.ToUpper() switch
        {
            "RUB" => "₽",
            "USD" => "$",
            "EUR" => "€",
            "KZT" => "₸",
            "BYN" => "Br",
            "UAH" => "₴",
            _ => currencyCode.ToUpper() // Если валюта неизвестна — показываем код
        };
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
        
        // 🔥 Проверка: если товар уже консумирован — не показываем окно покупки
        if (_vipPurchaseInfo != null && _vipPurchaseInfo.consumed)
        {
            Debug.LogWarning("Товар уже был приобретён, но статус не обновлён. Обновляем...");
            OnPurchaseSuccess(vipPurchaseId);
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

        // Сохраняем статус в облако и локально
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.SetVIPUnlocked(true);
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

    // 🔥 Подписка на события покупок
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
}
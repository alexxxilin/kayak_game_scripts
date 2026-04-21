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
    
    [Header("UI")]
    [SerializeField] private GameObject vipPanel;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private TextMeshProUGUI priceDisplayText;
    [SerializeField] private string loadingPriceText = "Загрузка...";

    [Header("Триггер-зоны")]
    [SerializeField] private List<Collider> triggerZones;
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
        {
            purchaseButton.onClick.RemoveAllListeners();
            purchaseButton.onClick.AddListener(BuyVIP);
            purchaseButton.interactable = true;
        }
    }

    private System.Collections.IEnumerator InitializeVIP()
    {
        while (!YG2InitializationManager.CanAccessSaves())
        {
            yield return null;
        }

        LoadVIPStatus();
        UpdatePriceDisplay();
        
        Debug.Log($"✅ VIPManager: инициализация завершена, VIP = {vipUnlocked}");
    }

    private void LoadVIPStatus()
    {
        if (PlayerStatsManager.Instance != null)
        {
            vipUnlocked = PlayerStatsManager.Instance.GetVIPUnlocked();
        }
        else
        {
            vipUnlocked = YG2.saves?.vipUnlocked ?? false;
        }

        if (vipUnlocked)
        {
            if (vipPanel != null) vipPanel.SetActive(false);
            HideTriggerObjects();
            Debug.Log("✅ VIP уже разблокирован, панель скрыта");
        }
    }

    private void UpdatePriceDisplay()
    {
        if (priceDisplayText == null) return;
        
        _vipPurchaseInfo = YG2.PurchaseByID(vipPurchaseId);
        
        if (_vipPurchaseInfo != null && !string.IsNullOrEmpty(_vipPurchaseInfo.priceCurrencyCode))
        {
            string currencyCode = _vipPurchaseInfo.priceCurrencyCode.ToUpper();
            priceDisplayText.text = $"{_vipPurchaseInfo.priceValue} {currencyCode}";
            priceDisplayText.gameObject.SetActive(true);
            
            if (purchaseButton != null)
            {
                purchaseButton.interactable = !vipUnlocked;
            }
        }
        else
        {
            priceDisplayText.text = loadingPriceText;
            if (purchaseButton != null) 
            {
                purchaseButton.interactable = false;
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
        
        if (!YG2.isSDKEnabled)
        {
            Debug.LogWarning("SDK ещё не инициализирован, покупка невозможна");
            return;
        }
        
        Debug.Log($"🛒 Покупка VIP: {vipPurchaseId}");
        YG2.BuyPayments(vipPurchaseId);
    }

    public void GrantVIP()
    {
        if (vipUnlocked) return;
    
        Debug.Log("✅ VIPManager: выдаём вознаграждение за покупку");
    
        vipUnlocked = true;
        if (vipPanel != null) vipPanel.SetActive(false);
        HideTriggerObjects();

        // 🔥 Сохраняем статус через PlayerStats + YG2.saves
        if (PlayerStatsManager.Instance != null)
        {
            PlayerStatsManager.Instance.SetVIPUnlocked(true);
        }
    
        if (YG2.saves != null)
        {
            YG2.saves.vipUnlocked = true;
        }
    
        // 🔥 Мгновенное сохранение в облако
        YG2.SaveProgress();

        // Обновляем доступ к VIP-лестницам
        var vipLadders = FindObjectsOfType<LadderZone>();
        foreach (var ladder in vipLadders)
            if (ladder.IsVIP) ladder.SetVIPAccess(true);
        
        UpdatePriceDisplay();
    
        Debug.Log("🎉 VIP активирован!");
    }

    public void OnVIPStatusReset()
    {
        Debug.Log("🔄 VIPManager: получен сигнал о сбросе статуса VIP");
        
        vipUnlocked = false;
        
        if (player != null && triggerZones != null)
        {
            bool insideAnyZone = false;
            foreach (var zone in triggerZones)
            {
                if (zone != null && zone.bounds.Contains(player.position))
                {
                    insideAnyZone = true;
                    break;
                }
            }
            
            if (insideAnyZone && vipPanel != null)
            {
                vipPanel.SetActive(true);
            }
        }
        
        ShowTriggerObjects();
        UpdatePriceDisplay();
        
        Debug.Log("✅ VIPManager: статус сброшен, интерфейс обновлён");
    }

    private void ShowTriggerObjects()
    {
        foreach (var obj in triggerObjectsToHide)
        {
            if (obj != null) obj.SetActive(true);
        }
    }

    private void HideTriggerObjects()
    {
        foreach (var obj in triggerObjectsToHide)
        {
            if (obj != null) obj.SetActive(false);
        }
    }

    public bool IsVIPUnlocked() => vipUnlocked;
    public string GetPurchaseId() => vipPurchaseId;
}
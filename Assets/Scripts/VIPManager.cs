using UnityEngine;
using UnityEngine.UI;
using YG;
using System.Collections.Generic;

public class VIPManager : MonoBehaviour
{
    [Header("Настройки покупки")]
    [SerializeField] private string vipPurchaseId = "vip_access";

    [Header("UI")]
    [SerializeField] private GameObject vipPanel;         // панель, которая появляется
    [SerializeField] private Button purchaseButton;       // кнопка покупки на панели

    [Header("Триггер-зоны")]
    [Tooltip("Коллайдеры, при входе в которые показывается панель")]
    [SerializeField] private List<Collider> triggerZones;
    [Tooltip("Объекты (обычно сами коллайдеры или их родители), которые нужно скрыть после покупки VIP")]
    [SerializeField] private List<GameObject> triggerObjectsToHide; // добавляем это поле

    private bool vipUnlocked = false;
    private Transform player;

    private void Start()
    {
        // Находим игрока по тегу (предполагается, что у игрока тег "Player")
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        // Ждем инициализации SDK перед доступом к YG2.saves
        StartCoroutine(InitializeVIP());
        
        // Подписываем кнопку на покупку
        if (purchaseButton != null)
            purchaseButton.onClick.AddListener(BuyVIP);
    }

    private System.Collections.IEnumerator InitializeVIP()
    {
        // Ждем пока SDK не будет инициализирован
        while (!YG2InitializationManager.CanAccessSaves())
        {
            yield return null;
        }

        // Получаем статус VIP из сохранений
        vipUnlocked = YG2.saves.vipUnlocked;

        // Если VIP уже куплен, панель не показываем и скрываем триггеры
        if (vipUnlocked)
        {
            if (vipPanel != null) vipPanel.SetActive(false);
            HideTriggerObjects();
        }
        
        Debug.Log($"✅ VIPManager: инициализация завершена, VIP разблокирован = {vipUnlocked}");
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

        // Показываем или скрываем панель в зависимости от нахождения в зоне
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
        YG2.BuyPayments(vipPurchaseId);
    }

    private void OnPurchaseSuccess(string purchaseId)
    {
        if (purchaseId == vipPurchaseId)
        {
            vipUnlocked = true;
            if (vipPanel != null) vipPanel.SetActive(false);

            // Скрываем объекты триггеров
            HideTriggerObjects();

            // Сохраняем VIP в Player Stats и облако
            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.SetVIPUnlocked(true);
            else
            {
                YG2.saves.vipUnlocked = true;
                YG2.SaveProgress();
            }

            // Активируем все VIP-горки на сцене
            var vipLadders = FindObjectsOfType<LadderZone>();
            foreach (var ladder in vipLadders)
                if (ladder.IsVIP) ladder.SetVIPAccess(true);
        }
    }

    private void HideTriggerObjects()
    {
        foreach (var obj in triggerObjectsToHide)
        {
            if (obj != null) obj.SetActive(false);
        }
    }

    private void OnEnable() => YG2.onPurchaseSuccess += OnPurchaseSuccess;
    private void OnDisable() => YG2.onPurchaseSuccess -= OnPurchaseSuccess;
}
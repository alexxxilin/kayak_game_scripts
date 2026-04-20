using UnityEngine;
using YG;
using System.Linq;

/// <summary>
/// Централизованный роутер покупок.
/// Обрабатывает ВСЕ внутриигровые покупки: реклама, спины, монеты, питомцы, VIP.
/// Гарантирует однократную обработку даже после перезагрузки.
/// </summary>
public class PurchaseRouter : MonoBehaviour
{
    private void Start()
    {
        // 🔥 Сначала подписываемся — чтобы не пропустить асинхронный вызов от ConsumePurchases
        YG2.onPurchaseSuccess += OnPurchaseSuccess;

        // 🔥 Затем консумируем все необработанные покупки (вызовет onPurchaseSuccess один раз для каждой)
        // onPurchaseSuccess: true — для необработанных покупок вызовет событие, чтобы мы выдали награду
        YG2.ConsumePurchases(onPurchaseSuccess: true);
    }

    private void OnDestroy()
    {
        YG2.onPurchaseSuccess -= OnPurchaseSuccess;
    }

    private void OnPurchaseSuccess(string productId)
    {
        Debug.Log($"🔄 PurchaseRouter: обработка покупки {productId}");

        // 1. Отключение рекламы
        if (productId == "disable_ads")
        {
            YG2.saves.adsDisabled = true;
            YG2.SaveProgress();

            var autoAdScript = FindObjectOfType<AutoInterstitialAd>();
            if (autoAdScript != null && autoAdScript.disableAdsButton != null)
                autoAdScript.disableAdsButton.gameObject.SetActive(false);

            return;
        }

        // 2. Спины колеса фортуны
        if (productId == "wheel_spin_10")
        {
            var fortuneWheel = FindFirstObjectByType<FortuneWheel>();
            if (fortuneWheel != null)
                fortuneWheel.AddSpins(10);

            return;
        }

        // 3. 🔥 VIP-доступ
        var vipManager = FindFirstObjectByType<VIPManager>();
        if (vipManager != null && productId == vipManager.GetPurchaseId())
        {
            Debug.Log($"👑 Обработка покупки VIP: {productId}");
            vipManager.GrantVIP();
            return;
        }

        // 4. Серебряные монеты
        var silverShop = FindFirstObjectByType<SilverCoinsShopManager>();
        if (silverShop != null && silverShop.silverCoinProductIds.Contains(productId))
        {
            // Награда уже выдана в SilverCoinsShopManager через событие
            Debug.Log($"🪙 Обработана покупка серебряных монет: {productId}");
            return;
        }

        // 5. Обычные монеты / питомцы из CoinShopManager
        var coinShopManager = FindFirstObjectByType<CoinShopManager>();
        if (coinShopManager != null && coinShopManager.IsCoinOffer(productId))
        {
            coinShopManager.OnPurchaseSuccess(productId);
            return;
        }

        // 6. Донатные питомцы из PetSystem
        var petSystem = FindFirstObjectByType<PetSystem>();
        if (petSystem != null)
        {
            // Проверяем, что это донатный питомец из списка
            var donateIdsField = petSystem.GetType().GetField("donatePetProductIds", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (donateIdsField != null)
            {
                var donateIds = donateIdsField.GetValue(petSystem) as System.Collections.Generic.List<string>;
                if (donateIds != null && donateIds.Contains(productId))
                {
                    petSystem.OnPurchaseSuccess(productId);
                    return;
                }
            }
        }

        Debug.LogWarning($"⚠️ PurchaseRouter: неизвестный товар {productId}");
    }
}
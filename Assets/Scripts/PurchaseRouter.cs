using UnityEngine;
using YG;

/// <summary>
/// Централизованный роутер покупок.
/// Обрабатывает все внутриигровые покупки: отключение рекламы, спины колеса фортуны,
/// монеты и питомцы из CoinShopManager, донатные питомцы из PetSystem.
/// Гарантирует однократную обработку даже после перезагрузки.
/// </summary>
public class PurchaseRouter : MonoBehaviour
{
    private void Start()
    {
        // 🔥 Сначала подписываемся — чтобы не пропустить асинхронный вызов от ConsumePurchases
        YG2.onPurchaseSuccess += OnPurchaseSuccess;

        // 🔥 Затем консумируем все необработанные покупки (вызовет onPurchaseSuccess один раз для каждой)
        YG2.ConsumePurchases(onPurchaseSuccess: true);
    }

    private void OnDestroy()
    {
        YG2.onPurchaseSuccess -= OnPurchaseSuccess;
    }

    private void OnPurchaseSuccess(string productId)
    {
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

        // 3. Покупка монет (и, возможно, питомца)
        var coinShopManager = FindFirstObjectByType<CoinShopManager>();
        if (coinShopManager != null && coinShopManager.IsCoinOffer(productId))
        {
            coinShopManager.OnPurchaseSuccess(productId);
            return;
        }

        // 4. Донатные питомцы
        var petSystem = FindFirstObjectByType<PetSystem>();
        if (petSystem != null)
            petSystem.OnPurchaseSuccess(productId);
    }
}
using UnityEngine;
using System.Collections.Generic;
using YG;

/// <summary>
/// Хелпер для отправки событий в Яндекс.Метрику
/// Все события отправляются только если SDK инициализирован и включена метрика
/// </summary>
public static class GameMetrics
{
    private const string EVENT_PET_PURCHASED = "pet_purchased";
    private const string EVENT_SILVER_SPENT = "silver_spent";
    private const string EVENT_DONATE_EGG_OPENED = "donate_egg_opened";
    private const string EVENT_COIN_PACK_PURCHASED = "coin_pack_purchased";

    /// <summary>
    /// Отправка события о покупке питомца
    /// </summary>
    public static void SendPetPurchased(int shopIndex, int petIndex, string petName, 
        string currencyType, int cost, bool isDonatePet, float multiplier, string purchaseMethod = "direct")
    {
        if (!YG2.isSDKEnabled) return;
        
        var eventData = new Dictionary<string, object>
        {
            { "shop_index", shopIndex },
            { "pet_index", petIndex },
            { "pet_name", petName ?? "unknown" },
            { "currency", currencyType }, // "rubles", "silver_coins", "regular_coins"
            { "cost", cost },
            { "is_donate", isDonatePet },
            { "multiplier", multiplier },
            { "purchase_method", purchaseMethod }, // "direct", "egg", "pack"
            { "player_id", YG2.player.auth ? YG2.player.id : "anonymous" },
            { "timestamp", System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        YG2.MetricaSend(EVENT_PET_PURCHASED, eventData);
        Debug.Log($"📊 Метрика: {EVENT_PET_PURCHASED} - {petName} за {cost} {currencyType}");
    }

    /// <summary>
    /// Отправка события о трате серебряных монет
    /// </summary>
    public static void SendSilverSpent(int amount, string reason, int shopIndex = -1, int petIndex = -1)
    {
        if (!YG2.isSDKEnabled) return;
        
        var eventData = new Dictionary<string, object>
        {
            { "amount", amount },
            { "reason", reason }, // "pet_purchase", "donate_egg", "coin_pack", "regular_pet"
            { "player_id", YG2.player.auth ? YG2.player.id : "anonymous" }
        };

        if (shopIndex >= 0) eventData.Add("shop_index", shopIndex);
        if (petIndex >= 0) eventData.Add("pet_index", petIndex);

        YG2.MetricaSend(EVENT_SILVER_SPENT, eventData);
        Debug.Log($"📊 Метрика: {EVENT_SILVER_SPENT} - {amount} серебра на {reason}");
    }

    /// <summary>
    /// Отправка события об открытии донатного яйца (выпал питомец)
    /// </summary>
    public static void SendDonateEggOpened(int shopIndex, int petIndex, string petName, 
        float multiplier, int silverCost)
    {
        if (!YG2.isSDKEnabled) return;
        
        var eventData = new Dictionary<string, object>
        {
            { "shop_index", shopIndex },
            { "pet_index", petIndex },
            { "pet_name", petName ?? "unknown" },
            { "multiplier", multiplier },
            { "silver_cost", silverCost },
            { "player_id", YG2.player.auth ? YG2.player.id : "anonymous" },
            { "egg_type", "silver_coin_purchase" },
            { "timestamp", System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        YG2.MetricaSend(EVENT_DONATE_EGG_OPENED, eventData);
        Debug.Log($"📊 Метрика: {EVENT_DONATE_EGG_OPENED} - выпал {petName} (x{multiplier})");
    }

    /// <summary>
    /// Отправка события о покупке пакета монет
    /// </summary>
    public static void SendCoinPackPurchased(string packId, int coinsReceived, 
        string currencyType, int cost, bool includesPet = false, int petIndex = -1)
    {
        if (!YG2.isSDKEnabled) return;
        
        var eventData = new Dictionary<string, object>
        {
            { "pack_id", packId },
            { "coins_received", coinsReceived },
            { "currency_type", currencyType },
            { "cost", cost },
            { "includes_pet", includesPet },
            { "player_id", YG2.player.auth ? YG2.player.id : "anonymous" }
        };

        if (includesPet && petIndex >= 0)
        {
            eventData.Add("bonus_pet_index", petIndex);
        }

        YG2.MetricaSend(EVENT_COIN_PACK_PURCHASED, eventData);
        Debug.Log($"📊 Метрика: {EVENT_COIN_PACK_PURCHASED} - пакет {packId} за {cost} {currencyType}");
    }

    /// <summary>
    /// Утилитарный метод для безопасного получения имени питомца
    /// </summary>
    public static string GetSafePetName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "unknown";
        return name.Replace(" ", "_").Replace(",", "").Replace(";", "");
    }
}
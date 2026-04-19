using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YG;
using YG.Utils.Pay;
using System.Collections;
using System.Collections.Generic;
using KinematicCharacterController.Examples;

/// <summary>
/// Управление покупками монет и питомцев в донатном магазине.
/// Поддерживает предложения: монеты, монеты + донатный питомец.
/// Поддерживает покупку за рубли ИЛИ за серебряные монеты.
/// 
/// 🔥 Player Stats является ИСТОЧНИКОМ ПРАВДЫ для донатных покупок.
/// 🔥 Интегрирована отправка метрик в Яндекс.Метрику
/// </summary>
public class CoinShopManager : MonoBehaviour
{
    [System.Serializable]
    public class CoinOffer
    {
        [Tooltip("ID товара в Яндексе (для покупки за рубли)")]
        public string productId;

        [Tooltip("Количество обычных монет")]
        public long coinsAmount;

        [Tooltip("Индекс магазина из PetSystem (если givePet = true)")]
        public int shopIndex = 0;

        [Tooltip("Индекс питомца в магазине (если givePet = true)")]
        public int petIndex = 0;

        [Tooltip("Выдавать ли питомца")]
        public bool givePet = false;

        [Tooltip("Картинка для панели получения (обязательно для всех предложений)")]
        public Sprite spriteForRecievedPanel;

        // === КНОПКИ И ЦЕНЫ ЗА РУБЛИ ===
        [Header("Покупка за рубли")]
        public Button buyButton;
        public TextMeshProUGUI priceText;

        // === КНОПКИ И ЦЕНЫ ЗА СЕРЕБРЯНЫЕ МОНЕТЫ ===
        [Header("Покупка за серебряные монеты")]
        public Button buyWithSilverButton;
        public int silverPrice = 0; // цена в серебряных монетах
        public TextMeshProUGUI silverPriceText;

        [Tooltip("Отображаемое имя (для отладки)")]
        public string displayName;
    }

    [Header("Предложения монет и питомцев")]
    public List<CoinOffer> coinOffers = new List<CoinOffer>();

    [Header("UI окно получения")]
    public GameObject purchaseSuccessPanel;
    public Image purchasedPetImage;
    public Button closePurchaseButton;

    [Header("Настройки надёжности")]
    [Tooltip("Количество повторных попыток сохранения в Cloud Saves")]
    public int saveRetryCount = 3;
    [Tooltip("Задержка между попытками сохранения (сек)")]
    public float saveRetryDelay = 0.5f;

    [Header("Интеграция с магазином серебряных монет")]
    [Tooltip("Ссылка на SilverCoinsShopManager")]
    public SilverCoinsShopManager silverCoinsShopManager;

    private ExampleCharacterController _playerController;
    private PetSystem _petSystem;
    private PlayerStatsManager _playerStatsManager;
    private SaveManager _saveManager;

    private void Awake()
    {
        YG2.onGetPayments += UpdateAllCoinOfferButtons;
    }

    private void Start()
    {
        _playerController = FindFirstObjectByType<ExampleCharacterController>();
        _petSystem = FindFirstObjectByType<PetSystem>();
        _playerStatsManager = FindFirstObjectByType<PlayerStatsManager>();
        _saveManager = FindFirstObjectByType<SaveManager>();

        // Создаем PlayerStatsManager если его нет
        if (_playerStatsManager == null)
        {
            GameObject statsGO = new GameObject("PlayerStatsManager");
            _playerStatsManager = statsGO.AddComponent<PlayerStatsManager>();
        }

        // Ждем инициализации SDK перед настройкой магазина
        StartCoroutine(InitializeCoinShop());
    }

    private System.Collections.IEnumerator InitializeCoinShop()
    {
        // Ждем пока SDK не будет инициализирован
        while (!YG2InitializationManager.CanAccessSaves())
        {
            yield return null;
        }

        for (int i = 0; i < coinOffers.Count; i++)
        {
            int index = i;

            // Кнопка покупки за рубли
            if (coinOffers[index].buyButton != null)
            {
                coinOffers[index].buyButton.onClick.RemoveAllListeners();
                coinOffers[index].buyButton.onClick.AddListener(() => BuyCoinOfferWithRubles(index));
            }

            // Кнопка покупки за серебряные монеты
            if (coinOffers[index].buyWithSilverButton != null)
            {
                coinOffers[index].buyWithSilverButton.onClick.RemoveAllListeners();
                coinOffers[index].buyWithSilverButton.onClick.AddListener(() => BuyCoinOfferWithSilver(index));
            }
        }

        if (closePurchaseButton != null)
        {
            closePurchaseButton.onClick.RemoveAllListeners();
            closePurchaseButton.onClick.AddListener(() => purchaseSuccessPanel.SetActive(false));
        }

        UpdateAllCoinOfferButtons();
        UpdateSilverPriceTexts();
        
        // 🔥 ВОССТАНАВЛИВАЕМ ДОНАТНЫХ ПИТОМЦЕВ ИЗ PLAYER STATS
        RestoreDonatePetsFromStats();
        
        // 🔥 Интеграция с SilverCoinsShopManager
        if (silverCoinsShopManager != null)
        {
            Debug.Log("✅ CoinShopManager: интегрирован с SilverCoinsShopManager");
        }
    }

    private void OnDestroy()
    {
        YG2.onGetPayments -= UpdateAllCoinOfferButtons;
    }

    // 🔥 СОХРАНЕНИЕ ПРИ СВОРАЧИВАНИИ ИГРЫ
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Debug.Log("⚠️ Игра свёрнута — форсируем сохранение доната");
            ForceSaveAllDonateData();
        }
    }

    // === ПОКУПКА ЗА РУБЛИ ===
    private void BuyCoinOfferWithRubles(int offerIndex)
    {
        if (offerIndex < 0 || offerIndex >= coinOffers.Count) return;
        string productId = coinOffers[offerIndex].productId;
        YG2.BuyPayments(productId);
    }

    // 🔥 ИЗМЕНЁННЫЙ МЕТОД: ПОКУПКА ЗА СЕРЕБРЯНЫЕ МОНЕТЫ С МЕТРИКАМИ
    private void BuyCoinOfferWithSilver(int offerIndex)
    {
        if (offerIndex < 0 || offerIndex >= coinOffers.Count) return;
        var offer = coinOffers[offerIndex];
        if (offer.silverPrice <= 0) return;
        if (_playerController == null) return;

        // Проверка баланса
        if (!_playerController.TrySpendSilverCoins(offer.silverPrice))
        {
            // 🔥 НОВОЕ: открываем магазин серебряных монет при нехватке
            if (silverCoinsShopManager != null)
            {
                silverCoinsShopManager.OpenSilverShopForInsufficientFunds();
            }
            return;
        }

        // 🔥 МЕТРИКА: потрачены серебряные монеты на пакет монет
        GameMetrics.SendSilverSpent(offer.silverPrice, "coin_pack_purchase");

        // 🔥 МГНОВЕННО СОХРАНЯЕМ СЕРЕБРЯНЫЕ МОНЕТЫ В PLAYER STATS
        if (_playerStatsManager != null)
        {
            _playerStatsManager.SetSilverCoins(_playerController.SilverCoins);
        }

        // 🔥 МЕТРИКА: если в пакете есть питомец — отслеживаем его получение
        if (offer.givePet && _petSystem != null)
        {
            var petMultiplier = _petSystem.petMultipliers.Find(m =>
                m.shopIndex == offer.shopIndex && 
                m.petLocalIndex == offer.petIndex);
            
            if (petMultiplier != null)
            {
                GameMetrics.SendPetPurchased(
                    offer.shopIndex,
                    offer.petIndex,
                    GameMetrics.GetSafePetName(petMultiplier.petName),
                    "silver_coins",
                    offer.silverPrice,
                    true,
                    petMultiplier.rocketMultiplier,
                    "pack"
                );
            }
        }
        
        // 🔥 МЕТРИКА: покупка пакета монет
        GameMetrics.SendCoinPackPurchased(
            offer.productId,
            (int)offer.coinsAmount,
            "silver_coins",
            offer.silverPrice,
            offer.givePet,
            offer.givePet ? offer.petIndex : -1
        );

        // Выдача награды
        ProcessOfferReward(offer);

        // 🔥 МГНОВЕННО СОХРАНЯЕМ ОБЫЧНЫЕ МОНЕТЫ В PLAYER STATS
        if (_playerStatsManager != null && offer.coinsAmount > 0)
        {
            _playerStatsManager.SetRegularCoins((long)_playerController.CoinsCollected);
        }

        // Обновление UI
        if (_petSystem != null)
        {
            _petSystem.UpdateSilverCoinsUI();
        }

        // 🔥 Сохранение в Cloud Saves с повторными попытками
        StartCoroutine(SaveWithRetry("silver_purchase"));
    }

    // === ОБЩАЯ ЛОГИКА ВЫДАЧИ НАГРАДЫ ===
    private void ProcessOfferReward(CoinOffer offer)
    {
        // 1. Выдача монет
        if (offer.coinsAmount > 0 && _playerController != null)
        {
            _playerController.AddCoins(offer.coinsAmount);
            Debug.Log($"Выдано {offer.coinsAmount} монет");
            
            // 🔥 МГНОВЕННО СОХРАНЯЕМ В PLAYER STATS
            if (_playerStatsManager != null)
            {
                _playerStatsManager.SetRegularCoins((long)_playerController.CoinsCollected);
            }
        }

        // 2. Выдача питомца (если нужно)
        if (offer.givePet && _petSystem != null)
        {
            if (offer.shopIndex < 0 || offer.shopIndex >= _petSystem.petShops.Count)
            {
                Debug.LogError($"Неверный shopIndex={offer.shopIndex}");
                return;
            }

            var shop = _petSystem.petShops[offer.shopIndex];
            if (!shop.isDonateShop)
            {
                Debug.LogError($"Магазин {offer.shopIndex} не является донатным");
                return;
            }

            if (offer.petIndex < 0 || offer.petIndex >= shop.pet3DPrefabs.Count)
            {
                Debug.LogError($"Неверный petIndex={offer.petIndex} в магазине {offer.shopIndex}");
                return;
            }

            var multiplierEntry = _petSystem.petMultipliers.Find(m =>
                m.shopIndex == offer.shopIndex &&
                m.petLocalIndex == offer.petIndex &&
                m.isDonatePet);

            if (multiplierEntry == null)
            {
                Debug.LogError($"Питомец [{offer.shopIndex}:{offer.petIndex}] не помечен как донатный");
                return;
            }

            var newPet = new PetSystem.PetInstance
            {
                id = _petSystem.GetNextPetId(),
                shopIndex = offer.shopIndex,
                petTypeIndex = offer.petIndex
            };
            newPet.SetDonateStatus(true);
            _petSystem.AddPetFromExternal(newPet);

            // 🔥 МГНОВЕННО ОТМЕЧАЕМ ДОНАТНОГО ПИТОМЦА В PLAYER STATS
            if (_playerStatsManager != null)
            {
                _playerStatsManager.SetDonatePetPurchased(offer.petIndex, offer.shopIndex);
            }
        }

        // 3. ПОКАЗ ПАНЕЛИ ПОЛУЧЕНИЯ
        if (purchaseSuccessPanel != null && offer.spriteForRecievedPanel != null && purchasedPetImage != null)
        {
            purchasedPetImage.sprite = offer.spriteForRecievedPanel;
            purchaseSuccessPanel.SetActive(true);
        }
    }

    // 🔥 ИЗМЕНЁННЫЙ МЕТОД: ОБРАБОТЧИК УСПЕХА ПОКУПКИ ЗА РУБЛИ С МЕТРИКАМИ
    public void OnPurchaseSuccess(string productId)
    {
        var offer = coinOffers.Find(o => o.productId == productId);
        if (offer == null) return;

        // 🔥 МЕТРИКА: покупка за рубли
        if (offer.givePet)
        {
            var petMultiplier = _petSystem?.petMultipliers.Find(m =>
                m.shopIndex == offer.shopIndex && 
                m.petLocalIndex == offer.petIndex);
            
            if (petMultiplier != null)
            {
                GameMetrics.SendPetPurchased(
                    offer.shopIndex,
                    offer.petIndex,
                    GameMetrics.GetSafePetName(petMultiplier.petName),
                    "rubles",
                    0,
                    true,
                    petMultiplier.rocketMultiplier,
                    "pack"
                );
            }
        }
        
        // 🔥 МЕТРИКА: покупка пакета за рубли
        GameMetrics.SendCoinPackPurchased(
            offer.productId,
            (int)offer.coinsAmount,
            "rubles",
            0,
            offer.givePet,
            offer.givePet ? offer.petIndex : -1
        );

        ProcessOfferReward(offer);

        // 🔥 МГНОВЕННОЕ СОХРАНЕНИЕ В PLAYER STATS (дубль для надёжности)
        if (_playerStatsManager != null && offer.coinsAmount > 0)
        {
            _playerStatsManager.SetRegularCoins((long)_playerController.CoinsCollected);
        }

        // 🔥 Сохранение в Cloud Saves с повторными попытками
        StartCoroutine(SaveWithRetry("donate_purchase_rubles"));
        
        _saveManager?.OnDonatePetPurchased();
    }

    // 🔥 НОВЫЙ МЕТОД: сохранение с повторными попытками для Cloud Saves
    private IEnumerator SaveWithRetry(string reason)
    {
        for (int attempt = 1; attempt <= saveRetryCount; attempt++)
        {
            Debug.Log($"💾 Cloud Saves: попытка #{attempt} ({reason})");
            
            if (_saveManager != null)
            {
                _saveManager.SaveImmediately(reason);
            }
            else
            {
                YG2.SaveProgress();
            }
            
            // Ждём немного, чтобы SDK успел отправить данные
            yield return new WaitForSecondsRealtime(saveRetryDelay);
        }
        
        Debug.Log($"✅ Cloud Saves: завершено {saveRetryCount} попыток ({reason})");
    }

    // 🔥 НОВЫЙ МЕТОД: форсированное сохранение всех донатных данных
    private void ForceSaveAllDonateData()
    {
        if (_playerController == null || _playerStatsManager == null) return;
        
        Debug.Log("💾 Форсированное сохранение донатных данных...");
        
        // Сохраняем монеты в Player Stats
        _playerStatsManager.SetRegularCoins((long)_playerController.CoinsCollected);
        _playerStatsManager.SetSilverCoins(_playerController.SilverCoins);
        
        // Обновляем Cloud Saves из Player Stats (если нужно)
        if (YG2.saves != null)
        {
            if (_playerStatsManager.GetRegularCoins() > YG2.saves.coinsCollected)
            {
                YG2.saves.coinsCollected = _playerStatsManager.GetRegularCoins();
            }
            if (_playerStatsManager.GetSilverCoins() > YG2.saves.silverCoins)
            {
                YG2.saves.silverCoins = _playerStatsManager.GetSilverCoins();
            }
        }
        
        // Сохраняем в Cloud Saves
        if (_saveManager != null)
        {
            _saveManager.SaveImmediately("force_save_pause");
        }
        else
        {
            YG2.SaveProgress();
        }
    }

    public bool IsCoinOffer(string productId)
    {
        return coinOffers.Exists(o => o.productId == productId);
    }

    private void UpdateAllCoinOfferButtons()
    {
        foreach (var offer in coinOffers)
        {
            if (offer.priceText != null)
            {
                var purchase = YG2.PurchaseByID(offer.productId);
                if (purchase != null && !string.IsNullOrEmpty(purchase.priceCurrencyCode))
                {
                    string currencyCode = purchase.priceCurrencyCode.ToUpper();
                    offer.priceText.text = $"{purchase.priceValue} {currencyCode}";
                    if (offer.buyButton != null) offer.buyButton.interactable = true;
                }
                else
                {
                    offer.priceText.text = "Загрузка...";
                    if (offer.buyButton != null) offer.buyButton.interactable = false;
                }
            }
        }
    }

    private void UpdateSilverPriceTexts()
    {
        foreach (var offer in coinOffers)
        {
            if (offer.silverPriceText != null)
            {
                if (offer.silverPrice > 0)
                {
                    offer.silverPriceText.text = $"{offer.silverPrice}";
                    if (offer.buyWithSilverButton != null)
                        offer.buyWithSilverButton.interactable = true;
                }
                else
                {
                    offer.silverPriceText.text = "—";
                    if (offer.buyWithSilverButton != null)
                        offer.buyWithSilverButton.interactable = false;
                }
            }
        }
    }

    // 🔥 МЕТОД ДЛЯ ВОССТАНОВЛЕНИЯ ДОНАТНЫХ ПИТОМЦЕВ ИЗ PLAYER STATS
    public void RestoreDonatePetsFromStats()
    {
        if (_petSystem == null || _playerStatsManager == null) return;

        var purchasedPets = _playerStatsManager.GetAllPurchasedDonatePets();
        Debug.Log($"🔍 Найдено {purchasedPets.Count} записей о донатных питомцах в Player Stats");

        foreach (string petKey in purchasedPets)
        {
            // Парсим ключ формата "DonatePet_shopIndex_petIndex"
            string[] parts = petKey.Replace("DonatePet_", "").Split('_');
            if (parts.Length == 2 && int.TryParse(parts[0], out int shopIndex) && int.TryParse(parts[1], out int petIndex))
            {
                // Проверяем, есть ли уже такой питомец
                bool alreadyOwned = false;
                foreach (var pet in _petSystem.ownedPets)
                {
                    if (pet.shopIndex == shopIndex && pet.petTypeIndex == petIndex && pet.IsDonatePet)
                    {
                        alreadyOwned = true;
                        break;
                    }
                }

                if (!alreadyOwned)
                {
                    Debug.Log($"🔄 Восстанавливаем донатного питомца [{shopIndex}:{petIndex}] из Player Stats");
                    
                    var newPet = new PetSystem.PetInstance
                    {
                        id = _petSystem.GetNextPetId(),
                        shopIndex = shopIndex,
                        petTypeIndex = petIndex
                    };
                    newPet.SetDonateStatus(true);
                    _petSystem.AddPetFromExternal(newPet);
                    
                    // 🔥 Также добавляем в Cloud Saves, если его там нет
                    if (YG2.saves != null)
                    {
                        bool existsInSaves = false;
                        foreach (var savedPet in YG2.saves.ownedPets)
                        {
                            if (savedPet.shopIndex == shopIndex && savedPet.petTypeIndex == petIndex && savedPet.isDonatePet)
                            {
                                existsInSaves = true;
                                break;
                            }
                        }
                        
                        if (!existsInSaves)
                        {
                            YG2.saves.ownedPets.Add(new PetSaveData
                            {
                                id = UnityEngine.Random.Range(10000, 99999),
                                shopIndex = shopIndex,
                                petTypeIndex = petIndex,
                                isDonatePet = true
                            });
                            Debug.Log($"✅ Питомец [{shopIndex}:{petIndex}] добавлен в Cloud Saves");
                        }
                    }
                }
            }
        }
        
        // Сохраняем обновления в Cloud Saves
        if (purchasedPets.Count > 0 && _saveManager != null)
        {
            _saveManager.SaveImmediately("restore_pets_from_stats");
        }
    }
}
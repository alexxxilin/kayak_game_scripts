using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using KinematicCharacterController.Examples;
using TMPro;
using YG;
using YG.Utils.Pay;
using System.Globalization;
using System.Linq;

[System.Serializable]
public class PetSaveData
{
    public int id;
    public int shopIndex;
    public int petTypeIndex;
    public bool isDonatePet;
}

public class PetSystem : MonoBehaviour
{
    [System.Serializable]
    public class PetShopSettings
    {
        public string shopName;
        public List<GameObject> pet3DPrefabs;
        public List<Sprite> petIcons;
        public List<float> dropChances = new List<float> { 50f, 25f, 15f, 9f, 1f };

        [Header("Цены")]
        [Tooltip("Цена за обычного питомца в обычных монетах")]
        public double petPrice = 100;

        [Tooltip("Цена за питомца в серебряных монетах (0 = покупка только за обычные монеты)")]
        public int silverCoinPrice = 0;

        [Tooltip("Разрешить покупку за серебряные монеты")]
        public bool allowSilverPurchase = false;

        public GameObject shopUI;
        public Button buyButton;

        [Tooltip("Текст цены на кнопке покупки")]
        public TextMeshProUGUI priceText;

        [Tooltip("Текст цены в серебряных монетах (опционально)")]
        public TextMeshProUGUI silverPriceText;

        [Tooltip("Добавьте все коллайдеры, которые должны активировать этот магазин")]
        public List<Collider> shopTriggers = new List<Collider>();
        public bool isDonateShop = false;
        public List<TextMeshProUGUI> petMultiplierTexts = new List<TextMeshProUGUI>();
    }

    [System.Serializable]
    public class PetMultiplier
    {
        public int shopIndex;
        public int petLocalIndex;
        public float rocketMultiplier = 1f;
        public float donateMultiplierPercent = 0f;
        public float donateBonusMultiplier = 0f;
        public string petName;
        public bool isDonatePet = false;
        public bool simpleDonatePet = false;
    }

    [Header("Настройки магазинов")]
    public List<PetShopSettings> petShops;

    [Header("Множители питомцев")]
    public List<PetMultiplier> petMultipliers;

    public TMP_Text multiplierText;
    public GameObject multiplierPanel;

    [Header("Общие UI элементы")]
    public Transform petsContainer;
    public GameObject petUIPrefab;
    public Text coinsText;
    public Image selectedPetImage;
    public Button equipButton;
    public Button unequipButton;
    public Button unequipAllButton;
    public List<Transform> petSlots;

    [Header("Управление инвентарем")]
    public Button openInventoryButton;
    public Button closeInventoryButton;
    public GameObject inventoryPanel;

    [Header("Окно покупки")]
    public GameObject purchaseSuccessPanel;
    public Image purchasedPetImage;
    public Button closePurchaseButton;

    [Header("Визуальные элементы")]
    public Sprite selectedFrameSprite;
    public Sprite defaultFrameSprite;

    [Header("Автоэкипировка")]
    public Toggle autoEquipToggle;
    public Button autoEquipOnceButton;

    [Header("Ограничение инвентаря")]
    public int maxPets = 75;
    public TextMeshProUGUI petCounterText;

    [Header("Сообщения")]
    public Text inventoryFullText;
    public Text cannotDeleteDonateText;
    public Text noPetsToDeleteText;
    public float messageDisplayTime = 3f;

    [Header("Удаление питомцев")]
    public Button deletePetButton;
    public Button deleteAllUnequippedButton;
    public GameObject deleteConfirmationPanel;
    public TextMeshProUGUI deleteConfirmationText;
    public Button confirmDeleteButton;
    public Button cancelDeleteButton;

    [Header("Донатный магазин — навигация")]
    public ScrollRect donateShopScrollRect;
    public Button scrollUpButton;
    public Button scrollDownButton;

    [Header("Донатный магазин")]
    public GameObject donateShopPanel;
    public Button openDonateShopButton;
    public Button closeDonateShopButton;
    public List<Button> donatePetBuyButtons;
    public List<TextMeshProUGUI> donatePetPriceTexts;
    public List<string> donatePetProductIds;

    [Header("Серебряные монеты")]
    public TextMeshProUGUI silverCoinsText;
    public Button watchAdForSilverCoinsButton;
    public int silverCoinsPerAd = 10;

    [Header("Покупка за серебряные монеты")]
    public List<Button> silverBuyButtons = new List<Button>();
    public List<int> silverPetPrices = new List<int>();

    [Header("Триггеры донатного магазина")]
    public List<Collider> donateShopTriggers = new List<Collider>();

    [Header("Дополнительная кнопка: открыть донат-магазин снизу")]
    public Button openDonateShopAtBottomButton;

    [Header("Интеграция с магазином серебряных монет")]
    [Tooltip("Ссылка на SilverCoinsShopManager")]
    public SilverCoinsShopManager silverCoinsShopManager;

    [Tooltip("Событие: вызывается когда не хватает серебряных монет для покупки")]
    public System.Action onNotEnoughSilverCoins;

    [System.Serializable]
    public class PetInstance
    {
        public int id;
        public int shopIndex;
        public int petTypeIndex;
        public GameObject petObject;
        public bool IsDonatePet { get; private set; }

        public void SetDonateStatus(bool isDonate)
        {
            IsDonatePet = isDonate;
        }

        public float GetMultiplier(List<PetMultiplier> multipliers, float bestRegularMultiplier)
        {
            var multiplier = multipliers.Find(m => m.shopIndex == shopIndex && m.petLocalIndex == petTypeIndex);
            if (multiplier == null) return 0f;

            if (multiplier.isDonatePet && multiplier.simpleDonatePet)
            {
                return multiplier.rocketMultiplier;
            }

            if (multiplier.isDonatePet)
            {
                float baseMultiplier = bestRegularMultiplier + (bestRegularMultiplier * multiplier.donateMultiplierPercent / 100f);
                return baseMultiplier + multiplier.donateBonusMultiplier;
            }
            else
            {
                return multiplier.rocketMultiplier;
            }
        }

        public string GetPetName(List<PetMultiplier> multipliers)
        {
            var multiplier = multipliers.Find(m => m.shopIndex == shopIndex && m.petLocalIndex == petTypeIndex);
            return multiplier != null ? multiplier.petName : "Unknown Pet";
        }

        public bool IsDonatePetMultiplier(List<PetMultiplier> multipliers)
        {
            var multiplier = multipliers.Find(m => m.shopIndex == shopIndex && m.petLocalIndex == petTypeIndex);
            return multiplier != null ? multiplier.isDonatePet : false;
        }
    }

    public List<PetInstance> ownedPets = new List<PetInstance>();
    private List<PetInstance> equippedPets = new List<PetInstance>();
    private int selectedPetIndex = -1;
    private int currentShopIndex = -1;
    private KinematicCharacterController.Examples.ExampleCharacterController playerController;
    private AudioSource audioSource;
    private int nextPetId = 0;
    private bool isInShopArea = false;
    private GameObject lastSelectedPetUI;
    private Coroutine messageCoroutine;
    public float PetMultiplierCounter { get; private set; } = 1f;
    private bool donateShopOpenedByTrigger = false;
    private PlayerStatsManager _playerStatsManager;
    private SaveManager _saveManager;

    private void Start()
    {
        playerController = FindFirstObjectByType<KinematicCharacterController.Examples.ExampleCharacterController>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        _playerStatsManager = FindFirstObjectByType<PlayerStatsManager>();
        _saveManager = FindFirstObjectByType<SaveManager>();

        StartCoroutine(InitializePetSystem());
    }

    private System.Collections.IEnumerator InitializePetSystem()
    {
        while (!YG2InitializationManager.CanAccessSaves())
        {
            yield return null;
        }

        // 🔥 Загружаем питомцев из YG2.saves (через SaveManager)
        RestoreAllPetsFromCloud();

        SetupButtonListeners();
        CloseAllPanels();
        InitializeShops();

        if (openDonateShopButton != null)
        {
            openDonateShopButton.onClick.RemoveAllListeners();
            openDonateShopButton.onClick.AddListener(ToggleDonateShop);
        }

        if (closeDonateShopButton != null)
        {
            closeDonateShopButton.onClick.AddListener(CloseDonateShop);
        }

        if (openDonateShopAtBottomButton != null)
        {
            openDonateShopAtBottomButton.onClick.RemoveAllListeners();
            openDonateShopAtBottomButton.onClick.AddListener(OpenDonateShopAtBottom);
        }

        foreach (var triggerCollider in donateShopTriggers)
        {
            if (triggerCollider != null)
            {
                var triggerComponent = triggerCollider.gameObject.GetComponent<DonateShopTrigger>();
                if (triggerComponent == null)
                {
                    triggerComponent = triggerCollider.gameObject.AddComponent<DonateShopTrigger>();
                }
                triggerComponent.Initialize(this);
            }
        }

        for (int i = 0; i < donatePetBuyButtons.Count && i < donatePetProductIds.Count; i++)
        {
            int capturedIndex = i;
            donatePetBuyButtons[capturedIndex].onClick.AddListener(() => BuyDonatePet(capturedIndex));
        }

        for (int i = 0; i < silverBuyButtons.Count && i < silverPetPrices.Count; i++)
        {
            int index = i;
            silverBuyButtons[index].onClick.AddListener(() => BuyDonatePetWithSilver(index));
        }

        if (watchAdForSilverCoinsButton != null)
        {
            watchAdForSilverCoinsButton.onClick.AddListener(() => YG2.RewardedAdvShow("silverCoins", () =>
            {
                var player = FindFirstObjectByType<KinematicCharacterController.Examples.ExampleCharacterController>();
                if (player != null)
                {
                    player.AddSilverCoins(silverCoinsPerAd);
                    UpdateSilverCoinsUI();

                    if (_playerStatsManager != null)
                    {
                        _playerStatsManager.SetSilverCoins(player.SilverCoins);
                    }
                }
            }));
        }

        if (scrollUpButton != null) scrollUpButton.onClick.AddListener(ScrollToTop);
        if (scrollDownButton != null) scrollDownButton.onClick.AddListener(ScrollToBottom);

        YG2.onGetPayments += UpdateAllDonatePetButtons;
        UpdateAllDonatePetButtons();

        CalculateCurrentMultiplier();
        UpdatePetCounter();
        UpdateDeleteButtonState();
        UpdateSilverCoinsUI();
        HideAllMessageTexts();

        if (silverCoinsShopManager != null)
        {
            onNotEnoughSilverCoins += silverCoinsShopManager.OpenSilverShopForInsufficientFunds;
        }
    }

    private void OnDestroy()
    {
        YG2.onGetPayments -= UpdateAllDonatePetButtons;

        if (silverCoinsShopManager != null)
        {
            onNotEnoughSilverCoins -= silverCoinsShopManager.OpenSilverShopForInsufficientFunds;
        }
    }

    private string GetLocalizedBuyText()
    {
        return YG2.lang switch
        {
            "en" => "",
            _ => ""
        };
    }

    public void OpenDonateShopAtBottom()
    {
        donateShopOpenedByTrigger = false;
        OpenDonateShop();
        ScrollToBottom();
    }

    private void UpdateAllDonatePetButtons()
    {
        for (int i = 0; i < donatePetBuyButtons.Count && i < donatePetProductIds.Count; i++)
        {
            var button = donatePetBuyButtons[i];
            TextMeshProUGUI text = null;
            if (i < donatePetPriceTexts.Count)
            {
                text = donatePetPriceTexts[i];
            }
            else
            {
                text = button.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (text != null)
            {
                var purchase = YG2.PurchaseByID(donatePetProductIds[i]);
                if (purchase != null && !string.IsNullOrEmpty(purchase.priceCurrencyCode))
                {
                    string buyText = GetLocalizedBuyText();
                    string currencyCode = purchase.priceCurrencyCode.ToUpper();
                    text.text = $"{buyText} {purchase.priceValue} {currencyCode}";
                    button.interactable = true;
                }
                else
                {
                    text.text = "Загрузка...";
                    button.interactable = false;
                }
            }
        }
    }

    public void ScrollToTop()
    {
        if (donateShopScrollRect != null)
        {
            donateShopScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void ScrollToBottom()
    {
        if (donateShopScrollRect != null)
        {
            donateShopScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    public void ToggleDonateShop()
    {
        donateShopOpenedByTrigger = false;
        if (donateShopPanel.activeSelf)
        {
            CloseDonateShop();
        }
        else
        {
            OpenDonateShop();
        }
    }

    public void OpenDonateShop()
    {
        donateShopPanel.SetActive(true);
        playerController?.OnUIOrAdOpened();
    }

    public void CloseDonateShop()
    {
        donateShopPanel.SetActive(false);
        donateShopOpenedByTrigger = false;
        playerController?.OnUIOrAdClosed();
    }

    public void OpenDonateShopFromTrigger()
    {
        OpenDonateShop();
    }

    public void TryCloseDonateShopFromTrigger()
    {
        if (donateShopPanel.activeSelf)
        {
            CloseDonateShop();
        }
    }

    private void BuyDonatePet(int petIndex)
    {
        if (petIndex < 0 || petIndex >= donatePetProductIds.Count) return;
        string productId = donatePetProductIds[petIndex];
        YG2.BuyPayments(productId);
    }

    private void BuyDonatePetWithSilver(int petIndex)
    {
        if (petIndex < 0 || petIndex >= silverPetPrices.Count) return;
        if (petIndex >= donatePetProductIds.Count) return;

        int price = silverPetPrices[petIndex];
        var player = FindFirstObjectByType<KinematicCharacterController.Examples.ExampleCharacterController>();
        if (player == null) return;

        if (!player.TrySpendSilverCoins(price))
        {
            onNotEnoughSilverCoins?.Invoke();

            if (silverCoinsShopManager != null)
            {
                silverCoinsShopManager.OpenSilverShopForInsufficientFunds();
            }

            return;
        }

        if (_playerStatsManager != null)
        {
            _playerStatsManager.SetSilverCoins(player.SilverCoins);
        }

        int donateShopIndex = -1;
        for (int i = 0; i < petShops.Count; i++)
        {
            if (petShops[i].isDonateShop)
            {
                donateShopIndex = i;
                break;
            }
        }

        if (donateShopIndex == -1)
        {
            Debug.LogError("No shop marked as isDonateShop = true!");
            player.AddSilverCoins(price);
            OnPetPurchased();
            return;
        }

        var multiplierEntry = petMultipliers.Find(m =>
            m.shopIndex == donateShopIndex &&
            m.petLocalIndex == petIndex &&
            m.isDonatePet);

        if (multiplierEntry == null)
        {
            Debug.LogError($"Donation multiplier not found for shop {donateShopIndex}, pet {petIndex}");
            player.AddSilverCoins(price);
            OnPetPurchased();
            return;
        }

        var newPet = new PetInstance
        {
            id = nextPetId++,
            shopIndex = donateShopIndex,
            petTypeIndex = petIndex
        };
        newPet.SetDonateStatus(true);

        ownedPets.Add(newPet);

        // 🔥 Сохраняем питомцев через YG2.saves (не через PlayerStats)
        SavePetsData();

        UpdatePetsListUI();
        UpdatePetCounter();
        CalculateCurrentMultiplier();
        UpdateSilverCoinsUI();

        var shop = petShops[donateShopIndex];
        if (shop != null && shop.petIcons != null && petIndex < shop.petIcons.Count && shop.petIcons[petIndex] != null && purchasedPetImage != null)
        {
            purchasedPetImage.sprite = shop.petIcons[petIndex];
        }

        purchaseSuccessPanel.SetActive(true);
        playerController?.OnUIOrAdOpened();

        if (autoEquipToggle != null && autoEquipToggle.isOn)
        {
            AutoEquipBestPets();
        }

        OnPetPurchased();
    }

    public void OnPurchaseSuccess(string productId)
    {
        int petIndex = donatePetProductIds.IndexOf(productId);
        if (petIndex == -1) return;

        int donateShopIndex = -1;
        for (int i = 0; i < petShops.Count; i++)
        {
            if (petShops[i].isDonateShop)
            {
                donateShopIndex = i;
                break;
            }
        }

        if (donateShopIndex == -1)
        {
            Debug.LogError("No shop marked as isDonateShop = true!");
            return;
        }

        var multiplierEntry = petMultipliers.Find(m =>
            m.shopIndex == donateShopIndex &&
            m.petLocalIndex == petIndex &&
            m.isDonatePet);

        if (multiplierEntry == null)
        {
            Debug.LogError($"Donation multiplier not found for shop {donateShopIndex}, pet {petIndex}");
            return;
        }

        var newPet = new PetInstance
        {
            id = nextPetId++,
            shopIndex = donateShopIndex,
            petTypeIndex = petIndex
        };
        newPet.SetDonateStatus(true);

        ownedPets.Add(newPet);

        // 🔥 Сохраняем питомцев через YG2.saves
        SavePetsData();

        UpdatePetsListUI();
        UpdatePetCounter();
        CalculateCurrentMultiplier();

        var shop = petShops[donateShopIndex];
        if (shop != null && shop.petIcons != null && petIndex < shop.petIcons.Count && shop.petIcons[petIndex] != null && purchasedPetImage != null)
        {
            purchasedPetImage.sprite = shop.petIcons[petIndex];
        }

        purchaseSuccessPanel.SetActive(true);
        playerController?.OnUIOrAdOpened();

        if (autoEquipToggle != null && autoEquipToggle.isOn)
        {
            AutoEquipBestPets();
        }

        OnPetPurchased();
    }

    public void OnGameLoad()
    {
        UpdateCoinsUI();
        UpdatePetsListUI();
    }

    public List<PetSaveData> GetOwnedPetsSaveData()
    {
        List<PetSaveData> saveData = new List<PetSaveData>();
        foreach (var pet in ownedPets)
        {
            saveData.Add(new PetSaveData
            {
                id = pet.id,
                shopIndex = pet.shopIndex,
                petTypeIndex = pet.petTypeIndex,
                isDonatePet = pet.IsDonatePet
            });
        }
        return saveData;
    }

    public List<int> GetEquippedPetIds()
    {
        List<int> ids = new List<int>();
        foreach (var pet in equippedPets)
        {
            if (pet != null)
                ids.Add(pet.id);
        }
        return ids;
    }

    public int GetNextPetId()
    {
        return nextPetId;
    }

    public bool GetAutoEquipStatus()
    {
        return autoEquipToggle != null && autoEquipToggle.isOn;
    }

    // 🔥 Загрузка питомцев из YG2.saves
    public void LoadPetsData()
    {
        if (YG2.saves == null)
        {
            Debug.LogWarning("⚠️ PetSystem: YG2.saves ещё не инициализирован!");
            return;
        }

        if (YG2.saves.ownedPets == null || YG2.saves.ownedPets.Count == 0)
        {
            Debug.Log("📭 No saved pet data found, starting with empty inventory");
            return;
        }

        ownedPets.Clear();
        equippedPets.Clear();
        nextPetId = YG2.saves.nextPetId > 0 ? YG2.saves.nextPetId : 0;

        Debug.Log($"📂 Начинаем загрузку {YG2.saves.ownedPets.Count} питомцев из сохранения...");

        foreach (var petData in YG2.saves.ownedPets)
        {
            var newPet = new PetInstance
            {
                id = petData.id,
                shopIndex = petData.shopIndex,
                petTypeIndex = petData.petTypeIndex
            };
            newPet.SetDonateStatus(petData.isDonatePet);
            ownedPets.Add(newPet);

            Debug.Log($"📥 Загружен питомец ID:{petData.id} Shop:{petData.shopIndex} Type:{petData.petTypeIndex} IsDonate:{petData.isDonatePet}");
        }

        if (YG2.saves.equippedPetIds != null)
        {
            foreach (int petId in YG2.saves.equippedPetIds)
            {
                var pet = ownedPets.Find(p => p.id == petId);
                if (pet != null && equippedPets.Count < petSlots.Count)
                {
                    equippedPets.Add(pet);
                    EquipPetVisual(pet, equippedPets.Count - 1);
                }
            }
        }

        if (autoEquipToggle != null)
        {
            autoEquipToggle.isOn = YG2.saves.autoEquipPets;
        }

        UpdatePetsListUI();
        CalculateCurrentMultiplier();
        UpdatePetCounter();

        Debug.Log($"✅ Loaded {ownedPets.Count} pets, {equippedPets.Count} equipped");
    }

    // 🔥 Восстановление питомцев из облака (YG2.saves)
    private void RestoreAllPetsFromCloud()
    {
        LoadPetsData();
        Debug.Log("✅ Все питомцы восстановлены из YG2.saves");
    }

    // 🔥 Сохранение питомцев в YG2.saves
    public void SavePetsData()
    {
        if (YG2.saves == null)
        {
            Debug.LogWarning("⚠️ PetSystem: YG2.saves ещё не инициализирован!");
            return;
        }

        YG2.saves.ownedPets = GetOwnedPetsSaveData();
        YG2.saves.equippedPetIds = GetEquippedPetIds();
        YG2.saves.nextPetId = nextPetId;
        YG2.saves.autoEquipPets = GetAutoEquipStatus();

        Debug.Log($"💾 Pet data saved: {ownedPets.Count} pets, {equippedPets.Count} equipped");

        // 🔥 Мгновенное сохранение в облако
        YG2.SaveProgress();
    }

    public void SetAutoEquipStatus(bool status)
    {
        if (autoEquipToggle != null)
        {
            autoEquipToggle.isOn = status;
        }
    }

    private void EquipPetVisual(PetInstance pet, int slotIndex)
    {
        if (pet.shopIndex < 0 || pet.shopIndex >= petShops.Count) return;
        var shop = petShops[pet.shopIndex];
        if (shop == null || petSlots == null || slotIndex >= petSlots.Count || petSlots[slotIndex] == null) return;

        if (pet.petTypeIndex < shop.pet3DPrefabs.Count)
        {
            pet.petObject = Instantiate(
                shop.pet3DPrefabs[pet.petTypeIndex],
                petSlots[slotIndex].position,
                petSlots[slotIndex].rotation,
                petSlots[slotIndex]
            );
            var canvas = pet.petObject.GetComponentInChildren<Canvas>();
            if (canvas != null) canvas.enabled = false;
        }
    }

    private float GetBestRegularMultiplier()
    {
        float bestMultiplier = 0f;
        foreach (var pet in ownedPets)
        {
            var multiplier = petMultipliers.Find(m => m.shopIndex == pet.shopIndex && m.petLocalIndex == pet.petTypeIndex);
            if (multiplier != null && !multiplier.isDonatePet)
            {
                if (multiplier.rocketMultiplier > bestMultiplier)
                {
                    bestMultiplier = multiplier.rocketMultiplier;
                }
            }
        }
        return bestMultiplier;
    }

    public float GetPetBonusMultiplier()
    {
        float bonus = 0f;
        float bestRegularMultiplier = GetBestRegularMultiplier();
        foreach (var pet in equippedPets)
        {
            if (pet != null)
            {
                bonus += pet.GetMultiplier(petMultipliers, bestRegularMultiplier);
            }
        }
        return bonus;
    }

    public float GetCurrentRocketMultiplier()
    {
        return PetMultiplierCounter;
    }

    private void CalculateCurrentMultiplier()
    {
        PetMultiplierCounter = 1f + GetPetBonusMultiplier();
        UpdateMultiplierDisplay();
    }

    private void UpdateMultiplierDisplay()
    {
        if (multiplierText != null)
        {
            multiplierText.text = FormatMultiplier(PetMultiplierCounter);
        }

        if (multiplierPanel == null) return;
        Text detailsText = multiplierPanel.GetComponentInChildren<Text>();
        if (detailsText == null) return;

        float bestRegularMultiplier = GetBestRegularMultiplier();
        detailsText.text = "Активные множители:\n";
        foreach (var pet in equippedPets)
        {
            if (pet != null)
            {
                float petMultiplier = pet.GetMultiplier(petMultipliers, bestRegularMultiplier);
                detailsText.text += $"{pet.GetPetName(petMultipliers)}: +{petMultiplier:F1}x\n";
            }
        }

        if (equippedPets.Count == 0)
        {
            detailsText.text += "Нет активных множителей\n";
        }

        detailsText.text += "\nСистема множителей:\n";
        detailsText.text += "- Базовый множитель: всегда 1x\n";
        detailsText.text += $"- Множитель питомцев: +{GetPetBonusMultiplier():F1}x\n";
        detailsText.text += $"- Итоговый множитель: {PetMultiplierCounter:F1}x\n";
        detailsText.text += $"- Лучший обычный множитель: {bestRegularMultiplier:F1}x";
    }

    private void UpdatePetMultipliersForShop(int shopIndex)
    {
        if (shopIndex < 0 || shopIndex >= petShops.Count || petShops[shopIndex] == null) return;
        var shop = petShops[shopIndex];
        if (shop == null || shop.petMultiplierTexts == null || shop.pet3DPrefabs == null) return;

        var multipliers = petMultipliers;
        float bestRegular = GetBestRegularMultiplier();

        for (int i = 0; i < shop.pet3DPrefabs.Count; i++)
        {
            if (i < shop.petMultiplierTexts.Count && shop.petMultiplierTexts[i] != null)
            {
                var multiplierEntry = multipliers.Find(m =>
                    m.shopIndex == shopIndex && m.petLocalIndex == i);

                if (multiplierEntry != null)
                {
                    if (multiplierEntry.isDonatePet && multiplierEntry.simpleDonatePet)
                    {
                        shop.petMultiplierTexts[i].text = "x" + FormatMultiplier(multiplierEntry.rocketMultiplier);
                    }
                    else if (multiplierEntry.isDonatePet)
                    {
                        float baseMult = bestRegular + (bestRegular * multiplierEntry.donateMultiplierPercent / 100f);
                        float finalMult = baseMult + multiplierEntry.donateBonusMultiplier;
                        shop.petMultiplierTexts[i].text = "x" + FormatMultiplier(finalMult);
                    }
                    else
                    {
                        shop.petMultiplierTexts[i].text = "x" + FormatMultiplier(multiplierEntry.rocketMultiplier);
                    }
                }
                else
                {
                    shop.petMultiplierTexts[i].text = "x0";
                }
            }
        }
    }

    private void InitializeShops()
    {
        for (int i = 0; i < petShops.Count; i++)
        {
            int shopIndex = i;
            var shop = petShops[shopIndex];
            if (shop == null) continue;

            if (shop.buyButton != null)
            {
                shop.buyButton.onClick.RemoveAllListeners();
                shop.buyButton.onClick.AddListener(() => BuyPet(shopIndex));
            }

            if (shop.shopUI != null)
            {
                shop.shopUI.SetActive(false);
            }

            UpdateShopPriceDisplay(shopIndex);

            foreach (var triggerCollider in shop.shopTriggers)
            {
                if (triggerCollider != null)
                {
                    var trigger = triggerCollider.gameObject.GetComponent<ShopTrigger>();
                    if (trigger == null)
                    {
                        trigger = triggerCollider.gameObject.AddComponent<ShopTrigger>();
                    }
                    trigger.Initialize(this, shopIndex);
                }
            }
        }

        if (openInventoryButton != null)
        {
            openInventoryButton.onClick.RemoveAllListeners();
            openInventoryButton.onClick.AddListener(OpenInventory);
        }

        if (closeInventoryButton != null)
        {
            closeInventoryButton.onClick.RemoveAllListeners();
            closeInventoryButton.onClick.AddListener(CloseInventory);
        }

        if (closePurchaseButton != null)
        {
            closePurchaseButton.onClick.RemoveAllListeners();
            closePurchaseButton.onClick.AddListener(() => purchaseSuccessPanel.SetActive(false));
        }

        if (deletePetButton != null)
        {
            deletePetButton.onClick.RemoveAllListeners();
            deletePetButton.onClick.AddListener(ShowDeleteConfirmationForSelectedPet);
            deletePetButton.gameObject.SetActive(false);
        }

        if (deleteAllUnequippedButton != null)
        {
            deleteAllUnequippedButton.onClick.RemoveAllListeners();
            deleteAllUnequippedButton.onClick.AddListener(ShowDeleteAllUnequippedConfirmation);
        }

        if (confirmDeleteButton != null)
        {
            confirmDeleteButton.onClick.RemoveAllListeners();
            confirmDeleteButton.onClick.AddListener(ConfirmDeletePet);
        }

        if (cancelDeleteButton != null)
        {
            cancelDeleteButton.onClick.RemoveAllListeners();
            cancelDeleteButton.onClick.AddListener(CancelDeletePet);
        }

        if (autoEquipOnceButton != null)
        {
            autoEquipOnceButton.onClick.RemoveAllListeners();
            autoEquipOnceButton.onClick.AddListener(AutoEquipBestPets);
        }

        if (autoEquipToggle != null)
        {
            autoEquipToggle.onValueChanged.RemoveAllListeners();
            autoEquipToggle.onValueChanged.AddListener(OnAutoEquipToggleChanged);
        }
    }

    private void UpdateShopPriceDisplay(int shopIndex)
    {
        if (shopIndex < 0 || shopIndex >= petShops.Count) return;
        var shop = petShops[shopIndex];
        if (shop == null) return;

        if (shop.priceText != null)
        {
            shop.priceText.text = FormatNumber(shop.petPrice);
        }

        if (shop.silverPriceText != null)
        {
            if (shop.allowSilverPurchase && shop.silverCoinPrice > 0)
            {
                shop.silverPriceText.text = $"{shop.silverCoinPrice} 💎";
                shop.silverPriceText.gameObject.SetActive(true);
            }
            else
            {
                shop.silverPriceText.gameObject.SetActive(false);
            }
        }
    }

    private void OpenInventory()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
            playerController?.OnUIOrAdOpened();
            UpdatePetsListUI();
            UpdateCoinsUI();
            UpdateDeleteButtonState();
        }
    }

    private void CloseInventory()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
            playerController?.OnUIOrAdClosed();
        }
    }

    public void OnShopTriggerEnter(int shopIndex)
    {
        if (shopIndex < 0 || shopIndex >= petShops.Count) return;
        currentShopIndex = shopIndex;
        isInShopArea = true;
        OpenShop(shopIndex);
    }

    public void OnShopTriggerExit(int shopIndex)
    {
        if (shopIndex == currentShopIndex)
        {
            isInShopArea = false;
            CloseAllPanels();
            playerController?.OnUIOrAdClosed();
        }
    }

    private void OpenShop(int shopIndex)
    {
        if (shopIndex < 0 || shopIndex >= petShops.Count) return;

        foreach (var shop in petShops)
        {
            if (shop != null && shop.shopUI != null) shop.shopUI.SetActive(false);
        }

        if (petShops[shopIndex] != null && petShops[shopIndex].shopUI != null)
        {
            petShops[shopIndex].shopUI.SetActive(true);
            playerController?.OnUIOrAdOpened();
            UpdatePetMultipliersForShop(shopIndex);
            UpdateShopPriceDisplay(shopIndex);
        }
    }

    public void BuyPet(int shopIndex)
    {
        if (!isInShopArea || shopIndex < 0 || shopIndex >= petShops.Count) return;

        var currentShop = petShops[shopIndex];
        if (currentShop == null) return;

        if (currentShop.allowSilverPurchase && currentShop.silverCoinPrice > 0)
        {
            var player = FindFirstObjectByType<KinematicCharacterController.Examples.ExampleCharacterController>();
            if (player == null) return;

            if (player.SilverCoins >= currentShop.silverCoinPrice)
            {
                BuyPetWithSilver(shopIndex, currentShop.silverCoinPrice);
            }
            else
            {
                Debug.Log($"Недостаточно серебряных монет! Нужно: {currentShop.silverCoinPrice}, Есть: {player.SilverCoins}");

                onNotEnoughSilverCoins?.Invoke();
                if (silverCoinsShopManager != null)
                {
                    silverCoinsShopManager.OpenSilverShopForInsufficientFunds();
                }
            }
            return;
        }

        if (playerController == null || playerController.CoinsCollected < currentShop.petPrice) return;

        playerController.SpendCoinsForPet(currentShop.petPrice);

        if (_playerStatsManager != null)
        {
            _playerStatsManager.SetRegularCoins((long)playerController.CoinsCollected);
        }

        CompletePetPurchase(shopIndex);
    }

    private void BuyPetWithSilver(int shopIndex, int silverPrice)
    {
        var player = FindFirstObjectByType<KinematicCharacterController.Examples.ExampleCharacterController>();
        if (player == null) return;

        if (!player.TrySpendSilverCoins(silverPrice))
        {
            onNotEnoughSilverCoins?.Invoke();
            if (silverCoinsShopManager != null)
            {
                silverCoinsShopManager.OpenSilverShopForInsufficientFunds();
            }
            return;
        }

        if (_playerStatsManager != null)
        {
            _playerStatsManager.SetSilverCoins(player.SilverCoins);
        }

        CompletePetPurchase(shopIndex);
    }

    private void CompletePetPurchase(int shopIndex)
    {
        int nonDonateCount = 0;
        foreach (var pet in ownedPets)
        {
            if (!pet.IsDonatePet) nonDonateCount++;
        }

        if (nonDonateCount >= maxPets)
        {
            ShowInventoryFullMessage();
            return;
        }

        var currentShop = petShops[shopIndex];
        if (currentShop == null) return;

        int newPetIndex = GetRandomPetIndex(currentShop);

        var newPet = new PetInstance
        {
            id = nextPetId++,
            shopIndex = shopIndex,
            petTypeIndex = newPetIndex
        };

        var petMultiplier = petMultipliers.Find(m => m.shopIndex == shopIndex && m.petLocalIndex == newPetIndex);
        if (petMultiplier != null)
        {
            newPet.SetDonateStatus(petMultiplier.isDonatePet);
        }

        ownedPets.Add(newPet);
        
        // 🔥 Сохранение через YG2.saves
        SavePetsData();

        UpdatePetsListUI();
        UpdateCoinsUI();
        UpdateSilverCoinsUI();
        UpdatePetCounter();
        ShowPurchaseSuccess(currentShop, newPetIndex);
        SelectPetInInventory(ownedPets.Count - 1);

        if (autoEquipToggle != null && autoEquipToggle.isOn)
        {
            AutoEquipBestPets();
        }

        CalculateCurrentMultiplier();
        OnPetPurchased();
    }

    private void OnPetPurchased()
    {
        SaveManager saveManager = FindFirstObjectByType<SaveManager>();
        if (saveManager != null)
        {
            saveManager.OnDonatePetPurchased();
        }
    }

    public void AddPetFromExternal(PetInstance pet)
    {
        var petMultiplier = petMultipliers.Find(m => m.shopIndex == pet.shopIndex && m.petLocalIndex == pet.petTypeIndex);
        if (petMultiplier != null)
        {
            pet.SetDonateStatus(petMultiplier.isDonatePet);
        }

        ownedPets.Add(pet);
        nextPetId = Mathf.Max(nextPetId, pet.id + 1);

        if (_playerStatsManager != null && playerController != null)
        {
            _playerStatsManager.SetRegularCoins((long)playerController.CoinsCollected);
        }

        SavePetsData();

        UpdatePetsListUI();
        UpdatePetCounter();
        CalculateCurrentMultiplier();

        if (autoEquipToggle != null && autoEquipToggle.isOn)
        {
            AutoEquipBestPets();
        }

        OnPetPurchased();
    }

    private void ShowInventoryFullMessage()
    {
        if (inventoryFullText != null)
        {
            inventoryFullText.gameObject.SetActive(true);
        }

        if (messageCoroutine != null)
        {
            StopCoroutine(messageCoroutine);
        }

        messageCoroutine = StartCoroutine(HideMessageAfterDelay(inventoryFullText));
    }

    private void ShowCannotDeleteDonatePetMessage()
    {
        if (cannotDeleteDonateText != null)
        {
            cannotDeleteDonateText.gameObject.SetActive(true);
        }

        if (messageCoroutine != null)
        {
            StopCoroutine(messageCoroutine);
        }

        messageCoroutine = StartCoroutine(HideMessageAfterDelay(cannotDeleteDonateText));
    }

    private void ShowNoPetsToDeleteMessage()
    {
        if (noPetsToDeleteText != null)
        {
            noPetsToDeleteText.gameObject.SetActive(true);
        }

        if (messageCoroutine != null)
        {
            StopCoroutine(messageCoroutine);
        }

        messageCoroutine = StartCoroutine(HideMessageAfterDelay(noPetsToDeleteText));
    }

    private IEnumerator HideMessageAfterDelay(Text messageText)
    {
        yield return new WaitForSeconds(messageDisplayTime);
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }
    }

    private void HideAllMessageTexts()
    {
        if (inventoryFullText != null) inventoryFullText.gameObject.SetActive(false);
        if (cannotDeleteDonateText != null) cannotDeleteDonateText.gameObject.SetActive(false);
        if (noPetsToDeleteText != null) noPetsToDeleteText.gameObject.SetActive(false);
    }

    private void UpdatePetCounter()
    {
        if (petCounterText != null)
        {
            int nonDonatePetsCount = 0;
            foreach (var pet in ownedPets)
            {
                if (!pet.IsDonatePet)
                {
                    nonDonatePetsCount++;
                }
            }
            petCounterText.text = $"{nonDonatePetsCount}/{maxPets}";
        }
    }

    private void UpdateDeleteButtonState()
    {
        if (deletePetButton != null)
        {
            bool canDelete = selectedPetIndex != -1 && !IsSelectedPetDonate();
            deletePetButton.gameObject.SetActive(canDelete);
        }
    }

    private bool IsSelectedPetDonate()
    {
        if (selectedPetIndex == -1 || selectedPetIndex >= ownedPets.Count) return false;
        return ownedPets[selectedPetIndex].IsDonatePet;
    }

    private int GetRandomPetIndex(PetShopSettings shop)
    {
        float randomValue = Random.Range(0f, 100f);
        float cumulative = 0f;
        for (int i = 0; i < shop.dropChances.Count; i++)
        {
            cumulative += shop.dropChances[i];
            if (randomValue <= cumulative) return i;
        }
        return 0;
    }

    private void ShowPurchaseSuccess(PetShopSettings shop, int petIndex)
    {
        if (purchaseSuccessPanel != null)
        {
            if (shop != null && shop.petIcons != null && petIndex < shop.petIcons.Count && shop.petIcons[petIndex] != null && purchasedPetImage != null)
            {
                purchasedPetImage.sprite = shop.petIcons[petIndex];
            }

            purchaseSuccessPanel.SetActive(true);
            playerController?.OnUIOrAdOpened();
        }
    }

    private void SetupButtonListeners()
    {
        if (equipButton != null)
        {
            equipButton.onClick.RemoveAllListeners();
            equipButton.onClick.AddListener(EquipSelectedPet);
        }

        if (unequipButton != null)
        {
            unequipButton.onClick.RemoveAllListeners();
            unequipButton.onClick.AddListener(UnequipSelectedPet);
        }

        if (unequipAllButton != null)
        {
            unequipAllButton.onClick.RemoveAllListeners();
            unequipAllButton.onClick.AddListener(UnequipAllPets);
        }
    }

    private void UpdatePetsListUI()
    {
        if (petsContainer == null || petUIPrefab == null) return;

        foreach (Transform child in petsContainer)
        {
            if (child != null) Destroy(child.gameObject);
        }

        float bestRegularMultiplier = GetBestRegularMultiplier();
        var sortedPets = new List<PetInstance>(ownedPets);
        sortedPets.Sort((pet1, pet2) =>
        {
            bool isEquipped1 = IsPetEquipped(pet1.id);
            bool isEquipped2 = IsPetEquipped(pet2.id);
            if (isEquipped1 != isEquipped2)
            {
                return isEquipped2.CompareTo(isEquipped1);
            }

            float multiplier1 = pet1.GetMultiplier(petMultipliers, bestRegularMultiplier);
            float multiplier2 = pet2.GetMultiplier(petMultipliers, bestRegularMultiplier);
            return multiplier2.CompareTo(multiplier1);
        });

        for (int i = 0; i < sortedPets.Count; i++)
        {
            var pet = sortedPets[i];
            if (pet.shopIndex < 0 || pet.shopIndex >= petShops.Count) continue;
            var shop = petShops[pet.shopIndex];
            if (shop == null || pet.petTypeIndex >= shop.petIcons.Count) continue;

            var petUI = Instantiate(petUIPrefab, petsContainer);
            if (petUI == null) continue;

            int originalIndex = ownedPets.IndexOf(pet);
            petUI.name = $"PetUI_{originalIndex}_{pet.GetPetName(petMultipliers)}";

            var image = petUI.GetComponentInChildren<Image>();
            if (image != null && shop.petIcons != null && pet.petTypeIndex < shop.petIcons.Count && shop.petIcons[pet.petTypeIndex] != null)
            {
                image.sprite = shop.petIcons[pet.petTypeIndex];
            }

            var button = petUI.GetComponentInChildren<Button>();
            if (button != null)
            {
                int index = originalIndex;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectPetInInventory(index));
            }

            var equippedIndicator = petUI.transform.Find("EquippedIndicator");
            if (equippedIndicator != null)
            {
                equippedIndicator.gameObject.SetActive(IsPetEquipped(pet.id));
            }

            var petNameText = petUI.transform.Find("PetNameText")?.GetComponent<Text>();
            if (petNameText != null)
            {
                petNameText.text = pet.GetPetName(petMultipliers);
            }

            var multiplierText = petUI.transform.Find("MultiplierText")?.GetComponent<Text>();
            if (multiplierText != null)
            {
                float multiplier = pet.GetMultiplier(petMultipliers, bestRegularMultiplier);
                multiplierText.text = "x" + FormatMultiplier(multiplier);
                multiplierText.gameObject.SetActive(true);
            }

            var donateIndicator = petUI.transform.Find("DonateIndicator");
            if (donateIndicator != null)
            {
                donateIndicator.gameObject.SetActive(pet.IsDonatePet);
            }

            var frameImage = petUI.transform.Find("FrameImage")?.GetComponent<Image>();
            if (frameImage != null)
            {
                if (originalIndex == selectedPetIndex && selectedFrameSprite != null)
                {
                    frameImage.sprite = selectedFrameSprite;
                    frameImage.gameObject.SetActive(true);
                }
                else if (defaultFrameSprite != null)
                {
                    frameImage.sprite = defaultFrameSprite;
                    frameImage.gameObject.SetActive(true);
                }
                else
                {
                    frameImage.gameObject.SetActive(false);
                }
            }

            var petUIClickHandler = petUI.AddComponent<PetUIClickHandler>();
            petUIClickHandler.Initialize(this, originalIndex);
        }
    }

    private void ShowDeleteConfirmationForSelectedPet()
    {
        if (selectedPetIndex == -1) return;
        var pet = ownedPets[selectedPetIndex];
        if (pet.IsDonatePet)
        {
            ShowCannotDeleteDonatePetMessage();
            return;
        }

        if (deleteConfirmationPanel != null)
        {
            deleteConfirmationPanel.SetActive(true);
        }

        if (deleteConfirmationText != null)
        {
            deleteConfirmationText.text = $"Вы уверены, что хотите удалить {pet.GetPetName(petMultipliers)}?";
        }
    }

    private void ShowDeleteAllUnequippedConfirmation()
    {
        int unequippedCount = GetUnequippedRegularPetsCount();
        if (unequippedCount == 0)
        {
            ShowNoPetsToDeleteMessage();
            return;
        }

        if (deleteConfirmationPanel != null)
        {
            deleteConfirmationPanel.SetActive(true);
        }

        if (deleteConfirmationText != null)
        {
            deleteConfirmationText.text = $"Вы уверены, что хотите удалить всех неэкипированных обычных питомцев? ({unequippedCount} шт.)\nДонатные питомцы не будут удалены.";
        }

        confirmDeleteButton.onClick.RemoveAllListeners();
        confirmDeleteButton.onClick.AddListener(ConfirmDeleteAllUnequippedPets);
    }

    private int GetUnequippedPetsCount()
    {
        int count = 0;
        foreach (var pet in ownedPets)
        {
            if (!IsPetEquipped(pet.id))
            {
                count++;
            }
        }
        return count;
    }

    private int GetUnequippedRegularPetsCount()
    {
        int count = 0;
        foreach (var pet in ownedPets)
        {
            if (!IsPetEquipped(pet.id) && !pet.IsDonatePet)
            {
                count++;
            }
        }
        return count;
    }

    private void ConfirmDeleteAllUnequippedPets()
    {
        List<PetInstance> petsToRemove = new List<PetInstance>();
        foreach (var pet in ownedPets)
        {
            if (!IsPetEquipped(pet.id) && !pet.IsDonatePet)
            {
                petsToRemove.Add(pet);
            }
        }

        foreach (var pet in petsToRemove)
        {
            if (pet.petObject != null)
            {
                Destroy(pet.petObject);
            }
            ownedPets.Remove(pet);
        }

        if (selectedPetIndex != -1 && selectedPetIndex < ownedPets.Count && !IsPetEquipped(ownedPets[selectedPetIndex].id) && !ownedPets[selectedPetIndex].IsDonatePet)
        {
            selectedPetIndex = -1;
            if (selectedPetImage != null)
            {
                selectedPetImage.gameObject.SetActive(false);
            }
            if (equipButton != null) equipButton.gameObject.SetActive(false);
            if (unequipButton != null) unequipButton.gameObject.SetActive(false);
        }

        SavePetsData();
        UpdatePetsListUI();
        UpdatePetCounter();
        UpdateDeleteButtonState();
        CalculateCurrentMultiplier();
        HideDeleteConfirmation();

        confirmDeleteButton.onClick.RemoveAllListeners();
        confirmDeleteButton.onClick.AddListener(ConfirmDeletePet);
    }

    private void ConfirmDeletePet()
    {
        if (selectedPetIndex == -1) return;
        var petToDelete = ownedPets[selectedPetIndex];
        if (petToDelete.IsDonatePet)
        {
            ShowCannotDeleteDonatePetMessage();
            HideDeleteConfirmation();
            return;
        }

        if (IsPetEquipped(petToDelete.id))
        {
            int equippedIndex = equippedPets.FindIndex(p => p != null && p.id == petToDelete.id);
            if (equippedIndex >= 0)
            {
                if (petToDelete.petObject != null)
                {
                    Destroy(petToDelete.petObject);
                }
                equippedPets.RemoveAt(equippedIndex);
                RearrangeEquippedPets();
            }
        }
        else
        {
            if (petToDelete.petObject != null)
            {
                Destroy(petToDelete.petObject);
            }
        }

        ownedPets.RemoveAt(selectedPetIndex);
        selectedPetIndex = -1;

        if (selectedPetImage != null)
        {
            selectedPetImage.gameObject.SetActive(false);
        }

        if (equipButton != null) equipButton.gameObject.SetActive(false);
        if (unequipButton != null) unequipButton.gameObject.SetActive(false);

        SavePetsData();
        UpdatePetsListUI();
        UpdatePetCounter();
        UpdateDeleteButtonState();
        CalculateCurrentMultiplier();
        HideDeleteConfirmation();
    }

    private void CancelDeletePet()
    {
        HideDeleteConfirmation();
        confirmDeleteButton.onClick.RemoveAllListeners();
        confirmDeleteButton.onClick.AddListener(ConfirmDeletePet);
    }

    private void HideDeleteConfirmation()
    {
        if (deleteConfirmationPanel != null)
        {
            deleteConfirmationPanel.SetActive(false);
        }
    }

    private void SelectPetInInventory(int inventoryIndex)
    {
        if (inventoryIndex < 0 || inventoryIndex >= ownedPets.Count) return;
        selectedPetIndex = inventoryIndex;
        var pet = ownedPets[inventoryIndex];

        if (pet.shopIndex >= 0 && pet.shopIndex < petShops.Count)
        {
            var shop = petShops[pet.shopIndex];
            if (shop != null && selectedPetImage != null && pet.petTypeIndex < shop.petIcons.Count && shop.petIcons[pet.petTypeIndex] != null)
            {
                selectedPetImage.sprite = shop.petIcons[pet.petTypeIndex];
                selectedPetImage.gameObject.SetActive(true);
            }
            else if (selectedPetImage != null)
            {
                selectedPetImage.gameObject.SetActive(false);
            }
        }

        if (equipButton != null && unequipButton != null)
        {
            bool isEquipped = IsPetEquipped(pet.id);
            equipButton.gameObject.SetActive(!isEquipped && equippedPets.Count < petSlots.Count);
            unequipButton.gameObject.SetActive(isEquipped);
        }

        UpdatePetsListUI();
        UpdateDeleteButtonState();
    }

    private bool IsPetEquipped(int petId)
    {
        return equippedPets.Exists(p => p.id == petId);
    }

    private void EquipSelectedPet()
    {
        if (selectedPetIndex == -1 || equippedPets.Count >= petSlots.Count) return;
        var petToEquip = ownedPets[selectedPetIndex];
        if (IsPetEquipped(petToEquip.id)) return;

        int slotIndex = equippedPets.Count;
        if (slotIndex >= petSlots.Count) return;

        if (petToEquip.shopIndex >= 0 && petToEquip.shopIndex < petShops.Count)
        {
            var shop = petShops[petToEquip.shopIndex];
            if (shop != null && petToEquip.petTypeIndex < shop.pet3DPrefabs.Count && petSlots[slotIndex] != null)
            {
                if (petToEquip.petObject != null) Destroy(petToEquip.petObject);

                petToEquip.petObject = Instantiate(
                    shop.pet3DPrefabs[petToEquip.petTypeIndex],
                    petSlots[slotIndex].position,
                    petSlots[slotIndex].rotation,
                    petSlots[slotIndex]
                );
                var canvas = petToEquip.petObject.GetComponentInChildren<Canvas>();
                if (canvas != null) canvas.enabled = false;
            }
        }

        equippedPets.Add(petToEquip);
        SavePetsData();
        UpdatePetsListUI();
        SelectPetInInventory(selectedPetIndex);
        CalculateCurrentMultiplier();
    }

    private void UnequipSelectedPet()
    {
        if (selectedPetIndex == -1) return;
        var petToUnequip = ownedPets[selectedPetIndex];
        int equippedIndex = equippedPets.FindIndex(p => p != null && p.id == petToUnequip.id);
        if (equippedIndex >= 0)
        {
            if (petToUnequip.petObject != null)
            {
                Destroy(petToUnequip.petObject);
                petToUnequip.petObject = null;
            }
            equippedPets.RemoveAt(equippedIndex);
            RearrangeEquippedPets();
            SavePetsData();
            UpdatePetsListUI();
            SelectPetInInventory(selectedPetIndex);
            CalculateCurrentMultiplier();
        }
    }

    private void RearrangeEquippedPets()
    {
        foreach (var pet in equippedPets)
        {
            if (pet.petObject != null)
            {
                Destroy(pet.petObject);
                pet.petObject = null;
            }
        }

        for (int i = 0; i < equippedPets.Count; i++)
        {
            var pet = equippedPets[i];
            if (pet.shopIndex >= 0 && pet.shopIndex < petShops.Count)
            {
                var shop = petShops[pet.shopIndex];
                if (shop != null && i < petSlots.Count && petSlots[i] != null &&
                    pet.petTypeIndex < shop.pet3DPrefabs.Count)
                {
                    pet.petObject = Instantiate(
                        shop.pet3DPrefabs[pet.petTypeIndex],
                        petSlots[i].position,
                        petSlots[i].rotation,
                        petSlots[i]
                    );
                    var canvas = pet.petObject.GetComponentInChildren<Canvas>();
                    if (canvas != null) canvas.enabled = false;
                }
            }
        }
    }

    private void UnequipAllPets()
    {
        foreach (var pet in equippedPets)
        {
            if (pet != null && pet.petObject != null)
            {
                Destroy(pet.petObject);
                pet.petObject = null;
            }
        }
        equippedPets.Clear();
        SavePetsData();
        UpdatePetsListUI();
        if (selectedPetIndex != -1)
        {
            SelectPetInInventory(selectedPetIndex);
        }
        CalculateCurrentMultiplier();
    }

    private void UpdateCoinsUI()
    {
        if (coinsText != null && playerController != null)
        {
            coinsText.text = $"Монеты: {FormatNumber(playerController.CoinsCollected)}";
        }
    }

    private string FormatNumber(double number)
    {
        if (number >= 1_000_000_000_000)
            return (number / 1_000_000_000_000).ToString("F1") + "T";
        else if (number >= 1_000_000_000)
            return (number / 1_000_000_000).ToString("F1") + "B";
        else if (number >= 1_000_000)
            return (number / 1_000_000).ToString("F1") + "M";
        else if (number >= 1_000)
            return (number / 1_000).ToString("F1") + "K";
        else
            return ((long)number).ToString();
    }

    private string FormatMultiplier(float value)
    {
        var culture = CultureInfo.InvariantCulture;
        if (value >= 1_000_000_000)
            return (value / 1_000_000_000f).ToString("0.##", culture) + "B";
        else if (value >= 1_000_000)
            return (value / 1_000_000f).ToString("0.##", culture) + "M";
        else if (value >= 1_000)
            return (value / 1_000f).ToString("0.##", culture) + "K";
        else
            return value.ToString("0.##", culture);
    }

    private void CloseAllPanels()
    {
        foreach (var shop in petShops)
        {
            if (shop != null && shop.shopUI != null) shop.shopUI.SetActive(false);
        }

        if (purchaseSuccessPanel != null) purchaseSuccessPanel.SetActive(false);
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (donateShopPanel != null) donateShopPanel.SetActive(false);
        if (deleteConfirmationPanel != null) deleteConfirmationPanel.SetActive(false);
    }

    public void OnPetUIClick(int petIndex)
    {
        SelectPetInInventory(petIndex);
    }

    private void OnAutoEquipToggleChanged(bool isOn)
    {
        if (isOn)
        {
            AutoEquipBestPets();
        }
    }

    public void AutoEquipBestPets()
    {
        UnequipAllPets();
        float bestRegularMultiplier = GetBestRegularMultiplier();
        var sortedPets = new List<PetInstance>(ownedPets);
        sortedPets.Sort((pet1, pet2) =>
        {
            float multiplier1 = pet1.GetMultiplier(petMultipliers, bestRegularMultiplier);
            float multiplier2 = pet2.GetMultiplier(petMultipliers, bestRegularMultiplier);
            return multiplier2.CompareTo(multiplier1);
        });

        int petsToEquip = Mathf.Min(3, sortedPets.Count, petSlots.Count);
        for (int i = 0; i < petsToEquip; i++)
        {
            var pet = sortedPets[i];
            int originalIndex = ownedPets.IndexOf(pet);
            if (originalIndex != -1)
            {
                selectedPetIndex = originalIndex;
                EquipSelectedPet();
            }
        }

        selectedPetIndex = -1;
        UpdatePetsListUI();
        UpdateDeleteButtonState();
        SavePetsData();
    }

    public void ForceSave()
    {
        SavePetsData();
    }

    public void UpdateSilverCoinsUI()
    {
        if (silverCoinsText != null)
        {
            var player = FindFirstObjectByType<KinematicCharacterController.Examples.ExampleCharacterController>();
            if (player != null)
            {
                silverCoinsText.text = player.SilverCoins.ToString();
            }
        }
    }

    [ContextMenu("Сбросить данные питомцев")]
    public void ResetPetsData()
    {
        ownedPets.Clear();
        equippedPets.Clear();
        nextPetId = 0;
        selectedPetIndex = -1;

        foreach (var shop in petShops)
        {
            if (shop != null && shop.shopUI != null) shop.shopUI.SetActive(false);
        }

        UpdatePetsListUI();
        CalculateCurrentMultiplier();
        UpdatePetCounter();
        UpdateDeleteButtonState();
        SavePetsData();
        OnPetPurchased();

        Debug.Log("Данные питомцев сброшены");
    }

    public string GetPetsStats()
    {
        int totalPets = ownedPets.Count;
        int equippedCount = equippedPets.Count;
        int donatePetsCount = 0;
        foreach (var pet in ownedPets)
        {
            if (pet.IsDonatePet) donatePetsCount++;
        }

        return $"Всего питомцев: {totalPets}\n" +
               $"Экипировано: {equippedCount}\n" +
               $"Донатных: {donatePetsCount}\n" +
               $"Множитель: {PetMultiplierCounter:F1}x";
    }
}

public class ShopTrigger : MonoBehaviour
{
    private PetSystem petSystem;
    private int shopIndex;

    public void Initialize(PetSystem system, int index)
    {
        petSystem = system;
        shopIndex = index;
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
        else
        {
            Debug.LogError("ShopTrigger: объект не имеет Collider!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            petSystem.OnShopTriggerEnter(shopIndex);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            petSystem.OnShopTriggerExit(shopIndex);
        }
    }
}

public class PetUIClickHandler : MonoBehaviour, IPointerClickHandler
{
    private PetSystem petSystem;
    private int petIndex;

    public void Initialize(PetSystem system, int index)
    {
        petSystem = system;
        petIndex = index;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        petSystem.OnPetUIClick(petIndex);
    }
}

public class DonateShopTrigger : MonoBehaviour
{
    private PetSystem petSystem;

    public void Initialize(PetSystem system)
    {
        petSystem = system;
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
        else
        {
            Debug.LogError("DonateShopTrigger: объект не имеет Collider!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            petSystem.OpenDonateShopFromTrigger();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            petSystem.TryCloseDonateShopFromTrigger();
        }
    }
}
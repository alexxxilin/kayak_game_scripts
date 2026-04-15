using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using KinematicCharacterController.Examples;
using System.Linq;
using YG;
using YG.Utils.Pay;

public class FortuneWheel : MonoBehaviour
{
    [System.Serializable]
    public class WheelSector
    {
        public string name;
        public int minAngle;
        public int maxAngle;
        public Sprite icon;
        public Color color = Color.white;

        [Header("Настройки награды")]
        public RewardType rewardType;
        public long rewardAmount;
        public int spinsToAdd;

        [Header("Для награды типа Pet")]
        public int shopIndex = 0;
        public bool isDonatePet = false;

        [Header("Шанс выпадения")]
        [Range(0.1f, 100f)]
        public float dropChance = 10f;
    }

    public enum RewardType
    {
        Coins,
        AdditionalSpins,
        Nothing,
        Pet
    }

    [Header("Основные настройки")]
    public Button wheelMenuButton;
    public GameObject wheelPanel;
    public Image wheelImage;
    public RectTransform wheelPointer;

    [Header("Элементы управления в панели")]
    public Button spinButton;
    public Button closeButton;
    public Button adSpinButton;
    public Button buySpin10DonateButton;
    public TextMeshProUGUI buySpin10ButtonText;

    [Header("Рекламное вращение")]
    public string adRewardId = "fortune_spin";
    public float adSpinCooldown = 300f;
    public TextMeshProUGUI adSpinTimerText;
    public TextMeshProUGUI adSpinAvailableText;

    [Header("Сектора колеса")]
    public WheelSector[] sectors = new WheelSector[]
    {
        new WheelSector { name = "1M монет", minAngle = 0, maxAngle = 90, color = Color.yellow, rewardType = RewardType.Coins, rewardAmount = 1000000, dropChance = 15f },
        new WheelSector { name = "15K монет", minAngle = 90, maxAngle = 180, color = Color.green, rewardType = RewardType.Coins, rewardAmount = 15000, dropChance = 40f },
        new WheelSector { name = "+1 вращение", minAngle = 180, maxAngle = 270, color = Color.cyan, rewardType = RewardType.AdditionalSpins, spinsToAdd = 1, dropChance = 40f },
        new WheelSector { name = "Питомец!", minAngle = 270, maxAngle = 360, color = Color.magenta, rewardType = RewardType.Pet, shopIndex = 0, isDonatePet = false, dropChance = 5f }
    };

    [Header("Настройки вращения")]
    public float spinDuration = 4f;
    public AnimationCurve spinCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public int minSpins = 2;
    public int maxSpins = 5;

    [Header("UI элементы в панели")]
    public TextMeshProUGUI spinsCountText;
    public TextMeshProUGUI freeSpinsTimerText;
    public TextMeshProUGUI sectorChancesText;
    public GameObject rewardPanel;
    public Image rewardIcon;

    [Header("Индикаторы на основном интерфейсе")]
    public GameObject spinAvailableIndicator;
    public TextMeshProUGUI globalFreeSpinTimerText;

    [Header("Настройки спинов")]
    public int startSpins = 5;

    [Header("Анимация кнопки")]
    public float buttonRotationSpeed = 90f;

    [Header("Бесплатные вращения")]
    public bool enableFreeSpins = true;
    public float freeSpinInterval = 300f;
    public int freeSpinsAmount = 3;

    private int currentSpins;
    private bool isSpinning = false;
    private bool isWheelOpen = false;
    private ExampleCharacterController playerController;
    private PetSystem petSystem;
    private float _freeSpinTimer;
    private float timeUntilAdSpinAvailable;
    private Coroutine freeSpinCoroutine;
    private Coroutine adSpinTimerCoroutine;
    private float totalChance;
    private float[] cumulativeChances;

    // === ПУБЛИЧНОЕ СВОЙСТВО ДЛЯ ДОСТУПА ИЗ SaveManager ===
    public float TimeUntilAdSpinAvailable
    {
        get => timeUntilAdSpinAvailable;
        set => timeUntilAdSpinAvailable = Mathf.Max(0f, value);
    }

    private void Start()
    {
        playerController = FindFirstObjectByType<ExampleCharacterController>();
        petSystem = FindFirstObjectByType<PetSystem>();
        InitializeUI();
        CalculateChances();
        StartCoroutine(WaitForSaveManager());
        UpdateBuySpinButton();
        YG2.onGetPayments += UpdateBuySpinButton;
    }

    private void OnDestroy()
    {
        YG2.onGetPayments -= UpdateBuySpinButton;
        if (freeSpinCoroutine != null) StopCoroutine(freeSpinCoroutine);
        if (adSpinTimerCoroutine != null) StopCoroutine(adSpinTimerCoroutine);
    }

    private IEnumerator WaitForSaveManager()
    {
        SaveManager saveManager = FindFirstObjectByType<SaveManager>();
        int maxWaitFrames = 100;
        int currentFrame = 0;
        while ((saveManager == null || !saveManager.HasSaveData()) && currentFrame < maxWaitFrames)
        {
            yield return new WaitForEndOfFrame();
            saveManager = FindFirstObjectByType<SaveManager>();
            currentFrame++;
        }
        LoadSpinsData();
        StartFreeSpinTimer();
        StartAdSpinTimer();
    }

    private void CalculateChances()
    {
        totalChance = 0f;
        foreach (var sector in sectors)
        {
            totalChance += sector.dropChance;
        }
        cumulativeChances = new float[sectors.Length];
        float cumulative = 0f;
        for (int i = 0; i < sectors.Length; i++)
        {
            cumulative += sectors[i].dropChance;
            cumulativeChances[i] = cumulative;
        }
        UpdateSectorChancesUI();
    }

    private void UpdateSectorChancesUI()
    {
        if (sectorChancesText != null)
        {
            string chancesText = "Шансы выпадения:\n";
            foreach (var sector in sectors)
            {
                float normalizedChance = (sector.dropChance / totalChance) * 100f;
                chancesText += $"{sector.name}: {normalizedChance:F1}%\n";
            }
            sectorChancesText.text = chancesText;
        }
    }

    private WheelSector GetRandomSectorByChance()
    {
        float randomValue = Random.Range(0f, totalChance);
        for (int i = 0; i < sectors.Length; i++)
        {
            if (randomValue <= cumulativeChances[i])
            {
                Debug.Log($"Выпал сектор: {sectors[i].name} (шанс: {sectors[i].dropChance}%)");
                return sectors[i];
            }
        }
        return sectors[sectors.Length - 1];
    }

    private void Update()
    {
        if (wheelMenuButton != null)
        {
            RotateMenuButton();
        }
        UpdateGlobalUI();
    }

    private void RotateMenuButton()
    {
        wheelMenuButton.transform.Rotate(0, 0, buttonRotationSpeed * Time.deltaTime);
    }

    private void UpdateGlobalUI()
    {
        if (spinAvailableIndicator != null)
        {
            spinAvailableIndicator.SetActive(currentSpins > 0);
        }
        if (globalFreeSpinTimerText != null)
        {
            globalFreeSpinTimerText.gameObject.SetActive(enableFreeSpins);
            if (enableFreeSpins)
            {
                if (_freeSpinTimer <= 0)
                {
                    globalFreeSpinTimerText.text = "Готово!";
                    globalFreeSpinTimerText.color = Color.green;
                }
                else
                {
                    int minutes = Mathf.FloorToInt(_freeSpinTimer / 60);
                    int seconds = Mathf.FloorToInt(_freeSpinTimer % 60);
                    globalFreeSpinTimerText.text = $"{minutes:00}:{seconds:00}";
                    globalFreeSpinTimerText.color = Color.white;
                }
            }
        }
    }

    private void LoadSpinsData()
    {
        currentSpins = YG2.saves?.fortuneWheelSpins ?? startSpins;
        _freeSpinTimer = freeSpinInterval;
        timeUntilAdSpinAvailable = YG2.saves?.timeUntilAdSpinAvailable ?? 0f;
        UpdateUI();
        UpdateGlobalUI();
        Debug.Log($"Загружено: спины = {currentSpins}. Таймер бесплатных вращений = {_freeSpinTimer} сек.");
    }

    private void SaveSpinsData()
    {
        if (YG2.saves != null)
        {
            YG2.saves.fortuneWheelSpins = currentSpins;
            YG2.saves.timeUntilAdSpinAvailable = timeUntilAdSpinAvailable;
            YG2.SaveProgress();
        }
    }

    [ContextMenu("Сбросить таймер бесплатных вращений")]
    public void ResetFreeSpinTimer()
    {
        _freeSpinTimer = freeSpinInterval;
        UpdateFreeSpinTimerUI();
        UpdateGlobalUI();
    }

    [ContextMenu("Сбросить колесо фортуны (с перезаписью облака)")]
    public void ResetFortuneWheelFully()
    {
        currentSpins = startSpins;
        _freeSpinTimer = freeSpinInterval;
        timeUntilAdSpinAvailable = 0f;
        SaveSpinsData();
        UpdateUI();
        UpdateGlobalUI();
    }

    private void StartFreeSpinTimer()
    {
        if (freeSpinCoroutine != null)
            StopCoroutine(freeSpinCoroutine);
        freeSpinCoroutine = StartCoroutine(FreeSpinTimerCoroutine());
    }

    private IEnumerator FreeSpinTimerCoroutine()
    {
        while (enableFreeSpins)
        {
            _freeSpinTimer -= Time.deltaTime;
            if (_freeSpinTimer <= 0)
            {
                AddSpins(freeSpinsAmount);
                _freeSpinTimer = freeSpinInterval;
                SaveSpinsData();
            }
            UpdateFreeSpinTimerUI();
            yield return null;
        }
    }

    private void StartAdSpinTimer()
    {
        if (adSpinTimerCoroutine != null)
            StopCoroutine(adSpinTimerCoroutine);
        adSpinTimerCoroutine = StartCoroutine(AdSpinTimerCoroutine());
    }

    private IEnumerator AdSpinTimerCoroutine()
    {
        while (true)
        {
            timeUntilAdSpinAvailable -= Time.deltaTime;
            if (timeUntilAdSpinAvailable < 0) timeUntilAdSpinAvailable = 0;
            UpdateAdSpinButtonState();
            yield return null;
        }
    }

    private void InitializeUI()
    {
        if (wheelMenuButton != null)
        {
            wheelMenuButton.onClick.RemoveAllListeners();
            wheelMenuButton.onClick.AddListener(OpenWheelPanel);
        }
        if (spinButton != null)
        {
            spinButton.onClick.RemoveAllListeners();
            spinButton.onClick.AddListener(StartSpin);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseWheelPanel);
        }
        if (adSpinButton != null)
        {
            adSpinButton.onClick.RemoveAllListeners();
            adSpinButton.onClick.AddListener(ShowAdForSpin);
        }
        if (buySpin10DonateButton != null)
        {
            buySpin10DonateButton.onClick.RemoveAllListeners();
            buySpin10DonateButton.onClick.AddListener(BuySpin10Donate);
        }
        if (adSpinAvailableText != null)
        {
            adSpinAvailableText.gameObject.SetActive(false);
        }
        if (wheelPanel != null) wheelPanel.SetActive(false);
        if (rewardPanel != null) rewardPanel.SetActive(false);
    }

    public void OpenWheelPanel()
    {
        if (wheelPanel != null && !isWheelOpen)
        {
            wheelPanel.SetActive(true);
            isWheelOpen = true;
            UpdateUI();
        }
    }

    public void CloseWheelPanel()
    {
        if (wheelPanel != null && isWheelOpen && !isSpinning)
        {
            wheelPanel.SetActive(false);
            isWheelOpen = false;
            SaveSpinsData();
        }
    }

    private void StartSpin()
    {
        if (currentSpins <= 0) return;
        if (isSpinning) return;
        if (!isWheelOpen) return;

        currentSpins--;
        SaveSpinsData();
        WheelSector winningSector = GetRandomSectorByChance();
        ProcessWin(winningSector);
        StartCoroutine(SpinWheelVisual(winningSector));
    }

    private void ShowAdForSpin()
    {
        if (timeUntilAdSpinAvailable <= 0)
        {
            YG2.RewardedAdvShow(adRewardId, () =>
            {
                AddSpins(1);
                timeUntilAdSpinAvailable = adSpinCooldown;
                SaveSpinsData();
                Debug.Log("Получено 1 вращение за рекламу!");
            });
        }
    }

    private void BuySpin10Donate()
    {
        YG2.BuyPayments("wheel_spin_10");
    }

    private IEnumerator SpinWheelVisual(WheelSector winningSector)
    {
        isSpinning = true;
        adSpinButton.interactable = false;
        buySpin10DonateButton.interactable = false;
        closeButton.interactable = false;

        float sectorCenter = (winningSector.minAngle + winningSector.maxAngle) / 2f;
        float targetAngle = (90f - sectorCenter + 360f) % 360f;
        int totalSpins = Random.Range(minSpins, maxSpins + 1);
        float targetRotation = 360f * totalSpins + targetAngle;
        float startRotation = wheelImage.transform.eulerAngles.z;
        float elapsedTime = 0f;

        while (elapsedTime < spinDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / spinDuration;
            float curvedProgress = spinCurve.Evaluate(progress);
            float currentRotation = Mathf.Lerp(startRotation, targetRotation, curvedProgress);
            wheelImage.transform.rotation = Quaternion.Euler(0, 0, currentRotation);
            yield return null;
        }

        wheelImage.transform.rotation = Quaternion.Euler(0, 0, targetRotation);
        yield return new WaitForSeconds(0.5f);
        ShowRewardPanelForSector(winningSector);

        isSpinning = false;
        adSpinButton.interactable = true;
        buySpin10DonateButton.interactable = true;
        closeButton.interactable = true;
        UpdateUI();
    }

    private void ProcessWin(WheelSector sector)
    {
        switch (sector.rewardType)
        {
            case RewardType.Coins:
                if (playerController != null)
                    playerController.AddCoins(sector.rewardAmount);
                else
                    Debug.LogError("FortuneWheel: playerController is null during Coins reward!");
                break;
            case RewardType.AdditionalSpins:
                currentSpins += sector.spinsToAdd;
                break;
            case RewardType.Pet:
                if (petSystem != null)
                {
                    if (sector.shopIndex >= 0 && sector.shopIndex < petSystem.petShops.Count)
                    {
                        var shop = petSystem.petShops[sector.shopIndex];
                        int petTypeIndex = GetRandomPetIndexFromShop(shop);
                        var newPet = new PetSystem.PetInstance
                        {
                            id = petSystem.GetNextPetId(),
                            shopIndex = sector.shopIndex,
                            petTypeIndex = petTypeIndex
                        };
                        newPet.SetDonateStatus(sector.isDonatePet);
                        petSystem.AddPetFromExternal(newPet);
                    }
                    else
                    {
                        Debug.LogError($"FortuneWheel: shopIndex {sector.shopIndex} вне диапазона");
                    }
                }
                else
                {
                    Debug.LogError("FortuneWheel: PetSystem is null!");
                }
                break;
        }
        SaveSpinsData();
    }

    private void ShowRewardPanelForSector(WheelSector sector)
    {
        string rewardMessage = "";
        Sprite rewardSprite = sector.icon;
        bool showPanel = true;

        switch (sector.rewardType)
        {
            case RewardType.Coins:
                rewardMessage = $"+{FormatNumber(sector.rewardAmount)} МОНЕТ!";
                break;
            case RewardType.AdditionalSpins:
                rewardMessage = $"+{sector.spinsToAdd} ВРАЩЕНИЙ!";
                break;
            case RewardType.Nothing:
                rewardMessage = "ПОВЕЗЁТ В СЛЕДУЮЩИЙ РАЗ!";
                showPanel = false;
                break;
            case RewardType.Pet:
                rewardMessage = "НОВЫЙ ПИТОМЕЦ!";
                break;
        }

        if (showPanel)
            ShowRewardPanel(rewardMessage, rewardSprite);
    }

    private int GetRandomPetIndexFromShop(PetSystem.PetShopSettings shop)
    {
        float randomValue = Random.Range(0f, 100f);
        float cumulative = 0f;
        for (int i = 0; i < shop.dropChances.Count; i++)
        {
            cumulative += shop.dropChances[i];
            if (randomValue <= cumulative)
                return i;
        }
        return 0;
    }

    public void AddSpins(int amount)
    {
        currentSpins += amount;
        SaveSpinsData();
        UpdateUI();
        UpdateGlobalUI();
    }

    private void UpdateAdSpinButtonState()
    {
        bool isAdReady = timeUntilAdSpinAvailable <= 0;
        bool canInteract = isAdReady && !isSpinning;
        if (adSpinButton != null)
        {
            adSpinButton.interactable = canInteract;
        }
        if (adSpinTimerText != null)
        {
            if (isAdReady)
            {
                adSpinTimerText.text = "";
            }
            else
            {
                int minutes = Mathf.FloorToInt(timeUntilAdSpinAvailable / 60);
                int seconds = Mathf.FloorToInt(timeUntilAdSpinAvailable % 60);
                adSpinTimerText.text = $"{minutes:00}:{seconds:00}";
                adSpinTimerText.color = Color.red;
            }
        }
        if (adSpinAvailableText != null)
        {
            adSpinAvailableText.gameObject.SetActive(isAdReady);
        }
    }

    private void UpdateUI()
    {
        if (!isWheelOpen) return;
        if (spinsCountText != null)
        {
            spinsCountText.text = $"{currentSpins}";
            spinsCountText.color = currentSpins > 0 ? Color.white : Color.red;
        }
        UpdateFreeSpinTimerUI();
        UpdateAdSpinButtonState();
    }

    private void UpdateFreeSpinTimerUI()
    {
        if (freeSpinsTimerText != null && enableFreeSpins)
        {
            int minutes = Mathf.FloorToInt(_freeSpinTimer / 60);
            int seconds = Mathf.FloorToInt(_freeSpinTimer % 60);
            freeSpinsTimerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void UpdateBuySpinButton()
    {
        if (buySpin10ButtonText != null && buySpin10DonateButton != null)
        {
            var purchase = YG2.PurchaseByID("wheel_spin_10");
            if (purchase != null && !string.IsNullOrEmpty(purchase.priceCurrencyCode))
            {
                string buyText = GetLocalizedBuyText();
                string currencyCode = purchase.priceCurrencyCode.ToUpper();
                buySpin10ButtonText.text = $"{buyText} {purchase.priceValue} {currencyCode}";
                buySpin10DonateButton.interactable = true;
            }
            else
            {
                buySpin10ButtonText.text = "Загрузка...";
                buySpin10DonateButton.interactable = false;
            }
        }
    }

    private string GetLocalizedBuyText()
    {
        return YG2.lang switch
        {
            "en" => "+10 spins",
            _ => "+10 вращений"
        };
    }

    private string FormatNumber(long number)
    {
        if (number >= 1000000000000) return (number / 1000000000000f).ToString("F1") + "T";
        if (number >= 1000000000) return (number / 1000000000f).ToString("F1") + "B";
        if (number >= 1000000) return (number / 1000000f).ToString("F1") + "M";
        if (number >= 1000) return (number / 1000f).ToString("F1") + "K";
        return number.ToString();
    }

    private void ShowRewardPanel(string message, Sprite icon)
    {
        if (rewardPanel != null)
        {
            if (rewardIcon != null)
            {
                rewardIcon.sprite = icon;
                rewardIcon.gameObject.SetActive(icon != null);
            }
            rewardPanel.SetActive(true);
        }
    }

    public void AddSpinsFromExternal(int amount) => AddSpins(amount);

    public void ResetSpins()
    {
        currentSpins = startSpins;
        _freeSpinTimer = freeSpinInterval;
        timeUntilAdSpinAvailable = 0f;
        SaveSpinsData();
        UpdateUI();
        UpdateGlobalUI();
    }

    public bool IsWheelOpen() => isWheelOpen;
    public int GetCurrentSpins() => currentSpins;
    public void SetSpins(int spins)
    {
        currentSpins = spins;
        UpdateUI();
        UpdateGlobalUI();
    }
    public float GetTimeUntilFreeSpin() => _freeSpinTimer;
    public void SetTimeUntilFreeSpin(float time)
    {
        _freeSpinTimer = time;
        UpdateFreeSpinTimerUI();
        UpdateGlobalUI();
    }
    public void UpdateSpinCounterText(int spins)
    {
        if (spinsCountText != null) spinsCountText.text = $"{spins}";
    }
    public void UpdateSpinTimerText(float timer)
    {
        if (freeSpinsTimerText != null)
        {
            int minutes = Mathf.FloorToInt(timer / 60);
            int seconds = Mathf.FloorToInt(timer % 60);
            freeSpinsTimerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) SaveSpinsData();
    }

    private void OnApplicationQuit()
    {
        SaveSpinsData();
    }

    public void OnGameLoad()
    {
        UpdateUI();
        UpdateGlobalUI();
    }
}
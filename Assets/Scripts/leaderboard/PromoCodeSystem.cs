using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using YG;
using KinematicCharacterController.Examples;

public class PromoCodeSystem : MonoBehaviour
{
    [Header("UI Элементы")]
    public TMP_InputField promoCodeInput;
    public Button activateButton;
    public Button closeButton;
    public TextMeshProUGUI resultText;
    public GameObject promoCodePanel;
    
    [Header("Определение языка")]
    [Tooltip("Текст для сканирования языка (должен содержать 'ещё игры' или 'more games')")]
    public TextMeshProUGUI languageDetectorText;
    
    [Header("Тексты результатов")]
    [Tooltip("Текст при успешной активации (русский)")]
    public string successTextRU = "Промокод активирован";
    [Tooltip("Текст при ошибке (русский)")]
    public string errorTextRU = "Промокод устарел или уже активирован";
    [Tooltip("Текст при успешной активации (английский)")]
    public string successTextEN = "Promo code activated";
    [Tooltip("Текст при ошибке (английский)")]
    public string errorTextEN = "Promo code expired or already activated";
    
    [Header("Настройки отображения")]
    public float resultDisplayTime = 3f;
    public Color successColor = Color.green;
    public Color errorColor = Color.red;
    
    [Header("Аудио")]
    public AudioClip successSound;
    public AudioClip errorSound;
    private AudioSource audioSource;
    
    [Header("Флаги Яндекс Игр")]
    [Tooltip("Название флага для списка активных промокодов (формат: CODE1,CODE2,CODE3)")]
    public string activePromoCodesFlag = "active_promo_codes";
    
    [Tooltip("Название флага для включения/выключения системы промокодов")]
    public string promoSystemEnabledFlag = "promo_system_enabled";
    
    [Tooltip("Базовое название флага для наград промокодов (будут использоваться флаги promo_rewards, promo_rewards1, promo_rewards2 и т.д.)")]
    public string promoRewardsFlag = "promo_rewards";
    
    [Tooltip("Название флага для максимального количества монет в награде")]
    public string maxCoinsRewardFlag = "max_coins_reward";
    
    private List<PromoCodeData> allPromoCodes = new List<PromoCodeData>();
    private ExampleCharacterController playerController;
    private PetSystem petSystem;
    private PlayerStatsManager playerStatsManager;
    
    private Coroutine messageCoroutine;
    
    private bool _isEnglish = false;
    private bool _languageDetected = false;
    
    private bool _isPromoSystemEnabled = true;
    private List<string> _allowedPromoCodes = new List<string>();
    private Dictionary<string, PromoCodeData> _flagRewards = new Dictionary<string, PromoCodeData>();
    private long _maxCoinsReward = 50000;

    private bool _sdkReady = false;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        
        playerController = FindFirstObjectByType<ExampleCharacterController>();
        petSystem = FindFirstObjectByType<PetSystem>();
        playerStatsManager = FindFirstObjectByType<PlayerStatsManager>();
        
        if (promoCodePanel != null)
            promoCodePanel.SetActive(false);
        
        YG2.onGetSDKData += OnSDKDataLoaded;
        
        SetupUI();
    }
    
    private void Start()
    {
        if (YG2.isSDKEnabled)
        {
            OnSDKDataLoaded();
        }
    }
    
    private void OnDestroy()
    {
        YG2.onGetSDKData -= OnSDKDataLoaded;
    }

    private void OnSDKDataLoaded()
    {
        _sdkReady = true;
        Debug.Log("[PromoCode] SDK загружен, инициализация...");
        
        LoadActivatedPromoCodes();
        LoadPromoCodesFromFlags();
        DetectLanguage();
    }
    
    private void DetectLanguage()
    {
        if (languageDetectorText != null && !string.IsNullOrEmpty(languageDetectorText.text))
        {
            string text = languageDetectorText.text.ToLower().Trim();
            
            if (text.Contains("more games"))
            {
                _isEnglish = true;
                _languageDetected = true;
                Debug.Log("[PromoCode] Язык определён: Английский (найдено 'more games')");
            }
            else if (text.Contains("ещё игры") || text.Contains("еще игры"))
            {
                _isEnglish = false;
                _languageDetected = true;
                Debug.Log("[PromoCode] Язык определён: Русский (найдено 'ещё игры')");
            }
            else
            {
                _isEnglish = (YG2.lang != "ru");
                _languageDetected = true;
                Debug.Log($"[PromoCode] Язык определён через YG2.lang: {(YG2.lang == "ru" ? "Русский" : "Английский")}");
            }
        }
        else
        {
            _isEnglish = (YG2.lang != "ru");
            _languageDetected = true;
            Debug.Log($"[PromoCode] Язык определён через YG2.lang (текст не назначен): {(YG2.lang == "ru" ? "Русский" : "Английский")}");
        }
    }
    
    private string GetLocalizedText(string ruText, string enText)
    {
        if (!_languageDetected) DetectLanguage();
        return _isEnglish ? enText : ruText;
    }
    
    private void LoadPromoCodesFromFlags()
    {
        if (!_sdkReady) return;

        Debug.Log("[PromoCode] Загрузка промокодов из флагов...");
        
        // Флаг включения системы
        if (YG2.TryGetFlagAsBool(promoSystemEnabledFlag, out bool systemEnabled))
        {
            _isPromoSystemEnabled = systemEnabled;
            Debug.Log($"[PromoCode] Система промокодов: {(_isPromoSystemEnabled ? "ВКЛЮЧЕНА" : "ВЫКЛЮЧЕНА")}");
        }
        else
        {
            _isPromoSystemEnabled = true;
            Debug.Log($"[PromoCode] Флаг {promoSystemEnabledFlag} не найден, система включена по умолчанию");
        }
        
        // Список активных кодов
        if (YG2.TryGetFlag(activePromoCodesFlag, out string activeCodes))
        {
            _allowedPromoCodes.Clear();
            if (!string.IsNullOrEmpty(activeCodes))
            {
                string[] codes = activeCodes.Split(',');
                foreach (var code in codes)
                {
                    _allowedPromoCodes.Add(code.Trim().ToUpper());
                }
            }
            Debug.Log($"[PromoCode] Активные промокоды из флага: {string.Join(", ", _allowedPromoCodes)}");
        }
        else
        {
            Debug.Log($"[PromoCode] Флаг {activePromoCodesFlag} не найден, будут доступны все промокоды из наград");
        }
        
        // Максимальная награда монетами
        if (YG2.TryGetFlag(maxCoinsRewardFlag, out string maxCoinsStr))
        {
            if (long.TryParse(maxCoinsStr, out long maxCoins))
            {
                _maxCoinsReward = maxCoins;
                Debug.Log($"[PromoCode] Максимальная награда монетами: {_maxCoinsReward}");
            }
        }
        else
        {
            Debug.Log($"[PromoCode] Флаг {maxCoinsRewardFlag} не найден, используется значение по умолчанию: {_maxCoinsReward}");
        }
        
        // Очищаем предыдущие награды
        _flagRewards.Clear();
        
        // Собираем все флаги с префиксом promo_rewards
        int flagsFound = 0;
        if (YG2.flags != null)
        {
            foreach (var flag in YG2.flags)
            {
                if (flag.name.StartsWith(promoRewardsFlag)) // например "promo_rewards", "promo_rewards1", "promo_rewards_2" и т.д.
                {
                    Debug.Log($"[PromoCode] Найден флаг наград: {flag.name} = {flag.value}");
                    AddRewardsFromFlag(flag.value);
                    flagsFound++;
                }
            }
        }
        
        if (flagsFound == 0)
        {
            Debug.LogError("[PromoCode] Не найдено ни одного флага с наградами! Промокоды не будут работать.");
        }
        
        InitializePromoCodes();
    }
    
    // Переименовано и больше не очищает _flagRewards
    private void AddRewardsFromFlag(string json)
    {
        try
        {
            var rewards = SimpleJson.Parse(json);
            Debug.Log($"[PromoCode] Распарсено {rewards.Count} записей в JSON");
            
            foreach (var reward in rewards)
            {
                string code = reward.Key.ToUpper();
                var data = reward.Value as Dictionary<string, object>;
                
                if (data != null)
                {
                    PromoCodeData promoData = new PromoCodeData();
                    promoData.code = code;
                    promoData.isActive = true;
                    promoData.expirationDate = DateTime.MaxValue;
                    
                    string type = data.ContainsKey("type") ? data["type"].ToString().ToLower() : "coins";
                    
                    if (type == "coins")
                    {
                        promoData.rewardType = RewardType.Coins;
                        promoData.coinAmount = data.ContainsKey("amount") ? Convert.ToInt64(data["amount"]) : 1000;
                        promoData.description = $"{promoData.coinAmount} монет";
                        Debug.Log($"[PromoCode] Промокод {code}: монеты {promoData.coinAmount}");
                    }
                    else if (type == "pet")
                    {
                        promoData.rewardType = RewardType.Pet;
                        promoData.petShopIndex = data.ContainsKey("shop") ? Convert.ToInt32(data["shop"]) : 0;
                        promoData.petTypeIndex = data.ContainsKey("pet") ? Convert.ToInt32(data["pet"]) : 0;
                        promoData.description = $"Питомец (магазин {promoData.petShopIndex}, тип {promoData.petTypeIndex})";
                        Debug.Log($"[PromoCode] Промокод {code}: питомец [магазин={promoData.petShopIndex}, тип={promoData.petTypeIndex}]");
                    }
                    else
                    {
                        Debug.LogWarning($"[PromoCode] Неизвестный тип награды '{type}' для промокода {code}, пропускаем");
                        continue;
                    }
                    
                    // Добавляем в общий словарь (если ключ уже есть, перезаписываем – последний флаг имеет приоритет)
                    _flagRewards[code] = promoData;
                }
                else
                {
                    Debug.LogWarning($"[PromoCode] Не удалось прочитать данные для промокода {code}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PromoCode] Ошибка парсинга JSON наград: {e.Message}\nJSON: {json}");
        }
    }
    
    private void InitializePromoCodes()
    {
        allPromoCodes.Clear();
        
        // Добавляем все промокоды из собранных наград
        foreach (var flagReward in _flagRewards.Values)
        {
            allPromoCodes.Add(flagReward);
        }
        
        // Фильтруем по списку активных промокодов, если он задан
        if (_allowedPromoCodes.Count > 0)
        {
            int before = allPromoCodes.Count;
            allPromoCodes.RemoveAll(p => !_allowedPromoCodes.Contains(p.code));
            Debug.Log($"[PromoCode] После фильтрации по активным кодам: было {before}, стало {allPromoCodes.Count}");
        }
        else
        {
            Debug.Log("[PromoCode] Список активных кодов не задан, доступны все промокоды из наград");
        }
        
        Debug.Log($"[PromoCode] Инициализировано {allPromoCodes.Count} промокодов");
    }
    
    private void SetupUI()
    {
        if (activateButton != null)
        {
            activateButton.onClick.RemoveAllListeners();
            activateButton.onClick.AddListener(OnActivateButtonClicked);
            activateButton.interactable = false;
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePromoCodePanel);
        }
        
        if (promoCodeInput != null)
        {
            promoCodeInput.onValueChanged.AddListener(OnInputValueChanged);
            promoCodeInput.characterValidation = TMP_InputField.CharacterValidation.None;
            promoCodeInput.contentType = TMP_InputField.ContentType.Alphanumeric;
        }
        
        if (resultText != null)
        {
            resultText.gameObject.SetActive(false);
        }
        
        if (promoCodePanel != null)
        {
            promoCodePanel.SetActive(false);
        }
    }
    
    private void OnInputValueChanged(string text)
    {
        if (activateButton != null)
        {
            activateButton.interactable = !string.IsNullOrWhiteSpace(text);
        }
    }
    
    private void OnActivateButtonClicked()
    {
        if (!_sdkReady)
        {
            ShowResult("SDK ещё не готов, попробуйте позже", errorColor);
            PlaySound(errorSound);
            return;
        }

        if (!_isPromoSystemEnabled)
        {
            ShowResult(GetLocalizedText(errorTextRU, errorTextEN), errorColor);
            PlaySound(errorSound);
            ClearInput();
            return;
        }
        
        if (promoCodeInput == null || string.IsNullOrWhiteSpace(promoCodeInput.text))
        {
            ShowResult(GetLocalizedText(errorTextRU, errorTextEN), errorColor);
            PlaySound(errorSound);
            return;
        }
        
        string inputCode = promoCodeInput.text.Trim().ToUpper();
        ActivatePromoCode(inputCode);
    }
    
    private void ActivatePromoCode(string code)
    {
        if (!_sdkReady) return;

        if (IsPromoCodeActivated(code))
        {
            ShowResult(GetLocalizedText(errorTextRU, errorTextEN), errorColor);
            PlaySound(errorSound);
            ClearInput();
            return;
        }
        
        PromoCodeData promoCode = allPromoCodes.Find(p => p.code == code);
        
        if (promoCode == null)
        {
            ShowResult(GetLocalizedText(errorTextRU, errorTextEN), errorColor);
            PlaySound(errorSound);
            ClearInput();
            return;
        }
        
        if (!promoCode.isActive)
        {
            ShowResult(GetLocalizedText(errorTextRU, errorTextEN), errorColor);
            PlaySound(errorSound);
            ClearInput();
            return;
        }
        
        if (promoCode.expirationDate < DateTime.Now)
        {
            ShowResult(GetLocalizedText(errorTextRU, errorTextEN), errorColor);
            PlaySound(errorSound);
            ClearInput();
            return;
        }
        
        bool success = false;
        string rewardDescription = "";
        
        switch (promoCode.rewardType)
        {
            case RewardType.Coins:
                success = GiveCoinReward(promoCode);
                rewardDescription = $"{promoCode.coinAmount} монет";
                break;
                
            case RewardType.Pet:
                success = GivePetReward(promoCode);
                rewardDescription = promoCode.description;
                break;
        }
        
        if (success)
        {
            SaveActivatedPromoCode(code);
            ShowResult(GetLocalizedText(successTextRU, successTextEN), successColor);
            PlaySound(successSound);
            YG2.SaveProgress();
            Debug.Log($"[PromoCode] Промокод {code} активирован. Награда: {rewardDescription}");
        }
        else
        {
            ShowResult(GetLocalizedText(errorTextRU, errorTextEN), errorColor);
            PlaySound(errorSound);
        }
        
        ClearInput();
    }
    
    private bool GiveCoinReward(PromoCodeData promoCode)
    {
        if (playerController == null)
        {
            Debug.LogError("[PromoCode] PlayerController не найден!");
            return false;
        }
        
        long coinsToGive = promoCode.coinAmount;
        if (coinsToGive > _maxCoinsReward)
        {
            coinsToGive = _maxCoinsReward;
        }
        
        playerController.AddCoins(coinsToGive);
        
        if (playerStatsManager != null)
        {
            playerStatsManager.SetRegularCoins((long)playerController.CoinsCollected);
        }
        
        return true;
    }
    
    private bool GivePetReward(PromoCodeData promoCode)
    {
        if (petSystem == null)
        {
            Debug.LogError("[PromoCode] PetSystem не найден!");
            return false;
        }

        int shopIndex = promoCode.petShopIndex;
        int petIndex = promoCode.petTypeIndex;

        // Проверка валидности индексов
        if (shopIndex < 0 || shopIndex >= petSystem.petShops.Count)
        {
            Debug.LogError($"[PromoCode] Неверный индекс магазина: {shopIndex}");
            return false;
        }

        var shop = petSystem.petShops[shopIndex];
        if (petIndex < 0 || petIndex >= shop.pet3DPrefabs.Count)
        {
            Debug.LogError($"[PromoCode] Неверный индекс питомца: {petIndex} в магазине {shopIndex}");
            return false;
        }

        // Проверка, не владеет ли игрок уже таким питомцем
        bool alreadyOwned = false;
        foreach (var pet in petSystem.ownedPets)
        {
            if (pet.shopIndex == shopIndex && pet.petTypeIndex == petIndex)
            {
                alreadyOwned = true;
                break;
            }
        }

        if (alreadyOwned)
        {
            ShowResult(GetLocalizedText(errorTextRU, errorTextEN), errorColor);
            return false;
        }

        // Создание нового питомца
        var newPet = new PetSystem.PetInstance
        {
            id = petSystem.GetNextPetId(),
            shopIndex = shopIndex,
            petTypeIndex = petIndex
        };
        newPet.SetDonateStatus(shop.isDonateShop); // Устанавливаем донатность в соответствии с магазином

        petSystem.AddPetFromExternal(newPet);

        // Если питомец донатный, отмечаем в PlayerStatsManager
        if (shop.isDonateShop && playerStatsManager != null)
        {
            playerStatsManager.SetDonatePetPurchased(petIndex, shopIndex);
        }

        return true;
    }
    
    private void LoadActivatedPromoCodes()
    {
        if (!_sdkReady || YG2.saves == null)
        {
            Debug.LogWarning("[PromoCode] YG2.saves не инициализирован или SDK не готов!");
            return;
        }
        
        if (YG2.saves.activatedPromoCodes != null)
        {
            Debug.Log($"[PromoCode] Загружено {YG2.saves.activatedPromoCodes.Count} активированных промокодов");
        }
        else
        {
            YG2.saves.activatedPromoCodes = new List<string>();
        }
    }
    
    private void SaveActivatedPromoCode(string code)
    {
        if (!_sdkReady || YG2.saves == null)
        {
            Debug.LogError("[PromoCode] YG2.saves не инициализирован или SDK не готов!");
            return;
        }
        
        if (YG2.saves.activatedPromoCodes == null)
        {
            YG2.saves.activatedPromoCodes = new List<string>();
        }
        
        if (!YG2.saves.activatedPromoCodes.Contains(code))
        {
            YG2.saves.activatedPromoCodes.Add(code);
            Debug.Log($"[PromoCode] Промокод {code} добавлен в список активированных");
        }
    }
    
    private bool IsPromoCodeActivated(string code)
    {
        if (!_sdkReady || YG2.saves == null || YG2.saves.activatedPromoCodes == null)
        {
            return false;
        }
        
        return YG2.saves.activatedPromoCodes.Contains(code);
    }
    
    private void ShowResult(string message, Color color)
    {
        if (resultText == null) return;
        
        resultText.text = message;
        resultText.color = color;
        resultText.gameObject.SetActive(true);
        
        if (messageCoroutine != null)
        {
            StopCoroutine(messageCoroutine);
        }
        messageCoroutine = StartCoroutine(HideResultAfterDelay());
    }
    
    private IEnumerator HideResultAfterDelay()
    {
        yield return new WaitForSeconds(resultDisplayTime);
        if (resultText != null)
        {
            resultText.gameObject.SetActive(false);
        }
    }
    
    private void ClearInput()
    {
        if (promoCodeInput != null)
        {
            promoCodeInput.text = "";
        }
        
        if (activateButton != null)
        {
            activateButton.interactable = false;
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    public void OpenPromoCodePanel()
    {
        if (!_isPromoSystemEnabled)
        {
            Debug.LogWarning("[PromoCode] Система промокодов отключена флагом!");
            return;
        }
        
        if (promoCodePanel != null)
        {
            promoCodePanel.SetActive(true);
            
            if (playerController != null)
            {
                playerController.OnUIOrAdOpened();
                playerController.DisableControl();
            }
            
            Debug.Log("[PromoCode] Панель открыта, управление заблокировано");
        }
    }
    
    public void ClosePromoCodePanel()
    {
        if (promoCodePanel != null)
        {
            promoCodePanel.SetActive(false);
            
            if (playerController != null)
            {
                playerController.OnUIOrAdClosed();
                playerController.EnableControl();
            }
            
            Debug.Log("[PromoCode] Панель закрыта, управление восстановлено");
        }
    }
    
    public void TogglePromoCodePanel()
    {
        if (promoCodePanel == null) return;
        
        if (promoCodePanel.activeSelf)
        {
            ClosePromoCodePanel();
        }
        else
        {
            OpenPromoCodePanel();
        }
    }
    
    public bool IsPromoSystemEnabled()
    {
        return _isPromoSystemEnabled;
    }
    
    public List<string> GetActivePromoCodes()
    {
        List<string> codes = new List<string>();
        foreach (var promo in allPromoCodes)
        {
            if (promo.isActive)
            {
                codes.Add(promo.code);
            }
        }
        return codes;
    }
    
    public void SetLanguage(bool isEnglish)
    {
        _isEnglish = isEnglish;
        _languageDetected = true;
        Debug.Log($"[PromoCode] Язык установлен вручную: {(isEnglish ? "Английский" : "Русский")}");
    }
    
    [ContextMenu("Сбросить все активированные промокоды")]
    public void ResetAllActivatedPromoCodes()
    {
        if (!_sdkReady) return;
        if (YG2.saves != null && YG2.saves.activatedPromoCodes != null)
        {
            YG2.saves.activatedPromoCodes.Clear();
            YG2.SaveProgress();
            Debug.Log("[PromoCode] Все активированные промокоды сброшены");
        }
    }
    
    [ContextMenu("Показать список активированных промокодов")]
    public void ShowActivatedPromoCodes()
    {
        if (!_sdkReady) return;
        if (YG2.saves != null && YG2.saves.activatedPromoCodes != null)
        {
            Debug.Log($"[PromoCode] Активированные промокоды ({YG2.saves.activatedPromoCodes.Count}):");
            foreach (var code in YG2.saves.activatedPromoCodes)
            {
                Debug.Log($"  - {code}");
            }
        }
    }
    
    [ContextMenu("Обновить промокоды из флагов")]
    public void RefreshPromoCodesFromFlags()
    {
        if (!_sdkReady) return;
        LoadPromoCodesFromFlags();
    }
    
    [ContextMenu("Показать текущие промокоды")]
    public void ShowCurrentPromoCodes()
    {
        Debug.Log($"[PromoCode] Текущие промокоды ({allPromoCodes.Count}):");
        foreach (var promo in allPromoCodes)
        {
            Debug.Log($"  {promo.code}: {promo.description}");
        }
    }
    
    [ContextMenu("Переопределить язык (для тестов)")]
    public void ToggleLanguageForTesting()
    {
        _isEnglish = !_isEnglish;
        Debug.Log($"[PromoCode] Язык переключён для теста: {(_isEnglish ? "Английский" : "Русский")}");
    }
}

[Serializable]
public class PromoCodeData
{
    public string code;
    public RewardType rewardType;
    public long coinAmount;
    public int petShopIndex;
    public int petTypeIndex;
    public string description;
    public bool isActive;
    public DateTime expirationDate;
}

public enum RewardType
{
    Coins,
    Pet
}

public static class SimpleJson
{
    public static Dictionary<string, object> Parse(string json)
    {
        var result = new Dictionary<string, object>();
        json = json.Trim();
        if (string.IsNullOrEmpty(json) || json[0] != '{' || json[^1] != '}')
            return result;

        int index = 1;
        int length = json.Length - 1;

        while (index < length)
        {
            // Пропускаем пробелы
            while (index < length && char.IsWhiteSpace(json[index])) index++;
            if (index >= length) break;

            // Парсим ключ
            if (json[index] != '"')
                throw new Exception($"Expected '\"' at position {index}");
            index++;
            int keyStart = index;
            while (index < length && json[index] != '"') index++;
            if (index >= length) throw new Exception("Unclosed string");
            string key = json.Substring(keyStart, index - keyStart);
            index++; // пропускаем закрывающую кавычку

            // Пропускаем пробелы
            while (index < length && char.IsWhiteSpace(json[index])) index++;
            if (index >= length || json[index] != ':')
                throw new Exception($"Expected ':' at position {index}");
            index++;

            // Пропускаем пробелы
            while (index < length && char.IsWhiteSpace(json[index])) index++;
            if (index >= length) break;

            // Парсим значение
            object value = ParseValue(json, ref index, length);
            result[key] = value;

            // Пропускаем пробелы
            while (index < length && char.IsWhiteSpace(json[index])) index++;
            if (index >= length) break;

            if (json[index] == ',')
            {
                index++;
                continue;
            }
            if (json[index] == '}')
                break;
        }
        return result;
    }

    private static object ParseValue(string json, ref int index, int length)
    {
        char c = json[index];
        if (c == '"')
        {
            // Строка
            index++;
            int start = index;
            while (index < length && json[index] != '"') index++;
            if (index >= length) throw new Exception("Unclosed string");
            string str = json.Substring(start, index - start);
            index++;
            return str;
        }
        else if (c == '{')
        {
            // Вложенный объект
            int objStart = index;
            int braceCount = 1;
            index++;
            while (index < length && braceCount > 0)
            {
                if (json[index] == '{') braceCount++;
                else if (json[index] == '}') braceCount--;
                index++;
            }
            string objJson = json.Substring(objStart, index - objStart);
            return Parse(objJson); // рекурсивно парсим
        }
        else if (c == '[')
        {
            // Массив (упрощённо, можно доработать)
            int arrStart = index;
            int bracketCount = 1;
            index++;
            while (index < length && bracketCount > 0)
            {
                if (json[index] == '[') bracketCount++;
                else if (json[index] == ']') bracketCount--;
                index++;
            }
            string arrJson = json.Substring(arrStart, index - arrStart);
            // Пока возвращаем как строку
            return arrJson;
        }
        else
        {
            // Число, булево, null
            int start = index;
            while (index < length && json[index] != ',' && json[index] != '}' && json[index] != ']' && !char.IsWhiteSpace(json[index]))
                index++;
            string token = json.Substring(start, index - start);
            if (token == "true") return true;
            if (token == "false") return false;
            if (token == "null") return null;
            if (long.TryParse(token, out long l)) return l;
            if (double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d)) return d;
            return token;
        }
    }
}
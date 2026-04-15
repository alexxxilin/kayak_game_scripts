using UnityEngine;
using UnityEngine.UI;
using YG;
using TMPro;
using System.Collections.Generic;

public class GameMenuManager : MonoBehaviour
{
    public static GameMenuManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private CanvasGroup menuCanvasGroup;
    [SerializeField] private Button openMenuButton;
    [SerializeField] private Button closeMenuButton;
    [SerializeField] private Button developerButton;

    [Header("Telegram Buttons List")]
    [Tooltip("Все кнопки в этом списке будут открывать Telegram-канал из поля World Telegram URL")]
    [SerializeField] private List<Button> telegramButtons = new List<Button>();
    
    [Header("World Telegram Reward System")]
    [SerializeField] private GameObject worldTelegramRewardPanel;
    [SerializeField] private Button worldClaimTelegramButton;
    [SerializeField] private TextMeshProUGUI worldTelegramStatusText;
    
    // 🔥 ИЗМЕНЕНО: вместо монет — питомец
    [Header("Telegram Reward: Pet Settings")]
    [SerializeField] private PetSystem petSystem; // 🔥 Ссылка на PetSystem
    [SerializeField] private int telegramRewardShopIndex = 0; // Индекс магазина питомцев
    [SerializeField] private int telegramRewardPetTypeIndex = 0; // Индекс питомца в магазине
    [SerializeField] private bool isTelegramRewardPetDonate = false; // Является ли питомец донатным
    
    [SerializeField] private string worldTelegramUrl = "https://t.me/RedFleetGames";
    
    [Header("Trigger Settings")]
    [Tooltip("Список объектов-триггеров для активации Telegram-награды")]
    [SerializeField] private List<GameObject> telegramTriggerObjects = new List<GameObject>();
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private string playerTag = "Player";

    private bool isWorldTelegramRewardClaimed = false;
    private KinematicCharacterController.Examples.ExampleCharacterController playerController;
    private bool isPlayerInTrigger = false;
    private List<TelegramTriggerComponent> triggerComponents = new List<TelegramTriggerComponent>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Initialize();
    }

    private void Start()
    {
        SetupTriggerObjects();
    }

    private void Initialize()
    {
        if (menuCanvasGroup == null && menuPanel != null)
        {
            menuCanvasGroup = menuPanel.GetComponent<CanvasGroup>();
            if (menuCanvasGroup == null)
                menuCanvasGroup = menuPanel.AddComponent<CanvasGroup>();
        }

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 0f;
            menuCanvasGroup.blocksRaycasts = false;
            menuCanvasGroup.interactable = false;
        }

        playerController = FindFirstObjectByType<KinematicCharacterController.Examples.ExampleCharacterController>();

        InitializeWorldTelegramReward();

        if (openMenuButton != null)
            openMenuButton.onClick.AddListener(OpenMenu);

        if (closeMenuButton != null)
            closeMenuButton.onClick.AddListener(CloseMenu);

        if (developerButton != null)
            developerButton.onClick.AddListener(YG2.OnDeveloperURL);

        if (worldClaimTelegramButton != null)
        {
            worldClaimTelegramButton.onClick.RemoveAllListeners();
            worldClaimTelegramButton.onClick.AddListener(OnWorldClaimTelegramButtonClick);
        }
        
        SetupTelegramButtons();
    }

    private void SetupTelegramButtons()
    {
        foreach (var button in telegramButtons)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OpenTelegramChannel);
                
                if (showDebugLogs)
                    Debug.Log($"[{gameObject.name}] Кнопка '{button.name}' настроена для открытия Telegram");
            }
        }
    }

    private void OpenTelegramChannel()
    {
        if (!string.IsNullOrEmpty(worldTelegramUrl))
        {
            YG2.OnURL(worldTelegramUrl);
            
            if (showDebugLogs)
                Debug.Log($"[{gameObject.name}] Открыт Telegram: {worldTelegramUrl}");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] World Telegram URL не задан!");
        }
    }

    private void SetupTriggerObjects()
    {
        // Очищаем старые компоненты при перезапуске
        foreach (var component in triggerComponents)
        {
            if (component != null)
                Destroy(component);
        }
        triggerComponents.Clear();

        if (telegramTriggerObjects == null || telegramTriggerObjects.Count == 0)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[{gameObject.name}] Список Telegram Trigger Objects пуст!");
            return;
        }

        foreach (var triggerObject in telegramTriggerObjects)
        {
            if (triggerObject == null)
            {
                if (showDebugLogs)
                    Debug.LogWarning($"[{gameObject.name}] Один из объектов в списке триггеров равен null!");
                continue;
            }

            var triggerComponent = triggerObject.GetComponent<TelegramTriggerComponent>();
            if (triggerComponent == null)
            {
                triggerComponent = triggerObject.AddComponent<TelegramTriggerComponent>();
                if (showDebugLogs)
                    Debug.Log($"[{gameObject.name}] Компонент триггера добавлен к объекту: {triggerObject.name}");
            }

            triggerComponent.Initialize(this, playerTag, showDebugLogs);
            triggerComponents.Add(triggerComponent);

            // Проверка коллайдера
            Collider collider = triggerObject.GetComponent<Collider>();
            if (collider == null)
            {
                Collider2D collider2D = triggerObject.GetComponent<Collider2D>();
                if (collider2D == null)
                {
                    Debug.LogError($"[{gameObject.name}] У объекта {triggerObject.name} нет коллайдера!");
                }
                else if (!collider2D.isTrigger)
                {
                    Debug.LogWarning($"[{gameObject.name}] У объекта {triggerObject.name} коллайдер 2D не является триггером!");
                }
            }
            else if (!collider.isTrigger)
            {
                Debug.LogWarning($"[{gameObject.name}] У объекта {triggerObject.name} коллайдер 3D не является триггером!");
            }
        }

        if (showDebugLogs)
            Debug.Log($"[{gameObject.name}] Настроено {triggerComponents.Count} триггеров для Telegram-награды");
    }

    private void InitializeWorldTelegramReward()
    {
        LoadWorldTelegramRewardState();

        if (worldTelegramRewardPanel != null)
        {
            worldTelegramRewardPanel.SetActive(false);
        }

        if (worldTelegramStatusText != null)
        {
            worldTelegramStatusText.gameObject.SetActive(false);
        }

        UpdateWorldTelegramRewardUI();
    }

    private void LoadWorldTelegramRewardState()
    {
        if (YG2.saves != null)
        {
            isWorldTelegramRewardClaimed = YG2.saves.worldTelegramRewardClaimed;
        }
        else
        {
            isWorldTelegramRewardClaimed = PlayerPrefs.GetInt("WorldTelegramRewardClaimed", 0) == 1;
        }

        if (showDebugLogs)
            Debug.Log($"[{gameObject.name}] Загружено состояние: {(isWorldTelegramRewardClaimed ? "уже получено" : "доступно")}");
    }

    private void SaveWorldTelegramRewardState()
    {
        if (YG2.saves != null)
        {
            YG2.saves.worldTelegramRewardClaimed = isWorldTelegramRewardClaimed;
            YG2.SaveProgress();
        }
        else
        {
            PlayerPrefs.SetInt("WorldTelegramRewardClaimed", isWorldTelegramRewardClaimed ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (showDebugLogs)
            Debug.Log($"[{gameObject.name}] Сохранено состояние");
    }

    private void UpdateWorldTelegramRewardUI()
    {
        if (worldTelegramStatusText != null)
        {
            worldTelegramStatusText.gameObject.SetActive(isWorldTelegramRewardClaimed);
        }
    }

    public void OnPlayerEnteredTrigger()
    {
        if (showDebugLogs)
            Debug.Log($"[{gameObject.name}] Игрок вошел в триггер");
        
        isPlayerInTrigger = true;
        
        if (worldTelegramRewardPanel != null)
        {
            worldTelegramRewardPanel.SetActive(true);
            UpdateWorldTelegramRewardUI();
            ShowCursor();
        }
    }

    public void OnPlayerExitedTrigger()
    {
        if (showDebugLogs)
            Debug.Log($"[{gameObject.name}] Игрок вышел из триггера");
        
        isPlayerInTrigger = false;
        
        if (worldTelegramRewardPanel != null)
        {
            worldTelegramRewardPanel.SetActive(false);
            HideCursor();
        }
    }

    private void OnWorldClaimTelegramButtonClick()
    {
        // Всегда открываем Telegram
        OpenTelegramChannel();
        
        if (isWorldTelegramRewardClaimed)
        {
            if (showDebugLogs)
                Debug.Log($"[{gameObject.name}] Награда уже была получена");
            return;
        }

        // 🔥 ВЫДАЧА ПИТОМЦА ВМЕСТО МОНЕТ
        if (petSystem != null)
        {
            // Проверка валидности индексов
            if (telegramRewardShopIndex >= 0 && telegramRewardShopIndex < petSystem.petShops.Count &&
                telegramRewardPetTypeIndex >= 0 && 
                telegramRewardPetTypeIndex < petSystem.petShops[telegramRewardShopIndex].pet3DPrefabs.Count)
            {
                var newPet = new PetSystem.PetInstance
                {
                    id = petSystem.GetNextPetId(),
                    shopIndex = telegramRewardShopIndex,
                    petTypeIndex = telegramRewardPetTypeIndex
                };
                newPet.SetDonateStatus(isTelegramRewardPetDonate);
                
                petSystem.AddPetFromExternal(newPet);
                
                Debug.Log($"[{gameObject.name}] Выдан питомец: магазин[{telegramRewardShopIndex}], тип[{telegramRewardPetTypeIndex}]");
            }
            else
            {
                Debug.LogError($"[{gameObject.name}] Неверные индексы для питомца-награды! Shop:{telegramRewardShopIndex}, Pet:{telegramRewardPetTypeIndex}");
            }
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] PetSystem не назначен! Награда не выдана.");
        }

        // Помечаем награду как полученную
        isWorldTelegramRewardClaimed = true;
        SaveWorldTelegramRewardState();
        UpdateWorldTelegramRewardUI();
    }

    public void OpenMenu()
    {
        if (menuCanvasGroup == null) return;
        
        menuCanvasGroup.alpha = 1f;
        menuCanvasGroup.blocksRaycasts = true;
        menuCanvasGroup.interactable = true;
        ShowCursor();
    }

    public void CloseMenu()
    {
        if (menuCanvasGroup == null) return;
        
        menuCanvasGroup.alpha = 0f;
        menuCanvasGroup.blocksRaycasts = false;
        menuCanvasGroup.interactable = false;
        HideCursor();
    }

    public void OpenDeveloperPage()
    {
        #if OpenURL_yg
        YG2.OnDeveloperURL();
        #endif
        CloseMenu();
    }

    public void OpenGameById(int gameId)
    {
        #if OpenURL_yg
        YG2.OnGameURL(gameId);
        #endif
        CloseMenu();
    }

    public void AddTelegramButton(Button newButton)
    {
        if (newButton != null && !telegramButtons.Contains(newButton))
        {
            telegramButtons.Add(newButton);
            newButton.onClick.RemoveAllListeners();
            newButton.onClick.AddListener(OpenTelegramChannel);
            
            if (showDebugLogs)
                Debug.Log($"[{gameObject.name}] Добавлена новая Telegram-кнопка: {newButton.name}");
        }
    }

    public void RemoveTelegramButton(Button buttonToRemove)
    {
        if (buttonToRemove != null && telegramButtons.Contains(buttonToRemove))
        {
            buttonToRemove.onClick.RemoveListener(OpenTelegramChannel);
            telegramButtons.Remove(buttonToRemove);
            
            if (showDebugLogs)
                Debug.Log($"[{gameObject.name}] Удалена Telegram-кнопка: {buttonToRemove.name}");
        }
    }

    // Методы для работы со списком триггеров
    public void AddTelegramTriggerObject(GameObject newTrigger)
    {
        if (newTrigger != null && !telegramTriggerObjects.Contains(newTrigger))
        {
            telegramTriggerObjects.Add(newTrigger);
            
            var triggerComponent = newTrigger.GetComponent<TelegramTriggerComponent>();
            if (triggerComponent == null)
            {
                triggerComponent = newTrigger.AddComponent<TelegramTriggerComponent>();
            }
            triggerComponent.Initialize(this, playerTag, showDebugLogs);
            triggerComponents.Add(triggerComponent);
            
            if (showDebugLogs)
                Debug.Log($"[{gameObject.name}] Добавлен новый триггер: {newTrigger.name}");
        }
    }

    public void RemoveTelegramTriggerObject(GameObject triggerToRemove)
    {
        if (triggerToRemove != null && telegramTriggerObjects.Contains(triggerToRemove))
        {
            var component = triggerToRemove.GetComponent<TelegramTriggerComponent>();
            if (component != null)
            {
                triggerComponents.Remove(component);
                Destroy(component);
            }
            telegramTriggerObjects.Remove(triggerToRemove);
            
            if (showDebugLogs)
                Debug.Log($"[{gameObject.name}] Удалён триггер: {triggerToRemove.name}");
        }
    }

    public List<GameObject> GetTelegramTriggerObjects()
    {
        return telegramTriggerObjects;
    }

    private void ShowCursor()
    {
        if (playerController != null)
        {
            playerController.OnUIOrAdOpened();
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void HideCursor()
    {
        if (playerController != null)
        {
            playerController.OnUIOrAdClosed();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public bool IsWorldTelegramRewardClaimed()
    {
        return isWorldTelegramRewardClaimed;
    }

    public List<Button> GetTelegramButtons()
    {
        return telegramButtons;
    }

    [ContextMenu("Сбросить World Telegram награду")]
    public void ResetWorldTelegramReward()
    {
        isWorldTelegramRewardClaimed = false;
        SaveWorldTelegramRewardState();
        UpdateWorldTelegramRewardUI();
        Debug.Log($"[{gameObject.name}] Награда сброшена");
    }

    [ContextMenu("Обновить все Telegram-кнопки")]
    public void RefreshTelegramButtons()
    {
        SetupTelegramButtons();
        Debug.Log($"[{gameObject.name}] Обновлено {telegramButtons.Count} Telegram-кнопок");
    }

    [ContextMenu("Перенастроить все триггеры")]
    public void RefreshTelegramTriggers()
    {
        SetupTriggerObjects();
    }

    private void OnDestroy()
    {
        if (openMenuButton != null) openMenuButton.onClick.RemoveListener(OpenMenu);
        if (closeMenuButton != null) closeMenuButton.onClick.RemoveListener(CloseMenu);
        if (worldClaimTelegramButton != null) worldClaimTelegramButton.onClick.RemoveListener(OnWorldClaimTelegramButtonClick);
        
        foreach (var button in telegramButtons)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OpenTelegramChannel);
            }
        }

        // Очистка компонентов триггеров
        foreach (var component in triggerComponents)
        {
            if (component != null)
                Destroy(component);
        }
        triggerComponents.Clear();
    }
}

[RequireComponent(typeof(Collider))]
public class TelegramTriggerComponent : MonoBehaviour
{
    private GameMenuManager gameMenuManager;
    private string playerTag = "Player";
    private bool showLogs = true;

    public void Initialize(GameMenuManager manager, string tag, bool debugLogs)
    {
        gameMenuManager = manager;
        playerTag = tag;
        showLogs = debugLogs;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            if (showLogs)
                Debug.Log($"[TelegramTrigger] Игрок вошел: {gameObject.name}");
            gameMenuManager?.OnPlayerEnteredTrigger();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            if (showLogs)
                Debug.Log($"[TelegramTrigger] Игрок вышел: {gameObject.name}");
            gameMenuManager?.OnPlayerExitedTrigger();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            if (showLogs)
                Debug.Log($"[TelegramTrigger] Игрок вошел (2D): {gameObject.name}");
            gameMenuManager?.OnPlayerEnteredTrigger();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            if (showLogs)
                Debug.Log($"[TelegramTrigger] Игрок вышел (2D): {gameObject.name}");
            gameMenuManager?.OnPlayerExitedTrigger();
        }
    }
}
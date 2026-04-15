using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using KinematicCharacterController.Examples;
using YG;

[System.Serializable]
public class WingLevel
{
    public string wingName = "Крылья";
    [Tooltip("Цена крыла (поддерживает до 9 квадриллионов)")]
    public double price = 1000;
    public float speedMultiplier = 1.5f;
    public Sprite icon;
    public GameObject wing3DModel;

    [Tooltip("ID лестницы, на которой крылья дают ускорение. Крылья работают ТОЛЬКО на своей локации (точное совпадение ID).")]
    public int requiredLadderId = 0;
}

public class WingsSystem : MonoBehaviour
{
    [Header("Данные крыльев")]
    public List<WingLevel> wingLevels = new List<WingLevel>();

    [Header("UI Магазина")]
    public GameObject shopPanel;
    public Transform offersContainer;
    public GameObject offerPrefab;
    
    [Tooltip("Префаб пустого слота-разделителя (опционально, если не назначен — создаётся программно)")]
    public GameObject emptySlotPrefab;

    [Tooltip("Индексы крыльев, ПОСЛЕ которых добавляются пустые слоты-разделители (по умолчанию: после 11-го и 22-го)")]
    [SerializeField] private List<int> dividerAfterWingIndices = new List<int> { 10, 21 };
    
    [Tooltip("Количество пустых слотов-разделителей в каждой группе")]
    [Range(1, 10)]
    public int emptySlotsCount = 4;

    public Button actionButton;
    public TMP_Text actionButtonText;

    public Image selectedWingIcon;
    public TMP_Text selectedWingPowerText;

    public Image buyModeIndicator;

    public Button closeShopButton;

    [Header("Слот экипировки")]
    public Transform wingSlot;

    [Header("UI Предупреждения")]
    [Tooltip("Текст, показываемый когда крылья не подходят для текущей лестницы")]
    public TMP_Text incompatibleWingsText;

    [Header("Триггеры")]
    public List<Collider> shopTriggers = new List<Collider>();

    public int purchasedWingIndex { get; private set; } = -1;
    public int equippedWingIndex { get; private set; } = -1;
    public int selectedWingIndex { get; private set; } = -1;

    private ExampleCharacterController playerController;
    private bool isInShopArea = false;
    private GameObject currentWingObject = null;
    private int _currentLadderId = -1;

    void Start()
    {
        playerController = FindFirstObjectByType<ExampleCharacterController>();
        
        // Ждем инициализации SDK перед загрузкой данных крыльев
        StartCoroutine(InitializeWingsSystem());
    }

    private System.Collections.IEnumerator InitializeWingsSystem()
    {
        // Ждем пока SDK не будет инициализирован
        while (!YG2InitializationManager.CanAccessSaves())
        {
            yield return null;
        }

        LoadWingsData();

        // 🔑 Инициализация первых крыльев если это новая игра
        if (purchasedWingIndex < 0 && wingLevels.Count > 0)
        {
            purchasedWingIndex = 0;
            equippedWingIndex = 0;
            selectedWingIndex = 0;
            SaveWingsData();
        }

        if (closeShopButton != null)
            closeShopButton.onClick.AddListener(CloseShop);

        if (actionButton != null)
            actionButton.onClick.AddListener(OnActionButtonClicked);

        SetupTriggers();
        UpdateShopUI();
        ApplyEquippedWings();
        
        UpdateIncompatibleWingsText(false);
    }

    void SetupTriggers()
    {
        foreach (var col in shopTriggers)
        {
            if (col == null) continue;
            var trigger = col.gameObject.GetComponent<WingsShopTrigger>();
            if (trigger == null)
                trigger = col.gameObject.AddComponent<WingsShopTrigger>();
            trigger.Initialize(this);
        }
    }

    public void OpenShop()
    {
        if (shopPanel == null) return;
        shopPanel.SetActive(true);
        isInShopArea = true;
        playerController?.OnUIOrAdOpened();
        UpdateShopUI();
    }

    public void CloseShop()
    {
        if (shopPanel == null) return;
        shopPanel.SetActive(false);
        isInShopArea = false;
        playerController?.OnUIOrAdClosed();
    }

    private T FindComponentOnChildWithName<T>(Transform root, string name) where T : Component
    {
        var children = root.GetComponentsInChildren<Transform>(true);
        foreach (var child in children)
        {
            if (child.name == name)
            {
                return child.GetComponent<T>();
            }
        }
        return null;
    }

    void UpdateShopUI()
    {
        if (offersContainer == null || offerPrefab == null) return;

        // Очищаем старые элементы
        foreach (Transform child in offersContainer)
            Destroy(child.gameObject);

        // 🔹 Рендерим крылья и вставляем пустые слоты после указанных индексов
        for (int i = 0; i < wingLevels.Count; i++)
        {
            if (wingLevels[i] == null) continue;

            var offer = Instantiate(offerPrefab, offersContainer);
            SetupWingOffer(offer, i);
            
            // 🔹 После нужных индексов добавляем пустые слоты-разделители
            if (dividerAfterWingIndices.Contains(i))
            {
                AddEmptyDividerSlots();
            }
        }

        UpdateActionUI();
        UpdateSelectedWingDisplay();
    }

    /// <summary>
    /// Настраивает визуал одного предложения крыла в магазине
    /// </summary>
    private void SetupWingOffer(GameObject offer, int wingIndex)
    {
        var iconImage = FindComponentOnChildWithName<Image>(offer.transform, "WingIcon");
        var lockedOverlay = FindComponentOnChildWithName<Image>(offer.transform, "LockedOverlay");
        var selectedOverlay = FindComponentOnChildWithName<Image>(offer.transform, "SelectedOverlay");

        if (iconImage != null && wingLevels[wingIndex].icon != null)
            iconImage.sprite = wingLevels[wingIndex].icon;

        bool isUnlocked = CanBuyWing(wingIndex) || wingIndex <= purchasedWingIndex;
        if (lockedOverlay != null)
            lockedOverlay.gameObject.SetActive(!isUnlocked);

        bool isSelected = wingIndex == selectedWingIndex;
        if (selectedOverlay != null)
            selectedOverlay.gameObject.SetActive(isSelected);

        var button = offer.GetComponent<Button>();
        if (button == null)
            button = offer.gameObject.AddComponent<Button>();
        
        int index = wingIndex;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SelectWing(index));
        
        // Делаем кнопку интерактивной
        button.interactable = true;
        var offerImage = offer.GetComponent<Image>();
        if (offerImage != null)
            offerImage.raycastTarget = true;
    }

    /// <summary>
    /// Добавляет пустые слоты-разделители в сетку магазина
    /// </summary>
    private void AddEmptyDividerSlots()
    {
        for (int i = 0; i < emptySlotsCount; i++)
        {
            GameObject emptySlot;
            
            if (emptySlotPrefab != null)
            {
                // Используем назначенный префаб, если есть
                emptySlot = Instantiate(emptySlotPrefab, offersContainer);
            }
            else
            {
                // Создаём программно: пустой объект с фоновой панелью
                emptySlot = new GameObject($"EmptySlot_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}");
                emptySlot.transform.SetParent(offersContainer, false);
                
                // Добавляем RectTransform для корректного отображения в Grid
                var rectTransform = emptySlot.AddComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.zero;
                rectTransform.sizeDelta = new Vector2(100, 100); // Подстройте под размер вашей сетки
                
                // Добавляем фоновую панель (полностью прозрачная)
                var image = emptySlot.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f); // ✅ Изменено на 0 (полная прозрачность)
                image.raycastTarget = false;
            }
            
            // 🔹 Важно: пустые слоты НЕ должны быть интерактивными
            var slotButton = emptySlot.GetComponent<Button>();
            if (slotButton != null)
            {
                slotButton.interactable = false;
                if (slotButton.targetGraphic != null)
                    slotButton.targetGraphic.raycastTarget = false;
            }
            
            // Отключаем raycastTarget у Image компонента
            var slotImage = emptySlot.GetComponent<Image>();
            if (slotImage != null)
                slotImage.raycastTarget = false;
        }
    }

    void SelectWing(int index)
    {
        if (index < 0 || index >= wingLevels.Count) return;
        if (!CanBuyWing(index) && index > purchasedWingIndex) return;

        selectedWingIndex = index;
        UpdateShopUI();
    }

    void OnActionButtonClicked()
    {
        if (selectedWingIndex < 0 || selectedWingIndex >= wingLevels.Count) return;

        if (selectedWingIndex <= purchasedWingIndex)
        {
            // 🔑 Нельзя снять крылья — только переключить на другие
            // Если выбрано уже надетое крыло — ничего не делаем (кнопка неактивна)
            if (selectedWingIndex == equippedWingIndex)
            {
                return;
            }
            else
            {
                EquipWing(selectedWingIndex);
            }
        }
        else if (CanBuyWing(selectedWingIndex))
        {
            BuyWing(selectedWingIndex);
        }
    }

    bool CanBuyWing(int index)
    {
        if (index == 0) return true;
        return index == purchasedWingIndex + 1;
    }

    void BuyWing(int index)
    {
        if (!CanBuyWing(index)) return;
        if (playerController == null) return;
        
        if (playerController.CoinsCollected < wingLevels[index].price) return;

        playerController.SpendCoins(wingLevels[index].price);
        purchasedWingIndex = index;
        EquipWing(index);
        
        // 🔑 Авто-переключение селектора на следующее крыло после покупки
        if (index + 1 < wingLevels.Count)
        {
            selectedWingIndex = index + 1;
        }
        
        SaveWingsData();
        UpdateShopUI();
    }

    void EquipWing(int index)
    {
        if (index > purchasedWingIndex) return;
        equippedWingIndex = index;
        ApplyEquippedWings();
        SaveWingsData();
        UpdateShopUI();
    }

    void UnequipWings()
    {
        // 🔑 Метод оставлен для совместимости, но больше не вызывается из UI
        equippedWingIndex = -1;
        ApplyEquippedWings();
        SaveWingsData();
        UpdateShopUI();
    }

    public void ApplyEquippedWings()
    {
        if (currentWingObject != null)
        {
            Destroy(currentWingObject);
            currentWingObject = null;
        }

        float multiplier = 1f;
        bool wingsAreIncompatible = false;

        if (playerController != null && playerController.CurrentCharacterState == CharacterState.Climbing)
        {
            var currentLadder = playerController.CurrentLadder as LadderZone;
            if (currentLadder != null && equippedWingIndex >= 0 && equippedWingIndex < wingLevels.Count)
            {
                if (wingLevels[equippedWingIndex] == null)
                {
                    equippedWingIndex = -1;
                    SaveWingsData();
                    return;
                }

                var wing = wingLevels[equippedWingIndex];
                if (wing.requiredLadderId == currentLadder.LadderId)
                {
                    multiplier = wing.speedMultiplier;
                    wingsAreIncompatible = false;
                }
                else
                {
                    multiplier = 1f;
                    wingsAreIncompatible = true;
                }

                if (wingSlot != null && wing.wing3DModel != null)
                {
                    currentWingObject = Instantiate(
                        wing.wing3DModel,
                        wingSlot.position,
                        wingSlot.rotation,
                        wingSlot
                    );
                }
            }
        }
        else if (equippedWingIndex >= 0 && equippedWingIndex < wingLevels.Count && wingSlot != null && wingLevels[equippedWingIndex].wing3DModel != null)
        {
            if (wingLevels[equippedWingIndex] == null) return;

            currentWingObject = Instantiate(
                wingLevels[equippedWingIndex].wing3DModel,
                wingSlot.position,
                wingSlot.rotation,
                wingSlot
            );
            multiplier = 1f;
            wingsAreIncompatible = false;
        }

        if (playerController != null)
        {
            playerController.SetWingsSpeedMultiplier(multiplier);
        }

        UpdateIncompatibleWingsText(wingsAreIncompatible);
    }

    public void OnLadderChanged(LadderZone newLadder)
    {
        bool wingsAreIncompatible = false;

        if (newLadder != null && equippedWingIndex >= 0 && equippedWingIndex < wingLevels.Count)
        {
            if (wingLevels[equippedWingIndex] == null)
            {
                equippedWingIndex = -1;
                SaveWingsData();
                UpdateIncompatibleWingsText(false);
                return;
            }

            var wing = wingLevels[equippedWingIndex];
            wingsAreIncompatible = (wing.requiredLadderId != newLadder.LadderId);
        }

        UpdateIncompatibleWingsText(wingsAreIncompatible);
    }

    private void UpdateIncompatibleWingsText(bool show)
    {
        if (incompatibleWingsText == null)
        {
            if (!show && !_debugNullTextLogged)
            {
                Debug.LogWarning("WingsSystem: поле Incompatible Wings Text не назначено в инспекторе.");
                _debugNullTextLogged = true;
            }
            return;
        }

        incompatibleWingsText.gameObject.SetActive(show);
        if (show)
        {
            incompatibleWingsText.text = YG2.lang == "en" 
                ? "The boat doesn't fit the ladder!" 
                : "Лодка не соответствует лестнице!";
        }
    }
    private bool _debugNullTextLogged = false;

    void UpdateActionUI()
    {
        bool isValidSelection = selectedWingIndex >= 0 && selectedWingIndex < wingLevels.Count;

        if (actionButton != null)
            actionButton.gameObject.SetActive(isValidSelection);

        if (buyModeIndicator != null)
            buyModeIndicator.gameObject.SetActive(false);

        if (!isValidSelection) return;

        string buttonText = "";
        bool interactable = true;
        bool isBuyMode = false;

        // 🔑 Изменено: вместо "Снять" теперь "Надето" (кнопка неактивна)
        if (selectedWingIndex == equippedWingIndex)
        {
            buttonText = YG2.lang == "en" ? "Equipped" : "Надето";
            interactable = false;
        }
        else if (selectedWingIndex <= purchasedWingIndex)
        {
            buttonText = YG2.lang == "en" ? "Equip" : "Надеть";
        }
        else if (CanBuyWing(selectedWingIndex))
        {
            if (wingLevels[selectedWingIndex] == null)
            {
                buttonText = "Error";
                interactable = false;
            }
            else
            {
                buttonText = FormatNumber(wingLevels[selectedWingIndex].price);
                isBuyMode = true;
            }
        }
        else
        {
            buttonText = YG2.lang == "en" ? "Unavailable" : "Недоступно";
            interactable = false;
        }

        if (actionButtonText != null)
            actionButtonText.text = buttonText;

        if (actionButton != null)
            actionButton.interactable = interactable;

        if (buyModeIndicator != null)
            buyModeIndicator.gameObject.SetActive(isBuyMode);
    }

    void UpdateSelectedWingDisplay()
    {
        bool isValidSelection = selectedWingIndex >= 0 && selectedWingIndex < wingLevels.Count;

        if (selectedWingIcon != null)
            selectedWingIcon.gameObject.SetActive(isValidSelection);

        if (selectedWingPowerText != null)
            selectedWingPowerText.gameObject.SetActive(isValidSelection);

        if (!isValidSelection) return;

        if (wingLevels[selectedWingIndex] == null) return;

        var wing = wingLevels[selectedWingIndex];
        if (selectedWingIcon != null)
        {
            selectedWingIcon.sprite = wing.icon;
            selectedWingIcon.gameObject.SetActive(wing.icon != null);
        }

        if (selectedWingPowerText != null)
        {
            selectedWingPowerText.text = $"×{wing.speedMultiplier:F1}";
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

    void SaveWingsData()
    {
        if (YG2.saves == null) return;
        YG2.saves.purchasedWingIndex = purchasedWingIndex;
        YG2.saves.equippedWingIndex = equippedWingIndex;
        YG2.SaveProgress();
    }

    void LoadWingsData()
    {
        if (YG2.saves == null) return;
        purchasedWingIndex = YG2.saves.purchasedWingIndex;
        equippedWingIndex = YG2.saves.equippedWingIndex;
        selectedWingIndex = equippedWingIndex;
    }

    public void OnPlayerEnter() => OpenShop();
    public void OnPlayerExit() => CloseShop();
    
    #if UNITY_EDITOR
    [ContextMenu("Сбросить разделители к значениям по умолчанию")]
    private void ResetDividerIndices()
    {
        dividerAfterWingIndices = new List<int> { 10, 21 };
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[WingsSystem] Разделители сброшены: после 11-го и 22-го крыла");
    }
    #endif
}

public class WingsShopTrigger : MonoBehaviour
{
    private WingsSystem wingsSystem;

    public void Initialize(WingsSystem system)
    {
        wingsSystem = system;
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            wingsSystem.OnPlayerEnter();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            wingsSystem.OnPlayerExit();
    }
}
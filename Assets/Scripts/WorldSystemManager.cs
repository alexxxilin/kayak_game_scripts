using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using KinematicCharacterController.Examples;
using YG;
// Компонент для триггеров телепорта
public class TeleportTriggerComponent : MonoBehaviour
{
    public WorldSystemManager Manager;
    public string TriggerID;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && Manager != null)
        {
            Manager.OnTeleportTriggerEnter(TriggerID);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && Manager != null)
        {
            Manager.OnTeleportTriggerExit(TriggerID);
        }
    }
}
[Serializable]
public class TeleportUIPanel
{
    [Tooltip("Небольшая панель телепорта")]
    public GameObject Panel;
    [Tooltip("Кнопка телепорта на этой панели")]
    public Button TeleportButton;
    [Tooltip("ID локации для телепорта")]
    public string TargetLocationID;
    [Tooltip("ID телепорт-триггера, который разблокирует эту панель при нажатии (необязательно)")]
    public string UnlockTriggerID;
    [Tooltip("Показывать ли панель изначально")]
    public bool ShowByDefault = false;
    [HideInInspector]
    public bool WasUnlocked = false;
}
[Serializable]
public class AltitudeBar
{
    [Tooltip("Ссылка на объект полосы высоты")]
    public GameObject BarObject;
    [Tooltip("Ссылка на изображение стрелки")]
    public Image ArrowImage;
    [Tooltip("Ссылка на текст текущей высоты")]
    public Text HeightText;
    [Tooltip("Ссылка на текст диапазона полосы")]
    public Text RangeText;
    [Tooltip("Минимальная высота для этой полосы")]
    public float MinHeight = 0f;
    [Tooltip("Максимальная высота для этой полосы")]
    public float MaxHeight = 10000f;
}
[Serializable]
public class TeleportTrigger
{
    [Tooltip("ID телепорта")]
    public string TriggerID;
    [Tooltip("Коллайдер телепорта (должен быть триггером)")]
    public Collider TeleportCollider;
    [Tooltip("UI панель телепорта")]
    public GameObject TeleportPanel;
    [Tooltip("Текст статуса разблокировки")]
    public Text UnlockStatusText;
    [Tooltip("Текст стоимости телепорта")]
    public Text CostText;
    [Tooltip("Кнопка телепорта")]
    public Button TeleportButton;
    [Tooltip("Кнопка закрытия панели")]
    public Button CloseButton;
    [Tooltip("Требуемая высота для разблокировки")]
    public float RequiredHeight = 10000f;
    [Tooltip("Стоимость телепорта в кубках")]
    public int TrophyCost = 1;
    [Tooltip("ID целевой локации для телепорта")]
    public string TargetLocationID;
    [HideInInspector]
    public bool IsInitialized = false;
    [HideInInspector]
    public bool WasBought = false;
}
[Serializable]
public class LocationSettings
{
    [Tooltip("ID локации")]
    public string LocationID;
    [Tooltip("Название локации")]
    public string LocationName = "Базовая локация";
    [Tooltip("Точка спавна на локации")]
    public Vector3 SpawnPosition = Vector3.zero;
    [Tooltip("Базовая виртуальная высота локации")]
    public float BaseVirtualHeight = 0f;
    [Tooltip("Индекс полосы высоты, которая должна отображаться на этой локации")]
    public int DisplayBarIndex = 0;
    [Header("Приоритетный скайбокс локации")]
    [Tooltip("Скайбокс с высшим приоритетом для этой локации. Включается в радиусе 10 метров от точки спавна")]
    public Material PrioritySkyboxMaterial;
    [Tooltip("Радиус действия приоритетного скайбокса (метры)")]
    public float PrioritySkyboxRadius = 10f;
}
[Serializable]
public class SkyboxLayer
{
    [Tooltip("Минимальная высота для этого скайбокса")]
    public float MinHeight = 0f;
    [Tooltip("Максимальная высота для этого скайбокса")]
    public float MaxHeight = 100f;
    [Tooltip("Материал скайбокса")]
    public Material SkyboxMaterial;
    [Tooltip("Если включено — для этого диапазона высот будет использоваться приоритетный скайбокс текущей локации")]
    public bool UseLocationSkybox = false;
}
public class WorldSystemManager : MonoBehaviour
{
    [Header("UI Система телепортов")]
    public Button OpenTeleportUIButton;
    public GameObject TeleportUIPanel;
    public Button CloseTeleportUIButton;
    public ScrollRect TeleportScrollView;
    public List<TeleportUIPanel> TeleportUIPanels = new List<TeleportUIPanel>();
    [Header("Система высотных полос")]
    public List<AltitudeBar> AltitudeBars = new List<AltitudeBar>();
    public float MaxBarHeight = 10000f;
    [Header("Система телепортов")]
    public List<TeleportTrigger> TeleportTriggers = new List<TeleportTrigger>();
    [Header("Система локаций")]
    public List<LocationSettings> LocationSettingsList = new List<LocationSettings>();
    public string CurrentLocationID = "base_location";
    public Text LocationNameText;
    [Header("Система кубков")]
    public int TrophiesCollected = 0;
    public Text TrophyCounterText;
    // === ПОЛЯ ДЛЯ АНИМАЦИИ КУБКОВ ===
    [Header("Анимация получения кубков")]
    [Tooltip("Префаб спрайта кубка для анимации (должен содержать TrophyFlyAnimation компонент)")]
    public GameObject TrophyFlyPrefab;
    [Tooltip("Канвас, на котором будут отображаться летящие кубки (обычно Screen Space - Overlay)")]
    public Canvas TrophyFlyCanvas;
    [Tooltip("Трансформ элемента счётчика кубков (для определения конечной точки полёта)")]
    public RectTransform TrophyCounterTransform;
    [Tooltip("Длительность полёта одного кубка (секунды)")]
    public float TrophyFlyDuration = 0.8f;
    [Tooltip("Задержка между полётами нескольких кубков (секунды)")]
    public float TrophyFlyDelay = 0.15f;
    [Tooltip("Высота дуги полёта относительно расстояния между точками (0.3 = 30% от расстояния)")]
    public float FlyArcHeightMultiplier = 0.4f;
    [Tooltip("Размер спрайта кубка в пикселях")]
    public Vector2 TrophySpriteSize = new Vector2(60, 60);
    // Пул объектов для оптимизации
    private Queue<GameObject> _trophyPool = new Queue<GameObject>();
    private const int POOL_SIZE = 10;
    [Header("Настройки скайбоксов")]
    public List<SkyboxLayer> SkyboxLayers = new List<SkyboxLayer>();
    [Header("Скайбокс для освещения")]
    public Material LightingSkyboxMaterial;
    [Header("Ссылки")]
    public ExampleCharacterController CharacterController;
    // === ССЫЛКА НА СИСТЕМУ СОХРАНЕНИЯ ===
    [Header("Система сохранения")]
    [Tooltip("Ссылка на менеджер сохранений для облачного сохранения после покупки телепорта")]
    public SaveManager SaveManager;
    private int _currentSkyboxIndex = -1;
    private GameObject _currentActivePanel = null;
    private TeleportTrigger _currentTeleport;
    private HashSet<string> _activeTriggers = new HashSet<string>();
    private float _currentVirtualHeight = 0f;
    private int _currentBarIndex = 0;
    private int _locationDisplayBarIndex = 0;
    private float _maxAchievedVirtualHeight = 0f;
    private bool _isTeleportUIOpen = false;
    private bool _teleportUIOpenedFromTrigger = false; // 🔑 Флаг: панель открыта через триггер
    private Material _currentPrioritySkybox = null;
    private bool _isPrioritySkyboxActive = false;
    private bool _isUsingLocationSkybox = false;
    private void Awake()
    {
        if (CharacterController == null)
        {
            CharacterController = FindFirstObjectByType<ExampleCharacterController>();
        }
        if (Camera.main != null && Camera.main.GetComponent<Skybox>() == null)
        {
            Camera.main.gameObject.AddComponent<Skybox>();
        }
        // Инициализация пула ТОЛЬКО в режиме игры
        if (Application.isPlaying)
        {
            InitializeTrophyPool();
        }
        InitializeTeleportUI();
        InitializeTeleports();
        InitializeAltitudeBars();
        DisableDefaultLightingSkybox();
        if (LightingSkyboxMaterial != null)
        {
            RenderSettings.skybox = LightingSkyboxMaterial;
            DynamicGI.UpdateEnvironment();
            Debug.Log($"[Skybox] Установлен скайбокс для освещения (Environment Lighting): '{LightingSkyboxMaterial.name}'");
        }
        LocationSettings currentLocation = GetLocationSettings(CurrentLocationID);
        if (currentLocation != null)
        {
            _currentVirtualHeight = currentLocation.BaseVirtualHeight;
            _locationDisplayBarIndex = currentLocation.DisplayBarIndex;
            CheckPrioritySkybox(currentLocation);
        }
        if (SkyboxLayers.Count > 0 && !_isPrioritySkyboxActive)
        {
            Debug.Log($"[Skybox] Инициализация: устанавливаем стартовый скайбокс по высоте (индекс 0, диапазон [{SkyboxLayers[0].MinHeight}-{SkyboxLayers[0].MaxHeight}]м)");
            SetSkybox(0);
        }
        UpdateLocationVisuals();
        UpdateCursorState();
        SwitchAltitudeBar(_locationDisplayBarIndex);
        // Автоматический поиск SaveManager если не назначен в инспекторе
        if (SaveManager == null)
        {
            SaveManager = FindFirstObjectByType<SaveManager>();
            if (SaveManager != null)
            {
                Debug.Log("[Save] SaveManager найден автоматически через FindFirstObjectByType");
            }
            else
            {
                Debug.LogWarning("[Save] SaveManager не найден! Облачное сохранение после покупки телепорта работать не будет.");
            }
        }
    }
    // === ИНИЦИАЛИЗАЦИЯ ПУЛА КУБКОВ ===
    private void InitializeTrophyPool()
    {
        // Защита от вызова вне режима игры
        if (!Application.isPlaying)
        {
            return;
        }
        if (TrophyFlyPrefab == null || TrophyFlyCanvas == null)
        {
            Debug.LogWarning("[Trophy] Не настроены параметры анимации кубков (TrophyFlyPrefab или TrophyFlyCanvas). Анимация отключена.");
            return;
        }
        // Создаём пул объектов
        for (int i = 0; i < POOL_SIZE; i++)
        {
            GameObject trophy = Instantiate(TrophyFlyPrefab, TrophyFlyCanvas.transform);
            trophy.name = $"TrophyFly_{i}";
            trophy.SetActive(false);
            // Настройка размера спрайта
            RectTransform rt = trophy.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = TrophySpriteSize;
            }
            _trophyPool.Enqueue(trophy);
        }
        Debug.Log($"[Trophy] Инициализирован пул кубков: {POOL_SIZE} объектов");
    }
    // === МЕТОД ПОЛУЧЕНИЯ КУБКОВ С АНИМАЦИЕЙ ===
    public void AddTrophies(int amount)
    {
        if (amount <= 0) return;
        TrophiesCollected += amount;
        UpdateTrophyCounter();
        Debug.Log($"🏆 Получено {amount} кубков! Всего: {TrophiesCollected}");
        // Запускаем анимацию для каждого кубка с задержкой
        StartCoroutine(AnimateTrophyCollection(amount));
    }
    private IEnumerator AnimateTrophyCollection(int amount)
    {
        // Защита от вызова вне режима игры и от некорректных значений
        if (!Application.isPlaying || amount <= 0)
        {
            yield break;
        }
        // Проверяем настройки
        if (TrophyFlyPrefab == null || TrophyFlyCanvas == null || TrophyCounterTransform == null)
        {
            Debug.LogWarning("[Trophy] Анимация отключена: не все компоненты настроены в инспекторе");
            yield break;
        }
        // === ИСПРАВЛЕНО: надёжный расчёт центра экрана для ЛЮБОГО типа канваса ===
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 startPos;
        // Определяем камеру для конвертации в зависимости от режима канваса
        Camera conversionCamera = null;
        if (TrophyFlyCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            conversionCamera = TrophyFlyCanvas.worldCamera ?? Camera.main;
        }
        // Конвертируем центр экрана в локальные координаты канваса
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)TrophyFlyCanvas.transform,
            screenCenter,
            conversionCamera,
            out startPos))
        {
            // Резервный вариант: используем центр через размеры канваса
            RectTransform canvasRect = TrophyFlyCanvas.transform as RectTransform;
            if (canvasRect != null)
            {
                startPos = Vector2.zero; // Для канваса с центральными якорями (0,0) = центр
            }
            else
            {
                startPos = Vector2.zero;
                Debug.LogWarning("[Trophy] Не удалось определить центр экрана для канваса");
            }
        }
        // Конечная позиция (счётчик кубков)
        Vector2 endPos = TrophyCounterTransform.position;
        Vector2 endPosInCanvas;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)TrophyFlyCanvas.transform,
            endPos,
            conversionCamera,
            out endPosInCanvas))
        {
            endPosInCanvas = Vector2.zero;
            Debug.LogWarning("[Trophy] Не удалось конвертировать позицию счётчика в пространство канваса");
        }
        // Рассчитываем контрольную точку для дуги (середина пути + высота)
        Vector2 midPoint = (startPos + endPosInCanvas) * 0.5f;
        float distance = Vector2.Distance(startPos, endPosInCanvas);
        Vector2 controlPoint = midPoint + Vector2.up * (distance * FlyArcHeightMultiplier);
        // Отладка для проверки позиций
        Debug.Log($"[Trophy] Старт: {startPos}, Конец: {endPosInCanvas}, Контрольная точка: {controlPoint}, Расстояние: {distance}");
        // Анимируем каждый кубок с задержкой
        for (int i = 0; i < amount; i++)
        {
            // Берём кубок из пула
            GameObject trophyObject = GetTrophyFromPool();
            if (trophyObject == null) continue;
            // Запускаем анимацию
            TrophyFlyAnimation animation = trophyObject.GetComponent<TrophyFlyAnimation>();
            if (animation != null)
            {
                animation.StartAnimation(
                    startPos,
                    controlPoint,
                    endPosInCanvas,
                    TrophyFlyDuration,
                    () => ReturnTrophyToPool(trophyObject)
                );
            }
            // Задержка перед следующим кубком
            if (i < amount - 1)
            {
                yield return new WaitForSecondsRealtime(TrophyFlyDelay);
            }
        }
    }
    // Получение объекта из пула
    private GameObject GetTrophyFromPool()
    {
        if (_trophyPool.Count > 0)
        {
            return _trophyPool.Dequeue();
        }
        else
        {
            // Если пул пуст — создаём новый объект (не идеально, но защита от крашей)
            GameObject newTrophy = Instantiate(TrophyFlyPrefab, TrophyFlyCanvas.transform);
            newTrophy.name = $"TrophyFly_Dynamic_{Time.frameCount}";
            newTrophy.GetComponent<RectTransform>().sizeDelta = TrophySpriteSize;
            Debug.LogWarning("[Trophy] Пул исчерпан! Создан динамический объект кубка.");
            return newTrophy;
        }
    }
    // Возврат объекта в пул
    private void ReturnTrophyToPool(GameObject trophy)
    {
        if (trophy != null)
        {
            trophy.SetActive(false);
            _trophyPool.Enqueue(trophy);
        }
    }
    private void InitializeTeleportUI()
    {
        if (OpenTeleportUIButton != null)
        {
            OpenTeleportUIButton.onClick.RemoveAllListeners();
            OpenTeleportUIButton.onClick.AddListener(() =>
            {
                OpenTeleportUI();
                _teleportUIOpenedFromTrigger = false; // 🔑 Сбрасываем флаг при ручном открытии
            });
        }
        if (CloseTeleportUIButton != null)
        {
            CloseTeleportUIButton.onClick.RemoveAllListeners();
            CloseTeleportUIButton.onClick.AddListener(CloseTeleportUI);
        }
        if (TeleportUIPanel != null)
        {
            TeleportUIPanel.SetActive(false);
        }
        foreach (var uiPanel in TeleportUIPanels)
        {
            if (uiPanel.Panel != null)
            {
                bool shouldShowPanel = uiPanel.ShowByDefault || uiPanel.WasUnlocked;
                uiPanel.Panel.SetActive(shouldShowPanel);
                if (uiPanel.TeleportButton != null)
                {
                    uiPanel.TeleportButton.onClick.RemoveAllListeners();
                    string targetLocationID = uiPanel.TargetLocationID;
                    uiPanel.TeleportButton.onClick.AddListener(() => TeleportFromUI(targetLocationID));
                }
            }
        }
    }
    private void OnTeleportButtonPressed(string triggerID)
    {
        foreach (var uiPanel in TeleportUIPanels)
        {
            if (uiPanel.UnlockTriggerID == triggerID)
            {
                uiPanel.WasUnlocked = true;
                if (uiPanel.Panel != null)
                {
                    uiPanel.Panel.SetActive(true);
                }
            }
        }
    }
    private void TeleportFromUI(string targetLocationID)
    {
        LocationSettings targetLocation = GetLocationSettings(targetLocationID);
        if (targetLocation == null)
        {
            Debug.LogError($"Target location '{targetLocationID}' not found!");
            return;
        }
        CurrentLocationID = targetLocationID;
        if (CharacterController != null)
        {
            CharacterController.Motor.SetPosition(targetLocation.SpawnPosition);
            CharacterController.Motor.SetRotation(Quaternion.identity);
        }
        _currentVirtualHeight = targetLocation.BaseVirtualHeight;
        _locationDisplayBarIndex = targetLocation.DisplayBarIndex;
        CheckPrioritySkybox(targetLocation);
        SwitchAltitudeBar(_locationDisplayBarIndex);
        UpdateAltitudeBars();
        UpdateLocationVisuals();
        CloseTeleportUI();
    }
    private void OpenTeleportUI()
    {
        if (TeleportUIPanel != null)
        {
            UpdateTeleportUIPanelsVisibility();
            TeleportUIPanel.SetActive(true);
            _isTeleportUIOpen = true;
            UpdateCursorState();
        }
    }
    public void UpdateTeleportUIPanelsVisibility()
    {
        foreach (var uiPanel in TeleportUIPanels)
        {
            if (uiPanel.Panel != null)
            {
                bool shouldShowPanel = uiPanel.ShowByDefault || uiPanel.WasUnlocked;
                uiPanel.Panel.SetActive(shouldShowPanel);
            }
        }
    }
    private void CloseTeleportUI()
    {
        if (TeleportUIPanel != null)
        {
            TeleportUIPanel.SetActive(false);
            _isTeleportUIOpen = false;
            _teleportUIOpenedFromTrigger = false; // 🔑 Сбрасываем флаг при любом закрытии
            UpdateCursorState();
        }
    }
    private void InitializeTeleports()
    {
        foreach (var teleport in TeleportTriggers)
        {
            if (teleport.TeleportPanel != null)
            {
                teleport.TeleportPanel.SetActive(false);
            }
            if (!teleport.IsInitialized)
            {
                InitializeTeleportButtons(teleport);
                teleport.IsInitialized = true;
            }
            if (teleport.TeleportCollider != null)
            {
                var triggerComponent = teleport.TeleportCollider.gameObject.GetComponent<TeleportTriggerComponent>();
                if (triggerComponent == null)
                {
                    triggerComponent = teleport.TeleportCollider.gameObject.AddComponent<TeleportTriggerComponent>();
                }
                triggerComponent.Manager = this;
                triggerComponent.TriggerID = teleport.TriggerID;
                teleport.TeleportCollider.isTrigger = true;
            }
        }
    }
    private void InitializeTeleportButtons(TeleportTrigger teleport)
    {
        if (teleport.TeleportButton != null)
        {
            teleport.TeleportButton.onClick.RemoveAllListeners();
            string targetLocationID = teleport.TargetLocationID;
            float requiredHeight = teleport.RequiredHeight;
            int trophyCost = teleport.TrophyCost;
            string triggerID = teleport.TriggerID;
            teleport.TeleportButton.onClick.AddListener(() =>
            {
                OnTeleportButtonPressed(triggerID);
                if (_maxAchievedVirtualHeight >= requiredHeight && TrophiesCollected >= trophyCost)
                {
                    PerformTeleportation(targetLocationID, trophyCost, teleport);
                }
            });
        }
        if (teleport.CloseButton != null)
        {
            teleport.CloseButton.onClick.RemoveAllListeners();
            teleport.CloseButton.onClick.AddListener(CloseCurrentPanel);
        }
    }
    // 🔑 ИСПРАВЛЕННЫЙ МЕТОД: сохранение факта покупки телепорта с немедленным сохранением
    private void PerformTeleportation(string targetLocationID, int cost, TeleportTrigger teleport)
    {
        LocationSettings targetLocation = GetLocationSettings(targetLocationID);
        if (targetLocation == null)
        {
            Debug.LogError($"Target location '{targetLocationID}' not found!");
            return;
        }
        // Тратим кубки
        TrophiesCollected -= cost;
        UpdateTrophyCounter();
        // Обновляем текущую локацию
        CurrentLocationID = targetLocationID;
        if (CharacterController != null)
        {
            CharacterController.Motor.SetPosition(targetLocation.SpawnPosition);
            CharacterController.Motor.SetRotation(Quaternion.identity);
        }
        _currentVirtualHeight = targetLocation.BaseVirtualHeight;
        _locationDisplayBarIndex = targetLocation.DisplayBarIndex;
        CheckPrioritySkybox(targetLocation);
        SwitchAltitudeBar(_locationDisplayBarIndex);
        UpdateAltitudeBars();
        UpdateLocationVisuals();
        // 🔑 КРИТИЧЕСКИ ВАЖНО: помечаем телепорт как купленный
        teleport.WasBought = true;
        Debug.Log($"[Teleport] Телепорт '{teleport.TriggerID}' куплен за {cost} кубков. WasBought = true");
        // 🔑 РАЗБЛОКИРУЕМ СООТВЕТСТВУЮЩУЮ ПАНЕЛЬ В ОБЩЕЙ ПАНЕЛИ
        foreach (var panel in TeleportUIPanels)
        {
            if (panel.TargetLocationID == targetLocationID)
            {
                panel.WasUnlocked = true;
                Debug.Log($"[Teleport] Панель телепорта для локации '{targetLocationID}' разблокирована в общей панели");
                break;
            }
        }
        // Закрываем текущую панель телепорта
        CloseCurrentPanel();
        // 🔑 СОХРАНЯЕМ СРАЗУ ПОСЛЕ ПОКУПКИ (без задержки)
        if (SaveManager != null)
        {
            SaveManager.SaveGameData();
            Debug.Log($"[Cloud Save] Телепорт '{teleport.TriggerID}' куплен. Данные сохранены в облако.");
        }
        else
        {
            YG2.SaveProgress();
            Debug.Log($"[Cloud Save] Телепорт '{teleport.TriggerID}' куплен. Прямое сохранение через YG.");
        }
    }
    private void InitializeAltitudeBars()
    {
        for (int i = 0; i < AltitudeBars.Count; i++)
        {
            if (AltitudeBars[i].BarObject != null)
            {
                AltitudeBars[i].BarObject.SetActive(i == _locationDisplayBarIndex);
                if (AltitudeBars[i].RangeText != null)
                {
                    LocationSettings currentLocation = GetLocationSettings(CurrentLocationID);
                    if (currentLocation != null)
                    {
                        float virtualMin = AltitudeBars[i].MinHeight + currentLocation.BaseVirtualHeight;
                        float virtualMax = AltitudeBars[i].MaxHeight + currentLocation.BaseVirtualHeight;
                        AltitudeBars[i].RangeText.text = $"{FormatHeight((long)virtualMin)} - {FormatHeight((long)virtualMax)}";
                    }
                }
            }
        }
    }
    private void UpdateAltitudeBars()
    {
        if (CharacterController == null || AltitudeBars.Count == 0) return;
        float currentRealHeight = CharacterController.Motor.TransientPosition.y;
        int targetBarIndex = _locationDisplayBarIndex;
        for (int i = _locationDisplayBarIndex; i < AltitudeBars.Count; i++)
        {
            if (currentRealHeight >= AltitudeBars[i].MinHeight && currentRealHeight < AltitudeBars[i].MaxHeight)
            {
                targetBarIndex = i;
                break;
            }
            if (i == AltitudeBars.Count - 1 && currentRealHeight >= AltitudeBars[i].MaxHeight)
            {
                targetBarIndex = i;
            }
        }
        if (targetBarIndex != _currentBarIndex)
        {
            SwitchAltitudeBar(targetBarIndex);
        }
        UpdateCurrentBar(currentRealHeight);
    }
    private void SwitchAltitudeBar(int newIndex)
    {
        if (newIndex < 0 || newIndex >= AltitudeBars.Count) return;
        if (_currentBarIndex >= 0 && _currentBarIndex < AltitudeBars.Count && AltitudeBars[_currentBarIndex].BarObject != null)
        {
            AltitudeBars[_currentBarIndex].BarObject.SetActive(false);
        }
        if (AltitudeBars[newIndex].BarObject != null)
        {
            AltitudeBars[newIndex].BarObject.SetActive(true);
        }
        _currentBarIndex = newIndex;
    }
    private void UpdateCurrentBar(float currentRealHeight)
    {
        if (_currentBarIndex < 0 || _currentBarIndex >= AltitudeBars.Count) return;
        var currentBar = AltitudeBars[_currentBarIndex];
        float heightInCurrentBar = currentRealHeight - currentBar.MinHeight;
        float normalizedHeight = Mathf.Clamp01(heightInCurrentBar / (currentBar.MaxHeight - currentBar.MinHeight));
        if (currentBar.ArrowImage != null)
        {
            RectTransform arrowRect = currentBar.ArrowImage.rectTransform;
            float arrowPosition = Mathf.Lerp(0f, 1f, normalizedHeight);
            arrowRect.anchorMin = new Vector2(0.5f, arrowPosition);
            arrowRect.anchorMax = new Vector2(0.5f, arrowPosition);
            arrowRect.anchoredPosition = Vector2.zero;
        }
        if (currentBar.HeightText != null)
        {
            LocationSettings currentLocation = GetLocationSettings(CurrentLocationID);
            if (currentLocation != null)
            {
                float totalHeight = currentRealHeight + currentLocation.BaseVirtualHeight;
                currentBar.HeightText.text = FormatHeight((long)totalHeight);
            }
        }
        if (currentBar.RangeText != null)
        {
            LocationSettings currentLocation = GetLocationSettings(CurrentLocationID);
            if (currentLocation != null)
            {
                float virtualMin = currentBar.MinHeight + currentLocation.BaseVirtualHeight;
                float virtualMax = currentBar.MaxHeight + currentLocation.BaseVirtualHeight;
                currentBar.RangeText.text = $"{FormatHeight((long)virtualMin)} - {FormatHeight((long)virtualMax)}";
            }
        }
    }
    // 🔑 НОВЫЙ МЕТОД: принудительная установка локации с корректным скайбоксом
    public void ForceSetLocation(string locationID)
    {
        if (locationID == CurrentLocationID) return;
        LocationSettings targetLocation = GetLocationSettings(locationID);
        if (targetLocation == null)
        {
            Debug.LogError($"[Location] Локация '{locationID}' не найдена!");
            return;
        }
        // Сохраняем предыдущую локацию для отладки
        string previousLocation = CurrentLocationID;
        // Устанавливаем новую локацию
        CurrentLocationID = locationID;
        _currentVirtualHeight = targetLocation.BaseVirtualHeight;
        _locationDisplayBarIndex = targetLocation.DisplayBarIndex;
        // Обновляем визуал
        UpdateLocationVisuals();
        SwitchAltitudeBar(_locationDisplayBarIndex);
        UpdateAltitudeBars();
        // 🔑 КРИТИЧЕСКИ ВАЖНО: сначала деактивируем приоритетный скайбокс СТАРОЙ локации
        if (_isPrioritySkyboxActive)
        {
            ClearPrioritySkybox(previousLocation);
        }
        // Проверяем приоритетный скайбокс НОВОЙ локации
        CheckPrioritySkybox(targetLocation);
        // 🔑 КРИТИЧЕСКИ ВАЖНО: принудительно обновляем скайбокс ПО ВЫСОТЕ после смены локации
        if (!_isPrioritySkyboxActive)
        {
            UpdateSkyboxBasedOnHeight();
        }
        Debug.Log($"[Location] ✅ Локация изменена: {previousLocation} → {locationID}. Базовая высота: {targetLocation.BaseVirtualHeight}м");
    }
    // === ИЗМЕНЁННЫЙ МЕТОД: ПРОВЕРКА КУПЛЕННОГО ТЕЛЕПОРТА ===
    public void OnTeleportTriggerEnter(string triggerID)
    {
        _activeTriggers.Add(triggerID);
        // Ищем триггер по ID
        TeleportTrigger teleport = null;
        foreach (var t in TeleportTriggers)
        {
            if (t.TriggerID == triggerID)
            {
                teleport = t;
                break;
            }
        }
        if (teleport == null) return;
        // === ЕСЛИ ТЕЛЕПОРТ УЖЕ КУПЛЕН — ОТКРЫВАЕМ ОБЩУЮ ПАНЕЛЬ ВМЕСТО ЛОКАЛЬНОЙ ===
        if (teleport.WasBought)
        {
            Debug.Log($"[Teleport] Телепорт '{triggerID}' уже куплен. Открываем общую панель телепортов вместо локальной панели.");
            OpenTeleportUI();
            _teleportUIOpenedFromTrigger = true; // 🔑 Запоминаем, что панель открыта через триггер
            return;
        }
        // Иначе открываем локальную панель как обычно
        if (teleport.TeleportPanel != null)
        {
            _currentTeleport = teleport;
            _currentActivePanel = teleport.TeleportPanel;
            _currentActivePanel.SetActive(true);
            UpdateCursorState();
            UpdateTeleportPanel();
        }
    }
    public void OnTeleportTriggerExit(string triggerID)
    {
        _activeTriggers.Remove(triggerID);
        // Если вышли из последнего триггера
        if (_activeTriggers.Count == 0)
        {
            // Закрываем локальную панель
            CloseCurrentPanel();
            // 🔑 Закрываем общую панель, если она была открыта через триггер
            if (_isTeleportUIOpen && _teleportUIOpenedFromTrigger)
            {
                CloseTeleportUI();
                Debug.Log("[Teleport] Игрок вышел из всех триггеров — закрыта общая панель телепортов");
            }
        }
        // Если ещё остались активные триггеры — просто закрываем локальную панель текущего триггера
        else if (_currentTeleport != null && _currentTeleport.TriggerID == triggerID)
        {
            CloseCurrentPanel();
        }
    }
    private void DisableDefaultLightingSkybox()
    {
        DynamicGI.UpdateEnvironment();
    }
    private void Update()
    {
        UpdateSkybox();
        UpdateTrophyCounter();
        UpdateAltitudeBars();
        float currentVirtualHeight = GetCurrentVirtualHeight();
        if (currentVirtualHeight > _maxAchievedVirtualHeight)
        {
            _maxAchievedVirtualHeight = currentVirtualHeight;
        }
        LocationSettings currentLocation = GetLocationSettings(CurrentLocationID);
        if (currentLocation != null)
        {
            CheckPrioritySkybox(currentLocation);
        }
        if (_currentActivePanel != null && _currentTeleport != null)
        {
            UpdateTeleportPanel();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseCurrentPanel();
            }
        }
        if (_currentActivePanel != null && _activeTriggers.Count == 0)
        {
            CloseCurrentPanel();
        }
    }
    private void UpdateTeleportPanel()
    {
        if (_currentTeleport == null) return;
        bool heightReached = _maxAchievedVirtualHeight >= _currentTeleport.RequiredHeight;
        bool hasEnoughTrophies = TrophiesCollected >= _currentTeleport.TrophyCost;
        if (_currentTeleport.UnlockStatusText != null)
        {
            _currentTeleport.UnlockStatusText.color = heightReached ? Color.green : Color.red;
        }
        if (_currentTeleport.CostText != null)
        {
            _currentTeleport.CostText.text = $"x{_currentTeleport.TrophyCost}"; // Символ "x" перед числом
            _currentTeleport.CostText.color = hasEnoughTrophies ? Color.green : Color.red;
        }
        if (_currentTeleport.TeleportButton != null)
        {
            _currentTeleport.TeleportButton.interactable = heightReached && hasEnoughTrophies;
        }
    }
    private float GetCurrentVirtualHeight()
    {
        if (CharacterController == null) return _currentVirtualHeight;
        LocationSettings currentLocation = GetLocationSettings(CurrentLocationID);
        if (currentLocation != null)
        {
            float realHeight = CharacterController.Motor.TransientPosition.y;
            return realHeight + currentLocation.BaseVirtualHeight;
        }
        return _currentVirtualHeight;
    }
    public void CloseCurrentPanel()
    {
        if (_currentActivePanel != null)
        {
            _currentActivePanel.SetActive(false);
            _currentActivePanel = null;
            _currentTeleport = null;
            UpdateCursorState();
        }
    }
    private void UpdateTrophyCounter()
    {
        // Защита от отсутствия ссылки на текст
        if (TrophyCounterText != null)
        {
            TrophyCounterText.text = TrophiesCollected.ToString(); // Только число без "Кубки: "
        }
        else
        {
            Debug.LogWarning("[Trophy] TrophyCounterText не назначен в инспекторе WorldSystemManager!");
        }
    }
    private void UpdateLocationVisuals()
    {
        LocationSettings currentLocation = GetLocationSettings(CurrentLocationID);
        if (currentLocation != null && LocationNameText != null)
        {
            LocationNameText.text = $"{currentLocation.LocationName} ({FormatHeight((long)currentLocation.BaseVirtualHeight)}м)";
        }
    }
    // 🔑 ИСПРАВЛЕННЫЙ МЕТОД: выбор скайбокса с учётом ТЕКУЩЕЙ ЛОКАЦИИ
    private void UpdateSkybox()
    {
        if (SkyboxLayers.Count == 0 || CharacterController == null || _isPrioritySkyboxActive) return;
        float currentVirtualHeight = GetCurrentVirtualHeight();
        int newIndex = 0;
        // 🔑 ИСПРАВЛЕНО: поиск диапазона с учётом ТЕКУЩЕЙ ЛОКАЦИИ
        for (int i = 0; i < SkyboxLayers.Count; i++)
        {
            if (currentVirtualHeight >= SkyboxLayers[i].MinHeight && currentVirtualHeight < SkyboxLayers[i].MaxHeight)
            {
                newIndex = i;
                break;
            }
            if (i == SkyboxLayers.Count - 1 && currentVirtualHeight >= SkyboxLayers[i].MaxHeight)
            {
                newIndex = i;
                break;
            }
        }
        // 🔑 ДОПОЛНИТЕЛЬНАЯ ПРОВЕРКА: если диапазон 0-7км и включена галочка "Use Location Skybox"
        // — БЕРЁМ СКАЙБОКС ИЗ ТЕКУЩЕЙ ЛОКАЦИИ, а не из слоя
        if (newIndex < SkyboxLayers.Count && SkyboxLayers[newIndex].UseLocationSkybox)
        {
            LocationSettings currentLocation = GetLocationSettings(CurrentLocationID);
            if (currentLocation != null && currentLocation.PrioritySkyboxMaterial != null)
            {
                // Не меняем индекс, но при применении будем использовать скайбокс локации
                Debug.Log($"[Skybox] Диапазон [{SkyboxLayers[newIndex].MinHeight}-{SkyboxLayers[newIndex].MaxHeight}]м → ИСПОЛЬЗУЕМ скайбокс локации '{currentLocation.LocationName}'");
            }
        }
        if (newIndex != _currentSkyboxIndex)
        {
            Debug.Log($"[Skybox] Смена скайбокса: текущая локация='{CurrentLocationID}', высота={currentVirtualHeight:F0}м → диапазон [{SkyboxLayers[newIndex].MinHeight}-{SkyboxLayers[newIndex].MaxHeight}]м");
            SetSkybox(newIndex);
        }
    }
    private void CheckPrioritySkybox(LocationSettings location)
    {
        if (location.PrioritySkyboxMaterial != null && CharacterController != null)
        {
            float distanceToSpawn = Vector3.Distance(CharacterController.Motor.TransientPosition, location.SpawnPosition);
            bool isInRadius = distanceToSpawn <= location.PrioritySkyboxRadius;
            if (isInRadius && (!_isPrioritySkyboxActive || _currentPrioritySkybox != location.PrioritySkyboxMaterial))
            {
                Debug.Log($"[Skybox] РЕШЕНИЕ: активация ПРИОРИТЕТНОГО скайбокса '{location.PrioritySkyboxMaterial.name}' для локации '{location.LocationName}'. Причина: игрок в пределах радиуса ({distanceToSpawn:F1}м <= {location.PrioritySkyboxRadius}м от спавна)");
                SetPrioritySkybox(location.PrioritySkyboxMaterial, location.LocationName, location.PrioritySkyboxRadius);
            }
            else if (!isInRadius && _isPrioritySkyboxActive && _currentPrioritySkybox == location.PrioritySkyboxMaterial)
            {
                Debug.Log($"[Skybox] РЕШЕНИЕ: деактивация ПРИОРИТЕТНОГО скайбокса по радиусу. Причина: игрок покинул радиус действия ({distanceToSpawn:F1}м > {location.PrioritySkyboxRadius}м от спавна локации '{location.LocationName}')");
                ClearPrioritySkybox(location.LocationName);
            }
        }
        else if (_isPrioritySkyboxActive && location.PrioritySkyboxMaterial == null)
        {
            Debug.Log($"[Skybox] РЕШЕНИЕ: деактивация ПРИОРИТЕТНОГО скайбокса по радиусу. Причина: текущая локация '{location.LocationName}' не имеет приоритетного скайбокса");
            ClearPrioritySkybox(location.LocationName);
        }
    }
    private void SetPrioritySkybox(Material skyboxMaterial, string locationName, float radius)
    {
        _currentPrioritySkybox = skyboxMaterial;
        _isPrioritySkyboxActive = true;
        _isUsingLocationSkybox = false;
        if (Camera.main != null)
        {
            var cameraSkybox = Camera.main.GetComponent<Skybox>();
            if (cameraSkybox != null)
            {
                cameraSkybox.material = skyboxMaterial;
                Debug.Log($"[Skybox] ПРИМЕНЕНО: установлен ПРИОРИТЕТНЫЙ скайбокс по радиусу '{skyboxMaterial.name}' для локации '{locationName}' (радиус {radius}м)");
            }
        }
        if (LightingSkyboxMaterial != null)
        {
            RenderSettings.skybox = LightingSkyboxMaterial;
        }
        DynamicGI.UpdateEnvironment();
    }
    private void ClearPrioritySkybox(string locationName)
    {
        _currentPrioritySkybox = null;
        _isPrioritySkyboxActive = false;
        Debug.Log($"[Skybox] ПРИМЕНЕНО: приоритетный скайбокс по радиусу отключён для локации '{locationName}'. Возврат к скайбоксу по высоте...");
        UpdateSkyboxBasedOnHeight();
    }
    private void UpdateSkyboxBasedOnHeight()
    {
        if (SkyboxLayers.Count == 0 || CharacterController == null) return;
        float currentVirtualHeight = GetCurrentVirtualHeight();
        int newIndex = 0;
        for (int i = 0; i < SkyboxLayers.Count; i++)
        {
            if (currentVirtualHeight >= SkyboxLayers[i].MinHeight && currentVirtualHeight < SkyboxLayers[i].MaxHeight)
            {
                newIndex = i;
                break;
            }
            if (i == SkyboxLayers.Count - 1 && currentVirtualHeight >= SkyboxLayers[i].MaxHeight)
            {
                newIndex = i;
            }
        }
        if (newIndex != _currentSkyboxIndex)
        {
            Debug.Log($"[Skybox] ВОССТАНОВЛЕНИЕ: возврат к скайбоксу по высоте после отключения приоритетного по радиусу. Текущая высота: {currentVirtualHeight:F0}м -> диапазон [{SkyboxLayers[newIndex].MinHeight}-{SkyboxLayers[newIndex].MaxHeight}]м");
            SetSkybox(newIndex);
        }
    }
    // 🔑 ИСПРАВЛЕННЫЙ МЕТОД: применение скайбокса с учётом локации
    private void SetSkybox(int index)
    {
        if (index < 0 || index >= SkyboxLayers.Count) return;
        _currentSkyboxIndex = index;
        var layer = SkyboxLayers[index];
        Material skyboxToApply = layer.SkyboxMaterial;
        _isUsingLocationSkybox = false;
        // 🔑 КРИТИЧЕСКИ ВАЖНО: для диапазона 0-7км БЕРЁМ СКАЙБОКС ИЗ ТЕКУЩЕЙ ЛОКАЦИИ
        if (layer.UseLocationSkybox)
        {
            LocationSettings currentLocation = GetLocationSettings(CurrentLocationID);
            if (currentLocation != null && currentLocation.PrioritySkyboxMaterial != null)
            {
                skyboxToApply = currentLocation.PrioritySkyboxMaterial;
                _isUsingLocationSkybox = true;
                Debug.Log($"[Skybox] ✅ ПРИМЕНЕНО: для диапазона [{layer.MinHeight}-{layer.MaxHeight}]м используется скайбокс ТЕКУЩЕЙ локации '{currentLocation.LocationName}': '{skyboxToApply.name}'");
            }
            else
            {
                Debug.LogWarning($"[Skybox] ⚠️ Галочка Use Location Skybox активна для диапазона [{layer.MinHeight}-{layer.MaxHeight}]м, но у локации '{currentLocation?.LocationName ?? "неизвестно"}' не назначен PrioritySkyboxMaterial. Используется стандартный скайбокс слоя.");
            }
        }
        // Применяем скайбокс
        if (Camera.main != null)
        {
            var cameraSkybox = Camera.main.GetComponent<Skybox>();
            if (cameraSkybox != null && skyboxToApply != null)
            {
                cameraSkybox.material = skyboxToApply;
                string source = _isUsingLocationSkybox ? "локации" : "слоя";
                Debug.Log($"[Skybox] 🌌 Установлен скайбокс из {source} '{skyboxToApply.name}' для диапазона [{layer.MinHeight}-{layer.MaxHeight}]м. Текущая локация: {CurrentLocationID}");
            }
        }
        // Обновляем освещение
        if (LightingSkyboxMaterial != null)
        {
            RenderSettings.skybox = LightingSkyboxMaterial;
        }
        DynamicGI.UpdateEnvironment();
    }
    private LocationSettings GetLocationSettings(string locationID)
    {
        foreach (var location in LocationSettingsList)
        {
            if (location.LocationID == locationID)
            {
                return location;
            }
        }
        Debug.LogError($"Локация с ID '{locationID}' не найдена!");
        return null;
    }
    private string FormatHeight(long height)
    {
        return $"{height:N0} м";
    }
    public float GetMaxAchievedVirtualHeight()
    {
        return _maxAchievedVirtualHeight;
    }
    // 🔑 ИСПРАВЛЕННЫЙ МЕТОД: телепортация с сохранением позиции
    public void TeleportToLocation(string locationID)
    {
        LocationSettings targetLocation = GetLocationSettings(locationID);
        if (targetLocation != null)
        {
            CurrentLocationID = locationID;
            if (CharacterController != null)
            {
                CharacterController.Motor.SetPosition(targetLocation.SpawnPosition);
                CharacterController.Motor.SetRotation(Quaternion.identity);
            }
            _currentVirtualHeight = targetLocation.BaseVirtualHeight;
            _locationDisplayBarIndex = targetLocation.DisplayBarIndex;
            UpdateLocationVisuals();
            SwitchAltitudeBar(_locationDisplayBarIndex);
            UpdateAltitudeBars();
            if (!_isPrioritySkyboxActive)
            {
                UpdateSkyboxBasedOnHeight();
            }
            Debug.Log($"[Teleport] Игрок телепортирован на локацию '{locationID}' (спавн: {targetLocation.SpawnPosition})");
        }
    }
#if UNITY_EDITOR
    [ContextMenu("➕ Добавить 1 кубок")]
    public void AddOneTrophyInEditor()
    {
        // Защита от вызова в режиме редактирования без инициализированных компонентов
        if (!Application.isPlaying)
        {
            // Просто увеличиваем счётчик без анимации
            TrophiesCollected++;
            if (TrophyCounterText != null)
            {
                TrophyCounterText.text = TrophiesCollected.ToString(); // Только число без "Кубки: "
            }
            Debug.Log($"[Editor] Добавлен 1 кубок в режиме редактирования. Всего: {TrophiesCollected}");
            UnityEditor.EditorUtility.SetDirty(this);
            return;
        }
        // В режиме игры запускаем полную анимацию
        if (TrophiesCollected < 0) TrophiesCollected = 0; // Защита от отрицательных значений
        AddTrophies(1);
        Debug.Log($"[Editor] Добавлен 1 кубок через инспектор. Всего кубков: {TrophiesCollected}");
    }
#endif
    private void UpdateCursorState()
    {
        bool showCursor = _currentActivePanel != null || _isTeleportUIOpen;
        if (showCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            if (CharacterController != null)
            {
                Cursor.lockState = CharacterController.cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !CharacterController.cursorLocked;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
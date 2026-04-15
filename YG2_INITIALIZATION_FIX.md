# Решение проблемы: методы вызываются до инициализации SDK YG2

## Проблема
В проекте некоторые скрипты обращаются к `YG2.saves` в методах `Start()` или `Awake()`, что происходит **до** полной инициализации Yandex Games SDK. Это приводит к ошибкам:
- `NullReferenceException` при доступе к `YG2.saves`
- Потеря данных сохранений
- Неправильная работа систем (VIP, питомцы, колесо фортуны)

## Решение

### 1. Создан новый менеджер инициализации
Файл: `Assets/Scripts/YG2InitializationManager.cs`

Этот синглтон-менеджер:
- Контролирует процесс инициализации YG2 SDK
- Предоставляет событие `OnSDKReady` для уведомления о готовности
- Имеет статические методы для безопасной проверки доступности saves

**Ключевые методы:**
```csharp
// Проверка можно ли безопасно обращаться к YG2.saves
YG2InitializationManager.CanAccessSaves() // возвращает bool

// Безопасное получение saves
if (YG2InitializationManager.TryGetSaves(out var saves))
{
    // Используем saves
}

// Событие о готовности SDK
YG2InitializationManager.Instance.OnSDKReady += YourCallback;
```

### 2. Обновленные скрипты

#### LadderZone.cs
**Было:**
```csharp
private void Start()
{
    if (isVIP)
    {
        bool vipUnlocked = YG2.saves.vipUnlocked; // ❌ Может быть null!
        SetVIPAccess(vipUnlocked);
    }
}
```

**Стало:**
```csharp
private void Start()
{
    if (isVIP)
    {
        if (YG2InitializationManager.CanAccessSaves())
        {
            bool vipUnlocked = YG2.saves.vipUnlocked;
            SetVIPAccess(vipUnlocked);
        }
        else
        {
            SetVIPAccess(false); // Значение по умолчанию
            Debug.LogWarning("YG2 SDK ещё не инициализирован");
        }
    }
}
```

#### VIPManager.cs
**Было:**
```csharp
private void Start()
{
    if (YG2.isSDKEnabled && YG2.saves != null)
        vipUnlocked = YG2.saves.vipUnlocked; // ❌ Проверка недостаточна!
}
```

**Стало:**
```csharp
private void Start()
{
    StartCoroutine(InitializeVIP());
}

private IEnumerator InitializeVIP()
{
    while (!YG2InitializationManager.CanAccessSaves())
        yield return null;
    
    vipUnlocked = YG2.saves.vipUnlocked; // ✅ Теперь безопасно!
}
```

#### PetSystem.cs
**Было:**
```csharp
private void Start()
{
    LoadPetsData(); // ❌ Вызывает YG2.saves сразу!
}
```

**Стало:**
```csharp
private void Start()
{
    StartCoroutine(InitializePetSystem());
}

private IEnumerator InitializePetSystem()
{
    while (!YG2InitializationManager.CanAccessSaves())
        yield return null;
    
    LoadPetsData(); // ✅ Теперь безопасно!
}
```

## Как использовать в других скриптах

### Вариант 1: Проверка перед доступом
```csharp
void Start()
{
    if (YG2InitializationManager.CanAccessSaves())
    {
        // Безопасный доступ к YG2.saves
        int coins = YG2.saves.coinsCollected;
    }
    else
    {
        // Используем значения по умолчанию
        Debug.LogWarning("SDK ещё не готов");
    }
}
```

### Вариант 2: Ожидание через корутину
```csharp
void Start()
{
    StartCoroutine(WaitForSDK());
}

IEnumerator WaitForSDK()
{
    while (!YG2InitializationManager.CanAccessSaves())
        yield return null;
    
    // Теперь можно использовать YG2.saves
    LoadData();
}
```

### Вариант 3: Подписка на событие
```csharp
void OnEnable()
{
    YG2InitializationManager.Instance.OnSDKReady += OnSDKReady;
}

void OnDisable()
{
    YG2InitializationManager.Instance.OnSDKReady -= OnSDKReady;
}

void OnSDKReady()
{
    // Инициализация после готовности SDK
    InitializeGame();
}
```

## Настройка в Unity

1. **Создайте пустой GameObject** на сцене (например, "GameManager")
2. **Добавьте компонент** `YG2InitializationManager`
3. **Настройте параметры** (опционально):
   - `Max Initialization Time`: макс. время ожидания SDK (по умолчанию 10 сек)
   - `Post Init Delay`: задержка после инициализации (по умолчанию 0.5 сек)

## Какие скрипты требуют обновления

Проверьте следующие файлы на наличие прямых обращений к `YG2.saves` в `Start()`/`Awake()`:

- ✅ `LadderZone.cs` - обновлено
- ✅ `VIPManager.cs` - обновлено  
- ✅ `PetSystem.cs` - обновлено
- ⚠️ `WingsSystem.cs` - требует проверки
- ⚠️ `FortuneWheel.cs` - уже есть WaitForSaveManager, но можно улучшить
- ⚠️ `AutoInterstitialAd.cs` - уже есть InitializeAfterSDK
- ⚠️ `CoinShopManager.cs` - требует проверки
- ⚠️ `SilverCoinsShopManager.cs` - требует проверки

## Рекомендации

1. **Не обращайтесь к `YG2.saves` напрямую в `Awake()` или `Start()`**
2. **Используйте `CanAccessSaves()` для проверки** перед каждым обращением
3. **Для сложной инициализации используйте корутины** с ожиданием готовности SDK
4. **SaveManager уже обрабатывает это правильно** через `onGetSDKData`

## Тестирование

После внедрения изменений:
1. Запустите проект в Unity
2. Откройте консоль (Window → General → Console)
3. Убедитесь что нет ошибок `NullReferenceException` связанных с `YG2.saves`
4. Проверьте логи инициализации:
   ```
   🚀 YG2InitializationManager: Начало инициализации Yandex Games SDK...
   ✅ YG2InitializationManager: SDK полностью инициализирован и готов к использованию!
   ```

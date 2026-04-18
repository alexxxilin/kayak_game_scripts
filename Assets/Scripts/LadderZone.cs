using UnityEngine;
using KinematicCharacterController.Examples;
using YG;
using System.Collections;

public class LadderZone : MonoBehaviour
{
    [Header("Настройки лестницы")]
    [SerializeField] private float climbSpeed = 3f;
    [SerializeField] private float snapDistance = 1f;
    [SerializeField] private Transform ladderTopPoint;
    [SerializeField] private Transform ladderBottomPoint;
    [SerializeField] private Vector3 mountOffset = new Vector3(0.5f, 0, 0);
    [SerializeField] private bool allowClimbingDown = false;

    [Header("Идентификатор")]
    [Tooltip("Уникальный ID лестницы")]
    [SerializeField] private int ladderId = 0;

    [Header("Связь с другой горкой")]
    [Tooltip("Для подъёмной горки: ссылка на спусковую горку, на которую можно перелезть по пробелу")]
    [SerializeField] private LadderZone connectedDescentLadder;
    [Tooltip("Для спусковой горки: ссылка на первую подъёмную горку")]
    [SerializeField] private LadderZone connectedAscentLadder;
    [Tooltip("Для спусковой горки: ссылка на вторую подъёмную горку (опционально)")]
    [SerializeField] private LadderZone connectedAscentLadder2;

    [Header("Отталкивание после спуска")]
    [Tooltip("Расстояние, на которое игрока отбрасывает от лестницы после завершения спуска (метры)")]
    [SerializeField] private float jumpOffHorizontalDistance = 5f;

    [Header("VIP Settings")]
    [Tooltip("Является ли лестница VIP-горкой")]
    [SerializeField] private bool isVIP = false;
    [Tooltip("Множитель монет (для VIP = 2)")]
    [SerializeField] private float coinsMultiplier = 1f;
    [Tooltip("Объект-преграда (коллайдер), который блокирует доступ к VIP-горке")]
    [SerializeField] private GameObject blocker;

    [Header("Trophy Reward")]
    [Tooltip("Количество кубков, выдаваемых при касании кубка после подъёма по этой лестнице")]
    [SerializeField] private float trophyRewardAmount = 1f;

    [Header("Gates")]
    [Tooltip("Объект-ворота / коллайдер внизу (вход)")]
    [SerializeField] private GameObject entranceGate;
    [Tooltip("Объект-ворота / коллайдер наверху (выход)")]
    [SerializeField] private GameObject exitGate;

    [Header("Gate Settings")]
    [Tooltip("Задержка перед повторным появлением ворот после выхода с лестницы (сек)")]
    [SerializeField] private float gateReappearDelay = 0.5f;
    [Tooltip("Дистанция от вершины лестницы, при которой выход считается 'успешным завершением' (метры)")]
    [SerializeField] private float topExitThreshold = 2.5f;
    [Tooltip("Включить отладочные логи")]
    [SerializeField] private bool debugLogs = false;

    [Header("Визуализация")]
    [SerializeField] private Color gizmoColor = new Color(0, 1, 0, 0.3f);

    private ExampleCharacterController currentCharacter;
    private bool _isPlayerInsideTrigger = false;
    private Coroutine showGateCoroutine;
    private bool _wasNearTopOnExit = false;

    // Публичные свойства
    public Transform GetTopPoint() => ladderTopPoint;
    public Transform GetBottomPoint() => ladderBottomPoint;
    public Vector3 GetMountOffset() => mountOffset;
    public bool AllowClimbingDown => allowClimbingDown;
    public float JumpOffHorizontalDistance => jumpOffHorizontalDistance;
    public int LadderId => ladderId;
    public LadderZone ConnectedDescentLadder => connectedDescentLadder;
    public bool IsPlayerInsideTrigger => _isPlayerInsideTrigger;
    public bool IsVIP => isVIP;
    public float CoinsMultiplier => coinsMultiplier;
    public float TrophyRewardAmount => trophyRewardAmount;

    public void SetVIPAccess(bool unlocked)
    {
        if (blocker != null)
            blocker.SetActive(!unlocked);
    }

    // Геометрические методы
    public Vector3 GetDirection() => (ladderTopPoint.position - ladderBottomPoint.position).normalized;
    public float GetLength() => Vector3.Distance(ladderBottomPoint.position, ladderTopPoint.position);
    public Vector3 GetPoint(float t) => Vector3.Lerp(ladderBottomPoint.position, ladderTopPoint.position, t);
    
    public float GetProjection(Vector3 worldPoint)
    {
        Vector3 bottom = ladderBottomPoint.position;
        Vector3 dir = GetDirection();
        float length = GetLength();
        Vector3 toPoint = worldPoint - bottom;
        float t = Vector3.Dot(toPoint, dir) / length;
        return Mathf.Clamp01(t);
    }

    public bool IsNearTop(Vector3 worldPoint, float threshold)
    {
        if (ladderTopPoint == null) return false;
        return Vector3.Distance(worldPoint, ladderTopPoint.position) <= threshold;
    }

    private void Start()
    {
        // 1. Открываем ворота сразу (это безопасно)
        SetGatesActive(true);

        // 2. Если лестница не VIP, преграды нет, ждать нечего
        if (!isVIP) return;

        // 3. Если VIP, проверяем, готов ли уже SDK
        if (YG2InitializationManager.Instance != null && YG2InitializationManager.Instance.IsInitialized)
        {
            // SDK уже готов (быстрая загрузка или тест в редакторе)
            ApplyVIPStatus();
        }
        else
        {
            // SDK ещё грузится. Подписываемся на событие готовности.
            // Как только SDK загрузится, он вызовет ApplyVIPStatus()
            if (YG2InitializationManager.Instance != null)
            {
                YG2InitializationManager.Instance.OnSDKReady += ApplyVIPStatus;
            }
            else
            {
                Debug.LogError("[LadderZone] YG2InitializationManager не найден на сцене! VIP горки будут заблокированы.");
            }
        }
    }

    // Этот метод вызывается, когда SDK полностью готов
    private void ApplyVIPStatus()
    {
        if (!isVIP) return;

        if (YG2InitializationManager.CanAccessSaves())
        {
            bool vipUnlocked = YG2.saves.vipUnlocked;
            SetVIPAccess(vipUnlocked);
            Debug.Log($"[LadderZone {ladderId}] VIP статус применен: {vipUnlocked}");
        }
        else
        {
            Debug.LogWarning($"[LadderZone {ladderId}] Не удалось получить сохранения после инициализации.");
            SetVIPAccess(false);
        }
    }

    private void OnDestroy()
    {
        // Обязательно отписываемся, чтобы избежать утечек памяти
        if (YG2InitializationManager.Instance != null)
        {
            YG2InitializationManager.Instance.OnSDKReady -= ApplyVIPStatus;
        }
    }

    private void SetGatesActive(bool active)
    {
        if (entranceGate != null) entranceGate.SetActive(active);
        if (exitGate != null) exitGate.SetActive(active);
    }

    private void SetAllConnectedGatesActive(bool active)
    {
        SetGatesActive(active);
        if (connectedDescentLadder != null)
            connectedDescentLadder.SetGatesActive(active);
        if (connectedAscentLadder != null)
            connectedAscentLadder.SetGatesActive(active);
        if (connectedAscentLadder2 != null)
            connectedAscentLadder2.SetGatesActive(active);
    }

    public void ForceHideGates()
    {
        if (showGateCoroutine != null) StopCoroutine(showGateCoroutine);
        SetAllConnectedGatesActive(false);
    }

    public void ShowGatesWithDelay()
    {
        if (showGateCoroutine != null) StopCoroutine(showGateCoroutine);
        showGateCoroutine = StartCoroutine(ShowGatesCoroutine());
    }

    public void ShowGatesImmediately()
    {
        if (showGateCoroutine != null) StopCoroutine(showGateCoroutine);
        showGateCoroutine = null;
        SetAllConnectedGatesActive(true);
    }

    private IEnumerator ShowGatesCoroutine()
    {
        yield return new WaitForSeconds(gateReappearDelay);
        
        bool playerInThis = currentCharacter != null || _isPlayerInsideTrigger;
        bool playerInDescent = connectedDescentLadder != null && connectedDescentLadder._isPlayerInsideTrigger;
        bool playerInAscent1 = connectedAscentLadder != null && connectedAscentLadder._isPlayerInsideTrigger;
        bool playerInAscent2 = connectedAscentLadder2 != null && connectedAscentLadder2._isPlayerInsideTrigger;
        
        if (!playerInThis && !playerInDescent && !playerInAscent1 && !playerInAscent2)
        {
            SetAllConnectedGatesActive(true);
        }
        showGateCoroutine = null;
    }

    public void OnPlayerExitedLadder()
    {
        currentCharacter = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentCharacter != null) return;
        
        var characterLadder = other.GetComponent<ExampleCharacterController>();
        if (characterLadder != null && characterLadder.CurrentCharacterState != CharacterState.Climbing)
        {
            // ✅ БЕЗОПАСНАЯ ПРОВЕРКА VIP В ТРИГГЕРЕ
            bool isLocked = false;
            if (isVIP)
            {
                if (YG2InitializationManager.CanAccessSaves())
                {
                    isLocked = !YG2.saves.vipUnlocked;
                }
                else
                {
                    // Если SDK не готов, считаем что заблокировано (безопасно)
                    isLocked = true; 
                }
            }

            if (isLocked)
            {
                Debug.Log("VIP ladder locked. Purchase VIP access.");
                return;
            }
            
            bool isTopEntry = allowClimbingDown &&
                ladderTopPoint != null &&
                Vector3.Distance(characterLadder.transform.position, ladderTopPoint.position) <= snapDistance;
            
            currentCharacter = characterLadder;
            _isPlayerInsideTrigger = true;
            
            if (isTopEntry)
            {
                characterLadder.StartClimbingFromTop(this);
                if (debugLogs)
                    Debug.Log($"[LadderZone] Игрок вошёл на спусковую лестницу {ladderId} сверху");
            }
            else
            {
                characterLadder.StartClimbing(this, allowClimbingDown);
                if (debugLogs)
                    Debug.Log($"[LadderZone] Игрок вошёл на лестницу {ladderId} (подъём: {!allowClimbingDown})");
            }
            
            var wings = FindObjectOfType<WingsSystem>();
            if (wings != null) wings.OnLadderChanged(this);
            ForceHideGates();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var character = other.GetComponent<ExampleCharacterController>();
        if (character == null) return;
        if (currentCharacter != null && character != currentCharacter) return;
        
        Vector3 playerPosition = character.transform.position;
        _wasNearTopOnExit = (!allowClimbingDown && ladderTopPoint != null &&
            IsNearTop(playerPosition, topExitThreshold));
        
        if (debugLogs)
        {
            Debug.Log($"[LadderZone] Игрок вышел из лестницы {ladderId}");
            Debug.Log($"[LadderZone] Позиция игрока: {playerPosition}");
            Debug.Log($"[LadderZone] Вершина лестницы: {ladderTopPoint.position}");
            Debug.Log($"[LadderZone] Расстояние до вершины: {Vector3.Distance(playerPosition, ladderTopPoint.position)}");
            Debug.Log($"[LadderZone] Был рядом с вершиной: {_wasNearTopOnExit}");
        }
        
        if (character.CurrentCharacterState == CharacterState.Climbing)
        {
            _isPlayerInsideTrigger = false;
            if (debugLogs)
                Debug.Log($"[LadderZone] Игрок всё ещё лазает - переход на другую горку");
            return;
        }
        
        _isPlayerInsideTrigger = false;
        
        bool playerInConnectedLadder = false;
        if (connectedDescentLadder != null && connectedDescentLadder._isPlayerInsideTrigger)
            playerInConnectedLadder = true;
        if (connectedAscentLadder != null && connectedAscentLadder._isPlayerInsideTrigger)
            playerInConnectedLadder = true;
        if (connectedAscentLadder2 != null && connectedAscentLadder2._isPlayerInsideTrigger)
            playerInConnectedLadder = true;
        
        if (debugLogs)
            Debug.Log($"[LadderZone] Игрок в связанной горке: {playerInConnectedLadder}");
        
        if (!playerInConnectedLadder)
        {
            if (currentCharacter != null)
            {
                currentCharacter = null;
                var wings = FindObjectOfType<WingsSystem>();
                if (wings != null) wings.OnLadderChanged(null);
            }
            
            if (_wasNearTopOnExit)
            {
                if (debugLogs)
                    Debug.Log($"[LadderZone] Успешный подъём! Ворота появляются мгновенно");
                ShowGatesImmediately();
            }
            else
            {
                if (debugLogs)
                    Debug.Log($"[LadderZone] Обычный выход. Ворота появятся с задержкой");
                ShowGatesWithDelay();
            }
        }
        else
        {
            if (debugLogs)
                Debug.Log($"[LadderZone] Игрок перешёл на связанную горку - ворота не показываем");
            currentCharacter = null;
        }
    }

    private void OnDrawGizmos()
    {
        if (ladderTopPoint == null || ladderBottomPoint == null) return;
        
        Gizmos.color = gizmoColor;
        Gizmos.DrawLine(ladderBottomPoint.position, ladderTopPoint.position);
        Gizmos.DrawSphere(ladderBottomPoint.position, 0.2f);
        Gizmos.DrawSphere(ladderTopPoint.position, 0.2f);
        
        if (ladderTopPoint != null && !allowClimbingDown)
        {
            Gizmos.color = new Color(0, 1, 1, 0.2f);
            Gizmos.DrawSphere(ladderTopPoint.position, topExitThreshold);
        }
        
        Vector3 middlePoint = (ladderBottomPoint.position + ladderTopPoint.position) / 2;
        if (allowClimbingDown)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(middlePoint, middlePoint + Vector3.down * 1f);
        }
        Gizmos.color = Color.green;
        Gizmos.DrawLine(middlePoint, middlePoint + Vector3.up * 1f);
        
        if (TryGetComponent<BoxCollider>(out var boxCollider))
        {
            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        }
    }
}
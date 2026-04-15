using UnityEngine;
using KinematicCharacterController.Examples;
using YG;

public class LadderTrophyManager : MonoBehaviour
{
    [Header("Ссылки на горки")]
    [Tooltip("Лестница для подъёма (обычная)")]
    public LadderZone normalAscentLadder;
    [Tooltip("Лестница для подъёма (VIP)")]
    public LadderZone vipAscentLadder;
    [Tooltip("Лестница для спуска (allowClimbingDown = true)")]
    public LadderZone descentLadder;

    [Header("Триггер на дне спуска")]
    [Tooltip("Триггер (например, пустой GameObject с коллайдером) внизу спусковой горки. При входе в него кубок снова становится видимым.")]
    public Collider descentBottomTrigger;

    [Header("Ссылки на системы")]
    public WorldSystemManager worldManager;
    public ExampleCharacterController characterController;

    // Кэш последней использованной подъёмной лестницы
    private LadderZone lastUsedAscentLadder = null;

    // Сам кубок (объект, к которому прикреплён этот скрипт)
    private GameObject trophyObject;

    private void Awake()
    {
        trophyObject = gameObject;

        // Автоматически находим менеджеры, если не назначены
        if (worldManager == null)
            worldManager = FindObjectOfType<WorldSystemManager>();

        if (characterController == null)
            characterController = FindObjectOfType<ExampleCharacterController>();

        // Подписываемся на событие входа в триггер на дне спуска
        if (descentBottomTrigger != null)
        {
            // Добавляем компонент-слушатель, если его нет
            var triggerListener = descentBottomTrigger.GetComponent<DescentBottomTriggerListener>();
            if (triggerListener == null)
                triggerListener = descentBottomTrigger.gameObject.AddComponent<DescentBottomTriggerListener>();
            triggerListener.OnTriggerEnterEvent += OnDescentBottomTriggerEnter;
        }
        else
        {
            Debug.LogWarning("[LadderTrophyManager] descentBottomTrigger не назначен! Кубок не будет восстанавливаться после спуска.");
        }

        // Изначально кубок активен (ждёт первого подъёма)
        trophyObject.SetActive(true);
    }

    private void Update()
    {
        // Отслеживаем, на какой лестнице игрок находится в момент начала лазания
        if (characterController.CurrentCharacterState == CharacterState.Climbing && characterController.CurrentLadder != null)
        {
            LadderZone currentLadder = characterController.CurrentLadder;
            if (currentLadder == normalAscentLadder || currentLadder == vipAscentLadder)
            {
                // Запоминаем последнюю использованную подъёмную лестницу
                lastUsedAscentLadder = currentLadder;
                // (опционально) лог для отладки
                // Debug.Log($"[LadderTrophyManager] Игрок начал подъём по лестнице {currentLadder.LadderId}, награда: {currentLadder.TrophyRewardAmount}");
            }
        }
    }

    // Когда игрок касается кубка (на платформе сверху)
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return; // только игрок

        if (lastUsedAscentLadder == null)
        {
            Debug.LogWarning("[LadderTrophyManager] Игрок коснулся кубка, но нет запомненной подъёмной лестницы. Награда не выдана.");
            return;
        }

        // Получаем количество кубков за эту лестницу
        float reward = lastUsedAscentLadder.TrophyRewardAmount;
        if (reward > 0 && worldManager != null)
        {
            worldManager.AddTrophies(Mathf.FloorToInt(reward)); // преобразуем в int
            Debug.Log($"[LadderTrophyManager] Выдано {reward} кубков за подъём по лестнице {lastUsedAscentLadder.LadderId}");
        }

        // Делаем кубок невидимым (отключаем его)
        trophyObject.SetActive(false);
    }

    // Когда игрок входит в триггер внизу спусковой горки (завершил спуск)
    private void OnDescentBottomTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Восстанавливаем кубок, чтобы он снова мог выдать награду после следующего подъёма
        if (!trophyObject.activeSelf)
        {
            trophyObject.SetActive(true);
            Debug.Log("[LadderTrophyManager] Кубок восстановлен после спуска.");
        }

        // Опционально: сбрасываем lastUsedAscentLadder, чтобы при случайном касании кубка без подъёма ничего не выдавалось
        // lastUsedAscentLadder = null; // раскомментировать, если нужно строгое требование "только после подъёма"
    }

    // Этот метод можно вызвать, если нужно принудительно восстановить кубок (например, при загрузке)
    public void ResetTrophy()
    {
        if (!trophyObject.activeSelf)
            trophyObject.SetActive(true);
        lastUsedAscentLadder = null;
    }
}

// Вспомогательный компонент для прослушивания триггера на дне спуска
public class DescentBottomTriggerListener : MonoBehaviour
{
    public System.Action<Collider> OnTriggerEnterEvent;

    private void OnTriggerEnter(Collider other)
    {
        OnTriggerEnterEvent?.Invoke(other);
    }
}
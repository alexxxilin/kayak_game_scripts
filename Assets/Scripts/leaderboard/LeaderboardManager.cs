using UnityEngine;
using YG;

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    [Header("Настройки лидерборда")]
    [SerializeField] private string leaderboardKey = "test"; // Техническое название из консоли

    // Счётчик завершений лестниц (теперь хранится ТОЛЬКО в YG2.saves)
    private int ladderCompletionCount = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Загружаем значение из YG2.saves (оно уже должно быть загружено плагином)
        LoadFromCloud();
    }

    /// <summary>
    /// Загружает значение из облачного сохранения
    /// </summary>
    private void LoadFromCloud()
    {
        if (YG2.saves != null)
        {
            ladderCompletionCount = YG2.saves.ladderCompletionCount;
            Debug.Log($"[Leaderboard] Загружено из облака: {ladderCompletionCount} завершений");
        }
        else
        {
            ladderCompletionCount = 0;
            Debug.Log("[Leaderboard] Нет облачных сохранений, старт с 0");
        }
    }

    /// <summary>
    /// Устанавливает значение из облачного сохранения (вызывается из SaveManager)
    /// </summary>
    public void SetLadderCompletionCount(int value)
    {
        ladderCompletionCount = value;
        Debug.Log($"[Leaderboard] Установлено значение из SaveManager: {ladderCompletionCount}");
    }

    /// <summary>
    /// Вызывается из LadderZone, когда игрок достиг вершины.
    /// Увеличивает счётчик и отправляет НОВОЕ ЗНАЧЕНИЕ в лидерборд.
    /// </summary>
    public void AddLadderCompletion()
    {
        ladderCompletionCount++;

        // Отправляем ОБЩЕЕ количество в лидерборд Яндекс.Игр
        YG2.SetLeaderboard(leaderboardKey, ladderCompletionCount);

        Debug.Log($"[Leaderboard] Лестница пройдена. Всего: {ladderCompletionCount}. Отправлено в лидерборд '{leaderboardKey}'.");
        
        // НЕ сохраняем в PlayerPrefs! Только облако через SaveManager
    }

    // ===== ПУБЛИЧНЫЕ МЕТОДЫ =====
    public int GetLadderCompletionCount() => ladderCompletionCount;

    // Для отладки в редакторе
    [ContextMenu("Тест: Добавить завершение")]
    public void TestAddCompletion()
    {
        AddLadderCompletion();
    }

    [ContextMenu("Показать статистику")]
    public void ShowStats()
    {
        Debug.Log($"=== СТАТИСТИКА ЛИДЕРБОРДА ===");
        Debug.Log($"Завершения лестниц: {ladderCompletionCount}");
        Debug.Log($"Ключ лидерборда: {leaderboardKey}");
        Debug.Log($"Значение в YG2.saves: {YG2.saves?.ladderCompletionCount}");
    }
}
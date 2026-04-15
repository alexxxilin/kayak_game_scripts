using UnityEngine;

public class VisibilityController : MonoBehaviour
{
    [Header("Основные настройки")]
    
    [Tooltip("Объект, который нужно скрывать или показывать")]
    public GameObject targetObject;

    [Tooltip("Объект-триггер, при входе в который целевой объект ПОЯВЛЯЕТСЯ")]
    public GameObject showTriggerObject;

    [Tooltip("Объект-триггер, при входе в который целевой объект СКРЫВАЕТСЯ")]
    public GameObject hideTriggerObject;

    [Header("Настройки активации")]

    [Tooltip("Тег объекта (например, 'Player'), который должен заходить в триггеры. Оставьте пустым, если подходит любой объект")]
    public string actorTag = "Player";

    // Внутренние переменные для хранения компонентов коллайдеров
    private Collider showCollider;
    private Collider hideCollider;

    // Переменные для отслеживания состояния в предыдущем кадре (для детекции входа)
    private bool wasInShowZone = false;
    private bool wasInHideZone = false;

    private void Start()
    {
        // Получаем коллайдеры из назначенных объектов при запуске
        // ВАЖНО: Мы НЕ меняем активность targetObject здесь, чтобы сохранить состояние, заданное в редакторе
        if (showTriggerObject != null)
        {
            showCollider = showTriggerObject.GetComponent<Collider>();
            if (showCollider == null)
            {
                Debug.LogWarning("VisibilityController: У объекта показа (Show Trigger) нет компонента Collider!");
            }
            else if (!showCollider.isTrigger)
            {
                Debug.LogWarning("VisibilityController: У объекта показа (Show Trigger) не включена галочка Is Trigger!");
            }
        }

        if (hideTriggerObject != null)
        {
            hideCollider = hideTriggerObject.GetComponent<Collider>();
            if (hideCollider == null)
            {
                Debug.LogWarning("VisibilityController: У объекта скрытия (Hide Trigger) нет компонента Collider!");
            }
            else if (!hideCollider.isTrigger)
            {
                Debug.LogWarning("VisibilityController: У объекта скрытия (Hide Trigger) не включена галочка Is Trigger!");
            }
        }
    }

    private void FixedUpdate()
    {
        // Если целевой объект не назначен, ничего не делаем
        if (targetObject == null) return;

        // Проверяем, находится ли актер в зонах прямо сейчас
        bool isInShowZone = false;
        bool isInHideZone = false;

        if (showCollider != null && showCollider.enabled)
        {
            isInShowZone = IsObjectInCollider(showCollider);
        }

        if (hideCollider != null && hideCollider.enabled)
        {
            isInHideZone = IsObjectInCollider(hideCollider);
        }

        // ЛОГИКА СРАБАТЫВАНИЯ ТОЛЬКО ПРИ ВХОДЕ (Edge Detection)
        
        // Если только что вошли в зону показа (раньше не было, сейчас есть)
        if (isInShowZone && !wasInShowZone)
        {
            targetObject.SetActive(true);
        }

        // Если только что вошли в зону скрытия (раньше не было, сейчас есть)
        if (isInHideZone && !wasInHideZone)
        {
            targetObject.SetActive(false);
        }

        // Запоминаем текущее состояние для следующего кадра
        wasInShowZone = isInShowZone;
        wasInHideZone = isInHideZone;
    }

    // Вспомогательная функция для проверки наличия объекта с тегом внутри коллайдера
    private bool IsObjectInCollider(Collider collider)
    {
        // Получаем границы коллайдера в мировом пространстве
        Bounds bounds = collider.bounds;

        // Используем Physics.OverlapBox для проверки попадания объектов в эту область
        // Центр коробки - центр границ, Размер - размер границ (половинчатый для OverlapBox)
        Collider[] hitColliders = Physics.OverlapBox(bounds.center, bounds.size / 2);

        foreach (Collider hit in hitColliders)
        {
            // Игнорируем сам коллайдер триггера, чтобы не срабатывать на себя
            if (hit == collider) continue;

            // Если тег не задан, считаем что подходит любой объект, иначе проверяем тег
            if (string.IsNullOrEmpty(actorTag))
            {
                return true;
            }
            else
            {
                if (hit.CompareTag(actorTag))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Отрисовка границ в редакторе для удобства (не влияет на игру)
    private void OnDrawGizmos()
    {
        if (showTriggerObject != null)
        {
            Collider showCol = showTriggerObject.GetComponent<Collider>();
            if (showCol != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(showCol.bounds.center, showCol.bounds.size);
            }
        }

        if (hideTriggerObject != null)
        {
            Collider hideCol = hideTriggerObject.GetComponent<Collider>();
            if (hideCol != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(hideCol.bounds.center, hideCol.bounds.size);
            }
        }
    }
}
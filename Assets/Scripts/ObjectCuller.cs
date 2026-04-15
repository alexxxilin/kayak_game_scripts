using UnityEngine;

public class ObjectCuller : MonoBehaviour
{
    [Tooltip("Интервал проверки (сек)")]
    public float checkInterval = 0.1f;

    [Tooltip("Радиус, внутри которого объекты активны")]
    public float cullingRadius = 20f;

    private GameObject[] cullableObjects;
    private Transform playerTransform;

    void Start()
    {
        // Поиск игрока по тегу "Player"
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("ObjectCuller: Не найден игрок с тегом 'Player'!");
            return;
        }

        // Поиск всех объектов с тегом "Cullable"
        cullableObjects = GameObject.FindGameObjectsWithTag("Cullable");

        // Запуск периодической проверки
        InvokeRepeating(nameof(CullObjects), 0f, checkInterval);
    }

    void CullObjects()
    {
        if (playerTransform == null) return;

        foreach (GameObject obj in cullableObjects)
        {
            if (obj == null) continue;

            // Вычисление расстояния от игрока до объекта
            float distance = Vector3.Distance(playerTransform.position, obj.transform.position);
            bool isInRadius = distance <= cullingRadius;

            // Включение/выключение объекта в зависимости от расстояния
            obj.SetActive(isInRadius);
        }
    }
}
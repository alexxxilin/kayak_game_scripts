using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TowerBuilder : MonoBehaviour
{
    [Header("Основные параметры")]
    public float totalHeight = 10000f;        // Общая высота башни (метры)
    public float segmentHeight = 100f;        // Высота одного сегмента (метры)

    [Header("Наклон")]
    [Tooltip("Угол наклона башни от вертикали (градусы). 0 = вертикально.")]
    public float inclinationAngle = 0f;
    [Tooltip("Ось, вдоль которой происходит наклон (например, Vector3.right или Vector3.forward).")]
    public Vector3 inclinationAxis = Vector3.right;

    [Header("Первый сегмент (обязательный)")]
    [Tooltip("Префаб для самого нижнего сегмента (высота 0). Всегда используется один.")]
    public GameObject firstSegmentPrefab;

    [Header("Размещение всей башни")]
    public Vector3 basePosition = Vector3.zero;
    public Vector3 globalRotation = Vector3.zero;

    [Header("Настройка сегментов (поворот и масштаб)")]
    [Tooltip("Дополнительный поворот, применяемый ко всем сегментам (в градусах, локально).")]
    public Vector3 segmentRotationOffset = Vector3.zero;
    [Tooltip("Масштаб, применяемый ко всем сегментам. По умолчанию (1,1,1).")]
    public Vector3 segmentScale = Vector3.one;
    [Tooltip("Если включено, масштаб сегментов будет прогрессивно уменьшаться с высотой (от segmentScale до segmentScale * minScaleMultiplier).")]
    public bool scaleWithHeight = false;
    [Range(0.1f, 1f)]
    public float minScaleMultiplier = 0.5f;

    [Header("Зоны для остальных сегментов (начиная с высоты segmentHeight)")]
    public Zone[] zones;

    [System.Serializable]
    public class Zone
    {
        [Tooltip("Высота начала зоны (метры). Рекомендуется начинать с segmentHeight или выше.")]
        public float startHeight;
        public GameObject[] segmentPrefabs;
        public bool randomize = true;
        
        [Header("Переопределение для зоны (опционально)")]
        [Tooltip("Если не (0,0,0), этот поворот заменит глобальный segmentRotationOffset для сегментов этой зоны.")]
        public Vector3 customRotationOffset = Vector3.zero;
        [Tooltip("Если не (0,0,0), этот масштаб заменит глобальный segmentScale для сегментов этой зоны.")]
        public Vector3 customScale = Vector3.zero;
        [Tooltip("Использовать ли кастомные значения вместо глобальных.")]
        public bool useCustomTransform = false;
    }

    void Reset()
    {
        totalHeight = 10000f;
        segmentHeight = 100f;
        inclinationAngle = 0f;
        inclinationAxis = Vector3.right;
        firstSegmentPrefab = null;
        basePosition = Vector3.zero;
        globalRotation = Vector3.zero;
        segmentRotationOffset = Vector3.zero;
        segmentScale = Vector3.one;
        scaleWithHeight = false;
        minScaleMultiplier = 0.5f;
        zones = new Zone[]
        {
            new Zone { startHeight = 100f, segmentPrefabs = new GameObject[0] },
            new Zone { startHeight = 2000f, segmentPrefabs = new GameObject[0] },
            new Zone { startHeight = 5000f, segmentPrefabs = new GameObject[0] },
            new Zone { startHeight = 8000f, segmentPrefabs = new GameObject[0] }
        };
    }

    public void BuildTower()
    {
        ClearTower();

        if (segmentHeight <= 0)
        {
            Debug.LogError("Segment height must be > 0!");
            return;
        }

        if (firstSegmentPrefab == null)
        {
            Debug.LogError("First segment prefab is not assigned!");
            return;
        }

        // Устанавливаем глобальное положение и поворот корня
        transform.position = basePosition;
        transform.rotation = Quaternion.Euler(globalRotation);

        int totalSegments = Mathf.CeilToInt(totalHeight / segmentHeight);
        int builtCount = 0;

        for (int i = 0; i < totalSegments; i++)
        {
            float currentHeight = i * segmentHeight;
            if (currentHeight >= totalHeight) break;

            GameObject prefabToUse = null;

            if (i == 0)
            {
                prefabToUse = firstSegmentPrefab;
            }
            else
            {
                prefabToUse = GetPrefabForHeight(currentHeight);
            }

            if (prefabToUse == null) continue;

            GameObject segment = Instantiate(prefabToUse, transform);

            // Вычисляем позицию с учётом наклона
            float horizontalOffset = currentHeight * Mathf.Tan(inclinationAngle * Mathf.Deg2Rad);
            Vector3 localPos = new Vector3(
                horizontalOffset * inclinationAxis.x,
                currentHeight,
                horizontalOffset * inclinationAxis.z
            );
            segment.transform.localPosition = localPos;

            // Применяем поворот и масштаб
            ApplySegmentTransform(segment, currentHeight, i == 0);

            builtCount++;
        }

        Debug.Log($"✅ Башня построена: {builtCount} сегментов до {totalHeight}м (наклон {inclinationAngle}°)");
    }

    void ApplySegmentTransform(GameObject segment, float currentHeight, bool isFirstSegment)
    {
        Vector3 rotationOffset = segmentRotationOffset;
        Vector3 scale = segmentScale;

        // Если это не первый сегмент, проверяем зоны на кастомные значения
        if (!isFirstSegment && zones != null)
        {
            Zone activeZone = GetZoneForHeight(currentHeight);
            if (activeZone != null && activeZone.useCustomTransform)
            {
                if (activeZone.customRotationOffset != Vector3.zero)
                    rotationOffset = activeZone.customRotationOffset;
                if (activeZone.customScale != Vector3.zero)
                    scale = activeZone.customScale;
            }
        }

        // Применяем поворот
        segment.transform.localRotation = Quaternion.Euler(rotationOffset);

        // Применяем масштаб (с опцией уменьшения с высотой)
        if (scaleWithHeight && !isFirstSegment)
        {
            float t = Mathf.InverseLerp(0, totalHeight, currentHeight);
            float multiplier = Mathf.Lerp(1f, minScaleMultiplier, t);
            segment.transform.localScale = Vector3.Scale(scale, Vector3.one * multiplier);
        }
        else
        {
            segment.transform.localScale = scale;
        }
    }

    Zone GetZoneForHeight(float height)
    {
        for (int i = zones.Length - 1; i >= 0; i--)
        {
            if (height >= zones[i].startHeight)
            {
                return zones[i];
            }
        }
        return null;
    }

    public void ClearTower()
    {
        while (transform.childCount > 0)
        {
#if UNITY_EDITOR
            DestroyImmediate(transform.GetChild(0).gameObject);
#else
            Destroy(transform.GetChild(0).gameObject);
#endif
        }
    }

    GameObject GetPrefabForHeight(float height)
    {
        for (int i = zones.Length - 1; i >= 0; i--)
        {
            if (height >= zones[i].startHeight)
            {
                var prefabs = zones[i].segmentPrefabs;
                if (prefabs == null || prefabs.Length == 0) continue;

                if (zones[i].randomize)
                {
                    return prefabs[Random.Range(0, prefabs.Length)];
                }
                else
                {
                    int segmentIndex = Mathf.FloorToInt(height / segmentHeight);
                    return prefabs[segmentIndex % prefabs.Length];
                }
            }
        }
        Debug.LogWarning($"Нет префаба для высоты {height}м (сегмент не первый)");
        return null;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TowerBuilder))]
public class TowerBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("totalHeight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("segmentHeight"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Наклон", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("inclinationAngle"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("inclinationAxis"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("firstSegmentPrefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("basePosition"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("globalRotation"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Настройка сегментов", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("segmentRotationOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("segmentScale"));
        
        TowerBuilder builder = (TowerBuilder)target;
        builder.scaleWithHeight = EditorGUILayout.Toggle("Масштаб с высотой", builder.scaleWithHeight);
        if (builder.scaleWithHeight)
        {
            EditorGUI.indentLevel++;
            builder.minScaleMultiplier = EditorGUILayout.Slider("Мин. множитель масштаба", builder.minScaleMultiplier, 0.1f, 1f);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Зоны", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("zones"), true);

        serializedObject.ApplyModifiedProperties();

        GUILayout.Space(10);

        if (GUILayout.Button("🏗️ Построить башню"))
        {
            builder.BuildTower();
        }

        if (GUILayout.Button("🧹 Очистить"))
        {
            builder.ClearTower();
        }

        int expected = Mathf.CeilToInt(builder.totalHeight / builder.segmentHeight);
        GUILayout.Label($"Сегментов: {builder.transform.childCount} / {expected}");
    }
}
#endif
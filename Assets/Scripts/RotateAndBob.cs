using UnityEngine;

public class SmoothRotateAndBob : MonoBehaviour
{
    public enum RotationMode
    {
        WorldY,     // Вокруг глобальной оси Y
        LocalY,     // Вокруг локальной оси Y
        CustomAxis  // Вокруг произвольной оси
    }

    [Header("Настройки вращения")]
    [SerializeField] private RotationMode rotationMode = RotationMode.WorldY;
    [SerializeField] private float rotationSpeed = 30f;
    [SerializeField] private Vector3 customRotationAxis = Vector3.up; // Для CustomAxis
    
    [Header("Настройки вертикального движения")]
    [SerializeField] private float bobHeight = 0.5f;
    [SerializeField] private float bobSpeed = 1f;
    [SerializeField] private bool smoothBobbing = true;
    
    private Vector3 localStartPosition;
    private float bobTime;

    void Start()
    {
        localStartPosition = transform.localPosition;
    }

    void Update()
    {
        HandleRotation();
        HandleBobbing();
    }

    private void HandleRotation()
    {
        switch (rotationMode)
        {
            case RotationMode.WorldY:
                // Вращение вокруг глобальной оси Y
                transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.World);
                break;
                
            case RotationMode.LocalY:
                // Вращение вокруг локальной оси Y
                transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.Self);
                break;
                
            case RotationMode.CustomAxis:
                // Вращение вокруг произвольной оси
                transform.Rotate(customRotationAxis.normalized * rotationSpeed * Time.deltaTime, Space.Self);
                break;
        }
    }

    private void HandleBobbing()
    {
        bobTime += Time.deltaTime * bobSpeed;
        
        float verticalOffset = smoothBobbing 
            ? Mathf.Cos(bobTime) * bobHeight
            : Mathf.Sin(bobTime) * bobHeight;
        
        Vector3 newLocalPosition = localStartPosition;
        newLocalPosition.y += verticalOffset;
        transform.localPosition = newLocalPosition;
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            localStartPosition = new Vector3(
                localStartPosition.x,
                localStartPosition.y,
                localStartPosition.z
            );
        }
    }

    public void ResetLocalPosition()
    {
        localStartPosition = transform.localPosition;
    }
}
using UnityEngine;

public class BeaconBillboard : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        // Найти камеру по тегу MainCamera
        GameObject camObj = GameObject.FindGameObjectWithTag("MainCamera");
        if (camObj != null)
        {
            mainCamera = camObj.GetComponent<Camera>();
        }
        else
        {
            Debug.LogWarning("BeaconBillboard: Camera with tag 'MainCamera' not found!");
        }
    }

    void LateUpdate()
    {
        if (mainCamera != null)
        {
            // Направление от спрайта к камере (без учёта Y)
            Vector3 direction = mainCamera.transform.position - transform.position;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                // Поворачиваем спрайт так, чтобы он смотрел в сторону камеры, но оставался горизонтальным
                Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = targetRot;
            }
        }
    }
}
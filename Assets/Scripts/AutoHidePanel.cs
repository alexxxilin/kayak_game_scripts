using UnityEngine;

public class AutoHidePanel : MonoBehaviour
{
    public float delay = 3f; // Время в секундах до скрытия

    void OnEnable()
    {
        Invoke(nameof(Hide), delay);
    }

    void OnDisable()
    {
        CancelInvoke();
    }

    void Hide()
    {
        gameObject.SetActive(false);
    }
}
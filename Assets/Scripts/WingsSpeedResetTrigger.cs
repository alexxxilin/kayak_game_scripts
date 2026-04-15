using UnityEngine;
using KinematicCharacterController.Examples;

public class WingsSpeedResetTrigger : MonoBehaviour
{
    private ExampleCharacterController player;
    private float originalWingsMultiplier = 1f;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            player = other.GetComponent<ExampleCharacterController>();
            if (player != null)
            {
                // Сохраняем текущий множитель и устанавливаем 1
                originalWingsMultiplier = player.GetWingsSpeedMultiplier();
                player.SetWingsSpeedMultiplier(1f);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && player != null)
        {
            // Восстанавливаем оригинальный множитель
            player.SetWingsSpeedMultiplier(originalWingsMultiplier);
            player = null;
        }
    }
}
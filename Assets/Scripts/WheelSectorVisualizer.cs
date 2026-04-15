using UnityEngine;
using UnityEngine.UI;

public class WheelSectorVisualizer : MonoBehaviour
{
    public FortuneWheel fortuneWheel;
    public Image wheelImage;

    private void Start()
    {
        if (fortuneWheel != null && wheelImage != null)
        {
            CreateSectorVisuals();
        }
    }

    private void CreateSectorVisuals()
    {
        // Этот метод можно использовать для визуального разделения секторов на колесе
        // Создает дочерние объекты с изображениями секторов
    }

    [ContextMenu("Update Wheel Visuals")]
    public void UpdateWheelVisuals()
    {
        // Метод для обновления визуала колеса в редакторе
    }
}
using UnityEngine;
using UnityEngine.UI;
using System;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class CoinFlyAnimation : MonoBehaviour
{
    private RectTransform _rectTransform;
    private Image _image;
    private CanvasGroup _canvasGroup;
    private bool _isAnimating = false;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _image = GetComponent<Image>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Запускает анимацию полёта монеты
    /// </summary>
    /// <param name="startPos">Позиция старта (даже если спрайт скрыт)</param>
    /// <param name="controlPoint">Контрольная точка для дуги</param>
    /// <param name="endPos">Конечная позиция (счётчик монет)</param>
    /// <param name="duration">Длительность полёта</param>
    /// <param name="onComplete">Коллбэк после завершения</param>
    public void StartAnimation(Vector2 startPos, Vector2 controlPoint, Vector2 endPos, float duration, System.Action onComplete = null)
    {
        if (_isAnimating) return;
        
        _isAnimating = true;
        gameObject.SetActive(true);
        _rectTransform.anchoredPosition = startPos;
        _canvasGroup.alpha = 1f;
        _rectTransform.localScale = Vector3.one * 1.5f; // Начинаем увеличенными
        
        StartCoroutine(Animate(startPos, controlPoint, endPos, duration, onComplete));
    }

    private System.Collections.IEnumerator Animate(Vector2 startPos, Vector2 controlPoint, Vector2 endPos, float duration, System.Action onComplete)
    {
        float elapsedTime = 0f;
        Quaternion startRotation = _rectTransform.rotation;
        Quaternion endRotation = Quaternion.Euler(0, 0, 720f); // Два оборота
        
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            
            // Квадратичная кривая Безье для плавной дуги
            Vector2 position = QuadraticBezier(startPos, controlPoint, endPos, t);
            _rectTransform.anchoredPosition = position;
            
            // Вращение
            _rectTransform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
            
            // Анимация масштаба: 1.5 → 1.2 → 0.5 + исчезновение
            float scale;
            if (t < 0.2f)
            {
                scale = Mathf.Lerp(1.5f, 1.2f, t / 0.2f);
            }
            else if (t < 0.7f)
            {
                scale = 1.2f;
            }
            else
            {
                float fadeT = (t - 0.7f) / 0.3f;
                scale = Mathf.Lerp(1.2f, 0.5f, fadeT);
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeT);
            }
            _rectTransform.localScale = Vector3.one * scale;
            
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }
        
        // Финальная очистка
        _rectTransform.anchoredPosition = endPos;
        _rectTransform.localScale = Vector3.one * 0.3f;
        _canvasGroup.alpha = 0f;
        
        _isAnimating = false;
        gameObject.SetActive(false);
        
        onComplete?.Invoke();
    }

    // Квадратичная кривая Безье: P(t) = (1-t)^2 * P0 + 2*(1-t)*t*P1 + t^2 * P2
    private Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        return uu * p0 + 2 * u * t * p1 + tt * p2;
    }
}
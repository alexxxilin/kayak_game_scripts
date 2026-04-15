using UnityEngine;
using System.Collections;

public class FinalLampAnimation : MonoBehaviour
{
    [Header("Ссылки")]
    public Transform lampCap;          // колпак (начальный поворот: -90 по X)
    public GameObject lampBulb;        // лампочка (Renderer + Light) — появляется ПОЗЖЕ
    public Transform lampBase;         // основание — появляется СРАЗУ

    [Header("Спрайт маяка")]
    public SpriteRenderer beaconSprite; // спрайт — появляется ПОЗЖЕ

    [Header("Точка эффекта искр")]
    [Tooltip("Пустышка (Transform), где будут появляться искры. Если не задана — используется позиция lampBase.")]
    public Transform sparkEffectPoint;

    [Header("Настройки анимации")]
    public float capRotationSpeed = 180f;
    public float lampDescentDistance = 0.5f;
    public float lampDescentSpeed = 1f;
    public float blinkInterval = 0.5f;
    public float capCloseDelay = 3f;   // задержка перед закрытием колпака

    [Header("Эффект искр")]
    public bool enableSparks = true;
    [Range(0f, 1f)] public float sparkTriggerProgress = 0.9f;
    public GameObject sparkEffectPrefab;
    public AudioClip sparkSound;
    public float sparkVolume = 0.5f;

    private Vector3 originalLampPosition;
    private Quaternion originalCapRotation;
    private bool isAnimating = false;
    private bool isBlinking = false;
    private Coroutine blinkCoroutine;

    void Start()
    {
        if (lampBase != null)
            originalLampPosition = lampBase.position;
        if (lampCap != null)
            originalCapRotation = lampCap.localRotation;

        // Скрыть всё при старте
        HideAllLampParts();
    }

    [ContextMenu("▶️ Запустить анимацию лампы")]
    public void StartFinalAnimation()
    {
        if (isAnimating) return;
        isAnimating = true;
        StartCoroutine(PlayAnimation());
    }

    public void StopAnimation()
    {
        isAnimating = false;
        isBlinking = false;
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        HideAllLampParts();
    }

    private IEnumerator PlayAnimation()
    {
        // === 1. ОТКРЫТЬ КОЛПАК ===
        if (lampCap != null)
        {
            Quaternion openRotation = originalCapRotation * Quaternion.Euler(90, 0, 0);
            while (Quaternion.Angle(lampCap.localRotation, openRotation) > 1f)
            {
                lampCap.localRotation = Quaternion.RotateTowards(lampCap.localRotation, openRotation, capRotationSpeed * Time.deltaTime);
                yield return null;
            }
            lampCap.localRotation = openRotation;
        }

        // === 2. ПОКАЗАТЬ ОСНОВАНИЕ И ОПУСТИТЬ ЕГО ===
        if (lampBase != null)
        {
            lampBase.gameObject.SetActive(true); // ← ПОЯВЛЯЕТСЯ СРАЗУ

            Vector3 startPosition = lampBase.position;
            Vector3 targetPosition = startPosition + Vector3.down * lampDescentDistance;
            float totalDistance = Vector3.Distance(startPosition, targetPosition);
            bool sparksPlayed = false;

            if (totalDistance <= 0.01f)
            {
                if (enableSparks)
                {
                    PlaySparkEffect(sparkEffectPoint != null ? sparkEffectPoint : lampBase);
                    sparksPlayed = true;
                }
            }
            else
            {
                while (Vector3.Distance(lampBase.position, targetPosition) > 0.01f)
                {
                    lampBase.position = Vector3.MoveTowards(lampBase.position, targetPosition, lampDescentSpeed * Time.deltaTime);

                    if (!sparksPlayed && enableSparks)
                    {
                        float currentDistance = Vector3.Distance(lampBase.position, startPosition);
                        float progress = currentDistance / totalDistance;
                        if (progress >= sparkTriggerProgress)
                        {
                            PlaySparkEffect(sparkEffectPoint != null ? sparkEffectPoint : lampBase);
                            sparksPlayed = true;
                        }
                    }

                    yield return null;
                }

                lampBase.position = targetPosition;

                if (enableSparks && !sparksPlayed)
                {
                    PlaySparkEffect(sparkEffectPoint != null ? sparkEffectPoint : lampBase);
                }
            }
        }

        // === 3. ЖДАТЬ, ПОТОМ ЗАКРЫТЬ КОЛПАК ===
        yield return new WaitForSeconds(capCloseDelay);

        if (lampCap != null)
        {
            while (Quaternion.Angle(lampCap.localRotation, originalCapRotation) > 1f)
            {
                lampCap.localRotation = Quaternion.RotateTowards(lampCap.localRotation, originalCapRotation, capRotationSpeed * Time.deltaTime);
                yield return null;
            }
            lampCap.localRotation = originalCapRotation;
        }

        // === 4. ТОЛЬКО СЕЙЧАС ПОКАЗЫВАЕМ ЛАМПОЧКУ И/ИЛИ СПРАЙТ ===
        ShowLampLight(true);

        if (beaconSprite != null || lampBulb != null)
        {
            isBlinking = true;
            blinkCoroutine = StartCoroutine(BlinkLight());
        }

        isAnimating = false;
    }

    private void PlaySparkEffect(Transform parent)
    {
        if (sparkEffectPrefab == null || parent == null) return;

        // Создаём эффект в мировой позиции родителя
        GameObject sparks = Instantiate(sparkEffectPrefab, parent.position, Quaternion.identity);
        
        // Делаем дочерним, сохраняя мировую позицию
        sparks.transform.SetParent(parent, true);
        
        // Обнуляем ЛОКАЛЬНУЮ позицию и поворот → эффект будет (0,0,0) и направлен вверх
        sparks.transform.localPosition = Vector3.zero;
        sparks.transform.localRotation = Quaternion.identity;

        var ps = sparks.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play();
            Destroy(sparks, ps.main.duration + 0.5f);
        }

        if (sparkSound != null)
        {
            AudioSource.PlayClipAtPoint(sparkSound, parent.position, sparkVolume);
        }
    }

    private IEnumerator BlinkLight()
    {
        bool useLampBulb = lampBulb != null;
        bool useBeaconSprite = beaconSprite != null;

        while (isBlinking)
        {
            // Fade IN
            if (useLampBulb) lampBulb.SetActive(true);
            if (useBeaconSprite)
            {
                yield return StartCoroutine(FadeSprite(beaconSprite, 0f, 1f, blinkInterval));
            }
            else
            {
                yield return new WaitForSeconds(blinkInterval);
            }

            // Fade OUT
            if (useLampBulb) lampBulb.SetActive(false);
            if (useBeaconSprite)
            {
                yield return StartCoroutine(FadeSprite(beaconSprite, 1f, 0f, blinkInterval));
            }
            else
            {
                yield return new WaitForSeconds(blinkInterval);
            }
        }

        // Финальное выключение
        ShowLampLight(false);
    }

    private IEnumerator FadeSprite(SpriteRenderer renderer, float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, alpha);
            yield return null;
        }
        renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, endAlpha);
    }

    private void ShowLampLight(bool visible)
    {
        if (lampBulb != null)
        {
            lampBulb.SetActive(visible);
        }

        if (beaconSprite != null)
        {
            beaconSprite.enabled = visible;
            if (!visible)
                beaconSprite.color = new Color(1, 1, 1, 0);
        }
    }

    private void HideAllLampParts()
    {
        if (lampBase != null)
        {
            lampBase.gameObject.SetActive(false);
            lampBase.position = originalLampPosition;
        }

        if (lampCap != null)
        {
            lampCap.localRotation = originalCapRotation;
        }

        ShowLampLight(false);
    }
}
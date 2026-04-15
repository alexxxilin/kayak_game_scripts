using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using KinematicCharacterController.Examples;
using YG;

public class TimedRewardsSystem : MonoBehaviour
{
    [System.Serializable]
    public class RewardButton
    {
        public Button button;
        public TMP_Text buttonText;
        public Image rewardImage;
        public float unlockTime = 10f;

        [Header("Награда")]
        public RewardType rewardType;
        public long rewardAmount = 100;
    }

    public enum RewardType
    {
        Coins
    }

    [Header("Основные настройки")]
    public GameObject rewardsPanel;
    public Button openPanelButton;
    public Button closePanelButton;

    [Header("Кнопки наград")]
    public List<RewardButton> rewardButtons = new List<RewardButton>();

    [Header("Глобальный индикатор")]
    public TMP_Text globalTimerText;
    public TMP_Text globalReadyText;
    public Image globalActiveIndicator;

    private bool isPanelOpen = false;
    private ExampleCharacterController playerController;
    private Coroutine globalTimerCoroutine;

    void Start()
    {
        Debug.Log("=== TimedRewardsSystem Start ===");
        playerController = FindFirstObjectByType<ExampleCharacterController>();
        if (playerController == null)
        {
            Debug.LogError("ExampleCharacterController не найден на сцене!");
        }
        InitializeButtons();
        SetupUIEvents();
        if (rewardsPanel != null)
            rewardsPanel.SetActive(false);
        InitializeGlobalIndicators();
        StartAllTimers();
        StartGlobalTimer();
    }

    void InitializeGlobalIndicators()
    {
        if (globalTimerText != null)
        {
            globalTimerText.text = "00:00:00";
            globalTimerText.gameObject.SetActive(true);
        }
        if (globalReadyText != null)
        {
            globalReadyText.gameObject.SetActive(false);
        }
        if (globalActiveIndicator != null)
            globalActiveIndicator.gameObject.SetActive(true);
    }

    void InitializeButtons()
    {
        Debug.Log($"Инициализация {rewardButtons.Count} кнопок");
        for (int i = 0; i < rewardButtons.Count; i++)
        {
            if (rewardButtons[i].button == null || rewardButtons[i].buttonText == null)
            {
                Debug.LogError($"Кнопка {i} не назначена корректно!");
                continue;
            }
            int index = i;
            rewardButtons[i].button.onClick.AddListener(() => OnButtonClick(index));
            rewardButtons[i].button.interactable = false;
            rewardButtons[i].buttonText.text = "00:00:00";
            if (rewardButtons[i].rewardImage != null)
            {
                rewardButtons[i].rewardImage.gameObject.SetActive(false);
            }
            Debug.Log($"Кнопка {i} настроена: {rewardButtons[i].rewardType} x{rewardButtons[i].rewardAmount}");
        }
    }

    void SetupUIEvents()
    {
        if (openPanelButton != null)
        {
            openPanelButton.onClick.AddListener(OpenPanel);
        }
        if (closePanelButton != null)
        {
            closePanelButton.onClick.AddListener(ClosePanel);
        }
    }

    void OpenPanel()
    {
        if (rewardsPanel != null && !isPanelOpen)
        {
            rewardsPanel.SetActive(true);
            isPanelOpen = true;
        }
    }

    void ClosePanel()
    {
        if (rewardsPanel != null && isPanelOpen)
        {
            rewardsPanel.SetActive(false);
            isPanelOpen = false;
        }
    }

    void StartAllTimers()
    {
        Debug.Log("Запуск всех таймеров");
        for (int i = 0; i < rewardButtons.Count; i++)
        {
            StartCoroutine(ButtonTimer(i));
        }
    }

    IEnumerator ButtonTimer(int buttonIndex)
    {
        var rewardButton = rewardButtons[buttonIndex];
        float timer = rewardButton.unlockTime;
        Debug.Log($"Таймер кнопки {buttonIndex} запущен: {timer} сек");
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            UpdateButtonText(buttonIndex, timer);
            yield return null;
        }
        rewardButton.button.interactable = true;
        rewardButton.buttonText.text = GetLocalizedText("get");
        if (rewardButton.rewardImage != null)
        {
            rewardButton.rewardImage.gameObject.SetActive(true);
        }
        Debug.Log($"Кнопка {buttonIndex} разблокирована!");
    }

    void UpdateButtonText(int buttonIndex, float timeLeft)
    {
        int hours = Mathf.FloorToInt(timeLeft / 3600);
        int minutes = Mathf.FloorToInt((timeLeft % 3600) / 60);
        int seconds = Mathf.FloorToInt(timeLeft % 60);
        rewardButtons[buttonIndex].buttonText.text = string.Format("{0:D2}:{1:D2}:{2:D2}", hours, minutes, seconds);
    }

    void OnButtonClick(int buttonIndex)
    {
        Debug.Log($"=== НАЖАТИЕ КНОПКИ {buttonIndex} ===");
        var rewardButton = rewardButtons[buttonIndex];
        ProcessReward(rewardButton);
        rewardButton.button.interactable = false;
        rewardButton.buttonText.text = GetLocalizedText("collected");
        if (rewardButton.rewardImage != null)
        {
            rewardButton.rewardImage.gameObject.SetActive(false);
        }
        Debug.Log("Награда выдана успешно!");
    }

    void ProcessReward(RewardButton rewardButton)
    {
        if (playerController == null)
        {
            Debug.LogError("PlayerController не найден!");
            return;
        }
        switch (rewardButton.rewardType)
        {
            case RewardType.Coins:
                playerController.AddCoins(rewardButton.rewardAmount);
                Debug.Log($"Начислено монет: {rewardButton.rewardAmount}. Всего: {playerController.CoinsCollected}");
                break;
        }
    }

    string GetLocalizedText(string key)
    {
        bool isEnglish = YG2.lang == "en";
        switch (key)
        {
            case "get":
                return isEnglish ? "GET!" : "ПОЛУЧИТЬ!";
            case "collected":
                return isEnglish ? "COLLECTED" : "ПОЛУЧЕНО";
            default:
                return isEnglish ? key.ToUpper() : key.ToUpper();
        }
    }

    void StartGlobalTimer()
    {
        if (globalTimerCoroutine != null)
            StopCoroutine(globalTimerCoroutine);
        globalTimerCoroutine = StartCoroutine(GlobalTimerUpdate());
    }

    IEnumerator GlobalTimerUpdate()
    {
        while (true)
        {
            UpdateGlobalTimer();
            yield return new WaitForSeconds(1f);
        }
    }

    void UpdateGlobalTimer()
    {
        if (globalTimerText == null && globalReadyText == null) return;
        bool anyButtonActive = false;
        foreach (var rewardButton in rewardButtons)
        {
            if (rewardButton.button.interactable)
            {
                anyButtonActive = true;
                break;
            }
        }

        if (anyButtonActive)
        {
            if (globalReadyText != null) globalReadyText.gameObject.SetActive(true);
            if (globalTimerText != null) globalTimerText.gameObject.SetActive(false);
        }
        else
        {
            if (globalReadyText != null) globalReadyText.gameObject.SetActive(false);
            if (globalTimerText != null) globalTimerText.gameObject.SetActive(true);
            float minTimeLeft = GetMinActiveTimer();
            if (minTimeLeft > 0)
            {
                int hours = Mathf.FloorToInt(minTimeLeft / 3600);
                int minutes = Mathf.FloorToInt((minTimeLeft % 3600) / 60);
                int seconds = Mathf.FloorToInt(minTimeLeft % 60);
                if (globalTimerText != null)
                {
                    globalTimerText.text = string.Format("{0:D2}:{1:D2}:{2:D2}", hours, minutes, seconds);
                }
            }
            else
            {
                if (globalTimerText != null)
                {
                    globalTimerText.text = "00:00:00";
                }
            }
        }

        if (globalActiveIndicator != null)
        {
            globalActiveIndicator.gameObject.SetActive(anyButtonActive);
        }
    }

    float GetMinActiveTimer()
    {
        float minTime = float.MaxValue;
        bool foundActiveTimer = false;
        for (int i = 0; i < rewardButtons.Count; i++)
        {
            if (!rewardButtons[i].button.interactable && rewardButtons[i].buttonText.text != GetLocalizedText("collected"))
            {
                string timeText = rewardButtons[i].buttonText.text;
                if (timeText.Contains(":"))
                {
                    string[] timeParts = timeText.Split(':');
                    if (timeParts.Length == 3 &&
                        int.TryParse(timeParts[0], out int hours) &&
                        int.TryParse(timeParts[1], out int minutes) &&
                        int.TryParse(timeParts[2], out int seconds))
                    {
                        float totalSeconds = hours * 3600 + minutes * 60 + seconds;
                        if (totalSeconds < minTime)
                        {
                            minTime = totalSeconds;
                            foundActiveTimer = true;
                        }
                    }
                }
            }
        }
        return foundActiveTimer ? minTime : 0f;
    }

    [ContextMenu("Принудительно разблокировать все кнопки")]
    void UnlockAllButtons()
    {
        Debug.Log("Принудительная разблокировка всех кнопок");
        for (int i = 0; i < rewardButtons.Count; i++)
        {
            rewardButtons[i].button.interactable = true;
            rewardButtons[i].buttonText.text = GetLocalizedText("get");
            if (rewardButtons[i].rewardImage != null)
            {
                rewardButtons[i].rewardImage.gameObject.SetActive(true);
            }
        }
        StopAllCoroutines();
    }

    [ContextMenu("Сбросить все таймеры")]
    void ResetAllTimers()
    {
        Debug.Log("Сброс всех таймеров");
        StopAllCoroutines();
        for (int i = 0; i < rewardButtons.Count; i++)
        {
            rewardButtons[i].button.interactable = false;
            rewardButtons[i].buttonText.text = "00:00:00";
            if (rewardButtons[i].rewardImage != null)
            {
                rewardButtons[i].rewardImage.gameObject.SetActive(false);
            }
        }
        StartAllTimers();
    }

    void OnDestroy()
    {
        if (globalTimerCoroutine != null)
            StopCoroutine(globalTimerCoroutine);
    }
}
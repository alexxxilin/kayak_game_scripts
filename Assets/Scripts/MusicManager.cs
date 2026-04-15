using UnityEngine;
using YG;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("Фоновая музыка")]
    public AudioSource backgroundMusicSource; // ← должно быть назначено в инспекторе

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Подписка на паузу/возобновление от Yandex Games
        YG2.onPauseGame += OnGamePauseResume;

        // Автозапуск музыки (если нужно)
        if (backgroundMusicSource != null && !backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.Play();
        }
    }

    private void OnDestroy()
    {
        YG2.onPauseGame -= OnGamePauseResume;
    }

    private void OnGamePauseResume(bool isPaused)
    {
        if (backgroundMusicSource == null) return;

        if (isPaused)
        {
            backgroundMusicSource.Pause();
        }
        else
        {
            backgroundMusicSource.UnPause();
        }
    }
}

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Music Settings")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private List<AudioClip> menuMusic;
    
    private List<AudioClip> playbackQueue = new List<AudioClip>();
    private float originalVolume;
    private bool isInGame = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeMusicQueue();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.OnGameStarted += HandleGameStarted;
            GameSettingsManager.Instance.OnMusicVolumeChanged += HandleVolumeChanged;
            
            // Установка начальной громкости
            if (musicSource != null)
            {
                originalVolume = GameSettingsManager.Instance.MusicVolume;
                musicSource.volume = originalVolume;
            }
        }

        PlayNextSong();
    }

    private void OnDestroy()
    {
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.OnGameStarted -= HandleGameStarted;
            GameSettingsManager.Instance.OnMusicVolumeChanged -= HandleVolumeChanged;
        }
    }

    private void Update()
    {
        // Проверяем, закончилась ли текущая песня
        if (musicSource != null && !musicSource.isPlaying)
        {
            PlayNextSong();
        }
    }

    private void InitializeMusicQueue()
    {
        // Очищаем и заполняем очередь перемешанными песнями
        playbackQueue.Clear();
        playbackQueue = menuMusic.OrderBy(x => Random.value).ToList();
    }

    private void PlayNextSong()
    {
        if (menuMusic == null || menuMusic.Count == 0 || musicSource == null)
            return;

        // Если очередь пуста, создаём новую перемешанную очередь
        if (playbackQueue.Count == 0)
        {
            InitializeMusicQueue();
        }

        // Берём первую песню из очереди и проигрываем её
        AudioClip nextSong = playbackQueue[0];
        playbackQueue.RemoveAt(0);

        musicSource.clip = nextSong;
        musicSource.Play();
    }

    private void HandleGameStarted()
    {
        isInGame = true;
        if (musicSource != null)
        {
            // Уменьшаем громкость на 50% при входе в игру
            musicSource.volume = originalVolume * 0.5f;
        }
    }

    private void HandleVolumeChanged(float volume)
    {
        if (musicSource != null)
        {
            originalVolume = volume;
            // Если мы в игре, применяем уменьшенную громкость
            musicSource.volume = isInGame ? volume * 0.5f : volume;
        }
    }
} 
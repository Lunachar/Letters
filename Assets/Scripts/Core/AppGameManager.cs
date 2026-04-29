using UnityEngine;

public class AppGameManager : MonoBehaviour
{
    public static AppGameManager Instance { get; private set; }

    [Header("Shared configs")]
    [SerializeField] private MainMenuConfig mainMenuConfig;
    [SerializeField] private StoryGameConfig storyGameConfig;

    [Header("Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip[] menuMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;
    [SerializeField] private bool playMenuMusicOnStart = true;
    [SerializeField] private bool loopMusic = true;

    [Header("UI sounds")]
    [SerializeField] private AudioSource soundSource;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.8f;

    [Header("Eye tracker defaults")]
    [SerializeField, Min(0.1f)] private float defaultDwellSeconds = 1.1f;

    public MainMenuConfig MainMenuConfig => mainMenuConfig;
    public StoryGameConfig StoryGameConfig => storyGameConfig;
    public float DefaultDwellSeconds => defaultDwellSeconds;
    public float MusicVolume => musicVolume;
    public float SoundVolume => soundVolume;

    private int menuMusicIndex;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureAudioSources();
            ApplyVolumes();
            return;
        }

        Destroy(gameObject);
    }

    private void Start()
    {
        if (playMenuMusicOnStart)
        {
            PlayMenuMusic();
        }
    }

    private void OnValidate()
    {
        ApplyVolumes();
    }

    public void PlayMenuMusic()
    {
        if (musicSource == null || menuMusic == null || menuMusic.Length == 0)
        {
            return;
        }

        AudioClip clip = menuMusic[Mathf.Clamp(menuMusicIndex, 0, menuMusic.Length - 1)];
        menuMusicIndex = (menuMusicIndex + 1) % menuMusic.Length;
        PlayMusic(clip, loopMusic);
    }

    public void PlayMusic(AudioClip clip, bool loop)
    {
        if (musicSource == null || clip == null)
        {
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    public void PlayButtonClick()
    {
        if (soundSource != null && buttonClickSound != null)
        {
            soundSource.PlayOneShot(buttonClickSound, soundVolume);
        }
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        ApplyVolumes();
    }

    public void SetSoundVolume(float value)
    {
        soundVolume = Mathf.Clamp01(value);
        ApplyVolumes();
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        if (soundSource == null)
        {
            soundSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.playOnAwake = false;
        soundSource.playOnAwake = false;
    }

    private void ApplyVolumes()
    {
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }

        if (soundSource != null)
        {
            soundSource.volume = soundVolume;
        }
    }
}

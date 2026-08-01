using UnityEngine;

public class AppGameManager : MonoBehaviour
{
    public static AppGameManager Instance { get; private set; }
    private const string MusicVolumePrefsKey = "AppGameManager.MusicVolume";
    private const string SoundVolumePrefsKey = "AppGameManager.SoundVolume";
    private const string EffectsVolumePrefsKey = "AppGameManager.EffectsVolume";
    private const string SpeechVolumePrefsKey = "AppGameManager.SpeechVolume";
    private const string FeedbackVoiceVolumePrefsKey = "AppGameManager.FeedbackVoiceVolume";

    [Header("Shared configs")]
    [SerializeField] private MainMenuConfig mainMenuConfig;
    [SerializeField] private StoryGameConfig storyGameConfig;
    [SerializeField] private TopicsGameConfig topicsGameConfig;
    [SerializeField] private ShopGameConfig shopGameConfig;
    [SerializeField] private TrainGameConfig trainGameConfig;

    [Header("Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip[] menuMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;
    [SerializeField] private bool playMenuMusicOnStart = true;
    [SerializeField] private bool loopMusic = true;

    [Header("UI sounds")]
    [SerializeField] private AudioSource soundSource;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip correctAnswerSound;
    [SerializeField] private AudioClip wrongAnswerSound;
    [SerializeField] private AudioClip celebrationSound;
    [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.8f;

    [Header("Speech")]
    [SerializeField, Range(0f, 1f)] private float speechVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float feedbackVoiceVolume = 0.9f;

    [Header("Eye tracker defaults")]
    [SerializeField, Min(0.1f)] private float defaultDwellSeconds = 1.1f;

    public MainMenuConfig MainMenuConfig => mainMenuConfig;
    public StoryGameConfig StoryGameConfig => storyGameConfig;
    public TopicsGameConfig TopicsGameConfig => topicsGameConfig;
    public ShopGameConfig ShopGameConfig => shopGameConfig;
    public TrainGameConfig TrainGameConfig => trainGameConfig;
    public float DefaultDwellSeconds => defaultDwellSeconds;
    public float MusicVolume => musicVolume;
    public float SoundVolume => effectsVolume;
    public float EffectsVolume => effectsVolume;
    public float SpeechVolume => speechVolume;
    public float FeedbackVoiceVolume => feedbackVoiceVolume;
    public AudioClip CorrectAnswerSound => correctAnswerSound;
    public AudioClip WrongAnswerSound => wrongAnswerSound;
    public AudioClip CelebrationSound => celebrationSound;

    private int menuMusicIndex;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LockLandscapeOrientation();
            EnsureAudioSources();
            EnsureGazePointer();
            LoadSavedVolumes();
            ApplyVolumes();
            return;
        }

        Destroy(gameObject);
    }

    private void Start()
    {
        LockLandscapeOrientation();

        if (playMenuMusicOnStart)
        {
            PlayMenuMusic();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            LockLandscapeOrientation();
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
        PlaySound(buttonClickSound);
    }

    public void PlayCorrectAnswer()
    {
        PlaySound(correctAnswerSound);
    }

    public void PlayWrongAnswer()
    {
        PlaySound(wrongAnswerSound);
    }

    public void PlayCelebration()
    {
        PlaySound(celebrationSound);
    }

    public void PlaySound(AudioClip clip)
    {
        if (soundSource != null && clip != null)
        {
            soundSource.PlayOneShot(clip, effectsVolume);
        }
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumePrefsKey, musicVolume);
        PlayerPrefs.Save();
        ApplyVolumes();
    }

    public void SetSoundVolume(float value)
    {
        SetEffectsVolume(value);
    }

    public void SetEffectsVolume(float value)
    {
        effectsVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(EffectsVolumePrefsKey, effectsVolume);
        PlayerPrefs.Save();
        ApplyVolumes();
    }

    public void SetSpeechVolume(float value)
    {
        speechVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SpeechVolumePrefsKey, speechVolume);
        PlayerPrefs.Save();
    }

    public void SetFeedbackVoiceVolume(float value)
    {
        feedbackVoiceVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(FeedbackVoiceVolumePrefsKey, feedbackVoiceVolume);
        PlayerPrefs.Save();
    }

    private void LoadSavedVolumes()
    {
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePrefsKey, musicVolume));
        effectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsVolumePrefsKey, PlayerPrefs.GetFloat(SoundVolumePrefsKey, effectsVolume)));
        speechVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SpeechVolumePrefsKey, speechVolume));
        feedbackVoiceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(FeedbackVoiceVolumePrefsKey, feedbackVoiceVolume));
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
            soundSource.volume = effectsVolume;
        }
    }

    private void EnsureGazePointer()
    {
        if (FindObjectOfType<GazePointer>() == null)
        {
            gameObject.AddComponent<GazePointer>();
        }
    }

    private void LockLandscapeOrientation()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
    }
}

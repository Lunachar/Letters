using System;
using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("Core Systems")]
    [SerializeField] private InputManager inputManager;
    [SerializeField] private LetterDisplayManager letterDisplay;
    [SerializeField] private EnvironmentScroller environment;
    [SerializeField] private ContentDatabase contentDatabase;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource soundSource;
    [SerializeField] private GameSettingsManager gameSettingsManager;
    [SerializeField] private LetterChallengeController letterChallengeController;
    [SerializeField] private LetterLessonController letterLessonController;
    
    [Header("Settings")]
    [SerializeField] private bool autoPronounceLetter = true;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSystems();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        gameSettingsManager.OnGameModeChanged += OnGameModeChanged;
    }

    private void OnGameModeChanged(GameMode mode)
    {
        if (mode == GameMode.LetterChallenge)
        {
            StartCoroutine(WaitAndStartChallenge());
        }
        else if (mode == GameMode.LetterLessons)
        {
            letterLessonController.StartLesson();
        }
        else
        {
            letterChallengeController.StopChallenge();
        }
    }

    private IEnumerator WaitAndStartChallenge()
    {
        // Активируем CanvasTask, если неактивен
        if (!letterChallengeController.gameObject.activeSelf)
            letterChallengeController.gameObject.SetActive(true);

        // Ждём, пока он действительно станет активен в иерархии
        yield return new WaitUntil(() => letterChallengeController.gameObject.activeInHierarchy);

        // Стартуем игру
        letterChallengeController.StartChallenge(contentDatabase.GetLetters());
    }


    private void InitializeSystems()
    {
        // Ensure all required components are present
        if (inputManager == null)
            inputManager = GetComponentInChildren<InputManager>();
            
        if (letterDisplay == null)
            letterDisplay = GetComponentInChildren<LetterDisplayManager>();
            
        if (environment == null)
            environment = GetComponentInChildren<EnvironmentScroller>();
            
        // Validate core components
        if (inputManager == null || letterDisplay == null || environment == null || contentDatabase == null)
        {
            Debug.LogError("GameManager: Missing required components!");
            enabled = false;
            return;
        }
        
    }
    
    public void SetAutoPronounce(bool enable)
    {
        autoPronounceLetter = enable;
    }
    
    public bool GetAutoPronounce()
    {
        return autoPronounceLetter;
    }
    // public void PlaySound(AudioClip clip)
    // {
    //     if (clip != null && soundSource != null)
    //     {
    //         soundSource.PlayOneShot(clip);
    //     }
    // }

    public void SetSoundVolume(float volume)
    {
        if (soundSource != null)
        {
            soundSource.volume = volume;
        }
    }

    public void SetMusicVolume(float volume)
    {
        if (musicSource != null)
        {
            musicSource.volume = volume;
        }
    }
} 
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

    [Header("Menu Canvas")]
    [SerializeField] private Canvas settingsCanvas;
    
    [Header("Character Selection")]
    [SerializeField] private ToggleGroup characterToggleGroup;
    [SerializeField] private Toggle rectangleToggle;
    [SerializeField] private Toggle humanToggle;
    [SerializeField] private Toggle carToggle;
    [SerializeField] private Toggle busToggle;
    [SerializeField] private Toggle trainToggle;
    
    [Header("Display Mode")]
    [SerializeField] private ToggleGroup displayModeToggleGroup;
    [SerializeField] private Toggle soundOnlyToggle;
    [SerializeField] private Toggle soundAndObjectsToggle;
    
    // [Header("Game Mode")]
    // [SerializeField] private ToggleGroup gameModeToggleGroup;
    // [SerializeField] private Toggle freePlayToggle;
    // [SerializeField] private Toggle letterModeToggle;
    // [SerializeField] private Toggle letterLessonsToggle;
    
    [Header("Volume Controls")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider soundVolumeSlider;
    
    [Header("Buttons")]
    [SerializeField] private Button freePlayButton;
    [SerializeField] private Button letterChallengeButton;
    [SerializeField] private Button letterLessonsButton;
    [SerializeField] private Button quitButton;
    
    [Header("Game Mode Canvases")]
    [SerializeField] private Canvas freePlayCanvas;
    [SerializeField] private Canvas letterChallengeCanvas;
    [SerializeField] private Canvas letterLessonsCanvas;
    
    // События для оповещения других компонентов об изменении настроек
    public event Action<CharacterType> OnCharacterChanged;
    public event Action<DisplayMode> OnDisplayModeChanged;
    public event Action<GameMode> OnGameModeChanged;
    public event Action<float> OnMusicVolumeChanged;
    public event Action<float> OnSoundVolumeChanged;
    public event Action OnGameStarted;

    // Текущие настройки
    public CharacterType CurrentCharacter { get; private set; }
    public DisplayMode CurrentDisplayMode { get; private set; }
    
    public GameMode CurrentGameMod { get; private set; }
    public float MusicVolume { get; private set; }
    public float SoundVolume { get; private set; }
    
    private float lastEscPressTime = 0f;
    private const float doubleTapInterval = 0.3f;
    private bool isGameRunning = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (isGameRunning && Input.GetKeyDown(KeyCode.Escape))
        {
            float currentTime = Time.time;
            if (currentTime - lastEscPressTime < doubleTapInterval)
            {
                ShowSettings();
                isGameRunning = false;
            }
            lastEscPressTime = currentTime;
        }

        if (!isGameRunning)
        {
            HandleCharacterHotkeys();
        }
    }

    private void ShowModeCanvas(GameMode mode)
    {
        settingsCanvas?.gameObject.SetActive(false);
        letterChallengeCanvas?.gameObject.SetActive(false);
        freePlayCanvas?.gameObject.SetActive(false);
        letterLessonsCanvas?.gameObject.SetActive(false);

        switch (mode)
        {
            case GameMode.Settings:
                settingsCanvas?.gameObject.SetActive(true);
                break;
            case GameMode.FreePlay:
                //settingsCanvas?.gameObject.SetActive(false);
                freePlayCanvas?.gameObject.SetActive(true);
                break;
            case GameMode.LetterChallenge:
                //settingsCanvas?.gameObject.SetActive(false);
                letterChallengeCanvas?.gameObject.SetActive(true);
                break;
            case GameMode.LetterLessons:
                //settingsCanvas?.gameObject.SetActive(false);
                letterLessonsCanvas?.gameObject.SetActive(true);
                break;
        }
    }
    private void HandleCharacterHotkeys()
    {
            if (Input.GetKeyDown(KeyCode.Alpha1)) rectangleToggle.isOn = true;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) humanToggle.isOn = true;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) carToggle.isOn = true;
            else if (Input.GetKeyDown(KeyCode.Alpha4)) busToggle.isOn = true;
            else if (Input.GetKeyDown(KeyCode.Alpha5)) trainToggle.isOn = true;
    }

    private void InitializeSettings()
    {
        // Отключаем возможность снять выделение со всех переключателей
        if (characterToggleGroup != null)
        {
            characterToggleGroup.allowSwitchOff = false;
        }
        if (displayModeToggleGroup != null)
        {
            displayModeToggleGroup.allowSwitchOff = false;
        }

        // Убеждаемся, что все Toggle'ы привязаны к своим группам
        SetupToggle(rectangleToggle, characterToggleGroup, CharacterType.Rectangle);
        SetupToggle(humanToggle, characterToggleGroup, CharacterType.Human);
        SetupToggle(carToggle, characterToggleGroup, CharacterType.Car);
        SetupToggle(busToggle, characterToggleGroup, CharacterType.Bus);
        SetupToggle(trainToggle, characterToggleGroup, CharacterType.Train);

        SetupToggle(soundOnlyToggle, displayModeToggleGroup, DisplayMode.SoundOnly);
        SetupToggle(soundAndObjectsToggle, displayModeToggleGroup, DisplayMode.SoundAndObjects);

        // Настраиваем слайдеры громкости
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        }
        if (soundVolumeSlider != null)
        {
            soundVolumeSlider.onValueChanged.AddListener(SetSoundVolume);
        }

        // Настраиваем кнопки
        if (freePlayButton != null)
            freePlayButton.onClick.AddListener(() => StartGameWithMode(GameMode.FreePlay));
        if (letterChallengeButton != null)
            letterChallengeButton.onClick.AddListener(() => StartGameWithMode(GameMode.LetterChallenge));
        if (letterLessonsButton != null)
            letterLessonsButton.onClick.AddListener(() => StartGameWithMode(GameMode.LetterLessons));

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(() =>
            {
                SoundManager.Instance?.PlayButtonClick();
                QuitGame();
            });
        }

        // Устанавливаем начальные значения
        rectangleToggle.isOn = true;
        soundAndObjectsToggle.isOn = true;
        if (musicVolumeSlider != null) musicVolumeSlider.value = 0.4f;
        if (soundVolumeSlider != null) soundVolumeSlider.value = 1.0f;
    }

    private void StartGameWithMode(GameMode mode)
    {
        SoundManager.Instance?.PlayButtonClick();
        SetGameMode(mode);
        StartGame();
    }


    private void SetupToggle(Toggle toggle, ToggleGroup group, Enum value)
    {
        if (toggle != null)
        {
            toggle.group = group;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                {
                    if (value is CharacterType character)
                        SelectCharacter(toggle, character);
                    else if (value is DisplayMode mode)
                        SetDisplayMode(mode);
                    // else if (value is GameMode gameMode)
                    //     SetGameMode(gameMode);
                }
            });
        }
    }

    private void SetGameMode(GameMode gameMode)
    {
        CurrentGameMod = gameMode;
        OnGameModeChanged?.Invoke(gameMode);
    }

    private void SelectCharacter(Toggle toggle, CharacterType character)
    {
        toggle.isOn = true;
        SetCharacter(character);
        SoundManager.Instance?.PlayButtonClick();
        AnimateSelection(toggle.gameObject);
    }

    private void AnimateSelection(GameObject obj)
    {
        StopAllCoroutines();
        StartCoroutine(PunchScale(obj.transform, 1.2f, 0.2f));
    }

    private IEnumerator PunchScale(Transform target, float punch, float duration)
    {
        Vector3 original = target.localScale;
        Vector3 targetScale = original * punch;
        float halfDuration = duration / 2f;
        float t = 0f;

        // Увеличение
        while (t < halfDuration)
        {
            target.localScale = Vector3.Lerp(original, targetScale, t / halfDuration);
            t += Time.deltaTime;
            yield return null;
        }
        target.localScale = targetScale;

        // Возврат
        t = 0f;
        while (t < halfDuration)
        {
            target.localScale = Vector3.Lerp(targetScale, original, t / halfDuration);
            t += Time.deltaTime;
            yield return null;
        }
        target.localScale = original;
    }


    private void SetCharacter(CharacterType character)
    {
        CurrentCharacter = character;
        OnCharacterChanged?.Invoke(character);
    }

    private void SetDisplayMode(DisplayMode mode)
    {
        CurrentDisplayMode = mode;
        OnDisplayModeChanged?.Invoke(mode);
    }

    private void SetMusicVolume(float volume)
    {
        MusicVolume = volume;
        OnMusicVolumeChanged?.Invoke(volume);
    }

    private void SetSoundVolume(float volume)
    {
        SoundVolume = volume;
        OnSoundVolumeChanged?.Invoke(volume);
    }

    private void StartGame()
    {
        ShowModeCanvas(CurrentGameMod);
        isGameRunning = true;
        OnGameStarted?.Invoke();
    }
    private void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void ShowSettings()
    {
        settingsCanvas.gameObject.SetActive(true);
        freePlayCanvas?.gameObject.SetActive(false);
        letterChallengeCanvas?.gameObject.SetActive(false);
        letterLessonsCanvas?.gameObject.SetActive(false);
    }
}

public enum CharacterType
{
    Rectangle,
    Human,
    Car,
    Bus,
    Train
}

public enum DisplayMode
{
    SoundOnly,
    SoundAndObjects
}

public enum GameMode
{
    Settings,
    FreePlay,
    LetterChallenge,
    LetterLessons
}
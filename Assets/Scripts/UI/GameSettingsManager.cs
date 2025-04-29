using UnityEngine;
using UnityEngine.UI;
using System;

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
    
    [Header("Volume Controls")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider soundVolumeSlider;
    
    [Header("Start Button")]
    [SerializeField] private Button startButton;

    // События для оповещения других компонентов об изменении настроек
    public event Action<CharacterType> OnCharacterChanged;
    public event Action<DisplayMode> OnDisplayModeChanged;
    public event Action<float> OnMusicVolumeChanged;
    public event Action<float> OnSoundVolumeChanged;
    public event Action OnGameStarted;

    // Текущие настройки
    public CharacterType CurrentCharacter { get; private set; }
    public DisplayMode CurrentDisplayMode { get; private set; }
    public float MusicVolume { get; private set; }
    public float SoundVolume { get; private set; }

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

        // Настраиваем кнопку старта
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartGame);
        }

        // Устанавливаем начальные значения
        rectangleToggle.isOn = true;
        soundAndObjectsToggle.isOn = true;
        if (musicVolumeSlider != null) musicVolumeSlider.value = 0.7f;
        if (soundVolumeSlider != null) soundVolumeSlider.value = 1.0f;
    }

    private void SetupToggle(Toggle toggle, ToggleGroup group, System.Enum value)
    {
        if (toggle != null)
        {
            toggle.group = group;
            toggle.onValueChanged.RemoveAllListeners();
            if (value is CharacterType characterType)
            {
                toggle.onValueChanged.AddListener(isOn => { if (isOn) SetCharacter(characterType); });
            }
            else if (value is DisplayMode displayMode)
            {
                toggle.onValueChanged.AddListener(isOn => { if (isOn) SetDisplayMode(displayMode); });
            }
        }
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
        settingsCanvas.gameObject.SetActive(false);
        OnGameStarted?.Invoke();
    }

    public void ShowSettings()
    {
        settingsCanvas.gameObject.SetActive(true);
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
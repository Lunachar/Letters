using UnityEngine;

public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }

    [Header("Character Prefabs")]
    [SerializeField] private GameObject rectanglePrefab;
    [SerializeField] private GameObject humanPrefab;
    [SerializeField] private GameObject carPrefab;
    [SerializeField] private GameObject busPrefab;
    [SerializeField] private GameObject trainPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnPoint;

    private GameObject currentCharacter;

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
        }
    }

    private void Start()
    {
        if (GameSettingsManager.Instance != null)
        {
            // Подписываемся на событие изменения персонажа
            GameSettingsManager.Instance.OnCharacterChanged += HandleCharacterChanged;
            GameSettingsManager.Instance.OnGameStarted += HandleGameStarted;

            // Создаем начальный персонаж
            HandleCharacterChanged(GameSettingsManager.Instance.CurrentCharacter);
        }
    }

    private void OnDestroy()
    {
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.OnCharacterChanged -= HandleCharacterChanged;
            GameSettingsManager.Instance.OnGameStarted -= HandleGameStarted;
        }
    }

    private void HandleCharacterChanged(CharacterType characterType)
    {
        // Удаляем текущего персонажа, если он есть
        if (currentCharacter != null)
        {
            Destroy(currentCharacter);
        }

        // Создаем нового персонажа
        GameObject prefab = GetPrefabForCharacterType(characterType);
        if (prefab != null && spawnPoint != null)
        {
            currentCharacter = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            currentCharacter.SetActive(false); // Скрываем до начала игры
        }
    }

    private void HandleGameStarted()
    {
        if (currentCharacter != null)
        {
            currentCharacter.SetActive(true); // Показываем персонажа при старте игры
        }
    }

    private GameObject GetPrefabForCharacterType(CharacterType characterType)
    {
        return characterType switch
        {
            CharacterType.Rectangle => rectanglePrefab,
            CharacterType.Human => humanPrefab,
            CharacterType.Car => carPrefab,
            CharacterType.Bus => busPrefab,
            CharacterType.Train => trainPrefab,
            _ => null
        };
    }
} 
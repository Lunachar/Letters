using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("Core Systems")]
    [SerializeField] private InputManager inputManager;
    [SerializeField] private LetterDisplayManager letterDisplay;
    [SerializeField] private EnvironmentScroller environment;
    [SerializeField] private ContentDatabase contentDatabase;
    
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
} 
using UnityEngine;

[CreateAssetMenu(fileName = "TopicsGameConfig", menuName = "Letters/Topics/Config")]
public class TopicsGameConfig : ScriptableObject
{
    [Header("Content")]
    public TopicRoomData[] startingRooms;
    public string menuSceneName = "MainMenuScene";

    [Header("Topic grid")]
    public string title = "Темы";
    public string subtitle = "Выбери комнату";
    public int gridColumns = 3;
    public float visibleRows = 2.45f;
    public Vector2 cardSize = new Vector2(520f, 285f);
    public Vector2 cardSpacing = new Vector2(34f, 34f);

    [Header("Timing")]
    [Min(0.1f)] public float dwellSeconds = 1.1f;
    [Min(1f)] public float noAnswerRepeatDelay = 8f;
    [Min(0)] public int maxQuestionRepeats = 2;
    [Min(0.1f)] public float feedbackDelay = 1.4f;
    [Min(0.5f)] public float resultAutoReturnDelay = 5f;

    [Header("Speech defaults")]
    public bool useTextToSpeech = true;
    [Range(0f, 1f)] public float speechVolume = 1f;
    [Range(0.5f, 2f)] public float speechRate = 0.95f;
    [Range(0.5f, 2f)] public float speechPitch = 1f;
    public string androidLanguage = "ru_RU";

    [Header("Sounds")]
    public AudioClip buttonClickSound;
    public AudioClip correctAnswerSound;
    public AudioClip wrongAnswerSound;
    public AudioClip celebrationSound;
    [Range(0f, 1f)] public float soundVolume = 0.85f;
    public string[] correctFeedbackPhrases = { "Правильно!", "Отлично!", "Верно!" };
    public string[] wrongFeedbackPhrases = { "Ничего страшного. Попробуем дальше.", "Почти. Слушаем следующий вопрос." };

    [Header("Creator")]
    public string creatorButtonLabel = "Редактор";
    public string creatorTitle = "Редактор тем";
    public bool onlineDraftGeneratorEnabled = true;
    public string generatorEndpoint = "";

    [Header("Style")]
    public Color backgroundColor = new Color(0.10f, 0.18f, 0.21f);
    public Color panelColor = new Color(0.08f, 0.13f, 0.15f, 0.9f);
    public Color primaryColor = new Color(0.95f, 0.67f, 0.24f);
    public Color textColor = Color.white;
    public Color correctColor = new Color(0.28f, 0.84f, 0.38f);
    public Color wrongColor = new Color(0.94f, 0.25f, 0.25f);
}

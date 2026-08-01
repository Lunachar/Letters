using UnityEngine;

[CreateAssetMenu(fileName = "LettersGameConfig", menuName = "Letters/Game Config")]
public class LettersGameConfig : ScriptableObject
{
    [Header("Lessons")]
    [TextArea(1, 2)] public string lessonOrder = "АОУИМПРСТНКЛЕВДБГЯЗЫЧЙЖШЮЦЩЭХФЪЬЁ";
    [Min(1)] public int initialRepetition = 10;
    [Min(0)] public int mixedRepetition = 10;
    [Range(1, 6)] public int visibleLetterCount = 3;

    [Header("Timing")]
    [Min(0f)] public float firstTaskDelay = 0.2f;
    [Min(0f)] public float taskIntroDelay = 1f;
    [Min(0.5f)] public float firstAnswerWaitSeconds = 4f;
    [Min(1f)] public float repeatQuestionInterval = 8f;
    [Min(0f)] public float correctFeedbackDelay = 0.35f;
    [Min(0f)] public float nextLetterDelay = 1f;
    [Min(0f)] public float lessonCompleteDelay = 1f;
    [Range(0.05f, 0.4f)] public float pressFeedbackDuration = 0.12f;
    [Range(0.90f, 1f)] public float pressScale = 0.96f;
    [Range(0.05f, 0.6f)] public float cardMoveDuration = 0.22f;
    [Range(0.05f, 0.5f)] public float cardSpawnDuration = 0.16f;
    [Range(0.05f, 0.5f)] public float correctPulseDuration = 0.24f;
    [Range(1f, 1.18f)] public float correctPulseScale = 1.08f;

    [Header("Audio")]
    public AudioClip taskSound;
    public AudioClip correctSound;
    public AudioClip wrongSound;
    public AudioClip praiseSound;
    public AudioClip lessonCompleteSound;

    [Header("Text")]
    public string challengeInstruction = "Послушай букву и нажми её на клавиатуре";
    public string lessonInstruction = "Нажимай подсвеченную клавишу";
    public string miniKeyboardTitle = "Найди клавишу";

    [Header("Layout")]
    public Vector2 letterCardSize = new Vector2(220f, 260f);
    public bool useConfiguredLetterLayout = true;
    public Vector2 letterAreaAnchorMin = new Vector2(0.18f, 0.24f);
    public Vector2 letterAreaAnchorMax = new Vector2(0.82f, 0.56f);
    public Vector2 keyboardAnchorMin = new Vector2(0.08f, 0.72f);
    public Vector2 keyboardAnchorMax = new Vector2(0.92f, 0.96f);
    [Min(24)] public int letterFontSize = 150;
    [Min(12)] public int keyboardFontSize = 30;

    [Header("Colors")]
    public Color trainingBackgroundColor = new Color(0.07f, 0.15f, 0.17f, 0.92f);
    public Color panelColor = new Color(0.10f, 0.20f, 0.22f, 0.88f);
    public Color cardColor = new Color(0.96f, 0.98f, 0.94f, 1f);
    public Color currentCardColor = new Color(1.00f, 0.86f, 0.36f, 1f);
    public Color correctColor = new Color(0.18f, 0.70f, 0.40f, 1f);
    public Color wrongColor = new Color(0.88f, 0.22f, 0.20f, 1f);
    public Color cardTextColor = new Color(0.05f, 0.08f, 0.09f, 1f);
    public Color invertedTextColor = Color.white;
    public Color keyboardPanelColor = new Color(0.04f, 0.10f, 0.12f, 0.94f);
    public Color keyboardKeyColor = new Color(0.88f, 0.93f, 0.90f, 1f);
    public Color keyboardTargetColor = new Color(1.00f, 0.81f, 0.24f, 1f);
    public Color keyboardPressedCorrectColor = new Color(0.18f, 0.70f, 0.40f, 1f);
    public Color keyboardPressedWrongColor = new Color(0.88f, 0.22f, 0.20f, 1f);
    public Color keyboardTextColor = new Color(0.05f, 0.08f, 0.09f, 1f);
    public Color mutedTextColor = new Color(0.78f, 0.86f, 0.84f, 1f);

    public char[] GetLessonOrder()
    {
        if (string.IsNullOrWhiteSpace(lessonOrder))
        {
            return "АОУИМПРСТНКЛЕВДБГЯЗЫЧЙЖШЮЦЩЭХФЪЬЁ".ToCharArray();
        }

        string upper = lessonOrder.ToUpperInvariant();
        string unique = string.Empty;
        for (int i = 0; i < upper.Length; i++)
        {
            char letter = upper[i];
            if (!char.IsLetter(letter) || unique.IndexOf(letter) >= 0)
            {
                continue;
            }

            unique += letter;
        }

        return unique.Length > 0 ? unique.ToCharArray() : "АОУИМПРС".ToCharArray();
    }
}

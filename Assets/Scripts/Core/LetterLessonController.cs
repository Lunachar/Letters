using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class LetterLessonController : MonoBehaviour
{
    [Header("UI")]
    public GameObject letterPrefab;
    public Transform letterQueueParent;
    public RectTransform[] spawnPoints = new RectTransform[3];
    public Slider lessonProgressSlider;
    public TextMeshProUGUI lessonTitle;
    public GameObject lessonUI;

    [Header("Config")]
    [SerializeField] private LettersGameConfig config;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip correctSound;
    public AudioClip lessonCompleteSound;

    private Queue<char> letterQueue = new();
    private List<LetterCardView> letterObjects = new();
    private List<char> fullSequence;
    private int currentStep = 0;
    private bool transitioning;
    private Coroutine feedbackRoutine;
    private KeyboardHintView keyboardHint;
    private Image trainingBackdrop;
    private RectTransform letterCardLayer;

    private LessonManager lessonManager;

    private void Start()
    {
        lessonManager = FindObjectOfType<LessonManager>();
        ResolveConfig();
        if (lessonManager != null)
        {
            lessonManager.ApplyConfig(config);
        }

        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPhysicalKeyPressed += HandlePhysicalKeyPressed;
        }
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPhysicalKeyPressed -= HandlePhysicalKeyPressed;
        }
    }

    public void StartLesson()
    {
        ResolveConfig();
        lessonManager = LessonManager.Instance != null ? LessonManager.Instance : FindObjectOfType<LessonManager>();
        if (lessonManager == null)
        {
            Debug.LogError("LetterLessonController: LessonManager is missing.");
            return;
        }

        lessonManager.ApplyConfig(config);
        lessonManager.LoadProgress();

        var lesson = lessonManager.CurrentLesson;
        if (lesson == null)
        {
            Debug.Log("All lessons complete.");
            lessonUI.SetActive(false);
            return;
        }

        fullSequence = new List<char>(lesson.GetSequence());
        currentStep = 0;
        transitioning = false;

        lessonUI.SetActive(true);
        EnsureTrainingUi();
        lessonTitle.text = config != null ? $"{lesson.Title}. {config.lessonInstruction}" : lesson.Title;
        lessonProgressSlider.maxValue = fullSequence.Count;
        lessonProgressSlider.value = 0;

        ClearLetterObjects();
        letterQueue.Clear();

        int visibleCount = config != null ? Mathf.Max(1, config.visibleLetterCount) : 3;
        if (!LettersTrainingLayout.UsesConfiguredLayout(config) && spawnPoints != null && spawnPoints.Length > 0)
        {
            visibleCount = Mathf.Min(visibleCount, spawnPoints.Length);
        }
        for (int i = 0; i < visibleCount && i < fullSequence.Count; i++)
        {
            char c = fullSequence[i];
            letterQueue.Enqueue(c);
            CreateLetterVisual(c);
        }

        UpdateLetterPositions();
        RefreshCurrentLetter();
    }

    private void HandlePhysicalKeyPressed(Key key)
    {
        if (lessonUI == null || !lessonUI.activeSelf || transitioning || letterQueue.Count == 0)
        {
            return;
        }

        if (!RussianKeyboardLayout.TryGetLetterForKey(key, out char input))
        {
            return;
        }

        char current = letterQueue.Peek();

        if (char.ToUpperInvariant(input) == char.ToUpperInvariant(current))
        {
            transitioning = true;
            PlayConfiguredSound(config != null && config.correctSound != null ? config.correctSound : correctSound);
            keyboardHint?.ShowCorrect();
            if (letterObjects.Count > 0)
            {
                letterObjects[0].SetState(LetterCardState.Correct);
                letterObjects[0].PlayCorrectFeedback();
            }

            StartCoroutine(AdvanceAfterCorrect());
            return;
        }

        if (!PlayConfiguredSound(config != null ? config.wrongSound : null))
        {
            SoundManager.Instance?.PlayErrorSound();
        }
        keyboardHint?.ShowError(key);
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
        }

        feedbackRoutine = StartCoroutine(FlashCurrentCard(LetterCardState.Wrong));
    }

    public void OnKeyPressed(char input)
    {
        if (!RussianKeyboardLayout.TryGetKeyForLetter(input, out Key key))
        {
            return;
        }

        HandlePhysicalKeyPressed(key);
    }

    private void CreateLetterVisual(char letter)
    {
        LetterCardView card = LetterCardView.Create(ResolveLetterCardParent(), letterPrefab, letter, config);
        letterObjects.Add(card);
        card.PlaySpawnFeedback();
    }

    private void UpdateLetterPositions()
    {
        if (LettersTrainingLayout.TryPositionCards(letterObjects, ResolveLetterCardParent(), config))
        {
            return;
        }

        for (int i = 0; i < letterObjects.Count && i < spawnPoints.Length; i++)
        {
            RectTransform rt = letterObjects[i].RectTransform;
            letterObjects[i].SetLayout(spawnPoints[i].anchoredPosition, rt.sizeDelta, true);

            SpawnPointSettings settings = spawnPoints[i].GetComponent<SpawnPointSettings>();
            if (settings != null)
                settings.ApplyTo(letterObjects[i].gameObject);
        }
    }

    private void RefreshCurrentLetter()
    {
        for (int i = 0; i < letterObjects.Count; i++)
        {
            letterObjects[i].SetState(i == 0 ? LetterCardState.Current : LetterCardState.Normal);
        }

        if (letterQueue.Count > 0)
        {
            keyboardHint?.ShowTarget(letterQueue.Peek());
        }
    }

    private void ClearLetterObjects()
    {
        foreach (var obj in letterObjects)
        {
            if (obj != null)
            {
                Destroy(obj.gameObject);
            }
        }

        letterObjects.Clear();
    }

    private void OnLessonComplete()
    {
        PlayConfiguredSound(config != null && config.lessonCompleteSound != null ? config.lessonCompleteSound : lessonCompleteSound);
        StartCoroutine(CompleteAndAdvanceLesson());
    }

    private IEnumerator CompleteAndAdvanceLesson()
    {
        yield return new WaitForSeconds(config != null ? config.lessonCompleteDelay : 1f);

        lessonManager.AdvanceLesson();

        if (lessonManager.CurrentLesson != null)
        {
            StartLesson();
        }
        else
        {
            Debug.Log("All lessons complete!");
            lessonUI.SetActive(false);
        }
    }

    public void ResetProgress()
    {
        if (lessonManager != null)
        {
            lessonManager.ResetProgress();
        }
    }

    private IEnumerator AdvanceAfterCorrect()
    {
        yield return new WaitForSeconds(config != null ? config.correctFeedbackDelay : 0.35f);

        currentStep++;
        lessonProgressSlider.value = currentStep;

        if (letterQueue.Count > 0)
        {
            letterQueue.Dequeue();
        }

        if (letterObjects.Count > 0)
        {
            Destroy(letterObjects[0].gameObject);
            letterObjects.RemoveAt(0);
        }

        if (currentStep + letterQueue.Count < fullSequence.Count)
        {
            char newChar = fullSequence[currentStep + letterQueue.Count];
            letterQueue.Enqueue(newChar);
            CreateLetterVisual(newChar);
        }

        UpdateLetterPositions();
        transitioning = false;

        if (currentStep >= fullSequence.Count)
        {
            OnLessonComplete();
        }
        else
        {
            RefreshCurrentLetter();
        }
    }

    private IEnumerator FlashCurrentCard(LetterCardState state)
    {
        if (letterObjects.Count == 0)
        {
            yield break;
        }

        letterObjects[0].SetState(state);
        letterObjects[0].PlayPressFeedback();
        yield return new WaitForSeconds(config != null ? config.correctFeedbackDelay : 0.35f);
        RefreshCurrentLetter();
        feedbackRoutine = null;
    }

    private void ResolveConfig()
    {
        if (config == null && GameManager.Instance != null)
        {
            config = GameManager.Instance.LettersConfig;
        }

        if (config == null)
        {
            config = Resources.Load<LettersGameConfig>("LettersGameConfig");
        }

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<LettersGameConfig>();
        }
    }

    private void EnsureTrainingUi()
    {
        RectTransform root = ResolveCanvasRoot();
        if (root == null)
        {
            return;
        }

        if (trainingBackdrop == null)
        {
            GameObject backdropObject = new GameObject("LettersTrainingBackdrop", typeof(RectTransform), typeof(Image));
            backdropObject.transform.SetParent(root, false);
            trainingBackdrop = backdropObject.GetComponent<Image>();
            trainingBackdrop.raycastTarget = false;
            RectTransform rect = trainingBackdrop.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            backdropObject.transform.SetAsFirstSibling();
        }

        trainingBackdrop.color = config != null ? config.trainingBackgroundColor : new Color(0.07f, 0.15f, 0.17f, 0.92f);

        if (keyboardHint == null)
        {
            keyboardHint = KeyboardHintView.Create(root, config);
        }

        if (lessonTitle != null)
        {
            RectTransform rect = lessonTitle.rectTransform;
            rect.anchorMin = new Vector2(0.12f, 0.61f);
            rect.anchorMax = new Vector2(0.88f, 0.69f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    private RectTransform ResolveCanvasRoot()
    {
        Canvas canvas = letterQueueParent != null ? letterQueueParent.GetComponentInParent<Canvas>() : null;
        return canvas != null ? canvas.GetComponent<RectTransform>() : letterQueueParent as RectTransform;
    }

    private RectTransform ResolveLetterCardParent()
    {
        if (letterCardLayer != null)
        {
            return letterCardLayer;
        }

        RectTransform root = ResolveCanvasRoot();
        if (root == null)
        {
            return letterQueueParent as RectTransform;
        }

        GameObject layerObject = new GameObject("LettersCardLayer", typeof(RectTransform));
        layerObject.transform.SetParent(root, false);
        letterCardLayer = layerObject.GetComponent<RectTransform>();
        letterCardLayer.anchorMin = Vector2.zero;
        letterCardLayer.anchorMax = Vector2.one;
        letterCardLayer.offsetMin = Vector2.zero;
        letterCardLayer.offsetMax = Vector2.zero;
        return letterCardLayer;
    }

    private bool PlayConfiguredSound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
            return true;
        }

        return false;
    }
}

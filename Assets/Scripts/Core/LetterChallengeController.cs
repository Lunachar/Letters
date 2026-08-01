using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class LetterChallengeController : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform letterQueueParent;
    [SerializeField] private GameObject letterPrefab;
    [SerializeField] private RectTransform[] spawnPoints = new RectTransform[3];
    [SerializeField] private LettersGameConfig config;

    private List<LetterData> letterPool;
    private List<LetterCardView> letterObjects = new();
    private Queue<LetterData> letterQueue = new();

    private LetterData currentLetter;
    private bool awaitingInput;
    private Coroutine repeatRoutine;
    private Coroutine nextLetterRoutine;
    private bool isActive = false;
    private Coroutine letterSoundRoutine;
    private Coroutine startWithFirstSoundCoroutine;
    private bool firstTime = true;
    private bool transitioning;
    private Coroutine feedbackRoutine;
    private KeyboardHintView keyboardHint;
    private Image trainingBackdrop;
    private Text challengeInstructionText;
    private Font uiFont;
    private RectTransform letterCardLayer;

    private void Start()
    {
        ResolveConfig();
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

    public void StartChallenge(List<LetterData> letters)
    {
        StartCoroutine(DelayedChallenge(letters));
    }

    private IEnumerator DelayedChallenge(List<LetterData> letters)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        
        yield return new WaitUntil(() => gameObject.activeInHierarchy);

        yield return null;
        
        ResolveConfig();
        if (letters == null || letters.Count == 0)
        {
            Debug.LogError("LetterChallengeController: Letter pool is empty.");
            yield break;
        }

        StopChallenge();
        EnsureTrainingUi();
        letterPool = letters;
        isActive = true;
        transitioning = false;
        firstTime = true;
        ResetQueue();

        if (letterQueue.Count == 0)
        {
            yield break;
        }

        currentLetter = letterQueue.Peek();
        RefreshCurrentLetter();
       
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        startWithFirstSoundCoroutine = StartCoroutine(StartWithFirstSound());
    }

    private IEnumerator StartWithFirstSound()
    {
        if (firstTime)
        {
            yield return new WaitForSeconds(config != null ? config.firstTaskDelay : 0.2f);
            PlayTaskSound();
            yield return new WaitForSeconds(config != null ? config.taskIntroDelay : 1f);
        }
    
        awaitingInput = true;
        StartCurrentLetterPlayback();
        repeatRoutine = StartCoroutine(RepeatTaskRoutine());
        firstTime = false;
    }

    private void ResetQueue()
    {
        foreach (var obj in letterObjects)
        {
            if (obj != null)
            {
                Destroy(obj.gameObject);
            }
        }

        letterObjects.Clear();
        letterQueue.Clear();

        int visibleCount = config != null ? Mathf.Max(1, config.visibleLetterCount) : 3;
        if (!LettersTrainingLayout.UsesConfiguredLayout(config) && spawnPoints != null && spawnPoints.Length > 0)
        {
            visibleCount = Mathf.Min(visibleCount, spawnPoints.Length);
        }
        for (int i = 0; i < visibleCount; i++)
        {
            var letter = GetRandomLetter();
            letterQueue.Enqueue(letter);
            AddLetterVisual(letter);
        }
        
        UpdateLetterPosition();
    }
    
    private LetterData GetRandomLetter()
    {
        return letterPool[Random.Range(0, letterPool.Count)];
    }
    
    private void AddLetterVisual(LetterData data)
    {
        LetterCardView card = LetterCardView.Create(ResolveLetterCardParent(), letterPrefab, data.letter, config);
        letterObjects.Add(card);
        card.PlaySpawnFeedback();
    }
    
    private void UpdateLetterPosition()
    {
        if (LettersTrainingLayout.TryPositionCards(letterObjects, ResolveLetterCardParent(), config))
        {
            return;
        }

        for (int i = 0; i < letterObjects.Count && i < spawnPoints.Length; i++)
        {
            RectTransform rt = letterObjects[i].RectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            letterObjects[i].SetLayout(spawnPoints[i].anchoredPosition, rt.sizeDelta, true);

            SpawnPointSettings settings = spawnPoints[i].GetComponent<SpawnPointSettings>();
            if (settings != null)
            {
                settings.ApplyTo(letterObjects[i].gameObject);
            }
        }
    }

    private IEnumerator RepeatTaskRoutine()
    {
        float firstWait = config != null ? config.firstAnswerWaitSeconds : 4f;
        float timer = 0f;
        while (timer < firstWait && awaitingInput)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        while (isActive && awaitingInput)
        {
            StartCurrentLetterPlayback();
            float interval = config != null ? config.repeatQuestionInterval : 8f;
            float elapsed = 0f;
            while (elapsed < interval && awaitingInput)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    private void StartCurrentLetterPlayback()
    {
        StopCurrentLetterPlayback(false);
        letterSoundRoutine = StartCoroutine(PlayCurrentLetterRoutine());
    }

    private void StopCurrentLetterPlayback(bool stopAudioSource = true)
    {
        if (letterSoundRoutine != null)
        {
            StopCoroutine(letterSoundRoutine);
            letterSoundRoutine = null;
        }

        if (stopAudioSource && audioSource != null)
        {
            audioSource.Stop();
        }
    }

    private IEnumerator PlayCurrentLetterRoutine()
    {
        if (currentLetter == null || currentLetter.letterSound == null)
        {
            letterSoundRoutine = null;
            yield break;
        }

        yield return new WaitForSeconds(0.3f);
        if (audioSource != null)
        {
            audioSource.PlayOneShot(currentLetter.letterSound);
        }
        yield return new WaitForSeconds(currentLetter.letterSound.length + 0.1f);
        letterSoundRoutine = null;
    }

    private void HandlePhysicalKeyPressed(Key key)
    {
        if (!isActive || !awaitingInput || transitioning)
        {
            return;
        }

        if (!RussianKeyboardLayout.TryGetLetterForKey(key, out char inputChar))
        {
            return;
        }

        if (currentLetter == null)
        {
            return;
        }

        char targetChar = char.ToUpperInvariant(currentLetter.letter);

        if (inputChar == targetChar)
        {
            awaitingInput = false;
            transitioning = true;
            StopCurrentLetterPlayback();
            StopRepeatRoutine();
            keyboardHint?.ShowCorrect();
            if (letterObjects.Count > 0)
            {
                letterObjects[0].SetState(LetterCardState.Correct);
                letterObjects[0].PlayCorrectFeedback();
            }

            PlayCorrectSound();
            StartCoroutine(DelayedPraise(config != null ? config.correctFeedbackDelay : 0.4f));
            nextLetterRoutine = StartCoroutine(NextLetter());
            return;
        }

        PlayWrongSound();
        keyboardHint?.ShowError(key);
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
        }

        feedbackRoutine = StartCoroutine(FlashCurrentCard(LetterCardState.Wrong));
    }

    private IEnumerator DelayedPraise(float f)
    {
        yield return new WaitForSeconds(f);
        if (config != null && config.praiseSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(config.praiseSound);
            yield break;
        }

        SoundManager.Instance?.PlayPraiseSound();
    }

    private IEnumerator NextLetter()
    {
        yield return new WaitForSeconds(config != null ? config.nextLetterDelay : 1f);

        if (letterObjects.Count > 0)
        {
            Destroy(letterObjects[0].gameObject);
            letterObjects.RemoveAt(0);
        }

        if (letterQueue.Count > 0)
        {
            letterQueue.Dequeue();
        }

        var newLetter = GetRandomLetter();
        letterQueue.Enqueue(newLetter);
        AddLetterVisual(newLetter);
        UpdateLetterPosition();

        currentLetter = letterQueue.Peek();
        transitioning = false;
        RefreshCurrentLetter();
        awaitingInput = true;
        StartCurrentLetterPlayback();
        repeatRoutine = StartCoroutine(RepeatTaskRoutine());
    }

    public void StopChallenge()
    {
        isActive = false;
        awaitingInput = false;
        currentLetter = null;

        if (repeatRoutine != null)
        {
            StopCoroutine(repeatRoutine);
        }

        if (nextLetterRoutine != null)
        {
            StopCoroutine(nextLetterRoutine);
        }

        if (startWithFirstSoundCoroutine != null)
        {
            StopCoroutine(startWithFirstSoundCoroutine);
        }

        StopCurrentLetterPlayback();

        foreach (var obj in letterObjects)
        {
            if (obj != null)
            {
                Destroy(obj.gameObject);
            }
        }

        letterObjects.Clear();
        letterQueue.Clear();
    }

    private void StopRepeatRoutine()
    {
        if (repeatRoutine != null)
        {
            StopCoroutine(repeatRoutine);
            repeatRoutine = null;
        }
    }

    private void RefreshCurrentLetter()
    {
        for (int i = 0; i < letterObjects.Count; i++)
        {
            letterObjects[i].SetState(i == 0 ? LetterCardState.Current : LetterCardState.Normal);
        }

        if (currentLetter != null)
        {
            keyboardHint?.ShowTarget(currentLetter.letter);
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

        if (challengeInstructionText == null)
        {
            GameObject textObject = new GameObject("LettersChallengeInstruction", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(root, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.12f, 0.61f);
            rect.anchorMax = new Vector2(0.88f, 0.69f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            challengeInstructionText = textObject.GetComponent<Text>();
            challengeInstructionText.font = GetUiFont();
            challengeInstructionText.alignment = TextAnchor.MiddleCenter;
            challengeInstructionText.fontSize = 34;
            challengeInstructionText.fontStyle = FontStyle.Bold;
            challengeInstructionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            challengeInstructionText.verticalOverflow = VerticalWrapMode.Overflow;
            challengeInstructionText.raycastTarget = false;
        }

        challengeInstructionText.text = config != null ? config.challengeInstruction : "Послушай букву и нажми её на клавиатуре";
        challengeInstructionText.color = config != null ? config.mutedTextColor : new Color(0.78f, 0.86f, 0.84f, 1f);
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
        letterCardLayer.SetSiblingIndex(Mathf.Min(root.childCount - 1, root.childCount));
        return letterCardLayer;
    }

    private Font GetUiFont()
    {
        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return uiFont;
    }

    private void PlayTaskSound()
    {
        if (config != null && config.taskSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(config.taskSound);
            return;
        }

        SoundManager.Instance?.PlayTaskSound();
    }

    private void PlayWrongSound()
    {
        if (config != null && config.wrongSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(config.wrongSound);
            return;
        }

        SoundManager.Instance?.PlayErrorSound();
    }

    private void PlayCorrectSound()
    {
        if (config != null && config.correctSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(config.correctSound);
            return;
        }

        SoundManager.Instance?.PlayCorrectSound();
    }
}

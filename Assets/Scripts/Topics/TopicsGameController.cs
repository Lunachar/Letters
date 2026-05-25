using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TopicsGameController : MonoBehaviour
{
    [Header("Editable settings")]
    [SerializeField] private TopicsGameConfig config;

    [Header("Runtime layout")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private Canvas canvas;
    private RectTransform root;
    private Font uiFont;
    private AudioSource soundSource;
    private AudioSource musicSource;
    private StoryTextSpeaker textSpeaker;
    private TopicsContentStore store;
    private List<TopicRoomRuntime> rooms = new List<TopicRoomRuntime>();
    private TopicRoomRuntime currentRoom;
    private List<TopicQuestionRuntime> activeQuestions = new List<TopicQuestionRuntime>();
    private int introIndex;
    private int questionIndex;
    private int correctCount;
    private int repeatCount;
    private float questionTimer;
    private bool awaitingAnswer;
    private bool feedbackActive;
    private readonly List<Button> activeAnswerButtons = new List<Button>();

    private void Start()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (config == null && AppGameManager.Instance != null)
        {
            config = AppGameManager.Instance.TopicsGameConfig;
        }

        EnsureServices();
        store = new TopicsContentStore(config);
        RefreshRooms();
        ShowTopicGrid();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ShowTopicGrid();
            return;
        }

        if (awaitingAnswer && !feedbackActive)
        {
            questionTimer += Time.unscaledDeltaTime;
            if (questionTimer >= ConfigValue(config != null ? config.noAnswerRepeatDelay : 8f, 8f))
            {
                questionTimer = 0f;
                if (repeatCount < (config != null ? config.maxQuestionRepeats : 2))
                {
                    repeatCount++;
                    ReadCurrentQuestion();
                }
                else
                {
                    StartCoroutine(SkipQuestionAfterDelay());
                }
            }
        }

        if (activeAnswerButtons.Count > 0 && !feedbackActive)
        {
            for (int i = 0; i < Mathf.Min(activeAnswerButtons.Count, 9); i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i)))
                {
                    activeAnswerButtons[i].onClick.Invoke();
                    break;
                }
            }
        }
    }

    private void EnsureServices()
    {
        EnsureEventSystem();
        EnsureCamera();

        if (GetComponent<TopicsSoftLock>() == null)
        {
            gameObject.AddComponent<TopicsSoftLock>();
        }

        soundSource = gameObject.AddComponent<AudioSource>();
        soundSource.playOnAwake = false;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        textSpeaker = gameObject.AddComponent<StoryTextSpeaker>();

        canvas = CreateCanvas();
        root = canvas.GetComponent<RectTransform>();
    }

    private void RefreshRooms()
    {
        rooms = store.GetRooms();
    }

    private void ShowTopicGrid()
    {
        StopAllCoroutines();
        textSpeaker.Stop();
        awaitingAnswer = false;
        feedbackActive = false;
        currentRoom = null;
        ClearRoot();
        PlayMenuMusicIfNeeded();

        CreateBackground();
        CreateTopBar(config != null ? config.title : "Темы", config != null ? config.subtitle : "Выбери комнату", true);

        Button creatorButton = CreateButton(root, "Редактор", 24, config != null ? config.primaryColor : Color.yellow, ShowCreatorGate);
        SetAnchors(creatorButton.GetComponent<RectTransform>(), new Vector2(0.86f, 0.90f), new Vector2(0.97f, 0.97f));

        Button settingsButton = CreateButton(root, "Звук", 24, new Color(0.29f, 0.54f, 0.68f), ShowAudioSettings);
        SetAnchors(settingsButton.GetComponent<RectTransform>(), new Vector2(0.74f, 0.90f), new Vector2(0.85f, 0.97f));

        GameObject scrollObject = new GameObject("Topic Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObject.transform.SetParent(root, false);
        SetAnchors(scrollObject.GetComponent<RectTransform>(), new Vector2(0.055f, 0.08f), new Vector2(0.945f, 0.78f));
        scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.05f);

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(scrollObject.transform, false);
        SetAnchors(viewportObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        viewportObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
        contentObject.transform.SetParent(viewportObject.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, config != null ? config.gridColumns : 3);
        grid.cellSize = config != null ? config.cardSize : new Vector2(520f, 285f);
        grid.spacing = config != null ? config.cardSpacing : new Vector2(34f, 34f);
        grid.padding = new RectOffset(12, 12, 12, 110);

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewportObject.GetComponent<RectTransform>();
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 34f;

        foreach (TopicRoomRuntime room in rooms)
        {
            CreateTopicCard(contentObject.transform, room);
        }

        UpdateTopicContentHeight(contentRect, grid, rooms.Count);
        scroll.verticalNormalizedPosition = 1f;
    }

    private void UpdateTopicContentHeight(RectTransform contentRect, GridLayoutGroup grid, int itemCount)
    {
        int columns = Mathf.Max(1, grid.constraintCount);
        int rows = Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)columns));
        float height = grid.padding.top + grid.padding.bottom + rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y;
        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        contentRect.anchoredPosition = Vector2.zero;
    }

    private void ShowAudioSettings()
    {
        PlayClick();
        ClearRoot();
        CreateBackground();
        CreateTopBar("Настройки", "Громкость музыки и звуков", false);

        Image panel = CreatePanel(root, "Audio Settings", config != null ? config.panelColor : new Color(0f, 0f, 0f, 0.5f), new Vector2(0.22f, 0.24f), new Vector2(0.78f, 0.72f));

        float musicValue = GetMusicVolume();
        float effectsValue = GetEffectsVolume();
        float speechValue = GetSpeechVolume();
        float feedbackValue = GetFeedbackVoiceVolume();

        Text musicValueText = null;
        Slider musicSlider = CreateLabeledSlider(panel.rectTransform, "Музыка", musicValue, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.88f), value =>
        {
            AppGameManager.Instance?.SetMusicVolume(value);
            if (musicSource != null)
            {
                musicSource.volume = value;
            }
            if (musicValueText != null)
            {
                musicValueText.text = Mathf.RoundToInt(value * 100f) + "%";
            }
        }, out musicValueText);

        Text effectsValueText = null;
        Slider effectsSlider = CreateLabeledSlider(panel.rectTransform, "Эффекты", effectsValue, new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.70f), value =>
        {
            AppGameManager.Instance?.SetEffectsVolume(value);
            if (effectsValueText != null)
            {
                effectsValueText.text = Mathf.RoundToInt(value * 100f) + "%";
            }
        }, out effectsValueText);

        Text speechValueText = null;
        Slider speechSlider = CreateLabeledSlider(panel.rectTransform, "Вопросы", speechValue, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.52f), value =>
        {
            AppGameManager.Instance?.SetSpeechVolume(value);
            if (speechValueText != null)
            {
                speechValueText.text = Mathf.RoundToInt(value * 100f) + "%";
            }
        }, out speechValueText);

        Text feedbackValueText = null;
        Slider feedbackSlider = CreateLabeledSlider(panel.rectTransform, "Комментарии", feedbackValue, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.34f), value =>
        {
            AppGameManager.Instance?.SetFeedbackVoiceVolume(value);
            if (feedbackValueText != null)
            {
                feedbackValueText.text = Mathf.RoundToInt(value * 100f) + "%";
            }
        }, out feedbackValueText);

        Button testEffect = CreateButton(panel.rectTransform, "Эффект", 22, config != null ? config.primaryColor : Color.yellow, () =>
        {
            PlayClick();
            if (config != null && config.correctAnswerSound != null)
            {
                PlayOneShot(config.correctAnswerSound);
            }
            else
            {
                AppGameManager.Instance?.PlayCorrectAnswer();
            }
        });
        SetAnchors(testEffect.GetComponent<RectTransform>(), new Vector2(0.08f, 0.05f), new Vector2(0.28f, 0.14f));

        Button testQuestion = CreateButton(panel.rectTransform, "Вопрос", 22, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            PlayClick();
            SpeakWithVolume("Какая это буква: А?", GetSpeechVolume());
        });
        SetAnchors(testQuestion.GetComponent<RectTransform>(), new Vector2(0.31f, 0.05f), new Vector2(0.51f, 0.14f));

        Button testFeedback = CreateButton(panel.rectTransform, "Комментарий", 22, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            PlayClick();
            SpeakFeedbackComment(true);
        });
        SetAnchors(testFeedback.GetComponent<RectTransform>(), new Vector2(0.54f, 0.05f), new Vector2(0.77f, 0.14f));

        musicSlider.value = musicValue;
        effectsSlider.value = effectsValue;
        speechSlider.value = speechValue;
        feedbackSlider.value = feedbackValue;
    }

    private void CreateTopicCard(Transform parent, TopicRoomRuntime room)
    {
        GameObject cardObject = new GameObject(room.title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(DwellSelectable));
        cardObject.transform.SetParent(parent, false);
        Image image = cardObject.GetComponent<Image>();
        image.color = room.cardColor;

        Outline outline = cardObject.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.28f);
        outline.effectDistance = new Vector2(4f, -4f);

        Button button = cardObject.GetComponent<Button>();
        ApplyButtonColors(button, room.cardColor);
        button.onClick.AddListener(() =>
        {
            PlayClick();
            StartRoom(room);
        });

        RectTransform rect = cardObject.GetComponent<RectTransform>();
        Image topGlow = CreatePanel(rect, "Card Highlight", new Color(1f, 1f, 1f, 0.10f), new Vector2(0f, 0.72f), new Vector2(1f, 1f));
        topGlow.raycastTarget = false;
        Image titleBand = CreatePanel(rect, "Title Band", new Color(0f, 0f, 0f, 0.22f), new Vector2(0f, 0f), new Vector2(1f, 0.26f));
        titleBand.raycastTarget = false;

        if (room.icon != null)
        {
            Image icon = CreateImage(rect, "Icon", room.icon, Color.white, new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.88f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
        else
        {
            Text symbol = CreateText(rect, room.cardSymbol, 104, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            symbol.raycastTarget = false;
            SetAnchors(symbol.rectTransform, new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.88f));
        }

        Text title = CreateText(rect, room.title, 42, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        title.raycastTarget = false;
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 24;
        title.resizeTextMaxSize = 42;
        SetAnchors(title.rectTransform, new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.24f));

        TopicScoreRecord score = store.GetScore(room.id);
        if (score != null && score.bestTotal > 0)
        {
            Text best = CreateText(rect, score.bestCorrect + "/" + score.bestTotal, 24, FontStyle.Bold, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.86f));
            best.raycastTarget = false;
            SetAnchors(best.rectTransform, new Vector2(0.72f, 0.80f), new Vector2(0.94f, 0.94f));
        }

        AddDwell(cardObject, rect);
    }

    private void StartRoom(TopicRoomRuntime room)
    {
        currentRoom = room;
        PlayRoomMusic(room);
        if (room.introEnabled && room.introPages.Count > 0)
        {
            introIndex = 0;
            ShowIntroPage();
        }
        else
        {
            StartQuiz();
        }
    }

    private void ShowIntroPage()
    {
        ClearRoot();
        CreateBackground();
        CreateTopBar(currentRoom.title, "Объяснение", false);

        TopicIntroPageRuntime page = currentRoom.introPages[Mathf.Clamp(introIndex, 0, currentRoom.introPages.Count - 1)];
        Image panel = CreatePanel(root, "Intro Panel", config != null ? config.panelColor : new Color(0f, 0f, 0f, 0.5f), new Vector2(0.09f, 0.14f), new Vector2(0.91f, 0.78f));

        Text text = CreateText(panel.rectTransform, page.text, 46, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(text.rectTransform, new Vector2(0.08f, page.photos.Count > 0 ? 0.08f : 0.16f), new Vector2(0.92f, page.photos.Count > 0 ? 0.42f : 0.88f));

        for (int i = 0; i < Mathf.Min(page.photos.Count, 3); i++)
        {
            float minX = 0.10f + i * 0.28f;
            Image photo = CreateImage(panel.rectTransform, "Photo", page.photos[i], Color.white, new Vector2(minX, 0.50f), new Vector2(minX + 0.24f, 0.88f));
            photo.preserveAspect = true;
        }

        Button next = CreateButton(root, introIndex < currentRoom.introPages.Count - 1 ? "Дальше" : "Начать", 42, config != null ? config.primaryColor : Color.yellow, () =>
        {
            PlayClick();
            if (introIndex < currentRoom.introPages.Count - 1)
            {
                introIndex++;
                ShowIntroPage();
            }
            else
            {
                StartQuiz();
            }
        });
        SetAnchors(next.GetComponent<RectTransform>(), new Vector2(0.36f, 0.03f), new Vector2(0.64f, 0.12f));

        SpeakOrPlay(page.text, page.narration, currentRoom);
    }

    private void StartQuiz()
    {
        activeQuestions.Clear();
        activeQuestions.AddRange(currentRoom.questions);
        if (currentRoom.questionsPerRun > 0 && activeQuestions.Count > currentRoom.questionsPerRun)
        {
            Shuffle(activeQuestions);
            activeQuestions.RemoveRange(currentRoom.questionsPerRun, activeQuestions.Count - currentRoom.questionsPerRun);
        }

        questionIndex = 0;
        correctCount = 0;
        ShowQuestion();
    }

    private void ShowQuestion()
    {
        if (questionIndex >= activeQuestions.Count)
        {
            ShowResult();
            return;
        }

        textSpeaker.Stop();
        ClearRoot();
        activeAnswerButtons.Clear();
        feedbackActive = false;
        awaitingAnswer = true;
        questionTimer = 0f;
        repeatCount = 0;

        TopicQuestionRuntime question = activeQuestions[questionIndex];
        CreateBackground();
        CreateTopBar(currentRoom.title, "Вопрос " + (questionIndex + 1) + " из " + activeQuestions.Count, false);

        Image questionPanel = CreatePanel(root, "Question Panel", config != null ? config.panelColor : new Color(0f, 0f, 0f, 0.5f), new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.84f));
        if (question.image != null)
        {
            Image questionImage = CreateImage(questionPanel.rectTransform, "Question Image", question.image, Color.white, new Vector2(0.04f, 0.12f), new Vector2(0.22f, 0.88f));
            questionImage.preserveAspect = true;
        }

        Text questionText = CreateText(questionPanel.rectTransform, question.text, 46, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(questionText.rectTransform, question.image != null ? new Vector2(0.25f, 0.08f) : new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f));

        GameObject answersObject = new GameObject("Answers", typeof(RectTransform), typeof(GridLayoutGroup));
        answersObject.transform.SetParent(root, false);
        SetAnchors(answersObject.GetComponent<RectTransform>(), new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.57f));

        GridLayoutGroup grid = answersObject.GetComponent<GridLayoutGroup>();
        int answerCount = Mathf.Clamp(question.answersToShow > 0 ? question.answersToShow : currentRoom.defaultAnswersToShow, 2, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = answerCount <= 4 ? 2 : 4;
        grid.cellSize = answerCount <= 4 ? new Vector2(770f, 205f) : new Vector2(370f, 180f);
        grid.spacing = new Vector2(28f, 28f);

        List<TopicAnswerRuntime> answers = BuildAnswerSet(question, answerCount);
        for (int i = 0; i < answers.Count; i++)
        {
            CreateAnswerButton(answersObject.transform, answers[i]);
        }

        ReadCurrentQuestion();
    }

    private void CreateAnswerButton(Transform parent, TopicAnswerRuntime answer)
    {
        Color baseColor = new Color(0.24f, 0.43f, 0.58f);
        GameObject buttonObject = new GameObject(answer.text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(DwellSelectable));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = baseColor;

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.22f);
        outline.effectDistance = new Vector2(4f, -4f);

        Button button = buttonObject.GetComponent<Button>();
        ApplyButtonColors(button, baseColor);
        button.onClick.AddListener(() => SelectAnswer(answer, buttonObject));

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        if (answer.image != null)
        {
            Image answerImage = CreateImage(rect, "Answer Image", answer.image, Color.white, new Vector2(0.05f, 0.18f), new Vector2(0.32f, 0.84f));
            answerImage.preserveAspect = true;
        }

        Text answerText = CreateText(rect, answer.text, answer.textSize, FontStyle.Bold, TextAnchor.MiddleCenter, answer.textColor);
        answerText.resizeTextForBestFit = true;
        answerText.resizeTextMinSize = 24;
        answerText.resizeTextMaxSize = Mathf.Clamp(answer.textSize, 28, 72);
        SetAnchors(answerText.rectTransform, answer.image != null ? new Vector2(0.34f, 0.08f) : new Vector2(0.07f, 0.08f), new Vector2(0.93f, 0.92f));

        AddDwell(buttonObject, rect);
        activeAnswerButtons.Add(button);
    }

    private void SelectAnswer(TopicAnswerRuntime answer, GameObject buttonObject)
    {
        if (feedbackActive)
        {
            return;
        }

        feedbackActive = true;
        awaitingAnswer = false;
        foreach (Button button in activeAnswerButtons)
        {
            button.interactable = false;
        }

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = answer.isCorrect ? (config != null ? config.correctColor : Color.green) : (config != null ? config.wrongColor : Color.red);
        outline.effectDistance = new Vector2(8f, -8f);

        if (answer.isCorrect)
        {
            correctCount++;
            if (answer.sound == null && !string.IsNullOrEmpty(answer.soundPath))
            {
                StartCoroutine(TopicsContentStore.LoadAudioClip(answer.soundPath, loaded =>
                {
                    answer.sound = loaded;
                    PlayOneShot(loaded != null ? loaded : config != null ? config.correctAnswerSound : null);
                }));
            }
            else if (answer.sound != null || config != null && config.correctAnswerSound != null)
            {
                PlayOneShot(answer.sound != null ? answer.sound : config.correctAnswerSound);
            }
            else
            {
                AppGameManager.Instance?.PlayCorrectAnswer();
            }
            SpeakFeedbackComment(true);
        }
        else
        {
            if (answer.sound == null && !string.IsNullOrEmpty(answer.soundPath))
            {
                StartCoroutine(TopicsContentStore.LoadAudioClip(answer.soundPath, loaded =>
                {
                    answer.sound = loaded;
                    PlayOneShot(loaded != null ? loaded : config != null ? config.wrongAnswerSound : null);
                }));
            }
            else if (answer.sound != null || config != null && config.wrongAnswerSound != null)
            {
                PlayOneShot(answer.sound != null ? answer.sound : config.wrongAnswerSound);
            }
            else
            {
                AppGameManager.Instance?.PlayWrongAnswer();
            }
            SpeakFeedbackComment(false);
        }

        StartCoroutine(NextQuestionAfterDelay());
    }

    private IEnumerator SkipQuestionAfterDelay()
    {
        feedbackActive = true;
        awaitingAnswer = false;
        yield return new WaitForSeconds(config != null ? config.feedbackDelay : 1.4f);
        questionIndex++;
        ShowQuestion();
    }

    private IEnumerator NextQuestionAfterDelay()
    {
        yield return new WaitForSeconds(config != null ? config.feedbackDelay : 1.4f);
        questionIndex++;
        ShowQuestion();
    }

    private IEnumerator PlayRewardEffect()
    {
        Color[] colors =
        {
            new Color(1f, 0.82f, 0.20f),
            new Color(0.30f, 0.76f, 1f),
            new Color(0.43f, 0.90f, 0.45f),
            new Color(1f, 0.42f, 0.55f),
            new Color(0.82f, 0.54f, 1f)
        };

        for (int i = 0; i < 34; i++)
        {
            Image piece = CreatePanel(root, "Reward Confetti", colors[i % colors.Length], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            RectTransform rect = piece.rectTransform;
            rect.sizeDelta = new Vector2(UnityEngine.Random.Range(12f, 24f), UnityEngine.Random.Range(12f, 24f));
            rect.anchoredPosition = new Vector2(UnityEngine.Random.Range(-420f, 420f), UnityEngine.Random.Range(-40f, 250f));
            rect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 180f));
            StartCoroutine(AnimateRewardPiece(rect, piece));
            yield return new WaitForSeconds(0.025f);
        }
    }

    private IEnumerator AnimateRewardPiece(RectTransform rect, Image image)
    {
        float duration = UnityEngine.Random.Range(1.2f, 2.1f);
        float elapsed = 0f;
        Vector2 start = rect.anchoredPosition;
        Vector2 end = start + new Vector2(UnityEngine.Random.Range(-90f, 90f), UnityEngine.Random.Range(-320f, -180f));
        Color startColor = image.color;

        while (elapsed < duration && rect != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rect.anchoredPosition = Vector2.Lerp(start, end, t);
            rect.localRotation = Quaternion.Euler(0f, 0f, rect.localRotation.eulerAngles.z + Time.unscaledDeltaTime * 180f);
            image.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
            yield return null;
        }

        if (rect != null)
        {
            Destroy(rect.gameObject);
        }
    }

    private void ShowResult()
    {
        textSpeaker.Stop();
        awaitingAnswer = false;
        feedbackActive = false;
        store.RegisterScore(currentRoom.id, correctCount, activeQuestions.Count);

        ClearRoot();
        CreateBackground();
        CreateTopBar(currentRoom.title, "Готово!", false);

        Image panel = CreatePanel(root, "Result", config != null ? config.panelColor : new Color(0f, 0f, 0f, 0.5f), new Vector2(0.20f, 0.25f), new Vector2(0.80f, 0.70f));
        Text result = CreateText(panel.rectTransform, "Правильных ответов: " + correctCount + " из " + activeQuestions.Count, 58, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(result.rectTransform, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.82f));
        string rewardText = currentRoom != null && !string.IsNullOrEmpty(currentRoom.rewardMessage) ? currentRoom.rewardMessage : "Отличная работа!";
        Text reward = CreateText(panel.rectTransform, rewardText, 34, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.55f));
        SetAnchors(reward.rectTransform, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.25f));

        PlayRewardMusic(currentRoom);
        if (currentRoom != null && currentRoom.rewardEffectEnabled)
        {
            StartCoroutine(PlayRewardEffect());
        }
        if (config != null && config.celebrationSound != null)
        {
            PlayOneShot(config.celebrationSound);
        }
        else
        {
            AppGameManager.Instance?.PlayCelebration();
        }
        StartCoroutine(ReturnToGridAfterResult());
    }

    private IEnumerator ReturnToGridAfterResult()
    {
        yield return new WaitForSeconds(config != null ? config.resultAutoReturnDelay : 5f);
        RefreshRooms();
        ShowTopicGrid();
    }

    private void ShowCreatorGate()
    {
        PlayClick();
        ClearRoot();
        CreateBackground();
        CreateTopBar(TopicsSecurity.HasPin ? "Введите PIN" : "Задайте PIN", "Доступ для взрослого", false);

        Image panel = CreatePanel(root, "PIN Panel", config != null ? config.panelColor : new Color(0f, 0f, 0f, 0.5f), new Vector2(0.28f, 0.30f), new Vector2(0.72f, 0.66f));
        Text message = CreateText(panel.rectTransform, TopicsSecurity.HasPin ? "Введите PIN-код" : "Придумайте 4-6 цифр", 36, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(message.rectTransform, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.92f));

        InputField input = CreateInput(panel.rectTransform, "PIN", true);
        SetAnchors(input.GetComponent<RectTransform>(), new Vector2(0.18f, 0.42f), new Vector2(0.82f, 0.62f));

        Text error = CreateText(panel.rectTransform, "", 26, FontStyle.Normal, TextAnchor.MiddleCenter, config != null ? config.wrongColor : Color.red);
        SetAnchors(error.rectTransform, new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.39f));

        Button enter = CreateButton(panel.rectTransform, TopicsSecurity.HasPin ? "Войти" : "Сохранить", 32, config != null ? config.primaryColor : Color.yellow, () =>
        {
            if (!TopicsSecurity.IsValidPinShape(input.text))
            {
                error.text = "PIN должен быть из 4-6 цифр";
                return;
            }

            if (!TopicsSecurity.HasPin)
            {
                TopicsSecurity.SetPin(input.text);
                ShowCreatorList();
                return;
            }

            if (TopicsSecurity.VerifyPin(input.text))
            {
                ShowCreatorList();
            }
            else
            {
                error.text = "Неверный PIN";
            }
        });
        SetAnchors(enter.GetComponent<RectTransform>(), new Vector2(0.18f, 0.07f), new Vector2(0.50f, 0.23f));

        Button back = CreateButton(panel.rectTransform, "Назад", 28, new Color(0.36f, 0.42f, 0.46f), ShowTopicGrid);
        SetAnchors(back.GetComponent<RectTransform>(), new Vector2(0.54f, 0.07f), new Vector2(0.82f, 0.23f));
    }

    private void ShowCreatorList()
    {
        RefreshRooms();
        ClearRoot();
        CreateBackground();
        CreateTopBar(config != null ? config.creatorTitle : "Редактор тем", "Создать, изменить или удалить", false);

        Button add = CreateButton(root, "Новая тема", 30, config != null ? config.primaryColor : Color.yellow, () =>
        {
            TopicRoomRuntime room = store.CreateEmptyRoom();
            ShowRoomEditor(room);
        });
        SetAnchors(add.GetComponent<RectTransform>(), new Vector2(0.08f, 0.79f), new Vector2(0.29f, 0.87f));

        Button generator = CreateButton(root, "Сгенерировать черновик", 26, new Color(0.29f, 0.54f, 0.68f), ShowDraftGeneratorNotice);
        SetAnchors(generator.GetComponent<RectTransform>(), new Vector2(0.31f, 0.79f), new Vector2(0.58f, 0.87f));

        Button back = CreateButton(root, "К темам", 26, new Color(0.36f, 0.42f, 0.46f), ShowTopicGrid);
        SetAnchors(back.GetComponent<RectTransform>(), new Vector2(0.79f, 0.79f), new Vector2(0.92f, 0.87f));

        GameObject list = new GameObject("Rooms List", typeof(RectTransform), typeof(VerticalLayoutGroup));
        list.transform.SetParent(root, false);
        SetAnchors(list.GetComponent<RectTransform>(), new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.75f));
        VerticalLayoutGroup layout = list.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        foreach (TopicRoomRuntime room in rooms)
        {
            CreateCreatorRow(list.transform, room);
        }
    }

    private void CreateCreatorRow(Transform parent, TopicRoomRuntime room)
    {
        GameObject row = new GameObject(room.title, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        row.GetComponent<LayoutElement>().preferredHeight = 82f;

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 10, 10);
        layout.spacing = 16f;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = false;

        Text title = CreateText(row.transform, room.title + (room.isUserCreated ? "" : " (Unity)"), 32, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        title.gameObject.AddComponent<LayoutElement>().preferredWidth = 900f;

        Button edit = CreateButton(row.transform, "Редактировать", 24, config != null ? config.primaryColor : Color.yellow, () => ShowRoomEditor(CloneForEditing(room)));
        edit.gameObject.AddComponent<LayoutElement>().preferredWidth = 260f;

        Button delete = CreateButton(row.transform, "Удалить", 24, new Color(0.68f, 0.25f, 0.25f), () =>
        {
            if (room.isUserCreated)
            {
                store.DeleteUserRoom(room.id);
            }
            ShowCreatorList();
        });
        delete.interactable = room.isUserCreated;
        delete.gameObject.AddComponent<LayoutElement>().preferredWidth = 180f;
    }

    private void ShowRoomEditorLegacy(TopicRoomRuntime room, int editQuestionIndex = 0)
    {
        ClearRoot();
        CreateBackground();
        CreateTopBar("Редактор темы", room.title, false);

        Image panel = CreatePanel(root, "Editor Panel", config != null ? config.panelColor : new Color(0f, 0f, 0f, 0.5f), new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.82f));

        Text labelTitle = CreateText(panel.rectTransform, "Название", 24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(labelTitle.rectTransform, new Vector2(0.04f, 0.86f), new Vector2(0.22f, 0.94f));
        InputField titleInput = CreateInput(panel.rectTransform, room.title, false);
        SetAnchors(titleInput.GetComponent<RectTransform>(), new Vector2(0.22f, 0.86f), new Vector2(0.58f, 0.94f));

        Text labelSymbol = CreateText(panel.rectTransform, "Значок", 24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(labelSymbol.rectTransform, new Vector2(0.62f, 0.86f), new Vector2(0.74f, 0.94f));
        InputField symbolInput = CreateInput(panel.rectTransform, room.cardSymbol, false);
        SetAnchors(symbolInput.GetComponent<RectTransform>(), new Vector2(0.74f, 0.86f), new Vector2(0.86f, 0.94f));

        Toggle introToggle = CreateToggle(panel.rectTransform, "Показывать объяснение", room.introEnabled);
        SetAnchors(introToggle.GetComponent<RectTransform>(), new Vector2(0.04f, 0.76f), new Vector2(0.38f, 0.84f));

        Text introLabel = CreateText(panel.rectTransform, "Текст объяснения", 24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(introLabel.rectTransform, new Vector2(0.04f, 0.65f), new Vector2(0.30f, 0.73f));
        InputField introInput = CreateInput(panel.rectTransform, room.introPages.Count > 0 ? room.introPages[0].text : "", false);
        introInput.lineType = InputField.LineType.MultiLineNewline;
        SetAnchors(introInput.GetComponent<RectTransform>(), new Vector2(0.04f, 0.45f), new Vector2(0.52f, 0.65f));

        Text qLabel = CreateText(panel.rectTransform, "Вопрос", 24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(qLabel.rectTransform, new Vector2(0.56f, 0.65f), new Vector2(0.88f, 0.73f));
        TopicQuestionRuntime question = room.questions.Count > 0 ? room.questions[Mathf.Clamp(editQuestionIndex, 0, room.questions.Count - 1)] : store.CreateSampleQuestion();
        if (room.questions.Count == 0)
        {
            room.questions.Add(question);
        }
        int selectedQuestionIndex = Mathf.Clamp(editQuestionIndex, 0, room.questions.Count - 1);
        qLabel.text = "Вопрос " + (selectedQuestionIndex + 1) + " из " + room.questions.Count;
        InputField questionInput = CreateInput(panel.rectTransform, question.text, false);
        SetAnchors(questionInput.GetComponent<RectTransform>(), new Vector2(0.56f, 0.55f), new Vector2(0.94f, 0.65f));

        InputField correctInput = CreateInput(panel.rectTransform, FindCorrectAnswer(question), false);
        SetAnchors(correctInput.GetComponent<RectTransform>(), new Vector2(0.56f, 0.43f), new Vector2(0.94f, 0.53f));

        InputField wrong1Input = CreateInput(panel.rectTransform, GetAnswerText(question, 1, "Ответ 2"), false);
        SetAnchors(wrong1Input.GetComponent<RectTransform>(), new Vector2(0.56f, 0.31f), new Vector2(0.94f, 0.41f));
        InputField wrong2Input = CreateInput(panel.rectTransform, GetAnswerText(question, 2, "Ответ 3"), false);
        SetAnchors(wrong2Input.GetComponent<RectTransform>(), new Vector2(0.56f, 0.19f), new Vector2(0.94f, 0.29f));
        InputField wrong3Input = CreateInput(panel.rectTransform, GetAnswerText(question, 3, "Ответ 4"), false);
        SetAnchors(wrong3Input.GetComponent<RectTransform>(), new Vector2(0.56f, 0.07f), new Vector2(0.94f, 0.17f));

        Button importIcon = CreateButton(panel.rectTransform, "Фото темы", 24, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            TopicsFilePicker.PickImage(path =>
            {
                string copied = TopicsFilePicker.CopyToMediaFolder(path, "Images");
                if (!string.IsNullOrEmpty(copied))
                {
                    room.iconPath = copied;
                    room.icon = TopicsContentStore.LoadSprite(copied);
                }
            });
        });
        SetAnchors(importIcon.GetComponent<RectTransform>(), new Vector2(0.04f, 0.31f), new Vector2(0.25f, 0.40f));

        Button importMusic = CreateButton(panel.rectTransform, "Музыка темы", 23, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            TopicsFilePicker.PickAudio(path =>
            {
                string copied = TopicsFilePicker.CopyToMediaFolder(path, "Audio");
                if (!string.IsNullOrEmpty(copied))
                {
                    room.musicPath = copied;
                    room.roomMusic = null;
                }
            });
        });
        SetAnchors(importMusic.GetComponent<RectTransform>(), new Vector2(0.27f, 0.31f), new Vector2(0.52f, 0.40f));

        Button addQuestion = CreateButton(panel.rectTransform, "Добавить вопрос", 24, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            ApplyRoomEditorFields(room, question, titleInput, symbolInput, introToggle, introInput, questionInput, correctInput, wrong1Input, wrong2Input, wrong3Input);
            room.questions.Add(store.CreateSampleQuestion());
            ShowRoomEditor(room, room.questions.Count - 1);
        });
        SetAnchors(addQuestion.GetComponent<RectTransform>(), new Vector2(0.04f, 0.20f), new Vector2(0.25f, 0.29f));

        Button importQuestionImage = CreateButton(panel.rectTransform, "Фото вопроса", 23, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            TopicsFilePicker.PickImage(path =>
            {
                string copied = TopicsFilePicker.CopyToMediaFolder(path, "Images");
                if (!string.IsNullOrEmpty(copied))
                {
                    question.imagePath = copied;
                    question.image = TopicsContentStore.LoadSprite(copied);
                }
            });
        });
        SetAnchors(importQuestionImage.GetComponent<RectTransform>(), new Vector2(0.27f, 0.20f), new Vector2(0.52f, 0.29f));

        Button importQuestionAudio = CreateButton(panel.rectTransform, "Звук вопроса", 23, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            TopicsFilePicker.PickAudio(path =>
            {
                string copied = TopicsFilePicker.CopyToMediaFolder(path, "Audio");
                if (!string.IsNullOrEmpty(copied))
                {
                    question.questionSoundPath = copied;
                    question.questionSound = null;
                }
            });
        });
        SetAnchors(importQuestionAudio.GetComponent<RectTransform>(), new Vector2(0.04f, 0.09f), new Vector2(0.25f, 0.18f));

        Button prevQuestion = CreateButton(panel.rectTransform, "←", 28, new Color(0.36f, 0.42f, 0.46f), () =>
        {
            ApplyRoomEditorFields(room, question, titleInput, symbolInput, introToggle, introInput, questionInput, correctInput, wrong1Input, wrong2Input, wrong3Input);
            ShowRoomEditor(room, Mathf.Max(0, selectedQuestionIndex - 1));
        });
        prevQuestion.interactable = selectedQuestionIndex > 0;
        SetAnchors(prevQuestion.GetComponent<RectTransform>(), new Vector2(0.27f, 0.09f), new Vector2(0.38f, 0.18f));

        Button nextQuestion = CreateButton(panel.rectTransform, "→", 28, new Color(0.36f, 0.42f, 0.46f), () =>
        {
            ApplyRoomEditorFields(room, question, titleInput, symbolInput, introToggle, introInput, questionInput, correctInput, wrong1Input, wrong2Input, wrong3Input);
            ShowRoomEditor(room, Mathf.Min(room.questions.Count - 1, selectedQuestionIndex + 1));
        });
        nextQuestion.interactable = selectedQuestionIndex < room.questions.Count - 1;
        SetAnchors(nextQuestion.GetComponent<RectTransform>(), new Vector2(0.41f, 0.09f), new Vector2(0.52f, 0.18f));

        Button save = CreateButton(root, "Сохранить", 30, config != null ? config.primaryColor : Color.yellow, () =>
        {
            ApplyRoomEditorFields(room, question, titleInput, symbolInput, introToggle, introInput, questionInput, correctInput, wrong1Input, wrong2Input, wrong3Input);
            room.isUserCreated = true;
            store.UpsertUserRoom(room);
            ShowCreatorList();
        });
        SetAnchors(save.GetComponent<RectTransform>(), new Vector2(0.58f, 0.01f), new Vector2(0.74f, 0.07f));

        Button cancel = CreateButton(root, "Назад", 28, new Color(0.36f, 0.42f, 0.46f), ShowCreatorList);
        SetAnchors(cancel.GetComponent<RectTransform>(), new Vector2(0.76f, 0.01f), new Vector2(0.90f, 0.07f));
    }

    private void ShowDraftGeneratorNotice()
    {
        ClearRoot();
        CreateBackground();
        CreateTopBar("Генератор черновиков", "Только для взрослого", false);
        Image panel = CreatePanel(root, "Generator Notice", config != null ? config.panelColor : new Color(0f, 0f, 0f, 0.5f), new Vector2(0.18f, 0.30f), new Vector2(0.82f, 0.66f));
        string message = "Сетевой генератор здесь зарезервирован для защищённого режима.\nВ детском режиме интернет не используется.\n\nДля подключения нужен endpoint генератора в TopicsGameConfig.";
        Text text = CreateText(panel.rectTransform, message, 36, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(text.rectTransform, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.92f));
        Button back = CreateButton(panel.rectTransform, "Назад", 28, config != null ? config.primaryColor : Color.yellow, ShowCreatorList);
        SetAnchors(back.GetComponent<RectTransform>(), new Vector2(0.36f, 0.06f), new Vector2(0.64f, 0.20f));
    }

    private void ShowRoomEditor(TopicRoomRuntime room, int editQuestionIndex = 0)
    {
        if (room.questions.Count == 0)
        {
            room.questions.Add(store.CreateSampleQuestion());
        }

        int selectedQuestionIndex = Mathf.Clamp(editQuestionIndex, 0, room.questions.Count - 1);
        TopicQuestionRuntime question = room.questions[selectedQuestionIndex];
        EnsureMinimumAnswers(question);

        ClearRoot();
        CreateBackground();
        CreateTopBar("Редактор темы", room.title, false);

        Image shell = CreatePanel(root, "Editor Shell", config != null ? config.panelColor : new Color(0f, 0f, 0f, 0.5f), new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.84f));

        Image roomPanel = CreatePanel(shell.rectTransform, "Room Settings", new Color(1f, 1f, 1f, 0.08f), new Vector2(0.02f, 0.64f), new Vector2(0.37f, 0.96f));
        Text roomHeader = CreateText(roomPanel.rectTransform, "Тема", 28, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(roomHeader.rectTransform, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.96f));

        InputField titleInput = CreateLabeledInput(roomPanel.rectTransform, "Название", room.title, new Vector2(0.05f, 0.51f), new Vector2(0.95f, 0.76f));
        InputField symbolInput = CreateLabeledInput(roomPanel.rectTransform, "Значок", room.cardSymbol, new Vector2(0.05f, 0.24f), new Vector2(0.47f, 0.49f));
        Toggle introToggle = CreateToggle(roomPanel.rectTransform, "Объяснение", room.introEnabled);
        SetAnchors(introToggle.GetComponent<RectTransform>(), new Vector2(0.52f, 0.25f), new Vector2(0.95f, 0.47f));

        Button importIcon = CreateButton(roomPanel.rectTransform, "Фото", 22, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            TopicsFilePicker.PickImage(path =>
            {
                string copied = TopicsFilePicker.CopyToMediaFolder(path, "Images");
                if (!string.IsNullOrEmpty(copied))
                {
                    room.iconPath = copied;
                    room.icon = TopicsContentStore.LoadSprite(copied);
                }
            });
        });
        SetAnchors(importIcon.GetComponent<RectTransform>(), new Vector2(0.05f, 0.05f), new Vector2(0.32f, 0.21f));

        Button importMusic = CreateButton(roomPanel.rectTransform, "Музыка", 22, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            TopicsFilePicker.PickAudio(path =>
            {
                string copied = TopicsFilePicker.CopyToMediaFolder(path, "Audio");
                if (!string.IsNullOrEmpty(copied))
                {
                    room.musicPath = copied;
                    room.roomMusic = null;
                }
            });
        });
        SetAnchors(importMusic.GetComponent<RectTransform>(), new Vector2(0.36f, 0.05f), new Vector2(0.63f, 0.21f));

        Button importRewardMusic = CreateButton(roomPanel.rectTransform, "Награда", 21, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            TopicsFilePicker.PickAudio(path =>
            {
                string copied = TopicsFilePicker.CopyToMediaFolder(path, "Audio");
                if (!string.IsNullOrEmpty(copied))
                {
                    room.rewardMusicPath = copied;
                    room.rewardMusic = null;
                }
            });
        });
        SetAnchors(importRewardMusic.GetComponent<RectTransform>(), new Vector2(0.67f, 0.05f), new Vector2(0.95f, 0.21f));

        Image introPanel = CreatePanel(shell.rectTransform, "Intro Settings", new Color(1f, 1f, 1f, 0.06f), new Vector2(0.02f, 0.32f), new Vector2(0.37f, 0.61f));
        Text introHeader = CreateText(introPanel.rectTransform, "Объяснение", 24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(introHeader.rectTransform, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.96f));
        InputField introInput = CreateInput(introPanel.rectTransform, room.introPages.Count > 0 ? room.introPages[0].text : "", false);
        introInput.lineType = InputField.LineType.MultiLineNewline;
        SetAnchors(introInput.GetComponent<RectTransform>(), new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.76f));

        Toggle rewardEffectToggle = CreateToggle(introPanel.rectTransform, "Эффект награды", room.rewardEffectEnabled);
        rewardEffectToggle.onValueChanged.AddListener(value => room.rewardEffectEnabled = value);
        SetAnchors(rewardEffectToggle.GetComponent<RectTransform>(), new Vector2(0.05f, 0.08f), new Vector2(0.43f, 0.29f));

        InputField rewardMessageInput = CreateInput(introPanel.rectTransform, string.IsNullOrEmpty(room.rewardMessage) ? "Отличная работа!" : room.rewardMessage, false);
        rewardMessageInput.onValueChanged.AddListener(value => room.rewardMessage = value);
        SetAnchors(rewardMessageInput.GetComponent<RectTransform>(), new Vector2(0.46f, 0.08f), new Vector2(0.95f, 0.29f));

        Image questionListPanel = CreatePanel(shell.rectTransform, "Question List", new Color(1f, 1f, 1f, 0.07f), new Vector2(0.02f, 0.04f), new Vector2(0.37f, 0.29f));
        Text listHeader = CreateText(questionListPanel.rectTransform, "Вопросы", 24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(listHeader.rectTransform, new Vector2(0.05f, 0.76f), new Vector2(0.58f, 0.96f));

        Button addQuestion = CreateButton(questionListPanel.rectTransform, "+ вопрос", 20, config != null ? config.primaryColor : Color.yellow, () =>
        {
            ApplyRoomEditorFields(room, question, titleInput, symbolInput, introToggle, introInput, null, null, null, question.answersToShow);
            room.questions.Add(store.CreateSampleQuestion());
            ShowRoomEditor(room, room.questions.Count - 1);
        });
        SetAnchors(addQuestion.GetComponent<RectTransform>(), new Vector2(0.62f, 0.77f), new Vector2(0.95f, 0.95f));

        GameObject questionList = new GameObject("Question Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
        questionList.transform.SetParent(questionListPanel.rectTransform, false);
        SetAnchors(questionList.GetComponent<RectTransform>(), new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.72f));
        VerticalLayoutGroup questionLayout = questionList.GetComponent<VerticalLayoutGroup>();
        questionLayout.spacing = 7f;
        questionLayout.childControlHeight = true;
        questionLayout.childForceExpandHeight = false;

        for (int i = 0; i < room.questions.Count; i++)
        {
            CreateQuestionListRow(questionList.transform, room, i, selectedQuestionIndex);
        }

        Image questionPanel = CreatePanel(shell.rectTransform, "Question Editor", new Color(1f, 1f, 1f, 0.08f), new Vector2(0.40f, 0.04f), new Vector2(0.98f, 0.96f));
        Text questionHeader = CreateText(questionPanel.rectTransform, "Вопрос " + (selectedQuestionIndex + 1) + " из " + room.questions.Count, 28, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(questionHeader.rectTransform, new Vector2(0.04f, 0.91f), new Vector2(0.58f, 0.98f));

        Button deleteQuestion = CreateButton(questionPanel.rectTransform, "Удалить вопрос", 22, new Color(0.68f, 0.25f, 0.25f), () =>
        {
            if (room.questions.Count <= 1)
            {
                return;
            }

            room.questions.RemoveAt(selectedQuestionIndex);
            ShowRoomEditor(room, Mathf.Clamp(selectedQuestionIndex, 0, room.questions.Count - 1));
        });
        deleteQuestion.interactable = room.questions.Count > 1;
        SetAnchors(deleteQuestion.GetComponent<RectTransform>(), new Vector2(0.72f, 0.91f), new Vector2(0.96f, 0.98f));

        InputField questionInput = CreateLabeledInput(questionPanel.rectTransform, "Текст вопроса", question.text, new Vector2(0.04f, 0.77f), new Vector2(0.66f, 0.89f));

        Button importQuestionImage = CreateButton(questionPanel.rectTransform, "Фото вопроса", 21, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            TopicsFilePicker.PickImage(path =>
            {
                string copied = TopicsFilePicker.CopyToMediaFolder(path, "Images");
                if (!string.IsNullOrEmpty(copied))
                {
                    question.imagePath = copied;
                    question.image = TopicsContentStore.LoadSprite(copied);
                }
            });
        });
        SetAnchors(importQuestionImage.GetComponent<RectTransform>(), new Vector2(0.69f, 0.82f), new Vector2(0.96f, 0.89f));

        Button importQuestionAudio = CreateButton(questionPanel.rectTransform, "Звук вопроса", 21, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            TopicsFilePicker.PickAudio(path =>
            {
                string copied = TopicsFilePicker.CopyToMediaFolder(path, "Audio");
                if (!string.IsNullOrEmpty(copied))
                {
                    question.questionSoundPath = copied;
                    question.questionSound = null;
                }
            });
        });
        SetAnchors(importQuestionAudio.GetComponent<RectTransform>(), new Vector2(0.69f, 0.74f), new Vector2(0.96f, 0.81f));

        Text showCountLabel = CreateText(questionPanel.rectTransform, "Показывать ответов", 21, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(showCountLabel.rectTransform, new Vector2(0.04f, 0.68f), new Vector2(0.34f, 0.74f));
        Text showCountValue = CreateText(questionPanel.rectTransform, Mathf.Clamp(question.answersToShow, 2, Mathf.Max(2, question.answers.Count)).ToString(), 34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(showCountValue.rectTransform, new Vector2(0.39f, 0.67f), new Vector2(0.47f, 0.75f));

        Button minusAnswers = CreateButton(questionPanel.rectTransform, "-", 30, new Color(0.36f, 0.42f, 0.46f), () =>
        {
            question.answersToShow = Mathf.Max(2, question.answersToShow - 1);
            ShowRoomEditor(room, selectedQuestionIndex);
        });
        SetAnchors(minusAnswers.GetComponent<RectTransform>(), new Vector2(0.33f, 0.67f), new Vector2(0.38f, 0.75f));

        Button plusAnswers = CreateButton(questionPanel.rectTransform, "+", 30, new Color(0.36f, 0.42f, 0.46f), () =>
        {
            question.answersToShow = Mathf.Min(8, question.answersToShow + 1);
            while (question.answers.Count < question.answersToShow)
            {
                question.answers.Add(CreateEmptyAnswer(false));
            }
            ShowRoomEditor(room, selectedQuestionIndex);
        });
        SetAnchors(plusAnswers.GetComponent<RectTransform>(), new Vector2(0.48f, 0.67f), new Vector2(0.53f, 0.75f));

        Button addAnswer = CreateButton(questionPanel.rectTransform, "+ ответ", 21, config != null ? config.primaryColor : Color.yellow, () =>
        {
            question.answers.Add(CreateEmptyAnswer(false));
            question.answersToShow = Mathf.Clamp(question.answersToShow + 1, 2, 8);
            ShowRoomEditor(room, selectedQuestionIndex);
        });
        SetAnchors(addAnswer.GetComponent<RectTransform>(), new Vector2(0.69f, 0.66f), new Vector2(0.96f, 0.73f));

        GameObject answerList = new GameObject("Answer List", typeof(RectTransform), typeof(VerticalLayoutGroup));
        answerList.transform.SetParent(questionPanel.rectTransform, false);
        SetAnchors(answerList.GetComponent<RectTransform>(), new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.64f));
        VerticalLayoutGroup answerLayout = answerList.GetComponent<VerticalLayoutGroup>();
        answerLayout.spacing = 8f;
        answerLayout.childControlHeight = true;
        answerLayout.childForceExpandHeight = false;

        List<InputField> answerInputs = new List<InputField>();
        List<Toggle> correctToggles = new List<Toggle>();
        for (int i = 0; i < question.answers.Count; i++)
        {
            CreateAnswerEditorRow(answerList.transform, room, question, i, selectedQuestionIndex, answerInputs, correctToggles);
        }

        Button save = CreateButton(root, "Сохранить", 30, config != null ? config.primaryColor : Color.yellow, () =>
        {
            ApplyRoomEditorFields(room, question, titleInput, symbolInput, introToggle, introInput, questionInput, answerInputs, correctToggles, question.answersToShow);
            room.isUserCreated = true;
            store.UpsertUserRoom(room);
            ShowCreatorList();
        });
        SetAnchors(save.GetComponent<RectTransform>(), new Vector2(0.58f, 0.01f), new Vector2(0.74f, 0.07f));

        Button cancel = CreateButton(root, "Назад", 28, new Color(0.36f, 0.42f, 0.46f), ShowCreatorList);
        SetAnchors(cancel.GetComponent<RectTransform>(), new Vector2(0.76f, 0.01f), new Vector2(0.90f, 0.07f));
    }

    private List<TopicAnswerRuntime> BuildAnswerSet(TopicQuestionRuntime question, int answerCount)
    {
        List<TopicAnswerRuntime> correct = question.answers.FindAll(answer => answer.isCorrect);
        List<TopicAnswerRuntime> wrong = question.answers.FindAll(answer => !answer.isCorrect);
        Shuffle(wrong);

        List<TopicAnswerRuntime> result = new List<TopicAnswerRuntime>();
        if (correct.Count > 0)
        {
            result.Add(correct[0]);
        }

        for (int i = 0; i < wrong.Count && result.Count < answerCount; i++)
        {
            result.Add(wrong[i]);
        }

        for (int i = result.Count; i < answerCount; i++)
        {
            result.Add(new TopicAnswerRuntime { id = Guid.NewGuid().ToString("N"), text = "...", textSize = 52, textColor = Color.white });
        }

        // Keep answer positions unpredictable for every quiz pass.
        Shuffle(result);
        return result;
    }

    private void ReadCurrentQuestion()
    {
        if (questionIndex < 0 || questionIndex >= activeQuestions.Count)
        {
            return;
        }

        TopicQuestionRuntime question = activeQuestions[questionIndex];
        if (question.questionSound == null && !string.IsNullOrEmpty(question.questionSoundPath))
        {
            StartCoroutine(TopicsContentStore.LoadAudioClip(question.questionSoundPath, clip =>
            {
                question.questionSound = clip;
                SpeakOrPlay(question.text, clip, currentRoom);
            }));
            return;
        }

        SpeakOrPlay(question.text, question.questionSound, currentRoom);
    }

    private void SpeakOrPlay(string text, AudioClip clip, TopicRoomRuntime room)
    {
        if (clip != null)
        {
            PlayOneShot(clip);
            return;
        }

        bool shouldSpeak = room != null ? room.useTextToSpeech : config != null && config.useTextToSpeech;
        if (shouldSpeak && textSpeaker != null)
        {
            SpeakWithVolume(
                text,
                GetSpeechVolume() * (room != null ? room.speechVolume : config != null ? config.speechVolume : 1f),
                room != null ? room.speechRate : config != null ? config.speechRate : 0.95f,
                room != null ? room.speechPitch : config != null ? config.speechPitch : 1f,
                room != null ? room.androidLanguage : config != null ? config.androidLanguage : "ru_RU");
        }
    }

    private void SpeakFeedbackComment(bool isCorrect)
    {
        string[] phrases = isCorrect ? config != null ? config.correctFeedbackPhrases : null : config != null ? config.wrongFeedbackPhrases : null;
        string fallback = isCorrect ? "Правильно!" : "Ничего страшного. Попробуем дальше.";
        string phrase = phrases != null && phrases.Length > 0 ? phrases[UnityEngine.Random.Range(0, phrases.Length)] : fallback;
        SpeakWithVolume(phrase, GetFeedbackVoiceVolume());
    }

    private void SpeakWithVolume(string text, float volume)
    {
        SpeakWithVolume(text, volume, config != null ? config.speechRate : 0.95f, config != null ? config.speechPitch : 1f, config != null ? config.androidLanguage : "ru_RU");
    }

    private void SpeakWithVolume(string text, float volume, float rate, float pitch, string language)
    {
        if (textSpeaker != null)
        {
            textSpeaker.Speak(text, Mathf.Clamp01(volume), rate, pitch, language);
        }
    }

    private void PlayRoomMusic(TopicRoomRuntime room)
    {
        if (room == null)
        {
            return;
        }

        if (room.roomMusic == null && !string.IsNullOrEmpty(room.musicPath))
        {
            StartCoroutine(TopicsContentStore.LoadAudioClip(room.musicPath, clip =>
            {
                room.roomMusic = clip;
                PlayRoomMusic(room);
            }));
            return;
        }

        if (room.roomMusic == null)
        {
            return;
        }

        musicSource.clip = room.roomMusic;
        musicSource.loop = true;
        musicSource.volume = GetMusicVolume();
        musicSource.Play();
    }

    private void PlayRewardMusic(TopicRoomRuntime room)
    {
        if (room == null)
        {
            return;
        }

        if (room.rewardMusic == null && !string.IsNullOrEmpty(room.rewardMusicPath))
        {
            StartCoroutine(TopicsContentStore.LoadAudioClip(room.rewardMusicPath, clip =>
            {
                room.rewardMusic = clip;
                PlayRewardMusic(room);
            }));
            return;
        }

        if (room.rewardMusic == null)
        {
            return;
        }

        musicSource.clip = room.rewardMusic;
        musicSource.loop = false;
        musicSource.volume = GetMusicVolume();
        musicSource.Play();
    }

    private void PlayMenuMusicIfNeeded()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }

        AppGameManager.Instance?.PlayMenuMusic();
    }

    private void PlayClick()
    {
        AppGameManager.Instance?.PlayButtonClick();
        PlayOneShot(config != null ? config.buttonClickSound : null);
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null && AppGameManager.Instance != null)
        {
            return;
        }

        if (clip != null && soundSource != null)
        {
            soundSource.PlayOneShot(clip, GetSoundVolume());
        }
    }

    private float GetMusicVolume()
    {
        return AppGameManager.Instance != null ? AppGameManager.Instance.MusicVolume : 0.35f;
    }

    private float GetEffectsVolume()
    {
        return AppGameManager.Instance != null ? AppGameManager.Instance.EffectsVolume : config != null ? config.soundVolume : 0.85f;
    }

    private float GetSoundVolume()
    {
        return GetEffectsVolume();
    }

    private float GetSpeechVolume()
    {
        return AppGameManager.Instance != null ? AppGameManager.Instance.SpeechVolume : 1f;
    }

    private float GetFeedbackVoiceVolume()
    {
        return AppGameManager.Instance != null ? AppGameManager.Instance.FeedbackVoiceVolume : 0.9f;
    }

    private void CreateTopBar(string title, string subtitle, bool showBackToMainMenu)
    {
        Text titleText = CreateText(root, title, 58, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(titleText.rectTransform, new Vector2(0.12f, 0.88f), new Vector2(0.88f, 0.98f));

        Text subtitleText = CreateText(root, subtitle, 28, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.86f, 0.94f, 0.96f));
        SetAnchors(subtitleText.rectTransform, new Vector2(0.12f, 0.82f), new Vector2(0.88f, 0.89f));

        Button back = CreateButton(root, showBackToMainMenu ? "Меню" : "Назад", 24, new Color(0.34f, 0.42f, 0.46f), () =>
        {
            if (showBackToMainMenu)
            {
                SceneManager.LoadScene(config != null ? config.menuSceneName : "MainMenuScene");
            }
            else
            {
                ShowTopicGrid();
            }
        });
        SetAnchors(back.GetComponent<RectTransform>(), new Vector2(0.03f, 0.90f), new Vector2(0.12f, 0.97f));
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Topics Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas newCanvas = canvasObject.GetComponent<Canvas>();
        newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        return newCanvas;
    }

    private void CreateBackground()
    {
        Image background = CreatePanel(root, "Background", config != null ? config.backgroundColor : new Color(0.10f, 0.18f, 0.21f), Vector2.zero, Vector2.one);
        background.transform.SetAsFirstSibling();
    }

    private Button CreateButton(Transform parent, string label, int fontSize, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        ApplyButtonColors(button, color);
        if (action != null)
        {
            button.onClick.AddListener(action);
        }

        Text text = CreateText(buttonObject.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 14;
        text.resizeTextMaxSize = fontSize;
        SetAnchors(text.rectTransform, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f));
        return button;
    }

    private InputField CreateInput(Transform parent, string value, bool password)
    {
        GameObject inputObject = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
        inputObject.transform.SetParent(parent, false);
        inputObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.92f);
        InputField input = inputObject.GetComponent<InputField>();
        input.text = value;
        input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;

        Text text = CreateText(inputObject.transform, value, 28, FontStyle.Normal, TextAnchor.MiddleLeft, Color.black);
        SetAnchors(text.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
        input.textComponent = text;
        return input;
    }

    private Toggle CreateToggle(Transform parent, string label, bool value)
    {
        GameObject toggleObject = new GameObject(label, typeof(RectTransform), typeof(Toggle));
        toggleObject.transform.SetParent(parent, false);

        Image background = CreatePanel(toggleObject.GetComponent<RectTransform>(), "Box", Color.white, new Vector2(0f, 0.18f), new Vector2(0.12f, 0.82f));
        Image check = CreatePanel(background.rectTransform, "Check", config != null ? config.primaryColor : Color.yellow, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = check;
        toggle.isOn = value;

        Text text = CreateText(toggleObject.transform, label, 24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(text.rectTransform, new Vector2(0.16f, 0f), new Vector2(1f, 1f));
        return toggle;
    }

    private Slider CreateLabeledSlider(RectTransform parent, string label, float value, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction<float> action, out Text valueText)
    {
        GameObject group = new GameObject(label + " Slider", typeof(RectTransform));
        group.transform.SetParent(parent, false);
        SetAnchors(group.GetComponent<RectTransform>(), anchorMin, anchorMax);

        Text labelText = CreateText(group.transform, label, 26, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(labelText.rectTransform, new Vector2(0f, 0.56f), new Vector2(0.62f, 1f));

        valueText = CreateText(group.transform, Mathf.RoundToInt(value * 100f) + "%", 24, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.86f, 0.94f, 0.96f));
        SetAnchors(valueText.rectTransform, new Vector2(0.66f, 0.56f), new Vector2(1f, 1f));

        GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(group.transform, false);
        SetAnchors(sliderObject.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0.42f));

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        Image background = CreatePanel(sliderObject.GetComponent<RectTransform>(), "Background", new Color(1f, 1f, 1f, 0.22f), new Vector2(0f, 0.34f), new Vector2(1f, 0.66f));
        Image fill = CreatePanel(background.rectTransform, "Fill", config != null ? config.primaryColor : Color.yellow, Vector2.zero, Vector2.one);
        Image handle = CreatePanel(sliderObject.GetComponent<RectTransform>(), "Handle", Color.white, new Vector2(0f, 0.12f), new Vector2(0.045f, 0.88f));

        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        slider.targetGraphic = handle;
        slider.fillRect = fillRect;
        slider.handleRect = handle.rectTransform;
        slider.value = Mathf.Clamp01(value);
        if (action != null)
        {
            slider.onValueChanged.AddListener(action);
        }

        return slider;
    }

    private Text CreateText(Transform parent, string value, int size, FontStyle style, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = uiFont;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Image CreatePanel(RectTransform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        SetAnchors(panelObject.GetComponent<RectTransform>(), anchorMin, anchorMax);
        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Image CreateImage(RectTransform parent, string name, Sprite sprite, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        Image image = CreatePanel(parent, name, color, anchorMin, anchorMax);
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        return image;
    }

    private void AddDwell(GameObject target, RectTransform rect)
    {
        Image progress = CreatePanel(rect, "Dwell Progress", new Color(1f, 1f, 1f, 0.35f), new Vector2(0f, 0f), new Vector2(1f, 0.06f));
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        target.GetComponent<DwellSelectable>().Configure(config != null ? config.dwellSeconds : 1.1f, progress);
    }

    private void ApplyButtonColors(Button button, Color color)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.16f);
        colors.disabledColor = new Color(color.r, color.g, color.b, 0.35f);
        button.colors = colors;
    }

    private void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ClearRoot()
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    private void EnsureCamera()
    {
        if (Camera.main != null)
        {
            if (Camera.main.GetComponent<AudioListener>() == null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
            }
            return;
        }

        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = config != null ? config.backgroundColor : new Color(0.10f, 0.18f, 0.21f);
        camera.orthographic = true;
        cameraObject.tag = "MainCamera";
    }

    private TopicRoomRuntime CloneForEditing(TopicRoomRuntime room)
    {
        TopicRoomRuntime clone = new TopicRoomRuntime
        {
            id = room.isUserCreated ? room.id : Guid.NewGuid().ToString("N"),
            title = room.title,
            cardSymbol = room.cardSymbol,
            icon = room.icon,
            iconPath = room.iconPath,
            cardColor = room.cardColor,
            roomMusic = room.roomMusic,
            musicPath = room.musicPath,
            rewardMusic = room.rewardMusic,
            rewardMusicPath = room.rewardMusicPath,
            useTextToSpeech = room.useTextToSpeech,
            speechVolume = room.speechVolume,
            speechRate = room.speechRate,
            speechPitch = room.speechPitch,
            androidLanguage = room.androidLanguage,
            introEnabled = room.introEnabled,
            autoStartAfterIntro = room.autoStartAfterIntro,
            defaultAnswersToShow = room.defaultAnswersToShow,
            questionsPerRun = room.questionsPerRun,
            rewardEffectEnabled = room.rewardEffectEnabled,
            rewardMessage = room.rewardMessage,
            isUserCreated = true
        };

        foreach (TopicIntroPageRuntime intro in room.introPages)
        {
            clone.introPages.Add(new TopicIntroPageRuntime { text = intro.text, narration = intro.narration, narrationPath = intro.narrationPath, photos = new List<Sprite>(intro.photos), photoPaths = new List<string>(intro.photoPaths) });
        }

        foreach (TopicQuestionRuntime question in room.questions)
        {
            TopicQuestionRuntime questionClone = new TopicQuestionRuntime
            {
                id = question.id,
                text = question.text,
                image = question.image,
                imagePath = question.imagePath,
                questionSound = question.questionSound,
                questionSoundPath = question.questionSoundPath,
                answersToShow = question.answersToShow
            };

            foreach (TopicAnswerRuntime answer in question.answers)
            {
                questionClone.answers.Add(new TopicAnswerRuntime
                {
                    id = answer.id,
                    text = answer.text,
                    textSize = answer.textSize,
                    textColor = answer.textColor,
                    image = answer.image,
                    imagePath = answer.imagePath,
                    sound = answer.sound,
                    soundPath = answer.soundPath,
                    isCorrect = answer.isCorrect
                });
            }

            clone.questions.Add(questionClone);
        }

        return clone;
    }

    private InputField CreateLabeledInput(RectTransform parent, string label, string value, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject group = new GameObject(label, typeof(RectTransform));
        group.transform.SetParent(parent, false);
        SetAnchors(group.GetComponent<RectTransform>(), anchorMin, anchorMax);

        Text labelText = CreateText(group.transform, label, 18, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.82f));
        SetAnchors(labelText.rectTransform, new Vector2(0f, 0.68f), new Vector2(1f, 1f));

        InputField input = CreateInput(group.transform, value, false);
        SetAnchors(input.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0.66f));
        return input;
    }

    private void CreateQuestionListRow(Transform parent, TopicRoomRuntime room, int index, int selectedIndex)
    {
        TopicQuestionRuntime question = room.questions[index];
        GameObject row = new GameObject("Question Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 42f;

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;

        string title = string.IsNullOrWhiteSpace(question.text) ? "Вопрос " + (index + 1) : (index + 1) + ". " + question.text;
        Button select = CreateButton(row.transform, title, 18, index == selectedIndex ? (config != null ? config.primaryColor : Color.yellow) : new Color(0.28f, 0.36f, 0.40f), () => ShowRoomEditor(room, index));
        select.gameObject.AddComponent<LayoutElement>().preferredWidth = 430f;

        Button delete = CreateButton(row.transform, "X", 18, new Color(0.68f, 0.25f, 0.25f), () =>
        {
            if (room.questions.Count <= 1)
            {
                return;
            }

            room.questions.RemoveAt(index);
            ShowRoomEditor(room, Mathf.Clamp(selectedIndex, 0, room.questions.Count - 1));
        });
        delete.interactable = room.questions.Count > 1;
        delete.gameObject.AddComponent<LayoutElement>().preferredWidth = 48f;
    }

    private void CreateAnswerEditorRow(
        Transform parent,
        TopicRoomRuntime room,
        TopicQuestionRuntime question,
        int index,
        int selectedQuestionIndex,
        List<InputField> answerInputs,
        List<Toggle> correctToggles)
    {
        TopicAnswerRuntime answer = question.answers[index];
        GameObject row = new GameObject("Answer Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = answer.isCorrect ? new Color(0.28f, 0.84f, 0.38f, 0.16f) : new Color(1f, 1f, 1f, 0.06f);
        row.GetComponent<LayoutElement>().preferredHeight = 58f;

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;

        Toggle correct = CreateToggle(row.transform, "Верный", answer.isCorrect);
        correct.gameObject.AddComponent<LayoutElement>().preferredWidth = 150f;
        correctToggles.Add(correct);

        InputField input = CreateInput(row.transform, answer.text, false);
        input.gameObject.AddComponent<LayoutElement>().preferredWidth = 520f;
        answerInputs.Add(input);

        Button image = CreateButton(row.transform, "Фото", 18, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            TopicsFilePicker.PickImage(path =>
            {
                string copied = TopicsFilePicker.CopyToMediaFolder(path, "Images");
                if (!string.IsNullOrEmpty(copied))
                {
                    answer.imagePath = copied;
                    answer.image = TopicsContentStore.LoadSprite(copied);
                }
            });
        });
        image.gameObject.AddComponent<LayoutElement>().preferredWidth = 82f;

        Button sound = CreateButton(row.transform, "Звук", 18, new Color(0.29f, 0.54f, 0.68f), () =>
        {
            TopicsFilePicker.PickAudio(path =>
            {
                string copied = TopicsFilePicker.CopyToMediaFolder(path, "Audio");
                if (!string.IsNullOrEmpty(copied))
                {
                    answer.soundPath = copied;
                    answer.sound = null;
                }
            });
        });
        sound.gameObject.AddComponent<LayoutElement>().preferredWidth = 82f;

        Button delete = CreateButton(row.transform, "X", 18, new Color(0.68f, 0.25f, 0.25f), () =>
        {
            if (question.answers.Count <= 2)
            {
                return;
            }

            question.answers.RemoveAt(index);
            question.answersToShow = Mathf.Clamp(question.answersToShow, 2, question.answers.Count);
            EnsureMinimumAnswers(question);
            ShowRoomEditor(room, selectedQuestionIndex);
        });
        delete.interactable = question.answers.Count > 2;
        delete.gameObject.AddComponent<LayoutElement>().preferredWidth = 48f;
    }

    private void EnsureMinimumAnswers(TopicQuestionRuntime question)
    {
        while (question.answers.Count < 2)
        {
            question.answers.Add(CreateEmptyAnswer(question.answers.Count == 0));
        }

        if (!question.answers.Exists(answer => answer.isCorrect))
        {
            question.answers[0].isCorrect = true;
        }

        question.answersToShow = Mathf.Clamp(question.answersToShow <= 0 ? Mathf.Min(4, question.answers.Count) : question.answersToShow, 2, Mathf.Max(2, question.answers.Count));
    }

    private TopicAnswerRuntime CreateEmptyAnswer(bool correct)
    {
        return new TopicAnswerRuntime
        {
            id = Guid.NewGuid().ToString("N"),
            text = correct ? "Верный ответ" : "Новый ответ",
            textSize = 52,
            textColor = Color.white,
            isCorrect = correct
        };
    }

    private void ApplyRoomEditorFields(
        TopicRoomRuntime room,
        TopicQuestionRuntime question,
        InputField titleInput,
        InputField symbolInput,
        Toggle introToggle,
        InputField introInput,
        InputField questionInput,
        List<InputField> answerInputs,
        List<Toggle> correctToggles,
        int answersToShow)
    {
        room.title = string.IsNullOrWhiteSpace(titleInput.text) ? "Новая тема" : titleInput.text.Trim();
        room.cardSymbol = string.IsNullOrWhiteSpace(symbolInput.text) ? "?" : symbolInput.text.Trim();
        room.introEnabled = introToggle.isOn;

        if (room.introPages.Count == 0)
        {
            room.introPages.Add(new TopicIntroPageRuntime());
        }

        room.introPages[0].text = introInput.text;

        if (questionInput != null)
        {
            question.text = questionInput.text;
        }

        if (answerInputs != null && correctToggles != null)
        {
            int correctIndex = 0;
            for (int i = 0; i < correctToggles.Count; i++)
            {
                if (correctToggles[i].isOn)
                {
                    correctIndex = i;
                    break;
                }
            }

            for (int i = 0; i < question.answers.Count && i < answerInputs.Count; i++)
            {
                question.answers[i].text = string.IsNullOrWhiteSpace(answerInputs[i].text) ? "Ответ " + (i + 1) : answerInputs[i].text.Trim();
                question.answers[i].isCorrect = i == correctIndex;
            }
        }

        EnsureMinimumAnswers(question);
        question.answersToShow = Mathf.Clamp(answersToShow, 2, Mathf.Max(2, question.answers.Count));
    }

    private void EnsureFourAnswers(TopicQuestionRuntime question, string correct, string wrong1, string wrong2, string wrong3)
    {
        question.answers.Clear();
        question.answers.Add(new TopicAnswerRuntime { id = Guid.NewGuid().ToString("N"), text = string.IsNullOrWhiteSpace(correct) ? "Верно" : correct, textSize = 52, textColor = Color.white, isCorrect = true });
        question.answers.Add(new TopicAnswerRuntime { id = Guid.NewGuid().ToString("N"), text = string.IsNullOrWhiteSpace(wrong1) ? "Ответ 2" : wrong1, textSize = 52, textColor = Color.white });
        question.answers.Add(new TopicAnswerRuntime { id = Guid.NewGuid().ToString("N"), text = string.IsNullOrWhiteSpace(wrong2) ? "Ответ 3" : wrong2, textSize = 52, textColor = Color.white });
        question.answers.Add(new TopicAnswerRuntime { id = Guid.NewGuid().ToString("N"), text = string.IsNullOrWhiteSpace(wrong3) ? "Ответ 4" : wrong3, textSize = 52, textColor = Color.white });
        question.answersToShow = 4;
    }

    private void ApplyRoomEditorFields(
        TopicRoomRuntime room,
        TopicQuestionRuntime question,
        InputField titleInput,
        InputField symbolInput,
        Toggle introToggle,
        InputField introInput,
        InputField questionInput,
        InputField correctInput,
        InputField wrong1Input,
        InputField wrong2Input,
        InputField wrong3Input)
    {
        room.title = string.IsNullOrWhiteSpace(titleInput.text) ? "Новая тема" : titleInput.text.Trim();
        room.cardSymbol = string.IsNullOrWhiteSpace(symbolInput.text) ? "?" : symbolInput.text.Trim();
        room.introEnabled = introToggle.isOn;

        if (room.introPages.Count == 0)
        {
            room.introPages.Add(new TopicIntroPageRuntime());
        }

        room.introPages[0].text = introInput.text;
        question.text = questionInput.text;
        EnsureFourAnswers(question, correctInput.text, wrong1Input.text, wrong2Input.text, wrong3Input.text);
    }

    private string FindCorrectAnswer(TopicQuestionRuntime question)
    {
        TopicAnswerRuntime answer = question.answers.Find(item => item.isCorrect);
        return answer != null ? answer.text : "Верно";
    }

    private string GetAnswerText(TopicQuestionRuntime question, int wrongIndex, string fallback)
    {
        List<TopicAnswerRuntime> wrong = question.answers.FindAll(item => !item.isCorrect);
        return wrongIndex - 1 >= 0 && wrongIndex - 1 < wrong.Count ? wrong[wrongIndex - 1].text : fallback;
    }

    private static float ConfigValue(float value, float fallback)
    {
        return value > 0f ? value : fallback;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int random = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[random];
            list[random] = temp;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ShopGameController : MonoBehaviour
{
    [Header("Editable settings")]
    [SerializeField] private ShopGameConfig config;

    private Canvas canvas;
    private RectTransform root;
    private Font uiFont;
    private AudioSource soundSource;
    private AudioSource musicSource;
    private StoryTextSpeaker textSpeaker;
    private ShopContentStore store;
    private List<ShopProductRuntime> products = new List<ShopProductRuntime>();
    private List<ShopProductRuntime> levelPool = new List<ShopProductRuntime>();
    private readonly List<ShopProductRuntime> currentShelfProducts = new List<ShopProductRuntime>();
    private readonly List<ShopProductView> shelfViews = new List<ShopProductView>();
    private readonly Dictionary<ShopProductView, Outline> shelfOutlines = new Dictionary<ShopProductView, Outline>();
    private RectTransform basketRect;
    private RectTransform basketItemsRoot;
    private Text taskText;
    private Text hintText;
    private RectTransform shelfPanelRect;
    private RectTransform shelfGridRect;
    private GridLayoutGroup shelfGrid;
    private ShopProductView selectedView;
    private int dragOriginalIndex = -1;
    private ShopTask currentTask;
    private int currentLevelIndex;
    private int questionIndex;
    private int correctTasks;
    private int repeatCount;
    private float actionTimer;
    private bool awaitingAction;
    private bool feedbackActive;

    private void Start()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (config == null && AppGameManager.Instance != null)
        {
            config = AppGameManager.Instance.ShopGameConfig;
        }

        EnsureServices();
        store = new ShopContentStore(config);
        RefreshProducts();
        ShowLevelSelect();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ShowLevelSelect();
            return;
        }

        if (awaitingAction && !feedbackActive)
        {
            actionTimer += Time.unscaledDeltaTime;
            if (actionTimer >= ConfigValue(config != null ? config.noActionRepeatDelay : 8f, 8f))
            {
                actionTimer = 0f;
                if (repeatCount < (config != null ? config.maxQuestionRepeats : 2))
                {
                    repeatCount++;
                    SpeakTask();
                }
                else
                {
                    StartCoroutine(SkipTaskAfterDelay());
                }
            }
        }

        if (!feedbackActive && shelfViews.Count > 0)
        {
            for (int i = 0; i < Mathf.Min(9, shelfViews.Count); i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i)))
                {
                    GazePointer.TryGetScreenPoint(out Vector2 point);
                    GazePointer.NotifyActivation(point);
                    TryPlaceProduct(shelfViews[i], false);
                    break;
                }
            }

            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && selectedView != null)
            {
                TryPlaceProduct(selectedView, false);
            }
        }
    }

    public void BeginDrag(ShopProductView view, Vector2 screenPoint)
    {
        if (feedbackActive || view == null)
        {
            return;
        }

        selectedView = view;
        HighlightSelected();
        dragOriginalIndex = Mathf.Clamp(view.ShelfIndex, 0, currentShelfProducts.Count - 1);
        if (dragOriginalIndex >= 0 && dragOriginalIndex < currentShelfProducts.Count)
        {
            currentShelfProducts.RemoveAt(dragOriginalIndex);
        }

        view.transform.SetParent(root, true);
        view.transform.SetAsLastSibling();
        view.SetScreenPosition(screenPoint);
        RenderShelfProducts();
    }

    public void DragProduct(ShopProductView view, Vector2 screenPoint)
    {
        if (view != null)
        {
            view.SetScreenPosition(screenPoint);
        }
    }

    public void EndDrag(ShopProductView view, Vector2 screenPoint)
    {
        if (view == null)
        {
            return;
        }

        if (basketRect != null && RectTransformUtility.RectangleContainsScreenPoint(basketRect, screenPoint, null))
        {
            TryPlaceProduct(view, true);
            return;
        }

        if (shelfPanelRect != null && RectTransformUtility.RectangleContainsScreenPoint(shelfPanelRect, screenPoint, null))
        {
            InsertDraggedProductOnShelf(view, screenPoint);
            return;
        }

        RestoreDraggedProduct(view);
    }

    private void EnsureServices()
    {
        EnsureEventSystem();
        EnsureCamera();
        if (FindObjectOfType<GazePointer>() == null)
        {
            gameObject.AddComponent<GazePointer>();
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

    private void RefreshProducts()
    {
        products = store.GetProducts();
    }

    private void ShowLevelSelect()
    {
        StopAllCoroutines();
        textSpeaker.Stop();
        awaitingAction = false;
        feedbackActive = false;
        selectedView = null;
        ClearRoot();
        PlayRoomMusic();

        CreateBackground();
        CreateTopBar(config != null ? config.title : "Магазин", config != null ? config.subtitle : "Собери покупки", true);

        ShopLevelSettings[] levels = config != null && config.levels != null && config.levels.Length > 0 ? config.levels : new[] { new ShopLevelSettings() };
        for (int i = 0; i < levels.Length; i++)
        {
            int levelIndex = i;
            float minX = 0.12f + i * 0.28f;
            Image panel = CreatePanel(root, "Level " + (i + 1), new Color(0.17f + i * 0.08f, 0.36f, 0.48f, 0.96f), new Vector2(minX, 0.30f), new Vector2(minX + 0.22f, 0.66f));
            Button button = panel.gameObject.AddComponent<Button>();
            ApplyButtonColors(button, panel.color);
            button.onClick.AddListener(() =>
            {
                PlayClick();
                StartLevel(levelIndex);
            });

            Text number = CreateText(panel.rectTransform, (i + 1).ToString(), 92, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            SetAnchors(number.rectTransform, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.88f));
            Text title = CreateText(panel.rectTransform, levels[i].title, 34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 20;
            title.resizeTextMaxSize = 34;
            SetAnchors(title.rectTransform, new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.42f));
            Text count = CreateText(panel.rectTransform, levels[i].questionCount + " заданий", 24, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.86f));
            SetAnchors(count.rectTransform, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.16f));
            AddDwell(panel.gameObject, panel.rectTransform);
        }

        Button editor = CreateButton(root, "Редактор", 24, config != null ? config.primaryColor : Color.yellow, ShowEditorGate);
        SetAnchors(editor.GetComponent<RectTransform>(), new Vector2(0.84f, 0.90f), new Vector2(0.96f, 0.97f));
    }

    private void StartLevel(int levelIndex)
    {
        RefreshProducts();
        List<ShopProductRuntime> active = products.FindAll(product => product.active);
        if (active.Count == 0)
        {
            ShowLevelSelect();
            return;
        }

        Shuffle(active);
        ShopLevelSettings level = GetLevel(levelIndex);
        int poolSize = Mathf.Clamp(level.productPoolSize, 1, active.Count);
        levelPool = active.GetRange(0, poolSize);
        currentLevelIndex = levelIndex;
        questionIndex = 0;
        correctTasks = 0;
        ShowTask();
    }

    private void ShowTask()
    {
        ShopLevelSettings level = GetLevel(currentLevelIndex);
        if (questionIndex >= level.questionCount)
        {
            ShowResult(level);
            return;
        }

        textSpeaker.Stop();
        ClearRoot();
        shelfViews.Clear();
        shelfOutlines.Clear();
        selectedView = null;
        awaitingAction = true;
        feedbackActive = false;
        actionTimer = 0f;
        repeatCount = 0;
        currentTask = GenerateTask(level, questionIndex);

        CreateBackground();
        CreateTopBar(config != null ? config.title : "Магазин", level.title + ": " + (questionIndex + 1) + " из " + level.questionCount, false);

        Image taskPanel = CreatePanel(root, "Task Panel", config != null ? config.panelColor : new Color(0f, 0f, 0f, 0.7f), new Vector2(0.08f, 0.77f), new Vector2(0.92f, 0.90f));
        taskText = CreateText(taskPanel.rectTransform, currentTask.prompt, 42, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        taskText.resizeTextForBestFit = true;
        taskText.resizeTextMinSize = 26;
        taskText.resizeTextMaxSize = 42;
        SetAnchors(taskText.rectTransform, new Vector2(0.04f, 0.10f), new Vector2(0.82f, 0.92f));

        Button repeat = CreateButton(taskPanel.rectTransform, "Повторить", 24, new Color(0.28f, 0.54f, 0.68f), SpeakTask);
        SetAnchors(repeat.GetComponent<RectTransform>(), new Vector2(0.84f, 0.18f), new Vector2(0.98f, 0.82f));

        CreateShelf();
        CreateBasket();
        SpeakTask();
    }

    private void CreateShelf()
    {
        Image shelfPanel = CreatePanel(root, "Shelf", new Color(0.18f, 0.11f, 0.07f, config != null && config.storeBackground != null ? 0.22f : 0.96f), new Vector2(0.06f, 0.10f), new Vector2(0.68f, 0.72f));
        shelfPanelRect = shelfPanel.rectTransform;
        GameObject gridObject = new GameObject("Shelf Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridObject.transform.SetParent(shelfPanel.rectTransform, false);
        shelfGridRect = gridObject.GetComponent<RectTransform>();
        SetAnchors(shelfGridRect, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f));

        shelfGrid = gridObject.GetComponent<GridLayoutGroup>();
        shelfGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        shelfGrid.constraintCount = 4;
        shelfGrid.cellSize = new Vector2(250f, 245f);
        shelfGrid.spacing = new Vector2(26f, 28f);

        currentShelfProducts.Clear();
        currentShelfProducts.AddRange(BuildShelfProducts());
        RenderShelfProducts();
    }

    private void CreateBasket()
    {
        Image basket = CreatePanel(root, "Basket", config != null ? config.basketColor : new Color(0.92f, 0.68f, 0.34f), new Vector2(0.72f, 0.12f), new Vector2(0.94f, 0.70f));
        basketRect = basket.rectTransform;
        if (config != null && config.basketSprite != null)
        {
            basket.sprite = config.basketSprite;
            basket.type = Image.Type.Simple;
            basket.preserveAspect = true;
            basket.color = Color.white;
        }
        Button basketButton = basket.gameObject.AddComponent<Button>();
        ApplyButtonColors(basketButton, basket.color);
        basketButton.onClick.AddListener(() =>
        {
            if (selectedView != null)
            {
                TryPlaceProduct(selectedView, false);
            }
        });
        AddDwell(basket.gameObject, basket.rectTransform);

        Text basketTitle = CreateText(basket.rectTransform, "Корзина", 40, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(basketTitle.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.97f));

        GameObject items = new GameObject("Basket Items", typeof(RectTransform));
        items.transform.SetParent(basket.rectTransform, false);
        basketItemsRoot = items.GetComponent<RectTransform>();
        SetAnchors(basketItemsRoot, new Vector2(0.10f, 0.20f), new Vector2(0.90f, 0.78f));

        hintText = CreateText(basket.rectTransform, "Нажми продукт, чтобы положить его сюда", 24, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.12f, 0.18f, 0.20f));
        hintText.resizeTextForBestFit = true;
        hintText.resizeTextMinSize = 16;
        hintText.resizeTextMaxSize = 24;
        SetAnchors(hintText.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.16f));
        basket.gameObject.AddComponent<UIHoverWiggle>();
    }

    private void CreateProductCard(Transform parent, ShopProductRuntime product, int number)
    {
        Color color = product.cardColor;
        GameObject card = new GameObject(product.displayName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(DwellSelectable), typeof(ShopProductView));
        card.transform.SetParent(parent, false);
        Image background = card.GetComponent<Image>();
        background.color = color;

        Button button = card.GetComponent<Button>();
        ApplyButtonColors(button, color);

        ShopProductView view = card.GetComponent<ShopProductView>();
        view.Configure(this, product, shelfViews.Count);
        button.onClick.AddListener(() =>
        {
            if (view.ConsumeSuppressedClick())
            {
                return;
            }

            GazePointer.TryGetScreenPoint(out Vector2 point);
            GazePointer.NotifyActivation(point);
            TryPlaceProduct(view, false);
        });

        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.22f);
        outline.effectDistance = new Vector2(4f, -4f);
        shelfOutlines[view] = outline;

        RectTransform rect = card.GetComponent<RectTransform>();
        Text index = CreateText(rect, number.ToString(), 22, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.86f));
        SetAnchors(index.rectTransform, new Vector2(0.04f, 0.80f), new Vector2(0.20f, 0.96f));

        if (product.icon != null)
        {
            Image icon = CreateImage(rect, "Icon", product.icon, Color.white, new Vector2(0.12f, 0.24f), new Vector2(0.88f, 0.92f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
        else
        {
            Text symbol = CreateText(rect, "?", 62, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            SetAnchors(symbol.rectTransform, new Vector2(0.12f, 0.30f), new Vector2(0.88f, 0.88f));
        }

        Text title = CreateText(rect, product.displayName, 30, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 18;
        title.resizeTextMaxSize = 30;
        SetAnchors(title.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.24f));

        AddDwell(card, rect);
        card.AddComponent<UIHoverWiggle>();
        view.SetHome();
        shelfViews.Add(view);
    }

    private void SelectProduct(ShopProductView view)
    {
        if (feedbackActive || view == null)
        {
            return;
        }

        selectedView = view;
        HighlightSelected();
        if (hintText != null)
        {
            hintText.text = "Теперь выбери корзину";
        }
    }

    private void TryPlaceProduct(ShopProductView view, bool fromDrag)
    {
        if (feedbackActive || currentTask == null || view == null || view.Product == null)
        {
            return;
        }

        bool correct = currentTask.remaining.ContainsKey(view.Product.id) && currentTask.remaining[view.Product.id] > 0;
        if (correct)
        {
            HandleCorrectProduct(view, fromDrag);
        }
        else
        {
            HandleWrongProduct(view, fromDrag);
        }
    }

    private void HandleCorrectProduct(ShopProductView view, bool fromDrag)
    {
        feedbackActive = true;
        actionTimer = 0f;
        currentTask.remaining[view.Product.id]--;
        AddProductToBasket(view.Product);
        PlayCorrectSound(view.Product);
        PlayCelebration(view.gameObject);
        bool taskComplete = currentTask.IsComplete;
        if (fromDrag)
        {
            Destroy(view.gameObject);
            dragOriginalIndex = -1;
        }
        else
        {
            view.ReturnHome();
        }
        selectedView = null;
        HighlightSelected();

        if (taskComplete)
        {
            correctTasks++;
            awaitingAction = false;
            StartCoroutine(NextTaskAfterDelay());
        }
        else
        {
            feedbackActive = false;
            RefreshShelf();
            if (hintText != null)
            {
                hintText.text = "Продолжай собирать список";
            }
        }
    }

    private void HandleWrongProduct(ShopProductView view, bool fromDrag)
    {
        PlayWrongSound();
        if (fromDrag)
        {
            RestoreDraggedProduct(view);
        }
        else
        {
            StartCoroutine(FlashOutline(view, config != null ? config.wrongColor : Color.red));
        }
    }

    private void AddProductToBasket(ShopProductRuntime product)
    {
        StartCoroutine(AnimateBasketWiggle());

        GameObject item = new GameObject(product.displayName, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(basketItemsRoot, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        int itemIndex = basketItemsRoot != null ? basketItemsRoot.childCount - 1 : 0;
        int column = itemIndex % 2;
        int row = itemIndex / 2;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2((column - 0.5f) * 105f, -row * 88f + 42f);
        rect.sizeDelta = new Vector2(150f, 135f);
        rect.localScale = Vector3.one * 0.82f;

        Image background = item.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.18f);
        background.raycastTarget = false;

        if (product.icon != null)
        {
            Image icon = CreateImage(rect, "Basket Product Icon", product.icon, Color.white, new Vector2(0.10f, 0.18f), new Vector2(0.90f, 0.96f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        Text title = CreateText(rect, product.displayName, 20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 14;
        title.resizeTextMaxSize = 20;
        SetAnchors(title.rectTransform, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.20f));
        StartCoroutine(PopBasketItem(rect));
    }

    private IEnumerator NextTaskAfterDelay()
    {
        yield return new WaitForSeconds(config != null ? config.correctDelay : 0.75f);
        questionIndex++;
        ShowTask();
    }

    private IEnumerator SkipTaskAfterDelay()
    {
        feedbackActive = true;
        awaitingAction = false;
        if (hintText != null)
        {
            hintText.text = "Переходим дальше";
        }
        yield return new WaitForSeconds(1f);
        questionIndex++;
        ShowTask();
    }

    private IEnumerator AnimateShelfRefill(RectTransform rect)
    {
        if (rect == null)
        {
            yield break;
        }

        Vector3 start = Vector3.one * 0.88f;
        Vector3 overshoot = Vector3.one * 1.12f;
        float elapsed = 0f;
        while (elapsed < 0.22f)
        {
            if (rect == null)
            {
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.22f);
            rect.localScale = Vector3.Lerp(start, overshoot, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < 0.18f)
        {
            if (rect == null)
            {
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.18f);
            rect.localScale = Vector3.Lerp(overshoot, Vector3.one, t);
            yield return null;
        }

        if (rect != null)
        {
            rect.localScale = Vector3.one;
        }
    }

    private IEnumerator AnimateBasketWiggle()
    {
        if (basketRect == null)
        {
            yield break;
        }

        float elapsed = 0f;
        const float duration = 0.42f;
        while (basketRect != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float angle = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t) * 7f;
            basketRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            basketRect.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.035f);
            yield return null;
        }

        if (basketRect != null)
        {
            basketRect.localRotation = Quaternion.identity;
            basketRect.localScale = Vector3.one;
        }
    }

    private IEnumerator PopBasketItem(RectTransform rect)
    {
        if (rect == null)
        {
            yield break;
        }

        Vector3 start = rect.localScale;
        Vector3 overshoot = Vector3.one * 1.08f;
        float elapsed = 0f;
        while (rect != null && elapsed < 0.16f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.16f);
            rect.localScale = Vector3.Lerp(start, overshoot, t);
            yield return null;
        }

        elapsed = 0f;
        while (rect != null && elapsed < 0.14f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.14f);
            rect.localScale = Vector3.Lerp(overshoot, Vector3.one, t);
            yield return null;
        }

        if (rect != null)
        {
            rect.localScale = Vector3.one;
        }
    }

    private IEnumerator FlashOutline(ShopProductView view, Color color)
    {
        if (view == null || !shelfOutlines.ContainsKey(view))
        {
            yield break;
        }

        Outline outline = shelfOutlines[view];
        Color original = outline.effectColor;
        Vector2 originalDistance = outline.effectDistance;
        outline.effectColor = color;
        outline.effectDistance = new Vector2(9f, -9f);
        yield return new WaitForSeconds(0.35f);
        outline.effectColor = original;
        outline.effectDistance = originalDistance;
    }

    private void ShowResult(ShopLevelSettings level)
    {
        textSpeaker.Stop();
        awaitingAction = false;
        feedbackActive = false;
        ClearRoot();
        CreateBackground();
        CreateTopBar(config != null ? config.title : "Магазин", "Готово!", false);

        Image panel = CreatePanel(root, "Result", config != null ? config.panelColor : new Color(0f, 0f, 0f, 0.7f), new Vector2(0.20f, 0.27f), new Vector2(0.80f, 0.68f));
        Text result = CreateText(panel.rectTransform, "Выполнено заданий: " + correctTasks + " из " + level.questionCount, 54, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(result.rectTransform, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.82f));
        Text reward = CreateText(panel.rectTransform, "Отличные покупки!", 38, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.55f));
        SetAnchors(reward.rectTransform, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.34f));

        if (config != null && config.celebrationSound != null)
        {
            PlayOneShot(config.celebrationSound);
        }
        else
        {
            AppGameManager.Instance?.PlayCelebration();
        }
        AnswerCelebrationEffect.Play(canvas, new Vector2(Screen.width * 0.5f, Screen.height * 0.58f));
        StartCoroutine(ReturnToMenuAfterResult());
    }

    private IEnumerator ReturnToMenuAfterResult()
    {
        yield return new WaitForSeconds(config != null ? config.resultAutoReturnDelay : 5f);
        ShowLevelSelect();
    }

    private ShopTask GenerateTask(ShopLevelSettings level, int index)
    {
        ShopTask task = new ShopTask();
        int maxKindsForProgress = Mathf.Clamp(level.minProductKinds + index / 4, level.minProductKinds, level.maxProductKinds);
        int kinds = UnityEngine.Random.Range(level.minProductKinds, maxKindsForProgress + 1);
        int maxQtyForProgress = Mathf.Clamp(level.minQuantity + index / 4, level.minQuantity, level.maxQuantity);

        List<ShopProductRuntime> candidates = new List<ShopProductRuntime>(levelPool);
        Shuffle(candidates);
        for (int i = 0; i < kinds && i < candidates.Count; i++)
        {
            int quantity = UnityEngine.Random.Range(level.minQuantity, maxQtyForProgress + 1);
            ShopTaskItem item = new ShopTaskItem { product = candidates[i], quantity = quantity };
            task.items.Add(item);
            task.remaining[item.product.id] = quantity;
        }

        task.prompt = BuildPrompt(task);
        return task;
    }

    private List<ShopProductRuntime> BuildShelfProducts()
    {
        List<ShopProductRuntime> shelf = new List<ShopProductRuntime>();
        foreach (ShopTaskItem item in currentTask.items)
        {
            int remaining = currentTask.remaining.ContainsKey(item.product.id) ? currentTask.remaining[item.product.id] : item.quantity;
            int copies = Mathf.Clamp(remaining, 1, 3);
            for (int i = 0; i < copies; i++)
            {
                shelf.Add(item.product);
            }
        }

        List<ShopProductRuntime> filler = new List<ShopProductRuntime>(levelPool);
        Shuffle(filler);
        int index = 0;
        int count = Mathf.Max(4, config != null ? config.shelfSlotCount : 8);
        while (shelf.Count < count && filler.Count > 0)
        {
            shelf.Add(filler[index % filler.Count]);
            index++;
        }

        Shuffle(shelf);
        if (shelf.Count > count)
        {
            shelf.RemoveRange(count, shelf.Count - count);
        }

        return shelf;
    }

    private void RefreshShelf()
    {
        if (shelfPanelRect == null || currentTask == null)
        {
            return;
        }

        currentShelfProducts.Clear();
        currentShelfProducts.AddRange(BuildShelfProducts());
        RenderShelfProducts();
    }

    private void RenderShelfProducts()
    {
        if (shelfGridRect == null)
        {
            return;
        }

        for (int i = shelfGridRect.childCount - 1; i >= 0; i--)
        {
            Destroy(shelfGridRect.GetChild(i).gameObject);
        }

        shelfViews.Clear();
        shelfOutlines.Clear();
        selectedView = null;

        for (int i = 0; i < currentShelfProducts.Count; i++)
        {
            CreateProductCard(shelfGridRect, currentShelfProducts[i], i + 1);
        }
    }

    private void InsertDraggedProductOnShelf(ShopProductView view, Vector2 screenPoint)
    {
        if (view == null || view.Product == null)
        {
            return;
        }

        int insertIndex = GetShelfInsertIndex(screenPoint);
        insertIndex = Mathf.Clamp(insertIndex, 0, currentShelfProducts.Count);
        currentShelfProducts.Insert(insertIndex, view.Product);
        Destroy(view.gameObject);
        dragOriginalIndex = -1;
        RenderShelfProducts();
    }

    private void RestoreDraggedProduct(ShopProductView view)
    {
        if (view == null || view.Product == null)
        {
            return;
        }

        int insertIndex = dragOriginalIndex >= 0 ? dragOriginalIndex : currentShelfProducts.Count;
        insertIndex = Mathf.Clamp(insertIndex, 0, currentShelfProducts.Count);
        currentShelfProducts.Insert(insertIndex, view.Product);
        Destroy(view.gameObject);
        dragOriginalIndex = -1;
        RenderShelfProducts();
    }

    private int GetShelfInsertIndex(Vector2 screenPoint)
    {
        if (shelfGridRect == null || shelfGrid == null)
        {
            return currentShelfProducts.Count;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(shelfGridRect, screenPoint, null, out Vector2 localPoint);
        Rect rect = shelfGridRect.rect;
        float x = localPoint.x - rect.xMin;
        float y = rect.yMax - localPoint.y;
        float stepX = Mathf.Max(1f, shelfGrid.cellSize.x + shelfGrid.spacing.x);
        float stepY = Mathf.Max(1f, shelfGrid.cellSize.y + shelfGrid.spacing.y);
        int column = Mathf.Clamp(Mathf.FloorToInt(x / stepX), 0, shelfGrid.constraintCount - 1);
        int row = Mathf.Clamp(Mathf.FloorToInt(y / stepY), 0, Mathf.CeilToInt((currentShelfProducts.Count + 1) / (float)shelfGrid.constraintCount));
        return Mathf.Clamp(row * shelfGrid.constraintCount + column, 0, currentShelfProducts.Count);
    }

    private string BuildPrompt(ShopTask task)
    {
        List<string> parts = new List<string>();
        foreach (ShopTaskItem item in task.items)
        {
            parts.Add(FormatQuantity(item.product, item.quantity));
        }

        return "Положи в корзину " + JoinRussian(parts) + ".";
    }

    private string FormatQuantity(ShopProductRuntime product, int quantity)
    {
        string one = !string.IsNullOrEmpty(product.spokenName) ? product.spokenName : product.displayName;
        string twoFour = !string.IsNullOrEmpty(product.countTwoFourName) ? product.countTwoFourName : one;
        string many = !string.IsNullOrEmpty(product.countManyName) ? product.countManyName : twoFour;

        if (quantity <= 1)
        {
            return one;
        }

        if (quantity >= 2 && quantity <= 4)
        {
            return NumberWord(quantity) + " " + twoFour;
        }

        return quantity + " " + many;
    }

    private void ApplyAutomaticNames(ShopProductRuntime product)
    {
        string name = string.IsNullOrWhiteSpace(product.displayName) ? "продукт" : product.displayName.Trim();
        string lower = char.ToLowerInvariant(name[0]) + (name.Length > 1 ? name.Substring(1) : "");
        product.displayName = name;
        product.spokenName = lower;
        product.countTwoFourName = GuessTwoFourName(lower);
        product.countManyName = GuessManyName(lower);
    }

    private string GuessTwoFourName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "продукта";
        }

        if (name.EndsWith("о", StringComparison.Ordinal) || name.EndsWith("е", StringComparison.Ordinal))
        {
            return name.Substring(0, name.Length - 1) + "а";
        }

        if (name.EndsWith("а", StringComparison.Ordinal))
        {
            return name.Substring(0, name.Length - 1) + "ы";
        }

        if (name.EndsWith("я", StringComparison.Ordinal))
        {
            return name.Substring(0, name.Length - 1) + "и";
        }

        return name + "а";
    }

    private string GuessManyName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "продуктов";
        }

        if (name.EndsWith("ка", StringComparison.Ordinal))
        {
            return name.Substring(0, name.Length - 2) + "ок";
        }

        if (name.EndsWith("а", StringComparison.Ordinal) || name.EndsWith("я", StringComparison.Ordinal))
        {
            return name.Substring(0, name.Length - 1);
        }

        if (name.EndsWith("о", StringComparison.Ordinal) || name.EndsWith("е", StringComparison.Ordinal))
        {
            return name.Substring(0, name.Length - 1) + "а";
        }

        return name + "ов";
    }

    private string JoinRussian(List<string> parts)
    {
        if (parts.Count == 0)
        {
            return "";
        }

        if (parts.Count == 1)
        {
            return parts[0];
        }

        if (parts.Count == 2)
        {
            return parts[0] + " и " + parts[1];
        }

        string result = "";
        for (int i = 0; i < parts.Count; i++)
        {
            if (i == parts.Count - 1)
            {
                result += " и " + parts[i];
            }
            else
            {
                result += (i == 0 ? "" : ", ") + parts[i];
            }
        }

        return result;
    }

    private string NumberWord(int value)
    {
        switch (value)
        {
            case 2:
                return "два";
            case 3:
                return "три";
            case 4:
                return "четыре";
            default:
                return value.ToString();
        }
    }

    private ShopLevelSettings GetLevel(int index)
    {
        if (config != null && config.levels != null && config.levels.Length > 0)
        {
            return config.levels[Mathf.Clamp(index, 0, config.levels.Length - 1)];
        }

        return new ShopLevelSettings();
    }

    private void SpeakTask()
    {
        PlayClick();
        if (currentTask != null && config != null && config.useTextToSpeech)
        {
            textSpeaker.Speak(currentTask.prompt, GetSpeechVolume(), config.speechRate, config.speechPitch, config.androidLanguage);
        }
    }

    private void PlayCelebration(GameObject sourceObject)
    {
        Vector2 screenPoint;
        if (!GazePointer.TryGetLastActivationScreenPoint(out screenPoint) && !GazePointer.TryGetScreenPoint(out screenPoint))
        {
            RectTransform rect = sourceObject != null ? sourceObject.GetComponent<RectTransform>() : null;
            screenPoint = rect != null ? RectTransformUtility.WorldToScreenPoint(null, rect.position) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        AnswerCelebrationEffect.Play(canvas, screenPoint);
    }

    private void PlayCorrectSound(ShopProductRuntime product)
    {
        if (config != null && config.correctSound != null)
        {
            PlayOneShot(config.correctSound);
        }
        else
        {
            AppGameManager.Instance?.PlayCorrectAnswer();
        }
    }

    private void PlayWrongSound()
    {
        if (config != null && config.wrongSound != null)
        {
            PlayOneShot(config.wrongSound);
        }
        else
        {
            AppGameManager.Instance?.PlayWrongAnswer();
        }
    }

    private void PlayClick()
    {
        if (config != null && config.buttonClickSound != null)
        {
            PlayOneShot(config.buttonClickSound);
        }
        else
        {
            AppGameManager.Instance?.PlayButtonClick();
        }
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (soundSource != null && clip != null)
        {
            soundSource.PlayOneShot(clip, GetEffectsVolume());
        }
    }

    private void PlayRoomMusic()
    {
        if (config != null && config.roomMusic != null && musicSource != null)
        {
            musicSource.clip = config.roomMusic;
            musicSource.loop = true;
            musicSource.volume = GetMusicVolume();
            musicSource.Play();
            return;
        }

        AppGameManager.Instance?.PlayMenuMusic();
    }

    private float GetMusicVolume()
    {
        return AppGameManager.Instance != null ? AppGameManager.Instance.MusicVolume : 0.35f;
    }

    private float GetEffectsVolume()
    {
        return AppGameManager.Instance != null ? AppGameManager.Instance.EffectsVolume : 0.8f;
    }

    private float GetSpeechVolume()
    {
        return AppGameManager.Instance != null ? AppGameManager.Instance.SpeechVolume : config != null ? config.speechVolume : 1f;
    }

    private void HighlightSelected()
    {
        foreach (KeyValuePair<ShopProductView, Outline> pair in shelfOutlines)
        {
            pair.Value.effectColor = pair.Key == selectedView ? new Color(1f, 0.92f, 0.35f) : new Color(1f, 1f, 1f, 0.22f);
            pair.Value.effectDistance = pair.Key == selectedView ? new Vector2(8f, -8f) : new Vector2(4f, -4f);
        }
    }

    private void ShowEditorGate()
    {
        PlayClick();
        ClearRoot();
        CreateBackground();
        CreateTopBar(TopicsSecurity.HasPin ? "Введите PIN" : "Задайте PIN", "Редактор продуктов для взрослого", false);

        Image panel = CreatePanel(root, "PIN Panel", config != null ? config.panelColor : new Color(0f, 0f, 0f, 0.7f), new Vector2(0.30f, 0.32f), new Vector2(0.70f, 0.66f));
        Text message = CreateText(panel.rectTransform, TopicsSecurity.HasPin ? "Введите PIN-код" : "Придумайте 4-6 цифр", 34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(message.rectTransform, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.92f));
        InputField input = CreateInput(panel.rectTransform, "PIN", true);
        SetAnchors(input.GetComponent<RectTransform>(), new Vector2(0.18f, 0.42f), new Vector2(0.82f, 0.62f));
        Text error = CreateText(panel.rectTransform, "", 24, FontStyle.Normal, TextAnchor.MiddleCenter, config != null ? config.wrongColor : Color.red);
        SetAnchors(error.rectTransform, new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.39f));

        Button enter = CreateButton(panel.rectTransform, TopicsSecurity.HasPin ? "Войти" : "Сохранить", 30, config != null ? config.primaryColor : Color.yellow, () =>
        {
            if (!TopicsSecurity.IsValidPinShape(input.text))
            {
                error.text = "PIN должен быть из 4-6 цифр";
                return;
            }

            if (!TopicsSecurity.HasPin)
            {
                TopicsSecurity.SetPin(input.text);
                ShowEditorList();
                return;
            }

            if (TopicsSecurity.VerifyPin(input.text))
            {
                ShowEditorList();
            }
            else
            {
                error.text = "Неверный PIN";
            }
        });
        SetAnchors(enter.GetComponent<RectTransform>(), new Vector2(0.18f, 0.07f), new Vector2(0.50f, 0.23f));

        Button back = CreateButton(panel.rectTransform, "Назад", 28, new Color(0.36f, 0.42f, 0.46f), ShowLevelSelect);
        SetAnchors(back.GetComponent<RectTransform>(), new Vector2(0.54f, 0.07f), new Vector2(0.82f, 0.23f));
    }

    private void ShowEditorList()
    {
        RefreshProducts();
        ClearRoot();
        CreateBackground();
        CreateTopBar("Редактор магазина", "Добавление продуктов", false);

        Button add = CreateButton(root, "Новый продукт", 28, config != null ? config.primaryColor : Color.yellow, () => ShowProductEditor(store.CreateEmptyProduct()));
        SetAnchors(add.GetComponent<RectTransform>(), new Vector2(0.08f, 0.80f), new Vector2(0.28f, 0.88f));
        Button back = CreateButton(root, "К уровням", 26, new Color(0.36f, 0.42f, 0.46f), ShowLevelSelect);
        SetAnchors(back.GetComponent<RectTransform>(), new Vector2(0.78f, 0.80f), new Vector2(0.92f, 0.88f));

        GameObject scrollObject = new GameObject("Products Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObject.transform.SetParent(root, false);
        SetAnchors(scrollObject.GetComponent<RectTransform>(), new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.76f));
        scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollObject.transform, false);
        SetAnchors(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(10, 10, 10, 10);

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;

        foreach (ShopProductRuntime product in products)
        {
            CreateEditorRow(content.transform, product);
        }

        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(640f, products.Count * 92f + 30f));
    }

    private void CreateEditorRow(Transform parent, ShopProductRuntime product)
    {
        GameObject row = new GameObject(product.displayName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(DwellSelectable));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = product.isUserCreated ? new Color(1f, 1f, 1f, 0.10f) : new Color(1f, 1f, 1f, 0.06f);
        row.GetComponent<LayoutElement>().preferredHeight = 82f;
        Button button = row.GetComponent<Button>();
        ApplyButtonColors(button, row.GetComponent<Image>().color);
        button.onClick.AddListener(() =>
        {
            ShowProductEditor(product);
        });

        RectTransform rect = row.GetComponent<RectTransform>();
        if (product.icon != null)
        {
            Image icon = CreateImage(rect, "Icon", product.icon, Color.white, new Vector2(0.02f, 0.12f), new Vector2(0.08f, 0.88f));
            icon.preserveAspect = true;
        }

        Text title = CreateText(rect, product.displayName, 30, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(title.rectTransform, new Vector2(0.10f, 0.18f), new Vector2(0.78f, 0.82f));
        Text state = CreateText(rect, product.active ? "В игре" : "Выключен", 24, FontStyle.Normal, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.75f));
        SetAnchors(state.rectTransform, new Vector2(0.78f, 0.18f), new Vector2(0.96f, 0.82f));
        AddDwell(row, rect);
    }

    private void ShowProductEditor(ShopProductRuntime product)
    {
        ClearRoot();
        CreateBackground();
        CreateTopBar("Продукт", "Название и картинка", false);

        Image panel = CreatePanel(root, "Product Editor", config != null ? config.panelColor : new Color(0f, 0f, 0f, 0.7f), new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.78f));
        InputField titleInput = CreateInput(panel.rectTransform, product.displayName, false);
        SetAnchors(titleInput.GetComponent<RectTransform>(), new Vector2(0.05f, 0.68f), new Vector2(0.46f, 0.83f));
        Toggle activeToggle = CreateToggle(panel.rectTransform, "Использовать в игре", product.active);
        SetAnchors(activeToggle.GetComponent<RectTransform>(), new Vector2(0.05f, 0.48f), new Vector2(0.36f, 0.60f));

        Image preview = CreatePanel(panel.rectTransform, "Preview", product.cardColor, new Vector2(0.55f, 0.42f), new Vector2(0.92f, 0.90f));
        if (product.icon != null)
        {
            Image icon = CreateImage(preview.rectTransform, "Icon", product.icon, Color.white, new Vector2(0.14f, 0.24f), new Vector2(0.86f, 0.88f));
            icon.preserveAspect = true;
        }
        Text previewTitle = CreateText(preview.rectTransform, product.displayName, 30, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(previewTitle.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.22f));

        Button imageButton = CreateButton(panel.rectTransform, "Картинка", 24, new Color(0.28f, 0.54f, 0.68f), () =>
        {
            TopicsFilePicker.PickImage(path =>
            {
                string copied = ShopContentStore.CopyToMediaFolder(path, "Images");
                if (!string.IsNullOrEmpty(copied))
                {
                    product.iconPath = copied;
                    product.icon = TopicsContentStore.LoadSprite(copied);
                    ShowProductEditor(product);
                }
            });
        });
        SetAnchors(imageButton.GetComponent<RectTransform>(), new Vector2(0.55f, 0.25f), new Vector2(0.92f, 0.35f));

        Button save = CreateButton(panel.rectTransform, "Сохранить", 28, config != null ? config.primaryColor : Color.yellow, () =>
        {
            product.displayName = titleInput.text;
            ApplyAutomaticNames(product);
            product.active = activeToggle.isOn;
            product.isUserCreated = true;
            store.UpsertUserProduct(product);
            ShowEditorList();
        });
        SetAnchors(save.GetComponent<RectTransform>(), new Vector2(0.55f, 0.08f), new Vector2(0.72f, 0.18f));

        Button delete = CreateButton(panel.rectTransform, product.isUserCreated ? "Удалить" : "Сбросить", 24, config != null ? config.wrongColor : Color.red, () =>
        {
            store.DeleteUserProduct(product.id);
            ShowEditorList();
        });
        SetAnchors(delete.GetComponent<RectTransform>(), new Vector2(0.75f, 0.08f), new Vector2(0.92f, 0.18f));
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas newCanvas = canvasObject.GetComponent<Canvas>();
        newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = config != null ? config.referenceResolution : new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return newCanvas;
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

    private void CreateBackground()
    {
        Image background = CreatePanel(root, "Background", config != null ? config.backgroundColor : new Color(0.12f, 0.20f, 0.22f), Vector2.zero, Vector2.one);
        if (config != null && config.storeBackground != null)
        {
            background.sprite = config.storeBackground;
            background.color = Color.white;
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
        }
        background.raycastTarget = false;
    }

    private void CreateTopBar(string title, string subtitle, bool showMenuButton)
    {
        Text titleText = CreateText(root, title, 50, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(titleText.rectTransform, new Vector2(0.25f, 0.88f), new Vector2(0.75f, 0.98f));
        Text subtitleText = CreateText(root, subtitle, 28, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.82f));
        SetAnchors(subtitleText.rectTransform, new Vector2(0.25f, 0.84f), new Vector2(0.75f, 0.89f));

        Button menu = CreateButton(root, showMenuButton ? "Меню" : "Назад", 24, new Color(0.08f, 0.18f, 0.20f), () =>
        {
            PlayClick();
            if (showMenuButton)
            {
                SceneManager.LoadScene(config != null ? config.menuSceneName : "MainMenuScene");
            }
            else
            {
                ShowLevelSelect();
            }
        });
        SetAnchors(menu.GetComponent<RectTransform>(), new Vector2(0.04f, 0.90f), new Vector2(0.16f, 0.97f));
    }

    private Button CreateButton(Transform parent, string label, int size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(DwellSelectable));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        ApplyButtonColors(button, color);
        button.onClick.AddListener(onClick);

        Text text = CreateText(buttonObject.GetComponent<RectTransform>(), label, size, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 14;
        text.resizeTextMaxSize = size;
        SetAnchors(text.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f));
        AddDwell(buttonObject, buttonObject.GetComponent<RectTransform>());
        return button;
    }

    private Image CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        SetAnchors(image.rectTransform, anchorMin, anchorMax);
        return image;
    }

    private Text CreateText(Transform parent, string value, int size, FontStyle style, TextAnchor anchor, Color color)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = uiFont;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Image CreateImage(Transform parent, string name, Sprite sprite, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        SetAnchors(image.rectTransform, anchorMin, anchorMax);
        return image;
    }

    private InputField CreateInput(Transform parent, string value, bool password)
    {
        GameObject inputObject = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
        inputObject.transform.SetParent(parent, false);
        inputObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.14f);
        InputField input = inputObject.GetComponent<InputField>();
        input.text = value;
        input.contentType = password ? InputField.ContentType.Pin : InputField.ContentType.Standard;

        Text text = CreateText(inputObject.transform, value, 26, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(text.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
        Text placeholder = CreateText(inputObject.transform, value, 24, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.35f));
        SetAnchors(placeholder.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    private Toggle CreateToggle(Transform parent, string label, bool value)
    {
        GameObject toggleObject = new GameObject(label, typeof(RectTransform), typeof(Toggle));
        toggleObject.transform.SetParent(parent, false);
        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.isOn = value;

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(toggleObject.transform, false);
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = new Color(1f, 1f, 1f, 0.18f);
        SetAnchors(backgroundImage.rectTransform, new Vector2(0f, 0.20f), new Vector2(0.15f, 0.80f));

        GameObject check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(background.transform, false);
        Image checkImage = check.GetComponent<Image>();
        checkImage.color = config != null ? config.correctColor : Color.green;
        SetAnchors(checkImage.rectTransform, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkImage;

        Text text = CreateText(toggleObject.transform, label, 24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(text.rectTransform, new Vector2(0.20f, 0f), new Vector2(1f, 1f));
        return toggle;
    }

    private void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ApplyButtonColors(Button button, Color baseColor)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
        button.colors = colors;
    }

    private void AddDwell(GameObject target, RectTransform rect)
    {
        DwellSelectable dwell = target.GetComponent<DwellSelectable>();
        if (dwell == null)
        {
            dwell = target.AddComponent<DwellSelectable>();
        }

        Image progress = CreatePanel(rect, "Dwell Progress", new Color(1f, 1f, 1f, 0.28f), new Vector2(0f, 0f), new Vector2(1f, 0.04f));
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        dwell.Configure(config != null ? config.dwellSeconds : 1.1f, progress);
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
        if (Camera.main == null)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int swapIndex = UnityEngine.Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temp;
        }
    }

    private float ConfigValue(float value, float fallback)
    {
        return value > 0f ? value : fallback;
    }
}

public class ShopProductView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ShopGameController controller;
    private ShopProductRuntime product;
    private RectTransform rectTransform;
    private Transform homeParent;
    private Vector2 homePosition;
    private bool dragging;
    private bool suppressClick;

    public ShopProductRuntime Product => product;
    public int ShelfIndex { get; private set; }

    public void Configure(ShopGameController controller, ShopProductRuntime product, int shelfIndex)
    {
        this.controller = controller;
        this.product = product;
        ShelfIndex = shelfIndex;
        rectTransform = GetComponent<RectTransform>();
        homeParent = transform.parent;
        homePosition = rectTransform.anchoredPosition;
    }

    public void SetHome()
    {
        rectTransform = GetComponent<RectTransform>();
        homeParent = transform.parent;
        homePosition = rectTransform.anchoredPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = true;
        suppressClick = true;
        GazePointer.NotifyActivation(eventData.position);
        if (controller != null)
        {
            controller.BeginDrag(this, eventData.position);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        GazePointer.NotifyActivation(eventData.position);
        if (controller != null)
        {
            controller.DragProduct(this, eventData.position);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        dragging = false;
        GazePointer.NotifyActivation(eventData.position);
        if (controller != null)
        {
            controller.EndDrag(this, eventData.position);
        }
    }

    public void SetScreenPosition(Vector2 screenPoint)
    {
        if (rectTransform != null)
        {
            rectTransform.position = screenPoint;
        }
    }

    public bool ConsumeSuppressedClick()
    {
        if (!suppressClick)
        {
            return false;
        }

        suppressClick = false;
        return true;
    }

    public void ReturnHome()
    {
        if (homeParent != null)
        {
            transform.SetParent(homeParent, false);
        }

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = homePosition;
        }
    }
}

public class UIHoverWiggle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float angle = 2.5f;
    [SerializeField] private float scale = 0.025f;
    [SerializeField] private float speed = 8f;

    private RectTransform rectTransform;
    private Quaternion baseRotation;
    private Vector3 baseScale;
    private bool hovering;
    private float phase;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        baseRotation = rectTransform != null ? rectTransform.localRotation : Quaternion.identity;
        baseScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
    }

    private void Update()
    {
        if (rectTransform == null)
        {
            return;
        }

        if (!hovering)
        {
            rectTransform.localRotation = Quaternion.Slerp(rectTransform.localRotation, baseRotation, Time.unscaledDeltaTime * 10f);
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, baseScale, Time.unscaledDeltaTime * 10f);
            return;
        }

        phase += Time.unscaledDeltaTime * speed;
        float wave = Mathf.Sin(phase);
        rectTransform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, wave * angle);
        rectTransform.localScale = baseScale * (1f + Mathf.Abs(wave) * scale);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        phase = 0f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StoryGameController : MonoBehaviour
{
    [Header("Editable story game settings")]
    [SerializeField] private StoryGameConfig config;

    [Header("Runtime layout")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private StoryTextSpeaker textSpeaker;

    private Font uiFont;
    private RectTransform root;
    private Text titleText;
    private Text helperText;
    private Image background;
    private Image backgroundSprite;

    private StoryCharacterData firstCharacter;
    private StoryCharacterData secondCharacter;
    private StoryTraitData firstTrait;
    private StoryTraitData secondTrait;
    private StoryLocationData selectedLocation;
    private Step currentStep;
    private Button continueButton;

    private enum Step
    {
        Characters,
        Traits,
        Location,
        Story
    }

    private void Start()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (config == null && AppGameManager.Instance != null)
        {
            config = AppGameManager.Instance.StoryGameConfig;
        }

        EnsureEventSystem();
        CreateCamera();
        BuildBaseUi();
        ShowStep(Step.Characters);
    }

    private void BuildBaseUi()
    {
        Canvas canvas = CreateCanvas();
        root = canvas.GetComponent<RectTransform>();

        background = CreatePanel(root, "Background", config != null ? config.backgroundColor : new Color(0.16f, 0.27f, 0.31f), Vector2.zero, Vector2.one);
        background.transform.SetAsFirstSibling();

        backgroundSprite = CreatePanel(root, "Location Background", Color.white, Vector2.zero, Vector2.one);
        backgroundSprite.enabled = false;
        backgroundSprite.preserveAspect = true;
        backgroundSprite.transform.SetAsLastSibling();
        backgroundSprite.transform.SetAsFirstSibling();
        background.transform.SetAsFirstSibling();

        titleText = CreateText(root, "", 48, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(titleText.rectTransform, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));

        helperText = CreateText(root, "", 24, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.88f, 0.95f, 0.96f));
        SetAnchors(helperText.rectTransform, new Vector2(0.07f, 0.81f), new Vector2(0.93f, 0.88f));
    }

    private void ShowStep(Step step)
    {
        currentStep = step;
        continueButton = null;
        ClearDynamicChildren();

        if (config == null)
        {
            titleText.text = "StoryGameConfig is missing";
            helperText.text = "Assign a config asset on StoryGameController.";
            return;
        }

        if (step != Step.Story)
        {
            textSpeaker?.Stop();
        }

        switch (step)
        {
            case Step.Characters:
                titleText.text = config.characterSelectTitle;
                helperText.text = config.characterSelectHint;
                ShowCharacterVersus();
                break;
            case Step.Traits:
                titleText.text = config.traitSelectTitle;
                helperText.text = firstCharacter.displayName + " VS " + secondCharacter.displayName;
                ShowTraitVersus();
                break;
            case Step.Location:
                titleText.text = config.locationSelectTitle;
                helperText.text = GetTraitName(firstTrait, firstCharacter) + " " + firstCharacter.displayName + " VS " + GetTraitName(secondTrait, secondCharacter) + " " + secondCharacter.displayName;
                ShowLocationGrid();
                break;
            case Step.Story:
                titleText.text = config.storyTitle;
                helperText.text = config.storyHint;
                ShowStory();
                break;
        }
    }

    private void ShowCharacterVersus()
    {
        RectTransform left = CreateSelectionPanel("First Character Panel", new Vector2(0.04f, 0.2f), new Vector2(0.48f, 0.78f), config.firstHeroLabel);
        RectTransform right = CreateSelectionPanel("Second Character Panel", new Vector2(0.52f, 0.2f), new Vector2(0.96f, 0.78f), config.secondHeroLabel);

        PopulateCharacterPanel(left, true);
        PopulateCharacterPanel(right, false);
        CreateFooter(true);
        UpdateContinueButton();
    }

    private void PopulateCharacterPanel(RectTransform panel, bool isFirst)
    {
        RectTransform grid = CreateGrid(panel, "Character Grid", 3, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.78f), new Vector2(220f, 150f));
        foreach (StoryCharacterData character in config.characters)
        {
            if (character == null)
            {
                continue;
            }

            Button button = CreateChoiceButton(grid, character.displayName, 24, character.cardColor, character.portrait);
            StoryCharacterData captured = character;
            button.onClick.AddListener(() =>
            {
                AppGameManager.Instance?.PlayButtonClick();
                if (isFirst)
                {
                    firstCharacter = captured;
                }
                else
                {
                    secondCharacter = captured;
                }

                ShowStep(Step.Characters);
            });
        }

        StoryCharacterData selected = isFirst ? firstCharacter : secondCharacter;
        if (selected != null)
        {
            CreateSelectedHero(panel, selected);
        }
    }

    private void ShowTraitVersus()
    {
        RectTransform left = CreateSelectionPanel("First Trait Panel", new Vector2(0.04f, 0.2f), new Vector2(0.48f, 0.78f), firstCharacter.displayName);
        RectTransform right = CreateSelectionPanel("Second Trait Panel", new Vector2(0.52f, 0.2f), new Vector2(0.96f, 0.78f), secondCharacter.displayName);

        PopulateTraitPanel(left, true);
        PopulateTraitPanel(right, false);
        CreateFooter(true);
        UpdateContinueButton();
    }

    private void PopulateTraitPanel(RectTransform panel, bool isFirst)
    {
        StoryCharacterData character = isFirst ? firstCharacter : secondCharacter;
        RectTransform grid = CreateGrid(panel, "Trait Grid", 3, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.78f), new Vector2(220f, 120f));
        foreach (StoryTraitData trait in config.traits)
        {
            if (trait == null)
            {
                continue;
            }

            Button button = CreateChoiceButton(grid, trait.GetFor(character), 26, trait.cardColor, null);
            StoryTraitData captured = trait;
            button.onClick.AddListener(() =>
            {
                AppGameManager.Instance?.PlayButtonClick();
                if (isFirst)
                {
                    firstTrait = captured;
                }
                else
                {
                    secondTrait = captured;
                }

                ShowStep(Step.Traits);
            });
        }

        StoryTraitData selected = isFirst ? firstTrait : secondTrait;
        if (selected != null)
        {
            Text selectedText = CreateText(panel, selected.GetFor(character), 34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            SetAnchors(selectedText.rectTransform, new Vector2(0.08f, 0.80f), new Vector2(0.92f, 0.92f));
        }
    }

    private void ShowLocationGrid()
    {
        RectTransform grid = CreateGrid(root, "Location Grid", 3, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.78f), new Vector2(500f, 210f));
        foreach (StoryLocationData location in config.locations)
        {
            if (location == null)
            {
                continue;
            }

            Button button = CreateChoiceButton(grid, location.displayName, 32, location.cardColor, location.backgroundImage);
            StoryLocationData captured = location;
            button.onClick.AddListener(() =>
            {
                AppGameManager.Instance?.PlayButtonClick();
                selectedLocation = captured;
                ApplyLocation(captured);
                ShowStep(Step.Story);
            });
        }

        CreateFooter(false);
    }

    private void ShowStory()
    {
        RectTransform panel = CreateBox(root, "Story Panel", new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.78f), config.storyPanelColor);
        string story = GenerateStory();
        Text storyText = CreateText(panel, story, 32, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.16f, 0.16f, 0.15f));
        storyText.resizeTextForBestFit = true;
        storyText.resizeTextMinSize = 20;
        storyText.resizeTextMaxSize = 32;
        SetAnchors(storyText.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f));

        GameObject footer = CreateFooter(false);
        CreateFooterButton(footer.transform, config.againLabel, ResetGame);
        textSpeaker?.Speak(story, config);
    }

    private string GenerateStory()
    {
        StoryTemplateData template = FindTemplate();
        if (template == null)
        {
            return "";
        }

        bool sameCharacter = firstCharacter == secondCharacter;
        return template.text
            .Replace("{trait1}", GetTraitName(firstTrait, firstCharacter))
            .Replace("{trait1Cap}", Capitalize(GetTraitName(firstTrait, firstCharacter)))
            .Replace("{trait2}", GetTraitName(secondTrait, secondCharacter))
            .Replace("{trait2Cap}", Capitalize(GetTraitName(secondTrait, secondCharacter)))
            .Replace("{char1}", firstCharacter.displayName)
            .Replace("{char1Acc}", firstCharacter.accusativeName)
            .Replace("{char1Dat}", firstCharacter.dativeName)
            .Replace("{char2}", secondCharacter.displayName)
            .Replace("{char2Acc}", secondCharacter.accusativeName)
            .Replace("{char2Dat}", secondCharacter.dativeName)
            .Replace("{location}", selectedLocation.displayName)
            .Replace("{locationTo}", selectedLocation.destinationName)
            .Replace("{treasure}", selectedLocation.treasureName)
            .Replace("{travel1}", firstCharacter.travelPhrase)
            .Replace("{appeared1}", firstCharacter.appearedPhrase)
            .Replace("{sameCharacterReaction}", sameCharacter ? "Он очень удивился, увидев точно такого же героя, как он сам." : "");
    }

    private StoryTemplateData FindTemplate()
    {
        bool sameCharacter = firstCharacter == secondCharacter;
        foreach (StoryTemplateData template in config.templates)
        {
            if (template != null && template.sameCharacterOnly && template.Matches(firstTrait, secondTrait, selectedLocation, sameCharacter))
            {
                return template;
            }
        }

        foreach (StoryTemplateData template in config.templates)
        {
            if (template != null && !template.sameCharacterOnly && template.Matches(firstTrait, secondTrait, selectedLocation, sameCharacter))
            {
                return template;
            }
        }

        return config.templates != null && config.templates.Length > 0 ? config.templates[config.templates.Length - 1] : null;
    }

    private RectTransform CreateSelectionPanel(string name, Vector2 anchorMin, Vector2 anchorMax, string label)
    {
        RectTransform panel = CreateBox(root, name, anchorMin, anchorMax, config.panelColor);
        Text title = CreateText(panel, label, 30, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(title.rectTransform, new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.98f));
        return panel;
    }

    private void CreateSelectedHero(RectTransform panel, StoryCharacterData selected)
    {
        Image portrait = CreatePanel(panel, "Selected Portrait", Color.white, new Vector2(0.04f, 0.79f), new Vector2(0.20f, 0.90f));
        portrait.sprite = selected.portrait;
        portrait.preserveAspect = true;
        portrait.enabled = selected.portrait != null;

        Text selectedText = CreateText(panel, selected.displayName, 26, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(selectedText.rectTransform, new Vector2(0.22f, 0.79f), new Vector2(0.96f, 0.90f));
    }

    private GameObject CreateFooter(bool withContinue)
    {
        GameObject footer = new GameObject("Footer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        footer.transform.SetParent(root, false);
        SetAnchors(footer.GetComponent<RectTransform>(), new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.16f));

        HorizontalLayoutGroup layout = footer.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        CreateFooterButton(footer.transform, config.backLabel, GoBack);
        CreateFooterButton(footer.transform, config.menuLabel, () => SceneManager.LoadScene(config.menuSceneName));

        if (withContinue)
        {
            continueButton = CreateFooterButton(footer.transform, config.continueLabel, ContinueFromStep);
        }

        return footer;
    }

    private Button CreateFooterButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateChoiceButton(parent, label, 28, config.primaryButtonColor, null);
        button.onClick.AddListener(() =>
        {
            AppGameManager.Instance?.PlayButtonClick();
            action.Invoke();
        });
        return button;
    }

    private void ContinueFromStep()
    {
        if (currentStep == Step.Characters && firstCharacter != null && secondCharacter != null)
        {
            ShowStep(Step.Traits);
            return;
        }

        if (currentStep == Step.Traits && firstTrait != null && secondTrait != null)
        {
            ShowStep(Step.Location);
        }
    }

    private void UpdateContinueButton()
    {
        if (continueButton == null)
        {
            return;
        }

        continueButton.interactable = currentStep == Step.Characters
            ? firstCharacter != null && secondCharacter != null
            : firstTrait != null && secondTrait != null;
    }

    private void GoBack()
    {
        if (currentStep == Step.Characters)
        {
            SceneManager.LoadScene(config.menuSceneName);
            return;
        }

        ShowStep((Step)((int)currentStep - 1));
    }

    private void ResetGame()
    {
        textSpeaker?.Stop();
        firstCharacter = null;
        secondCharacter = null;
        firstTrait = null;
        secondTrait = null;
        selectedLocation = null;
        background.color = config.backgroundColor;
        backgroundSprite.enabled = false;
        ShowStep(Step.Characters);
    }

    private void ApplyLocation(StoryLocationData location)
    {
        background.color = location.backgroundColor;
        backgroundSprite.sprite = location.backgroundImage;
        backgroundSprite.enabled = location.backgroundImage != null;

        if (location.music != null)
        {
            AppGameManager.Instance?.PlayMusic(location.music, true);
        }
    }

    private RectTransform CreateGrid(Transform parent, string name, int columns, Vector2 anchorMin, Vector2 anchorMax, Vector2 cellSize)
    {
        GameObject gridObject = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup));
        gridObject.transform.SetParent(parent, false);
        RectTransform rect = gridObject.GetComponent<RectTransform>();
        SetAnchors(rect, anchorMin, anchorMax);

        GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.spacing = new Vector2(18f, 18f);
        grid.cellSize = cellSize;
        grid.childAlignment = TextAnchor.MiddleCenter;
        return rect;
    }

    private Button CreateChoiceButton(Transform parent, string label, int fontSize, Color color, Sprite sprite)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(DwellSelectable));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.12f);
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.55f);
        button.colors = colors;

        if (sprite != null)
        {
            Image portrait = CreatePanel(buttonObject.GetComponent<RectTransform>(), "Portrait", Color.white, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.92f));
            portrait.sprite = sprite;
            portrait.preserveAspect = true;
        }

        Text text = CreateText(buttonObject.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 16;
        text.resizeTextMaxSize = fontSize;
        SetAnchors(text.rectTransform, sprite != null ? new Vector2(0.06f, 0.05f) : new Vector2(0.07f, 0.08f), sprite != null ? new Vector2(0.94f, 0.32f) : new Vector2(0.93f, 0.92f));

        Image progress = CreatePanel(buttonObject.GetComponent<RectTransform>(), "Dwell Progress", new Color(1f, 1f, 1f, 0.35f), new Vector2(0f, 0f), new Vector2(1f, 0.08f));
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        progress.fillOrigin = 0;
        float dwellSeconds = config != null ? config.dwellSeconds : AppGameManager.Instance != null ? AppGameManager.Instance.DefaultDwellSeconds : 1.1f;
        buttonObject.GetComponent<DwellSelectable>().Configure(dwellSeconds, progress);
        return button;
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Story Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private Image CreatePanel(RectTransform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform rect = CreateBox(parent, name, anchorMin, anchorMax, color);
        return rect.GetComponent<Image>();
    }

    private RectTransform CreateBox(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject boxObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        boxObject.transform.SetParent(parent, false);
        RectTransform rect = boxObject.GetComponent<RectTransform>();
        SetAnchors(rect, anchorMin, anchorMax);
        boxObject.GetComponent<Image>().color = color;
        return rect;
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

    private void ClearDynamicChildren()
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child == background.transform || child == backgroundSprite.transform || child == titleText.transform || child == helperText.transform)
            {
                continue;
            }

            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = config != null ? config.backgroundColor : new Color(0.16f, 0.27f, 0.31f);
        camera.orthographic = true;
        cameraObject.tag = "MainCamera";

        if (textSpeaker == null)
        {
            textSpeaker = GetComponent<StoryTextSpeaker>();
        }

        if (textSpeaker == null)
        {
            textSpeaker = gameObject.AddComponent<StoryTextSpeaker>();
        }
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private string GetTraitName(StoryTraitData trait, StoryCharacterData character)
    {
        return trait != null ? trait.GetFor(character) : "";
    }

    private string Capitalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return char.ToUpper(value[0]) + value.Substring(1);
    }
}

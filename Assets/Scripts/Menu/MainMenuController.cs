using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Editable menu settings")]
    [SerializeField] private MainMenuConfig config;

    [Header("Runtime layout")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private Vector2 buttonAreaMin = new Vector2(0.08f, 0.22f);
    [SerializeField] private Vector2 buttonAreaMax = new Vector2(0.92f, 0.62f);
    [SerializeField] private float buttonSpacing = 36f;

    private Font uiFont;

    private void Start()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (config == null && AppGameManager.Instance != null)
        {
            config = AppGameManager.Instance.MainMenuConfig;
        }

        EnsureEventSystem();
        BuildMenu();
    }

    private void BuildMenu()
    {
        Camera.main?.gameObject.SetActive(false);
        CreateCamera();

        Canvas canvas = CreateCanvas("Main Menu Canvas");
        RectTransform root = canvas.GetComponent<RectTransform>();

        Color backgroundColor = config != null ? config.backgroundColor : new Color(0.13f, 0.22f, 0.29f);
        Image background = CreatePanel(root, "Background", backgroundColor, Vector2.zero, Vector2.one);
        background.transform.SetAsFirstSibling();

        Text title = CreateText(root, config != null ? config.title : "Letters", 64, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(title.rectTransform, new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.95f));

        Text subtitle = CreateText(root, config != null ? config.subtitle : "Choose a game", 32, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.87f, 0.94f, 0.96f));
        SetAnchors(subtitle.rectTransform, new Vector2(0.08f, 0.67f), new Vector2(0.92f, 0.76f));

        GameObject row = CreateLayout(root, "Game Buttons", buttonAreaMin, buttonAreaMax, buttonSpacing);
        MenuGameEntry[] games = config != null ? config.games : null;
        if (games == null || games.Length == 0)
        {
            return;
        }

        foreach (MenuGameEntry game in games)
        {
            if (game == null)
            {
                continue;
            }

            CreateMenuButton(row.transform, game);
        }
    }

    private void CreateMenuButton(Transform parent, MenuGameEntry game)
    {
        string label = game.title;
        if (!string.IsNullOrEmpty(game.subtitle))
        {
            label += "\n" + game.subtitle;
        }

        Button button = CreateButton(parent, label, 36, game.buttonColor, game.icon);
        button.onClick.AddListener(() =>
        {
            AppGameManager.Instance?.PlayButtonClick();
            SceneManager.LoadScene(game.sceneName);
        });
    }

    private void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = config != null ? config.backgroundColor : new Color(0.13f, 0.22f, 0.29f);
        camera.orthographic = true;
        cameraObject.tag = "MainCamera";
    }

    private Canvas CreateCanvas(string name)
    {
        GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private GameObject CreateLayout(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float spacing)
    {
        GameObject layoutObject = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        layoutObject.transform.SetParent(parent, false);
        SetAnchors(layoutObject.GetComponent<RectTransform>(), anchorMin, anchorMax);

        HorizontalLayoutGroup layout = layoutObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        return layoutObject;
    }

    private Button CreateButton(Transform parent, string label, int fontSize, Color color, Sprite icon)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(DwellSelectable));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.12f);
        button.colors = colors;

        if (icon != null)
        {
            Image iconImage = CreatePanel(buttonObject.GetComponent<RectTransform>(), "Icon", Color.white, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.9f));
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.type = Image.Type.Simple;
        }

        Text text = CreateText(buttonObject.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 20;
        text.resizeTextMaxSize = fontSize;
        SetAnchors(text.rectTransform, icon != null ? new Vector2(0.08f, 0.08f) : new Vector2(0.08f, 0.08f), icon != null ? new Vector2(0.92f, 0.38f) : new Vector2(0.92f, 0.92f));

        Image progress = CreatePanel(buttonObject.GetComponent<RectTransform>(), "Dwell Progress", new Color(1f, 1f, 1f, 0.35f), new Vector2(0f, 0f), new Vector2(1f, 0.08f));
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        progress.fillOrigin = 0;

        float dwellSeconds = config != null ? config.dwellSeconds : AppGameManager.Instance != null ? AppGameManager.Instance.DefaultDwellSeconds : 1.1f;
        buttonObject.GetComponent<DwellSelectable>().Configure(dwellSeconds, progress);
        return button;
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

    private void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}

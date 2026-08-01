using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TrainGameController : MonoBehaviour
{
    private const int MaxCoupledCars = 2;

    [Header("Editable settings")]
    [SerializeField] private TrainGameConfig config;

    private Canvas canvas;
    private RectTransform root;
    private RectTransform mapRect;
    private RectTransform worldRoot;
    private RectTransform effectsRoot;
    private Image environmentFilterImage;
    private RectTransform controlsRect;
    private Font uiFont;

    private AudioSource soundSource;
    private AudioSource musicSource;
    private AudioSource hornSource;
    private AudioSource switchSource;
    private AudioSource coupleSource;
    private AudioSource doorSource;
    private AudioSource cargoSource;
    private AudioSource successSource;
    private AudioSource movementSource;
    private StoryTextSpeaker textSpeaker;
    private TrainScrollerWorldController world;

    private Text taskTitleText;
    private Text taskPromptText;
    private Text statusText;
    private Text feedbackText;
    private Button forwardButton;
    private Button backButton;
    private Button hornButton;
    private Button switchButton;
    private Button coupleButton;
    private Button uncoupleButton;
    private Button doorButton;
    private Button cargoButton;
    private RectTransform hornLeverHandle;
    private RectTransform hornRopeRect;
    private RectTransform hornCrossbarRect;
    private bool hornRopeDragging;
    private float hornManualPull;
    private Coroutine hornRopeRoutine;

    private RectTransform locomotiveRect;
    private RectTransform trainContentRoot;
    private RectTransform stationHighlightRect;
    private Image stationHighlightImage;
    private Outline stationHighlightOutline;
    private readonly List<TrainCarRuntime> coupledCars = new List<TrainCarRuntime>();
    private readonly List<Image> couplerLines = new List<Image>();
    private readonly List<Vector2> trainTrail = new List<Vector2>();
    private readonly float[] sectionSwitchJoltTimers = new float[MaxCoupledCars + 1];
    private readonly float[] previousSectionXs = new float[MaxCoupledCars + 1];
    private readonly bool[] sectionPositionInitialized = new bool[MaxCoupledCars + 1];

    private bool gameStarted;
    private float trainX;
    private float trainY;
    private int currentLane;
    private int targetLane;
    private bool laneChangeActive;
    private float laneChangeStartX;
    private float laneChangeEndX;
    private int laneChangeTravelDirection = 1;
    private int laneChangeFromLane;
    private int laneChangeToLane;
    private float currentSpeed;
    private int targetDirection;
    private int throttleLevel;
    private int lastTravelDirection = 1;
    private bool useTapStopDeceleration;
    private float cameraX;
    private float smokeTimer;
    private float hornSmokeBoostTimer;
    private Color hornSmokeColor = new Color(0.42f, 0.43f, 0.44f, 0.88f);
    private TrainStationRuntime currentStation;
    private TrainStationRuntime announcedStation;
    private int currentTaskIndex;
    private bool taskAdvanceActive;

    private int lastHornSoundIndex = -1;
    private int lastSwitchSoundIndex = -1;
    private int lastCoupleSoundIndex = -1;
    private int lastDoorSoundIndex = -1;
    private int lastCargoSoundIndex = -1;
    private int lastSuccessSoundIndex = -1;
    private int currentMovementSoundLevel;
    private readonly Dictionary<string, Sprite> trainSpriteCache = new Dictionary<string, Sprite>();
    private float environmentFilterTimer;
    private Color environmentFilterCurrent = Color.clear;
    private Color environmentFilterTarget = Color.clear;

    private readonly string[] softTaskTitles =
    {
        "Свободная поездка",
        "Пассажиры",
        "Груз",
        "Стрелка",
        "Встречный путь"
    };

    private readonly string[] softTaskPrompts =
    {
        "Езжай вправо, остановись на станции и попробуй кнопки.",
        "Остановись у пассажирской станции и открой двери.",
        "Остановись у грузовой станции и загрузи дерево или камень.",
        "Найди стрелку и переключи путь, когда это безопасно.",
        "Следи за встречными поездами: опасный путь игра мягко заблокирует."
    };

    private float StopThreshold => Mathf.Max(0.1f, config != null ? config.nearStopSpeedThreshold : 5f);
    private bool IsStopped => Mathf.Abs(currentSpeed) <= StopThreshold && targetDirection == 0;
    private bool IsBraking => targetDirection == 0 && Mathf.Abs(currentSpeed) > StopThreshold;
    private float TrainSpeed => config != null ? Mathf.Max(40f, config.trainSpeed) : 190f;
    private float ReverseSpeed => config != null ? Mathf.Max(40f, config.reverseSpeed) : 145f;
    private float CarSpacing => Mathf.Max(config != null ? config.carSpacing : 350f, (config != null ? config.carSize.x : 280f) + 70f);

    private void Start()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (config == null && AppGameManager.Instance != null)
        {
            config = AppGameManager.Instance.TrainGameConfig;
        }

        if (config == null)
        {
            config = Resources.Load<TrainGameConfig>("Train/TrainGameConfig");
        }

        EnsureServices();
        canvas = CreateCanvas();
        root = canvas.GetComponent<RectTransform>();
        PlayMusic();

        if (config == null || config.showIntroCinematic)
        {
            TrainIntroCinematicController intro = gameObject.AddComponent<TrainIntroCinematicController>();
            intro.Play(root, config, uiFont, BeginGame);
        }
        else
        {
            BeginGame();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ReturnToMenu();
            return;
        }

        if (!gameStarted)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        UpdateTrainMotion(deltaTime);
        UpdateWorldGeneration();
        UpdateCamera(deltaTime);
        UpdateEnvironmentFilter(deltaTime);
        UpdateOncomingTrains(deltaTime);
        UpdateMovementSound();
        RenderTrain(deltaTime);
        UpdateLocomotiveSmoke(deltaTime);
        UpdateStationState();
        UpdateStationHighlight();
        UpdateSwitchIndicators();
        UpdateControls();
        UpdateStatus();
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
        hornSource = CreateEventAudioSource("Horn Audio");
        switchSource = CreateEventAudioSource("Switch Audio");
        coupleSource = CreateEventAudioSource("Couple Audio");
        doorSource = CreateEventAudioSource("Door Audio");
        cargoSource = CreateEventAudioSource("Cargo Audio");
        successSource = CreateEventAudioSource("Success Audio");
        movementSource = CreateEventAudioSource("Movement Audio");
        movementSource.loop = true;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        textSpeaker = gameObject.AddComponent<StoryTextSpeaker>();
    }

    private void BeginGame()
    {
        ClearRoot();
        world = gameObject.AddComponent<TrainScrollerWorldController>();
        world.Initialize(config);
        BuildGame();
        gameStarted = true;
        StartSoftTask(0);
    }

    private void BuildGame()
    {
        Image background = CreatePanel(root, "Background", config != null ? config.backgroundColor : new Color(0.10f, 0.19f, 0.22f), Vector2.zero, Vector2.one);
        background.raycastTarget = false;

        CreateHeader();
        CreateMap();
        CreateControls();

        trainX = -240f;
        currentLane = 0;
        targetLane = 0;
        trainY = world.GetLaneY(currentLane);
        cameraX = trainX - (config != null ? config.cameraFollowOffset : 180f);
        worldRoot.anchoredPosition = new Vector2(-cameraX, 0f);

        CreatePlayerTrain();
        world.PlayerLane = currentLane;
        foreach (TrainWorldChunkRuntime chunk in world.GenerateAhead(trainX))
        {
            CreateChunkVisuals(chunk);
        }

        ResetTrainTrail();
        RenderTrain(0f, true);
        UpdateControls();
        UpdateStatus();
    }

    private void CreateHeader()
    {
        Text title = CreateText(root, config != null ? config.title : "Поезда", 42, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(title.rectTransform, new Vector2(0.22f, 0.92f), new Vector2(0.78f, 0.99f));

        Button back = CreateButton(root, "Меню", 24, Color.black, ReturnToMenu);
        SetAnchors(back.GetComponent<RectTransform>(), new Vector2(0.88f, 0.925f), new Vector2(0.985f, 0.985f));

        feedbackText = CreateText(root, string.Empty, 30, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(feedbackText.rectTransform, new Vector2(0.24f, 0.85f), new Vector2(0.76f, 0.91f));
    }

    private void CreateMap()
    {
        Image mapPanel = CreatePanel(root, "Map", config != null ? config.mapColor : new Color(0.78f, 0.88f, 0.74f), new Vector2(0.02f, 0.08f), new Vector2(0.78f, 0.91f));
        mapPanel.raycastTarget = true;
        mapRect = mapPanel.rectTransform;

        GameObject worldObject = new GameObject("ScrollerWorld", typeof(RectTransform));
        worldObject.transform.SetParent(mapRect, false);
        worldRoot = worldObject.GetComponent<RectTransform>();
        worldRoot.anchorMin = worldRoot.anchorMax = new Vector2(0.5f, 0.5f);
        worldRoot.anchoredPosition = Vector2.zero;
        worldRoot.sizeDelta = Vector2.zero;

        GameObject effectsObject = new GameObject("WorldEffects", typeof(RectTransform));
        effectsObject.transform.SetParent(worldRoot, false);
        effectsRoot = effectsObject.GetComponent<RectTransform>();
        effectsRoot.anchorMin = effectsRoot.anchorMax = new Vector2(0.5f, 0.5f);
        effectsRoot.anchoredPosition = Vector2.zero;
        effectsRoot.sizeDelta = Vector2.zero;

        environmentFilterImage = CreatePanel(mapRect, "EnvironmentFilter", Color.clear, Vector2.zero, Vector2.one);
        environmentFilterImage.raycastTarget = false;
        environmentFilterImage.rectTransform.SetAsLastSibling();
        PickNextEnvironmentFilter(true);
    }

    private void CreateControls()
    {
        Image controls = CreatePanel(root, "Controls", new Color(0.06f, 0.11f, 0.13f, 0.88f), new Vector2(0.80f, 0.24f), new Vector2(0.98f, 0.91f));
        controlsRect = controls.rectTransform;

        taskTitleText = CreateText(controlsRect, string.Empty, 25, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(taskTitleText.rectTransform, new Vector2(0.05f, 0.87f), new Vector2(0.95f, 0.97f));
        taskPromptText = CreateText(controlsRect, string.Empty, 20, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.88f));
        taskPromptText.resizeTextForBestFit = true;
        taskPromptText.resizeTextMinSize = 14;
        taskPromptText.resizeTextMaxSize = 20;
        SetAnchors(taskPromptText.rectTransform, new Vector2(0.05f, 0.69f), new Vector2(0.95f, 0.87f));

        backButton = CreateButton(root, "◀", 76, ControlColor(), ToggleBack);
        SetAnchors(backButton.GetComponent<RectTransform>(), new Vector2(0.02f, 0.035f), new Vector2(0.15f, 0.205f));
        forwardButton = CreateButton(root, "▶", 76, ControlColor(), ToggleForward);
        SetAnchors(forwardButton.GetComponent<RectTransform>(), new Vector2(0.84f, 0.035f), new Vector2(0.97f, 0.205f));

        hornButton = CreateButton(root, string.Empty, 24, new Color(0.68f, 0.23f, 0.12f), TriggerHornPull);
        SetAnchors(hornButton.GetComponent<RectTransform>(), new Vector2(0.025f, 0.785f), new Vector2(0.15f, 0.90f));
        BuildHornRope(hornButton.GetComponent<RectTransform>());
        TrainHornRopeTarget hornTarget = hornButton.gameObject.AddComponent<TrainHornRopeTarget>();
        hornTarget.Configure(TriggerHornPull, SetHornManualPull, ReleaseHornManualPull);
        coupleButton = CreateButton(root, "Прицепить", 21, new Color(0.25f, 0.45f, 0.80f), CoupleAtStation);
        SetAnchors(coupleButton.GetComponent<RectTransform>(), new Vector2(0.27f, 0.035f), new Vector2(0.40f, 0.17f));
        uncoupleButton = CreateButton(root, "Отцепить", 19, new Color(0.34f, 0.38f, 0.58f), UncoupleAtStation);
        SetAnchors(uncoupleButton.GetComponent<RectTransform>(), new Vector2(0.41f, 0.035f), new Vector2(0.54f, 0.17f));

        doorButton = CreateButton(root, "Двери", 22, new Color(0.25f, 0.65f, 0.46f), ToggleDoors);
        SetAnchors(doorButton.GetComponent<RectTransform>(), new Vector2(0.55f, 0.035f), new Vector2(0.68f, 0.17f));
        cargoButton = CreateButton(root, "Груз", 22, new Color(0.75f, 0.45f, 0.12f), HandleCargo);
        SetAnchors(cargoButton.GetComponent<RectTransform>(), new Vector2(0.69f, 0.035f), new Vector2(0.82f, 0.17f));

        statusText = CreateText(controlsRect, string.Empty, 17, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(statusText.rectTransform, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.30f));
    }

    private void CreatePlayerTrain()
    {
        GameObject locomotive = new GameObject("Locomotive", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button), typeof(DwellSelectable));
        locomotive.transform.SetParent(worldRoot, false);
        locomotive.transform.SetAsLastSibling();
        locomotiveRect = locomotive.GetComponent<RectTransform>();
        locomotiveRect.anchorMin = locomotiveRect.anchorMax = new Vector2(0.5f, 0.5f);
        locomotiveRect.sizeDelta = config != null ? config.locomotiveSize : new Vector2(320f, 170f);
        Image locomotiveImage = locomotive.GetComponent<Image>();
        locomotiveImage.color = config != null ? config.locomotiveColor : new Color(0.88f, 0.15f, 0.12f);
        bool hasLocomotiveSprite = ApplySprite(locomotiveImage, "player_locomotive");
        locomotive.GetComponent<Button>().onClick.AddListener(StopOrResumeFromTap);
        AddDwell(locomotive, locomotiveRect);
        Outline outline = locomotive.GetComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(4f, -4f);
        if (!hasLocomotiveSprite)
        {
            AddLocomotiveDetails(locomotiveRect);
        }

        trainContentRoot = new GameObject("TrainContent", typeof(RectTransform)).GetComponent<RectTransform>();
        trainContentRoot.SetParent(worldRoot, false);
        trainContentRoot.anchorMin = trainContentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        trainContentRoot.sizeDelta = Vector2.zero;

        CreateStationHighlight();
    }

    private void CreateStationHighlight()
    {
        GameObject highlight = new GameObject("StationActionHighlight", typeof(RectTransform), typeof(Image), typeof(Outline));
        highlight.transform.SetParent(worldRoot, false);
        stationHighlightRect = highlight.GetComponent<RectTransform>();
        stationHighlightRect.anchorMin = stationHighlightRect.anchorMax = new Vector2(0.5f, 0.5f);
        stationHighlightRect.sizeDelta = Vector2.zero;
        stationHighlightImage = highlight.GetComponent<Image>();
        stationHighlightImage.raycastTarget = false;
        stationHighlightImage.color = new Color(0.12f, 0.95f, 0.34f, 0.035f);
        stationHighlightOutline = highlight.GetComponent<Outline>();
        stationHighlightOutline.effectDistance = new Vector2(3f, -3f);
        stationHighlightOutline.effectColor = new Color(0.12f, 1f, 0.32f, 0.72f);
        highlight.SetActive(false);
    }

    private void CreateChunkVisuals(TrainWorldChunkRuntime chunk)
    {
        GameObject chunkObject = new GameObject("Chunk " + chunk.index + " " + chunk.type, typeof(RectTransform));
        chunkObject.transform.SetParent(worldRoot, false);
        chunk.root = chunkObject.GetComponent<RectTransform>();
        chunk.root.anchorMin = chunk.root.anchorMax = new Vector2(0.5f, 0.5f);
        chunk.root.anchoredPosition = Vector2.zero;
        chunk.root.sizeDelta = Vector2.zero;

        CreateGroundPatch(chunk);
        for (int lane = 0; lane < world.LaneCount; lane++)
        {
            CreateRailPair(chunk.root, chunk.startX, chunk.endX, world.GetLaneY(lane));
        }

        if (chunk.hasSwitch)
        {
            CreateSwitchVisual(chunk);
        }

        for (int i = 0; i < chunk.stations.Count; i++)
        {
            CreateStationVisual(chunk, chunk.stations[i]);
        }

        for (int i = 0; i < chunk.freeCars.Count; i++)
        {
            CreateFreeCarVisual(chunk.freeCars[i]);
        }

        for (int i = 0; i < chunk.oncomingTrains.Count; i++)
        {
            CreateOncomingTrainVisual(chunk, chunk.oncomingTrains[i]);
        }

        CreateChunkDecorations(chunk);
    }

    private void CreateGroundPatch(TrainWorldChunkRuntime chunk)
    {
        Color baseColor = config != null ? config.mapColor : new Color(0.78f, 0.88f, 0.74f);
        Color patchColor = chunk.index % 2 == 0 ? baseColor : Color.Lerp(baseColor, new Color(0.65f, 0.82f, 0.62f), 0.18f);
        Image patch = CreatePanel(chunk.root, "Ground", patchColor, Vector2.zero, Vector2.zero);
        patch.raycastTarget = false;
        patch.rectTransform.anchorMin = patch.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        patch.rectTransform.anchoredPosition = new Vector2((chunk.startX + chunk.endX) * 0.5f, 0f);
        patch.rectTransform.sizeDelta = new Vector2(chunk.endX - chunk.startX + 12f, 900f);
        patch.rectTransform.SetAsFirstSibling();
    }

    private void CreateRailPair(RectTransform parent, float startX, float endX, float y)
    {
        Color sleeper = config != null ? config.sleeperColor : new Color(0.48f, 0.34f, 0.22f);
        Color rail = config != null ? config.railColor : new Color(0.22f, 0.22f, 0.24f);
        CreateRailSpriteTiles(parent, startX, endX, y);
        CreateLine(parent, "Sleepers", new Vector2(startX, y), new Vector2(endX, y), 58f, sleeper * 0.72f);
        CreateLine(parent, "Rail", new Vector2(startX, y + 16f), new Vector2(endX, y + 16f), 10f, rail);
        CreateLine(parent, "Rail", new Vector2(startX, y - 16f), new Vector2(endX, y - 16f), 10f, rail);

        int sleeperCount = Mathf.Max(4, Mathf.RoundToInt((endX - startX) / 100f));
        for (int i = 0; i <= sleeperCount; i++)
        {
            float x = Mathf.Lerp(startX, endX, i / (float)sleeperCount);
            Image cross = CreatePanel(parent, "Sleeper", sleeper, Vector2.zero, Vector2.zero);
            cross.raycastTarget = false;
            cross.rectTransform.anchorMin = cross.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            cross.rectTransform.anchoredPosition = new Vector2(x, y);
            cross.rectTransform.sizeDelta = new Vector2(14f, 74f);
        }
    }

    private void CreateSwitchVisual(TrainWorldChunkRuntime chunk)
    {
        Color rail = config != null ? config.railColor : new Color(0.22f, 0.22f, 0.24f);
        Color sleeper = config != null ? config.sleeperColor : new Color(0.48f, 0.34f, 0.22f);
        CreateSwitchTrack(chunk.root, "SwitchTrack01", chunk.switchX, 0, 1, rail, sleeper, 0.72f);
        CreateSwitchTrack(chunk.root, "SwitchTrack10", chunk.switchX, 1, 0, rail, sleeper, 0.42f);
        CreateSwitchTrack(chunk.root, "SwitchTrack12", chunk.switchX, 1, 2, rail, sleeper, 0.72f);
        CreateSwitchTrack(chunk.root, "SwitchTrack21", chunk.switchX, 2, 1, rail, sleeper, 0.42f);
        CreateSwitchTrack(chunk.root, "SwitchTrack02", chunk.switchX, 0, 2, rail, sleeper, 0.48f);
        CreateSwitchTrack(chunk.root, "SwitchTrack20", chunk.switchX, 2, 0, rail, sleeper, 0.32f);
        chunk.switchDirectionLine = CreateLine(chunk.root, "SwitchDirection", Vector2.zero, Vector2.right, 14f, new Color(0.05f, 0.92f, 0.18f, 0.95f));
        chunk.switchDirectionLine.raycastTarget = false;
        Image marker = CreatePanel(chunk.root, "SwitchButton", new Color(1f, 0.86f, 0.18f, 0.96f), Vector2.zero, Vector2.zero);
        marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        marker.rectTransform.anchoredPosition = new Vector2(chunk.switchX, world.GetLaneY(1) + 92f);
        marker.rectTransform.sizeDelta = new Vector2(132f, 78f);
        Button button = marker.gameObject.AddComponent<Button>();
        button.onClick.AddListener(ToggleSwitch);
        AddDwell(marker.gameObject, marker.rectTransform);
        Text label = CreateText(marker.rectTransform, chunk.targetLane == 1 ? "↗ Верх" : "↘ Низ", 25, FontStyle.Bold, TextAnchor.MiddleCenter, Color.black);
        SetAnchors(label.rectTransform, Vector2.zero, Vector2.one);
        chunk.switchLight = marker;
        chunk.switchLabel = label;
        UpdateSwitchVisual(chunk);
    }

    private void CreateSwitchTrack(RectTransform parent, string name, float switchX, int fromLane, int toLane, Color rail, Color sleeper, float alpha)
    {
        Vector2 start = GetSwitchPoint(switchX, fromLane, true);
        Vector2 end = GetSwitchPoint(switchX, toLane, false);
        Color sleeperColor = new Color(sleeper.r, sleeper.g, sleeper.b, Mathf.Clamp01(alpha * 0.64f));
        Color railColor = new Color(rail.r, rail.g, rail.b, Mathf.Clamp01(alpha));
        CreateLine(parent, name + "Sleepers", start, end, 44f, sleeperColor).raycastTarget = false;
        Vector2 normal = Vector2.Perpendicular((end - start).normalized) * 12f;
        CreateLine(parent, name + "RailA", start + normal, end + normal, 7f, railColor).raycastTarget = false;
        CreateLine(parent, name + "RailB", start - normal, end - normal, 7f, railColor).raycastTarget = false;
    }

    private Vector2 GetSwitchPoint(float switchX, int lane, bool leftSide)
    {
        return new Vector2(switchX + (leftSide ? -280f : 320f), world.GetLaneY(lane));
    }

    private void CreateStationVisual(TrainWorldChunkRuntime chunk, TrainStationRuntime station)
    {
        Color color = station.kind == TrainStationKind.CargoStation
            ? (config != null ? config.cargoStationColor : new Color(0.55f, 0.37f, 0.19f))
            : (config != null ? config.platformColor : new Color(0.64f, 0.58f, 0.50f));
        Image stopZone = CreatePanel(chunk.root, "StationStopZone", new Color(station.routeColor.r, station.routeColor.g, station.routeColor.b, 0.14f), Vector2.zero, Vector2.zero);
        stopZone.raycastTarget = false;
        stopZone.rectTransform.anchorMin = stopZone.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        stopZone.rectTransform.anchoredPosition = new Vector2(station.position.x, world.GetLaneY(station.lane));
        stopZone.rectTransform.sizeDelta = new Vector2(Mathf.Max(460f, station.stopRadius * 2f), 210f);

        Image panel = CreatePanel(chunk.root, "Station " + station.title, color, Vector2.zero, Vector2.zero);
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panel.rectTransform.anchoredPosition = station.position;
        panel.rectTransform.sizeDelta = station.kind == TrainStationKind.CargoStation ? new Vector2(300f, 130f) : new Vector2(340f, 122f);
        station.rect = panel.rectTransform;
        Image routeStripe = CreatePanel(panel.rectTransform, "RouteColor", station.routeColor, new Vector2(0f, 0.88f), Vector2.one);
        routeStripe.raycastTarget = false;
        station.label = new TextMeshLabel();
        station.label.title = CreateText(panel.rectTransform, station.title, 30, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(station.label.title.rectTransform, new Vector2(0.05f, 0.52f), new Vector2(0.95f, 0.95f));
        station.label.detail = CreateText(panel.rectTransform, string.Empty, 21, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.92f));
        SetAnchors(station.label.detail.rectTransform, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.48f));
        RefreshStationVisual(station);
    }

    private void CreateFreeCarVisual(TrainCarRuntime car)
    {
        if (car.rect != null)
        {
            return;
        }

        GameObject carObject = new GameObject(car.title, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(TrainLongPressTarget));
        carObject.transform.SetParent(worldRoot, false);
        car.rect = carObject.GetComponent<RectTransform>();
        car.rect.anchorMin = car.rect.anchorMax = new Vector2(0.5f, 0.5f);
        car.rect.sizeDelta = config != null ? config.carSize : new Vector2(280f, 150f);
        Image carImage = carObject.GetComponent<Image>();
        carImage.color = car.type == TrainCarType.Passenger
            ? (config != null ? config.passengerCarColor : Color.blue)
            : (config != null ? config.cargoCarColor : Color.yellow);
        bool hasSprite = !string.IsNullOrEmpty(car.spriteKey) && ApplySprite(carImage, car.spriteKey);
        Outline outline = carObject.GetComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(3f, -3f);
        if (!hasSprite)
        {
            AddCarDetails(car.rect, car.type);
        }
        TrainLongPressTarget longPress = carObject.GetComponent<TrainLongPressTarget>();
        longPress.Configure(config != null ? config.carLongPressSeconds : 1f, () => CoupleAtStation(), () => StartCoroutine(PulseRect(car.rect)));
        RenderCarRect(car);
    }

    private void CreateOncomingTrainVisual(TrainWorldChunkRuntime chunk, OncomingTrainRuntime train)
    {
        GameObject trainObject = new GameObject("Oncoming " + train.trainType, typeof(RectTransform));
        trainObject.transform.SetParent(worldRoot, false);
        train.root = trainObject.GetComponent<RectTransform>();
        train.root.anchorMin = train.root.anchorMax = new Vector2(0.5f, 0.5f);
        train.root.sizeDelta = Vector2.zero;
        train.hornSource = trainObject.AddComponent<AudioSource>();
        train.hornSource.playOnAwake = false;
        train.hornSource.loop = false;

        int carCount = Mathf.Max(1, train.carCount);
        Image locomotive = CreatePanel(train.root, "OncomingLocomotive", train.locomotiveColor, Vector2.zero, Vector2.zero);
        locomotive.rectTransform.anchorMin = locomotive.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        locomotive.rectTransform.anchoredPosition = Vector2.zero;
        locomotive.rectTransform.sizeDelta = new Vector2(220f, 112f);
        bool locomotiveSprite = ApplySprite(locomotive, train.trainType == TrainCarType.Passenger ? "oncoming_locomotive_passenger" : "oncoming_locomotive_cargo");
        if (!locomotiveSprite)
        {
            AddLocomotiveDetails(locomotive.rectTransform);
        }

        for (int i = 1; i <= carCount; i++)
        {
            Color color = train.carColors != null && train.carColors.Length >= i ? train.carColors[i - 1] : (train.trainType == TrainCarType.Passenger ? new Color(0.14f, 0.34f, 0.68f) : new Color(0.65f, 0.42f, 0.14f));
            Image body = CreatePanel(train.root, "OncomingCar", color, Vector2.zero, Vector2.zero);
            body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            body.rectTransform.anchoredPosition = new Vector2(i * 230f, 0f);
            body.rectTransform.sizeDelta = new Vector2(205f, 104f);
            string spriteKey = train.trainType == TrainCarType.Cargo ? (i % 2 == 0 ? "carriage_stone" : "carriage_wood") : PassengerSpriteKey(i);
            bool hasCarSprite = !string.IsNullOrEmpty(spriteKey) && ApplySprite(body, spriteKey);
            if (!hasCarSprite)
            {
                AddCarDetails(body.rectTransform, train.trainType);
            }
        }

        UpdateOncomingVisual(train);
        train.root.SetAsLastSibling();
    }

    private string PassengerSpriteKey(int variant)
    {
        return Mathf.Abs(variant) % 2 == 0 ? "passenger_carriage" : "passenger_carriage2";
    }

    private void CreateChunkDecorations(TrainWorldChunkRuntime chunk)
    {
        if (chunk.index % 5 == 2)
        {
            CreateRiver(chunk.root, chunk.startX + 70f, chunk.endX - 70f, chunk.index % 2 == 0 ? -405f : 438f, chunk.index % 3 == 0);
        }

        if (chunk.index % 6 == 3)
        {
            CreateMountains(chunk.root, chunk.startX + 120f, chunk.endX - 80f, 432f);
        }

        int count = 5 + chunk.index % 4;
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(chunk.startX + 120f, chunk.endX - 120f, (i + 0.35f) / count);
            float y = i % 2 == 0 ? -360f - (i % 3) * 22f : 385f - (i % 3) * 30f;
            int decorationType = (chunk.index + i) % 8;
            if (decorationType == 0)
            {
                CreateTree(chunk.root, new Vector2(x, y));
            }
            else if (decorationType == 1)
            {
                CreateSpringTree(chunk.root, new Vector2(x, y));
            }
            else if (decorationType == 2)
            {
                CreateConifer(chunk.root, new Vector2(x, y));
            }
            else if (decorationType == 3)
            {
                CreatePine(chunk.root, new Vector2(x, y));
            }
            else if (decorationType == 4)
            {
                CreateRock(chunk.root, new Vector2(x, y));
            }
            else if (decorationType == 5)
            {
                CreateAnimal(chunk.root, new Vector2(x, y), i % 2 == 0);
            }
            else
            {
                CreateRoadCar(chunk.root, new Vector2(x, y), i % 2 == 0 ? new Color(0.1f, 0.36f, 0.78f) : new Color(0.90f, 0.18f, 0.16f));
            }
        }
    }

    private void UpdateTrainMotion(float deltaTime)
    {
        if (targetDirection > 0 && IsOncomingDanger(targetLane, 720f))
        {
            targetDirection = 0;
            throttleLevel = 0;
            laneChangeActive = false;
            targetLane = currentLane;
            useTapStopDeceleration = true;
            StartCoroutine(ShowFeedback("Встречный поезд. Подождём.", WarningColor(), 1f));
        }

        float targetSpeed = targetDirection > 0 ? TrainSpeed * GetThrottleFraction() : targetDirection < 0 ? -ReverseSpeed * GetThrottleFraction() : 0f;
        float brakeRate = useTapStopDeceleration
            ? (config != null ? config.stopTapDeceleration : 360f)
            : (config != null ? config.brakeDeceleration : 260f);
        float rate = targetDirection == 0 || Mathf.Sign(currentSpeed) != Mathf.Sign(targetSpeed) && Mathf.Abs(currentSpeed) > StopThreshold
            ? brakeRate
            : (config != null ? config.acceleration : 140f);
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * deltaTime);

        if (Mathf.Abs(currentSpeed) <= StopThreshold && targetDirection == 0)
        {
            currentSpeed = 0f;
            useTapStopDeceleration = false;
        }

        trainX += currentSpeed * deltaTime;
        trainX = Mathf.Max(trainX, -420f);

        TryEnterSwitch();

        if (laneChangeActive)
        {
            float progress = Mathf.InverseLerp(laneChangeStartX, laneChangeEndX, trainX);
            progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            trainY = Mathf.Lerp(world.GetLaneY(laneChangeFromLane), world.GetLaneY(laneChangeToLane), progress);
            float lastSectionX = coupledCars.Count > 0 ? trainX - CarSpacing * coupledCars.Count : trainX;
            bool laneChangeComplete = laneChangeTravelDirection >= 0 ? lastSectionX >= laneChangeEndX : trainX <= laneChangeEndX;
            if (laneChangeComplete)
            {
                laneChangeActive = false;
                currentLane = laneChangeToLane;
                targetLane = laneChangeToLane;
                trainY = world.GetLaneY(currentLane);
            }
        }
        else
        {
            trainY = world.GetLaneY(currentLane);
        }

        Vector2 currentPosition = new Vector2(trainX, trainY);
        if (trainTrail.Count == 0 || Vector2.Distance(trainTrail[trainTrail.Count - 1], currentPosition) > 4f)
        {
            trainTrail.Add(currentPosition);
            if (trainTrail.Count > 1200)
            {
                trainTrail.RemoveAt(0);
            }
        }

        UpdateSectionJoltTimers(deltaTime);
    }

    private void UpdateWorldGeneration()
    {
        world.PlayerLane = laneChangeActive ? laneChangeToLane : currentLane;
        List<TrainWorldChunkRuntime> newChunks = world.GenerateAhead(trainX);
        for (int i = 0; i < newChunks.Count; i++)
        {
            CreateChunkVisuals(newChunks[i]);
        }

        List<TrainWorldChunkRuntime> removedChunks = world.RemoveBehind(trainX);
        for (int i = 0; i < removedChunks.Count; i++)
        {
            for (int t = 0; t < removedChunks[i].oncomingTrains.Count; t++)
            {
                if (removedChunks[i].oncomingTrains[t].root != null)
                {
                    Destroy(removedChunks[i].oncomingTrains[t].root.gameObject);
                }
            }

            if (removedChunks[i].root != null)
            {
                Destroy(removedChunks[i].root.gameObject);
            }
        }

        if (environmentFilterImage != null)
        {
            environmentFilterImage.rectTransform.SetAsLastSibling();
        }
    }

    private void UpdateCamera(float deltaTime)
    {
        if (worldRoot == null)
        {
            enabled = false;
            return;
        }

        float targetCameraX = trainX - (config != null ? config.cameraFollowOffset : 180f);
        cameraX = Mathf.Lerp(cameraX, targetCameraX, 1f - Mathf.Exp(-4.5f * deltaTime));
        worldRoot.anchoredPosition = new Vector2(-cameraX, 0f);
    }

    private void UpdateEnvironmentFilter(float deltaTime)
    {
        if (environmentFilterImage == null)
        {
            return;
        }

        environmentFilterTimer -= deltaTime;
        if (environmentFilterTimer <= 0f)
        {
            PickNextEnvironmentFilter(false);
        }

        environmentFilterCurrent = Color.Lerp(environmentFilterCurrent, environmentFilterTarget, 1f - Mathf.Exp(-0.65f * deltaTime));
        environmentFilterImage.color = environmentFilterCurrent;
    }

    private void PickNextEnvironmentFilter(bool initial)
    {
        environmentFilterTimer = UnityEngine.Random.Range(18f, 36f);
        int roll = initial ? 0 : UnityEngine.Random.Range(0, 5);
        switch (roll)
        {
            case 1:
                environmentFilterTarget = new Color(1f, 0.55f, 0.66f, 0.10f);
                break;
            case 2:
                environmentFilterTarget = new Color(1f, 0.56f, 0.24f, 0.13f);
                break;
            case 3:
                environmentFilterTarget = new Color(0.86f, 0.92f, 0.95f, 0.18f);
                environmentFilterTimer *= 0.65f;
                break;
            default:
                environmentFilterTarget = Color.clear;
                break;
        }

        if (initial)
        {
            environmentFilterCurrent = environmentFilterTarget;
            if (environmentFilterImage != null)
            {
                environmentFilterImage.color = environmentFilterCurrent;
            }
        }
    }

    private void UpdateOncomingTrains(float deltaTime)
    {
        for (int i = 0; i < world.OncomingTrains.Count; i++)
        {
            OncomingTrainRuntime train = world.OncomingTrains[i];
            if (!train.active)
            {
                continue;
            }

            if (train.lane == currentLane && train.x > trainX + 1100f)
            {
                train.lane = world.PickLaneAwayFrom(currentLane, train.id != null ? train.id.GetHashCode() : i);
            }

            train.x -= train.speed * deltaTime;
            float oncomingTailX = train.x + GetOncomingTrainLength(train);
            float leftDespawnX = cameraX - 1220f;
            if (oncomingTailX < leftDespawnX)
            {
                train.active = false;
                if (train.root != null)
                {
                    train.root.gameObject.SetActive(false);
                }
                continue;
            }

            MaybePlayOncomingHorn(train);
            UpdateOncomingSmoke(train, deltaTime);
            UpdateOncomingVisual(train);
            if (train.root != null)
            {
                train.root.SetAsLastSibling();
            }
        }
    }

    private float GetOncomingTrainLength(OncomingTrainRuntime train)
    {
        return 220f + Mathf.Max(1, train.carCount) * 230f + 170f;
    }

    private void MaybePlayOncomingHorn(OncomingTrainRuntime train)
    {
        if (train.hornStarted || train.hornSource == null)
        {
            return;
        }

        float distance = train.x - trainX;
        if (distance > 1150f || distance < -240f)
        {
            return;
        }

        train.hornStarted = true;
        StartCoroutine(PlayOncomingHornRoutine(train));
    }

    private IEnumerator PlayOncomingHornRoutine(OncomingTrainRuntime train)
    {
        int clipIndex = -1;
        AudioClip clip = PickRandomClip(config != null ? config.hornSounds : null, config != null ? config.hornSound : null, ref clipIndex);
        if (clip == null || train.hornSource == null)
        {
            yield break;
        }

        AudioSource source = train.hornSource;
        source.clip = clip;
        source.volume = 0f;
        source.Play();
        float baseVolume = (AppGameManager.Instance != null ? AppGameManager.Instance.EffectsVolume : 0.8f) * 0.9f;
        float duration = Mathf.Max(0.8f, clip.length);
        float elapsed = 0f;
        while (elapsed < duration && source != null && source.isPlaying)
        {
            elapsed += Time.deltaTime;
            float fadeIn = Mathf.Clamp01(elapsed / 0.42f);
            float fadeOut = Mathf.Clamp01((duration - elapsed) / 0.68f);
            float distance = Mathf.Abs(train.x - trainX);
            float distanceFade = Mathf.Clamp01(1f - distance / 1250f);
            source.volume = baseVolume * Mathf.Min(fadeIn, fadeOut) * Mathf.Lerp(0.35f, 1f, distanceFade);
            yield return null;
        }

        if (source != null)
        {
            source.Stop();
        }
    }

    private void UpdateOncomingSmoke(OncomingTrainRuntime train, float deltaTime)
    {
        train.smokeTimer -= deltaTime;
        if (train.smokeTimer > 0f)
        {
            return;
        }

        train.smokeTimer = Mathf.Lerp(0.32f, 0.12f, Mathf.InverseLerp(160f, 340f, train.speed));
        StartCoroutine(SmokePuffRoutine(new Vector2(train.x - 88f, world.GetLaneY(train.lane) + 76f)));
    }

    private void RenderTrain(float deltaTime, bool snap = false)
    {
        if (locomotiveRect != null)
        {
            UpdateSectionSwitchJoltState(0, trainX);
            locomotiveRect.anchoredPosition = new Vector2(trainX, trainY) + GetSwitchJoltOffset(trainX, 0);
            locomotiveRect.localRotation = GetSectionRotation(trainX, 0);
            locomotiveRect.SetAsLastSibling();
        }

        float spring = Mathf.Max(config != null ? config.carSpring : 18f, 16f);
        float t = snap ? 1f : 1f - Mathf.Exp(-spring * Mathf.Max(0.001f, deltaTime));
        for (int i = 0; i < coupledCars.Count; i++)
        {
            Vector2 target = GetCarTargetPosition(i);
            coupledCars[i].position = Vector2.Lerp(coupledCars[i].position, target, t);
            if (!snap)
            {
                float maxLag = Mathf.Max(26f, CarSpacing * 0.12f);
                float lag = Vector2.Distance(coupledCars[i].position, target);
                if (lag > maxLag)
                {
                    coupledCars[i].position = Vector2.MoveTowards(coupledCars[i].position, target, lag - maxLag);
                }
            }
            UpdateSectionSwitchJoltState(i + 1, coupledCars[i].position.x);
            RenderCarRect(coupledCars[i], i + 1);
            if (coupledCars[i].rect != null)
            {
                coupledCars[i].rect.SetAsLastSibling();
            }
        }

        for (int i = 0; i < world.FreeCars.Count; i++)
        {
            RenderCarRect(world.FreeCars[i]);
            if (world.FreeCars[i].rect != null)
            {
                world.FreeCars[i].rect.SetAsLastSibling();
            }
        }

        UpdateCouplerLines();
        if (effectsRoot != null)
        {
            effectsRoot.SetAsLastSibling();
        }
    }

    private void RenderCarRect(TrainCarRuntime car, int joltIndex = -1)
    {
        if (car == null || car.rect == null)
        {
            return;
        }

        car.rect.anchoredPosition = car.position + (joltIndex >= 0 ? GetSwitchJoltOffset(car.position.x, joltIndex) : Vector2.zero);
        car.rect.localRotation = joltIndex >= 0 ? GetSectionRotation(car.position.x, joltIndex) : Quaternion.identity;
        RefreshCarContents(car);
    }

    private Vector2 GetSwitchJoltOffset(float sectionX, int index)
    {
        float intensity = GetSwitchJoltIntensity(index);
        if (intensity <= 0f)
        {
            return Vector2.zero;
        }

        float wave = Mathf.Sin(Time.time * 64f + index * 1.35f);
        return new Vector2(0f, wave * 4.2f * intensity);
    }

    private Quaternion GetSectionRotation(float sectionX, int index)
    {
        return Quaternion.Euler(0f, 0f, GetPathAngleAtX(sectionX) + GetSwitchJoltAngle(index));
    }

    private float GetSwitchJoltAngle(int index)
    {
        float intensity = GetSwitchJoltIntensity(index);
        if (intensity <= 0f)
        {
            return 0f;
        }

        return Mathf.Sin(Time.time * 52f + index) * 0.85f * intensity;
    }

    private float GetPathAngleAtX(float sectionX)
    {
        if (!laneChangeActive)
        {
            return 0f;
        }

        float min = Mathf.Min(laneChangeStartX, laneChangeEndX) - 18f;
        float max = Mathf.Max(laneChangeStartX, laneChangeEndX) + 18f;
        if (sectionX < min || sectionX > max)
        {
            return 0f;
        }

        Vector2 before = GetConsistPositionAtX(sectionX - 16f);
        Vector2 after = GetConsistPositionAtX(sectionX + 16f);
        Vector2 delta = after - before;
        return delta.sqrMagnitude <= 0.001f ? 0f : Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
    }

    private Vector2 GetCarTargetPosition(int carIndex)
    {
        float spacing = CarSpacing * (carIndex + 1);
        return GetConsistPositionAtX(trainX - spacing);
    }

    private Vector2 GetConsistPositionAtX(float sectionX)
    {
        if (!laneChangeActive)
        {
            return new Vector2(sectionX, world != null ? world.GetLaneY(currentLane) : trainY);
        }

        float progress = Mathf.InverseLerp(laneChangeStartX, laneChangeEndX, sectionX);
        float fromY = world != null ? world.GetLaneY(laneChangeFromLane) : trainY;
        float toY = world != null ? world.GetLaneY(laneChangeToLane) : trainY;
        return new Vector2(sectionX, Mathf.SmoothStep(fromY, toY, progress));
    }

    private float GetSwitchJoltIntensity(int sectionIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= sectionSwitchJoltTimers.Length)
        {
            return 0f;
        }

        return Mathf.Clamp01(sectionSwitchJoltTimers[sectionIndex] / 0.30f);
    }

    private void UpdateSectionJoltTimers(float deltaTime)
    {
        for (int i = 0; i < sectionSwitchJoltTimers.Length; i++)
        {
            if (sectionSwitchJoltTimers[i] > 0f)
            {
                sectionSwitchJoltTimers[i] = Mathf.Max(0f, sectionSwitchJoltTimers[i] - deltaTime);
            }
        }
    }

    private void UpdateSectionSwitchJoltState(int sectionIndex, float sectionX)
    {
        if (sectionIndex < 0 || sectionIndex >= sectionSwitchJoltTimers.Length || world == null)
        {
            return;
        }

        if (!sectionPositionInitialized[sectionIndex])
        {
            previousSectionXs[sectionIndex] = sectionX;
            sectionPositionInitialized[sectionIndex] = true;
            return;
        }

        float previousX = previousSectionXs[sectionIndex];
        previousSectionXs[sectionIndex] = sectionX;
        if (Mathf.Abs(sectionX - previousX) < 0.25f)
        {
            return;
        }

        for (int i = 0; i < world.Chunks.Count; i++)
        {
            TrainWorldChunkRuntime chunk = world.Chunks[i];
            if (!chunk.hasSwitch)
            {
                continue;
            }

            float min = Mathf.Min(previousX, sectionX) - 12f;
            float max = Mathf.Max(previousX, sectionX) + 12f;
            if (chunk.switchX >= min && chunk.switchX <= max)
            {
                sectionSwitchJoltTimers[sectionIndex] = 0.30f;
                return;
            }
        }
    }

    private void UpdateCouplerLines()
    {
        for (int i = couplerLines.Count - 1; i >= 0; i--)
        {
            if (couplerLines[i] == null)
            {
                couplerLines.RemoveAt(i);
            }
        }

        if (worldRoot == null)
        {
            return;
        }

        int needed = coupledCars.Count;
        while (couplerLines.Count < needed)
        {
            Image line = CreateLine(worldRoot, "RubberCoupler", Vector2.zero, Vector2.right, 7f, new Color(0.05f, 0.05f, 0.06f, 0.82f));
            line.rectTransform.SetAsLastSibling();
            couplerLines.Add(line);
        }

        for (int i = 0; i < couplerLines.Count; i++)
        {
            if (couplerLines[i] == null)
            {
                continue;
            }

            bool active = i < needed;
            couplerLines[i].gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            Vector2 from = i == 0 ? new Vector2(trainX, trainY) : coupledCars[i - 1].position;
            Vector2 to = coupledCars[i].position;
            UpdateLine(couplerLines[i].rectTransform, from, to, 7f);
        }
    }

    private void UpdateLocomotiveSmoke(float deltaTime)
    {
        if (hornSmokeBoostTimer > 0f)
        {
            hornSmokeBoostTimer = Mathf.Max(0f, hornSmokeBoostTimer - deltaTime);
        }

        smokeTimer -= deltaTime;
        if (smokeTimer > 0f)
        {
            return;
        }

        bool hornBoost = hornSmokeBoostTimer > 0f;
        smokeTimer = hornBoost
            ? Mathf.Lerp(0.06f, 0.12f, Mathf.Clamp01(hornSmokeBoostTimer / 2.4f))
            : Mathf.Abs(currentSpeed) < 8f ? 0.42f : Mathf.Lerp(0.22f, 0.07f, Mathf.InverseLerp(0f, TrainSpeed * 1.5f, Mathf.Abs(currentSpeed)));
        Vector2 start = new Vector2(trainX + 150f, trainY + 88f);
        Color smokeColor = hornBoost ? hornSmokeColor : new Color(0.86f, 0.88f, 0.90f, 0.82f);
        StartCoroutine(SmokePuffRoutine(start, hornBoost ? 1.35f : 1f, smokeColor));
    }

    private IEnumerator SmokePuffRoutine(Vector2 start, float scaleMultiplier = 1f, Color? colorOverride = null)
    {
        if (effectsRoot != null)
        {
            effectsRoot.SetAsLastSibling();
        }

        Color startColor = colorOverride ?? new Color(0.86f, 0.88f, 0.90f, 0.82f);
        Image puff = CreatePanel(effectsRoot != null ? effectsRoot : worldRoot, "SmokePuff", startColor, Vector2.zero, Vector2.zero);
        puff.raycastTarget = false;
        RectTransform rect = puff.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = start;
        rect.sizeDelta = new Vector2(68f, 68f) * scaleMultiplier;
        rect.SetAsLastSibling();

        Vector2 drift = new Vector2(UnityEngine.Random.Range(-34f, 78f), UnityEngine.Random.Range(102f, 172f));
        float elapsed = 0f;
        const float duration = 1.45f;
        while (elapsed < duration && rect != null)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            rect.anchoredPosition = Vector2.Lerp(start, start + drift, Mathf.SmoothStep(0f, 1f, k));
            float size = Mathf.Lerp(68f, 190f, k) * scaleMultiplier;
            rect.sizeDelta = new Vector2(size, size);
            Color color = puff.color;
            color.a = Mathf.Lerp(startColor.a, 0f, k);
            puff.color = color;
            yield return null;
        }

        if (puff != null)
        {
            Destroy(puff.gameObject);
        }
    }

    private void UpdateStationState()
    {
        TrainStationRuntime station = FindStationForTrain();
        currentStation = station;
        if (station == null || !IsStopped)
        {
            return;
        }

        if (announcedStation != station)
        {
            announcedStation = station;
            string text = "Поезд прибыл на станцию: " + station.title;
            Speak(text);
            StartCoroutine(ShowFeedback(text, Color.white, 1.2f));
            MaybeCompleteSoftTask(0);
        }
    }

    private TrainStationRuntime FindStationForTrain()
    {
        float radius = config != null ? config.stationRadius : 360f;
        TrainStationRuntime best = world.FindStationNear(trainX, currentLane, radius);
        float bestDistance = best != null ? Mathf.Abs(best.position.x - trainX) : radius;
        for (int i = 0; i < coupledCars.Count; i++)
        {
            int lane = world.GetNearestLaneIndex(coupledCars[i].position.y);
            TrainStationRuntime station = world.FindStationNear(coupledCars[i].position.x, lane, radius);
            if (station == null)
            {
                continue;
            }

            float distance = Mathf.Abs(station.position.x - coupledCars[i].position.x);
            if (best == null || distance < bestDistance)
            {
                best = station;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void UpdateStationHighlight()
    {
        if (stationHighlightRect == null)
        {
            return;
        }

        if (currentStation == null)
        {
            stationHighlightRect.gameObject.SetActive(false);
            return;
        }

        float minX = trainX - locomotiveRect.sizeDelta.x * 0.5f;
        float maxX = trainX + locomotiveRect.sizeDelta.x * 0.5f;
        float minY = trainY - locomotiveRect.sizeDelta.y * 0.5f;
        float maxY = trainY + locomotiveRect.sizeDelta.y * 0.5f;
        for (int i = 0; i < coupledCars.Count; i++)
        {
            Vector2 size = coupledCars[i].rect != null ? coupledCars[i].rect.sizeDelta : (config != null ? config.carSize : new Vector2(280f, 150f));
            Vector2 position = coupledCars[i].position;
            minX = Mathf.Min(minX, position.x - size.x * 0.5f);
            maxX = Mathf.Max(maxX, position.x + size.x * 0.5f);
            minY = Mathf.Min(minY, position.y - size.y * 0.5f);
            maxY = Mathf.Max(maxY, position.y + size.y * 0.5f);
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6.5f);
        stationHighlightRect.gameObject.SetActive(true);
        stationHighlightRect.anchoredPosition = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        stationHighlightRect.sizeDelta = new Vector2(maxX - minX + 34f, maxY - minY + 30f);
        stationHighlightRect.SetAsLastSibling();
        if (stationHighlightImage != null)
        {
            stationHighlightImage.color = new Color(0.12f, 0.95f, 0.34f, Mathf.Lerp(0.02f, 0.07f, pulse));
        }

        if (stationHighlightOutline != null)
        {
            stationHighlightOutline.effectColor = new Color(0.12f, 1f, 0.32f, Mathf.Lerp(0.35f, 0.88f, pulse));
        }
    }

    private void ToggleForward()
    {
        PlayClick();
        if (throttleLevel < 0 || targetDirection < 0 || currentSpeed < -StopThreshold)
        {
            SmoothStop(false);
            return;
        }

        throttleLevel = Mathf.Clamp(throttleLevel + 1, 1, 3);
        targetDirection = 1;
        lastTravelDirection = 1;
    }

    private void ToggleBack()
    {
        PlayClick();
        if (throttleLevel > 0 || targetDirection > 0 || currentSpeed > StopThreshold)
        {
            SmoothStop(false);
            return;
        }

        throttleLevel = Mathf.Clamp(throttleLevel - 1, -3, -1);
        targetDirection = -1;
        lastTravelDirection = -1;
    }

    private void StopOrResumeFromTap()
    {
        PlayClick();
        if (IsStopped)
        {
            throttleLevel = lastTravelDirection < 0 ? -1 : 1;
            targetDirection = lastTravelDirection == 0 ? 1 : lastTravelDirection;
            StartCoroutine(ShowFeedback("Поехали", Color.white, 0.8f));
            return;
        }

        SmoothStop(true);
        StartCoroutine(ShowFeedback("Тормозим", Color.white, 0.8f));
    }

    private void SmoothStop(bool fromTap)
    {
        throttleLevel = 0;
        targetDirection = 0;
        useTapStopDeceleration = fromTap;
    }

    private void Honk()
    {
        hornSmokeBoostTimer = 2.4f;
        smokeTimer = 0f;
        float gray = UnityEngine.Random.Range(0.18f, 0.52f);
        hornSmokeColor = new Color(gray, gray + UnityEngine.Random.Range(0f, 0.035f), gray + UnityEngine.Random.Range(0.01f, 0.055f), UnityEngine.Random.Range(0.84f, 0.96f));
        PlayTrainEventSound(hornSource, config != null ? config.hornSounds : null, config != null ? config.hornSound : null, ref lastHornSoundIndex);
        StartCoroutine(ShowFeedback("Ту-ту!", Color.white, 0.8f));
    }

    private void TriggerHornPull()
    {
        if (hornRopeRoutine != null)
        {
            StopCoroutine(hornRopeRoutine);
        }

        hornRopeRoutine = StartCoroutine(HornRopeRoutine());
    }

    private IEnumerator HornRopeRoutine()
    {
        hornRopeDragging = false;
        hornManualPull = 0f;
        Honk();

        float elapsed = 0f;
        while (elapsed < 0.18f)
        {
            elapsed += Time.deltaTime;
            SetHornPullVisual(Mathf.SmoothStep(0f, 1f, elapsed / 0.18f), true);
            yield return null;
        }

        float hold = 0f;
        while (hold < 0.55f || hornSource != null && hornSource.isPlaying)
        {
            hold += Time.deltaTime;
            SetHornPullVisual(1f, true);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < 0.28f)
        {
            elapsed += Time.deltaTime;
            SetHornPullVisual(1f - Mathf.SmoothStep(0f, 1f, elapsed / 0.28f), true);
            yield return null;
        }

        SetHornPullVisual(0f, false);
        hornRopeRoutine = null;
    }

    private void SetHornManualPull(float normalizedPull)
    {
        hornRopeDragging = true;
        hornManualPull = Mathf.Clamp01(normalizedPull);
        SetHornPullVisual(hornManualPull, true);
        if (hornManualPull >= 0.72f)
        {
            TriggerHornPull();
        }
    }

    private void ReleaseHornManualPull()
    {
        hornRopeDragging = false;
        if (hornManualPull >= 0.45f)
        {
            TriggerHornPull();
            return;
        }

        hornManualPull = 0f;
        SetHornPullVisual(0f, false);
    }

    private void ToggleSwitch()
    {
        PlayClick();
        TrainWorldChunkRuntime switchChunk = world.FindSwitchNear(trainX, 460f);
        if (switchChunk == null)
        {
            StartCoroutine(ShowFeedback("Нужна развилка рядом", Color.yellow, 1f));
            MaybeCompleteSoftTask(3);
            return;
        }

        int desiredLane = (switchChunk.targetLane + 1) % world.LaneCount;

        if (IsOncomingDanger(desiredLane, 900f))
        {
            SmoothStop(true);
            PlayTrainEventSound(switchSource, config != null ? config.switchSounds : null, config != null ? config.switchSound : null, ref lastSwitchSoundIndex);
            StartCoroutine(ShowFeedback("Путь занят встречным поездом", WarningColor(), 1.2f));
            return;
        }

        switchChunk.targetLane = desiredLane;
        switchChunk.switchConsumed = false;
        UpdateSwitchVisual(switchChunk);
        PlayTrainEventSound(switchSource, config != null ? config.switchSounds : null, config != null ? config.switchSound : null, ref lastSwitchSoundIndex);
        StartCoroutine(ShowFeedback("Стрелка: " + LaneName(desiredLane) + " путь", Color.yellow, 1f));
        MaybeCompleteSoftTask(3);
    }

    private void TryEnterSwitch()
    {
        if (world == null || laneChangeActive || targetDirection == 0 || Mathf.Abs(currentSpeed) <= StopThreshold)
        {
            return;
        }

        for (int i = 0; i < world.Chunks.Count; i++)
        {
            TrainWorldChunkRuntime chunk = world.Chunks[i];
            if (!chunk.hasSwitch)
            {
                continue;
            }

            if (chunk.switchConsumed && Mathf.Abs(trainX - chunk.switchX) > 680f)
            {
                chunk.switchConsumed = false;
            }

            if (chunk.switchConsumed)
            {
                continue;
            }

            if (trainX < chunk.switchX - 220f || trainX > chunk.switchX + 260f)
            {
                continue;
            }

            if (chunk.targetLane != currentLane && IsOncomingDanger(chunk.targetLane, 900f))
            {
                SmoothStop(true);
                StartCoroutine(ShowFeedback("Путь занят. Остановимся.", WarningColor(), 1.1f));
                return;
            }

            chunk.switchConsumed = true;
            if (chunk.targetLane != currentLane)
            {
                BeginLaneChange(chunk, chunk.targetLane);
            }

            return;
        }
    }

    private void UpdateSwitchVisual(TrainWorldChunkRuntime switchChunk)
    {
        if (switchChunk == null)
        {
            return;
        }

        int desiredLane = switchChunk.targetLane;
        if (switchChunk.switchLight != null)
        {
            switchChunk.switchLight.color = desiredLane == currentLane ? new Color(0.94f, 0.82f, 0.18f, 0.98f) : new Color(0.25f, 0.85f, 0.35f, 0.94f);
            switchChunk.switchLight.rectTransform.anchoredPosition = new Vector2(switchChunk.switchX, world.GetLaneY(1) + 92f);
        }

        if (switchChunk.switchLabel != null)
        {
            switchChunk.switchLabel.text = desiredLane == 1 ? "↗ Верх" : "↘ Низ";
        }

        RefreshSwitchDirectionLine(switchChunk, desiredLane);
    }

    private void UpdateSwitchIndicators()
    {
        if (world == null)
        {
            return;
        }

        for (int i = 0; i < world.Chunks.Count; i++)
        {
            if (world.Chunks[i].hasSwitch && world.Chunks[i].switchDirectionLine != null)
            {
                UpdateSwitchVisual(world.Chunks[i]);
            }
        }
    }

    private void RefreshSwitchDirectionLine(TrainWorldChunkRuntime switchChunk, int desiredLane)
    {
        if (switchChunk.switchLabel != null)
        {
            switchChunk.switchLabel.text = SwitchLabelText(desiredLane);
        }

        if (switchChunk.switchDirectionLine == null)
        {
            return;
        }

        int sourceLane = Mathf.Clamp(currentLane, 0, world.LaneCount - 1);
        Vector2 start = GetSwitchPoint(switchChunk.switchX, sourceLane, true);
        Vector2 end = GetSwitchPoint(switchChunk.switchX, desiredLane, false);
        if (sourceLane == desiredLane)
        {
            start = new Vector2(switchChunk.switchX - 300f, world.GetLaneY(desiredLane));
            end = new Vector2(switchChunk.switchX + 340f, world.GetLaneY(desiredLane));
        }

        UpdateLine(switchChunk.switchDirectionLine.rectTransform, start, end, 14f);
    }

    private string SwitchLabelText(int lane)
    {
        string arrow = lane > currentLane ? "↗" : lane < currentLane ? "↘" : "→";
        return arrow + "\n" + LaneName(lane);
    }

    private void BeginLaneChange(TrainWorldChunkRuntime switchChunk, int desiredLane)
    {
        laneChangeActive = true;
        laneChangeFromLane = currentLane;
        laneChangeToLane = desiredLane;
        targetLane = desiredLane;
        laneChangeTravelDirection = Mathf.Abs(currentSpeed) > StopThreshold ? (currentSpeed >= 0f ? 1 : -1) : (targetDirection >= 0 ? 1 : -1);
        laneChangeStartX = laneChangeTravelDirection >= 0 ? Mathf.Max(trainX, switchChunk.switchX - 220f) : Mathf.Min(trainX, switchChunk.switchX + 260f);
        laneChangeEndX = laneChangeStartX + laneChangeTravelDirection * 520f;
        if (Mathf.Abs(currentSpeed) < 30f)
        {
            throttleLevel = laneChangeTravelDirection >= 0 ? Mathf.Max(throttleLevel, 1) : Mathf.Min(throttleLevel, -1);
            targetDirection = laneChangeTravelDirection >= 0 ? 1 : -1;
            lastTravelDirection = targetDirection;
        }
    }

    private bool IsOncomingDanger(int lane, float radius)
    {
        for (int i = 0; i < world.OncomingTrains.Count; i++)
        {
            OncomingTrainRuntime train = world.OncomingTrains[i];
            if (train.active && train.lane == lane && Mathf.Abs(train.x - trainX) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    private void CoupleAtStation()
    {
        PlayClick();
        if (coupledCars.Count >= MaxCoupledCars)
        {
            StartCoroutine(ShowFeedback("Можно прицепить только два вагона", WarningColor(), 1.1f));
            return;
        }

        if (!IsStopped)
        {
            StartCoroutine(ShowFeedback("Сначала останови поезд", WarningColor(), 1.1f));
            return;
        }

        if (currentStation == null)
        {
            StartCoroutine(ShowFeedback("Остановись у станции", WarningColor(), 1.1f));
            return;
        }

        TrainCarRuntime car = world.FindFreeCarAtStation(currentStation);
        if (car == null)
        {
            StartCoroutine(ShowFeedback("На станции нет свободного вагона", WarningColor(), 1.1f));
            return;
        }

        car.coupled = true;
        car.stationId = string.Empty;
        world.FreeCars.Remove(car);
        coupledCars.Add(car);
        ResetTrainTrail();
        RenderTrain(0f, true);
        PlayTrainEventSound(coupleSource, config != null ? config.coupleSounds : null, config != null ? config.coupleSound : null, ref lastCoupleSoundIndex);
        StartCoroutine(ShowFeedback("Вагон прицеплен", SuccessColor(), 1f));
    }

    private void UncoupleAtStation()
    {
        PlayClick();
        if (!IsStopped)
        {
            StartCoroutine(ShowFeedback("Сначала останови поезд", WarningColor(), 1.1f));
            return;
        }

        if (currentStation == null)
        {
            StartCoroutine(ShowFeedback("Остановись у станции", WarningColor(), 1.1f));
            return;
        }

        if (coupledCars.Count == 0)
        {
            StartCoroutine(ShowFeedback("Нечего отцеплять", WarningColor(), 1f));
            return;
        }

        TrainCarRuntime car = coupledCars[coupledCars.Count - 1];
        coupledCars.RemoveAt(coupledCars.Count - 1);
        car.coupled = false;
        car.stationId = currentStation.id;
        car.position = world.GetFreeStationSlot(currentStation);
        world.FreeCars.Add(car);
        ResetTrainTrail();
        RenderTrain(0f, true);
        PlayTrainEventSound(coupleSource, config != null ? config.coupleSounds : null, config != null ? config.coupleSound : null, ref lastCoupleSoundIndex);
        StartCoroutine(ShowFeedback("Вагон отцеплен", Color.white, 1f));
    }

    private void ToggleDoors()
    {
        PlayClick();
        if (!IsStopped || currentStation == null || !currentStation.acceptsPassengers)
        {
            StartCoroutine(ShowFeedback("Нужна пассажирская станция", WarningColor(), 1.1f));
            return;
        }

        TrainCarRuntime passengerCar = FindFirstCar(TrainCarType.Passenger);
        if (passengerCar == null)
        {
            StartCoroutine(ShowFeedback("Нужен пассажирский вагон", WarningColor(), 1.1f));
            return;
        }

        int moved = 0;
        int waitingToBoard = currentStation.waitingPassengerList.Count;
        int capacity = config != null ? config.passengerCarCapacity : 6;
        for (int i = passengerCar.passengers.Count - 1; i >= 0; i--)
        {
            TrainPassengerRuntime passenger = passengerCar.passengers[i];
            if (passenger.originStationId == currentStation.id || !SameRouteColor(passenger.routeColor, currentStation.routeColor))
            {
                continue;
            }

            passengerCar.passengers.RemoveAt(i);
            passenger.currentCar = null;
            passenger.currentStationId = currentStation.id;
            passenger.location = TrainPassengerLocation.Station;
            currentStation.waitingPassengerList.Add(passenger);
            moved++;
        }

        int boarded = 0;
        while (boarded < waitingToBoard && currentStation.waitingPassengerList.Count > 0 && passengerCar.passengers.Count < capacity)
        {
            TrainPassengerRuntime passenger = currentStation.waitingPassengerList[0];
            currentStation.waitingPassengerList.RemoveAt(0);
            passenger.currentCar = passengerCar;
            passenger.originStationId = currentStation.id;
            passenger.currentStationId = string.Empty;
            passenger.location = TrainPassengerLocation.Car;
            passengerCar.passengers.Add(passenger);
            moved++;
            boarded++;
        }

        passengerCar.passengerCount = passengerCar.passengers.Count;
        currentStation.waitingPassengers = currentStation.waitingPassengerList.Count;
        RefreshCarContents(passengerCar);
        RefreshStationVisual(currentStation);
        PlayTrainEventSound(doorSource, config != null ? config.doorSounds : null, config != null ? config.doorSound : null, ref lastDoorSoundIndex);
        StartCoroutine(ShowFeedback(moved > 0 ? "Пассажиры вошли или вышли" : "Пассажиров пока нет", moved > 0 ? SuccessColor() : Color.white, 1.1f));
        if (moved > 0)
        {
            MaybeCompleteSoftTask(1);
        }
    }

    private bool SameRouteColor(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.08f;
    }

    private void HandleCargo()
    {
        PlayClick();
        if (!IsStopped || currentStation == null || !currentStation.handlesCargo)
        {
            StartCoroutine(ShowFeedback("Нужна грузовая станция", WarningColor(), 1.1f));
            return;
        }

        TrainCarRuntime cargoCar = FindFirstCar(TrainCarType.Cargo);
        if (cargoCar == null)
        {
            StartCoroutine(ShowFeedback("Нужен грузовой вагон", WarningColor(), 1.1f));
            return;
        }

        int capacity = config != null ? config.cargoCarCapacity : 4;
        if (!string.IsNullOrEmpty(currentStation.cargoTitle) && cargoCar.cargos.Count < capacity)
        {
            TrainCargoRuntime cargo = new TrainCargoRuntime
            {
                id = string.IsNullOrEmpty(currentStation.cargoId) ? currentStation.cargoTitle : currentStation.cargoId,
                title = currentStation.cargoTitle,
                color = currentStation.cargoColor.a > 0f ? currentStation.cargoColor : new Color(0.55f, 0.32f, 0.12f)
            };
            cargoCar.cargos.Add(cargo);
            cargoCar.cargoCount = cargoCar.cargos.Count;
            cargoCar.cargoTitle = cargo.title;
            cargoCar.cargoColor = cargo.color;
            RefreshCarContents(cargoCar);
            PlayTrainEventSound(cargoSource, config != null ? config.cargoSounds : null, config != null ? config.cargoSound : null, ref lastCargoSoundIndex);
            StartCoroutine(ShowFeedback("Груз загружен: " + cargo.title, SuccessColor(), 1.1f));
            MaybeCompleteSoftTask(2);
            return;
        }

        if (cargoCar.cargos.Count > 0)
        {
            cargoCar.cargos.Clear();
            cargoCar.cargoCount = 0;
            cargoCar.cargoTitle = string.Empty;
            RefreshCarContents(cargoCar);
            PlayTrainEventSound(cargoSource, config != null ? config.cargoSounds : null, config != null ? config.cargoSound : null, ref lastCargoSoundIndex);
            StartCoroutine(ShowFeedback("Груз выгружен", SuccessColor(), 1.1f));
            MaybeCompleteSoftTask(2);
            return;
        }

        StartCoroutine(ShowFeedback("Вагон уже полный или груза нет", WarningColor(), 1.1f));
    }

    private TrainCarRuntime FindFirstCar(TrainCarType type)
    {
        for (int i = 0; i < coupledCars.Count; i++)
        {
            if (coupledCars[i].type == type)
            {
                return coupledCars[i];
            }
        }

        return null;
    }

    private void UpdateControls()
    {
        bool stoppedAtStation = IsStopped && currentStation != null;
        bool canCouple = stoppedAtStation && coupledCars.Count < MaxCoupledCars && world.FindFreeCarAtStation(currentStation) != null;
        bool canUncouple = stoppedAtStation && coupledCars.Count > 0;
        bool canDoors = stoppedAtStation && currentStation.acceptsPassengers && FindFirstCar(TrainCarType.Passenger) != null;
        bool canCargo = stoppedAtStation && currentStation.handlesCargo && FindFirstCar(TrainCarType.Cargo) != null;
        coupleButton.gameObject.SetActive(canCouple);
        uncoupleButton.gameObject.SetActive(canUncouple);
        doorButton.gameObject.SetActive(canDoors);
        cargoButton.gameObject.SetActive(canCargo);
        SetButtonTint(forwardButton, GetThrottleButtonColor(1));
        SetButtonTint(backButton, GetThrottleButtonColor(-1));
        SetArrowButtonText(forwardButton, "▶", Mathf.Max(1, Mathf.Max(0, throttleLevel)));
        SetArrowButtonText(backButton, "◀", Mathf.Max(1, Mathf.Max(0, -throttleLevel)));
        SetButtonTint(switchButton, targetLane == 1 ? ActiveColor() : new Color(0.66f, 0.49f, 0.12f));
        bool hornActive = hornSource != null && hornSource.isPlaying;
        SetButtonTint(hornButton, hornActive ? new Color(0.96f, 0.35f, 0.14f) : new Color(0.68f, 0.23f, 0.12f));
        if (!hornActive && !hornRopeDragging && hornRopeRoutine == null)
        {
            SetHornPullVisual(0f, false);
        }
    }

    private float GetThrottleFraction()
    {
        int level = Mathf.Abs(throttleLevel);
        if (level <= 0)
        {
            return 0f;
        }

        return level == 1 ? 0.48f : level == 2 ? 0.86f : 1.55f;
    }

    private void SetArrowButtonText(Button button, string arrow, int level)
    {
        if (button == null)
        {
            return;
        }

        Text text = button.GetComponentInChildren<Text>();
        if (text == null)
        {
            return;
        }

        level = Mathf.Clamp(level, 1, 3);
        text.text = level == 1 ? arrow : level == 2 ? arrow + arrow : arrow + arrow + arrow;
    }

    private Color GetThrottleButtonColor(int direction)
    {
        int activeLevel = direction > 0 ? Mathf.Max(0, throttleLevel) : Mathf.Max(0, -throttleLevel);
        if (activeLevel == 0)
        {
            return ControlColor();
        }

        Color target = direction > 0 ? ActiveColor() : new Color(0.32f, 0.55f, 0.95f);
        return Color.Lerp(ControlColor(), target, activeLevel / 3f);
    }

    private void UpdateStatus()
    {
        int passengers = 0;
        int cargo = 0;
        int passengerCars = 0;
        int cargoCars = 0;
        for (int i = 0; i < coupledCars.Count; i++)
        {
            if (coupledCars[i].type == TrainCarType.Passenger)
            {
                passengerCars++;
                passengers += coupledCars[i].passengers.Count;
            }
            else
            {
                cargoCars++;
                cargo += coupledCars[i].cargos.Count;
            }
        }

        string move = IsStopped ? "стоп" : IsBraking ? "тормозит" : currentSpeed > 0f ? "вперёд" : "назад";
        string station = currentStation != null ? currentStation.title : "нет";
        statusText.text = "Ход: " + move + "\nПуть: " + LaneName(targetLane) + "\nСтанция: " + station + "\nСостав: пасс. " + passengerCars + ", груз. " + cargoCars + "\nВнутри: " + passengers + " пасс., " + cargo + " груз.";
    }

    private string LaneName(int lane)
    {
        return lane == 0 ? "нижний" : lane == 1 ? "средний" : "верхний";
    }

    private void StartSoftTask(int index)
    {
        currentTaskIndex = Mathf.Clamp(index, 0, softTaskTitles.Length - 1);
        taskAdvanceActive = false;
        taskTitleText.text = softTaskTitles[currentTaskIndex];
        taskPromptText.text = softTaskPrompts[currentTaskIndex];
        SpeakCurrentTask();
    }

    private void MaybeCompleteSoftTask(int typeIndex)
    {
        if (taskAdvanceActive)
        {
            return;
        }

        if (currentTaskIndex == typeIndex || currentTaskIndex == 0)
        {
            StartCoroutine(CompleteSoftTaskRoutine());
        }
    }

    private IEnumerator CompleteSoftTaskRoutine()
    {
        taskAdvanceActive = true;
        PlayTrainEventSound(successSource, config != null ? config.successSounds : null, config != null ? config.successSound : null, ref lastSuccessSoundIndex);
        yield return ShowFeedback("Отлично!", SuccessColor(), 0.9f);
        yield return new WaitForSeconds(0.25f);
        StartSoftTask((currentTaskIndex + 1) % softTaskTitles.Length);
    }

    private void SpeakCurrentTask()
    {
        Speak(taskPromptText != null ? taskPromptText.text : "Свободная поездка.");
    }

    private void Speak(string text)
    {
        if (config == null || !config.useTextToSpeech || textSpeaker == null)
        {
            return;
        }

        float volume = AppGameManager.Instance != null ? AppGameManager.Instance.SpeechVolume : config.speechVolume;
        textSpeaker.Speak(text, Mathf.Clamp01(volume), config.speechRate, config.speechPitch, config.androidLanguage);
    }

    private Vector2 GetTrailPosition(float distanceBehind)
    {
        if (trainTrail.Count < 2)
        {
            return new Vector2(trainX - distanceBehind, trainY);
        }

        float covered = 0f;
        Vector2 previous = new Vector2(trainX, trainY);
        for (int i = trainTrail.Count - 1; i >= 0; i--)
        {
            Vector2 current = trainTrail[i];
            float segment = Vector2.Distance(previous, current);
            if (covered + segment >= distanceBehind)
            {
                float t = Mathf.InverseLerp(covered, covered + segment, distanceBehind);
                return Vector2.Lerp(previous, current, t);
            }

            covered += segment;
            previous = current;
        }

        return trainTrail[0];
    }

    private void ResetTrainTrail()
    {
        trainTrail.Clear();
        for (int i = coupledCars.Count + 7; i >= 0; i--)
        {
            trainTrail.Add(new Vector2(trainX - CarSpacing * i, trainY));
        }

        for (int i = 0; i < sectionPositionInitialized.Length; i++)
        {
            sectionPositionInitialized[i] = false;
            sectionSwitchJoltTimers[i] = 0f;
        }
    }

    private void RefreshCarContents(TrainCarRuntime car)
    {
        if (car == null || car.rect == null)
        {
            return;
        }

        if (car.contentRoot == null)
        {
            GameObject rootObject = new GameObject("CarContent", typeof(RectTransform));
            rootObject.transform.SetParent(car.rect, false);
            car.contentRoot = rootObject.GetComponent<RectTransform>();
            SetAnchors(car.contentRoot, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.82f));
        }

        for (int i = car.contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(car.contentRoot.GetChild(i).gameObject);
        }

        if (car.type == TrainCarType.Passenger)
        {
            for (int i = 0; i < car.passengers.Count; i++)
            {
                int col = i % 3;
                int row = i / 3;
                Image dot = CreatePanel(car.contentRoot, "PassengerDot", car.passengers[i].routeColor, Vector2.zero, Vector2.zero);
                dot.raycastTarget = false;
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                dot.rectTransform.sizeDelta = new Vector2(24f, 24f);
                dot.rectTransform.anchoredPosition = new Vector2(-56f + col * 56f, 24f - row * 48f);
            }
            car.passengerCount = car.passengers.Count;
            return;
        }

        for (int i = 0; i < car.cargos.Count; i++)
        {
            int col = i % 2;
            int row = i / 2;
            Image cargo = CreatePanel(car.contentRoot, "CargoBlock", car.cargos[i].color, Vector2.zero, Vector2.zero);
            cargo.raycastTarget = false;
            cargo.rectTransform.anchorMin = cargo.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            cargo.rectTransform.sizeDelta = new Vector2(72f, 44f);
            cargo.rectTransform.anchoredPosition = new Vector2(-42f + col * 84f, 24f - row * 56f);
        }
        car.cargoCount = car.cargos.Count;
    }

    private void RefreshStationVisual(TrainStationRuntime station)
    {
        if (station == null || station.rect == null)
        {
            return;
        }

        if (station.label != null && station.label.detail != null)
        {
            if (station.acceptsPassengers)
            {
                station.label.detail.text = station.waitingPassengerList.Count > 0 ? "Пассажиры: " + station.waitingPassengerList.Count : "Пассажиров нет";
            }
            else
            {
                station.label.detail.text = string.IsNullOrEmpty(station.cargoTitle) ? "Груз ждёт" : "Груз: " + station.cargoTitle;
            }
        }

        Transform existingDots = station.rect.Find("StationWaitingDots");
        RectTransform dotsRoot;
        if (existingDots == null)
        {
            dotsRoot = new GameObject("StationWaitingDots", typeof(RectTransform)).GetComponent<RectTransform>();
            dotsRoot.SetParent(station.rect, false);
            SetAnchors(dotsRoot, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.30f));
        }
        else
        {
            dotsRoot = existingDots.GetComponent<RectTransform>();
        }

        for (int i = dotsRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(dotsRoot.GetChild(i).gameObject);
        }

        if (!station.acceptsPassengers)
        {
            return;
        }

        int dotCount = Mathf.Min(10, station.waitingPassengerList.Count);
        for (int i = 0; i < dotCount; i++)
        {
            Image dot = CreatePanel(dotsRoot, "WaitingPassengerDot", station.waitingPassengerList[i].routeColor, Vector2.zero, Vector2.zero);
            dot.raycastTarget = false;
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            dot.rectTransform.sizeDelta = new Vector2(18f, 18f);
            dot.rectTransform.anchoredPosition = new Vector2(14f + i * 24f, 0f);
        }
    }

    private void UpdateOncomingVisual(OncomingTrainRuntime train)
    {
        if (train.root == null)
        {
            return;
        }

        train.root.anchoredPosition = new Vector2(train.x, world.GetLaneY(train.lane));
    }

    private void AddLocomotiveDetails(RectTransform parent)
    {
        AddSmallBlock(parent, new Vector2(0.04f, 0.18f), new Vector2(0.20f, 0.82f), new Color(0.08f, 0.09f, 0.11f, 0.88f));
        AddSmallBlock(parent, new Vector2(0.22f, 0.25f), new Vector2(0.42f, 0.75f), new Color(0.12f, 0.14f, 0.16f, 0.76f));
        AddSmallBlock(parent, new Vector2(0.54f, 0.18f), new Vector2(0.86f, 0.82f), new Color(0.95f, 0.68f, 0.22f, 0.92f));
        AddSmallBlock(parent, new Vector2(0.87f, 0.34f), new Vector2(1.02f, 0.66f), new Color(0.12f, 0.12f, 0.13f, 0.95f));
        AddSmallBlock(parent, new Vector2(0.10f, 0.04f), new Vector2(0.24f, 0.16f), Color.black);
        AddSmallBlock(parent, new Vector2(0.56f, 0.04f), new Vector2(0.72f, 0.16f), Color.black);
        AddSmallBlock(parent, new Vector2(0.10f, 0.84f), new Vector2(0.24f, 0.96f), Color.black);
        AddSmallBlock(parent, new Vector2(0.56f, 0.84f), new Vector2(0.72f, 0.96f), Color.black);
        Text label = CreateText(parent, "ЛОК", 32, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(label.rectTransform, new Vector2(0.25f, 0.28f), new Vector2(0.62f, 0.72f));
    }

    private void AddCarDetails(RectTransform parent, TrainCarType type)
    {
        if (type == TrainCarType.Passenger)
        {
            for (int i = 0; i < 4; i++)
            {
                AddSmallBlock(parent, new Vector2(0.14f + i * 0.18f, 0.25f), new Vector2(0.26f + i * 0.18f, 0.75f), new Color(0.82f, 0.94f, 1f, 0.92f));
            }
        }
        else
        {
            AddSmallBlock(parent, new Vector2(0.08f, 0.18f), new Vector2(0.30f, 0.82f), new Color(0.50f, 0.28f, 0.10f));
            AddSmallBlock(parent, new Vector2(0.38f, 0.18f), new Vector2(0.60f, 0.82f), new Color(0.68f, 0.42f, 0.16f));
            AddSmallBlock(parent, new Vector2(0.68f, 0.18f), new Vector2(0.90f, 0.82f), new Color(0.40f, 0.30f, 0.18f));
        }

        AddSmallBlock(parent, new Vector2(0.14f, 0.03f), new Vector2(0.32f, 0.16f), Color.black);
        AddSmallBlock(parent, new Vector2(0.68f, 0.03f), new Vector2(0.86f, 0.16f), Color.black);
        AddSmallBlock(parent, new Vector2(0.14f, 0.84f), new Vector2(0.32f, 0.97f), Color.black);
        AddSmallBlock(parent, new Vector2(0.68f, 0.84f), new Vector2(0.86f, 0.97f), Color.black);
        Text label = CreateText(parent, type == TrainCarType.Passenger ? "Пасс." : "Груз", 27, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(label.rectTransform, new Vector2(0.18f, 0.32f), new Vector2(0.82f, 0.68f));
    }

    private void CreateTree(RectTransform parent, Vector2 position)
    {
        Image trunk = CreatePanel(parent, "TreeTrunk", new Color(0.40f, 0.22f, 0.10f), Vector2.zero, Vector2.zero);
        trunk.raycastTarget = false;
        trunk.rectTransform.anchorMin = trunk.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        trunk.rectTransform.anchoredPosition = position + new Vector2(0f, -24f);
        trunk.rectTransform.sizeDelta = new Vector2(22f, 44f);
        Image crown = CreatePanel(parent, "Tree", new Color(0.13f, 0.48f, 0.18f), Vector2.zero, Vector2.zero);
        crown.raycastTarget = false;
        crown.rectTransform.anchorMin = crown.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        crown.rectTransform.anchoredPosition = position;
        crown.rectTransform.sizeDelta = new Vector2(74f, 74f);
    }

    private void CreateSpringTree(RectTransform parent, Vector2 position)
    {
        CreateWorldBlock(parent, "SpringTreeTrunk", position + new Vector2(0f, -24f), new Vector2(20f, 44f), new Color(0.42f, 0.24f, 0.12f));
        CreateWorldBlock(parent, "SpringTreeCrown", position, new Vector2(78f, 68f), new Color(0.94f, 0.65f, 0.78f));
        CreateWorldBlock(parent, "SpringTreeLeaf", position + new Vector2(18f, 8f), new Vector2(36f, 34f), new Color(0.70f, 0.88f, 0.48f));
        CreateWorldBlock(parent, "SpringTreeBloom", position + new Vector2(-20f, 10f), new Vector2(24f, 22f), new Color(1f, 0.86f, 0.92f));
    }

    private void CreateConifer(RectTransform parent, Vector2 position)
    {
        CreateWorldBlock(parent, "ConiferTrunk", position + new Vector2(0f, -34f), new Vector2(18f, 52f), new Color(0.36f, 0.20f, 0.10f));
        CreateWorldBlock(parent, "ConiferLow", position + new Vector2(0f, -10f), new Vector2(86f, 42f), new Color(0.08f, 0.36f, 0.16f));
        CreateWorldBlock(parent, "ConiferMid", position + new Vector2(0f, 16f), new Vector2(66f, 38f), new Color(0.10f, 0.46f, 0.19f));
        CreateWorldBlock(parent, "ConiferTop", position + new Vector2(0f, 42f), new Vector2(42f, 34f), new Color(0.13f, 0.54f, 0.22f));
    }

    private void CreatePine(RectTransform parent, Vector2 position)
    {
        CreateWorldBlock(parent, "PineTrunk", position + new Vector2(0f, -26f), new Vector2(18f, 72f), new Color(0.42f, 0.24f, 0.12f));
        CreateWorldBlock(parent, "PineNeedles", position + new Vector2(0f, 38f), new Vector2(78f, 52f), new Color(0.10f, 0.42f, 0.18f));
        CreateWorldBlock(parent, "PineNeedlesDark", position + new Vector2(18f, 48f), new Vector2(34f, 28f), new Color(0.06f, 0.30f, 0.14f));
    }

    private void CreateRiver(RectTransform parent, float startX, float endX, float y, bool slopesUp)
    {
        Vector2 start = new Vector2(startX, y + (slopesUp ? -28f : 28f));
        Vector2 end = new Vector2(endX, y + (slopesUp ? 28f : -28f));
        CreateLine(parent, "River", start, end, 78f, new Color(0.18f, 0.58f, 0.86f, 0.62f));
        CreateLine(parent, "RiverFoam", start + new Vector2(0f, 14f), end + new Vector2(0f, 14f), 10f, new Color(0.78f, 0.94f, 1f, 0.45f));
        CreateLine(parent, "RiverFoam", start + new Vector2(0f, -16f), end + new Vector2(0f, -16f), 8f, new Color(0.78f, 0.94f, 1f, 0.30f));
    }

    private void CreateMountains(RectTransform parent, float startX, float endX, float y)
    {
        int count = 4;
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(startX, endX, (i + 0.25f) / count);
            float size = 92f + (i % 2) * 34f;
            Image mountain = CreateWorldBlock(parent, "Mountain", new Vector2(x, y), new Vector2(size, size), new Color(0.48f, 0.54f, 0.58f, 0.72f));
            mountain.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image snow = CreateWorldBlock(parent, "MountainSnow", new Vector2(x, y + size * 0.35f), new Vector2(size * 0.34f, size * 0.22f), new Color(0.94f, 0.95f, 0.92f, 0.86f));
            snow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
    }

    private void CreateAnimal(RectTransform parent, Vector2 position, bool light)
    {
        string[] keys = { "animal_baby_deer", "animal_bear", "animal_deer", "animal_fox", "animal_hedgehog", "animal_mouse", "animal_owl", "animal_wolf" };
        string key = keys[Mathf.Abs(Mathf.RoundToInt(position.x + position.y)) % keys.Length];
        Sprite animalSprite = LoadTrainSprite(key);
        if (animalSprite != null)
        {
            Image animal = CreatePanel(parent, "AnimalSprite", Color.white, Vector2.zero, Vector2.zero);
            animal.raycastTarget = false;
            animal.sprite = animalSprite;
            animal.preserveAspect = true;
            animal.rectTransform.anchorMin = animal.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            animal.rectTransform.anchoredPosition = position;
            animal.rectTransform.sizeDelta = new Vector2(88f, 88f);
            return;
        }

        Color bodyColor = light ? new Color(0.72f, 0.48f, 0.24f) : new Color(0.38f, 0.30f, 0.22f);
        CreateWorldBlock(parent, "AnimalBody", position, new Vector2(62f, 32f), bodyColor);
        CreateWorldBlock(parent, "AnimalHead", position + new Vector2(38f, 8f), new Vector2(28f, 24f), bodyColor);
        CreateWorldBlock(parent, "AnimalLeg", position + new Vector2(-20f, -24f), new Vector2(8f, 24f), new Color(0.20f, 0.14f, 0.09f));
        CreateWorldBlock(parent, "AnimalLeg", position + new Vector2(18f, -24f), new Vector2(8f, 24f), new Color(0.20f, 0.14f, 0.09f));
        CreateWorldBlock(parent, "AnimalEye", position + new Vector2(46f, 14f), new Vector2(6f, 6f), Color.black);
    }

    private void CreateRock(RectTransform parent, Vector2 position)
    {
        Image rock = CreatePanel(parent, "Rock", new Color(0.42f, 0.44f, 0.45f), Vector2.zero, Vector2.zero);
        rock.raycastTarget = false;
        rock.rectTransform.anchorMin = rock.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rock.rectTransform.anchoredPosition = position;
        rock.rectTransform.sizeDelta = new Vector2(70f, 44f);
    }

    private void CreateRoadCar(RectTransform parent, Vector2 position, Color color)
    {
        string spriteKey = "car" + (Mathf.Abs(Mathf.RoundToInt(position.x)) % 4 + 1);
        Sprite carSprite = LoadTrainSprite(spriteKey);
        if (carSprite != null)
        {
            Image carSpriteImage = CreatePanel(parent, "CarSprite", Color.white, Vector2.zero, Vector2.zero);
            carSpriteImage.raycastTarget = false;
            carSpriteImage.sprite = carSprite;
            carSpriteImage.preserveAspect = true;
            carSpriteImage.rectTransform.anchorMin = carSpriteImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            carSpriteImage.rectTransform.anchoredPosition = position;
            carSpriteImage.rectTransform.sizeDelta = new Vector2(86f, 86f);
            carSpriteImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            return;
        }

        Image car = CreatePanel(parent, "Car", color, Vector2.zero, Vector2.zero);
        car.raycastTarget = false;
        car.rectTransform.anchorMin = car.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        car.rectTransform.anchoredPosition = position;
        car.rectTransform.sizeDelta = new Vector2(92f, 46f);
        AddSmallBlock(car.rectTransform, new Vector2(0.18f, 0.18f), new Vector2(0.36f, 0.82f), Color.black);
        AddSmallBlock(car.rectTransform, new Vector2(0.64f, 0.18f), new Vector2(0.82f, 0.82f), Color.black);
    }

    private Image CreateWorldBlock(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        Image block = CreatePanel(parent, name, color, Vector2.zero, Vector2.zero);
        block.raycastTarget = false;
        block.rectTransform.anchorMin = block.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        block.rectTransform.anchoredPosition = position;
        block.rectTransform.sizeDelta = size;
        return block;
    }

    private Image AddSmallBlock(RectTransform parent, Vector2 min, Vector2 max, Color color)
    {
        Image block = CreatePanel(parent, "Detail", color, min, max);
        block.raycastTarget = false;
        return block;
    }

    private void BuildHornRope(RectTransform parent)
    {
        Text label = CreateText(parent, "Гудок", 20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(label.rectTransform, new Vector2(0.05f, 0.52f), new Vector2(0.62f, 0.92f));

        Image rope = AddSmallBlock(parent, new Vector2(0.70f, 0.18f), new Vector2(0.74f, 0.88f), new Color(0.08f, 0.05f, 0.03f, 0.94f));
        hornRopeRect = rope.rectTransform;
        hornCrossbarRect = AddSmallBlock(parent, new Vector2(0.60f, 0.70f), new Vector2(0.84f, 0.83f), new Color(0.05f, 0.03f, 0.02f, 0.96f)).rectTransform;
        hornLeverHandle = hornCrossbarRect;
    }

    private void SetHornPullVisual(float pull, bool wobble)
    {
        if (hornCrossbarRect == null)
        {
            return;
        }

        float y = Mathf.Lerp(0f, -42f, Mathf.Clamp01(pull));
        float angle = wobble ? Mathf.Sin(Time.time * 18f) * 5f * Mathf.Clamp01(pull) : 0f;
        hornCrossbarRect.anchoredPosition = new Vector2(0f, y);
        hornCrossbarRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        if (hornRopeRect != null)
        {
            hornRopeRect.sizeDelta = new Vector2(hornRopeRect.sizeDelta.x, Mathf.Lerp(0f, 26f, Mathf.Clamp01(pull)));
        }
    }

    private bool ApplySprite(Image image, string key)
    {
        Sprite sprite = LoadTrainSprite(key);
        if (sprite == null || image == null)
        {
            return false;
        }

        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        return true;
    }

    private Sprite LoadTrainSprite(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (trainSpriteCache.TryGetValue(key, out Sprite cached))
        {
            return cached;
        }

        Texture2D texture = Resources.Load<Texture2D>("TrainSprites/" + key);
        if (texture == null)
        {
            trainSpriteCache[key] = null;
            return null;
        }

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        trainSpriteCache[key] = sprite;
        return sprite;
    }

    private void CreateRailSpriteTiles(RectTransform parent, float startX, float endX, float y)
    {
        Sprite railSprite = LoadTrainSprite("railroad");
        if (railSprite == null)
        {
            return;
        }

        float tileWidth = 300f;
        int count = Mathf.CeilToInt((endX - startX) / tileWidth);
        for (int i = 0; i < count; i++)
        {
            Image tile = CreatePanel(parent, "RailSprite", Color.white, Vector2.zero, Vector2.zero);
            tile.raycastTarget = false;
            tile.sprite = railSprite;
            tile.preserveAspect = true;
            tile.rectTransform.anchorMin = tile.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            tile.rectTransform.anchoredPosition = new Vector2(startX + tileWidth * (i + 0.5f), y);
            tile.rectTransform.sizeDelta = new Vector2(tileWidth, 62f);
            tile.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }
    }

    private IEnumerator PulseRect(RectTransform rect)
    {
        if (rect == null)
        {
            yield break;
        }

        Vector3 start = rect.localScale;
        float elapsed = 0f;
        while (elapsed < 0.35f && rect != null)
        {
            elapsed += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(elapsed / 0.35f * Mathf.PI) * 0.12f;
            rect.localScale = start * pulse;
            yield return null;
        }

        if (rect != null)
        {
            rect.localScale = start;
        }
    }

    private IEnumerator ShowFeedback(string text, Color color, float seconds)
    {
        if (feedbackText == null)
        {
            yield break;
        }

        feedbackText.text = text;
        feedbackText.color = color;
        yield return new WaitForSeconds(seconds);
        if (feedbackText != null && feedbackText.text == text)
        {
            feedbackText.text = string.Empty;
        }
    }

    private void ReturnToMenu()
    {
        StopLocalAudio();
        textSpeaker?.Stop();
        AppGameManager.Instance?.PlayMenuMusic();
        SceneManager.LoadScene(config != null ? config.menuSceneName : "MainMenuScene");
    }

    private void PlayMusic()
    {
        AppGameManager.Instance?.StopMusic();
        if (config == null || config.roomMusic == null || musicSource == null)
        {
            return;
        }

        if (musicSource.clip == config.roomMusic && musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = config.roomMusic;
        musicSource.loop = true;
        musicSource.volume = AppGameManager.Instance != null ? AppGameManager.Instance.MusicVolume : 0.35f;
        musicSource.Play();
    }

    private void StopLocalAudio()
    {
        musicSource?.Stop();
        movementSource?.Stop();
        hornSource?.Stop();
        switchSource?.Stop();
        coupleSource?.Stop();
        doorSource?.Stop();
        cargoSource?.Stop();
        successSource?.Stop();
        soundSource?.Stop();
    }

    private void OnDestroy()
    {
        StopLocalAudio();
    }

    private void UpdateMovementSound()
    {
        if (movementSource == null || config == null)
        {
            return;
        }

        int level = GetMovementSoundLevel();
        if (level <= 0)
        {
            if (movementSource.isPlaying)
            {
                movementSource.Stop();
            }

            currentMovementSoundLevel = 0;
            return;
        }

        AudioClip clip = GetMovementClip(level);
        if (clip == null)
        {
            if (movementSource.isPlaying)
            {
                movementSource.Stop();
            }

            currentMovementSoundLevel = 0;
            return;
        }

        movementSource.volume = (AppGameManager.Instance != null ? AppGameManager.Instance.EffectsVolume : 0.8f) * Mathf.Clamp01(config.movementSoundVolume);
        if (currentMovementSoundLevel != level || movementSource.clip != clip)
        {
            movementSource.clip = clip;
            movementSource.Play();
            currentMovementSoundLevel = level;
        }
        else if (!movementSource.isPlaying)
        {
            movementSource.Play();
        }
    }

    private int GetMovementSoundLevel()
    {
        float speed = Mathf.Abs(currentSpeed);
        if (speed <= StopThreshold * 1.4f)
        {
            return 0;
        }

        int throttle = Mathf.Abs(throttleLevel);
        if (throttle > 0)
        {
            return Mathf.Clamp(throttle, 1, 3);
        }

        float maxSpeed = TrainSpeed * 1.55f;
        return Mathf.Clamp(Mathf.CeilToInt(Mathf.InverseLerp(0f, maxSpeed, speed) * 3f), 1, 3);
    }

    private AudioClip GetMovementClip(int level)
    {
        if (config == null)
        {
            return null;
        }

        if (level <= 1)
        {
            return config.movementSpeed1Sound;
        }

        return level == 2 ? config.movementSpeed2Sound : config.movementSpeed3Sound;
    }

    private void PlayClick()
    {
        AppGameManager.Instance?.PlayButtonClick();
    }

    private AudioSource CreateEventAudioSource(string sourceName)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.name = sourceName;
        return source;
    }

    private void PlayTrainEventSound(AudioSource source, AudioClip[] clips, AudioClip fallback, ref int lastClipIndex)
    {
        if (source != null && source.isPlaying)
        {
            return;
        }

        AudioClip clip = PickRandomClip(clips, fallback, ref lastClipIndex);
        if (clip != null)
        {
            if (source != null)
            {
                source.clip = clip;
                source.volume = AppGameManager.Instance != null ? AppGameManager.Instance.EffectsVolume : 0.8f;
                source.Play();
            }
            else if (soundSource != null)
            {
                soundSource.PlayOneShot(clip, AppGameManager.Instance != null ? AppGameManager.Instance.EffectsVolume : 0.8f);
            }

            return;
        }

        AppGameManager.Instance?.PlayButtonClick();
    }

    private AudioClip PickRandomClip(AudioClip[] clips, AudioClip fallback, ref int lastClipIndex)
    {
        if (clips == null || clips.Length == 0)
        {
            lastClipIndex = -1;
            return fallback;
        }

        List<int> candidates = new List<int>();
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && (clips.Length == 1 || i != lastClipIndex))
            {
                candidates.Add(i);
            }
        }

        if (candidates.Count == 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                {
                    candidates.Add(i);
                }
            }
        }

        if (candidates.Count == 0)
        {
            lastClipIndex = -1;
            return fallback;
        }

        int selectedIndex = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        lastClipIndex = selectedIndex;
        return clips[selectedIndex];
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas result = canvasObject.GetComponent<Canvas>();
        result.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = config != null ? config.referenceResolution : new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return result;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(transform, false);
        }
    }

    private void EnsureCamera()
    {
        if (Camera.main != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = config != null ? config.backgroundColor : Color.black;
        camera.orthographic = true;
    }

    private Image CreatePanel(RectTransform parent, string name, Color color, Vector2 min, Vector2 max)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        SetAnchors(panel.GetComponent<RectTransform>(), min, max);
        return image;
    }

    private Image CreateLine(RectTransform parent, string name, Vector2 start, Vector2 end, float width, Color color)
    {
        Image image = CreatePanel(parent, name, color, Vector2.zero, Vector2.zero);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        UpdateLine(rect, start, end, width);
        image.raycastTarget = false;
        return image;
    }

    private void UpdateLine(RectTransform rect, Vector2 start, Vector2 end, float width)
    {
        Vector2 delta = end - start;
        rect.anchoredPosition = (start + end) * 0.5f;
        rect.sizeDelta = new Vector2(Mathf.Max(1f, delta.magnitude), width);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private Text CreateText(RectTransform parent, string text, int size, FontStyle style, TextAnchor anchor, Color color)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.font = uiFont;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = anchor;
        label.color = color;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    private Button CreateButton(RectTransform parent, string label, int fontSize, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(DwellSelectable));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        SetButtonTint(button, color);
        button.onClick.AddListener(onClick);

        Text text = CreateText(buttonObject.GetComponent<RectTransform>(), label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = fontSize;
        SetAnchors(text.rectTransform, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f));
        AddDwell(buttonObject, buttonObject.GetComponent<RectTransform>());
        return button;
    }

    private void AddDwell(GameObject target, RectTransform rect)
    {
        DwellSelectable dwell = target.GetComponent<DwellSelectable>();
        if (dwell == null)
        {
            dwell = target.AddComponent<DwellSelectable>();
        }

        Image progress = CreatePanel(rect, "DwellProgress", new Color(1f, 1f, 1f, 0.32f), new Vector2(0f, 0f), new Vector2(1f, 0.08f));
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        progress.fillOrigin = 0;
        progress.fillAmount = 0f;
        dwell.Configure(config != null ? config.dwellSeconds : 1.1f, progress);
    }

    private void SetButtonTint(Button button, Color color)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.14f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
    }

    private void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
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

    private Color ControlColor()
    {
        return config != null ? config.buttonColor : new Color(0.08f, 0.35f, 0.58f);
    }

    private Color ActiveColor()
    {
        return config != null ? config.activeColor : new Color(0.20f, 0.75f, 0.35f);
    }

    private Color WarningColor()
    {
        return config != null ? config.warningColor : new Color(0.92f, 0.25f, 0.20f);
    }

    private Color SuccessColor()
    {
        return config != null ? config.activeColor : new Color(0.20f, 0.75f, 0.35f);
    }
}

public enum TrainWorldChunkType
{
    Straight,
    PassengerStation,
    CargoStation,
    Switch,
    OncomingTrain,
    Scenery
}

public class TrainWorldChunkRuntime
{
    public int index;
    public float startX;
    public float endX;
    public TrainWorldChunkType type;
    public RectTransform root;
    public bool hasSwitch;
    public bool switchConsumed;
    public float switchX;
    public int targetLane;
    public Image switchLight;
    public Image switchDirectionLine;
    public Text switchLabel;
    public readonly List<TrainStationRuntime> stations = new List<TrainStationRuntime>();
    public readonly List<TrainCarRuntime> freeCars = new List<TrainCarRuntime>();
    public readonly List<OncomingTrainRuntime> oncomingTrains = new List<OncomingTrainRuntime>();
}

public class OncomingTrainRuntime
{
    public string id;
    public TrainCarType trainType;
    public int lane;
    public int carCount;
    public float x;
    public float speed;
    public float chunkStartX;
    public float smokeTimer;
    public bool hornStarted;
    public bool active = true;
    public Color locomotiveColor;
    public Color[] carColors;
    public RectTransform root;
    public AudioSource hornSource;
}

public class TrainScrollerWorldController : MonoBehaviour
{
    private const int LaneCountValue = 3;
    private TrainGameConfig config;
    private readonly System.Random random = new System.Random(24);
    private float generatedUntil;
    private int nextChunkIndex;
    private int stationNameIndex;
    private int routeColorIndex;

    public readonly List<TrainWorldChunkRuntime> Chunks = new List<TrainWorldChunkRuntime>();
    public readonly List<TrainStationRuntime> Stations = new List<TrainStationRuntime>();
    public readonly List<TrainCarRuntime> FreeCars = new List<TrainCarRuntime>();
    public readonly List<OncomingTrainRuntime> OncomingTrains = new List<OncomingTrainRuntime>();

    public int PlayerLane { get; set; }
    public int LaneCount => LaneCountValue;
    public float OncomingLaneY => GetLaneY(2);

    public void Initialize(TrainGameConfig trainConfig)
    {
        config = trainConfig;
        Chunks.Clear();
        Stations.Clear();
        FreeCars.Clear();
        OncomingTrains.Clear();
        generatedUntil = -800f;
        nextChunkIndex = 0;
        stationNameIndex = 0;
        routeColorIndex = 0;
    }

    public float GetLaneY(int lane)
    {
        return lane == 0 ? -165f : lane == 1 ? 105f : 315f;
    }

    public int GetNearestLaneIndex(float y)
    {
        int bestLane = 0;
        float bestDistance = Mathf.Abs(y - GetLaneY(0));
        for (int lane = 1; lane < LaneCountValue; lane++)
        {
            float distance = Mathf.Abs(y - GetLaneY(lane));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestLane = lane;
            }
        }

        return bestLane;
    }

    public int PickLaneAwayFrom(int occupiedLane, int seed)
    {
        int normalizedOccupied = Mathf.Clamp(occupiedLane, 0, LaneCountValue - 1);
        int lane = Mathf.Abs(seed) % (LaneCountValue - 1);
        if (lane >= normalizedOccupied)
        {
            lane++;
        }

        return Mathf.Clamp(lane, 0, LaneCountValue - 1);
    }

    public List<TrainWorldChunkRuntime> GenerateAhead(float trainX)
    {
        List<TrainWorldChunkRuntime> result = new List<TrainWorldChunkRuntime>();
        float ahead = config != null ? config.generateAheadDistance : 3600f;
        while (generatedUntil < trainX + ahead)
        {
            TrainWorldChunkRuntime chunk = CreateNextChunk();
            Chunks.Add(chunk);
            Stations.AddRange(chunk.stations);
            FreeCars.AddRange(chunk.freeCars);
            OncomingTrains.AddRange(chunk.oncomingTrains);
            result.Add(chunk);
        }

        return result;
    }

    public List<TrainWorldChunkRuntime> RemoveBehind(float trainX)
    {
        List<TrainWorldChunkRuntime> result = new List<TrainWorldChunkRuntime>();
        float behind = config != null ? config.despawnBehindDistance : 1800f;
        for (int i = Chunks.Count - 1; i >= 0; i--)
        {
            TrainWorldChunkRuntime chunk = Chunks[i];
            if (chunk.endX >= trainX - behind)
            {
                continue;
            }

            Chunks.RemoveAt(i);
            result.Add(chunk);
            for (int s = 0; s < chunk.stations.Count; s++)
            {
                Stations.Remove(chunk.stations[s]);
            }

            for (int c = 0; c < chunk.freeCars.Count; c++)
            {
                if (!chunk.freeCars[c].coupled)
                {
                    FreeCars.Remove(chunk.freeCars[c]);
                }
            }

            for (int t = 0; t < chunk.oncomingTrains.Count; t++)
            {
                OncomingTrains.Remove(chunk.oncomingTrains[t]);
            }
        }

        return result;
    }

    public TrainStationRuntime FindStationNear(float trainX, int lane, float radius)
    {
        TrainStationRuntime best = null;
        float bestDistance = radius;
        for (int i = 0; i < Stations.Count; i++)
        {
            TrainStationRuntime station = Stations[i];
            if (station.lane != lane)
            {
                continue;
            }

            float distance = Mathf.Abs(station.position.x - trainX);
            if (distance <= bestDistance)
            {
                best = station;
                bestDistance = distance;
            }
        }

        return best;
    }

    public TrainWorldChunkRuntime FindSwitchNear(float trainX, float radius)
    {
        TrainWorldChunkRuntime bestAhead = null;
        float bestAheadDistance = radius;
        for (int i = 0; i < Chunks.Count; i++)
        {
            if (!Chunks[i].hasSwitch)
            {
                continue;
            }

            float forwardDistance = Chunks[i].switchX - trainX;
            if (forwardDistance >= 0f && forwardDistance <= bestAheadDistance)
            {
                bestAhead = Chunks[i];
                bestAheadDistance = forwardDistance;
            }
        }

        if (bestAhead != null)
        {
            return bestAhead;
        }

        for (int i = 0; i < Chunks.Count; i++)
        {
            if (Chunks[i].hasSwitch && Mathf.Abs(Chunks[i].switchX - trainX) <= radius)
            {
                return Chunks[i];
            }
        }

        return null;
    }

    public TrainCarRuntime FindFreeCarAtStation(TrainStationRuntime station)
    {
        if (station == null)
        {
            return null;
        }

        for (int i = 0; i < FreeCars.Count; i++)
        {
            if (!FreeCars[i].coupled && FreeCars[i].stationId == station.id)
            {
                return FreeCars[i];
            }
        }

        return null;
    }

    public Vector2 GetFreeStationSlot(TrainStationRuntime station)
    {
        if (station == null)
        {
            return Vector2.zero;
        }

        int used = 0;
        for (int i = 0; i < FreeCars.Count; i++)
        {
            if (FreeCars[i].stationId == station.id)
            {
                used++;
            }
        }

        float laneY = GetLaneY(station.lane);
        return new Vector2(station.position.x + 360f + used * 330f, laneY);
    }

    private TrainWorldChunkRuntime CreateNextChunk()
    {
        float width = config != null ? Mathf.Max(800f, config.chunkWidth) : 1250f;
        TrainWorldChunkRuntime chunk = new TrainWorldChunkRuntime
        {
            index = nextChunkIndex,
            startX = generatedUntil,
            endX = generatedUntil + width,
            type = PickChunkType(nextChunkIndex)
        };

        if (nextChunkIndex == 0)
        {
            chunk.type = TrainWorldChunkType.PassengerStation;
            AddStation(chunk, TrainStationKind.Depot, "Депо", 0, chunk.startX + 720f);
            ConfigureStationSwitch(chunk, chunk.stations[0], width);
            AddFreeCar(chunk, TrainCarType.Passenger, chunk.stations[0], 0);
            AddFreeCar(chunk, TrainCarType.Cargo, chunk.stations[0], 1);
        }
        else if (chunk.type == TrainWorldChunkType.PassengerStation)
        {
            TrainStationRuntime station = AddStation(chunk, TrainStationKind.PassengerPlatform, NextStationName(), random.Next(0, LaneCountValue), chunk.startX + width * 0.52f);
            ConfigureStationSwitch(chunk, station, width);
            AddWaitingPassengers(station, 2 + nextChunkIndex % 4);
            AddFreeCar(chunk, TrainCarType.Passenger, station, 0);
        }
        else if (chunk.type == TrainWorldChunkType.CargoStation)
        {
            TrainStationRuntime station = AddStation(chunk, TrainStationKind.CargoStation, NextStationName(), random.Next(0, LaneCountValue), chunk.startX + width * 0.50f);
            ConfigureStationSwitch(chunk, station, width);
            AssignCargo(station, nextChunkIndex);
            AddFreeCar(chunk, TrainCarType.Cargo, station, 0);
        }
        else if (chunk.type == TrainWorldChunkType.Switch)
        {
            chunk.hasSwitch = true;
            chunk.switchX = chunk.startX + width * 0.48f;
            chunk.targetLane = random.Next(0, LaneCountValue);
        }
        else if (chunk.type == TrainWorldChunkType.OncomingTrain)
        {
            int carCount = 2 + random.Next(0, 5);
            Color[] carColors = new Color[carCount];
            for (int i = 0; i < carColors.Length; i++)
            {
                carColors[i] = GetTrainColor(nextChunkIndex + i + 3);
            }

            chunk.oncomingTrains.Add(new OncomingTrainRuntime
            {
                id = "oncoming-" + nextChunkIndex,
                trainType = nextChunkIndex % 2 == 0 ? TrainCarType.Passenger : TrainCarType.Cargo,
                lane = PickLaneAwayFrom(PlayerLane, nextChunkIndex + 2),
                carCount = carCount,
                x = chunk.endX + 420f,
                speed = 230f + (nextChunkIndex % 3) * 35f,
                chunkStartX = chunk.startX,
                locomotiveColor = GetTrainColor(nextChunkIndex),
                carColors = carColors
            });
        }

        generatedUntil = chunk.endX;
        nextChunkIndex++;
        return chunk;
    }

    private TrainWorldChunkType PickChunkType(int index)
    {
        if (index < 2)
        {
            return TrainWorldChunkType.Straight;
        }

        double roll = random.NextDouble();
        float passengerChance = config != null ? config.passengerStationChance : 0.26f;
        float cargoChance = config != null ? config.cargoStationChance : 0.22f;
        float switchChance = config != null ? config.switchChance : 0.20f;
        float oncomingChance = config != null ? config.oncomingTrainChance : 0.28f;

        if (roll < passengerChance)
        {
            return TrainWorldChunkType.PassengerStation;
        }

        roll -= passengerChance;
        if (roll < cargoChance)
        {
            return TrainWorldChunkType.CargoStation;
        }

        roll -= cargoChance;
        if (roll < switchChance)
        {
            return TrainWorldChunkType.Switch;
        }

        roll -= switchChance;
        if (roll < oncomingChance)
        {
            return TrainWorldChunkType.OncomingTrain;
        }

        return TrainWorldChunkType.Scenery;
    }

    private TrainStationRuntime AddStation(TrainWorldChunkRuntime chunk, TrainStationKind kind, string title, int lane, float x)
    {
        int routeIndex = routeColorIndex++;
        TrainStationRuntime station = new TrainStationRuntime
        {
            id = "station-" + chunk.index + "-" + chunk.stations.Count,
            title = title,
            kind = kind,
            lane = lane,
            routeIndex = routeIndex,
            position = new Vector2(x, GetLaneY(lane) + GetStationLabelOffset(kind, lane)),
            acceptsPassengers = kind != TrainStationKind.CargoStation,
            handlesCargo = kind != TrainStationKind.PassengerPlatform,
            passengerDestinationId = string.Empty,
            stopRadius = config != null ? config.stationRadius : 130f,
            routeColor = GetRouteColor(routeIndex)
        };
        chunk.stations.Add(station);
        return station;
    }

    private float GetStationLabelOffset(TrainStationKind kind, int lane)
    {
        if (lane >= 2)
        {
            return -170f;
        }

        if (lane == 1)
        {
            return kind == TrainStationKind.CargoStation ? -165f : 170f;
        }

        return kind == TrainStationKind.CargoStation ? -185f : 185f;
    }

    private void ConfigureStationSwitch(TrainWorldChunkRuntime chunk, TrainStationRuntime station, float width)
    {
        if (station == null)
        {
            return;
        }

        chunk.hasSwitch = true;
        chunk.switchConsumed = false;
        chunk.switchX = Mathf.Clamp(station.position.x - 470f, chunk.startX + 180f, chunk.endX - 220f);
        chunk.targetLane = station.lane;
    }

    private void AddWaitingPassengers(TrainStationRuntime station, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int destinationRouteIndex = station.routeIndex + 1 + (i % 3);
            station.waitingPassengerList.Add(new TrainPassengerRuntime
            {
                id = station.id + "-p" + i,
                originStationId = station.id,
                destinationStationId = "route-" + destinationRouteIndex,
                currentStationId = station.id,
                location = TrainPassengerLocation.Station,
                routeColor = GetRouteColor(destinationRouteIndex)
            });
        }

        station.waitingPassengers = station.waitingPassengerList.Count;
    }

    private Color GetRouteColor(int index)
    {
        Color[] palette =
        {
            new Color(0.95f, 0.18f, 0.18f),
            new Color(0.16f, 0.46f, 0.95f),
            new Color(0.95f, 0.78f, 0.16f),
            new Color(0.18f, 0.72f, 0.30f),
            new Color(0.64f, 0.26f, 0.92f)
        };
        return palette[Mathf.Abs(index) % palette.Length];
    }

    private Color GetTrainColor(int index)
    {
        Color[] palette =
        {
            new Color(0.88f, 0.16f, 0.12f),
            new Color(0.12f, 0.42f, 0.86f),
            new Color(0.90f, 0.58f, 0.12f),
            new Color(0.20f, 0.62f, 0.28f),
            new Color(0.56f, 0.22f, 0.78f),
            new Color(0.18f, 0.62f, 0.70f)
        };
        return palette[Mathf.Abs(index) % palette.Length];
    }

    private void AssignCargo(TrainStationRuntime station, int index)
    {
        string[] names = config != null && config.cargoNames != null && config.cargoNames.Length > 0 ? config.cargoNames : new[] { "дерево", "камень" };
        Color[] colors = config != null && config.cargoColors != null && config.cargoColors.Length > 0 ? config.cargoColors : new[] { new Color(0.55f, 0.32f, 0.12f), new Color(0.45f, 0.47f, 0.48f) };
        int cargoIndex = Mathf.Abs(index) % names.Length;
        station.cargoId = names[cargoIndex];
        station.cargoTitle = names[cargoIndex];
        station.cargoColor = colors[Mathf.Clamp(cargoIndex, 0, colors.Length - 1)];
    }

    private string CargoSpriteKey(string cargoTitle)
    {
        string value = (cargoTitle ?? string.Empty).ToLowerInvariant();
        if (value.Contains("wood") || value.Contains("дерев"))
        {
            return "carriage_wood";
        }

        if (value.Contains("stone") || value.Contains("кам"))
        {
            return "carriage_stone";
        }

        if (value.Contains("oil") || value.Contains("нефт"))
        {
            return "carriage_oil";
        }

        return "carriage_wood";
    }

    private void AddFreeCar(TrainWorldChunkRuntime chunk, TrainCarType type, TrainStationRuntime station, int slot)
    {
        TrainCarRuntime car = new TrainCarRuntime
        {
            id = "car-" + chunk.index + "-" + chunk.freeCars.Count,
            title = type == TrainCarType.Passenger ? "Пасс." : "Груз",
            type = type,
            stationId = station.id,
            homeStationId = station.id,
            spriteKey = type == TrainCarType.Cargo ? CargoSpriteKey(station.cargoTitle) : PassengerSpriteKey(chunk.index + slot),
            position = new Vector2(station.position.x + 360f + slot * 330f, GetLaneY(station.lane))
        };
        chunk.freeCars.Add(car);
    }

    private string PassengerSpriteKey(int variant)
    {
        return Mathf.Abs(variant) % 2 == 0 ? "passenger_carriage" : "passenger_carriage2";
    }

    private string NextStationName()
    {
        string[] names = config != null && config.stationNames != null && config.stationNames.Length > 0
            ? config.stationNames
            : new[] { "Сосновка", "Речной", "Город", "Лесная", "Карьер", "Каменная", "Луговая" };
        string name = names[stationNameIndex % names.Length];
        stationNameIndex++;
        return name;
    }
}

public class TrainIntroCinematicController : MonoBehaviour
{
    private RectTransform overlay;
    private TrainGameConfig config;
    private Font font;
    private Action onComplete;
    private bool completed;
    private AudioSource introHornSource;

    public void Play(RectTransform parent, TrainGameConfig trainConfig, Font uiFont, Action complete)
    {
        config = trainConfig;
        font = uiFont;
        onComplete = complete;
        Build(parent);
        StartCoroutine(PlayRoutine());
    }

    private void Build(RectTransform parent)
    {
        GameObject overlayObject = new GameObject("TrainIntroCinematic", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(parent, false);
        overlay = overlayObject.GetComponent<RectTransform>();
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;
        overlayObject.GetComponent<Image>().color = new Color(0.07f, 0.12f, 0.14f, 1f);
        introHornSource = overlayObject.AddComponent<AudioSource>();
        introHornSource.playOnAwake = false;

        CreatePanel("Sky", new Color(0.52f, 0.73f, 0.82f), new Vector2(0f, 0.52f), new Vector2(1f, 1f));
        CreatePanel("Field", new Color(0.36f, 0.58f, 0.31f), new Vector2(0f, 0f), new Vector2(1f, 0.56f));
        CreateText("ПОЕЗДА", 62, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0.30f, 0.78f), new Vector2(0.70f, 0.91f));
        CreateText("Короткая поездка начинается", 30, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.86f), new Vector2(0.28f, 0.70f), new Vector2(0.72f, 0.78f));

        CreateLine(new Vector2(-760f, -90f), new Vector2(760f, -90f), 86f, new Color(0.45f, 0.30f, 0.18f));
        CreateLine(new Vector2(-760f, -65f), new Vector2(760f, -65f), 10f, new Color(0.20f, 0.20f, 0.22f));
        CreateLine(new Vector2(-760f, -115f), new Vector2(760f, -115f), 10f, new Color(0.20f, 0.20f, 0.22f));
        CreateLine(new Vector2(-760f, 120f), new Vector2(760f, 120f), 48f, new Color(0.45f, 0.30f, 0.18f, 0.75f));
        CreateLine(new Vector2(-760f, 136f), new Vector2(760f, 136f), 8f, new Color(0.20f, 0.20f, 0.22f, 0.85f));
        CreateLine(new Vector2(-760f, 104f), new Vector2(760f, 104f), 8f, new Color(0.20f, 0.20f, 0.22f, 0.85f));

        for (int i = 0; i < 8; i++)
        {
            CreateTree(new Vector2(-690f + i * 210f, 250f + (i % 2) * 35f));
        }

        CreateText("Станция Сосновка", 28, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0.63f, 0.47f), new Vector2(0.84f, 0.56f), new Color(0.40f, 0.28f, 0.18f, 0.95f));

        if (config == null || config.introCanSkip)
        {
            Button skip = CreateButton("Пропустить", 23, new Color(0f, 0f, 0f, 0.58f), Complete);
            RectTransform skipRect = skip.GetComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(0.82f, 0.90f);
            skipRect.anchorMax = new Vector2(0.98f, 0.98f);
            skipRect.offsetMin = Vector2.zero;
            skipRect.offsetMax = Vector2.zero;
        }
    }

    private IEnumerator PlayRoutine()
    {
        RectTransform train = CreateIntroTrain(new Vector2(-640f, -88f), false);
        RectTransform oncoming = CreateIntroTrain(new Vector2(760f, 120f), true);
        float duration = config != null ? Mathf.Max(1f, config.introDuration) : 8f;
        float elapsed = 0f;
        bool hornPlayed = false;
        while (elapsed < duration && !completed)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            train.anchoredPosition = Vector2.Lerp(new Vector2(-640f, -88f), new Vector2(420f, -88f), Mathf.SmoothStep(0f, 1f, t));
            oncoming.anchoredPosition = Vector2.Lerp(new Vector2(760f, 120f), new Vector2(-720f, 120f), t);
            if (!hornPlayed && elapsed > 1.35f)
            {
                hornPlayed = true;
                PlayIntroHorn(train.anchoredPosition + new Vector2(130f, 35f));
            }
            if (elapsed > 0.2f && Mathf.FloorToInt(elapsed * 3f) % 2 == 0)
            {
                CreateSmoke(train.anchoredPosition + new Vector2(130f, 35f));
            }
            yield return null;
        }

        Complete();
    }

    private RectTransform CreateIntroTrain(Vector2 position, bool oncoming)
    {
        GameObject train = new GameObject(oncoming ? "IntroOncoming" : "IntroTrain", typeof(RectTransform));
        train.transform.SetParent(overlay, false);
        RectTransform rect = train.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = Vector2.zero;

        Color locomotiveColor = oncoming ? new Color(0.12f, 0.30f, 0.70f) : (config != null ? config.locomotiveColor : new Color(0.88f, 0.15f, 0.12f));
        for (int i = 0; i < 3; i++)
        {
            Image body = CreatePanel(oncoming ? "IntroCar" : "IntroCar", i == 0 ? locomotiveColor : new Color(0.18f, 0.42f, 0.78f), Vector2.zero, Vector2.zero, rect);
            body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            body.rectTransform.anchoredPosition = new Vector2((oncoming ? i : -i) * 250f, 0f);
            body.rectTransform.sizeDelta = i == 0 ? new Vector2(260f, 130f) : new Vector2(220f, 110f);
            CreatePanel("Window", new Color(0.82f, 0.94f, 1f, 0.8f), new Vector2(0.18f, 0.28f), new Vector2(0.72f, 0.72f), body.rectTransform);
        }

        return rect;
    }

    private void Complete()
    {
        if (completed)
        {
            return;
        }

        completed = true;
        if (overlay != null)
        {
            Destroy(overlay.gameObject);
        }
        onComplete?.Invoke();
        Destroy(this);
    }

    private void PlayIntroHorn(Vector2 smokeOrigin)
    {
        AudioClip clip = null;
        if (config != null && config.hornSounds != null && config.hornSounds.Length > 0)
        {
            clip = config.hornSounds[0];
        }
        else if (config != null)
        {
            clip = config.hornSound;
        }

        if (clip != null && introHornSource != null)
        {
            introHornSource.clip = clip;
            introHornSource.volume = AppGameManager.Instance != null ? AppGameManager.Instance.EffectsVolume : 0.8f;
            introHornSource.Play();
        }

        StartCoroutine(IntroHornSmokeRoutine(smokeOrigin));
    }

    private IEnumerator IntroHornSmokeRoutine(Vector2 origin)
    {
        float elapsed = 0f;
        Color smokeColor = new Color(0.08f, 0.08f, 0.08f, 0.72f);
        while (elapsed < 2.4f && !completed)
        {
            elapsed += Time.deltaTime;
            CreateSmoke(origin + new Vector2(UnityEngine.Random.Range(-8f, 32f), UnityEngine.Random.Range(-4f, 22f)), smokeColor, UnityEngine.Random.Range(1.7f, 2.5f));
            yield return new WaitForSeconds(0.08f);
        }
    }

    private void CreateSmoke(Vector2 position)
    {
        CreateSmoke(position, new Color(0.82f, 0.84f, 0.86f, 0.18f), 1f);
    }

    private void CreateSmoke(Vector2 position, Color color, float scale)
    {
        Image smoke = CreatePanel("IntroSmoke", color, Vector2.zero, Vector2.zero);
        smoke.raycastTarget = false;
        smoke.rectTransform.anchorMin = smoke.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        smoke.rectTransform.anchoredPosition = position + new Vector2(UnityEngine.Random.Range(-12f, 12f), UnityEngine.Random.Range(14f, 45f));
        smoke.rectTransform.sizeDelta = new Vector2(42f, 42f) * Mathf.Max(0.2f, scale);
        Destroy(smoke.gameObject, 1.2f);
    }

    private void CreateTree(Vector2 position)
    {
        Image trunk = CreatePanel("IntroTreeTrunk", new Color(0.40f, 0.22f, 0.10f), Vector2.zero, Vector2.zero);
        trunk.raycastTarget = false;
        trunk.rectTransform.anchorMin = trunk.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        trunk.rectTransform.anchoredPosition = position + new Vector2(0f, -28f);
        trunk.rectTransform.sizeDelta = new Vector2(20f, 54f);
        Image crown = CreatePanel("IntroTree", new Color(0.12f, 0.40f, 0.16f), Vector2.zero, Vector2.zero);
        crown.raycastTarget = false;
        crown.rectTransform.anchorMin = crown.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        crown.rectTransform.anchoredPosition = position;
        crown.rectTransform.sizeDelta = new Vector2(82f, 82f);
    }

    private Image CreatePanel(string name, Color color, Vector2 min, Vector2 max, RectTransform parent = null)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent != null ? parent : overlay, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private Image CreateLine(Vector2 start, Vector2 end, float width, Color color)
    {
        Image image = CreatePanel("IntroLine", color, Vector2.zero, Vector2.zero);
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        Vector2 delta = end - start;
        rect.anchoredPosition = (start + end) * 0.5f;
        rect.sizeDelta = new Vector2(Mathf.Max(1f, delta.magnitude), width);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        return image;
    }

    private Text CreateText(string value, int size, FontStyle style, TextAnchor anchor, Color color, Vector2 min, Vector2 max, Color? panelColor = null)
    {
        RectTransform parent = overlay;
        if (panelColor.HasValue)
        {
            parent = CreatePanel("TextPanel", panelColor.Value, min, max).rectTransform;
            min = Vector2.zero;
            max = Vector2.one;
        }

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text label = textObject.GetComponent<Text>();
        label.text = value;
        label.font = font;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = anchor;
        label.color = color;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 14;
        label.resizeTextMaxSize = size;
        return label;
    }

    private Button CreateButton(string label, int fontSize, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(DwellSelectable));
        buttonObject.transform.SetParent(overlay, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(onClick);
        Text text = CreateText(label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
        text.transform.SetParent(buttonObject.transform, false);
        Image progress = CreatePanel("DwellProgress", new Color(1f, 1f, 1f, 0.32f), new Vector2(0f, 0f), new Vector2(1f, 0.10f), buttonObject.GetComponent<RectTransform>());
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        progress.fillOrigin = 0;
        progress.fillAmount = 0f;
        buttonObject.GetComponent<DwellSelectable>().Configure(config != null ? config.dwellSeconds : 1.1f, progress);
        return button;
    }
}

public class TrainLongPressTarget : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
{
    private float holdSeconds = 1f;
    private Action onLongPress;
    private Action onPointerEnterAction;
    private bool pressing;
    private bool invoked;
    private float timer;

    public void Configure(float seconds, Action longPressAction, Action pointerEnterAction)
    {
        holdSeconds = Mathf.Max(0.2f, seconds);
        onLongPress = longPressAction;
        onPointerEnterAction = pointerEnterAction;
    }

    private void Update()
    {
        if (!pressing || invoked)
        {
            return;
        }

        timer += Time.unscaledDeltaTime;
        if (timer >= holdSeconds)
        {
            invoked = true;
            onLongPress?.Invoke();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressing = true;
        invoked = false;
        timer = 0f;
        GazePointer.NotifyActivation(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressing = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pressing = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        onPointerEnterAction?.Invoke();
    }
}

public class TrainHornRopeTarget : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerExitHandler
{
    private Action onPull;
    private Action<float> onDragPull;
    private Action onRelease;
    private bool dragging;
    private Vector2 downPosition;

    public void Configure(Action pullAction, Action<float> dragAction, Action releaseAction)
    {
        onPull = pullAction;
        onDragPull = dragAction;
        onRelease = releaseAction;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        dragging = true;
        downPosition = eventData.position;
        GazePointer.NotifyActivation(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        float pull = Mathf.Clamp01((downPosition.y - eventData.position.y) / 90f);
        onDragPull?.Invoke(pull);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        float pull = Mathf.Clamp01((downPosition.y - eventData.position.y) / 90f);
        dragging = false;
        if (pull < 0.08f)
        {
            onPull?.Invoke();
            return;
        }

        onRelease?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        dragging = false;
        onRelease?.Invoke();
    }
}

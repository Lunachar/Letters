using System;
using UnityEngine;

public enum TrainCarType
{
    Passenger,
    Cargo
}

public enum TrainTaskType
{
    CouplePassengerCar,
    OpenPassengerDoors,
    CoupleCargoCar,
    LoadCargo,
    DeliverCargo,
    TransportPassenger
}

[Serializable]
public class TrainTaskData
{
    public TrainTaskType taskType;
    public string title;
    [TextArea(2, 4)] public string prompt;
}

[CreateAssetMenu(fileName = "TrainGameConfig", menuName = "Letters/Train/Config")]
public class TrainGameConfig : ScriptableObject
{
    [Header("Scenes")]
    public string menuSceneName = "MainMenuScene";
    public string title = "Поезда";
    public string subtitle = "Вези вагоны, пассажиров и грузы";

    [Header("Movement")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public bool showIntroCinematic = true;
    [Min(1f)] public float introDuration = 8f;
    public bool introCanSkip = true;
    [Min(40f)] public float trainSpeed = 190f;
    [Min(40f)] public float reverseSpeed = 145f;
    [Min(20f)] public float acceleration = 140f;
    [Min(20f)] public float brakeDeceleration = 260f;
    [Min(20f)] public float stopTapDeceleration = 360f;
    [Min(0.1f)] public float nearStopSpeedThreshold = 5f;
    public Vector2 locomotiveSize = new Vector2(320f, 170f);
    public Vector2 carSize = new Vector2(280f, 150f);
    [Min(40f)] public float carSpacing = 132f;
    [Min(1f)] public float carSpring = 18f;
    [Min(30f)] public float coupleDistance = 115f;
    [Min(30f)] public float stationRadius = 360f;
    [Min(20f)] public float switchLockRadius = 150f;
    [Min(0.2f)] public float carLongPressSeconds = 1f;
    [Range(0.5f, 3f)] public float dwellSeconds = 1.1f;

    [Header("Scroller")]
    public float cameraFollowOffset = 180f;
    [Min(800f)] public float chunkWidth = 1250f;
    [Min(1200f)] public float generateAheadDistance = 3600f;
    [Min(800f)] public float despawnBehindDistance = 1800f;
    [Range(0f, 1f)] public float passengerStationChance = 0.26f;
    [Range(0f, 1f)] public float cargoStationChance = 0.22f;
    [Range(0f, 1f)] public float switchChance = 0.20f;
    [Range(0f, 1f)] public float oncomingTrainChance = 0.28f;
    [Min(1)] public int passengerCarCapacity = 6;
    [Min(1)] public int cargoCarCapacity = 4;
    public string[] stationNames = { "Депо", "Сосновка", "Речной", "Город", "Лесная", "Карьер", "Каменная", "Луговая" };
    public string[] cargoNames = { "дерево", "камень" };
    public Color[] cargoColors = { new Color(0.55f, 0.32f, 0.12f), new Color(0.45f, 0.47f, 0.48f) };

    [Header("Speech")]
    public bool useTextToSpeech = true;
    [Range(0f, 1f)] public float speechVolume = 1f;
    [Range(0.5f, 2f)] public float speechRate = 0.95f;
    [Range(0.5f, 2f)] public float speechPitch = 1f;
    public string androidLanguage = "ru_RU";

    [Header("Audio")]
    public AudioClip roomMusic;
    [Tooltip("Fallback clip if the matching random list is empty.")]
    public AudioClip hornSound;
    [Tooltip("Fallback clip if the matching random list is empty.")]
    public AudioClip switchSound;
    [Tooltip("Fallback clip if the matching random list is empty.")]
    public AudioClip coupleSound;
    [Tooltip("Fallback clip if the matching random list is empty.")]
    public AudioClip doorSound;
    [Tooltip("Fallback clip if the matching random list is empty.")]
    public AudioClip cargoSound;
    [Tooltip("Fallback clip if the matching random list is empty.")]
    public AudioClip successSound;
    [Tooltip("Loop played while the train moves at speed level 1.")]
    public AudioClip movementSpeed1Sound;
    [Tooltip("Loop played while the train moves at speed level 2.")]
    public AudioClip movementSpeed2Sound;
    [Tooltip("Loop played while the train moves at speed level 3.")]
    public AudioClip movementSpeed3Sound;
    [Range(0f, 1f)] public float movementSoundVolume = 0.7f;

    [Header("Random event sounds")]
    public AudioClip[] hornSounds;
    public AudioClip[] switchSounds;
    public AudioClip[] coupleSounds;
    public AudioClip[] doorSounds;
    public AudioClip[] cargoSounds;
    public AudioClip[] successSounds;

    [Header("Colors")]
    public Color backgroundColor = new Color(0.10f, 0.19f, 0.22f);
    public Color mapColor = new Color(0.78f, 0.88f, 0.74f);
    public Color railColor = new Color(0.22f, 0.22f, 0.24f);
    public Color sleeperColor = new Color(0.48f, 0.34f, 0.22f);
    public Color locomotiveColor = new Color(0.88f, 0.15f, 0.12f);
    public Color passengerCarColor = new Color(0.14f, 0.42f, 0.86f);
    public Color cargoCarColor = new Color(0.88f, 0.57f, 0.12f);
    public Color platformColor = new Color(0.64f, 0.58f, 0.50f);
    public Color cargoStationColor = new Color(0.55f, 0.37f, 0.19f);
    public Color buttonColor = new Color(0.08f, 0.35f, 0.58f);
    public Color activeColor = new Color(0.20f, 0.75f, 0.35f);
    public Color warningColor = new Color(0.92f, 0.25f, 0.20f);

    [Header("Tasks")]
    public TrainTaskData[] tasks =
    {
        new TrainTaskData
        {
            taskType = TrainTaskType.CouplePassengerCar,
            title = "Пассажирский вагон",
            prompt = "Подъедь к синему пассажирскому вагону, остановись и нажми сцепку."
        },
        new TrainTaskData
        {
            taskType = TrainTaskType.OpenPassengerDoors,
            title = "Платформа",
            prompt = "Доедь до платформы с пассажирским вагоном и открой двери."
        },
        new TrainTaskData
        {
            taskType = TrainTaskType.CoupleCargoCar,
            title = "Грузовой вагон",
            prompt = "Подцепи жёлтый грузовой вагон."
        },
        new TrainTaskData
        {
            taskType = TrainTaskType.LoadCargo,
            title = "Погрузка",
            prompt = "Остановись у грузовой станции и загрузи товар в грузовой вагон."
        },
        new TrainTaskData
        {
            taskType = TrainTaskType.DeliverCargo,
            title = "Доставка",
            prompt = "Привези груз в депо и выгрузи его."
        },
        new TrainTaskData
        {
            taskType = TrainTaskType.TransportPassenger,
            title = "Пассажир",
            prompt = "Посади пассажира на платформе и отвези его в депо."
        },
        new TrainTaskData
        {
            taskType = TrainTaskType.OpenPassengerDoors,
            title = "Уровень 2: город",
            prompt = "Доедь до дальней станции Город с пассажирским вагоном и открой двери."
        },
        new TrainTaskData
        {
            taskType = TrainTaskType.LoadCargo,
            title = "Уровень 2: дерево",
            prompt = "Остановись у грузовой станции и загрузи дерево в грузовой вагон."
        },
        new TrainTaskData
        {
            taskType = TrainTaskType.DeliverCargo,
            title = "Уровень 2: доставка дерева",
            prompt = "Привези дерево в депо и выгрузи его."
        },
        new TrainTaskData
        {
            taskType = TrainTaskType.LoadCargo,
            title = "Уровень 2: камень",
            prompt = "Доедь до карьера и загрузи камень в грузовой вагон."
        },
        new TrainTaskData
        {
            taskType = TrainTaskType.DeliverCargo,
            title = "Уровень 2: доставка камня",
            prompt = "Привези камень в депо и выгрузи его."
        }
    };
}

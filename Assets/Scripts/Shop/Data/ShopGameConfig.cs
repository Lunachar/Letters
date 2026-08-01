using System;
using UnityEngine;

[Serializable]
public class ShopLevelSettings
{
    public string title = "Уровень";
    [Min(1)] public int questionCount = 10;
    [Min(1)] public int minProductKinds = 1;
    [Min(1)] public int maxProductKinds = 1;
    [Min(1)] public int minQuantity = 1;
    [Min(1)] public int maxQuantity = 1;
    [Min(1)] public int productPoolSize = 10;
}

[CreateAssetMenu(fileName = "ShopGameConfig", menuName = "Letters/Shop/Config")]
public class ShopGameConfig : ScriptableObject
{
    [Header("Content")]
    public ShopProductData[] startingProducts;
    public ShopLevelSettings[] levels =
    {
        new ShopLevelSettings { title = "Простые покупки", questionCount = 10, minProductKinds = 1, maxProductKinds = 1, minQuantity = 1, maxQuantity = 1, productPoolSize = 12 },
        new ShopLevelSettings { title = "Считаем продукты", questionCount = 10, minProductKinds = 1, maxProductKinds = 1, minQuantity = 1, maxQuantity = 3, productPoolSize = 18 },
        new ShopLevelSettings { title = "Большой список", questionCount = 10, minProductKinds = 2, maxProductKinds = 4, minQuantity = 1, maxQuantity = 3, productPoolSize = 28 }
    };

    [Header("Scenes")]
    public string menuSceneName = "MainMenuScene";
    public string title = "Магазин";
    public string subtitle = "Собери покупки в корзину";

    [Header("Layout")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Range(0.5f, 3f)] public float dwellSeconds = 1.1f;
    [Min(4)] public int shelfSlotCount = 8;
    public Sprite storeBackground;
    public Sprite basketSprite;

    [Header("Timing")]
    public float noActionRepeatDelay = 8f;
    public int maxQuestionRepeats = 2;
    public float correctDelay = 0.75f;
    public float wrongReturnDuration = 0.35f;
    public float resultAutoReturnDelay = 5f;

    [Header("Speech")]
    public bool useTextToSpeech = true;
    [Range(0f, 1f)] public float speechVolume = 1f;
    [Range(0.5f, 2f)] public float speechRate = 0.95f;
    [Range(0.5f, 2f)] public float speechPitch = 1f;
    public string androidLanguage = "ru_RU";

    [Header("Audio")]
    public AudioClip roomMusic;
    public AudioClip buttonClickSound;
    public AudioClip correctSound;
    public AudioClip wrongSound;
    public AudioClip celebrationSound;

    [Header("Colors")]
    public Color backgroundColor = new Color(0.12f, 0.20f, 0.22f);
    public Color shelfColor = new Color(0.47f, 0.29f, 0.16f);
    public Color basketColor = new Color(0.92f, 0.68f, 0.34f);
    public Color panelColor = new Color(0.07f, 0.12f, 0.14f, 0.92f);
    public Color primaryColor = new Color(0.96f, 0.63f, 0.22f);
    public Color correctColor = new Color(0.28f, 0.84f, 0.38f);
    public Color wrongColor = new Color(0.94f, 0.25f, 0.25f);
}

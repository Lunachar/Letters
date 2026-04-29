using UnityEngine;

[CreateAssetMenu(fileName = "StoryGameConfig", menuName = "Letters/Story Game/Config")]
public class StoryGameConfig : ScriptableObject
{
    [Header("Content")]
    public StoryCharacterData[] characters;
    public StoryTraitData[] traits;
    public StoryLocationData[] locations;
    public StoryTemplateData[] templates;

    [Header("Scenes")]
    public string menuSceneName = "MainMenuScene";

    [Header("Text")]
    public string characterSelectTitle = "Выбери героев";
    public string characterSelectHint = "Слева первый герой, справа второй герой";
    public string traitSelectTitle = "Выбери характеры";
    public string locationSelectTitle = "Где будет история?";
    public string storyTitle = "История готова";
    public string storyHint = "Можно придумать ещё одну";
    public string firstHeroLabel = "Герой 1";
    public string secondHeroLabel = "Герой 2";
    public string chooseLabel = "Выбрать";
    public string continueLabel = "Дальше";
    public string backLabel = "Назад";
    public string menuLabel = "Меню";
    public string againLabel = "Ещё история";

    [Header("Eye tracker")]
    public float dwellSeconds = 1.1f;

    [Header("Story speech")]
    public bool speakFinalStoryText = true;
    [Range(0f, 1f)] public float speechVolume = 1f;
    [Range(0.5f, 2f)] public float speechRate = 0.95f;
    [Range(0.5f, 2f)] public float speechPitch = 1f;
    public string androidLanguage = "ru_RU";

    [Header("Layout")]
    public Color backgroundColor = new Color(0.16f, 0.27f, 0.31f);
    public Color primaryButtonColor = new Color(0.95f, 0.64f, 0.25f);
    public Color panelColor = new Color(0.10f, 0.16f, 0.19f, 0.82f);
    public Color storyPanelColor = new Color(1f, 0.97f, 0.86f);
}

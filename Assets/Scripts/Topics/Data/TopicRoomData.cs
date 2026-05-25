using UnityEngine;

[CreateAssetMenu(fileName = "TopicRoom", menuName = "Letters/Topics/Room")]
public class TopicRoomData : ScriptableObject
{
    [Header("Card")]
    public string id;
    public string title;
    public string cardSymbol = "?";
    public Sprite icon;
    public Color cardColor = new Color(0.27f, 0.58f, 0.74f);

    [Header("Audio")]
    public AudioClip roomMusic;
    public AudioClip rewardMusic;
    public bool useTextToSpeech = true;
    [Range(0f, 1f)] public float speechVolume = 1f;
    [Range(0.5f, 2f)] public float speechRate = 0.95f;
    [Range(0.5f, 2f)] public float speechPitch = 1f;
    public string androidLanguage = "ru_RU";

    [Header("Intro")]
    public bool introEnabled = true;
    public bool autoStartAfterIntro;
    public TopicIntroPageData[] introPages;

    [Header("Quiz")]
    [Min(2)] public int defaultAnswersToShow = 4;
    [Min(0)] public int questionsPerRun;
    public TopicQuestionData[] questions;

    [Header("Reward")]
    public bool rewardEffectEnabled = true;
    public string rewardMessage = "Отличная работа!";
}

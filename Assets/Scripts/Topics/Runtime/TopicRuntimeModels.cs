using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TopicsSaveData
{
    public List<TopicRoomRecord> rooms = new List<TopicRoomRecord>();
    public List<TopicScoreRecord> scores = new List<TopicScoreRecord>();
}

[Serializable]
public class TopicRoomRecord
{
    public string id;
    public string title;
    public string cardSymbol;
    public string cardColorHtml;
    public string iconPath;
    public string musicPath;
    public string rewardMusicPath;
    public bool useTextToSpeech = true;
    public float speechVolume = 1f;
    public float speechRate = 0.95f;
    public float speechPitch = 1f;
    public string androidLanguage = "ru_RU";
    public bool introEnabled = true;
    public bool autoStartAfterIntro;
    public int defaultAnswersToShow = 4;
    public int questionsPerRun;
    public bool rewardEffectEnabled = true;
    public string rewardMessage = "Отличная работа!";
    public List<TopicIntroPageRecord> introPages = new List<TopicIntroPageRecord>();
    public List<TopicQuestionRecord> questions = new List<TopicQuestionRecord>();
}

[Serializable]
public class TopicIntroPageRecord
{
    public string text;
    public List<string> photoPaths = new List<string>();
    public string narrationPath;
}

[Serializable]
public class TopicQuestionRecord
{
    public string id;
    public string text;
    public string imagePath;
    public string questionSoundPath;
    public int answersToShow = 4;
    public List<TopicAnswerRecord> answers = new List<TopicAnswerRecord>();
}

[Serializable]
public class TopicAnswerRecord
{
    public string id;
    public string text;
    public int textSize = 52;
    public string textColorHtml = "#FFFFFFFF";
    public string imagePath;
    public string soundPath;
    public bool isCorrect;
}

[Serializable]
public class TopicScoreRecord
{
    public string roomId;
    public int bestCorrect;
    public int bestTotal;
    public string bestDateUtc;
}

public class TopicRoomRuntime
{
    public string id;
    public string title;
    public string cardSymbol;
    public Sprite icon;
    public string iconPath;
    public Color cardColor;
    public AudioClip roomMusic;
    public string musicPath;
    public AudioClip rewardMusic;
    public string rewardMusicPath;
    public bool useTextToSpeech;
    public float speechVolume;
    public float speechRate;
    public float speechPitch;
    public string androidLanguage;
    public bool introEnabled;
    public bool autoStartAfterIntro;
    public int defaultAnswersToShow;
    public int questionsPerRun;
    public bool rewardEffectEnabled;
    public string rewardMessage;
    public List<TopicIntroPageRuntime> introPages = new List<TopicIntroPageRuntime>();
    public List<TopicQuestionRuntime> questions = new List<TopicQuestionRuntime>();
    public bool isUserCreated;
}

public class TopicIntroPageRuntime
{
    public string text;
    public List<Sprite> photos = new List<Sprite>();
    public List<string> photoPaths = new List<string>();
    public AudioClip narration;
    public string narrationPath;
}

public class TopicQuestionRuntime
{
    public string id;
    public string text;
    public Sprite image;
    public string imagePath;
    public AudioClip questionSound;
    public string questionSoundPath;
    public int answersToShow;
    public List<TopicAnswerRuntime> answers = new List<TopicAnswerRuntime>();
}

public class TopicAnswerRuntime
{
    public string id;
    public string text;
    public int textSize;
    public Color textColor;
    public Sprite image;
    public string imagePath;
    public AudioClip sound;
    public string soundPath;
    public bool isCorrect;
}

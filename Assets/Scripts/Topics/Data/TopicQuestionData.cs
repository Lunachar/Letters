using System;
using UnityEngine;

[Serializable]
public class TopicQuestionData
{
    public string id = Guid.NewGuid().ToString("N");
    [TextArea(2, 5)] public string text;
    public Sprite image;
    public AudioClip questionSound;
    [Min(2)] public int answersToShow = 4;
    public TopicAnswerData[] answers;
}

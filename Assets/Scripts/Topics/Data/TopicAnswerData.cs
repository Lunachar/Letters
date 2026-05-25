using System;
using UnityEngine;

[Serializable]
public class TopicAnswerData
{
    public string id = Guid.NewGuid().ToString("N");
    [TextArea(1, 3)] public string text;
    public int textSize = 52;
    public Color textColor = Color.white;
    public Sprite image;
    public AudioClip sound;
    public bool isCorrect;
}

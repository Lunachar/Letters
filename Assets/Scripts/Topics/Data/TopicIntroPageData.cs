using System;
using UnityEngine;

[Serializable]
public class TopicIntroPageData
{
    [TextArea(3, 8)] public string text;
    public Sprite[] photos;
    public AudioClip narration;
}

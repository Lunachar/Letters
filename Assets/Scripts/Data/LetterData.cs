using UnityEngine;

[CreateAssetMenu(fileName = "LetterData", menuName = "Letters/Letter Data")]
public class LetterData : ScriptableObject
{
    [Header("Letter Information")]
    public char letter;
    public string letterName;
    
    [Header("Visual Assets")]
    public Sprite letterSprite;
    public Sprite objectSprite;
    
    [Header("Audio")]
    public AudioClip letterSound;
    public AudioClip objectSound;
    
    [Header("Statistics")]
    public int timesPressed;
    
    public void IncrementPressCount()
    {
        timesPressed++;
    }
} 
using UnityEngine;

[CreateAssetMenu(fileName = "StoryCharacter", menuName = "Letters/Story Game/Character")]
public class StoryCharacterData : ScriptableObject
{
    [Header("Display")]
    public string displayName;
    public Sprite portrait;
    public Color cardColor = new Color(0.88f, 0.48f, 0.26f);

    [Header("Russian grammar")]
    public bool isMasculine = true;
    public string accusativeName;
    public string dativeName;
    public string travelPhrase;
    public string appearedPhrase;
}

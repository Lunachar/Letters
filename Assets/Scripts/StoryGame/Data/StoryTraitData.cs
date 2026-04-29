using UnityEngine;

[CreateAssetMenu(fileName = "StoryTrait", menuName = "Letters/Story Game/Trait")]
public class StoryTraitData : ScriptableObject
{
    public string tagId;
    public string feminineName;
    public string masculineName;
    public Color cardColor = new Color(0.34f, 0.59f, 0.76f);

    public string GetFor(StoryCharacterData character)
    {
        if (character != null && character.isMasculine)
        {
            return masculineName;
        }

        return feminineName;
    }
}

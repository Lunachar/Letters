using UnityEngine;

[CreateAssetMenu(fileName = "StoryTemplate", menuName = "Letters/Story Game/Story Template")]
public class StoryTemplateData : ScriptableObject
{
    [Tooltip("Use * to match any trait.")]
    public string firstTraitTag = "*";

    [Tooltip("Use * to match any trait.")]
    public string secondTraitTag = "*";

    [Tooltip("Optional. Use * or leave empty to match any location.")]
    public string locationTag = "*";

    [Tooltip("When true, template is used only if both selected characters are the same asset.")]
    public bool sameCharacterOnly;

    [TextArea(5, 12)]
    public string text;

    public bool Matches(StoryTraitData firstTrait, StoryTraitData secondTrait, StoryLocationData location, bool sameCharacter)
    {
        if (sameCharacterOnly && !sameCharacter)
        {
            return false;
        }

        return MatchesTag(firstTraitTag, firstTrait != null ? firstTrait.tagId : null)
            && MatchesTag(secondTraitTag, secondTrait != null ? secondTrait.tagId : null)
            && MatchesTag(locationTag, location != null ? location.tagId : null);
    }

    private bool MatchesTag(string templateTag, string value)
    {
        return string.IsNullOrEmpty(templateTag) || templateTag == "*" || templateTag == value;
    }
}

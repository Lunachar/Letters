using UnityEngine;

[CreateAssetMenu(fileName = "StoryLocation", menuName = "Letters/Story Game/Location")]
public class StoryLocationData : ScriptableObject
{
    public string displayName;
    public string destinationName;
    public string treasureName;
    public string tagId;
    public Color backgroundColor = new Color(0.16f, 0.27f, 0.31f);
    public Color cardColor = new Color(0.28f, 0.52f, 0.58f);
    public Sprite backgroundImage;
    public AudioClip music;
}

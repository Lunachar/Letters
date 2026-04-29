using UnityEngine;

[CreateAssetMenu(fileName = "MenuGameEntry", menuName = "Letters/Menu/Game Entry")]
public class MenuGameEntry : ScriptableObject
{
    public string title;
    public string subtitle;
    public string sceneName;
    public Color buttonColor = new Color(0.95f, 0.64f, 0.25f);
    public Sprite icon;
}

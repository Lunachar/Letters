using UnityEngine;

[CreateAssetMenu(fileName = "MainMenuConfig", menuName = "Letters/Menu/Config")]
public class MainMenuConfig : ScriptableObject
{
    public string title = "Letters";
    public string subtitle = "Выбери игру";
    public Color backgroundColor = new Color(0.13f, 0.22f, 0.29f);
    public float dwellSeconds = 1.1f;
    public MenuGameEntry[] games;
}

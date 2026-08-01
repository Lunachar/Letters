using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ShopProduct", menuName = "Letters/Shop/Product")]
public class ShopProductData : ScriptableObject
{
    public string id = Guid.NewGuid().ToString("N");
    public string displayName;
    public string spokenName;
    public string countTwoFourName;
    public string countManyName;
    public Sprite icon;
    public Color cardColor = new Color(0.94f, 0.63f, 0.28f);
    public bool active = true;
}

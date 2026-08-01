using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ShopSaveData
{
    public List<ShopProductRecord> products = new List<ShopProductRecord>();
}

[Serializable]
public class ShopProductRecord
{
    public string id;
    public string displayName;
    public string spokenName;
    public string countTwoFourName;
    public string countManyName;
    public string iconPath;
    public string cardColorHtml = "#F0A047FF";
    public bool active = true;
}

public class ShopProductRuntime
{
    public string id;
    public string displayName;
    public string spokenName;
    public string countTwoFourName;
    public string countManyName;
    public Sprite icon;
    public string iconPath;
    public Color cardColor;
    public bool active;
    public bool isUserCreated;
}

public class ShopTaskItem
{
    public ShopProductRuntime product;
    public int quantity;
}

public class ShopTask
{
    public readonly List<ShopTaskItem> items = new List<ShopTaskItem>();
    public readonly Dictionary<string, int> remaining = new Dictionary<string, int>();
    public string prompt;

    public int TotalCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                total += items[i].quantity;
            }
            return total;
        }
    }

    public bool IsComplete
    {
        get
        {
            foreach (KeyValuePair<string, int> pair in remaining)
            {
                if (pair.Value > 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

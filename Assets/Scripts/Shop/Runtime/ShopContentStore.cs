using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ShopContentStore
{
    private const string SaveFileName = "shop-save.json";

    private readonly ShopGameConfig config;
    private readonly string savePath;
    private ShopSaveData saveData;

    public ShopContentStore(ShopGameConfig config)
    {
        this.config = config;
        savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
        Load();
    }

    public List<ShopProductRuntime> GetProducts()
    {
        List<ShopProductRuntime> products = new List<ShopProductRuntime>();
        Dictionary<string, int> indexById = new Dictionary<string, int>();

        if (config != null && config.startingProducts != null)
        {
            foreach (ShopProductData product in config.startingProducts)
            {
                if (product != null)
                {
                    ShopProductRuntime runtime = FromAsset(product);
                    indexById[runtime.id] = products.Count;
                    products.Add(runtime);
                }
            }
        }

        foreach (ShopProductRecord record in saveData.products)
        {
            if (!string.IsNullOrEmpty(record.id) && indexById.ContainsKey(record.id))
            {
                ApplyRecord(products[indexById[record.id]], record);
            }
            else
            {
                products.Add(FromRecord(record));
            }
        }

        return products;
    }

    public ShopProductRuntime CreateEmptyProduct()
    {
        return new ShopProductRuntime
        {
            id = Guid.NewGuid().ToString("N"),
            displayName = "Новый продукт",
            spokenName = "новый продукт",
            countTwoFourName = "новых продукта",
            countManyName = "новых продуктов",
            cardColor = new Color(0.92f, 0.58f, 0.26f),
            active = true,
            isUserCreated = true
        };
    }

    public void UpsertUserProduct(ShopProductRuntime product)
    {
        ShopProductRecord record = ToRecord(product);
        int index = saveData.products.FindIndex(item => item.id == product.id);
        if (index >= 0)
        {
            saveData.products[index] = record;
        }
        else
        {
            saveData.products.Add(record);
        }

        Save();
    }

    public void DeleteUserProduct(string id)
    {
        saveData.products.RemoveAll(product => product.id == id);
        Save();
    }

    public static string CopyToMediaFolder(string sourcePath, string subfolder)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        string mediaFolder = Path.Combine(Application.persistentDataPath, "ShopMedia", subfolder);
        Directory.CreateDirectory(mediaFolder);
        string destination = Path.Combine(mediaFolder, Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath));
        File.Copy(sourcePath, destination, true);
        return destination;
    }

    private void Load()
    {
        if (!File.Exists(savePath))
        {
            saveData = new ShopSaveData();
            return;
        }

        try
        {
            saveData = JsonUtility.FromJson<ShopSaveData>(File.ReadAllText(savePath)) ?? new ShopSaveData();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("ShopContentStore: cannot load save file: " + exception.Message);
            saveData = new ShopSaveData();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(savePath));
        File.WriteAllText(savePath, JsonUtility.ToJson(saveData, true));
    }

    private static ShopProductRuntime FromAsset(ShopProductData asset)
    {
        return new ShopProductRuntime
        {
            id = string.IsNullOrEmpty(asset.id) ? asset.name : asset.id,
            displayName = asset.displayName,
            spokenName = string.IsNullOrEmpty(asset.spokenName) ? asset.displayName : asset.spokenName,
            countTwoFourName = string.IsNullOrEmpty(asset.countTwoFourName) ? asset.displayName : asset.countTwoFourName,
            countManyName = string.IsNullOrEmpty(asset.countManyName) ? asset.displayName : asset.countManyName,
            icon = asset.icon,
            cardColor = asset.cardColor,
            active = asset.active,
            isUserCreated = false
        };
    }

    private static ShopProductRuntime FromRecord(ShopProductRecord record)
    {
        Color cardColor = new Color(0.92f, 0.58f, 0.26f);
        ColorUtility.TryParseHtmlString(record.cardColorHtml, out cardColor);

        return new ShopProductRuntime
        {
            id = record.id,
            displayName = record.displayName,
            spokenName = record.spokenName,
            countTwoFourName = record.countTwoFourName,
            countManyName = record.countManyName,
            iconPath = record.iconPath,
            icon = TopicsContentStore.LoadSprite(record.iconPath),
            cardColor = cardColor,
            active = record.active,
            isUserCreated = true
        };
    }

    private static void ApplyRecord(ShopProductRuntime product, ShopProductRecord record)
    {
        Color cardColor = product.cardColor;
        ColorUtility.TryParseHtmlString(record.cardColorHtml, out cardColor);

        product.displayName = string.IsNullOrEmpty(record.displayName) ? product.displayName : record.displayName;
        product.spokenName = string.IsNullOrEmpty(record.spokenName) ? product.displayName : record.spokenName;
        product.countTwoFourName = string.IsNullOrEmpty(record.countTwoFourName) ? product.spokenName : record.countTwoFourName;
        product.countManyName = string.IsNullOrEmpty(record.countManyName) ? product.countTwoFourName : record.countManyName;
        product.iconPath = record.iconPath;
        if (!string.IsNullOrEmpty(record.iconPath))
        {
            Sprite loadedIcon = TopicsContentStore.LoadSprite(record.iconPath);
            if (loadedIcon != null)
            {
                product.icon = loadedIcon;
            }
        }
        product.cardColor = cardColor;
        product.active = record.active;
    }

    private static ShopProductRecord ToRecord(ShopProductRuntime product)
    {
        return new ShopProductRecord
        {
            id = product.id,
            displayName = product.displayName,
            spokenName = product.spokenName,
            countTwoFourName = product.countTwoFourName,
            countManyName = product.countManyName,
            iconPath = product.iconPath,
            cardColorHtml = "#" + ColorUtility.ToHtmlStringRGBA(product.cardColor),
            active = product.active
        };
    }
}

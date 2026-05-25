using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class TopicsFilePicker
{
    public static void PickImage(Action<string> onPicked)
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Выбери картинку", "", "png,jpg,jpeg");
        onPicked?.Invoke(string.IsNullOrEmpty(path) ? null : path);
#else
        onPicked?.Invoke(null);
#endif
    }

    public static void PickAudio(Action<string> onPicked)
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Выбери звук", "", "wav,mp3,ogg");
        onPicked?.Invoke(string.IsNullOrEmpty(path) ? null : path);
#else
        onPicked?.Invoke(null);
#endif
    }

    public static string CopyToMediaFolder(string sourcePath, string subfolder)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        string mediaFolder = Path.Combine(Application.persistentDataPath, "TopicsMedia", subfolder);
        Directory.CreateDirectory(mediaFolder);

        string extension = Path.GetExtension(sourcePath);
        string fileName = Guid.NewGuid().ToString("N") + extension;
        string destination = Path.Combine(mediaFolder, fileName);
        File.Copy(sourcePath, destination, true);
        return destination;
    }
}

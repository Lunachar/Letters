#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TopicsEditorMenu
{
    private const string ConfigPath = "Assets/Data/Topics/TopicsGameConfig.asset";
    private const string RoomsFolder = "Assets/Data/Topics/Rooms";

    [MenuItem("Letters/Topics/Open Config")]
    public static void OpenConfig()
    {
        Object config = AssetDatabase.LoadAssetAtPath<Object>(ConfigPath);
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }

    [MenuItem("Letters/Topics/Open Rooms Folder")]
    public static void OpenRoomsFolder()
    {
        Object folder = AssetDatabase.LoadAssetAtPath<Object>(RoomsFolder);
        Selection.activeObject = folder;
        EditorGUIUtility.PingObject(folder);
    }
}
#endif

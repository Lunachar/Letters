using UnityEngine;

public class TopicsSoftLock : MonoBehaviour
{
    [SerializeField] private bool forceFullscreen = true;
    [SerializeField] private bool hideAndroidSystemUi = true;

    private void Awake()
    {
        Apply();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            Apply();
        }
    }

    private void Apply()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        if (forceFullscreen)
        {
            Screen.fullScreen = true;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (hideAndroidSystemUi)
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow"))
            using (AndroidJavaObject decorView = window.Call<AndroidJavaObject>("getDecorView"))
            {
                const int immersiveSticky = 4096;
                const int fullscreen = 4;
                const int hideNavigation = 2;
                const int layoutFullscreen = 1024;
                const int layoutHideNavigation = 512;
                const int layoutStable = 256;
                decorView.Call("setSystemUiVisibility", immersiveSticky | fullscreen | hideNavigation | layoutFullscreen | layoutHideNavigation | layoutStable);
            }
        }
#endif
    }
}

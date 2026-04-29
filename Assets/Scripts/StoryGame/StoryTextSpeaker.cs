using UnityEngine;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Diagnostics;
using System.Text;
#endif

public class StoryTextSpeaker : MonoBehaviour
{
    [SerializeField] private bool stopPreviousSpeech = true;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private Process speechProcess;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject textToSpeech;
    private bool androidReady;
    private string pendingText;
    private float pendingVolume;
    private float pendingRate;
    private float pendingPitch;
#endif

    public void Speak(string text, StoryGameConfig config)
    {
        if (string.IsNullOrWhiteSpace(text) || config == null || !config.speakFinalStoryText)
        {
            return;
        }

        Speak(text, config.speechVolume, config.speechRate, config.speechPitch, config.androidLanguage);
    }

    public void Speak(string text, float volume, float rate, float pitch, string androidLanguage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (stopPreviousSpeech)
        {
            Stop();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        SpeakAndroid(text, volume, rate, pitch, androidLanguage);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        SpeakWindows(text, volume, rate);
#else
        Debug.Log("StoryTextSpeaker: speech is not implemented on this platform.");
#endif
    }

    public void Stop()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        pendingText = null;
        if (textToSpeech != null)
        {
            textToSpeech.Call<int>("stop");
        }
#endif

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (speechProcess != null && !speechProcess.HasExited)
        {
            speechProcess.Kill();
        }

        speechProcess?.Dispose();
        speechProcess = null;
#endif
    }

    private void OnDestroy()
    {
        Stop();

#if UNITY_ANDROID && !UNITY_EDITOR
        if (textToSpeech != null)
        {
            textToSpeech.Call("shutdown");
            textToSpeech.Dispose();
            textToSpeech = null;
        }
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private void SpeakWindows(string text, float volume, float rate)
    {
        int sapiVolume = Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f);
        int sapiRate = Mathf.RoundToInt(Mathf.Lerp(-3f, 3f, Mathf.InverseLerp(0.5f, 2f, rate)));
        string escapedText = text.Replace("'", "''");

        string script =
            "Add-Type -AssemblyName System.Speech; " +
            "$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
            "$speaker.Volume = " + sapiVolume + "; " +
            "$speaker.Rate = " + sapiRate + "; " +
            "$speaker.Speak('" + escapedText + "'); " +
            "$speaker.Dispose();";

        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded,
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        speechProcess = Process.Start(startInfo);
    }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    private void SpeakAndroid(string text, float volume, float rate, float pitch, string androidLanguage)
    {
        pendingText = text;
        pendingVolume = volume;
        pendingRate = rate;
        pendingPitch = pitch;

        if (textToSpeech == null)
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                textToSpeech = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, new TtsInitListener(this, androidLanguage));
            }
            return;
        }

        if (androidReady)
        {
            SpeakPendingAndroid();
        }
    }

    private void HandleAndroidTtsReady(string languageCode)
    {
        androidReady = true;

        if (textToSpeech != null)
        {
            string[] parts = string.IsNullOrEmpty(languageCode) ? new[] { "ru", "RU" } : languageCode.Split('_');
            string language = parts.Length > 0 ? parts[0] : "ru";
            string country = parts.Length > 1 ? parts[1] : "RU";
            using (AndroidJavaObject locale = new AndroidJavaObject("java.util.Locale", language, country))
            {
                textToSpeech.Call<int>("setLanguage", locale);
            }
        }

        SpeakPendingAndroid();
    }

    private void SpeakPendingAndroid()
    {
        if (textToSpeech == null || string.IsNullOrEmpty(pendingText))
        {
            return;
        }

        textToSpeech.Call<int>("setSpeechRate", pendingRate);
        textToSpeech.Call<int>("setPitch", pendingPitch);

        using (AndroidJavaObject bundle = new AndroidJavaObject("android.os.Bundle"))
        {
            bundle.Call("putFloat", "volume", Mathf.Clamp01(pendingVolume));
            textToSpeech.Call<int>("speak", pendingText, 0, bundle, "story_text");
        }

        pendingText = null;
    }

    private class TtsInitListener : AndroidJavaProxy
    {
        private readonly StoryTextSpeaker speaker;
        private readonly string languageCode;

        public TtsInitListener(StoryTextSpeaker speaker, string languageCode)
            : base("android.speech.tts.TextToSpeech$OnInitListener")
        {
            this.speaker = speaker;
            this.languageCode = languageCode;
        }

        public void onInit(int status)
        {
            if (status == 0)
            {
                speaker.HandleAndroidTtsReady(languageCode);
            }
        }
    }
#endif
}

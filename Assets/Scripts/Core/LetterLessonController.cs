using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LetterLessonController : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI letterDisplay;
    public Slider lessonProgressSlider;
    public TextMeshProUGUI lessonTitle;
    public GameObject lessonUI;
    
    [Header("Audio & Effects")]
    public AudioSource audioSource;
    public AudioClip correctSound;
    public AudioClip lessonCompleteSound;

    private List<char> currentSequence;
    private int currentStep;
    private int currentLessonIndex;
    
    private LessonManager lessonManager;

    // private void Start()
    // {
    //     
    //     StartLesson(currentLessonIndex);
    // }
    

    

    public void StartLesson()
    {
        lessonManager = FindObjectOfType<LessonManager>();
        LoadProgress();
        var lesson = LessonManager.Instance.CurrentLesson;
        if (lesson == null)
        {
            Debug.Log("All lessons complete or invalid index.");
            return;
        }
        
        //currentLessonIndex;
        currentSequence = new List<char>(lesson.GetSequence());
        currentStep = 0;
        
        lessonUI.SetActive(true);
        lessonTitle.text = lesson.Title;
        lessonProgressSlider.maxValue = currentSequence.Count;
        lessonProgressSlider.value = 0;
        
        ShowNextLetter();
    }

    public void OnKeyPressed(char input)
    {
        if (currentStep >= currentSequence.Count)
        {
            return;
        }

        if (char.ToUpperInvariant(input) == char.ToUpperInvariant(currentSequence[currentStep]))
        {
            audioSource.PlayOneShot(correctSound);
            currentStep++;
            lessonProgressSlider.value = currentStep;

            if (currentStep < currentSequence.Count)
            {
                ShowNextLetter();
            }
            else
            {
                OnLessonComplete();
            }
        }
    }

    private void ShowNextLetter()
    {
        letterDisplay.text = currentSequence[currentStep].ToString().ToUpper();
    }

    private void LoadProgress()
    {
        currentLessonIndex = PlayerPrefs.GetInt("LessonProgress", 0);
    }
    
    private void OnLessonComplete()
    {
        audioSource.PlayOneShot(lessonCompleteSound);
        PlayerPrefs.SetInt("LessonProgress", currentLessonIndex + 1);
        PlayerPrefs.Save();
    }

    public void ResetProgress()
    {
        PlayerPrefs.DeleteKey("LessonProgress");
        PlayerPrefs.Save();    
        currentLessonIndex = 0;
    }

    public void StartNextLesson()
    {
        currentLessonIndex++;
        StartLesson();
    }
}

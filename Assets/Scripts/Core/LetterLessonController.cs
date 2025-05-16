using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LetterLessonController : MonoBehaviour
{
    [Header("UI")]
    public GameObject letterPrefab;
    public Transform letterQueueParent;
    public RectTransform[] spawnPoints = new RectTransform[3];
    public Slider lessonProgressSlider;
    public TextMeshProUGUI lessonTitle;
    public GameObject lessonUI;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip correctSound;
    public AudioClip lessonCompleteSound;

    private Queue<char> letterQueue = new();
    private List<GameObject> letterObjects = new();
    private List<char> fullSequence;
    private int currentStep = 0;

    private LessonManager lessonManager;

    private void Start()
    {
        lessonManager = FindObjectOfType<LessonManager>();
    }

    private void Update()
    {
        if (!lessonUI.activeSelf) return;

        foreach (char c in Input.inputString)
        {
            if (char.IsLetter(c))
            {
                OnKeyPressed(c);
            }
        }
    }

    public void StartLesson()
    {
        lessonManager = LessonManager.Instance;
        lessonManager.LoadProgress();
        lessonTitle.text = lessonManager.CurrentLesson.Title;

        var lesson = lessonManager.CurrentLesson;
        if (lesson == null)
        {
            Debug.Log("All lessons complete.");
            lessonUI.SetActive(false);
            return;
        }

        fullSequence = new List<char>(lesson.GetSequence());
        currentStep = 0;

        lessonUI.SetActive(true);
        lessonTitle.text = lesson.Title;
        lessonProgressSlider.maxValue = fullSequence.Count;
        lessonProgressSlider.value = 0;

        ClearLetterObjects();
        letterQueue.Clear();

        for (int i = 0; i < 3 && i < fullSequence.Count; i++)
        {
            char c = fullSequence[i];
            letterQueue.Enqueue(c);
            CreateLetterVisual(c);
        }

        UpdateLetterPositions();
    }

    public void OnKeyPressed(char input)
    {
        if (letterQueue.Count == 0) return;

        char current = letterQueue.Peek();

        if (char.ToUpperInvariant(input) == char.ToUpperInvariant(current))
        {
            audioSource.PlayOneShot(correctSound);
            currentStep++;
            lessonProgressSlider.value = currentStep;

            letterQueue.Dequeue();
            Destroy(letterObjects[0]);
            letterObjects.RemoveAt(0);

            if (currentStep + letterQueue.Count < fullSequence.Count)
            {
                char newChar = fullSequence[currentStep + letterQueue.Count];
                letterQueue.Enqueue(newChar);
                CreateLetterVisual(newChar);
            }

            UpdateLetterPositions();

            if (currentStep >= fullSequence.Count)
            {
                OnLessonComplete();
            }
        }
    }

    private void CreateLetterVisual(char letter)
    {
        GameObject obj = Instantiate(letterPrefab, Vector3.zero, Quaternion.identity, letterQueueParent);
        TMP_Text text = obj.GetComponentInChildren<TMP_Text>();
        text.text = letter.ToString().ToUpperInvariant();
        letterObjects.Add(obj);
    }

    private void UpdateLetterPositions()
    {
        for (int i = 0; i < letterObjects.Count && i < spawnPoints.Length; i++)
        {
            RectTransform rt = letterObjects[i].GetComponent<RectTransform>();
            rt.anchoredPosition = spawnPoints[i].anchoredPosition;

            SpawnPointSettings settings = spawnPoints[i].GetComponent<SpawnPointSettings>();
            if (settings != null)
                settings.ApplyTo(letterObjects[i]);
        }
    }

    private void ClearLetterObjects()
    {
        foreach (var obj in letterObjects)
            Destroy(obj);

        letterObjects.Clear();
    }

    private void OnLessonComplete()
    {
        audioSource.PlayOneShot(lessonCompleteSound);
        StartCoroutine(CompleteAndAdvanceLesson());
    }

    private IEnumerator CompleteAndAdvanceLesson()
    {
        yield return new WaitForSeconds(1f);

        lessonManager.AdvanceLesson();

        if (lessonManager.CurrentLesson != null)
        {
            StartLesson();
        }
        else
        {
            Debug.Log("All lessons complete!");
            lessonUI.SetActive(false);
        }
    }

    public void ResetProgress()
    {
        lessonManager.ResetProgress();
    }
}

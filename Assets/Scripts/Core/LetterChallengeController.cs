using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Random = UnityEngine.Random;

public class LetterChallengeController : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform letterQueueParent;
    [SerializeField] private GameObject letterPrefab;
    [SerializeField] private RectTransform[] spawnPoints = new RectTransform[3];

    private List<LetterData> letterPool;
    private List<GameObject> letterObjects = new();
    private Queue<LetterData> letterQueue = new();

    private LetterData currentLetter;
    private bool awaitingInput;
    private Coroutine repeatRoutine;
    private Coroutine nextLetterRoutine;
    private bool isActive = false;
    private bool isSpeaking = false;
    
    private readonly int[] fontSizes = {180, 90, 75};

    public void StartChallenge(List<LetterData> letters)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        letterPool = letters;
        isActive = true;
        ResetQueue();
        currentLetter = letterQueue.Peek();
        awaitingInput = true;
        repeatRoutine = StartCoroutine(RepeatTaskRoutine());
    }


    private void ResetQueue()
    {
        foreach (var obj in letterObjects)
            Destroy(obj);

        letterObjects.Clear();
        letterQueue.Clear();

        for (int i = 0; i < 3; i++)
        {
            var letter = GetRandomLetter();
            letterQueue.Enqueue(letter);
            AddLetterVisual(letter);
        }
        
        UpdateLetterPosition();
    }
    
    private LetterData GetRandomLetter()
    {
        return letterPool[Random.Range(0, letterPool.Count)];
    }
    
    private void AddLetterVisual(LetterData data)
    {
        GameObject obj = Instantiate(letterPrefab, Vector3.zero, Quaternion.identity, letterQueueParent);
        TMP_Text text = obj.GetComponentInChildren<TMP_Text>();
        text.text = data.letter.ToString().ToUpperInvariant();
        letterObjects.Add(obj);
    }
    
    private void UpdateLetterPosition()
    {
        for (int i = 0; i < letterObjects.Count && i < spawnPoints.Length; i++)
        {
            RectTransform rt = letterObjects[i].GetComponent<RectTransform>();
            rt.anchoredPosition = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = spawnPoints[i].anchoredPosition;

            SpawnPointSettings settings = spawnPoints[i].GetComponent<SpawnPointSettings>();
            if (settings != null)
            {
                settings.ApplyTo(letterObjects[i]);
            }
        }
    }

    private IEnumerator RepeatTaskRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        SoundManager.Instance?.PlayTaskSound();
        yield return new WaitForSeconds(1f);

        while (isActive)
        {
            yield return StartCoroutine(PlayCurrentLetter());

            float timer = 0f;
            while (timer < 4f && awaitingInput)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            while (awaitingInput)
            {
                yield return StartCoroutine(PlayCurrentLetter());
                float interval = 8f;
                float elapsed = 0f;
                while (elapsed < interval && awaitingInput)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }
    }

    private IEnumerator PlayCurrentLetter()
    {
        isSpeaking = true;
        yield return new WaitForSeconds(0.3f);
        audioSource.PlayOneShot(currentLetter.letterSound);
        yield return new WaitForSeconds(currentLetter.letterSound.length + 0.1f);
        isSpeaking = false;
    }

    private void Update()
    {
        if (!isActive || !awaitingInput || isSpeaking) return;

        foreach (char c in Input.inputString)
        {
            if (char.IsLetter(c))
            {
                char inputChar = char.ToUpperInvariant(c);
                char targetChar = char.ToUpperInvariant(currentLetter.letter);

                if (inputChar == targetChar)
                {
                    awaitingInput = false;
                    StartCoroutine(DelayedPraise(0.4f));
                    if (repeatRoutine!=null)
                    {
                        StopCoroutine(repeatRoutine);
                    }
                    
                    nextLetterRoutine = StartCoroutine(NextLetter());
                }
                else
                {
                    SoundManager.Instance?.PlayErrorSound();
                }
                
                break;
            }
        }
    }

    private IEnumerator DelayedPraise(float f)
    {
        yield return new WaitForSeconds(f);
        SoundManager.Instance?.PlayPraiseSound();
    }

    private IEnumerator NextLetter()
    {
        yield return new WaitForSeconds(1f);

        if (letterObjects.Count > 0)
        {
            Destroy(letterObjects[0]);
            letterObjects.RemoveAt(0);
        }

        if (letterQueue.Count > 0)
        {
            letterQueue.Dequeue();
        }

        var newLetter = GetRandomLetter();
        letterQueue.Enqueue(newLetter);
        AddLetterVisual(newLetter);
        UpdateLetterPosition();

        currentLetter = letterQueue.Peek();
        awaitingInput = true;
        repeatRoutine = StartCoroutine(RepeatTaskRoutine());
    }

    public void StopChallenge()
    {
        isActive = false;
        awaitingInput = false;
        currentLetter = null;

        if (repeatRoutine != null)
        {
            StopCoroutine(repeatRoutine);
        }

        if (nextLetterRoutine != null)
        {
            StopCoroutine(nextLetterRoutine);
        }

        foreach (var obj in letterObjects)
            Destroy(obj);

        letterObjects.Clear();
        letterQueue.Clear();
    }
}

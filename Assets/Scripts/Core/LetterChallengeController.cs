using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class LetterChallengeController : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform letterQueueParent;
    [SerializeField] private float delayBetweenLetters = 0.5f;
    [SerializeField] private float spacing = 50f;
    [SerializeField] private Transform[] spawnPoints = new Transform[3];

    private List<LetterData> letterPool;
    private List<GameObject> letterObjects = new();
    private Queue<LetterData> letterQueue = new();

    private LetterData currentLetter;
    private float[] scales = { 1.0f, 0.6f, 0.4f };
    private bool awaitingInput;
    private Coroutine currentRoutine;
    private bool isActive = false;

    public void StartChallenge(List<LetterData> letters)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        letterPool = letters;
        isActive = true;
        ResetQueue();
        currentLetter = letterQueue.Dequeue();
        awaitingInput = true;
        currentRoutine = StartCoroutine(PlayFirstLetterIntro());
    }

    private IEnumerator PlayFirstLetterIntro()
    {
        yield return new WaitForSeconds(0.6f);
        SoundManager.Instance?.PlayTaskSound();
        
        yield return new WaitForSeconds(0.4f);
        audioSource.PlayOneShot(currentLetter.letterSound);
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
            AddLetterVisual(letter, i);
        }

        AnimateQueueShift();
    }

    private void AddLetterVisual(LetterData data, int index)
    {
        GameObject obj = new GameObject("LetterVisual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(letterQueueParent, false);
        obj.transform.localScale = Vector3.one * scales[index];

        var img = obj.GetComponent<Image>();
        img.sprite = data.letterSprite;
        img.SetNativeSize();

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.position = spawnPoints[index].position;
        
        obj.transform.SetAsLastSibling();

        letterObjects.Add(obj);

        rt.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
    }

    private void AnimateQueueShift()
    {
        for (int i = 0; i < letterObjects.Count; i++)
        {
            RectTransform rt = letterObjects[i].GetComponent<RectTransform>();
            float targetX = i * spacing;

            rt.DOAnchorPosX(targetX, 0.4f).SetEase(Ease.InOutSine);
            rt.DOScale(Vector3.one * scales[i], 0.4f).SetEase(Ease.InOutSine);
        }
    }

    private IEnumerator NextLetter()
    {
        yield return new WaitForSeconds(delayBetweenLetters);

        currentLetter = letterQueue.Dequeue();
        awaitingInput = true;

        if (letterObjects.Count > 0)
        {
            Destroy(letterObjects[0]);
            letterObjects.RemoveAt(0);
        }

        for (int i = 0; i < letterObjects.Count; i++)
        {
            letterObjects[i].transform.DOMove(spawnPoints[i].position, 0.4f).SetEase(Ease.InOutSine);
            letterObjects[i].transform.DOScale(Vector3.one * scales[i], 0.4f).SetEase(Ease.InOutSine);
        }

        LetterData newLetter = GetRandomLetter();
        letterQueue.Enqueue(newLetter);
        AddLetterVisual(newLetter, letterObjects.Count);
        
        yield return new WaitForSeconds(1f);
        SoundManager.Instance?.PlayTaskSound();

        yield return new WaitForSeconds(1f);
        audioSource.PlayOneShot(currentLetter.letterSound);
    }

    private LetterData GetRandomLetter()
    {
        return letterPool[UnityEngine.Random.Range(0, letterPool.Count)];
    }

    private void OnGUI()
    {
        if (!isActive || !awaitingInput) return;

        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.character != '\0')
        {
            char inputChar = char.ToUpperInvariant(e.character);
            char targetChar = char.ToUpperInvariant(currentLetter.letter);

            if (inputChar == targetChar)
            {
                awaitingInput = false;
                StartCoroutine(PlayPraiseThenNext());
            }
        }
    }

    private IEnumerator PlayPraiseThenNext()
    {
        yield return new WaitForSeconds(1f);
        SoundManager.Instance?.PlayPraiseSound();

        yield return new WaitForSeconds(2f);
        currentRoutine = StartCoroutine(NextLetter());
    }

    public void StopChallenge()
    {
        isActive = false;

        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        foreach (var obj in letterObjects)
            Destroy(obj);

        letterObjects.Clear();
        letterQueue.Clear();
        awaitingInput = false;
        currentLetter = null;
    }
}

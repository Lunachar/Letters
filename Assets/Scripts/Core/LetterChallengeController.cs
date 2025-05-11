using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LetterChallengeController : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GameObject letterVisualPrefab;
    [SerializeField] private Transform letterSpawnPoint;
    [SerializeField] private float delayBetweenLetters = 2.0f;

    private List<LetterData> letterPool;
    private LetterData currentLetter;
    private bool awaitingInput;
    
    private Coroutine currentRoutine;
    private GameObject currentLetterGO;


    public void StartChallenge(List<LetterData> letters)
    {
        letterPool = letters;
        currentRoutine = StartCoroutine(NextLetter());
    }

    private IEnumerator NextLetter()
    {
        yield return new WaitForSeconds(delayBetweenLetters);
        currentLetter = GetRandomLetter();
        awaitingInput = true;

        currentLetterGO = Instantiate(letterVisualPrefab, letterSpawnPoint.position, Quaternion.identity, letterSpawnPoint);
        currentLetterGO.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = currentLetter.letter.ToString();
        
        SoundManager.Instance?.PlayTaskSound();
        yield return new WaitForSeconds(currentLetter.letterSound.length);
        audioSource.PlayOneShot(currentLetter.letterSound);
    }

    private LetterData GetRandomLetter()
    {
        return letterPool[UnityEngine.Random.Range(0, letterPool.Count)];
    }

    private void Update()
    {
        if (!awaitingInput) return;

        foreach (KeyCode code in Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(code) && code.ToString().Length == 1)
            {
                char keyPressed = code.ToString()[0];
                if (char.ToUpperInvariant(keyPressed) == char.ToUpperInvariant(currentLetter.letter))
                {
                    awaitingInput = false;
                    HandleCorrectLetter();
                }
            }
        }
    }

    private void HandleCorrectLetter()
    {
        SoundManager.Instance?.PlayCorrectSound();
        StartCoroutine(NextLetter());
    }


    public void StopChallenge()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        awaitingInput = false;

        if (currentLetterGO != null)
        {
            Destroy(currentLetterGO);
            currentLetterGO = null;
        }

        currentLetter = null;
    }
}

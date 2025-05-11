using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("UI Sounds")]
    [SerializeField] private AudioClip buttonClickSound;

    private bool isPlayingLetterSound = false;

    
    [System.Serializable]
    public class CharacterSounds
    {
        public AudioClip appearanceSound;
        public AudioClip[] actionSounds;
        public AudioClip specialActionSound; // пробел
        public AudioClip hornSound; // для буквы Б
    }

    [Header("Character Sounds")]
    [SerializeField] private CharacterSounds rectangleSounds;
    [SerializeField] private CharacterSounds humanSounds;
    [SerializeField] private CharacterSounds carSounds;
    [SerializeField] private CharacterSounds busSounds;
    [SerializeField] private CharacterSounds trainSounds;

    [Header("Sound Settings")]
    [SerializeField] private float minDelayBetweenSounds = 0.1f;
    [SerializeField] private int maxSimultaneousSounds = 3;
    
    [Header("Letter Challenge")]
    [SerializeField] private AudioClip LetterTaskSound;
    [SerializeField] private AudioClip CorrectLetterSound;

    private AudioSource[] audioSources;
    private float lastSoundPlayTime;
    private CharacterType currentCharacter;
    private Queue<AudioSource> availableAudioSources = new Queue<AudioSource>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.OnCharacterChanged += HandleCharacterChanged;
            GameSettingsManager.Instance.OnGameStarted += HandleGameStarted;
            GameSettingsManager.Instance.OnSoundVolumeChanged += HandleSoundVolumeChanged;
        }
    }

    private void OnDestroy()
    {
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.OnCharacterChanged -= HandleCharacterChanged;
            GameSettingsManager.Instance.OnGameStarted -= HandleGameStarted;
            GameSettingsManager.Instance.OnSoundVolumeChanged -= HandleSoundVolumeChanged;
        }
    }

    private void InitializeAudioSources()
    {
        audioSources = new AudioSource[maxSimultaneousSounds];
        for (int i = 0; i < maxSimultaneousSounds; i++)
        {
            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSources[i] = audioSource;
            availableAudioSources.Enqueue(audioSource);
        }
    }

    private void HandleCharacterChanged(CharacterType character)
    {
        currentCharacter = character;
    }

    private void HandleGameStarted()
    {
        PlayAppearanceSound();
    }

    private void HandleSoundVolumeChanged(float volume)
    {
        foreach (var source in audioSources)
        {
            source.volume = volume;
        }
    }

    public void PlayButtonClick()
    {
        if (buttonClickSound != null)
        {
            PlaySound(buttonClickSound);
        }
    }

    public void PlayAppearanceSound()
    {
        var sounds = GetCurrentCharacterSounds();
        if (sounds?.appearanceSound != null)
        {
            PlaySound(sounds.appearanceSound);
        }
    }

    public void PlayActionSound()
    {
        if (Time.time - lastSoundPlayTime < minDelayBetweenSounds)
            return;

        var sounds = GetCurrentCharacterSounds();
        if (sounds?.actionSounds != null && sounds.actionSounds.Length > 0)
        {
            PlaySound(sounds.actionSounds[UnityEngine.Random.Range(0, sounds.actionSounds.Length)]);
        }
    }

    public void PlaySpecialActionSound()
    {
        if (Time.time - lastSoundPlayTime < minDelayBetweenSounds)
            return;

        var sounds = GetCurrentCharacterSounds();
        if (sounds?.specialActionSound != null)
        {
            PlaySound(sounds.specialActionSound);
        }
    }

    public void PlayHornSound()
    {
        if (Time.time - lastSoundPlayTime < minDelayBetweenSounds)
            return;

        var sounds = GetCurrentCharacterSounds();
        if (sounds?.hornSound != null)
        {
            PlaySound(sounds.hornSound);
        }
    }

    private CharacterSounds GetCurrentCharacterSounds()
    {
        return currentCharacter switch
        {
            CharacterType.Rectangle => rectangleSounds,
            CharacterType.Human => humanSounds,
            CharacterType.Car => carSounds,
            CharacterType.Bus => busSounds,
            CharacterType.Train => trainSounds,
            _ => null
        };
    }

    public void PlayLetterSequence(AudioClip letterSound, AudioClip objectSound, float delay)
    {
        StartCoroutine(PlayLetterSequenceCoroutine(letterSound, objectSound, delay));
    }

    private IEnumerator PlayLetterSequenceCoroutine(AudioClip letterSound, AudioClip objectSound, float delay)
    {
        if (letterSound != null)
        {
            PlaySound(letterSound);
            yield return new WaitForSeconds(letterSound.length + delay);
        }

        if (objectSound != null)
        {
            PlaySound(objectSound);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || availableAudioSources.Count == 0) return;

        var audioSource = availableAudioSources.Dequeue();
        audioSource.clip = clip;
        audioSource.Play();
        lastSoundPlayTime = Time.time;

        StartCoroutine(ReturnAudioSourceToPool(audioSource, clip.length));
    }

    private IEnumerator ReturnAudioSourceToPool(AudioSource audioSource, float delay)
    {
        yield return new WaitForSeconds(delay);
        availableAudioSources.Enqueue(audioSource);
    }

    public void PlayTaskSound()
    {
        PlaySound(LetterTaskSound);
    }

    public void PlayCorrectSound()
    {
        PlaySound(CorrectLetterSound);
    }
} 
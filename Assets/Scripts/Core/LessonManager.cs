using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public class LessonManager : MonoBehaviour
{
   public static LessonManager Instance {get; private set; }
   [SerializeField] private LettersGameConfig lettersConfig;

   [Serializable]
   public class Lesson
   {
      public string Title => $"Урок " + Letter;
      public char Letter;
      public int InitialRepetition;
      public int MixedRepetition;
      public List<char> PreviousLetters;

      public Lesson(char letter, int initial, int mixed, List<char> previous)
      {
         Letter = letter;
         InitialRepetition = initial;
         MixedRepetition = mixed;
         PreviousLetters = new List<char>(previous);
      }

      public List<char> GetSequence()
      {
         List<char> sequence = new();

         for (int i = 0; i < InitialRepetition; i++)
         {
            sequence.Add(Letter);
         }

         if (PreviousLetters != null && PreviousLetters.Count > 0)
         {
            List<char> mixedPool = new(PreviousLetters) { Letter };
            Random rand = new();
            for (int i = 0; i < MixedRepetition; i++)
            {
               int randIndex = rand.Next(mixedPool.Count);
               sequence.Add(mixedPool[randIndex]);
            }
         }
         
         return sequence;
      }
   }

   public List<Lesson> AllLessons { get; private set; } = new();
   public int CurrentLessonIndex { get; private set; }
   
   private const string ProgressKey = "LettersLessonProgress";
   private const string LegacyProgressKey = "LessonProgress";
   
   public Lesson CurrentLesson =>
   CurrentLessonIndex >= 0 && CurrentLessonIndex < AllLessons.Count
   ? AllLessons[CurrentLessonIndex]
   : null;

   private void Awake()
   {
      if (Instance != null && Instance != this)
      {
         Destroy(gameObject);
         return;
      }

      Instance = this;
      DontDestroyOnLoad(gameObject);
      ResolveConfig();
      InitializeLessons();
      LoadProgress();
   }

   public void ApplyConfig(LettersGameConfig config)
   {
      if (config == null || config == lettersConfig)
      {
         return;
      }

      lettersConfig = config;
      InitializeLessons();
      LoadProgress();
   }

   private void ResolveConfig()
   {
      if (lettersConfig == null && GameManager.Instance != null)
      {
         lettersConfig = GameManager.Instance.LettersConfig;
      }
   }

   private void InitializeLessons()
   {
      AllLessons.Clear();
      ResolveConfig();
      char[] configuredSequence = lettersConfig != null ? lettersConfig.GetLessonOrder() : "АОУИМПРСТНКЛЕВДБГЯЗЫЧЙЖШЮЦЩЭХФЪЬЁ".ToCharArray();
      
      List<char> previousL = new();
      foreach (char letter in configuredSequence)
      {
         int initialReps = lettersConfig != null ? Mathf.Max(1, lettersConfig.initialRepetition) : 10;
         int mixedReps = lettersConfig != null ? Mathf.Max(0, lettersConfig.mixedRepetition) : 10;
         AllLessons.Add(new Lesson(letter, initialReps, mixedReps, new List<char>(previousL)));
         previousL.Add(letter);
      }
   }
   
   public void LoadProgress()
   {
      CurrentLessonIndex = PlayerPrefs.HasKey(ProgressKey)
         ? PlayerPrefs.GetInt(ProgressKey, 0)
         : PlayerPrefs.GetInt(LegacyProgressKey, 0);
      CurrentLessonIndex = Mathf.Clamp(CurrentLessonIndex, 0, Mathf.Max(0, AllLessons.Count));
   }

   public void SaveProgress()
   {
      PlayerPrefs.SetInt(ProgressKey, CurrentLessonIndex);
      PlayerPrefs.Save();
   }

   public void AdvanceLesson()
   {
      CurrentLessonIndex++;
      SaveProgress();
   }

   public void ResetProgress()
   {
      CurrentLessonIndex = 0;
      SaveProgress();
   }

   public bool HasNextLesson()
   {
      return CurrentLessonIndex + 1 < AllLessons.Count;
   }
}

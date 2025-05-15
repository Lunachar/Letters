using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public class LessonManager : MonoBehaviour
{
   public static LessonManager Instance {get; private set; }

   [Serializable]
   public class Lesson
   {
      public string Title => $"Урок:  {Letter}";
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
   
   private const string ProgressKey = "LessonProgress";
   
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
      InitializeLessons();
      LoadProgress();
   }

   private void InitializeLessons()
   {
      AllLessons.Clear();
      List<char> sequence = new() { 'А', 'О', 'У', 'И', 'М', 'П', 'Р', 'С' };
      
      List<char> previousL = new();
      foreach (char letter in sequence)
      {
         int initialReps = 10;
         int mixedReps = 10;
         AllLessons.Add(new Lesson(letter, initialReps, mixedReps, new List<char>(previousL)));
         previousL.Add(letter);
      }
      
      char[] remaining = { 'Т', 'Н', 'К', 'Л', 'Е', 'В', 'Д', 'Б', 'Г', 'Я', 'З', 'Ы', 'Ч', 'Й', 'Ж', 'Ш', 'Ю', 'Ц', 'Щ', 'Э', 'Х', 'Ф', 'Ъ', 'Ь' };
      foreach (char letter in remaining)
      {
         AllLessons.Add(new Lesson(letter, 10, 10, new List<char>(previousL)));
         previousL.Add(letter);
      }
   }
   
   public void LoadProgress()
   {
      CurrentLessonIndex = PlayerPrefs.GetInt(ProgressKey, 0);
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

using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "ContentDatabase", menuName = "Letters/Content Database")]
public class ContentDatabase : ScriptableObject
{
    [SerializeField] private List<LetterData> letters = new List<LetterData>();
    
    public LetterData GetLetterData(char letter)
    {
        return letters.FirstOrDefault(l => l.letter == char.ToUpper(letter));
    }
    
    public bool IsValidLetter(char letter)
    {
        return letters.Any(l => l.letter == char.ToUpper(letter));
    }
    
    public void SaveStatistics()
    {
        // Mark the database as dirty so Unity saves it
        //EditorUtility.SetDirty(this);
    }
} 
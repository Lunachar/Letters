using System.Collections.Generic;
using UnityEngine.InputSystem;

public readonly struct RussianKeyboardKeyInfo
{
    public RussianKeyboardKeyInfo(char letter, Key key, string physicalLabel)
    {
        Letter = letter;
        Key = key;
        PhysicalLabel = physicalLabel;
    }

    public char Letter { get; }
    public Key Key { get; }
    public string PhysicalLabel { get; }
}

public static class RussianKeyboardLayout
{
    private static readonly RussianKeyboardKeyInfo[][] rows =
    {
        new[]
        {
            new RussianKeyboardKeyInfo('Ё', Key.Backquote, "`"),
            new RussianKeyboardKeyInfo('Й', Key.Q, "Q"),
            new RussianKeyboardKeyInfo('Ц', Key.W, "W"),
            new RussianKeyboardKeyInfo('У', Key.E, "E"),
            new RussianKeyboardKeyInfo('К', Key.R, "R"),
            new RussianKeyboardKeyInfo('Е', Key.T, "T"),
            new RussianKeyboardKeyInfo('Н', Key.Y, "Y"),
            new RussianKeyboardKeyInfo('Г', Key.U, "U"),
            new RussianKeyboardKeyInfo('Ш', Key.I, "I"),
            new RussianKeyboardKeyInfo('Щ', Key.O, "O"),
            new RussianKeyboardKeyInfo('З', Key.P, "P"),
            new RussianKeyboardKeyInfo('Х', Key.LeftBracket, "["),
            new RussianKeyboardKeyInfo('Ъ', Key.RightBracket, "]")
        },
        new[]
        {
            new RussianKeyboardKeyInfo('Ф', Key.A, "A"),
            new RussianKeyboardKeyInfo('Ы', Key.S, "S"),
            new RussianKeyboardKeyInfo('В', Key.D, "D"),
            new RussianKeyboardKeyInfo('А', Key.F, "F"),
            new RussianKeyboardKeyInfo('П', Key.G, "G"),
            new RussianKeyboardKeyInfo('Р', Key.H, "H"),
            new RussianKeyboardKeyInfo('О', Key.J, "J"),
            new RussianKeyboardKeyInfo('Л', Key.K, "K"),
            new RussianKeyboardKeyInfo('Д', Key.L, "L"),
            new RussianKeyboardKeyInfo('Ж', Key.Semicolon, ";"),
            new RussianKeyboardKeyInfo('Э', Key.Quote, "'")
        },
        new[]
        {
            new RussianKeyboardKeyInfo('Я', Key.Z, "Z"),
            new RussianKeyboardKeyInfo('Ч', Key.X, "X"),
            new RussianKeyboardKeyInfo('С', Key.C, "C"),
            new RussianKeyboardKeyInfo('М', Key.V, "V"),
            new RussianKeyboardKeyInfo('И', Key.B, "B"),
            new RussianKeyboardKeyInfo('Т', Key.N, "N"),
            new RussianKeyboardKeyInfo('Ь', Key.M, "M"),
            new RussianKeyboardKeyInfo('Б', Key.Comma, ","),
            new RussianKeyboardKeyInfo('Ю', Key.Period, ".")
        }
    };

    private static readonly Dictionary<char, RussianKeyboardKeyInfo> byLetter = new Dictionary<char, RussianKeyboardKeyInfo>();
    private static readonly Dictionary<Key, RussianKeyboardKeyInfo> byKey = new Dictionary<Key, RussianKeyboardKeyInfo>();

    static RussianKeyboardLayout()
    {
        for (int row = 0; row < rows.Length; row++)
        {
            for (int i = 0; i < rows[row].Length; i++)
            {
                RussianKeyboardKeyInfo info = rows[row][i];
                byLetter[info.Letter] = info;
                byKey[info.Key] = info;
            }
        }
    }

    public static IReadOnlyList<RussianKeyboardKeyInfo[]> Rows => rows;

    public static bool TryGetInfoForLetter(char letter, out RussianKeyboardKeyInfo info)
    {
        return byLetter.TryGetValue(char.ToUpperInvariant(letter), out info);
    }

    public static bool TryGetKeyForLetter(char letter, out Key key)
    {
        if (TryGetInfoForLetter(letter, out RussianKeyboardKeyInfo info))
        {
            key = info.Key;
            return true;
        }

        key = Key.None;
        return false;
    }

    public static bool TryGetLetterForKey(Key key, out char letter)
    {
        if (byKey.TryGetValue(key, out RussianKeyboardKeyInfo info))
        {
            letter = info.Letter;
            return true;
        }

        letter = '\0';
        return false;
    }

    public static string GetPhysicalLabel(Key key)
    {
        return byKey.TryGetValue(key, out RussianKeyboardKeyInfo info) ? info.PhysicalLabel : key.ToString();
    }
}

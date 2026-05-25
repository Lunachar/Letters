using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class TopicsSecurity
{
    private const string PinHashKey = "Topics.Creator.PinHash";
    private const string PinSaltKey = "Topics.Creator.PinSalt";

    public static bool HasPin => PlayerPrefs.HasKey(PinHashKey) && PlayerPrefs.HasKey(PinSaltKey);

    public static bool IsValidPinShape(string pin)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length < 4 || pin.Length > 6)
        {
            return false;
        }

        for (int i = 0; i < pin.Length; i++)
        {
            if (!char.IsDigit(pin[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static void SetPin(string pin)
    {
        if (!IsValidPinShape(pin))
        {
            throw new ArgumentException("PIN must contain 4-6 digits.", nameof(pin));
        }

        string salt = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(PinSaltKey, salt);
        PlayerPrefs.SetString(PinHashKey, HashPin(pin, salt));
        PlayerPrefs.Save();
    }

    public static bool VerifyPin(string pin)
    {
        if (!IsValidPinShape(pin) || !HasPin)
        {
            return false;
        }

        string salt = PlayerPrefs.GetString(PinSaltKey);
        string hash = PlayerPrefs.GetString(PinHashKey);
        return string.Equals(hash, HashPin(pin, salt), StringComparison.Ordinal);
    }

    private static string HashPin(string pin, string salt)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(salt + ":" + pin));
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    
    public event Action<char> OnLetterPressed;
    public event Action<Key> OnPhysicalKeyPressed;
    public event Action<int> OnNumberPressed;
    public event Action OnAnyKeyPressed;
    public event Action OnSpacePressed;

    private Keyboard subscribedKeyboard;
    private int anyKeyFrame = -1;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("InputManager: Instance created");
        }
        else
        {
            Debug.Log("InputManager: Instance already exists, destroying");
            Destroy(gameObject);
        }
    }
    
    private void OnEnable()
    {
        SubscribeKeyboard();
    }
    
    private void OnDisable()
    {
        UnsubscribeKeyboard();
    }

    private void Update()
    {
        SubscribeKeyboard();

        if (Keyboard.current == null)
        {
            return;
        }

        foreach (KeyControl keyControl in Keyboard.current.allKeys)
        {
            if (!keyControl.wasPressedThisFrame)
            {
                continue;
            }

            NotifyAnyKeyPressed();
            OnPhysicalKeyPressed?.Invoke(keyControl.keyCode);
            if (keyControl.keyCode == Key.Space)
            {
                OnSpacePressed?.Invoke();
            }

            break;
        }
    }

    private void SubscribeKeyboard()
    {
        if (subscribedKeyboard == Keyboard.current)
        {
            return;
        }

        UnsubscribeKeyboard();
        subscribedKeyboard = Keyboard.current;
        if (subscribedKeyboard != null)
        {
            subscribedKeyboard.onTextInput += HandleTextInput;
        }
    }

    private void UnsubscribeKeyboard()
    {
        if (subscribedKeyboard != null)
        {
            subscribedKeyboard.onTextInput -= HandleTextInput;
            subscribedKeyboard = null;
        }
    }
    
    private void HandleTextInput(char inputChar)
    {
        // Вызываем общее событие нажатия клавиши
        NotifyAnyKeyPressed();
        
        // Обрабатываем буквы
        if (char.IsLetter(inputChar))
        {
            OnLetterPressed?.Invoke(char.ToUpper(inputChar));
        }
        // Обрабатываем цифры
        else if (char.IsDigit(inputChar))
        {
            int number = int.Parse(inputChar.ToString());
            OnNumberPressed?.Invoke(number);
        }
    }

    private void NotifyAnyKeyPressed()
    {
        if (anyKeyFrame == Time.frameCount)
        {
            return;
        }

        anyKeyFrame = Time.frameCount;
        OnAnyKeyPressed?.Invoke();
    }
}

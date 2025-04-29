using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    
    public event Action<char> OnLetterPressed;
    public event Action<int> OnNumberPressed;
    public event Action OnAnyKeyPressed;
    
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
        Keyboard.current.onTextInput += HandleTextInput;
    }
    
    private void OnDisable()
    {
        Keyboard.current.onTextInput -= HandleTextInput;
    }
    
    private void HandleTextInput(char inputChar)
    {
        // Вызываем общее событие нажатия клавиши
        OnAnyKeyPressed?.Invoke();
        
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
} 
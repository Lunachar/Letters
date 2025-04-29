using UnityEngine;
using System.Collections;
using DG.Tweening;

public class BusAnimator : MonoBehaviour
{
    [Header("Wheel Settings")]
    [SerializeField] private Transform leftWheelPoint;
    [SerializeField] private Transform rightWheelPoint;
    [SerializeField] private GameObject wheelPrefab;
    [SerializeField] private float wheelRotationSpeed = 360f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 0.2f;
    [SerializeField] private float jumpDuration = 0.3f;

    private GameObject leftWheel;
    private GameObject rightWheel;
    private bool isAnimating = false;
    private Vector3 originalPosition;
    private Sequence jumpSequence;

    private void Start()
    {
        // Создаем колеса
        if (wheelPrefab != null)
        {
            if (leftWheelPoint != null)
                leftWheel = Instantiate(wheelPrefab, leftWheelPoint.position, Quaternion.identity, transform);
            
            if (rightWheelPoint != null)
                rightWheel = Instantiate(wheelPrefab, rightWheelPoint.position, Quaternion.identity, transform);
        }

        originalPosition = transform.position;

        // Подписываемся на события нажатия клавиш
        InputManager.Instance.OnLetterPressed += HandleLetterPressed;
        InputManager.Instance.OnSpacePressed += HandleSpacePressed;
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnLetterPressed -= HandleLetterPressed;
            InputManager.Instance.OnSpacePressed -= HandleSpacePressed;
        }

        // Останавливаем все анимации при уничтожении
        if (jumpSequence != null)
        {
            jumpSequence.Kill();
        }
    }

    private void HandleLetterPressed(char letter)
    {
        // Вращаем колеса и делаем небольшой прыжок
        StartWheelRotation();
        JumpBus();

        // Проигрываем звук движения
        SoundManager.Instance?.PlayActionSound();

        // Если нажата буква 'Б', проигрываем гудок
        if (letter == 'Б')
        {
            SoundManager.Instance?.PlayHornSound();
        }
    }

    private void HandleSpacePressed()
    {
        // Проигрываем звук открытия дверей
        SoundManager.Instance?.PlaySpecialActionSound();
    }

    private void StartWheelRotation()
    {
        if (leftWheel != null)
            leftWheel.transform.DORotate(new Vector3(0, 0, -360), 1f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear);

        if (rightWheel != null)
            rightWheel.transform.DORotate(new Vector3(0, 0, -360), 1f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear);
    }

    private void JumpBus()
    {
        // Если уже прыгаем, не начинаем новый прыжок
        if (isAnimating) return;

        isAnimating = true;

        // Создаем последовательность анимации
        jumpSequence = DOTween.Sequence();

        // Добавляем прыжок вверх и вниз
        jumpSequence.Append(transform.DOMove(originalPosition + Vector3.up * jumpHeight, jumpDuration / 2)
            .SetEase(Ease.OutQuad));
        jumpSequence.Append(transform.DOMove(originalPosition, jumpDuration / 2)
            .SetEase(Ease.InQuad));

        // Добавляем небольшой наклон
        jumpSequence.Join(transform.DORotate(new Vector3(0, 0, -5), jumpDuration / 4)
            .SetLoops(2, LoopType.Yoyo));

        jumpSequence.OnComplete(() => {
            isAnimating = false;
            transform.rotation = Quaternion.identity;
        });
    }
} 
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class MenuBackgroundEffect : MonoBehaviour
{
    [Header("Letter Settings")]
    [SerializeField] private TMPro.TextMeshProUGUI letterPrefab;
    [SerializeField] private float minSize = 40f;
    [SerializeField] private float maxSize = 520f;
    [SerializeField] private float minSpeed = 50f;
    [SerializeField] private float maxSpeed = 200f;
    [SerializeField] private float spawnInterval = 0.5f;
    [SerializeField] private int maxLetters = 20;
    [SerializeField] private Color[] letterColors;
    [SerializeField] private TMP_FontAsset robotoFontAsset;

    [Header("Spawn Area")]
    [SerializeField] private float spawnLeftX = -500f;
    [SerializeField] private float spawnRightX = 100f;
    [SerializeField] private float destroyX = 1000f;

    private readonly string russianLetters = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
    private List<TMPro.TextMeshProUGUI> activeLetters = new List<TMPro.TextMeshProUGUI>();
    private RectTransform canvasRect;

    private void Start()
    {
        canvasRect = GetComponent<RectTransform>();
        if (letterColors == null || letterColors.Length == 0)
        {
            // Если цвета не заданы, используем стандартные
            letterColors = new Color[]
            {
                new Color(1f, 1f, 1f, 0.2f), // Белый полупрозрачный
                new Color(0.8f, 0.8f, 0.8f, 0.15f), // Серый полупрозрачный
                new Color(0.7f, 0.7f, 0.7f, 0.1f) // Тёмно-серый полупрозрачный
            };
        }

        StartCoroutine(SpawnLetters());
    }

    private void Update()
    {
        // Перемещаем все активные буквы
        for (int i = activeLetters.Count - 1; i >= 0; i--)
        {
            if (activeLetters[i] == null) continue;

            var letterRect = activeLetters[i].rectTransform;
            var speed = (float)letterRect.GetComponent<LetterMovement>().speed;
            
            // Двигаем букву
            letterRect.anchoredPosition += Vector2.right * speed * Time.deltaTime;

            // Проверяем, не вышла ли буква за пределы экрана
            if (letterRect.anchoredPosition.x > destroyX)
            {
                Destroy(activeLetters[i].gameObject);
                activeLetters.RemoveAt(i);
            }
        }
    }

    private IEnumerator SpawnLetters()
    {
        while (true)
        {
            if (activeLetters.Count < maxLetters)
            {
                SpawnLetter();
            }
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnLetter()
    {
        // Создаём букву
        TextMeshProUGUI newLetter = Instantiate(letterPrefab, transform);
        activeLetters.Add(newLetter);
        newLetter.font = robotoFontAsset;


        // Задаём размер букв
        float size = Random.Range(minSize, maxSize);
        newLetter.fontSize = Mathf.RoundToInt(size);

        // Устанавливаем случайную букву
        newLetter.text = russianLetters[Random.Range(0, russianLetters.Length)].ToString();
        
        // Устанавливаем местоположение
        RectTransform rectTransform = newLetter.rectTransform;
        rectTransform.anchoredPosition = new Vector2(
            Random.Range(spawnLeftX, spawnRightX),
            Random.Range(-canvasRect.rect.height / 2f, canvasRect.rect.height / 2f)
            );

        // Устанавливаем случайный цвет
        newLetter.color = letterColors[Random.Range(0, letterColors.Length)];

        // Добавляем компонент движения
        var movement = newLetter.gameObject.AddComponent<LetterMovement>();
        movement.speed = Random.Range(minSpeed, maxSpeed);
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}

// Вспомогательный класс для хранения скорости движения буквы
public class LetterMovement : MonoBehaviour
{
    public float speed;
} 
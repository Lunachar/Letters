using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AnswerCelebrationEffect : MonoBehaviour
{
    [SerializeField] private int particleCount = 28;
    [SerializeField] private float spread = 260f;
    [SerializeField] private float durationMin = 0.55f;
    [SerializeField] private float durationMax = 0.95f;

    private readonly Color[] colors =
    {
        new Color(1f, 0.82f, 0.20f),
        new Color(0.30f, 0.76f, 1f),
        new Color(0.43f, 0.90f, 0.45f),
        new Color(1f, 0.42f, 0.55f),
        new Color(0.82f, 0.54f, 1f)
    };

    public static void Play(Canvas canvas, Vector2 screenPoint)
    {
        if (canvas == null)
        {
            return;
        }

        AnswerCelebrationEffect effect = canvas.GetComponent<AnswerCelebrationEffect>();
        if (effect == null)
        {
            effect = canvas.gameObject.AddComponent<AnswerCelebrationEffect>();
        }

        effect.PlayAt(screenPoint);
    }

    public void PlayAt(Vector2 screenPoint)
    {
        Canvas canvas = GetComponent<Canvas>();
        RectTransform root = canvas != null ? canvas.GetComponent<RectTransform>() : transform as RectTransform;
        if (root == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, null, out Vector2 localPoint);
        StartCoroutine(Spawn(root, localPoint));
    }

    private IEnumerator Spawn(RectTransform root, Vector2 localPoint)
    {
        for (int i = 0; i < particleCount; i++)
        {
            GameObject piece = new GameObject("Answer Confetti", typeof(RectTransform), typeof(Image));
            piece.transform.SetParent(root, false);

            RectTransform rect = piece.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = localPoint;
            rect.sizeDelta = new Vector2(Random.Range(10f, 22f), Random.Range(10f, 22f));
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));

            Image image = piece.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = colors[i % colors.Length];

            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction == Vector2.zero)
            {
                direction = Vector2.up;
            }

            Vector2 end = localPoint + new Vector2(direction.x * Random.Range(80f, spread), Mathf.Abs(direction.y) * Random.Range(80f, spread) + Random.Range(30f, 120f));
            StartCoroutine(AnimatePiece(rect, image, end, Random.Range(durationMin, durationMax)));
            yield return new WaitForSeconds(0.01f);
        }
    }

    private IEnumerator AnimatePiece(RectTransform rect, Image image, Vector2 end, float duration)
    {
        float elapsed = 0f;
        Vector2 start = rect.anchoredPosition;
        Color color = image.color;

        while (rect != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float arc = Mathf.Sin(t * Mathf.PI) * 80f;
            rect.anchoredPosition = Vector2.Lerp(start, end, t) + new Vector2(0f, arc - t * 140f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rect.localRotation.eulerAngles.z + Time.unscaledDeltaTime * 360f);
            image.color = new Color(color.r, color.g, color.b, 1f - t);
            yield return null;
        }

        if (rect != null)
        {
            Destroy(rect.gameObject);
        }
    }
}

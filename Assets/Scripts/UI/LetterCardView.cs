using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum LetterCardState
{
    Normal,
    Current,
    Correct,
    Wrong
}

public class LetterCardView : MonoBehaviour
{
    private Image background;
    private Outline outline;
    private Text label;
    private LettersGameConfig config;
    private Coroutine feedbackRoutine;
    private Coroutine moveRoutine;
    private bool hasLayout;

    public RectTransform RectTransform { get; private set; }

    public static LetterCardView Create(RectTransform parent, GameObject letterPrefab, char letter, LettersGameConfig config)
    {
        GameObject card = new GameObject("LetterCard_" + letter, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(LetterCardView));
        card.transform.SetParent(parent, false);

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = config != null ? config.letterCardSize : new Vector2(220f, 260f);

        GameObject textObject = new GameObject("Letter", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(card.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        textRect.pivot = new Vector2(0.5f, 0.5f);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = letter.ToString().ToUpperInvariant();
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = config != null ? config.letterFontSize : 150;
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        LetterCardView view = card.GetComponent<LetterCardView>();
        view.Initialize(config, text);
        view.SetState(LetterCardState.Normal);
        return view;
    }

    private void Initialize(LettersGameConfig lettersConfig, Text text)
    {
        config = lettersConfig;
        RectTransform = GetComponent<RectTransform>();
        background = GetComponent<Image>();
        outline = GetComponent<Outline>();
        label = text;

        background.raycastTarget = false;
        outline.effectDistance = new Vector2(4f, -4f);
    }

    public void SetLetter(char letter)
    {
        if (label != null)
        {
            label.text = letter.ToString().ToUpperInvariant();
        }
    }

    public void SetState(LetterCardState state)
    {
        if (background == null || label == null || outline == null)
        {
            return;
        }

        Color cardColor = config != null ? config.cardColor : new Color(0.96f, 0.98f, 0.94f, 1f);
        Color currentColor = config != null ? config.currentCardColor : new Color(1f, 0.86f, 0.36f, 1f);
        Color correctColor = config != null ? config.correctColor : new Color(0.18f, 0.70f, 0.40f, 1f);
        Color wrongColor = config != null ? config.wrongColor : new Color(0.88f, 0.22f, 0.20f, 1f);
        Color textColor = config != null ? config.cardTextColor : new Color(0.05f, 0.08f, 0.09f, 1f);
        Color invertedText = config != null ? config.invertedTextColor : Color.white;

        switch (state)
        {
            case LetterCardState.Current:
                background.color = currentColor;
                label.color = textColor;
                outline.effectColor = new Color(1f, 1f, 1f, 0.75f);
                break;
            case LetterCardState.Correct:
                background.color = correctColor;
                label.color = invertedText;
                outline.effectColor = new Color(1f, 1f, 1f, 0.82f);
                break;
            case LetterCardState.Wrong:
                background.color = wrongColor;
                label.color = invertedText;
                outline.effectColor = new Color(1f, 1f, 1f, 0.82f);
                break;
            default:
                background.color = cardColor;
                label.color = textColor;
                outline.effectColor = new Color(0f, 0f, 0f, 0.18f);
                break;
        }
    }

    public void SetLayout(Vector2 anchoredPosition, Vector2 size, bool animate)
    {
        if (RectTransform == null)
        {
            return;
        }

        RectTransform.anchorMin = RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        RectTransform.pivot = new Vector2(0.5f, 0.5f);
        RectTransform.sizeDelta = size;

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        if (!hasLayout || !animate || config == null || config.cardMoveDuration <= 0f)
        {
            RectTransform.anchoredPosition = anchoredPosition;
            hasLayout = true;
            return;
        }

        moveRoutine = StartCoroutine(MoveToRoutine(anchoredPosition, config.cardMoveDuration));
    }

    public void PlaySpawnFeedback()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
        }

        feedbackRoutine = StartCoroutine(SpawnFeedbackRoutine());
    }

    public void PlayPressFeedback()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
        }

        feedbackRoutine = StartCoroutine(PressFeedbackRoutine());
    }

    public void PlayCorrectFeedback()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
        }

        feedbackRoutine = StartCoroutine(CorrectFeedbackRoutine());
    }

    private IEnumerator MoveToRoutine(Vector2 targetPosition, float duration)
    {
        Vector2 startPosition = RectTransform.anchoredPosition;
        float safeDuration = Mathf.Max(0.01f, duration);

        for (float t = 0f; t < safeDuration; t += Time.deltaTime)
        {
            float normalized = Mathf.Clamp01(t / safeDuration);
            float eased = 1f - Mathf.Pow(1f - normalized, 3f);
            RectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
            yield return null;
        }

        RectTransform.anchoredPosition = targetPosition;
        hasLayout = true;
        moveRoutine = null;
    }

    private IEnumerator PressFeedbackRoutine()
    {
        float duration = config != null ? config.pressFeedbackDuration : 0.12f;
        float targetScale = config != null ? config.pressScale : 0.96f;
        Vector3 original = Vector3.one;
        Vector3 pressed = Vector3.one * targetScale;
        float half = Mathf.Max(0.01f, duration * 0.5f);

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(original, pressed, t / half);
            yield return null;
        }

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(pressed, original, t / half);
            yield return null;
        }

        transform.localScale = original;
        feedbackRoutine = null;
    }

    private IEnumerator CorrectFeedbackRoutine()
    {
        float duration = config != null ? config.correctPulseDuration : 0.24f;
        float targetScale = config != null ? config.correctPulseScale : 1.08f;
        Vector3 original = Vector3.one;
        Vector3 enlarged = Vector3.one * targetScale;
        float half = Mathf.Max(0.01f, duration * 0.5f);

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / half), 2f);
            transform.localScale = Vector3.LerpUnclamped(original, enlarged, eased);
            yield return null;
        }

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            float normalized = Mathf.Clamp01(t / half);
            float eased = normalized * normalized;
            transform.localScale = Vector3.LerpUnclamped(enlarged, original, eased);
            yield return null;
        }

        transform.localScale = original;
        feedbackRoutine = null;
    }

    private IEnumerator SpawnFeedbackRoutine()
    {
        float duration = config != null ? config.cardSpawnDuration : 0.16f;
        float safeDuration = Mathf.Max(0.01f, duration);
        Vector3 start = Vector3.one * 0.92f;
        Vector3 end = Vector3.one;
        transform.localScale = start;

        for (float t = 0f; t < safeDuration; t += Time.deltaTime)
        {
            float normalized = Mathf.Clamp01(t / safeDuration);
            float eased = 1f - Mathf.Pow(1f - normalized, 3f);
            transform.localScale = Vector3.LerpUnclamped(start, end, eased);
            yield return null;
        }

        transform.localScale = end;
        feedbackRoutine = null;
    }
}

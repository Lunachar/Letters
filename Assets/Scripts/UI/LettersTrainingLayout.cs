using System.Collections.Generic;
using UnityEngine;

public static class LettersTrainingLayout
{
    public static bool UsesConfiguredLayout(LettersGameConfig config)
    {
        return config == null || config.useConfiguredLetterLayout;
    }

    public static bool TryPositionCards(IList<LetterCardView> cards, RectTransform parent, LettersGameConfig config, bool animate = true)
    {
        if (!UsesConfiguredLayout(config) || cards == null || cards.Count == 0 || parent == null)
        {
            return false;
        }

        Rect parentRect = parent.rect;
        if (parentRect.width <= 1f || parentRect.height <= 1f)
        {
            return false;
        }

        Vector2 min = config != null ? config.letterAreaAnchorMin : new Vector2(0.16f, 0.40f);
        Vector2 max = config != null ? config.letterAreaAnchorMax : new Vector2(0.84f, 0.78f);
        Vector2 cardSize = config != null ? config.letterCardSize : new Vector2(220f, 260f);

        float yAnchor = Mathf.Lerp(min.y, max.y, 0.5f);
        float y = (yAnchor - parent.pivot.y) * parentRect.height;
        for (int i = 0; i < cards.Count; i++)
        {
            LetterCardView card = cards[i];
            if (card == null || card.RectTransform == null)
            {
                continue;
            }

            float t = cards.Count == 1 ? 0.5f : i / (float)(cards.Count - 1);
            float xAnchor = Mathf.Lerp(min.x, max.x, t);
            float x = (xAnchor - parent.pivot.x) * parentRect.width;
            card.SetLayout(new Vector2(x, y), cardSize, animate);
        }

        return true;
    }
}

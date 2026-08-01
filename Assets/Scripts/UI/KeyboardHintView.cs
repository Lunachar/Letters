using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class KeyboardHintView : MonoBehaviour
{
    private readonly Dictionary<Key, Image> keyBackgrounds = new Dictionary<Key, Image>();
    private readonly Dictionary<Key, Text> keyLabels = new Dictionary<Key, Text>();
    private LettersGameConfig config;
    private RectTransform rectTransform;
    private Font uiFont;
    private char targetLetter;
    private Key targetKey;

    public static KeyboardHintView Create(RectTransform parent, LettersGameConfig config)
    {
        GameObject root = new GameObject("KeyboardHintView", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(KeyboardHintView));
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = config != null ? config.keyboardAnchorMin : new Vector2(0.12f, 0.03f);
        rect.anchorMax = config != null ? config.keyboardAnchorMax : new Vector2(0.88f, 0.32f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image panel = root.GetComponent<Image>();
        panel.color = config != null ? config.keyboardPanelColor : new Color(0.04f, 0.10f, 0.12f, 0.94f);
        panel.raycastTarget = false;

        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 10, 10);
        layout.spacing = 5f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = true;

        KeyboardHintView view = root.GetComponent<KeyboardHintView>();
        view.Initialize(config);
        return view;
    }

    private void Initialize(LettersGameConfig lettersConfig)
    {
        config = lettersConfig;
        rectTransform = GetComponent<RectTransform>();
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildKeyboard();
    }

    public void ShowTarget(char letter)
    {
        targetLetter = char.ToUpperInvariant(letter);
        RussianKeyboardLayout.TryGetKeyForLetter(targetLetter, out targetKey);
        ResetKeys();
        HighlightKey(targetKey, config != null ? config.keyboardTargetColor : new Color(1f, 0.81f, 0.24f, 1f), 1.12f);
    }

    public void ShowCorrect()
    {
        ResetKeys();
        HighlightKey(targetKey, config != null ? config.keyboardPressedCorrectColor : new Color(0.18f, 0.70f, 0.40f, 1f), 1.12f, true);
    }

    public void ShowError(Key pressedKey)
    {
        ResetKeys();
        HighlightKey(targetKey, config != null ? config.keyboardTargetColor : new Color(1f, 0.81f, 0.24f, 1f), 1.12f);
        HighlightKey(pressedKey, config != null ? config.keyboardPressedWrongColor : new Color(0.88f, 0.22f, 0.20f, 1f), 1.04f, true);
    }

    private void BuildKeyboard()
    {
        AddTitle();

        IReadOnlyList<RussianKeyboardKeyInfo[]> rows = RussianKeyboardLayout.Rows;
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            GameObject rowObject = new GameObject("KeyboardRow_" + rowIndex, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowObject.transform.SetParent(rectTransform, false);

            LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
            rowElement.flexibleHeight = 1f;

            HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 5f;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childForceExpandWidth = true;

            RussianKeyboardKeyInfo[] row = rows[rowIndex];
            for (int i = 0; i < row.Length; i++)
            {
                CreateKey(rowObject.transform, row[i]);
            }
        }
    }

    private void AddTitle()
    {
        GameObject titleObject = new GameObject("KeyboardTitle", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        titleObject.transform.SetParent(rectTransform, false);
        LayoutElement layoutElement = titleObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 28f;
        layoutElement.flexibleHeight = 0f;

        Text title = titleObject.GetComponent<Text>();
        title.font = uiFont;
        title.text = config != null ? config.miniKeyboardTitle : "Найди клавишу";
        title.alignment = TextAnchor.MiddleCenter;
        title.fontSize = 20;
        title.fontStyle = FontStyle.Bold;
        title.color = config != null ? config.mutedTextColor : new Color(0.78f, 0.86f, 0.84f, 1f);
        title.raycastTarget = false;
    }

    private void CreateKey(Transform parent, RussianKeyboardKeyInfo info)
    {
        GameObject keyObject = new GameObject(info.Letter.ToString(), typeof(RectTransform), typeof(Image), typeof(Outline), typeof(LayoutElement));
        keyObject.transform.SetParent(parent, false);

        LayoutElement element = keyObject.GetComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        element.minWidth = info.Letter == 'Ё' ? 42f : 54f;

        Image background = keyObject.GetComponent<Image>();
        background.color = config != null ? config.keyboardKeyColor : new Color(0.88f, 0.93f, 0.90f, 1f);
        background.raycastTarget = false;

        Outline outline = keyObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.16f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(keyObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(3f, 2f);
        labelRect.offsetMax = new Vector2(-3f, -2f);

        Text label = labelObject.GetComponent<Text>();
        label.font = uiFont;
        label.text = info.Letter.ToString();
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = config != null ? config.keyboardFontSize : 30;
        label.fontStyle = FontStyle.Bold;
        label.supportRichText = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.color = config != null ? config.keyboardTextColor : new Color(0.05f, 0.08f, 0.09f, 1f);
        label.raycastTarget = false;

        keyBackgrounds[info.Key] = background;
        keyLabels[info.Key] = label;
    }

    private void ResetKeys()
    {
        Color keyColor = config != null ? config.keyboardKeyColor : new Color(0.88f, 0.93f, 0.90f, 1f);
        Color textColor = config != null ? config.keyboardTextColor : new Color(0.05f, 0.08f, 0.09f, 1f);
        foreach (KeyValuePair<Key, Image> pair in keyBackgrounds)
        {
            pair.Value.color = keyColor;
            pair.Value.transform.localScale = Vector3.one;
        }

        foreach (Text label in keyLabels.Values)
        {
            label.color = textColor;
        }
    }

    private void HighlightKey(Key key, Color color, float scale, bool invertText = false)
    {
        if (!keyBackgrounds.TryGetValue(key, out Image background))
        {
            return;
        }

        background.color = color;
        background.transform.localScale = Vector3.one * scale;

        if (invertText && keyLabels.TryGetValue(key, out Text label))
        {
            label.color = config != null ? config.invertedTextColor : Color.white;
        }
    }
}

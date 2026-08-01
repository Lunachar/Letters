using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DwellSelectable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private float dwellTime = 1.1f;
    [SerializeField] private Image progressImage;

    private Button button;
    private RectTransform rectTransform;
    private float hoverTime;
    private bool pointerHovering;
    private bool dwellActive;
    private bool invoked;

    public void Configure(float seconds, Image progress)
    {
        dwellTime = Mathf.Max(0.1f, seconds);
        progressImage = progress;
        SetProgress(0f);
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        rectTransform = GetComponent<RectTransform>();
    }

    private void Update()
    {
        bool isHovering = pointerHovering || IsGazeHovering();
        if (!isHovering || button == null || !button.interactable)
        {
            if (dwellActive)
            {
                ResetDwell();
            }
            return;
        }

        if (!dwellActive)
        {
            dwellActive = true;
            hoverTime = 0f;
            invoked = false;
            SetProgress(0f);
        }

        hoverTime += Time.unscaledDeltaTime;
        SetProgress(Mathf.Clamp01(hoverTime / dwellTime));

        if (!invoked && hoverTime >= dwellTime)
        {
            invoked = true;
            if (GazePointer.TryGetScreenPoint(out Vector2 screenPoint))
            {
                GazePointer.NotifyActivation(screenPoint);
            }
            button.onClick.Invoke();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerHovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerHovering = false;
        if (!IsGazeHovering())
        {
            ResetDwell();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GazePointer.NotifyActivation(eventData.position);
    }

    private bool IsGazeHovering()
    {
        return rectTransform != null
            && GazePointer.TryGetScreenPoint(out Vector2 screenPoint)
            && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint, null);
    }

    private void ResetDwell()
    {
        dwellActive = false;
        hoverTime = 0f;
        invoked = false;
        SetProgress(0f);
    }

    private void SetProgress(float value)
    {
        if (progressImage != null)
        {
            progressImage.fillAmount = value;
        }
    }
}

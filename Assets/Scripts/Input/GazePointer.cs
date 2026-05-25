using UnityEngine;
using UnityEngine.UI;

#if TOBII_GAMING
using Tobii.Gaming;
#endif

public enum GazePointerProvider
{
    MouseOrTouch,
    Tobii,
    Auto
}

public class GazePointer : MonoBehaviour
{
    public static GazePointer Instance { get; private set; }

    [SerializeField] private GazePointerProvider provider = GazePointerProvider.Auto;
    [SerializeField] private bool useMouseFallback = true;
    [SerializeField] private bool showDebugCursor;

    private RectTransform debugCursor;

    public static bool TryGetScreenPoint(out Vector2 screenPoint)
    {
        if (Instance != null)
        {
            return Instance.TryGetPoint(out screenPoint);
        }

        screenPoint = Input.mousePosition;
        return true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (showDebugCursor && TryGetPoint(out Vector2 point))
        {
            EnsureDebugCursor();
            debugCursor.position = point;
        }
        else if (debugCursor != null)
        {
            debugCursor.gameObject.SetActive(false);
        }
    }

    private bool TryGetPoint(out Vector2 screenPoint)
    {
        if (provider == GazePointerProvider.Tobii || provider == GazePointerProvider.Auto)
        {
            if (TryGetTobiiPoint(out screenPoint))
            {
                return true;
            }
        }

        if (provider == GazePointerProvider.MouseOrTouch || provider == GazePointerProvider.Auto && useMouseFallback)
        {
            return TryGetPointerFallback(out screenPoint);
        }

        screenPoint = Vector2.zero;
        return false;
    }

    private bool TryGetPointerFallback(out Vector2 screenPoint)
    {
        if (Input.touchCount > 0)
        {
            screenPoint = Input.GetTouch(0).position;
            return true;
        }

        screenPoint = Input.mousePosition;
        return true;
    }

    private bool TryGetTobiiPoint(out Vector2 screenPoint)
    {
#if TOBII_GAMING
        GazePoint gazePoint = TobiiAPI.GetGazePoint();
        if (gazePoint.IsValid)
        {
            screenPoint = gazePoint.Screen;
            return true;
        }
#endif
        screenPoint = Vector2.zero;
        return false;
    }

    private void EnsureDebugCursor()
    {
        if (debugCursor != null)
        {
            debugCursor.gameObject.SetActive(true);
            return;
        }

        GameObject canvasObject = new GameObject("Gaze Debug Canvas", typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(canvasObject);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        GameObject cursorObject = new GameObject("Gaze Cursor", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        cursorObject.transform.SetParent(canvasObject.transform, false);
        debugCursor = cursorObject.GetComponent<RectTransform>();
        debugCursor.sizeDelta = new Vector2(28f, 28f);
        cursorObject.GetComponent<UnityEngine.UI.Image>().color = new Color(1f, 0.86f, 0.1f, 0.85f);
    }
}

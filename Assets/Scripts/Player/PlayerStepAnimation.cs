using UnityEngine;

public class PlayerStepAnimation : MonoBehaviour
{
    [SerializeField] private float rotationDuration = 0.2f;
    [SerializeField] private float characterWidth = 1f;    // Длинная сторона
    [SerializeField] private float characterHeight = 0.5f; // Короткая сторона
    [SerializeField] private float floorY = 0f;
    [SerializeField] private bool showDebug = true;

    private float currentAngle = 0f;
    private float targetAngle = 0f;
    private float rotationStartTime;
    private bool isRotating = false;
    private float floorLevel;

    private void Start()
    {
        floorLevel = floorY;
        currentAngle = 0f;
        targetAngle = 0f;
        transform.rotation = Quaternion.identity;
        PositionOnFloor();
        
        InputManager.Instance.OnAnyKeyPressed += TriggerRotation;
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnAnyKeyPressed -= TriggerRotation;
        }
    }

    private void TriggerRotation()
    {
        if (!isRotating)
        {
            isRotating = true;
            rotationStartTime = Time.time;
            currentAngle = transform.rotation.eulerAngles.z;
            targetAngle = currentAngle - 90f;
            
            if (showDebug)
            {
                Debug.Log($"Starting rotation: {currentAngle} -> {targetAngle}");
            }
        }
    }

    private void Update()
    {
        if (isRotating)
        {
            float elapsedTime = Time.time - rotationStartTime;
            float t = Mathf.Clamp01(elapsedTime / rotationDuration);
            
            // Плавное вращение
            float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, t);
            transform.rotation = Quaternion.Euler(0, 0, newAngle);
            
            // Обновляем позицию
            PositionOnFloor();
            
            if (t >= 1f)
            {
                isRotating = false;
                currentAngle = targetAngle;
                
                if (showDebug)
                {
                    Debug.Log($"Rotation complete. Current angle: {currentAngle}");
                }
            }
        }
    }

    private void PositionOnFloor()
    {
        // Получаем текущий угол и нормализуем его
        float angle = transform.rotation.eulerAngles.z;
        angle = Mathf.Repeat(angle, 360f);
        
        // Определяем, какая сторона внизу
        float currentHeight;
        if ((angle >= 0f && angle < 45f) || (angle >= 315f && angle < 360f) ||
            (angle >= 135f && angle < 225f))
        {
            currentHeight = characterHeight; // Короткая сторона внизу
        }
        else
        {
            currentHeight = characterWidth; // Длинная сторона внизу
        }
        
        // Устанавливаем позицию
        float centerY = floorLevel + (currentHeight / 2f);
        transform.position = new Vector3(transform.position.x, centerY, transform.position.z);
        
        if (showDebug)
        {
            Debug.Log($"Positioning: Angle={angle}, Height={currentHeight}, CenterY={centerY}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;
        
        // Линия пола
        Gizmos.color = Color.red;
        Vector3 left = new Vector3(transform.position.x - 2f, floorLevel, transform.position.z);
        Vector3 right = new Vector3(transform.position.x + 2f, floorLevel, transform.position.z);
        Gizmos.DrawLine(left, right);
        
        // Текущая высота
        Gizmos.color = Color.green;
        float currentHeight = GetCurrentHeight();
        float halfHeight = currentHeight / 2f;
        Vector3 center = transform.position;
        Vector3 bottom = new Vector3(center.x, center.y - halfHeight, center.z);
        Vector3 top = new Vector3(center.x, center.y + halfHeight, center.z);
        Gizmos.DrawLine(bottom, top);
    }

    private float GetCurrentHeight()
    {
        float angle = transform.rotation.eulerAngles.z;
        angle = Mathf.Repeat(angle, 360f);
        
        if ((angle >= 0f && angle < 45f) || (angle >= 315f && angle < 360f) ||
            (angle >= 135f && angle < 225f))
        {
            return characterHeight;
        }
        return characterWidth;
    }
} 
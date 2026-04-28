using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class EnvironmentScroller : MonoBehaviour
{
    [System.Serializable]
    public class ParallaxLayer
    {
        public Transform transform;
        public float scrollSpeed;
        public float width;
        public Transform[] segments;
        public SpriteRenderer[] renderers;
        public Sprite sprite;
        public int sortingOrder;
    }
    
    [Header("Background Settings")]
    [SerializeField] private List<ParallaxLayer> parallaxLayers = new List<ParallaxLayer>();
    [SerializeField] private float baseScrollSpeed = 1f;
    [SerializeField] private float stepDistance = 1f;
    [SerializeField] private float moveDuration = 0.3f;
    
    [Header("Ground Settings")]
    [SerializeField] private Transform groundLayer;
    [SerializeField] private float groundScrollSpeed = 1f;
    [SerializeField] private float groundWidth = 20f;
    [SerializeField] private Transform[] groundSegments;
    [SerializeField] private SpriteRenderer[] groundRenderers;
    [SerializeField] private Sprite groundSprite;
    [SerializeField] private int groundSortingOrder;
    
    private Camera mainCamera;
    private float cameraWidth;
    private float cameraHeight;
    private bool isMoving;
    
    private void Awake()
    {
        mainCamera = Camera.main;
        UpdateCameraSize();
    }
    
    private void Start()
    {
        // Initialize segments for each layer
        foreach (var layer in parallaxLayers)
        {
            if (layer.transform != null)
            {
                // Store sprite and sorting order before removing SpriteRenderer
                SpriteRenderer originalRenderer = layer.transform.GetComponent<SpriteRenderer>();
                if (originalRenderer != null)
                {
                    layer.sprite = originalRenderer.sprite;
                    layer.sortingOrder = originalRenderer.sortingOrder;
                    Destroy(originalRenderer);
                }

                float layerScale = GetCoverScale(layer.sprite);
                layer.width = GetScaledSpriteWidth(layer.sprite, layerScale);
                float layerLeftOffset = GetScaledSpriteLeftOffset(layer.sprite, layerScale);
                float layerCameraLeft = GetCameraLeftX(layer.transform);
                
                // Create just two segments that will loop
                layer.segments = new Transform[2];
                layer.renderers = new SpriteRenderer[2];
                
                for (int i = 0; i < 2; i++)
                {
                    GameObject segment = new GameObject($"Segment_{i}");
                    segment.transform.SetParent(layer.transform);
                    SpriteRenderer sr = segment.AddComponent<SpriteRenderer>();
                    sr.sprite = layer.sprite;
                    sr.sortingOrder = layer.sortingOrder;
                    layer.segments[i] = segment.transform;
                    layer.renderers[i] = sr;
                    segment.transform.localScale = new Vector3(layerScale, layerScale, 1f);
                    
                    // Position segments side by side
                    segment.transform.localPosition = new Vector3(layerCameraLeft - layerLeftOffset + i * layer.width, 0, 0);
                }
            }
        }
        
        // Initialize ground segments
        if (groundLayer != null)
        {
            // Store ground sprite and sorting order
            SpriteRenderer groundRenderer = groundLayer.GetComponent<SpriteRenderer>();
            if (groundRenderer != null)
            {
                groundSprite = groundRenderer.sprite;
                groundSortingOrder = groundRenderer.sortingOrder;
                Destroy(groundRenderer);
            }

            float groundScale = GetCoverScale(groundSprite);
            groundWidth = GetScaledSpriteWidth(groundSprite, groundScale);
            float groundLeftOffset = GetScaledSpriteLeftOffset(groundSprite, groundScale);
            float groundCameraLeft = GetCameraLeftX(groundLayer);
            
            // Create just two ground segments that will loop
            groundSegments = new Transform[2];
            groundRenderers = new SpriteRenderer[2];
            
            for (int i = 0; i < 2; i++)
            {
                GameObject segment = new GameObject($"GroundSegment_{i}");
                segment.transform.SetParent(groundLayer);
                SpriteRenderer sr = segment.AddComponent<SpriteRenderer>();
                sr.sprite = groundSprite;
                sr.sortingOrder = groundSortingOrder;
                groundSegments[i] = segment.transform;
                groundRenderers[i] = sr;
                segment.transform.localScale = new Vector3(groundScale, groundScale, 1f);
                
                // Position ground segments side by side
                segment.transform.localPosition = new Vector3(groundCameraLeft - groundLeftOffset + i * groundWidth, 0, 0);
            }
        }
        
        // Subscribe to input events
        InputManager.Instance.OnAnyKeyPressed += TriggerScroll;
    }

    private void UpdateCameraSize()
    {
        if (mainCamera == null)
        {
            cameraHeight = 0f;
            cameraWidth = 0f;
            return;
        }

        cameraHeight = 2f * mainCamera.orthographicSize;
        cameraWidth = cameraHeight * mainCamera.aspect;
    }

    private float GetCoverScale(Sprite sprite)
    {
        if (sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f)
        {
            return 1f;
        }

        UpdateCameraSize();

        if (cameraWidth <= 0f || cameraHeight <= 0f)
        {
            return 1f;
        }

        float scaleX = cameraWidth / sprite.bounds.size.x;
        float scaleY = cameraHeight / sprite.bounds.size.y;
        return Mathf.Max(scaleX, scaleY);
    }

    private float GetScaledSpriteWidth(Sprite sprite, float scale)
    {
        if (sprite == null)
        {
            return cameraWidth;
        }

        return sprite.bounds.size.x * scale;
    }

    private float GetScaledSpriteLeftOffset(Sprite sprite, float scale)
    {
        if (sprite == null)
        {
            return 0f;
        }

        return sprite.bounds.min.x * scale;
    }

    private float GetScaledSpriteRightOffset(Sprite sprite, float scale)
    {
        if (sprite == null)
        {
            return 0f;
        }

        return sprite.bounds.max.x * scale;
    }

    private float GetCameraLeftX(Transform relativeTo)
    {
        UpdateCameraSize();

        if (mainCamera == null)
        {
            return -cameraWidth * 0.5f;
        }

        float cameraLeftWorldX = mainCamera.transform.position.x - cameraWidth * 0.5f;
        if (relativeTo == null)
        {
            return cameraLeftWorldX;
        }

        return relativeTo.InverseTransformPoint(new Vector3(cameraLeftWorldX, 0f, 0f)).x;
    }
    
    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnAnyKeyPressed -= TriggerScroll;
        }
    }
    
    private void TriggerScroll()
    {
        if (!isMoving)
        {
            StartCoroutine(StepScroll());
        }
    }
    
    private IEnumerator StepScroll()
    {
        isMoving = true;
        
        // Store initial positions for all layers
        Vector3[][] startPositions = new Vector3[parallaxLayers.Count][];
        Vector3[][] targetPositions = new Vector3[parallaxLayers.Count][];
        bool[][] isResetting = new bool[parallaxLayers.Count][];
        
        // Calculate target positions for all layers
        for (int layerIndex = 0; layerIndex < parallaxLayers.Count; layerIndex++)
        {
            var layer = parallaxLayers[layerIndex];
            if (layer.segments != null)
            {
                float cameraLeft = GetCameraLeftX(layer.transform);
                startPositions[layerIndex] = new Vector3[2];
                targetPositions[layerIndex] = new Vector3[2];
                isResetting[layerIndex] = new bool[2];
                
                for (int i = 0; i < 2; i++)
                {
                    startPositions[layerIndex][i] = layer.segments[i].localPosition;
                    targetPositions[layerIndex][i] = startPositions[layerIndex][i] + 
                        Vector3.left * stepDistance * layer.scrollSpeed;
                    
                    // Check if segment needs to be reset
                    float rightEdge = targetPositions[layerIndex][i].x + 
                        GetScaledSpriteRightOffset(layer.sprite, layer.segments[i].localScale.x);
                    isResetting[layerIndex][i] = rightEdge <= cameraLeft;
                }
            }
        }
        
        // Calculate ground target positions
        Vector3[] groundStartPositions = new Vector3[2];
        Vector3[] groundTargetPositions = new Vector3[2];
        bool[] groundIsResetting = new bool[2];
        
        if (groundSegments != null)
        {
            float cameraLeft = GetCameraLeftX(groundLayer);
            for (int i = 0; i < 2; i++)
            {
                groundStartPositions[i] = groundSegments[i].localPosition;
                groundTargetPositions[i] = groundStartPositions[i] + 
                    Vector3.left * stepDistance * groundScrollSpeed;
                
                // Check if segment needs to be reset
                float rightEdge = groundTargetPositions[i].x + 
                    GetScaledSpriteRightOffset(groundSprite, groundSegments[i].localScale.x);
                groundIsResetting[i] = rightEdge <= cameraLeft;
            }
        }
        
        // Animate all layers simultaneously
        float elapsedTime = 0f;
        while (elapsedTime < moveDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / moveDuration);
            
            // Move all parallax layers
            for (int layerIndex = 0; layerIndex < parallaxLayers.Count; layerIndex++)
            {
                var layer = parallaxLayers[layerIndex];
                if (layer.segments != null)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        layer.segments[i].localPosition = Vector3.Lerp(
                            startPositions[layerIndex][i],
                            targetPositions[layerIndex][i],
                            t
                        );
                    }
                }
            }
            
            // Move ground segments
            if (groundSegments != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    groundSegments[i].localPosition = Vector3.Lerp(
                        groundStartPositions[i],
                        groundTargetPositions[i],
                        t
                    );
                }
            }
            
            yield return null;
        }
        
        // Ensure final positions are exact and re-enable renderers
        for (int layerIndex = 0; layerIndex < parallaxLayers.Count; layerIndex++)
        {
            var layer = parallaxLayers[layerIndex];
            if (layer.segments != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector3 finalPosition = targetPositions[layerIndex][i];
                    if (isResetting[layerIndex][i])
                    {
                        finalPosition.x += layer.width * 2;
                    }

                    layer.segments[i].localPosition = finalPosition;
                    layer.renderers[i].enabled = true;
                }
            }
        }
        
        if (groundSegments != null)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector3 finalPosition = groundTargetPositions[i];
                if (groundIsResetting[i])
                {
                    finalPosition.x += groundWidth * 2;
                }

                groundSegments[i].localPosition = finalPosition;
                groundRenderers[i].enabled = true;
            }
        }
        
        isMoving = false;
    }
} 

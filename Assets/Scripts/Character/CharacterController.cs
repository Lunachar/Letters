using UnityEngine;
using DG.Tweening;

public class CharacterController : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private Animator animator;
    [SerializeField] private float walkAnimationDuration = 0.3f;
    
    [Header("Character Settings")]
    [SerializeField] private Transform characterTransform;
    [SerializeField] private float walkDistance = 1.0f;
    
    private Vector3 startPosition;
    private bool isWalking;
    
    private void Awake()
    {
        Debug.Log("CharacterController: Awake called");
        if (characterTransform == null)
        {
            characterTransform = transform;
            Debug.Log("CharacterController: Using self transform");
        }
        
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator != null)
            {
                Debug.Log("CharacterController: Found Animator component");
            }
            else
            {
                Debug.Log("CharacterController: No Animator component found - animations will be disabled");
            }
        }
    }
    
    private void Start()
    {
        Debug.Log("CharacterController: Start called");
        if (characterTransform == null)
        {
            Debug.LogError("CharacterController: characterTransform is not assigned!");
            return;
        }
        
        startPosition = characterTransform.position;
        Debug.Log($"CharacterController: Start position set to {startPosition}");
        
        // Subscribe to input events
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnAnyKeyPressed += TriggerWalk;
            Debug.Log("CharacterController: Subscribed to input events");
        }
        else
        {
            Debug.LogError("CharacterController: InputManager.Instance is null!");
        }
    }
    
    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnAnyKeyPressed -= TriggerWalk;
            Debug.Log("CharacterController: Unsubscribed from input events");
        }
    }
    
    private void TriggerWalk()
    {
        Debug.Log("CharacterController: TriggerWalk called");
        if (isWalking)
        {
            Debug.Log("CharacterController: Already walking, ignoring trigger");
            return;
        }
        
        if (characterTransform == null)
        {
            Debug.LogError("CharacterController: characterTransform is null!");
            return;
        }
        
        isWalking = true;
        if (animator != null)
        {
            animator.SetBool("IsWalking", true);
            Debug.Log("CharacterController: Started walking animation");
        }
        
        // Only trigger animation, character stays centered
        DOVirtual.DelayedCall(walkAnimationDuration, () => {
            Debug.Log("CharacterController: Animation completed");
            isWalking = false;
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
            }
        });
    }
} 
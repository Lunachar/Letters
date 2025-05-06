using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;

public class LetterDisplayManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ContentDatabase contentDatabase;
    [SerializeField] private Image letterImage;
    [SerializeField] private Image objectImage;
    
    [Header("Animation Settings")]
    [SerializeField] private float showDuration = 0.5f;
    [SerializeField] private float stayDuration = 2f;
    [SerializeField] private float hideDuration = 0.5f;
    
    private bool showObjects = true;
    
    private void Start()
    {
        // Hide images initially
        SetImagesAlpha(0);
        
        // Subscribe to input events
        InputManager.Instance.OnLetterPressed += HandleLetterPressed;
        
        // Subscribe to settings events
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.OnDisplayModeChanged += HandleDisplayModeChanged;
            GameSettingsManager.Instance.OnSoundVolumeChanged += HandleSoundVolumeChanged;
            
            // Initialize with current settings
            showObjects = GameSettingsManager.Instance.CurrentDisplayMode == DisplayMode.SoundAndObjects;
        }
    }
    
    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnLetterPressed -= HandleLetterPressed;
        }
        
        if (GameSettingsManager.Instance != null)
        {
            GameSettingsManager.Instance.OnDisplayModeChanged -= HandleDisplayModeChanged;
            GameSettingsManager.Instance.OnSoundVolumeChanged -= HandleSoundVolumeChanged;
        }
        
        // Убираем все анимации при уничтожении
        if (letterImage != null)
        {
            letterImage.DOKill();
        }
        if (objectImage != null)
        {
            objectImage.DOKill();
        }
    }
    
    private void HandleDisplayModeChanged(DisplayMode mode)
    {
        showObjects = mode == DisplayMode.SoundAndObjects;
        
        // Если объекты отключены, сразу скрываем их
        if (!showObjects && objectImage != null)
        {
            objectImage.DOKill();
            SetObjectImageAlpha(0);
        }
    }
    
    private void HandleSoundVolumeChanged(float volume)
    {
        GameManager.Instance.SetSoundVolume(volume);
    }
    
    private void HandleLetterPressed(char letter)
    {
        LetterData data = contentDatabase.GetLetterData(letter);
        if (data == null) return;
        
        // Update statistics
        data.IncrementPressCount();
        contentDatabase.SaveStatistics();
        
        // Stop any running display sequence
        StopAllCoroutines();
        
        // Start new display sequence
        StartCoroutine(DisplaySequence(data));
    }
    
    private IEnumerator DisplaySequence(LetterData data)
    {
        // Setup and show letter image if available
        if (letterImage != null && data.letterSprite != null)
        {
            letterImage.sprite = data.letterSprite;
            letterImage.DOKill();
            letterImage.DOFade(1f, showDuration);
        }
        
        // Setup and show object image if enabled and available
        if (showObjects && objectImage != null && data.objectSprite != null)
        {
            objectImage.sprite = data.objectSprite;
            objectImage.DOKill();
            objectImage.DOFade(1f, showDuration);
        }
        
        // Play letter sound
        GameManager.Instance.PlaySound(data.letterSound);
        
        yield return new WaitForSeconds(stayDuration);
        
        // Play object sound if enabled
        GameManager.Instance.PlaySound(data.objectSound);
        
        yield return new WaitForSeconds(stayDuration);
        
        // Hide letter image if available
        if (letterImage != null)
        {
            letterImage.DOKill();
            letterImage.DOFade(0f, hideDuration);
        }
        
        // Hide object image if available
        if (objectImage != null)
        {
            objectImage.DOKill();
            objectImage.DOFade(0f, hideDuration);
        }
    }
    
    private void SetImagesAlpha(float alpha)
    {
        if (letterImage != null)
        {
            Color color = letterImage.color;
            color.a = alpha;
            letterImage.color = color;
        }
        
        if (objectImage != null)
        {
            SetObjectImageAlpha(alpha);
        }
    }
    
    private void SetObjectImageAlpha(float alpha)
    {
        if (objectImage != null)
        {
            Color color = objectImage.color;
            color.a = alpha;
            objectImage.color = color;
        }
    }
} 
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class SpawnPointSettings : MonoBehaviour
{
    public int fontSize = 100;
    [Range(0f, 1f)] public float alpha = 1f;
    public float appearDuration = 0.5f;

    public void ApplyTo(GameObject letterObject)
    {
        if (letterObject == null) return;

        TMP_Text text = letterObject.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.fontSize = fontSize;

            // set alpha
            Color startColor = text.color;
            startColor.a = 0;
            text.color = startColor;
            text.DOFade(alpha, appearDuration);

            // set appearance animation
            Color targetColor = startColor;
            targetColor.a = alpha;
            text.DOFade(alpha, appearDuration);
            
            // set scale
            Transform t = text.transform;
            t.localScale = Vector3.zero;
            t.DOScale(Vector3.one, appearDuration).SetEase(Ease.OutBack);
        }
    }
}

using System;
using UnityEngine;

public sealed class IndustrialToolAlertMessageSequence : MonoBehaviour
{
    private const float PulsePulsesPerSecond = 1.2f;
    private const float MinScaleMultiplier = 0.94f;
    private const float MaxScaleMultiplier = 1.06f;

    [SerializeField] private RectTransform[] alertMessages;
    [SerializeField] private GameObject screenRoot;

    private Vector3[] initialScales;
    private int activeIndex = -1;

    public int Count => alertMessages != null ? alertMessages.Length : 0;

    public void Configure()
    {
        activeIndex = -1;

        if (alertMessages == null)
        {
            return;
        }

        initialScales = new Vector3[alertMessages.Length];
        for (int i = 0; i < alertMessages.Length; i++)
        {
            if (alertMessages[i] != null)
            {
                initialScales[i] = alertMessages[i].localScale;
            }
            else
            {
                initialScales[i] = Vector3.one;
            }
        }

        HideAll();
    }

    public void SetActiveIndex(int index)
    {
        activeIndex = index;
        if (alertMessages == null)
        {
            return;
        }

        for (int i = 0; i < alertMessages.Length; i++)
        {
            SetActive(i, false);
        }

        if (activeIndex >= 0 && activeIndex < alertMessages.Length)
        {
            SetActive(activeIndex, true);
            if (screenRoot != null)
            {
                screenRoot.SetActive(true);
            }
        }
    }

    private void SetActive(int index, bool active)
    {
        if (alertMessages == null || index < 0 || index >= alertMessages.Length)
        {
            return;
        }

        RectTransform rectTransform = alertMessages[index];
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.localScale = initialScales[index];
        rectTransform.gameObject.SetActive(active);
    }

    public void HideAll()
    {
        activeIndex = -1;
        if (alertMessages != null)
        {
            for (int i = 0; i < alertMessages.Length; i++)
            {
                if (alertMessages[i] != null)
                {
                    alertMessages[i].gameObject.SetActive(false);
                }
            }
        }

        if (screenRoot != null)
        {
            screenRoot.SetActive(false);
        }
    }

    public void UpdatePulse(float time)
    {
        if (activeIndex < 0 || alertMessages == null || activeIndex >= alertMessages.Length)
        {
            return;
        }

        RectTransform rectTransform = alertMessages[activeIndex];
        if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
        {
            return;
        }

        float wave = (Mathf.Sin(time * PulsePulsesPerSecond * Mathf.PI * 2f) + 1f) * 0.5f;
        float scaleMultiplier = Mathf.Lerp(MinScaleMultiplier, MaxScaleMultiplier, wave);
        rectTransform.localScale = initialScales[activeIndex] * scaleMultiplier;
    }
}

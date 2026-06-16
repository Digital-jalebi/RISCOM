using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class RISCOMLanguageToggleController : MonoBehaviour
{
    [SerializeField] private Toggle gujaratiToggle;
    [SerializeField] private bool applyToggleStateOnEnable = true;
    [SerializeField] private bool setNativeSizeAfterSwap;
    [SerializeField] private TextImageLanguageSwap[] textImageSwaps;

    private bool isWired;

    private void Awake()
    {
        CacheInitialSprites();
    }

    private void OnEnable()
    {
        WireToggle();

        if (applyToggleStateOnEnable)
        {
            ApplyLanguage(gujaratiToggle != null && gujaratiToggle.isOn);
        }
    }

    private void OnDisable()
    {
        if (gujaratiToggle != null && isWired)
        {
            gujaratiToggle.onValueChanged.RemoveListener(ApplyLanguage);
            isWired = false;
        }
    }

    public void ApplyLanguage(bool useGujarati)
    {
        if (textImageSwaps == null)
        {
            return;
        }

        for (int i = 0; i < textImageSwaps.Length; i++)
        {
            TextImageLanguageSwap swap = textImageSwaps[i];
            if (swap != null)
            {
                swap.Apply(useGujarati, setNativeSizeAfterSwap);
            }
        }
    }

    public void SetGujaratiEnabled(bool enabled)
    {
        if (gujaratiToggle != null && gujaratiToggle.isOn != enabled)
        {
            gujaratiToggle.isOn = enabled;
            return;
        }

        ApplyLanguage(enabled);
    }

    private void WireToggle()
    {
        if (gujaratiToggle == null || isWired)
        {
            return;
        }

        gujaratiToggle.onValueChanged.AddListener(ApplyLanguage);
        isWired = true;
    }

    private void CacheInitialSprites()
    {
        if (textImageSwaps == null)
        {
            return;
        }

        for (int i = 0; i < textImageSwaps.Length; i++)
        {
            TextImageLanguageSwap swap = textImageSwaps[i];
            if (swap != null)
            {
                swap.CacheInitialSprite();
            }
        }
    }

    [Serializable]
    private sealed class TextImageLanguageSwap
    {
        [SerializeField] private Image textImage;
        [SerializeField] private Sprite englishSprite;
        [SerializeField] private Sprite gujaratiSprite;

        private Sprite initialSprite;

        public void CacheInitialSprite()
        {
            if (textImage != null)
            {
                initialSprite = textImage.sprite;
            }
        }

        public void Apply(bool useGujarati, bool setNativeSize)
        {
            if (textImage == null)
            {
                return;
            }

            Sprite targetSprite = useGujarati
                ? gujaratiSprite
                : englishSprite != null
                    ? englishSprite
                    : initialSprite;

            if (targetSprite == null)
            {
                return;
            }

            textImage.sprite = targetSprite;

            if (setNativeSize)
            {
                textImage.SetNativeSize();
            }
        }
    }
}

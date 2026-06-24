using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class RISCOMLanguageToggleController : MonoBehaviour
{
    [SerializeField] private bool useGujaratiOnStart;
    [SerializeField] private bool applyInitialLanguageOnEnable = true;
    [SerializeField] private bool setNativeSizeAfterSwap;
    [SerializeField, FormerlySerializedAs("textImageSwaps")] private TextImageLanguageSwap[] cycloneTextImageSwaps;
    [SerializeField] private TextImageLanguageSwap[] floodTextImageSwaps;
    [SerializeField] private TextImageLanguageSwap[] droughtTextImageSwaps;
    [SerializeField] private TextImageLanguageSwap[] industrialTextImageSwaps;

    private bool isGujaratiEnabled;

    public event Action<bool> LanguageChanged;
    public bool IsGujaratiEnabled => isGujaratiEnabled;

    private void Awake()
    {
        isGujaratiEnabled = useGujaratiOnStart;
        CacheInitialSprites();
    }

    private void OnEnable()
    {
        if (applyInitialLanguageOnEnable)
        {
            ApplyLanguage(isGujaratiEnabled);
        }
    }

    public void ApplyLanguage(bool useGujarati)
    {
        isGujaratiEnabled = useGujarati;

        ApplySwaps(cycloneTextImageSwaps, useGujarati);
        ApplySwaps(floodTextImageSwaps, useGujarati);
        ApplySwaps(droughtTextImageSwaps, useGujarati);
        ApplySwaps(industrialTextImageSwaps, useGujarati);

        LanguageChanged?.Invoke(useGujarati);
    }

    public void SetGujaratiEnabled(bool enabled)
    {
        ApplyLanguage(enabled);
    }

    public void SelectEnglish()
    {
        ApplyLanguage(false);
    }

    public void SelectGujarati()
    {
        ApplyLanguage(true);
    }

    private void CacheInitialSprites()
    {
        CacheInitialSprites(cycloneTextImageSwaps);
        CacheInitialSprites(floodTextImageSwaps);
        CacheInitialSprites(droughtTextImageSwaps);
        CacheInitialSprites(industrialTextImageSwaps);
    }

    private void ApplySwaps(TextImageLanguageSwap[] swaps, bool useGujarati)
    {
        if (swaps == null)
        {
            return;
        }

        for (int i = 0; i < swaps.Length; i++)
        {
            TextImageLanguageSwap swap = swaps[i];
            if (swap != null)
            {
                swap.Apply(useGujarati, setNativeSizeAfterSwap);
            }
        }
    }

    private static void CacheInitialSprites(TextImageLanguageSwap[] swaps)
    {
        if (swaps == null)
        {
            return;
        }

        for (int i = 0; i < swaps.Length; i++)
        {
            TextImageLanguageSwap swap = swaps[i];
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

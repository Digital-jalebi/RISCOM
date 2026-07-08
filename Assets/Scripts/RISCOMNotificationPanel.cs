using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[Serializable]
public sealed class RISCOMNotificationPanel
{
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private Image template;
    [SerializeField] private Scrollbar scrollbar;
    [SerializeField] private bool useNativeSize = true;
    [SerializeField] private float bottomPadding = 1f;
    [SerializeField] private float spacing = 0f;
    [SerializeField] private float scrollWheelSensitivity = 1f;

    private readonly List<Image> activeNotifications = new List<Image>();
    private Vector2 contentStartSize;
    private bool configured;
    private bool scrollbarWired;
    private bool suppressScrollbarCallback;

    public void Configure()
    {
        if (content != null)
        {
            contentStartSize = content.sizeDelta;
        }

        if (template != null)
        {
            template.gameObject.SetActive(false);
        }

        ConfigureScrollbar();
        configured = true;
    }

    public void Clear()
    {
        for (int i = 0; i < activeNotifications.Count; i++)
        {
            Image notification = activeNotifications[i];
            if (notification != null)
            {
                UnityEngine.Object.Destroy(notification.gameObject);
            }
        }

        activeNotifications.Clear();

        if (template != null)
        {
            template.gameObject.SetActive(false);
        }

        if (configured)
        {
            ResetScroll();
        }
    }

    public void Show(Sprite sprite)
    {
        if (template == null)
        {
            return;
        }

        RectTransform parent = content != null ? content : template.transform.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        Image notification = UnityEngine.Object.Instantiate(template, parent);
        notification.gameObject.SetActive(true);
        notification.raycastTarget = false;

        if (sprite != null)
        {
            notification.sprite = sprite;
        }

        if (useNativeSize && notification.sprite != null)
        {
            notification.SetNativeSize();
        }

        notification.rectTransform.SetAsLastSibling();
        activeNotifications.Add(notification);
        ScrollToLatest();
    }

    public void UpdateScrollInput()
    {
        if (!configured || content == null || !content.gameObject.activeInHierarchy)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollY, 0f))
        {
            return;
        }

        ScrollByWheelDelta(scrollY);
    }

    private void ResetScroll()
    {
        if (content == null)
        {
            UpdateScrollbar(0f, 1f);
            return;
        }

        Vector2 anchoredPosition = content.anchoredPosition;
        anchoredPosition.y = 0f;
        content.anchoredPosition = anchoredPosition;
        content.sizeDelta = contentStartSize;
        UpdateScrollbar(0f, 1f);
    }

    private void ScrollToLatest()
    {
        if (content == null)
        {
            return;
        }

        UpdateContentHeight();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        RectTransform targetViewport = GetViewport();
        if (targetViewport == null || targetViewport == content)
        {
            UpdateScrollbar(0f, 1f);
            return;
        }

        float overflow = GetOverflow(targetViewport);
        Vector2 anchoredPosition = content.anchoredPosition;
        anchoredPosition.y = overflow > 0f ? overflow + bottomPadding : 0f;
        content.anchoredPosition = anchoredPosition;
        UpdateScrollbar(overflow, overflow > 0f ? 0f : 1f);
    }

    private void UpdateContentHeight()
    {
        RectTransform targetViewport = GetViewport();
        if (targetViewport == null || targetViewport == content)
        {
            return;
        }

        float contentHeight = 0f;
        for (int i = 0; i < activeNotifications.Count; i++)
        {
            Image notification = activeNotifications[i];
            if (notification == null)
            {
                continue;
            }

            contentHeight += notification.rectTransform.rect.height;
            if (i < activeNotifications.Count - 1)
            {
                contentHeight += spacing;
            }
        }

        float preferredHeight = LayoutUtility.GetPreferredHeight(content);
        if (preferredHeight > 0f)
        {
            contentHeight = Mathf.Max(contentHeight, preferredHeight);
        }

        Vector2 size = content.sizeDelta;
        size.y = Mathf.Max(contentStartSize.y, targetViewport.rect.height, contentHeight);
        content.sizeDelta = size;
    }

    private void ConfigureScrollbar()
    {
        if (scrollbar == null)
        {
            return;
        }

        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        if (!scrollbarWired)
        {
            scrollbar.onValueChanged.AddListener(HandleScrollbarChanged);
            scrollbarWired = true;
        }

        UpdateScrollbar(0f, 1f);
    }

    private void HandleScrollbarChanged(float value)
    {
        if (suppressScrollbarCallback || content == null)
        {
            return;
        }

        RectTransform targetViewport = GetViewport();
        if (targetViewport == null || targetViewport == content)
        {
            return;
        }

        UpdateContentHeight();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float overflow = GetOverflow(targetViewport);
        if (overflow <= 0f)
        {
            return;
        }

        Vector2 anchoredPosition = content.anchoredPosition;
        anchoredPosition.y = Mathf.Lerp(overflow + bottomPadding, 0f, value);
        content.anchoredPosition = anchoredPosition;
    }

    private void ScrollByWheelDelta(float scrollY)
    {
        RectTransform targetViewport = GetViewport();
        if (targetViewport == null || targetViewport == content)
        {
            return;
        }

        UpdateContentHeight();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float overflow = GetOverflow(targetViewport);
        if (overflow <= 0f)
        {
            UpdateScrollbar(0f, 1f);
            return;
        }

        float maxScroll = overflow + bottomPadding;
        Vector2 anchoredPosition = content.anchoredPosition;
        anchoredPosition.y = Mathf.Clamp(
            anchoredPosition.y - scrollY * scrollWheelSensitivity,
            0f,
            maxScroll);
        content.anchoredPosition = anchoredPosition;

        SetScrollbarValue(GetScrollbarValueForContentY(anchoredPosition.y, overflow));
    }

    private float GetScrollbarValueForContentY(float contentY, float overflow)
    {
        return overflow > 0f ? Mathf.InverseLerp(overflow + bottomPadding, 0f, contentY) : 1f;
    }

    private RectTransform GetViewport()
    {
        return viewport != null
            ? viewport
            : content != null
                ? content.parent as RectTransform
                : null;
    }

    private float GetOverflow(RectTransform targetViewport)
    {
        if (content == null || targetViewport == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, content.rect.height - targetViewport.rect.height);
    }

    private void UpdateScrollbar(float overflow, float normalizedPosition)
    {
        if (scrollbar == null)
        {
            return;
        }

        RectTransform targetViewport = GetViewport();
        float contentHeight = content != null ? content.rect.height : 0f;
        float viewportHeight = targetViewport != null ? targetViewport.rect.height : 0f;
        bool canScroll = overflow > 0f && contentHeight > 0f && viewportHeight > 0f;

        scrollbar.interactable = canScroll;
        scrollbar.size = canScroll ? Mathf.Clamp01(viewportHeight / contentHeight) : 1f;
        SetScrollbarValue(canScroll ? normalizedPosition : 1f);
    }

    private void SetScrollbarValue(float value)
    {
        if (scrollbar == null)
        {
            return;
        }

        suppressScrollbarCallback = true;
        scrollbar.value = Mathf.Clamp01(value);
        suppressScrollbarCallback = false;
    }
}

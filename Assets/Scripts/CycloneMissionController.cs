using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class CycloneMissionController : MonoBehaviour
{
    private const int TotalMarkers = 7;
    private const float MissionDurationSeconds = 600f;

    [SerializeField] private GameObject missionBackground;
    [SerializeField] private RectTransform markerTransform;
    [SerializeField] private RectTransform dragMarkerTransform;
    [SerializeField] private Image markerImage;
    [SerializeField] private Image dragMarkerImage;
    [SerializeField] private Sprite dragMarkerSprite;
    [SerializeField, Range(0.1f, 2f)] private float dragMarkerSizeMultiplier = 0.85f;
    [SerializeField] private CycloneDistrictMarkerDragHandler markerDragHandler;
    [SerializeField] private CycloneMapDropZone[] dropZones;
    [SerializeField] private Slider missionProgressSlider;
    [SerializeField] private Slider timeRemainingSlider;
    [SerializeField] private TextMeshProUGUI timeRemainingLabel;
    [SerializeField] private RISCOMLanguageToggleController languageToggleController;
    [SerializeField] private RectTransform notificationViewport;
    [SerializeField] private RectTransform notificationContent;
    [SerializeField] private Image notificationTemplate;
    [SerializeField] private Scrollbar notificationScrollbar;
    [SerializeField] private Sprite[] notificationSprites;
    [SerializeField] private Sprite[] gujaratiNotificationSprites;
    [SerializeField] private bool useNotificationNativeSize = true;
    [SerializeField] private float notificationBottomPadding = 1f;
    [SerializeField] private float notificationSpacing = 0f;
    [SerializeField] private float notificationScrollWheelSensitivity = 1f;

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private readonly List<Image> activeNotifications = new List<Image>();
    [SerializeField] private CycloneToolAlertMessageSequence toolAlertMessages;

    private Vector2 notificationContentStartSize;
    private bool notificationScrollbarWired;
    private bool suppressNotificationScrollbarCallback;
    private Vector2 markerStartPosition;
    private Coroutine shakeRoutine;
    private Action onMissionComplete;
    private int markersRemaining;
    private int placedMarkers;
    private float timeRemaining;
    private bool isConfigured;
    private bool isRunning;
    private bool isComplete;

    public void Configure()
    {
        if (isConfigured)
        {
            return;
        }

        if (markerTransform != null)
        {
            markerStartPosition = markerTransform.anchoredPosition;
            if (markerImage != null)
            {
                markerImage.raycastTarget = true;
            }

            if (markerDragHandler == null)
            {
                Debug.LogWarning("Cyclone mission is missing its marker drag handler reference.");
            }
        }

        if (dragMarkerTransform != null)
        {
            dragMarkerTransform.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Mission 1 Game is missing DraggingDistrictMarker.");
        }

        CacheDropZones();
        ConfigureSliders();
        ConfigureNotifications();
        toolAlertMessages?.Configure();

        isConfigured = true;
    }

    public void BeginMission(Action onComplete)
    {
        Configure();

        onMissionComplete = onComplete;
        markersRemaining = TotalMarkers;
        placedMarkers = 0;
        timeRemaining = MissionDurationSeconds;
        isRunning = true;
        isComplete = false;

        SetActive(missionBackground, true);
        ResetDropZones();
        ClearNotifications();
        DestroyDragMarker();
        ResetMarkerPosition();
        SetMarkerVisible(true);
        toolAlertMessages?.SetActiveIndex(0);
        UpdateProgress();
        UpdateTimerDisplay();
    }

    private void Update()
    {
        if (!isRunning || isComplete)
        {
            return;
        }

        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerDisplay();
        UpdateNotificationScrollInput();
        toolAlertMessages?.UpdatePulse(Time.unscaledTime);

        if (timeRemaining <= 0f)
        {
            isRunning = false;
            DestroyDragMarker();
            toolAlertMessages?.HideAll();
            Debug.LogWarning("Cyclone mission timer expired.");
        }
    }

    public void MoveMarker(PointerEventData eventData)
    {
        if (!isRunning || dragMarkerTransform == null || dragMarkerTransform.parent == null)
        {
            return;
        }

        RectTransform parentRect = dragMarkerTransform.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint))
        {
            dragMarkerTransform.anchoredPosition = localPoint;
        }
    }

    public void TryPlaceMarker(PointerEventData eventData)
    {
        if (!isRunning || markersRemaining <= 0)
        {
            DestroyDragMarker();
            return;
        }

        CycloneMapDropZone dropZone = FindDropZoneUnderPointer(eventData);
        DestroyDragMarker();

        if (dropZone != null && dropZone.CanAcceptMarker)
        {
            PlaceMarker(dropZone);
            return;
        }

        ResetMarkerWithShake();
    }

    public void BeginMarkerDrag(PointerEventData eventData)
    {
        if (!isRunning || markerTransform == null || markersRemaining <= 0)
        {
            return;
        }

        CreateDragMarker();
        MoveMarker(eventData);
    }

    private void PlaceMarker(CycloneMapDropZone dropZone)
    {
        dropZone.MarkPlaced();
        placedMarkers++;
        markersRemaining--;
        UpdateProgress();
        ShowPlacementNotification(dropZone);

        if (placedMarkers >= TotalMarkers)
        {
            CompleteMission();
            return;
        }

        ResetMarkerPosition();
        SetMarkerVisible(markersRemaining > 0);
    }

    private void CompleteMission()
    {
        isComplete = true;
        isRunning = false;
        DestroyDragMarker();
        SetMarkerVisible(false);
        SetActive(missionBackground, false);
        toolAlertMessages?.HideAll();
        onMissionComplete?.Invoke();
    }

    private CycloneMapDropZone FindDropZoneUnderPointer(PointerEventData eventData)
    {
        if (EventSystem.current == null)
        {
            return null;
        }

        raycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, raycastResults);

        for (int i = 0; i < raycastResults.Count; i++)
        {
            CycloneMapDropZone dropZone = FindDropZoneFor(raycastResults[i].gameObject);
            if (dropZone != null)
            {
                return dropZone;
            }
        }

        return null;
    }

    private CycloneMapDropZone FindDropZoneFor(GameObject raycastTarget)
    {
        if (dropZones == null)
        {
            return null;
        }

        for (int i = 0; i < dropZones.Length; i++)
        {
            CycloneMapDropZone dropZone = dropZones[i];
            if (dropZone != null && dropZone.Contains(raycastTarget))
            {
                return dropZone;
            }
        }

        return null;
    }

    private void CacheDropZones()
    {
        if (dropZones == null)
        {
            return;
        }

        for (int i = 0; i < dropZones.Length; i++)
        {
            CycloneMapDropZone zone = dropZones[i];
            if (zone != null)
            {
                zone.Configure();
            }
        }
    }

    private void ConfigureSliders()
    {
        if (missionProgressSlider != null)
        {
            missionProgressSlider.minValue = 0f;
            missionProgressSlider.maxValue = TotalMarkers;
            missionProgressSlider.wholeNumbers = true;
            missionProgressSlider.interactable = false;
        }

        if (timeRemainingSlider != null)
        {
            timeRemainingSlider.minValue = 0f;
            timeRemainingSlider.maxValue = MissionDurationSeconds;
            timeRemainingSlider.interactable = false;
        }
    }

    private void ConfigureNotifications()
    {
        if (notificationContent != null)
        {
            notificationContentStartSize = notificationContent.sizeDelta;
        }

        if (notificationTemplate != null)
        {
            notificationTemplate.gameObject.SetActive(false);
        }

        ConfigureNotificationScrollbar();
    }

    private void ClearNotifications()
    {
        for (int i = 0; i < activeNotifications.Count; i++)
        {
            Image notification = activeNotifications[i];
            if (notification != null)
            {
                Destroy(notification.gameObject);
            }
        }

        activeNotifications.Clear();

        if (notificationTemplate != null)
        {
            notificationTemplate.gameObject.SetActive(false);
        }

        ResetNotificationScroll();
    }

    private void ShowPlacementNotification(CycloneMapDropZone dropZone)
    {
        if (notificationTemplate == null)
        {
            return;
        }

        RectTransform parent = notificationContent != null
            ? notificationContent
            : notificationTemplate.transform.parent as RectTransform;

        if (parent == null)
        {
            return;
        }

        Image notification = Instantiate(notificationTemplate, parent);
        notification.gameObject.SetActive(true);
        notification.raycastTarget = false;

        Sprite sprite = GetNotificationSprite(dropZone);
        if (sprite != null)
        {
            notification.sprite = sprite;
        }

        if (useNotificationNativeSize && notification.sprite != null)
        {
            notification.SetNativeSize();
        }

        notification.rectTransform.SetAsLastSibling();
        activeNotifications.Add(notification);
        ScrollNotificationsToLatest();
    }

    private Sprite GetNotificationSprite(CycloneMapDropZone dropZone)
    {
        bool useGujarati = IsGujaratiEnabled();
        int notificationIndex = GetDropZoneIndex(dropZone);

        if (useGujarati)
        {
            if (dropZone != null && dropZone.GujaratiNotificationSprite != null)
            {
                return dropZone.GujaratiNotificationSprite;
            }

            Sprite gujaratiSprite = GetNotificationSpriteAt(gujaratiNotificationSprites, notificationIndex);
            if (gujaratiSprite != null)
            {
                return gujaratiSprite;
            }
        }

        if (dropZone != null && dropZone.NotificationSprite != null)
        {
            return dropZone.NotificationSprite;
        }

        Sprite englishSprite = GetNotificationSpriteAt(notificationSprites, notificationIndex);
        if (englishSprite != null)
        {
            return englishSprite;
        }

        return notificationTemplate != null ? notificationTemplate.sprite : null;
    }

    private static Sprite GetNotificationSpriteAt(Sprite[] sprites, int index)
    {
        return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }

    private bool IsGujaratiEnabled()
    {
        return languageToggleController != null && languageToggleController.IsGujaratiEnabled;
    }

    private int GetDropZoneIndex(CycloneMapDropZone dropZone)
    {
        if (dropZone == null || dropZones == null)
        {
            return -1;
        }

        for (int i = 0; i < dropZones.Length; i++)
        {
            if (dropZones[i] == dropZone)
            {
                return i;
            }
        }

        return -1;
    }

    private void ResetNotificationScroll()
    {
        if (notificationContent == null)
        {
            UpdateNotificationScrollbar(0f, 1f);
            return;
        }

        Vector2 anchoredPosition = notificationContent.anchoredPosition;
        anchoredPosition.y = 0f;
        notificationContent.anchoredPosition = anchoredPosition;
        notificationContent.sizeDelta = notificationContentStartSize;
        UpdateNotificationScrollbar(0f, 1f);
    }

    private void ScrollNotificationsToLatest()
    {
        if (notificationContent == null)
        {
            return;
        }

        UpdateNotificationContentHeight();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(notificationContent);

        RectTransform viewport = GetNotificationViewport();

        if (viewport == null || viewport == notificationContent)
        {
            UpdateNotificationScrollbar(0f, 1f);
            return;
        }

        float overflow = GetNotificationOverflow(viewport);
        Vector2 anchoredPosition = notificationContent.anchoredPosition;
        anchoredPosition.y = overflow > 0f ? overflow + notificationBottomPadding : 0f;
        notificationContent.anchoredPosition = anchoredPosition;
        UpdateNotificationScrollbar(overflow, overflow > 0f ? 0f : 1f);
    }

    private void UpdateNotificationContentHeight()
    {
        RectTransform viewport = GetNotificationViewport();

        if (viewport == null || viewport == notificationContent)
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
                contentHeight += notificationSpacing;
            }
        }

        float preferredHeight = LayoutUtility.GetPreferredHeight(notificationContent);
        if (preferredHeight > 0f)
        {
            contentHeight = Mathf.Max(contentHeight, preferredHeight);
        }

        Vector2 size = notificationContent.sizeDelta;
        size.y = Mathf.Max(notificationContentStartSize.y, viewport.rect.height, contentHeight);
        notificationContent.sizeDelta = size;
    }

    private void ConfigureNotificationScrollbar()
    {
        if (notificationScrollbar == null)
        {
            return;
        }

        notificationScrollbar.direction = Scrollbar.Direction.BottomToTop;

        if (!notificationScrollbarWired)
        {
            notificationScrollbar.onValueChanged.AddListener(HandleNotificationScrollbarChanged);
            notificationScrollbarWired = true;
        }

        UpdateNotificationScrollbar(0f, 1f);
    }

    private void HandleNotificationScrollbarChanged(float value)
    {
        if (suppressNotificationScrollbarCallback || notificationContent == null)
        {
            return;
        }

        RectTransform viewport = GetNotificationViewport();
        if (viewport == null || viewport == notificationContent)
        {
            return;
        }

        UpdateNotificationContentHeight();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(notificationContent);

        float overflow = GetNotificationOverflow(viewport);
        if (overflow <= 0f)
        {
            return;
        }

        Vector2 anchoredPosition = notificationContent.anchoredPosition;
        anchoredPosition.y = Mathf.Lerp(overflow + notificationBottomPadding, 0f, value);
        notificationContent.anchoredPosition = anchoredPosition;
    }

    private void UpdateNotificationScrollInput()
    {
        if (notificationContent == null || !notificationContent.gameObject.activeInHierarchy)
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

        RectTransform viewport = GetNotificationViewport();
        if (viewport == null || viewport == notificationContent)
        {
            return;
        }

        UpdateNotificationContentHeight();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(notificationContent);

        float overflow = GetNotificationOverflow(viewport);
        if (overflow <= 0f)
        {
            UpdateNotificationScrollbar(0f, 1f);
            return;
        }

        float maxScroll = overflow + notificationBottomPadding;
        Vector2 anchoredPosition = notificationContent.anchoredPosition;
        anchoredPosition.y = Mathf.Clamp(
            anchoredPosition.y - scrollY * notificationScrollWheelSensitivity,
            0f,
            maxScroll);
        notificationContent.anchoredPosition = anchoredPosition;
        SetNotificationScrollbarValue(GetNotificationScrollbarValueForContentY(anchoredPosition.y, overflow));
    }

    private float GetNotificationScrollbarValueForContentY(float contentY, float overflow)
    {
        return overflow > 0f ? Mathf.InverseLerp(overflow + notificationBottomPadding, 0f, contentY) : 1f;
    }

    private RectTransform GetNotificationViewport()
    {
        return notificationViewport != null
            ? notificationViewport
            : notificationContent != null
                ? notificationContent.parent as RectTransform
                : null;
    }

    private float GetNotificationOverflow(RectTransform viewport)
    {
        if (notificationContent == null || viewport == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, notificationContent.rect.height - viewport.rect.height);
    }

    private void UpdateNotificationScrollbar(float overflow, float normalizedPosition)
    {
        if (notificationScrollbar == null)
        {
            return;
        }

        RectTransform viewport = GetNotificationViewport();
        float contentHeight = notificationContent != null ? notificationContent.rect.height : 0f;
        float viewportHeight = viewport != null ? viewport.rect.height : 0f;
        bool canScroll = overflow > 0f && contentHeight > 0f && viewportHeight > 0f;

        notificationScrollbar.interactable = canScroll;
        notificationScrollbar.size = canScroll ? Mathf.Clamp01(viewportHeight / contentHeight) : 1f;
        SetNotificationScrollbarValue(canScroll ? normalizedPosition : 1f);
    }

    private void SetNotificationScrollbarValue(float value)
    {
        if (notificationScrollbar == null)
        {
            return;
        }

        suppressNotificationScrollbarCallback = true;
        notificationScrollbar.value = Mathf.Clamp01(value);
        suppressNotificationScrollbarCallback = false;
    }

    private void ResetDropZones()
    {
        if (dropZones == null)
        {
            return;
        }

        for (int i = 0; i < dropZones.Length; i++)
        {
            if (dropZones[i] != null)
            {
                dropZones[i].ResetZone();
            }
        }
    }

    private void UpdateProgress()
    {
        if (missionProgressSlider != null)
        {
            missionProgressSlider.value = placedMarkers;
        }
    }

    private void UpdateTimerDisplay()
    {
        if (timeRemainingSlider != null)
        {
            timeRemainingSlider.value = timeRemaining;
        }

        if (timeRemainingLabel != null)
        {
            int roundedSeconds = Mathf.CeilToInt(timeRemaining);
            int minutes = roundedSeconds / 60;
            int seconds = roundedSeconds % 60;
            timeRemainingLabel.text = $"{minutes:00}:{seconds:00}s";
        }
    }

    private void ResetMarkerWithShake()
    {
        DestroyDragMarker();
        ResetMarkerPosition();

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
        }

        shakeRoutine = StartCoroutine(ShakeMarker());
    }

    private IEnumerator ShakeMarker()
    {
        if (markerTransform == null)
        {
            yield break;
        }

        const float duration = 0.25f;
        const float frequency = 48f;
        const float amplitude = 12f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * frequency) * amplitude;
            markerTransform.anchoredPosition = markerStartPosition + new Vector2(offset, 0f);
            yield return null;
        }

        markerTransform.anchoredPosition = markerStartPosition;
        shakeRoutine = null;
    }

    private void ResetMarkerPosition()
    {
        if (markerTransform != null)
        {
            markerTransform.anchoredPosition = markerStartPosition;
        }
    }

    private void SetMarkerVisible(bool visible)
    {
        if (markerTransform != null)
        {
            markerTransform.gameObject.SetActive(visible);
        }
    }

    private void CreateDragMarker()
    {
        DestroyDragMarker();

        if (dragMarkerTransform == null || markerTransform == null)
        {
            return;
        }

        dragMarkerTransform.gameObject.SetActive(true);
        dragMarkerTransform.position = markerTransform.position;
        dragMarkerTransform.SetAsLastSibling();

        if (dragMarkerImage != null)
        {
            Sprite sprite = dragMarkerSprite != null ? dragMarkerSprite : markerImage != null ? markerImage.sprite : null;
            dragMarkerImage.sprite = sprite;
            dragMarkerImage.color = markerImage != null ? markerImage.color : Color.white;
            dragMarkerImage.material = markerImage != null ? markerImage.material : null;
            dragMarkerImage.raycastTarget = false;

            if (dragMarkerSprite != null && sprite != null)
            {
                dragMarkerImage.SetNativeSize();
                dragMarkerTransform.sizeDelta *= dragMarkerSizeMultiplier;
            }
        }
    }

    private void DestroyDragMarker()
    {
        if (dragMarkerTransform == null)
        {
            return;
        }

        dragMarkerTransform.gameObject.SetActive(false);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }
}

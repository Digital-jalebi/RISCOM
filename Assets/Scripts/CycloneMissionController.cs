using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CycloneMissionController : MonoBehaviour
{
    private const int TotalMarkers = 7;
    private const float MissionDurationSeconds = 600f;

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

    private readonly System.Collections.Generic.List<RaycastResult> raycastResults = new System.Collections.Generic.List<RaycastResult>();

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

        ResetDropZones();
        DestroyDragMarker();
        ResetMarkerPosition();
        SetMarkerVisible(true);
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

        if (timeRemaining <= 0f)
        {
            isRunning = false;
            DestroyDragMarker();
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

}

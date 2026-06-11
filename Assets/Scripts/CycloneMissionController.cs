using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CycloneMissionController : MonoBehaviour
{
    private static readonly string[] ProneAreaNames =
    {
        "Kutch",
        "DevBhoomi",
        "RamNagar",
        "Morbi",
        "Porbandar",
        "Junagadh",
        "GirSomnath"
    };

    private const int TotalMarkers = 7;
    private const float MissionDurationSeconds = 600f;

    private readonly List<CycloneMapDropZone> dropZones = new List<CycloneMapDropZone>();
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    private RectTransform markerTransform;
    private RectTransform dragMarkerTransform;
    private Image markerImage;
    private Slider missionProgressSlider;
    private Slider timeRemainingSlider;
    private TextMeshProUGUI timeRemainingLabel;
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

        markerTransform = FindChildTransform(transform, "DistrictMarker") as RectTransform;
        dragMarkerTransform = FindChildTransform(transform, "DraggingDistrictMarker") as RectTransform;
        Transform mapTransform = FindChildTransform(transform, "Map");
        missionProgressSlider = GetSlider("MissionProgressBar");
        timeRemainingSlider = GetSlider("TimeRemainingSlider");
        timeRemainingLabel = timeRemainingSlider != null
            ? timeRemainingSlider.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;

        if (markerTransform != null)
        {
            markerStartPosition = markerTransform.anchoredPosition;
            markerImage = markerTransform.GetComponent<Image>();
            if (markerImage != null)
            {
                markerImage.raycastTarget = true;
            }

            CycloneDistrictMarkerDragHandler dragHandler = markerTransform.GetComponent<CycloneDistrictMarkerDragHandler>();
            if (dragHandler == null)
            {
                Debug.LogWarning("DistrictMarker is missing CycloneDistrictMarkerDragHandler.");
            }
            else
            {
                dragHandler.Initialize(this);
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

        CacheDropZones(mapTransform);
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
        if (!isRunning || dragMarkerTransform == null || markerTransform == null || markerTransform.parent == null)
        {
            return;
        }

        RectTransform parentRect = markerTransform.parent as RectTransform;
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
            CycloneMapDropZone dropZone = raycastResults[i].gameObject.GetComponentInParent<CycloneMapDropZone>();
            if (dropZone != null)
            {
                return dropZone;
            }
        }

        return null;
    }

    private void CacheDropZones(Transform mapTransform)
    {
        dropZones.Clear();

        if (mapTransform == null)
        {
            Debug.LogWarning("Cyclone mission Map object was not found.");
            return;
        }

        for (int i = 0; i < mapTransform.childCount; i++)
        {
            Transform area = mapTransform.GetChild(i);
            Image image = area.GetComponent<Image>();
            if (image == null)
            {
                continue;
            }

            image.raycastTarget = true;

            CycloneMapDropZone zone = area.GetComponent<CycloneMapDropZone>();
            if (zone == null)
            {
                Debug.LogWarning($"{area.name} is missing CycloneMapDropZone.");
                continue;
            }

            zone.Initialize(IsProneArea(area.name), image);
            dropZones.Add(zone);
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
        for (int i = 0; i < dropZones.Count; i++)
        {
            dropZones[i].ResetZone();
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
        dragMarkerTransform.anchorMin = markerTransform.anchorMin;
        dragMarkerTransform.anchorMax = markerTransform.anchorMax;
        dragMarkerTransform.pivot = markerTransform.pivot;
        dragMarkerTransform.sizeDelta = markerTransform.sizeDelta;
        dragMarkerTransform.anchoredPosition = markerStartPosition;
        dragMarkerTransform.localScale = markerTransform.localScale;
        dragMarkerTransform.localRotation = markerTransform.localRotation;
        dragMarkerTransform.SetAsLastSibling();

        Image dragImage = dragMarkerTransform.GetComponent<Image>();
        if (markerImage != null)
        {
            dragImage.sprite = markerImage.sprite;
            dragImage.color = markerImage.color;
            dragImage.material = markerImage.material;
            dragImage.type = markerImage.type;
            dragImage.preserveAspect = markerImage.preserveAspect;
            dragImage.fillCenter = markerImage.fillCenter;
            dragImage.fillMethod = markerImage.fillMethod;
            dragImage.fillAmount = markerImage.fillAmount;
            dragImage.fillClockwise = markerImage.fillClockwise;
            dragImage.fillOrigin = markerImage.fillOrigin;
            dragImage.pixelsPerUnitMultiplier = markerImage.pixelsPerUnitMultiplier;
        }

        dragImage.raycastTarget = false;
    }

    private void DestroyDragMarker()
    {
        if (dragMarkerTransform == null)
        {
            return;
        }

        dragMarkerTransform.gameObject.SetActive(false);
    }

    private Slider GetSlider(string objectName)
    {
        Transform sliderTransform = FindChildTransform(transform, objectName);
        return sliderTransform != null ? sliderTransform.GetComponent<Slider>() : null;
    }

    private static bool IsProneArea(string areaName)
    {
        for (int i = 0; i < ProneAreaNames.Length; i++)
        {
            if (string.Equals(areaName, ProneAreaNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Transform FindChildTransform(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (string.Equals(root.name, childName, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChildTransform(root.GetChild(i), childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}

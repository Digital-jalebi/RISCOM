using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class FloodMissionOneController : MonoBehaviour
{
    [SerializeField] private GameObject playRoot;
    [SerializeField] private GameObject completeScreen;
    [SerializeField] private GameObject missionBackground;
    [SerializeField] private Slider missionProgressSlider;
    [SerializeField] private Slider timeRemainingSlider;
    [SerializeField] private TextMeshProUGUI timeRemainingLabel;
    [SerializeField] private RISCOMLanguageToggleController languageToggleController;
    [SerializeField] private RISCOMNotificationPanel notificationPanel = new RISCOMNotificationPanel();
    [SerializeField] private RectTransform dragMarkerTransform;
    [SerializeField] private Image dragMarkerImage;
    [SerializeField] private Sprite dragMarkerSprite;
    [SerializeField, Range(0.1f, 2f)] private float dragMarkerSizeMultiplier = 1f;
    [SerializeField] private float missionDurationSeconds = 600f;
    [SerializeField] private FloodMarkerTool[] markerTools;
    [SerializeField] private FloodDistrictTarget[] districts;

    [SerializeField] private FloodToolAlertMessageSequence toolAlertMessages;

    private FloodMarkerTool draggedTool;
    private float timeRemaining;
    private int placedDistricts;
    private bool configured;
    private bool isRunning;
    private bool isComplete;
    private Coroutine shakeRoutine;

    public void Configure()
    {
        if (configured)
        {
            return;
        }

        CacheMarkerTools();
        CacheDistricts();
        ConfigureSliders();
        notificationPanel.Configure();

        if (dragMarkerTransform != null)
        {
            dragMarkerTransform.gameObject.SetActive(false);
        }

        toolAlertMessages?.Configure();
        configured = true;
    }

    public void BeginMission()
    {
        Configure();

        StopActiveDrag();
        StopShake();

        placedDistricts = 0;
        timeRemaining = missionDurationSeconds;
        isRunning = true;
        isComplete = false;

        SetActive(missionBackground, true);
        ResetMarkerTools();
        ResetDistricts();
        notificationPanel.Clear();
        SetActive(playRoot, true);
        SetActive(completeScreen, false);
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

        UpdateTimer();
        notificationPanel.UpdateScrollInput();

        if (isRunning && !isComplete)
        {
            UpdateDragInput();
        }
    }

    private void UpdateTimer()
    {
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerDisplay();

        if (timeRemaining <= 0f)
        {
            isRunning = false;
            StopActiveDrag();
            toolAlertMessages?.HideAll();
            Debug.LogWarning("Flood Mission 1 timer expired.");
        }
    }

    private void UpdateDragInput()
    {
        if (UpdateTouchInput())
        {
            return;
        }

        UpdateMouseInput();
    }

    private bool UpdateTouchInput()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
        {
            return false;
        }

        foreach (var touch in touchscreen.touches)
        {
            Vector2 touchPosition = touch.position.ReadValue();
            if (touch.press.wasPressedThisFrame)
            {
                BeginDrag(touchPosition);
                return true;
            }

            if (touch.press.isPressed)
            {
                MoveDrag(touchPosition);
                return true;
            }

            if (touch.press.wasReleasedThisFrame)
            {
                EndDrag(touchPosition);
                return true;
            }
        }

        return false;
    }

    private void UpdateMouseInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        Vector2 mousePosition = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame)
        {
            BeginDrag(mousePosition);
        }

        if (mouse.leftButton.isPressed)
        {
            MoveDrag(mousePosition);
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            EndDrag(mousePosition);
        }
    }

    private void BeginDrag(Vector2 screenPosition)
    {
        if (draggedTool != null)
        {
            return;
        }

        FloodMarkerTool tool = FindMarkerToolAt(screenPosition);
        if (tool == null || tool.ToolTransform == null)
        {
            return;
        }

        draggedTool = tool;
        CreateDragMarker(tool);
        MoveDrag(screenPosition);
    }

    private void MoveDrag(Vector2 screenPosition)
    {
        if (draggedTool == null || dragMarkerTransform == null)
        {
            return;
        }

        Vector3 pointerWorldPosition;
        if (TryGetPointerWorldPosition(screenPosition, out pointerWorldPosition))
        {
            dragMarkerTransform.position = pointerWorldPosition;
        }
    }

    private void EndDrag(Vector2 screenPosition)
    {
        if (draggedTool == null)
        {
            return;
        }

        FloodMarkerTool droppedTool = draggedTool;
        draggedTool = null;

        FloodDistrictTarget district = FindDistrictForDrop(screenPosition, droppedTool.MarkerType);
        DestroyDragMarker();

        if (district != null && district.CanAccept(droppedTool.MarkerType))
        {
            AcceptPlacement(district);
            return;
        }

        ResetMarkerWithShake(droppedTool);
    }

    private void AcceptPlacement(FloodDistrictTarget district)
    {
        district.MarkPlaced();
        ShowDistrictNotification(district);
        placedDistricts++;
        UpdateProgress();

        if (placedDistricts >= GetDistrictCount())
        {
            CompleteMission();
        }
    }

    private void CompleteMission()
    {
        isComplete = true;
        isRunning = false;
        StopActiveDrag();
        toolAlertMessages?.HideAll();
        SetActive(missionBackground, false);
        SetActive(playRoot, false);
        SetActive(completeScreen, true);
    }

    private FloodMarkerTool FindMarkerToolAt(Vector2 screenPosition)
    {
        if (markerTools == null)
        {
            return null;
        }

        for (int i = 0; i < markerTools.Length; i++)
        {
            FloodMarkerTool tool = markerTools[i];
            if (tool != null && tool.Contains(screenPosition))
            {
                return tool;
            }
        }

        return null;
    }

    private FloodDistrictTarget FindDistrictForDrop(Vector2 screenPosition, FloodMarkerType markerType)
    {
        if (districts == null)
        {
            return null;
        }

        FloodDistrictTarget firstContainedDistrict = null;
        for (int i = 0; i < districts.Length; i++)
        {
            FloodDistrictTarget district = districts[i];
            if (district == null || !district.Contains(screenPosition))
            {
                continue;
            }

            if (firstContainedDistrict == null)
            {
                firstContainedDistrict = district;
            }

            if (district.CanAccept(markerType))
            {
                return district;
            }
        }

        return firstContainedDistrict;
    }

    private void CreateDragMarker(FloodMarkerTool tool)
    {
        if (dragMarkerTransform == null || tool == null || tool.ToolTransform == null)
        {
            return;
        }

        dragMarkerTransform.gameObject.SetActive(true);
        dragMarkerTransform.position = tool.ToolTransform.position;
        dragMarkerTransform.sizeDelta = tool.ToolTransform.sizeDelta * dragMarkerSizeMultiplier;
        dragMarkerTransform.SetAsLastSibling();

        if (dragMarkerImage != null)
        {
            Sprite sprite = dragMarkerSprite != null ? dragMarkerSprite : tool.ToolImage != null ? tool.ToolImage.sprite : null;
            dragMarkerImage.sprite = sprite;
            dragMarkerImage.color = dragMarkerSprite != null ? Color.white : tool.ToolImage != null ? tool.ToolImage.color : Color.white;
            dragMarkerImage.material = tool.ToolImage != null ? tool.ToolImage.material : null;
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
        if (dragMarkerTransform != null)
        {
            dragMarkerTransform.gameObject.SetActive(false);
        }
    }

    private void StopActiveDrag()
    {
        draggedTool = null;
        DestroyDragMarker();
    }

    private void ResetMarkerWithShake(FloodMarkerTool tool)
    {
        if (tool == null)
        {
            return;
        }

        tool.ResetPosition();

        StopShake();
        shakeRoutine = StartCoroutine(ShakeMarker(tool));
    }

    private IEnumerator ShakeMarker(FloodMarkerTool tool)
    {
        if (tool == null || tool.ToolTransform == null)
        {
            yield break;
        }

        const float duration = 0.25f;
        const float frequency = 48f;
        const float amplitude = 12f;
        float elapsed = 0f;
        Vector2 startPosition = tool.StartAnchoredPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * frequency) * amplitude;
            tool.ToolTransform.anchoredPosition = startPosition + new Vector2(offset, 0f);
            yield return null;
        }

        tool.ToolTransform.anchoredPosition = startPosition;
        shakeRoutine = null;
    }

    private void StopShake()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }
    }

    private bool TryGetPointerWorldPosition(Vector2 screenPosition, out Vector3 worldPosition)
    {
        RectTransform parent = dragMarkerTransform != null
            ? dragMarkerTransform.parent as RectTransform
            : transform as RectTransform;

        if (parent != null &&
            RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screenPosition, null, out worldPosition))
        {
            return true;
        }

        worldPosition = Vector3.zero;
        return false;
    }

    private void CacheMarkerTools()
    {
        if (markerTools == null)
        {
            return;
        }

        for (int i = 0; i < markerTools.Length; i++)
        {
            markerTools[i]?.CacheInitialState();
        }
    }

    private void CacheDistricts()
    {
        if (districts == null)
        {
            return;
        }

        for (int i = 0; i < districts.Length; i++)
        {
            districts[i]?.CacheInitialState();
        }
    }

    private void ConfigureSliders()
    {
        if (missionProgressSlider != null)
        {
            missionProgressSlider.minValue = 0f;
            missionProgressSlider.maxValue = GetDistrictCount();
            missionProgressSlider.wholeNumbers = true;
            missionProgressSlider.interactable = false;
        }

        if (timeRemainingSlider != null)
        {
            timeRemainingSlider.minValue = 0f;
            timeRemainingSlider.maxValue = missionDurationSeconds;
            timeRemainingSlider.interactable = false;
        }
    }

    private void ResetMarkerTools()
    {
        if (markerTools == null)
        {
            return;
        }

        for (int i = 0; i < markerTools.Length; i++)
        {
            markerTools[i]?.ResetPosition();
        }
    }

    private void ResetDistricts()
    {
        if (districts == null)
        {
            return;
        }

        for (int i = 0; i < districts.Length; i++)
        {
            districts[i]?.ResetDistrict();
        }
    }

    private void UpdateProgress()
    {
        if (missionProgressSlider != null)
        {
            missionProgressSlider.value = placedDistricts;
        }
    }

    private void ShowDistrictNotification(FloodDistrictTarget district)
    {
        if (district == null)
        {
            return;
        }

        Sprite notificationSprite = district.GetNotificationSprite(IsGujaratiEnabled());
        if (notificationSprite == null)
        {
            return;
        }

        notificationPanel.Show(notificationSprite);
    }

    private bool IsGujaratiEnabled()
    {
        return languageToggleController != null && languageToggleController.IsGujaratiEnabled;
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

    private int GetDistrictCount()
    {
        return districts != null ? districts.Length : 0;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private enum FloodMarkerType
    {
        Isolated,
        FewPlaces,
        ManyPlaces,
        MostPlaces
    }

    [Serializable]
    private sealed class FloodMarkerTool
    {
        [SerializeField] private FloodMarkerType markerType;
        [SerializeField] private RectTransform toolTransform;
        [SerializeField] private Image toolImage;

        private Vector2 startAnchoredPosition;

        public FloodMarkerType MarkerType => markerType;
        public RectTransform ToolTransform => toolTransform;
        public Image ToolImage => toolImage;
        public Vector2 StartAnchoredPosition => startAnchoredPosition;

        public void CacheInitialState()
        {
            if (toolTransform != null)
            {
                startAnchoredPosition = toolTransform.anchoredPosition;
            }

            if (toolImage != null)
            {
                toolImage.raycastTarget = true;
            }
        }

        public bool Contains(Vector2 screenPosition)
        {
            return toolTransform != null &&
                   toolTransform.gameObject.activeInHierarchy &&
                   RectTransformUtility.RectangleContainsScreenPoint(toolTransform, screenPosition, null);
        }

        public void ResetPosition()
        {
            if (toolTransform != null)
            {
                toolTransform.anchoredPosition = startAnchoredPosition;
            }
        }
    }

    [Serializable]
    private sealed class FloodDistrictTarget
    {
        [SerializeField] private string districtName;
        [SerializeField] private FloodMarkerType requiredMarkerType;
        [SerializeField] private RectTransform districtTransform;
        [SerializeField] private Image districtImage;
        [SerializeField] private Sprite colouredSprite;
        [SerializeField] private Sprite englishNotificationSprite;
        [SerializeField] private Sprite gujaratiNotificationSprite;

        private Sprite initialSprite;
        private Color initialColor;
        private bool isPlaced;

        public void CacheInitialState()
        {
            if (districtImage != null)
            {
                initialSprite = districtImage.sprite;
                initialColor = districtImage.color;
                districtImage.raycastTarget = true;
            }
        }

        public bool Contains(Vector2 screenPosition)
        {
            return districtTransform != null &&
                   districtTransform.gameObject.activeInHierarchy &&
                   RectTransformUtility.RectangleContainsScreenPoint(districtTransform, screenPosition, null);
        }

        public bool CanAccept(FloodMarkerType markerType)
        {
            return !isPlaced && markerType == requiredMarkerType;
        }

        public void MarkPlaced()
        {
            isPlaced = true;

            if (districtImage != null && colouredSprite != null)
            {
                districtImage.sprite = colouredSprite;
            }
        }

        public Sprite GetNotificationSprite(bool useGujarati)
        {
            return useGujarati && gujaratiNotificationSprite != null
                ? gujaratiNotificationSprite
                : englishNotificationSprite;
        }

        public void ResetDistrict()
        {
            isPlaced = false;

            if (districtImage != null)
            {
                districtImage.sprite = initialSprite;
                districtImage.color = initialColor;
            }
        }
    }
}

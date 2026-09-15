using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class IndustrialMissionOneController : MonoBehaviour
{
    private const float WrongDropShakeDuration = 0.25f;
    private const float WrongDropShakeFrequency = 48f;
    private const float WrongDropShakeAmplitude = 12f;
    private const float DisabledToolAlphaMultiplier = 0.5f;

    [SerializeField] private GameObject playRoot;
    [SerializeField] private GameObject completeScreen;
    [SerializeField] private GameObject missionBackground;
    [SerializeField] private Slider timeRemainingSlider;
    [SerializeField] private TextMeshProUGUI timeRemainingLabel;
    [SerializeField] private RISCOMLanguageToggleController languageToggleController;
    [SerializeField] private RISCOMNotificationPanel notificationPanel = new RISCOMNotificationPanel();
    [SerializeField] private PhaseNotificationSprites alarmActivatedNotification = new PhaseNotificationSprites();
    [SerializeField] private PhaseNotificationSprites hazardsIdentifiedNotification = new PhaseNotificationSprites();
    [SerializeField] private PhaseNotificationSprites leaksIsolatedNotification = new PhaseNotificationSprites();
    [SerializeField] private RectTransform dragPreviewTransform;
    [SerializeField] private Image dragPreviewImage;
    [SerializeField] private RectTransform alarmDropTarget;
    [SerializeField] private GameObject alarmsRoot;
    [SerializeField] private GameObject smokeObject;
    [SerializeField] private float missionDurationSeconds = 600f;
    [SerializeField] private float identifyDetectionPadding = 40f;
    [SerializeField] private float isolationFillDurationSeconds = 1.25f;
    [SerializeField] private float completionDelaySeconds = 0.5f;
    [SerializeField] private IndustrialTool activateAlarmTool;
    [SerializeField] private IndustrialTool identifyHazardTool;
    [SerializeField] private IndustrialTool gasValveTool;
    [SerializeField] private List<HazardMarkerTarget> hazardMarkers = new List<HazardMarkerTarget>();

    private readonly List<Coroutine> isolationRoutines = new List<Coroutine>();

    private MissionPhase phase;
    private IndustrialTool draggedTool;
    private float timeRemaining;
    private int detectedHazards;
    private int placedValves;
    private int completedIsolations;
    private bool configured;
    private bool isRunning;
    private bool isComplete;
    private Coroutine shakeRoutine;
    private Coroutine completionRoutine;
    [SerializeField] private IndustrialToolAlertMessageSequence toolAlertMessages;

    public void Configure()
    {
        if (configured)
        {
            return;
        }

        activateAlarmTool?.CacheInitialState();
        identifyHazardTool?.CacheInitialState();
        gasValveTool?.CacheInitialState();
        CacheHazardMarkers();
        ConfigureTimerSlider();
        notificationPanel.Configure();
        toolAlertMessages?.Configure();

        if (dragPreviewTransform != null)
        {
            dragPreviewTransform.gameObject.SetActive(false);
        }

        configured = true;
    }

    public void BeginMission()
    {
        Configure();
        StopActiveDrag();
        StopMissionRoutines();

        phase = MissionPhase.ActivateAlarm;
        timeRemaining = missionDurationSeconds;
        detectedHazards = 0;
        placedValves = 0;
        completedIsolations = 0;
        isRunning = true;
        isComplete = false;

        SetActive(missionBackground, true);
        SetActive(playRoot, true);
        SetActive(completeScreen, false);
        SetActive(alarmsRoot, false);
        SetActive(smokeObject, true);
        notificationPanel.Clear();

        ResetTools();
        ResetHazardMarkers();
        SetToolUsable(activateAlarmTool, true);
        SetToolUsable(identifyHazardTool, false);
        SetToolUsable(gasValveTool, false);
        toolAlertMessages?.SetActiveIndex(0);
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
        toolAlertMessages?.UpdatePulse(Time.unscaledTime);

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
            Debug.LogWarning("Industrial Mission 1 timer expired.");
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
                EndDrag();
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
            EndDrag();
        }
    }

    private void BeginDrag(Vector2 screenPosition)
    {
        if (draggedTool != null)
        {
            return;
        }

        IndustrialTool tool = GetActiveTool();
        if (tool == null || !tool.Contains(screenPosition))
        {
            return;
        }

        draggedTool = tool;
        CreateDragPreview(tool);
        MoveDrag(screenPosition);
    }

    private void MoveDrag(Vector2 screenPosition)
    {
        if (draggedTool == null || dragPreviewTransform == null)
        {
            return;
        }

        Vector3 pointerWorldPosition;
        if (TryGetPointerWorldPosition(screenPosition, out pointerWorldPosition))
        {
            dragPreviewTransform.position = pointerWorldPosition;
        }

        if (phase == MissionPhase.IdentifyHazards && draggedTool == identifyHazardTool)
        {
            TryAutoDetectHazardWhileDragging();
        }
    }

    private void EndDrag()
    {
        if (draggedTool == null)
        {
            return;
        }

        IndustrialTool droppedTool = draggedTool;
        RectTransform dropTransform = GetDropTransform(droppedTool);
        draggedTool = null;

        if (droppedTool == identifyHazardTool)
        {
            if (phase == MissionPhase.IdentifyHazards)
            {
                TryAutoDetectHazardWhileDragging();
            }

            DestroyDragPreview();
            return;
        }

        bool accepted = TryApplyTool(droppedTool, dropTransform);
        DestroyDragPreview();

        if (!accepted)
        {
            ResetToolWithShake(droppedTool);
        }
    }

    private bool TryApplyTool(IndustrialTool tool, RectTransform dropTransform)
    {
        if (phase == MissionPhase.ActivateAlarm && tool == activateAlarmTool)
        {
            return TryActivateAlarm(dropTransform);
        }

        if (phase == MissionPhase.IsolateLeaks && tool == gasValveTool)
        {
            return TryPlaceGasValve(dropTransform);
        }

        return false;
    }

    private bool TryActivateAlarm(RectTransform dropTransform)
    {
        if (alarmDropTarget != null && !RectTransformsOverlap(dropTransform, alarmDropTarget))
        {
            return false;
        }

        SetActive(alarmsRoot, true);
        phase = MissionPhase.IdentifyHazards;
        SetToolUsable(activateAlarmTool, false);
        SetToolUsable(identifyHazardTool, true);
        SetToolUsable(gasValveTool, false);
        toolAlertMessages?.SetActiveIndex(1);
        ShowNotification(alarmActivatedNotification.GetSprite(IsGujaratiEnabled()));
        return true;
    }

    private void TryAutoDetectHazardWhileDragging()
    {
        HazardMarkerTarget marker = FindDetectableMarker(dragPreviewTransform);
        if (marker == null)
        {
            return;
        }

        marker.MarkDetected();
        detectedHazards++;

        if (detectedHazards >= GetHazardMarkerCount())
        {
            ShowNotification(hazardsIdentifiedNotification.GetSprite(IsGujaratiEnabled()));
            phase = MissionPhase.IsolateLeaks;
            SetToolUsable(identifyHazardTool, false);
            SetToolUsable(gasValveTool, true);
            toolAlertMessages?.SetActiveIndex(2);
            draggedTool = null;
            DestroyDragPreview();
        }
    }

    private bool TryPlaceGasValve(RectTransform dropTransform)
    {
        HazardMarkerTarget marker = FindValveMarker(dropTransform);
        if (marker == null)
        {
            return false;
        }

        marker.MarkValvePlaced();
        placedValves++;
        isolationRoutines.Add(StartCoroutine(FillIsolation(marker)));

        if (placedValves >= GetHazardMarkerCount())
        {
            SetActive(smokeObject, false);
            SetToolUsable(gasValveTool, false);
        }

        return true;
    }

    private IEnumerator FillIsolation(HazardMarkerTarget marker)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, isolationFillDurationSeconds);
        marker.SetIsolationProgress(0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            marker.SetIsolationProgress(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        marker.SetIsolationProgress(1f);
        completedIsolations++;

        if (completedIsolations >= GetHazardMarkerCount())
        {
            ShowNotification(leaksIsolatedNotification.GetSprite(IsGujaratiEnabled()));
            CompleteMissionAfterDelay();
        }
    }

    private HazardMarkerTarget FindDetectableMarker(RectTransform dropTransform)
    {
        for (int i = 0; i < hazardMarkers.Count; i++)
        {
            HazardMarkerTarget marker = hazardMarkers[i];
            if (marker != null && marker.CanDetect(dropTransform, identifyDetectionPadding))
            {
                return marker;
            }
        }

        return null;
    }

    private HazardMarkerTarget FindValveMarker(RectTransform dropTransform)
    {
        for (int i = 0; i < hazardMarkers.Count; i++)
        {
            HazardMarkerTarget marker = hazardMarkers[i];
            if (marker != null && marker.CanPlaceValve(dropTransform))
            {
                return marker;
            }
        }

        return null;
    }

    private void CompleteMissionAfterDelay()
    {
        if (isComplete)
        {
            return;
        }

        isRunning = false;
        isComplete = true;
        StopActiveDrag();
        toolAlertMessages?.HideAll();

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
        }

        completionRoutine = StartCoroutine(ShowCompletionAfterDelay());
    }

    private void ShowNotification(Sprite sprite)
    {
        if (sprite != null)
        {
            notificationPanel.Show(sprite);
        }
    }

    private bool IsGujaratiEnabled()
    {
        return languageToggleController != null && languageToggleController.IsGujaratiEnabled;
    }

    private IEnumerator ShowCompletionAfterDelay()
    {
        yield return new WaitForSeconds(completionDelaySeconds);

        SetActive(missionBackground, false);
        SetActive(playRoot, false);
        SetActive(completeScreen, true);
        completionRoutine = null;
    }

    private IndustrialTool GetActiveTool()
    {
        if (phase == MissionPhase.ActivateAlarm)
        {
            return activateAlarmTool;
        }

        if (phase == MissionPhase.IdentifyHazards)
        {
            return identifyHazardTool;
        }

        if (phase == MissionPhase.IsolateLeaks)
        {
            return gasValveTool;
        }

        return null;
    }

    private void CreateDragPreview(IndustrialTool tool)
    {
        if (dragPreviewTransform == null || tool == null || tool.ToolTransform == null)
        {
            return;
        }

        dragPreviewTransform.gameObject.SetActive(true);
        dragPreviewTransform.position = tool.ToolTransform.position;
        dragPreviewTransform.sizeDelta = tool.ToolTransform.sizeDelta * tool.DragSizeMultiplier;
        dragPreviewTransform.SetAsLastSibling();

        if (dragPreviewImage != null)
        {
            Sprite sprite = tool.DragSprite != null ? tool.DragSprite : tool.ToolSprite;
            dragPreviewImage.sprite = sprite;
            dragPreviewImage.color = Color.white;
            dragPreviewImage.material = tool.ToolImage != null ? tool.ToolImage.material : null;
            dragPreviewImage.raycastTarget = false;

            if (tool.DragSprite != null && sprite != null)
            {
                dragPreviewImage.SetNativeSize();
                dragPreviewTransform.sizeDelta *= tool.DragSizeMultiplier;
            }
        }
    }

    private void DestroyDragPreview()
    {
        if (dragPreviewTransform != null)
        {
            dragPreviewTransform.gameObject.SetActive(false);
        }
    }

    private RectTransform GetDropTransform(IndustrialTool tool)
    {
        if (dragPreviewTransform != null && dragPreviewTransform.gameObject.activeSelf)
        {
            return dragPreviewTransform;
        }

        return tool != null ? tool.ToolTransform : null;
    }

    private void StopActiveDrag()
    {
        draggedTool = null;
        DestroyDragPreview();
    }

    private void ResetToolWithShake(IndustrialTool tool)
    {
        if (tool == null)
        {
            return;
        }

        tool.ResetPosition();

        StopShake();
        shakeRoutine = StartCoroutine(ShakeTool(tool));
    }

    private IEnumerator ShakeTool(IndustrialTool tool)
    {
        if (tool == null || tool.ToolTransform == null)
        {
            yield break;
        }

        float elapsed = 0f;
        Vector2 startPosition = tool.InitialPosition;

        while (elapsed < WrongDropShakeDuration)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * WrongDropShakeFrequency) * WrongDropShakeAmplitude;
            tool.ToolTransform.anchoredPosition = startPosition + new Vector2(offset, 0f);
            yield return null;
        }

        tool.ToolTransform.anchoredPosition = startPosition;
        shakeRoutine = null;
    }

    private void StopMissionRoutines()
    {
        StopShake();

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }

        for (int i = 0; i < isolationRoutines.Count; i++)
        {
            if (isolationRoutines[i] != null)
            {
                StopCoroutine(isolationRoutines[i]);
            }
        }

        isolationRoutines.Clear();
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
        RectTransform parent = dragPreviewTransform != null
            ? dragPreviewTransform.parent as RectTransform
            : transform as RectTransform;

        if (parent != null &&
            RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screenPosition, null, out worldPosition))
        {
            return true;
        }

        worldPosition = Vector3.zero;
        return false;
    }

    private void CacheHazardMarkers()
    {
        for (int i = 0; i < hazardMarkers.Count; i++)
        {
            hazardMarkers[i]?.CacheInitialState();
        }
    }

    private void ConfigureTimerSlider()
    {
        if (timeRemainingSlider != null)
        {
            timeRemainingSlider.minValue = 0f;
            timeRemainingSlider.maxValue = missionDurationSeconds;
            timeRemainingSlider.interactable = false;
        }
    }

    private void ResetTools()
    {
        activateAlarmTool?.ResetPosition();
        identifyHazardTool?.ResetPosition();
        gasValveTool?.ResetPosition();
    }

    private void ResetHazardMarkers()
    {
        for (int i = 0; i < hazardMarkers.Count; i++)
        {
            hazardMarkers[i]?.ResetTarget();
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

    private int GetHazardMarkerCount()
    {
        int count = 0;
        for (int i = 0; i < hazardMarkers.Count; i++)
        {
            if (hazardMarkers[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private static void SetToolUsable(IndustrialTool tool, bool usable)
    {
        tool?.SetUsable(usable);
    }

    private static bool RectTransformsOverlap(RectTransform first, RectTransform second)
    {
        if (first == null || second == null ||
            !first.gameObject.activeInHierarchy ||
            !second.gameObject.activeInHierarchy)
        {
            return false;
        }

        Rect firstRect = GetWorldRect(first);
        Rect secondRect = GetWorldRect(second);

        return firstRect.xMin < secondRect.xMax &&
               firstRect.xMax > secondRect.xMin &&
               firstRect.yMin < secondRect.yMax &&
               firstRect.yMax > secondRect.yMin;
    }

    private static bool RectTransformIsNear(RectTransform first, RectTransform second, float padding)
    {
        if (first == null || second == null ||
            !first.gameObject.activeInHierarchy ||
            !second.gameObject.activeInHierarchy)
        {
            return false;
        }

        Rect firstRect = GetWorldRect(first);
        Rect secondRect = GetWorldRect(second);
        float extraPadding = Mathf.Max(0f, padding);

        secondRect.xMin -= extraPadding;
        secondRect.xMax += extraPadding;
        secondRect.yMin -= extraPadding;
        secondRect.yMax += extraPadding;

        return firstRect.xMin < secondRect.xMax &&
               firstRect.xMax > secondRect.xMin &&
               firstRect.yMin < secondRect.yMax &&
               firstRect.yMax > secondRect.yMin;
    }

    private static Rect GetWorldRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        float minX = corners[0].x;
        float maxX = corners[0].x;
        float minY = corners[0].y;
        float maxY = corners[0].y;

        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 corner = corners[i];
            minX = Mathf.Min(minX, corner.x);
            maxX = Mathf.Max(maxX, corner.x);
            minY = Mathf.Min(minY, corner.y);
            maxY = Mathf.Max(maxY, corner.y);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private enum MissionPhase
    {
        ActivateAlarm,
        IdentifyHazards,
        IsolateLeaks
    }

    [Serializable]
    private sealed class PhaseNotificationSprites
    {
        [SerializeField] private Sprite englishSprite;
        [SerializeField] private Sprite gujaratiSprite;

        public Sprite GetSprite(bool useGujarati)
        {
            if (useGujarati && gujaratiSprite != null)
            {
                return gujaratiSprite;
            }

            return englishSprite;
        }
    }

    [Serializable]
    private sealed class IndustrialTool
    {
        [SerializeField] private RectTransform toolTransform;
        [SerializeField] private Image toolImage;
        [SerializeField] private Sprite dragSprite;
        [SerializeField, Range(0.1f, 2f)] private float dragSizeMultiplier = 1f;

        private Vector2 initialPosition;
        private Color initialColor;

        public RectTransform ToolTransform => toolTransform;
        public Image ToolImage => toolImage;
        public Sprite ToolSprite => toolImage != null ? toolImage.sprite : null;
        public Sprite DragSprite => dragSprite;
        public float DragSizeMultiplier => dragSizeMultiplier;
        public Vector2 InitialPosition => initialPosition;

        public void CacheInitialState()
        {
            if (toolTransform != null)
            {
                initialPosition = toolTransform.anchoredPosition;
            }

            if (toolImage != null)
            {
                initialColor = toolImage.color;
                toolImage.raycastTarget = true;
            }
        }

        public bool Contains(Vector2 screenPosition)
        {
            return toolTransform != null &&
                   toolTransform.gameObject.activeInHierarchy &&
                   toolImage != null &&
                   toolImage.raycastTarget &&
                   RectTransformUtility.RectangleContainsScreenPoint(toolTransform, screenPosition, null);
        }

        public void ResetPosition()
        {
            if (toolTransform != null)
            {
                toolTransform.anchoredPosition = initialPosition;
            }
        }

        public void SetUsable(bool usable)
        {
            if (toolTransform != null)
            {
                toolTransform.gameObject.SetActive(true);
            }

            if (toolImage != null)
            {
                Color color = initialColor;
                color.a = usable ? initialColor.a : initialColor.a * DisabledToolAlphaMultiplier;
                toolImage.color = color;
                toolImage.raycastTarget = usable;
            }
        }
    }

    [Serializable]
    private sealed class HazardMarkerTarget
    {
        [SerializeField] private string markerName;
        [SerializeField] private RectTransform markerTransform;
        [SerializeField] private GameObject detectedImage;
        [SerializeField] private GameObject isolateLeakImage;
        [SerializeField] private Slider isolationSlider;

        private bool isDetected;
        private bool isValvePlaced;

        public void CacheInitialState()
        {
            ConfigureSlider();
            SetActive(detectedImage, false);
            SetActive(isolateLeakImage, false);
        }

        public bool CanDetect(RectTransform toolTransform, float detectionPadding)
        {
            return !isDetected &&
                   RectTransformIsNear(toolTransform, markerTransform, detectionPadding);
        }

        public bool CanPlaceValve(RectTransform toolTransform)
        {
            return isDetected &&
                   !isValvePlaced &&
                   RectTransformsOverlap(toolTransform, markerTransform);
        }

        public void MarkDetected()
        {
            isDetected = true;
            SetActive(detectedImage, true);
        }

        public void MarkValvePlaced()
        {
            isValvePlaced = true;
            SetActive(detectedImage, false);
            SetActive(isolateLeakImage, true);
            SetIsolationProgress(0f);
        }

        public void SetIsolationProgress(float progress)
        {
            if (isolationSlider != null)
            {
                isolationSlider.value = Mathf.Clamp01(progress);
            }
        }

        public void ResetTarget()
        {
            isDetected = false;
            isValvePlaced = false;
            SetActive(detectedImage, false);
            SetActive(isolateLeakImage, false);
            SetIsolationProgress(0f);
        }

        private void ConfigureSlider()
        {
            if (isolationSlider != null)
            {
                isolationSlider.minValue = 0f;
                isolationSlider.maxValue = 1f;
                isolationSlider.interactable = false;
                isolationSlider.value = 0f;
            }
        }
    }
}

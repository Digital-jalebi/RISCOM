using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class IndustrialMissionThreeController : MonoBehaviour
{
    private const float DefaultMissionDurationSeconds = 600f;
    private const float DisabledToolAlphaMultiplier = 0.5f;
    private const float WrongDropShakeDuration = 0.25f;
    private const float WrongDropShakeFrequency = 48f;
    private const float WrongDropShakeAmplitude = 12f;

    [SerializeField] private GameObject alertScreen;
    [SerializeField] private Button alertNextButton;
    [SerializeField] private GameObject playRoot;
    [SerializeField] private GameObject completeScreen;
    [SerializeField] private Button reportNextButton;
    [SerializeField] private RectTransform areaDropTarget;
    [SerializeField] private Slider timeRemainingSlider;
    [SerializeField] private TextMeshProUGUI timeRemainingLabel;
    [SerializeField] private RISCOMLanguageToggleController languageToggleController;
    [SerializeField] private RISCOMNotificationPanel notificationPanel = new RISCOMNotificationPanel();
    [SerializeField] private PhaseNotificationSprites publicAnnouncementCompleteNotification = new PhaseNotificationSprites();
    [SerializeField] private PhaseNotificationSprites rescueBusCompleteNotification = new PhaseNotificationSprites();
    [SerializeField] private PhaseNotificationSprites sheltersCompleteNotification = new PhaseNotificationSprites();
    [SerializeField] private float missionDurationSeconds = DefaultMissionDurationSeconds;
    [SerializeField] private float completionDelaySeconds = 1f;
    [SerializeField] private float announcementVehicleMoveSpeed = 420f;
    [SerializeField] private float rescueBusMoveSpeed = 420f;
    [SerializeField] private IndustrialMissionThreeTool publicAnnouncementVehicleTool;
    [SerializeField] private IndustrialMissionThreeTool busTool;
    [SerializeField] private IndustrialMissionThreeTool shelterTool;
    [SerializeField] private GameObject announcementVehiclePointerObject;
    [SerializeField] private RectTransform announcementVehiclePointer;
    [SerializeField] private List<RectTransform> announcementVehicleRoutePoints = new List<RectTransform>();
    [SerializeField] private GameObject rescueBusObject;
    [SerializeField] private RectTransform rescueBusTransform;
    [SerializeField] private Image rescueBusImage;
    [SerializeField] private BusDirectionSprites rescueBusSprites = new BusDirectionSprites();
    [SerializeField] private List<RescueBusRoutePoint> rescueBusRoutePoints = new List<RescueBusRoutePoint>();
    [SerializeField] private GameObject shelterHighlight;
    [SerializeField] private GameObject shelterInfo;
    [SerializeField] private List<ShelterPlacementTarget> shelterPlacementTargets = new List<ShelterPlacementTarget>();

    private System.Action onReportNext;
    private MissionPhase phase;
    private IndustrialMissionThreeTool draggedTool;
    private Vector3 dragWorldOffset;
    private float timeRemaining;
    private int placedShelters;
    private bool configured;
    private bool buttonsWired;
    private bool languageControllerWired;
    private bool isRunning;
    private bool isComplete;
    private bool inputLocked;
    private Coroutine shakeRoutine;
    private Coroutine routeRoutine;
    private Coroutine completionRoutine;
    [SerializeField] private IndustrialToolAlertMessageSequence toolAlertMessages;

    public void Configure(System.Action reportNextHandler = null)
    {
        if (reportNextHandler != null)
        {
            onReportNext = reportNextHandler;
        }

        if (!buttonsWired)
        {
            if (alertNextButton != null)
            {
                alertNextButton.onClick.AddListener(StartMissionGame);
            }

            if (reportNextButton != null)
            {
                reportNextButton.onClick.AddListener(HandleReportNext);
            }

            buttonsWired = true;
        }

        WireLanguageController();

        if (configured)
        {
            return;
        }

        publicAnnouncementVehicleTool?.CacheInitialState();
        busTool?.CacheInitialState();
        shelterTool?.CacheInitialState();
        CacheShelterTargets();
        ConfigureTimerSlider();
        if (rescueBusImage == null && rescueBusTransform != null)
        {
            rescueBusImage = rescueBusTransform.GetComponent<Image>();
        }

        ResetRouteObjects();
        notificationPanel.Configure();
        toolAlertMessages?.Configure();
        SetActive(shelterHighlight, false);
        SetActive(shelterInfo, false);

        configured = true;
    }

    public void ShowIntro()
    {
        Configure(onReportNext);
        StopMission();

        gameObject.SetActive(true);
        SetActive(alertScreen, true);
        SetActive(playRoot, false);
        SetActive(completeScreen, false);
    }

    public void Hide()
    {
        StopMission();
        gameObject.SetActive(false);
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

    private void HandleReportNext()
    {
        onReportNext?.Invoke();
    }

    private void StartMissionGame()
    {
        SetActive(alertScreen, false);
        SetActive(playRoot, true);
        SetActive(completeScreen, false);
        BeginMission();
    }

    private void BeginMission()
    {
        Configure(onReportNext);
        StopActiveDrag();
        StopMissionRoutines();

        phase = MissionPhase.PublicAnnouncementVehicle;
        timeRemaining = missionDurationSeconds;
        placedShelters = 0;
        isRunning = true;
        isComplete = false;
        inputLocked = false;

        ResetTools();
        ResetRouteObjects();
        ResetShelterTargets();
        notificationPanel.Clear();
        toolAlertMessages?.SetActiveIndex(0);
        SetActive(shelterHighlight, false);
        SetActive(shelterInfo, false);
        SetToolUsable(publicAnnouncementVehicleTool, true);
        SetToolUsable(busTool, false);
        SetToolUsable(shelterTool, false);
        UpdateTimerDisplay();
    }

    private void StopMission()
    {
        StopActiveDrag();
        StopMissionRoutines();
        isRunning = false;
        isComplete = false;
        inputLocked = false;

        if (!configured)
        {
            return;
        }

        ResetTools();
        ResetRouteObjects();
        ResetShelterTargets();
        notificationPanel.Clear();
        toolAlertMessages?.HideAll();
        SetActive(shelterHighlight, false);
        SetActive(shelterInfo, false);
    }

    private void UpdateTimer()
    {
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerDisplay();

        if (timeRemaining <= 0f)
        {
            StopMission();
            Debug.LogWarning("Industrial Mission 3 timer expired.");
        }
    }

    private void UpdateDragInput()
    {
        if (inputLocked)
        {
            return;
        }

        if (UpdateTouchDragInput())
        {
            return;
        }

        UpdateMouseDragInput();
    }

    private bool UpdateTouchDragInput()
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

    private void UpdateMouseDragInput()
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

        IndustrialMissionThreeTool selectedTool = GetActiveTool();
        if (selectedTool == null || !selectedTool.Contains(screenPosition))
        {
            return;
        }

        draggedTool = selectedTool;
        if (draggedTool.ToolTransform != null)
        {
            Vector3 pointerWorldPosition;
            dragWorldOffset = TryGetPointerWorldPosition(draggedTool, screenPosition, out pointerWorldPosition)
                ? draggedTool.ToolTransform.position - pointerWorldPosition
                : Vector3.zero;
        }

        draggedTool.BeginDragVisual();
        MoveDrag(screenPosition);
    }

    private void MoveDrag(Vector2 screenPosition)
    {
        if (draggedTool == null || draggedTool.ToolTransform == null)
        {
            return;
        }

        Vector3 pointerWorldPosition;
        if (TryGetPointerWorldPosition(draggedTool, screenPosition, out pointerWorldPosition))
        {
            draggedTool.ToolTransform.position = pointerWorldPosition + dragWorldOffset;
        }
    }

    private void EndDrag()
    {
        if (draggedTool == null)
        {
            return;
        }

        IndustrialMissionThreeTool droppedTool = draggedTool;
        draggedTool = null;

        if (TryApplyTool(droppedTool))
        {
            return;
        }

        ResetToolWithShake(droppedTool);
    }

    private bool TryApplyTool(IndustrialMissionThreeTool tool)
    {
        if (phase == MissionPhase.PublicAnnouncementVehicle && tool == publicAnnouncementVehicleTool)
        {
            return TryPlacePublicAnnouncementVehicle(tool.ToolTransform);
        }

        if (phase == MissionPhase.RescueBus && tool == busTool)
        {
            return TryPlaceRescueBus(tool.ToolTransform);
        }

        if (phase == MissionPhase.Shelters && tool == shelterTool)
        {
            return TryPlaceShelter(tool.ToolTransform);
        }

        return false;
    }

    private bool TryPlacePublicAnnouncementVehicle(RectTransform toolTransform)
    {
        if (!CanPlaceOnArea(toolTransform))
        {
            return false;
        }

        ResetTool(publicAnnouncementVehicleTool, false);
        ShowNotification(publicAnnouncementCompleteNotification.GetSprite(IsGujaratiEnabled()));
        inputLocked = true;
        routeRoutine = StartCoroutine(RunAnnouncementVehicleRoute());
        return true;
    }

    private bool TryPlaceRescueBus(RectTransform toolTransform)
    {
        if (!CanPlaceOnArea(toolTransform))
        {
            return false;
        }

        ResetTool(busTool, false);
        ShowNotification(rescueBusCompleteNotification.GetSprite(IsGujaratiEnabled()));
        inputLocked = true;
        routeRoutine = StartCoroutine(RunRescueBusRoute());
        return true;
    }

    private bool TryPlaceShelter(RectTransform toolTransform)
    {
        ShelterPlacementTarget target = FindAvailableShelterTarget(toolTransform);
        if (target == null)
        {
            return false;
        }

        target.MarkPlaced();
        SetActive(shelterHighlight, false);
        placedShelters++;
        ResetTool(shelterTool, HasPendingShelters());

        if (!HasPendingShelters())
        {
            ShowNotification(sheltersCompleteNotification.GetSprite(IsGujaratiEnabled()));
            CompleteMissionAfterDelay();
        }

        return true;
    }

    private IEnumerator RunAnnouncementVehicleRoute()
    {
        RectTransform movingTransform = announcementVehiclePointer;
        int startIndex = FindFirstValidRoutePointIndex(announcementVehicleRoutePoints);
        if (movingTransform == null || startIndex < 0)
        {
            FinishAnnouncementVehicleRoute();
            yield break;
        }

        SetActive(announcementVehiclePointerObject, true);
        movingTransform.position = announcementVehicleRoutePoints[startIndex].position;

        for (int i = startIndex + 1; i < announcementVehicleRoutePoints.Count; i++)
        {
            RectTransform point = announcementVehicleRoutePoints[i];
            if (point != null)
            {
                yield return MoveTransformTo(movingTransform, point.position, announcementVehicleMoveSpeed);
            }
        }

        FinishAnnouncementVehicleRoute();
    }

    private void FinishAnnouncementVehicleRoute()
    {
        SetActive(announcementVehiclePointerObject, false);
        phase = MissionPhase.RescueBus;
        inputLocked = false;
        routeRoutine = null;
        SetToolUsable(busTool, true);
        toolAlertMessages?.SetActiveIndex(1);
    }

    private IEnumerator RunRescueBusRoute()
    {
        RectTransform movingTransform = rescueBusTransform;
        int startIndex = FindFirstValidRescueRoutePointIndex();
        if (movingTransform == null || startIndex < 0)
        {
            FinishRescueBusRoute();
            yield break;
        }

        SetActive(rescueBusObject, true);

        RescueBusRoutePoint firstPoint = rescueBusRoutePoints[startIndex];
        if (firstPoint != null && firstPoint.Point != null)
        {
            movingTransform.position = firstPoint.Point.position;
        }

        for (int i = startIndex + 1; i < rescueBusRoutePoints.Count; i++)
        {
            RescueBusRoutePoint routePoint = rescueBusRoutePoints[i];
            if (routePoint == null || routePoint.Point == null)
            {
                continue;
            }

            yield return MoveTransformTo(movingTransform, routePoint.Point.position, rescueBusMoveSpeed);

            if (routePoint.PauseSeconds > 0f)
            {
                yield return new WaitForSeconds(routePoint.PauseSeconds);
            }
        }

        FinishRescueBusRoute();
    }

    private void FinishRescueBusRoute()
    {
        SetActive(rescueBusObject, false);
        SetActive(shelterHighlight, true);
        SetActive(shelterInfo, true);
        phase = MissionPhase.Shelters;
        inputLocked = false;
        routeRoutine = null;
        SetToolUsable(shelterTool, true);
        toolAlertMessages?.SetActiveIndex(2);

        if (GetShelterTargetCount() == 0)
        {
            CompleteMissionAfterDelay();
        }
    }

    private IEnumerator MoveTransformTo(RectTransform movingTransform, Vector3 targetPosition, float speed)
    {
        if (movingTransform == null)
        {
            yield break;
        }

        targetPosition.z = movingTransform.position.z;
        if (movingTransform == rescueBusTransform)
        {
            ApplyRescueBusDirection(movingTransform.position, targetPosition);
        }

        float moveSpeed = Mathf.Max(1f, speed);

        while (movingTransform != null && Vector3.Distance(movingTransform.position, targetPosition) > 0.5f)
        {
            movingTransform.position = Vector3.MoveTowards(
                movingTransform.position,
                targetPosition,
                moveSpeed * Time.deltaTime);
            yield return null;
        }

        if (movingTransform != null)
        {
            movingTransform.position = targetPosition;
        }
    }

    private void ApplyRescueBusDirection(Vector3 from, Vector3 to)
    {
        Sprite sprite = rescueBusSprites.GetSprite(from, to);
        if (rescueBusImage != null && sprite != null)
        {
            rescueBusImage.sprite = sprite;
        }
    }

    private void CompleteMissionAfterDelay()
    {
        if (isComplete)
        {
            return;
        }

        isRunning = false;
        isComplete = true;
        inputLocked = true;
        StopActiveDrag();
        SetToolUsable(shelterTool, false);
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

        SetActive(playRoot, false);
        SetActive(completeScreen, true);
        completionRoutine = null;
    }

    private IndustrialMissionThreeTool GetActiveTool()
    {
        if (phase == MissionPhase.PublicAnnouncementVehicle)
        {
            return publicAnnouncementVehicleTool;
        }

        if (phase == MissionPhase.RescueBus)
        {
            return busTool;
        }

        if (phase == MissionPhase.Shelters)
        {
            return shelterTool;
        }

        return null;
    }

    private bool CanPlaceOnArea(RectTransform toolTransform)
    {
        return areaDropTarget == null || RectTransformsOverlap(toolTransform, areaDropTarget);
    }

    private ShelterPlacementTarget FindAvailableShelterTarget(RectTransform toolTransform)
    {
        for (int i = 0; i < shelterPlacementTargets.Count; i++)
        {
            ShelterPlacementTarget target = shelterPlacementTargets[i];
            if (target != null && target.CanAccept(toolTransform))
            {
                return target;
            }
        }

        return null;
    }

    private void StopActiveDrag()
    {
        if (draggedTool != null)
        {
            draggedTool.ResetPosition();
            draggedTool = null;
        }
    }

    private void StopMissionRoutines()
    {
        StopShake();

        if (routeRoutine != null)
        {
            StopCoroutine(routeRoutine);
            routeRoutine = null;
        }

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }
    }

    private void StopShake()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }
    }

    private void ResetTools()
    {
        ResetTool(publicAnnouncementVehicleTool, true);
        ResetTool(busTool, false);
        ResetTool(shelterTool, false);
    }

    private void ResetTool(IndustrialMissionThreeTool tool, bool usable)
    {
        if (tool == null)
        {
            return;
        }

        tool.ResetPosition();
        tool.SetUsable(usable);
    }

    private void ResetToolWithShake(IndustrialMissionThreeTool tool)
    {
        if (tool == null)
        {
            return;
        }

        tool.ResetPosition();
        tool.SetUsable(true);

        StopShake();
        shakeRoutine = StartCoroutine(ShakeTool(tool));
    }

    private IEnumerator ShakeTool(IndustrialMissionThreeTool tool)
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

        tool.ResetPosition();
        shakeRoutine = null;
    }

    private void ResetRouteObjects()
    {
        SetActive(announcementVehiclePointerObject, false);
        SetActive(rescueBusObject, false);

        int announcementStartIndex = FindFirstValidRoutePointIndex(announcementVehicleRoutePoints);
        if (announcementVehiclePointer != null && announcementStartIndex >= 0)
        {
            announcementVehiclePointer.position = announcementVehicleRoutePoints[announcementStartIndex].position;
        }

        int rescueBusStartIndex = FindFirstValidRescueRoutePointIndex();
        if (rescueBusTransform != null && rescueBusStartIndex >= 0)
        {
            rescueBusTransform.position = rescueBusRoutePoints[rescueBusStartIndex].Point.position;
        }
    }

    private int FindFirstValidRescueRoutePointIndex()
    {
        for (int i = 0; i < rescueBusRoutePoints.Count; i++)
        {
            if (rescueBusRoutePoints[i]?.Point != null)
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindFirstValidRoutePointIndex(List<RectTransform> routePoints)
    {
        if (routePoints == null)
        {
            return -1;
        }

        for (int i = 0; i < routePoints.Count; i++)
        {
            if (routePoints[i] != null)
            {
                return i;
            }
        }

        return -1;
    }

    private void CacheShelterTargets()
    {
        for (int i = 0; i < shelterPlacementTargets.Count; i++)
        {
            shelterPlacementTargets[i]?.CacheInitialState();
        }
    }

    private void ResetShelterTargets()
    {
        for (int i = 0; i < shelterPlacementTargets.Count; i++)
        {
            shelterPlacementTargets[i]?.ResetTarget();
        }
    }

    private bool HasPendingShelters()
    {
        return placedShelters < GetShelterTargetCount();
    }

    private int GetShelterTargetCount()
    {
        int count = 0;
        for (int i = 0; i < shelterPlacementTargets.Count; i++)
        {
            if (shelterPlacementTargets[i] != null)
            {
                count++;
            }
        }

        return count;
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

    private void WireLanguageController()
    {
        if (languageToggleController == null || languageControllerWired)
        {
            return;
        }

        languageToggleController.LanguageChanged += HandleLanguageChanged;
        languageControllerWired = true;
    }

    private void HandleLanguageChanged(bool useGujarati)
    {
        RefreshToolLanguageState();
    }

    private void RefreshToolLanguageState()
    {
        publicAnnouncementVehicleTool?.CacheCurrentLanguageState();
        busTool?.CacheCurrentLanguageState();
        shelterTool?.CacheCurrentLanguageState();
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

    private static void SetToolUsable(IndustrialMissionThreeTool tool, bool usable)
    {
        tool?.SetUsable(usable);
    }

    private static bool TryGetPointerWorldPosition(
        IndustrialMissionThreeTool tool,
        Vector2 screenPosition,
        out Vector3 pointerWorldPosition)
    {
        pointerWorldPosition = Vector3.zero;
        if (tool == null || tool.ToolTransform == null)
        {
            return false;
        }

        RectTransform parentRect = tool.ToolTransform.parent as RectTransform;
        return parentRect != null &&
               RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRect, screenPosition, null, out pointerWorldPosition);
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
        PublicAnnouncementVehicle,
        RescueBus,
        Shelters
    }

    [System.Serializable]
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

    [System.Serializable]
    private sealed class IndustrialMissionThreeTool
    {
        [SerializeField] private RectTransform toolTransform;
        [SerializeField] private Image toolImage;
        [SerializeField] private Sprite dragSprite;
        [SerializeField, Range(0.1f, 2f)] private float dragSizeMultiplier = 1f;

        private Vector2 initialPosition;
        private Vector2 initialSizeDelta;
        private Vector3 initialScale;
        private Color initialColor;
        private Sprite initialSprite;
        private bool initialPreserveAspect;
        private int initialSiblingIndex;

        public RectTransform ToolTransform => toolTransform;
        public Vector2 InitialPosition => initialPosition;

        public void CacheInitialState()
        {
            if (toolTransform != null)
            {
                initialPosition = toolTransform.anchoredPosition;
                initialSizeDelta = toolTransform.sizeDelta;
                initialScale = toolTransform.localScale;
                initialSiblingIndex = toolTransform.GetSiblingIndex();
            }

            if (toolImage != null)
            {
                initialSprite = toolImage.sprite;
                initialColor = toolImage.color;
                initialPreserveAspect = toolImage.preserveAspect;
                toolImage.raycastTarget = true;
            }
        }

        public void CacheCurrentLanguageState()
        {
            if (toolTransform != null)
            {
                initialSizeDelta = toolTransform.sizeDelta;
            }

            if (toolImage != null)
            {
                initialSprite = toolImage.sprite;
                initialPreserveAspect = toolImage.preserveAspect;
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

        public void BeginDragVisual()
        {
            if (toolTransform != null)
            {
                toolTransform.SetAsLastSibling();
                toolTransform.localScale = initialScale;
                toolTransform.sizeDelta = initialSizeDelta * dragSizeMultiplier;
            }

            if (toolImage != null && dragSprite != null)
            {
                toolImage.sprite = dragSprite;
                toolImage.preserveAspect = true;
                toolImage.color = Color.white;

                if (toolTransform != null)
                {
                    toolImage.SetNativeSize();
                    toolTransform.sizeDelta *= dragSizeMultiplier;
                }
            }
        }

        public void ResetPosition()
        {
            if (toolTransform != null)
            {
                toolTransform.anchoredPosition = initialPosition;
                toolTransform.sizeDelta = initialSizeDelta;
                toolTransform.localScale = initialScale;
                toolTransform.SetSiblingIndex(initialSiblingIndex);
            }

            if (toolImage != null)
            {
                toolImage.sprite = initialSprite;
                toolImage.preserveAspect = initialPreserveAspect;
                toolImage.color = initialColor;
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

    [System.Serializable]
    private sealed class RescueBusRoutePoint
    {
        [SerializeField] private RectTransform point;
        [SerializeField] private float pauseSeconds;

        public RectTransform Point => point;
        public float PauseSeconds => Mathf.Max(0f, pauseSeconds);
    }

    [System.Serializable]
    private sealed class BusDirectionSprites
    {
        [SerializeField] private Sprite upSprite;
        [SerializeField] private Sprite downSprite;
        [SerializeField] private Sprite leftSprite;
        [SerializeField] private Sprite rightSprite;

        public Sprite GetSprite(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude <= 0.01f)
            {
                return null;
            }

            if (Mathf.Approximately(delta.x, 0f))
            {
                return delta.y >= 0f ? upSprite : downSprite;
            }

            if (Mathf.Approximately(delta.y, 0f))
            {
                return delta.x >= 0f ? rightSprite : leftSprite;
            }

            if (delta.y > 0f)
            {
                return delta.x < 0f ? upSprite : rightSprite;
            }

            return delta.x < 0f ? leftSprite : downSprite;
        }
    }

    [System.Serializable]
    private sealed class ShelterPlacementTarget
    {
        [SerializeField] private string targetName;
        [SerializeField] private RectTransform placementPoint;
        [SerializeField] private GameObject placedVisual;

        private bool initialPlacedVisualActive;
        private bool isPlaced;

        public void CacheInitialState()
        {
            initialPlacedVisualActive = placedVisual != null && placedVisual.activeSelf;
        }

        public bool CanAccept(RectTransform toolTransform)
        {
            return !isPlaced &&
                   RectTransformsOverlap(toolTransform, placementPoint);
        }

        public void MarkPlaced()
        {
            isPlaced = true;
            SetActive(placedVisual, true);
        }

        public void ResetTarget()
        {
            isPlaced = false;
            SetActive(placedVisual, initialPlacedVisualActive);
        }
    }
}

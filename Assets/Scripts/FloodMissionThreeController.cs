using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class FloodMissionThreeController : MonoBehaviour
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
    [SerializeField] private Slider timeRemainingSlider;
    [SerializeField] private TextMeshProUGUI timeRemainingLabel;
    [SerializeField] private RISCOMLanguageToggleController languageToggleController;
    [SerializeField] private float missionDurationSeconds = DefaultMissionDurationSeconds;
    [SerializeField] private float completionDelaySeconds = 2f;
    [SerializeField] private FloodTool boatTool;
    [SerializeField] private FloodTool generatorTool;
    [SerializeField] private FloodTool dewateringPumpTool;
    [SerializeField] private RectTransform boatExitBounds;
    [SerializeField] private float boatExitPadding = 140f;
    [SerializeField] private float boatExitDurationSeconds = 1.35f;
    [SerializeField] private float boatRotationOffsetDegrees = -30f;
    [SerializeField] private List<SosHouseTarget> sosHouseTargets = new List<SosHouseTarget>();
    [SerializeField] private List<BuildingTarget> buildingTargets = new List<BuildingTarget>();

    private readonly List<Coroutine> boatExitRoutines = new List<Coroutine>();

    private Action onReportNext;
    private MissionPhase phase;
    private FloodTool draggedTool;
    private Vector3 dragWorldOffset;
    private float timeRemaining;
    private int placedBoats;
    private int placedGenerators;
    private int placedPumps;
    private bool configured;
    private bool buttonsWired;
    private bool languageControllerWired;
    private bool isRunning;
    private bool isComplete;
    private bool inputLocked;
    private Coroutine shakeRoutine;
    private Coroutine completionRoutine;

    public void Configure(Action reportNextHandler = null)
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

        boatTool?.CacheInitialState();
        generatorTool?.CacheInitialState();
        dewateringPumpTool?.CacheInitialState();
        CacheSosHouseTargets();
        CacheBuildingTargets();
        ConfigureTimerSlider();

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

    private void OnDisable()
    {
        UnwireLanguageController();
    }

    private void Update()
    {
        if (!isRunning || isComplete)
        {
            return;
        }

        UpdateTimer();

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
        SetActive(completeScreen, false);
        SetActive(playRoot, true);
        BeginMission();
    }

    private void BeginMission()
    {
        Configure(onReportNext);
        StopMissionRoutines();

        phase = MissionPhase.Boats;
        draggedTool = null;
        placedBoats = 0;
        placedGenerators = 0;
        placedPumps = 0;
        timeRemaining = missionDurationSeconds;
        isRunning = true;
        isComplete = false;
        inputLocked = false;

        ResetTools();
        ResetSosHouseTargets();
        ResetBuildingTargets();
        SetToolUsable(boatTool, true);
        SetToolUsable(generatorTool, false);
        SetToolUsable(dewateringPumpTool, false);
        UpdateTimerDisplay();

        if (GetSosHouseTargetCount() == 0)
        {
            EnterGeneratorPhase();
        }
    }

    private void StopMission()
    {
        ResetActiveDrag();
        StopMissionRoutines();

        isRunning = false;
        isComplete = false;
        inputLocked = false;

        if (!configured)
        {
            return;
        }

        ResetTools();
        ResetSosHouseTargets();
        ResetBuildingTargets();
    }

    private void StopMissionRoutines()
    {
        StopBoatExitRoutines();
        StopShake();

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }
    }

    private void UpdateTimer()
    {
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerDisplay();

        if (timeRemaining <= 0f)
        {
            StopMission();
            Debug.LogWarning("Flood Mission 3 timer expired.");
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
                EndDrag(touchPosition);
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
            EndDrag(mousePosition);
        }
    }

    private void BeginDrag(Vector2 screenPosition)
    {
        if (draggedTool != null)
        {
            return;
        }

        FloodTool selectedTool = GetActiveTool();
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

            draggedTool.ToolTransform.SetAsLastSibling();
        }

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

    private void EndDrag(Vector2 screenPosition)
    {
        if (draggedTool == null)
        {
            return;
        }

        FloodTool droppedTool = draggedTool;
        draggedTool = null;

        if (phase == MissionPhase.Boats && droppedTool == boatTool && TryPlaceBoat(droppedTool.ToolTransform))
        {
            ResetTool(droppedTool, HasPendingBoats());
            if (!HasPendingBoats())
            {
                EnterGeneratorPhase();
            }

            return;
        }

        if (phase == MissionPhase.Generators && droppedTool == generatorTool && TryPlaceGenerator(droppedTool.ToolTransform))
        {
            ResetTool(droppedTool, HasPendingGenerators());
            if (!HasPendingGenerators())
            {
                EnterPumpPhase();
            }

            return;
        }

        if (phase == MissionPhase.DewateringPumps && droppedTool == dewateringPumpTool && TryPlacePump(droppedTool.ToolTransform))
        {
            ResetTool(droppedTool, HasPendingPumps());
            if (!HasPendingPumps())
            {
                CompleteMissionAfterDelay();
            }

            return;
        }

        ResetToolWithShake(droppedTool);
    }

    private bool TryPlaceBoat(RectTransform toolTransform)
    {
        SosHouseTarget target = FindSosHouseTarget(toolTransform);
        if (target == null)
        {
            return false;
        }

        RectTransform boatTransform = target.MarkBoatPlaced();
        StartBoatExitMovement(target, boatTransform);
        placedBoats++;
        return true;
    }

    private bool TryPlaceGenerator(RectTransform toolTransform)
    {
        BuildingTarget target = FindGeneratorTarget(toolTransform);
        if (target == null)
        {
            return false;
        }

        target.MarkGeneratorPlaced();
        placedGenerators++;
        return true;
    }

    private bool TryPlacePump(RectTransform toolTransform)
    {
        BuildingTarget target = FindPumpTarget(toolTransform);
        if (target == null)
        {
            return false;
        }

        target.MarkPumpPlaced(IsGujaratiEnabled(), dewateringPumpTool != null ? dewateringPumpTool.ToolSprite : null);
        placedPumps++;
        return true;
    }

    private SosHouseTarget FindSosHouseTarget(RectTransform toolTransform)
    {
        for (int i = 0; i < sosHouseTargets.Count; i++)
        {
            SosHouseTarget target = sosHouseTargets[i];
            if (target != null && target.CanAcceptBoat(toolTransform))
            {
                return target;
            }
        }

        return null;
    }

    private BuildingTarget FindGeneratorTarget(RectTransform toolTransform)
    {
        for (int i = 0; i < buildingTargets.Count; i++)
        {
            BuildingTarget target = buildingTargets[i];
            if (target != null && target.CanAcceptGenerator(toolTransform))
            {
                return target;
            }
        }

        return null;
    }

    private BuildingTarget FindPumpTarget(RectTransform toolTransform)
    {
        for (int i = 0; i < buildingTargets.Count; i++)
        {
            BuildingTarget target = buildingTargets[i];
            if (target != null && target.CanAcceptPump(toolTransform))
            {
                return target;
            }
        }

        return null;
    }

    private void EnterGeneratorPhase()
    {
        phase = MissionPhase.Generators;
        SetToolUsable(boatTool, false);
        SetToolUsable(generatorTool, true);
        SetToolUsable(dewateringPumpTool, false);

        if (GetBuildingTargetCount() == 0)
        {
            EnterPumpPhase();
        }
    }

    private void EnterPumpPhase()
    {
        phase = MissionPhase.DewateringPumps;
        SetToolUsable(generatorTool, false);
        SetToolUsable(dewateringPumpTool, true);

        if (GetBuildingTargetCount() == 0)
        {
            CompleteMissionAfterDelay();
        }
    }

    private void CompleteMissionAfterDelay()
    {
        isRunning = false;
        isComplete = true;
        inputLocked = true;
        SetToolUsable(dewateringPumpTool, false);

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
        }

        completionRoutine = StartCoroutine(ShowCompletionAfterDelay());
    }

    private IEnumerator ShowCompletionAfterDelay()
    {
        yield return new WaitForSeconds(completionDelaySeconds);

        SetActive(playRoot, false);
        SetActive(completeScreen, true);
        completionRoutine = null;
    }

    private FloodTool GetActiveTool()
    {
        if (phase == MissionPhase.Boats)
        {
            return boatTool;
        }

        if (phase == MissionPhase.Generators)
        {
            return generatorTool;
        }

        if (phase == MissionPhase.DewateringPumps)
        {
            return dewateringPumpTool;
        }

        return null;
    }

    private void ResetTools()
    {
        ResetTool(boatTool, true);
        ResetTool(generatorTool, false);
        ResetTool(dewateringPumpTool, false);
    }

    private static void ResetTool(FloodTool tool, bool usable)
    {
        if (tool == null)
        {
            return;
        }

        tool.ResetPosition();
        tool.SetUsable(usable);
    }

    private static void SetToolUsable(FloodTool tool, bool usable)
    {
        tool?.SetUsable(usable);
    }

    private void ResetActiveDrag()
    {
        if (draggedTool != null)
        {
            ResetTool(draggedTool, true);
            draggedTool = null;
        }
    }

    private void ResetToolWithShake(FloodTool tool)
    {
        StopShake();
        shakeRoutine = StartCoroutine(ShakeTool(tool));
    }

    private IEnumerator ShakeTool(FloodTool tool)
    {
        if (tool == null || tool.ToolTransform == null)
        {
            yield break;
        }

        float elapsed = 0f;
        tool.SetUsable(true);
        tool.RestoreScaleAndOrder();

        while (elapsed < WrongDropShakeDuration)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * WrongDropShakeFrequency) * WrongDropShakeAmplitude;
            tool.ToolTransform.anchoredPosition = tool.InitialPosition + new Vector2(offset, 0f);
            yield return null;
        }

        tool.ResetPosition();
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

    private void CacheSosHouseTargets()
    {
        for (int i = 0; i < sosHouseTargets.Count; i++)
        {
            sosHouseTargets[i]?.CacheInitialState();
        }
    }

    private void CacheBuildingTargets()
    {
        for (int i = 0; i < buildingTargets.Count; i++)
        {
            buildingTargets[i]?.CacheInitialState();
        }
    }

    private void ResetSosHouseTargets()
    {
        for (int i = 0; i < sosHouseTargets.Count; i++)
        {
            sosHouseTargets[i]?.ResetTarget();
        }
    }

    private void ResetBuildingTargets()
    {
        for (int i = 0; i < buildingTargets.Count; i++)
        {
            buildingTargets[i]?.ResetTarget();
        }
    }

    private bool HasPendingBoats()
    {
        return placedBoats < GetSosHouseTargetCount();
    }

    private bool HasPendingGenerators()
    {
        return placedGenerators < GetBuildingTargetCount();
    }

    private bool HasPendingPumps()
    {
        return placedPumps < GetBuildingTargetCount();
    }

    private int GetSosHouseTargetCount()
    {
        int count = 0;
        for (int i = 0; i < sosHouseTargets.Count; i++)
        {
            if (sosHouseTargets[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private int GetBuildingTargetCount()
    {
        int count = 0;
        for (int i = 0; i < buildingTargets.Count; i++)
        {
            if (buildingTargets[i] != null)
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

    private void UnwireLanguageController()
    {
        if (languageToggleController == null || !languageControllerWired)
        {
            return;
        }

        languageToggleController.LanguageChanged -= HandleLanguageChanged;
        languageControllerWired = false;
    }

    private void HandleLanguageChanged(bool useGujarati)
    {
        RefreshPlacedPumpSprites(useGujarati);
    }

    private bool IsGujaratiEnabled()
    {
        return languageToggleController != null && languageToggleController.IsGujaratiEnabled;
    }

    private void RefreshPlacedPumpSprites(bool useGujarati)
    {
        Sprite fallbackSprite = dewateringPumpTool != null ? dewateringPumpTool.ToolSprite : null;

        for (int i = 0; i < buildingTargets.Count; i++)
        {
            buildingTargets[i]?.RefreshPumpSprite(useGujarati, fallbackSprite);
        }
    }

    private void StartBoatExitMovement(SosHouseTarget target, RectTransform boatTransform)
    {
        if (target == null || boatTransform == null)
        {
            return;
        }

        boatExitRoutines.Add(StartCoroutine(MoveBoatOutOfMap(target, boatTransform)));
    }

    private IEnumerator MoveBoatOutOfMap(SosHouseTarget target, RectTransform boatTransform)
    {
        Vector3 startPosition = boatTransform.position;
        List<Vector3> routePositions = new List<Vector3> { startPosition };
        routePositions.AddRange(target.GetBoatRouteWorldPositions());

        if (routePositions.Count == 1)
        {
            routePositions.Add(GetBoatExitPosition(boatTransform));
        }

        NormalizeRouteDepth(routePositions, startPosition.z);
        yield return MoveBoatAlongRoute(boatTransform, routePositions, target.GetBoatRotationOffset(boatRotationOffsetDegrees));

        target.HideBoatAfterExit();
    }

    private IEnumerator MoveBoatAlongRoute(RectTransform boatTransform, List<Vector3> routePositions, float rotationOffsetDegrees)
    {
        if (boatTransform == null || routePositions == null || routePositions.Count < 2)
        {
            yield break;
        }

        for (int segmentIndex = 0; segmentIndex < routePositions.Count - 1 && boatTransform != null; segmentIndex++)
        {
            Vector3 previousPoint = routePositions[Mathf.Max(segmentIndex - 1, 0)];
            Vector3 startPoint = routePositions[segmentIndex];
            Vector3 endPoint = routePositions[segmentIndex + 1];
            Vector3 nextPoint = routePositions[Mathf.Min(segmentIndex + 2, routePositions.Count - 1)];
            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, boatExitDurationSeconds);

            FaceBoatToward(boatTransform, GetRouteTangent(previousPoint, startPoint, endPoint, nextPoint, 0f), rotationOffsetDegrees);

            while (elapsed < duration && boatTransform != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                Vector3 routePosition = GetRoutePosition(previousPoint, startPoint, endPoint, nextPoint, progress);
                Vector3 routeTangent = GetRouteTangent(previousPoint, startPoint, endPoint, nextPoint, progress);

                boatTransform.position = routePosition;
                FaceBoatToward(boatTransform, routeTangent, rotationOffsetDegrees);

                yield return null;
            }

            if (boatTransform != null)
            {
                boatTransform.position = endPoint;
                FaceBoatToward(boatTransform, endPoint - startPoint, rotationOffsetDegrees);
            }
        }
    }

    private static void NormalizeRouteDepth(List<Vector3> routePositions, float z)
    {
        for (int i = 0; i < routePositions.Count; i++)
        {
            Vector3 position = routePositions[i];
            position.z = z;
            routePositions[i] = position;
        }
    }

    private static Vector3 GetRoutePosition(Vector3 previousPoint, Vector3 startPoint, Vector3 endPoint, Vector3 nextPoint, float progress)
    {
        if ((previousPoint - startPoint).sqrMagnitude <= 0.001f &&
            (endPoint - nextPoint).sqrMagnitude <= 0.001f)
        {
            return Vector3.Lerp(startPoint, endPoint, progress);
        }

        float t2 = progress * progress;
        float t3 = t2 * progress;

        return 0.5f * ((2f * startPoint) +
                       (-previousPoint + endPoint) * progress +
                       (2f * previousPoint - 5f * startPoint + 4f * endPoint - nextPoint) * t2 +
                       (-previousPoint + 3f * startPoint - 3f * endPoint + nextPoint) * t3);
    }

    private static Vector3 GetRouteTangent(Vector3 previousPoint, Vector3 startPoint, Vector3 endPoint, Vector3 nextPoint, float progress)
    {
        if ((previousPoint - startPoint).sqrMagnitude <= 0.001f &&
            (endPoint - nextPoint).sqrMagnitude <= 0.001f)
        {
            return endPoint - startPoint;
        }

        float t2 = progress * progress;

        return 0.5f * ((-previousPoint + endPoint) +
                       2f * (2f * previousPoint - 5f * startPoint + 4f * endPoint - nextPoint) * progress +
                       3f * (-previousPoint + 3f * startPoint - 3f * endPoint + nextPoint) * t2);
    }

    private static void FaceBoatToward(RectTransform boatTransform, Vector3 direction, float rotationOffsetDegrees)
    {
        if (boatTransform == null || direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffsetDegrees;
        boatTransform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Vector3 GetBoatExitPosition(RectTransform boatTransform)
    {
        RectTransform bounds = boatExitBounds != null ? boatExitBounds : playRoot != null ? playRoot.transform as RectTransform : null;
        if (bounds == null || boatTransform == null)
        {
            return boatTransform != null ? boatTransform.position : Vector3.zero;
        }

        Vector2 boatLocalPosition;
        Vector2 direction;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bounds,
            RectTransformUtility.WorldToScreenPoint(null, boatTransform.position),
            null,
            out boatLocalPosition);

        direction = boatLocalPosition.sqrMagnitude > 0.001f ? boatLocalPosition.normalized : Vector2.right;

        Rect rect = bounds.rect;
        float halfWidth = rect.width * 0.5f;
        float halfHeight = rect.height * 0.5f;
        float xDistance = Mathf.Abs(direction.x) > 0.001f ? halfWidth / Mathf.Abs(direction.x) : float.PositiveInfinity;
        float yDistance = Mathf.Abs(direction.y) > 0.001f ? halfHeight / Mathf.Abs(direction.y) : float.PositiveInfinity;
        float edgeDistance = Mathf.Min(xDistance, yDistance);
        Vector2 exitLocalPosition = direction * (edgeDistance + Mathf.Max(0f, boatExitPadding));

        Vector3 exitWorldPosition = bounds.TransformPoint(exitLocalPosition);
        exitWorldPosition.z = boatTransform.position.z;
        return exitWorldPosition;
    }

    private void StopBoatExitRoutines()
    {
        for (int i = 0; i < boatExitRoutines.Count; i++)
        {
            if (boatExitRoutines[i] != null)
            {
                StopCoroutine(boatExitRoutines[i]);
            }
        }

        boatExitRoutines.Clear();
    }

    private static bool TryGetPointerWorldPosition(FloodTool tool, Vector2 screenPosition, out Vector3 pointerWorldPosition)
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
        Boats,
        Generators,
        DewateringPumps
    }

    [Serializable]
    private sealed class FloodTool
    {
        [SerializeField] private RectTransform toolTransform;
        [SerializeField] private Image toolImage;

        private Vector2 initialPosition;
        private Vector3 initialScale;
        private Color initialColor;
        private int initialSiblingIndex;

        public RectTransform ToolTransform => toolTransform;
        public Vector2 InitialPosition => initialPosition;
        public Sprite ToolSprite => toolImage != null ? toolImage.sprite : null;

        public void CacheInitialState()
        {
            if (toolTransform != null)
            {
                initialPosition = toolTransform.anchoredPosition;
                initialScale = toolTransform.localScale;
                initialSiblingIndex = toolTransform.GetSiblingIndex();
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
            if (toolTransform == null)
            {
                return;
            }

            toolTransform.anchoredPosition = initialPosition;
            RestoreScaleAndOrder();
        }

        public void RestoreScaleAndOrder()
        {
            if (toolTransform == null)
            {
                return;
            }

            toolTransform.localScale = initialScale;
            toolTransform.SetSiblingIndex(initialSiblingIndex);
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
    private sealed class SosHouseTarget
    {
        [SerializeField] private string targetName;
        [SerializeField] private RectTransform dropTarget;
        [SerializeField] private GameObject boatObject;
        [SerializeField] private RectTransform boatTransform;
        [SerializeField] private List<RectTransform> boatRoutePoints = new List<RectTransform>();
        [SerializeField] private bool overrideBoatRotationOffset;
        [SerializeField] private float boatRotationOffsetDegrees;
        [SerializeField] private GameObject infoIcon;

        private Vector2 initialBoatPosition;
        private Vector3 initialBoatScale;
        private Quaternion initialBoatRotation;
        private bool isBoatPlaced;

        public void CacheInitialState()
        {
            if (boatTransform != null)
            {
                initialBoatPosition = boatTransform.anchoredPosition;
                initialBoatScale = boatTransform.localScale;
                initialBoatRotation = boatTransform.localRotation;
            }

            SetActive(boatObject, false);
            SetActive(infoIcon, true);
        }

        public bool CanAcceptBoat(RectTransform toolTransform)
        {
            return !isBoatPlaced &&
                   RectTransformsOverlap(toolTransform, dropTarget);
        }

        public RectTransform MarkBoatPlaced()
        {
            isBoatPlaced = true;
            RestoreBoatVisual();
            SetActive(boatObject, true);
            SetActive(infoIcon, false);
            return boatTransform;
        }

        public List<Vector3> GetBoatRouteWorldPositions()
        {
            List<Vector3> routePositions = new List<Vector3>();

            for (int i = 0; i < boatRoutePoints.Count; i++)
            {
                if (boatRoutePoints[i] != null)
                {
                    routePositions.Add(boatRoutePoints[i].position);
                }
            }

            return routePositions;
        }

        public float GetBoatRotationOffset(float defaultOffset)
        {
            return overrideBoatRotationOffset ? boatRotationOffsetDegrees : defaultOffset;
        }

        public void HideBoatAfterExit()
        {
            RestoreBoatVisual();
            SetActive(boatObject, false);
        }

        public void ResetTarget()
        {
            isBoatPlaced = false;
            RestoreBoatVisual();
            SetActive(boatObject, false);
            SetActive(infoIcon, true);
        }

        private void RestoreBoatVisual()
        {
            if (boatTransform == null)
            {
                return;
            }

            boatTransform.anchoredPosition = initialBoatPosition;
            boatTransform.localScale = initialBoatScale;
            boatTransform.localRotation = initialBoatRotation;
        }
    }

    [Serializable]
    private sealed class BuildingTarget
    {
        [SerializeField] private string targetName;
        [SerializeField] private RectTransform dropTarget;
        [SerializeField] private GameObject generatorObject;
        [SerializeField] private Image generatorImage;
        [SerializeField, FormerlySerializedAs("dewateringPumpSprite")] private Sprite englishDewateringPumpSprite;
        [SerializeField] private Sprite gujaratiDewateringPumpSprite;

        private Sprite initialGeneratorSprite;
        private Color initialGeneratorColor;
        private bool initialGeneratorPreserveAspect;
        private bool isGeneratorPlaced;
        private bool isPumpPlaced;

        public void CacheInitialState()
        {
            if (generatorImage != null)
            {
                initialGeneratorSprite = generatorImage.sprite;
                initialGeneratorColor = generatorImage.color;
                initialGeneratorPreserveAspect = generatorImage.preserveAspect;
            }

            SetActive(generatorObject, false);
        }

        public bool CanAcceptGenerator(RectTransform toolTransform)
        {
            return !isGeneratorPlaced &&
                   RectTransformsOverlap(toolTransform, dropTarget);
        }

        public bool CanAcceptPump(RectTransform toolTransform)
        {
            return isGeneratorPlaced &&
                   !isPumpPlaced &&
                   RectTransformsOverlap(toolTransform, dropTarget);
        }

        public void MarkGeneratorPlaced()
        {
            isGeneratorPlaced = true;
            SetActive(generatorObject, true);

            if (generatorImage != null)
            {
                generatorImage.sprite = initialGeneratorSprite;
                generatorImage.color = initialGeneratorColor;
                generatorImage.preserveAspect = initialGeneratorPreserveAspect;
            }
        }

        public void MarkPumpPlaced(bool useGujarati, Sprite fallbackPumpSprite)
        {
            isPumpPlaced = true;
            SetActive(generatorObject, true);
            ApplyPumpSprite(useGujarati, fallbackPumpSprite);
        }

        public void RefreshPumpSprite(bool useGujarati, Sprite fallbackPumpSprite)
        {
            if (isPumpPlaced)
            {
                ApplyPumpSprite(useGujarati, fallbackPumpSprite);
            }
        }

        private void ApplyPumpSprite(bool useGujarati, Sprite fallbackPumpSprite)
        {
            if (generatorImage == null)
            {
                return;
            }

            Sprite replacementSprite = GetDewateringPumpSprite(useGujarati, fallbackPumpSprite);
            if (replacementSprite == null)
            {
                return;
            }

            generatorImage.sprite = replacementSprite;
            generatorImage.preserveAspect = true;
        }

        public void ResetTarget()
        {
            isGeneratorPlaced = false;
            isPumpPlaced = false;

            if (generatorImage != null)
            {
                generatorImage.sprite = initialGeneratorSprite;
                generatorImage.color = initialGeneratorColor;
                generatorImage.preserveAspect = initialGeneratorPreserveAspect;
            }

            SetActive(generatorObject, false);
        }

        private Sprite GetDewateringPumpSprite(bool useGujarati, Sprite fallbackPumpSprite)
        {
            if (useGujarati && gujaratiDewateringPumpSprite != null)
            {
                return gujaratiDewateringPumpSprite;
            }

            return englishDewateringPumpSprite != null ? englishDewateringPumpSprite : fallbackPumpSprite;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class CycloneMissionTwoController : MonoBehaviour
{
    private const int StepCount = 3;
    private const float MissionDurationSeconds = 600f;
    private const float EffectAnimationDuration = 0.85f;
    private const float BoatMoveDuration = 0.85f;
    private const float BoatReadyVibrationDuration = 0.18f;
    private const int BoatReadyVibrato = 18;
    private const float BoatReadyRandomness = 65f;
    private const float ReportDelaySeconds = 2f;
    private static readonly Vector3 BoatReadyVibrationStrength = new Vector3(2f, 1.4f, 0f);

    [SerializeField] private GameObject alertScreen;
    [SerializeField] private Button alertNextButton;
    [SerializeField] private GameObject playRoot;
    [SerializeField] private GameObject completeScreen;
    [SerializeField] private Button reportNextButton;
    [SerializeField] private RectTransform dropArea;
    [SerializeField] private RectTransform boatDockingDropArea;
    [SerializeField] private Slider timeRemainingSlider;
    [SerializeField] private TextMeshProUGUI timeRemainingLabel;
    [SerializeField] private RectTransform radarScannerTool;
    [SerializeField] private Image radarScannerToolImage;
    [SerializeField] private GameObject radarScannerEffect;
    [SerializeField] private Image radarScannerEffectImage;
    [SerializeField] private RectTransform radioTransmitterTool;
    [SerializeField] private Image radioTransmitterToolImage;
    [SerializeField] private GameObject radioTransmitterEffect;
    [SerializeField] private Image radioTransmitterEffectImage;
    [SerializeField] private RectTransform boatDockingTool;
    [SerializeField] private Image boatDockingToolImage;
    [SerializeField] private GameObject boatDockingEffect;
    [SerializeField] private Image boatDockingEffectImage;
    [SerializeField] private GameObject[] boatJets;
    [SerializeField] private List<DockPlacement> dockPlacements = new List<DockPlacement>();

    private readonly MissionTwoStep[] steps = new MissionTwoStep[StepCount];
    private readonly List<RectTransform> managedBoats = new List<RectTransform>();

    private Action onReportNext;
    private bool configured;
    private bool buttonsWired;
    private int stepIndex;
    private float timeRemaining;
    private bool isRunning;
    private bool isComplete;
    private bool inputLocked;
    private MissionTwoStep draggedStep;
    private Vector3 dragWorldOffset;
    private Vector3[] boatStartPositions;
    private int dockPlacementIndex;
    private Coroutine shakeRoutine;
    private Coroutine effectRoutine;
    private Tween[] boatReadyTweens;
    private Coroutine reportDelayRoutine;

    public void Configure(Action reportNextHandler)
    {
        onReportNext = reportNextHandler;

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

        if (configured)
        {
            return;
        }

        steps[0] = new MissionTwoStep(radarScannerTool, radarScannerToolImage, radarScannerEffect, radarScannerEffectImage);
        steps[1] = new MissionTwoStep(radioTransmitterTool, radioTransmitterToolImage, radioTransmitterEffect, radioTransmitterEffectImage);
        steps[2] = new MissionTwoStep(boatDockingTool, boatDockingToolImage, boatDockingEffect, boatDockingEffectImage);

        for (int i = 0; i < steps.Length; i++)
        {
            MissionTwoStep step = steps[i];
            step.CacheInitialState();
            SetToolRaycast(step, true);
            HideEffect(step);
        }

        CacheBoatStartPositions();
        ResetBoats();
        ResetDocks();

        if (timeRemainingSlider != null)
        {
            timeRemainingSlider.minValue = 0f;
            timeRemainingSlider.maxValue = MissionDurationSeconds;
            timeRemainingSlider.interactable = false;
        }

        configured = true;
    }

    public void ShowIntro()
    {
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

        stepIndex = 0;
        dockPlacementIndex = 0;
        timeRemaining = MissionDurationSeconds;
        isRunning = true;
        isComplete = false;
        inputLocked = false;
        draggedStep = null;

        StopMissionRoutines();

        for (int i = 0; i < steps.Length; i++)
        {
            ResetTool(steps[i], true);
            HideEffect(steps[i]);
        }

        ResetBoats();
        ResetDocks();
        UpdateTimerDisplay();
    }

    private void StopMission()
    {
        ResetActiveDrag();
        isRunning = false;
        isComplete = false;
        inputLocked = false;
        StopMissionRoutines();

        if (configured)
        {
            for (int i = 0; i < steps.Length; i++)
            {
                HideEffect(steps[i]);
            }
        }

        ResetBoats();
        ResetDocks();
    }

    private void StopMissionRoutines()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        if (effectRoutine != null)
        {
            StopCoroutine(effectRoutine);
            effectRoutine = null;
        }

        StopBoatReadyShake(true);

        if (reportDelayRoutine != null)
        {
            StopCoroutine(reportDelayRoutine);
            reportDelayRoutine = null;
        }
    }

    private void UpdateTimer()
    {
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerDisplay();

        if (timeRemaining <= 0f)
        {
            StopMission();
            Debug.LogWarning("Cyclone Mission 2 timer expired.");
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
        if (draggedStep != null)
        {
            return;
        }

        MissionTwoStep step = FindToolAt(screenPosition);
        if (step == null)
        {
            return;
        }

        draggedStep = step;
        if (draggedStep.ToolTransform != null)
        {
            Vector3 pointerWorldPosition;
            dragWorldOffset = TryGetPointerWorldPosition(draggedStep, screenPosition, out pointerWorldPosition)
                ? draggedStep.ToolTransform.position - pointerWorldPosition
                : Vector3.zero;

            draggedStep.ToolTransform.SetAsLastSibling();
        }

        MoveDrag(screenPosition);
    }

    private void MoveDrag(Vector2 screenPosition)
    {
        if (draggedStep == null || draggedStep.ToolTransform == null)
        {
            return;
        }

        Vector3 pointerWorldPosition;
        if (TryGetPointerWorldPosition(draggedStep, screenPosition, out pointerWorldPosition))
        {
            draggedStep.ToolTransform.position = pointerWorldPosition + dragWorldOffset;
        }
    }

    private void EndDrag(Vector2 screenPosition)
    {
        if (draggedStep == null)
        {
            return;
        }

        MissionTwoStep droppedStep = draggedStep;
        draggedStep = null;

        bool isExpectedStep = stepIndex < steps.Length && droppedStep == steps[stepIndex];
        bool isOnDropArea = IsOnRequiredDropArea(droppedStep, screenPosition);

        if (isExpectedStep && isOnDropArea)
        {
            AcceptTool(droppedStep);
            return;
        }

        ResetToolWithShake(droppedStep);
    }

    private bool IsOnRequiredDropArea(MissionTwoStep step, Vector2 screenPosition)
    {
        RectTransform requiredDropArea = IsBoatDockingStep(step) && boatDockingDropArea != null
            ? boatDockingDropArea
            : dropArea;

        return requiredDropArea != null &&
               RectTransformUtility.RectangleContainsScreenPoint(requiredDropArea, screenPosition, null);
    }

    private MissionTwoStep FindToolAt(Vector2 screenPosition)
    {
        for (int i = 0; i < steps.Length; i++)
        {
            MissionTwoStep step = steps[i];
            if (step.ToolTransform == null || !step.ToolTransform.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(step.ToolTransform, screenPosition, null))
            {
                return step;
            }
        }

        return null;
    }

    private void AcceptTool(MissionTwoStep step)
    {
        inputLocked = true;

        if (IsBoatDockingStep(step))
        {
            AcceptBoatDockingTool(step);
            return;
        }

        stepIndex++;
        ResetTool(step, false);
        SetToolRaycast(step, false);

        if (IsRadarScannerStep(step))
        {
            SetBoatsVisible(true);
        }

        if (IsRadioTransmitterStep(step))
        {
            SetBoatJetsVisible(true);
            StartBoatReadyShake();
        }

        if (stepIndex >= steps.Length)
        {
            isRunning = false;
        }

        if (effectRoutine != null)
        {
            StopCoroutine(effectRoutine);
        }

        effectRoutine = StartCoroutine(PlayEffect(step, false));
    }

    private void AcceptBoatDockingTool(MissionTwoStep step)
    {
        if (!HasDockPlacements())
        {
            inputLocked = false;
            Debug.LogWarning("Cyclone Mission 2 needs Dock Placements assigned before boat docking can start.");
            ResetToolWithShake(step);
            return;
        }

        DockPlacement dockPlacement = ActivateNextDock();
        bool allDocksPlaced = AreAllDocksPlaced();
        if (dockPlacement != null)
        {
            StopBoatReadyShakeForMovements(dockPlacement.BoatMovements, true);
        }
        else if (allDocksPlaced)
        {
            StopBoatReadyShake(true);
        }

        ResetTool(step, !allDocksPlaced);
        SetToolRaycast(step, !allDocksPlaced);

        if (allDocksPlaced)
        {
            stepIndex = steps.Length;
            isRunning = false;
        }

        if (effectRoutine != null)
        {
            StopCoroutine(effectRoutine);
        }

        effectRoutine = StartCoroutine(PlayEffect(step, dockPlacement, allDocksPlaced));
    }

    private IEnumerator PlayEffect(MissionTwoStep step, bool completeAfterEffect)
    {
        yield return PlayEffect(step, null, completeAfterEffect);
    }

    private IEnumerator PlayEffect(MissionTwoStep step, DockPlacement dockPlacement, bool completeAfterEffect)
    {
        List<BoatMovement> dockBoatMovements = dockPlacement != null ? dockPlacement.BoatMovements : null;
        bool moveDockBoats = dockBoatMovements != null && dockBoatMovements.Count > 0;
        int boatMoveCount = moveDockBoats ? dockBoatMovements.Count : 0;
        float boatMoveDuration = boatMoveCount > 0 ? BoatMoveDuration * boatMoveCount : 0f;
        float duration = boatMoveCount > 0 ? Mathf.Max(EffectAnimationDuration, boatMoveDuration) : EffectAnimationDuration;
        Vector3[] movementStarts = moveDockBoats ? CreateBoatMovementStartPositions(dockBoatMovements) : null;

        if (step.EffectObject != null)
        {
            step.EffectObject.SetActive(true);
            step.EffectObject.transform.localScale = step.InitialEffectScale * 0.75f;
        }

        SetImageAlpha(step.EffectImage, step.InitialEffectColor.a);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float effectT = Mathf.Clamp01(elapsed / EffectAnimationDuration);

            if (step.EffectObject != null)
            {
                step.EffectObject.transform.localScale = Vector3.Lerp(
                    step.InitialEffectScale * 0.75f,
                    step.InitialEffectScale * 1.15f,
                    effectT);
            }

            SetImageAlpha(step.EffectImage, Mathf.Lerp(step.InitialEffectColor.a, 0f, effectT));

            if (moveDockBoats)
            {
                MoveBoatsSequentially(dockBoatMovements, movementStarts, boatMoveDuration > 0f ? Mathf.Clamp01(elapsed / boatMoveDuration) : 1f);
            }

            yield return null;
        }

        if (moveDockBoats)
        {
            MoveBoatsSequentially(dockBoatMovements, movementStarts, 1f);
        }

        HideEffect(step);
        effectRoutine = null;

        if (completeAfterEffect)
        {
            CompleteMission();
            yield break;
        }

        inputLocked = false;
    }

    private void CompleteMission()
    {
        isRunning = false;
        isComplete = true;
        inputLocked = false;
        draggedStep = null;
        StopBoatReadyShake(false);

        if (reportDelayRoutine != null)
        {
            StopCoroutine(reportDelayRoutine);
        }

        reportDelayRoutine = StartCoroutine(ShowReportAfterDelay());
    }

    private IEnumerator ShowReportAfterDelay()
    {
        yield return new WaitForSeconds(ReportDelaySeconds);

        SetActive(playRoot, false);
        SetActive(completeScreen, true);
        reportDelayRoutine = null;
    }

    private bool IsBoatDockingStep(MissionTwoStep step)
    {
        return steps.Length >= StepCount && step == steps[2];
    }

    private bool IsRadarScannerStep(MissionTwoStep step)
    {
        return steps.Length >= StepCount && step == steps[0];
    }

    private bool IsRadioTransmitterStep(MissionTwoStep step)
    {
        return steps.Length >= StepCount && step == steps[1];
    }

    private void CacheBoatStartPositions()
    {
        BuildManagedBoatList();

        int boatCount = managedBoats.Count;
        boatStartPositions = new Vector3[boatCount];

        for (int i = 0; i < boatCount; i++)
        {
            RectTransform boat = managedBoats[i];
            if (boat != null)
            {
                boatStartPositions[i] = boat.position;
            }
        }
    }

    private void ResetBoats()
    {
        if (boatStartPositions != null)
        {
            int boatCount = Mathf.Min(managedBoats.Count, boatStartPositions.Length);
            for (int i = 0; i < boatCount; i++)
            {
                RectTransform boat = managedBoats[i];
                if (boat != null)
                {
                    boat.position = boatStartPositions[i];
                }
            }
        }

        SetBoatsVisible(false);
        SetBoatJetsVisible(false);
    }

    private void MoveBoatsSequentially(List<BoatMovement> movements, Vector3[] starts, float progress)
    {
        if (movements == null || starts == null)
        {
            return;
        }

        int boatCount = Mathf.Min(movements.Count, starts.Length);
        float scaledProgress = Mathf.Clamp01(progress) * boatCount;
        for (int i = 0; i < boatCount; i++)
        {
            float easedProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(scaledProgress - i));
            MoveBoatToTarget(movements[i], starts[i], easedProgress);
        }
    }

    private static void MoveBoatToTarget(BoatMovement movement, Vector3 start, float easedProgress)
    {
        if (movement == null || movement.Boat == null || movement.Target == null)
        {
            return;
        }

        movement.Boat.position = Vector3.Lerp(start, movement.Target.position, easedProgress);
        if (easedProgress >= 1f)
        {
            SetActive(movement.Jet, false);
        }
    }

    private static Vector3[] CreateBoatMovementStartPositions(List<BoatMovement> movements)
    {
        if (movements == null)
        {
            return null;
        }

        Vector3[] starts = new Vector3[movements.Count];
        for (int i = 0; i < movements.Count; i++)
        {
            BoatMovement movement = movements[i];
            if (movement != null && movement.Boat != null)
            {
                starts[i] = movement.Boat.position;
            }
        }

        return starts;
    }

    private void BuildManagedBoatList()
    {
        managedBoats.Clear();

        if (dockPlacements == null)
        {
            return;
        }

        for (int i = 0; i < dockPlacements.Count; i++)
        {
            DockPlacement placement = dockPlacements[i];
            if (placement == null || placement.BoatMovements == null)
            {
                continue;
            }

            List<BoatMovement> movements = placement.BoatMovements;
            for (int movementIndex = 0; movementIndex < movements.Count; movementIndex++)
            {
                AddManagedBoat(movements[movementIndex] != null ? movements[movementIndex].Boat : null);
            }
        }
    }

    private void AddManagedBoat(RectTransform boat)
    {
        if (boat == null || managedBoats.Contains(boat))
        {
            return;
        }

        managedBoats.Add(boat);
    }

    private void SetBoatsVisible(bool visible)
    {
        int boatCount = managedBoats.Count;
        for (int i = 0; i < boatCount; i++)
        {
            RectTransform boat = managedBoats[i];
            if (boat != null)
            {
                boat.gameObject.SetActive(visible);
            }
        }
    }

    private void SetBoatJetsVisible(bool visible)
    {
        if (boatJets == null)
        {
            SetDockPlacementJetsVisible(visible);
            return;
        }

        for (int i = 0; i < boatJets.Length; i++)
        {
            SetActive(boatJets[i], visible);
        }

        SetDockPlacementJetsVisible(visible);
    }

    private void SetDockPlacementJetsVisible(bool visible)
    {
        if (dockPlacements == null)
        {
            return;
        }

        for (int i = 0; i < dockPlacements.Count; i++)
        {
            DockPlacement placement = dockPlacements[i];
            if (placement == null || placement.BoatMovements == null)
            {
                continue;
            }

            List<BoatMovement> movements = placement.BoatMovements;
            for (int movementIndex = 0; movementIndex < movements.Count; movementIndex++)
            {
                BoatMovement movement = movements[movementIndex];
                if (movement != null)
                {
                    SetActive(movement.Jet, visible);
                }
            }
        }
    }

    private void ResetDocks()
    {
        dockPlacementIndex = 0;

        if (dockPlacements == null)
        {
            return;
        }

        for (int i = 0; i < dockPlacements.Count; i++)
        {
            DockPlacement placement = dockPlacements[i];
            if (placement != null)
            {
                SetActive(placement.Dock, false);
            }
        }
    }

    private DockPlacement ActivateNextDock()
    {
        DockPlacement placement = GetCurrentDockPlacement();
        if (placement != null)
        {
            SetActive(placement.Dock, true);
            dockPlacementIndex++;
            return placement;
        }

        dockPlacementIndex++;
        return null;
    }

    private bool AreAllDocksPlaced()
    {
        int dockCount = dockPlacements != null ? dockPlacements.Count : 0;

        return dockCount == 0 || dockPlacementIndex >= dockCount;
    }

    private DockPlacement GetCurrentDockPlacement()
    {
        return HasDockPlacements() && dockPlacementIndex >= 0 && dockPlacementIndex < dockPlacements.Count
            ? dockPlacements[dockPlacementIndex]
            : null;
    }

    private bool HasDockPlacements()
    {
        return dockPlacements != null && dockPlacements.Count > 0;
    }

    private void StartBoatReadyShake()
    {
        StopBoatReadyShake(true);

        int boatCount = Mathf.Min(managedBoats.Count, boatStartPositions != null ? boatStartPositions.Length : 0);
        boatReadyTweens = new Tween[boatCount];
        for (int i = 0; i < boatCount; i++)
        {
            RectTransform boat = managedBoats[i];
            if (boat == null || !boat.gameObject.activeInHierarchy)
            {
                continue;
            }

            boat.position = boatStartPositions[i];
            boatReadyTweens[i] = boat
                .DOShakePosition(
                    BoatReadyVibrationDuration,
                    BoatReadyVibrationStrength,
                    BoatReadyVibrato,
                    BoatReadyRandomness,
                    false,
                    false)
                .SetLoops(-1, LoopType.Restart)
                .SetEase(Ease.Linear);
        }
    }

    private void StopBoatReadyShake(bool restoreStartPositions)
    {
        if (boatReadyTweens != null)
        {
            for (int i = 0; i < boatReadyTweens.Length; i++)
            {
                if (boatReadyTweens[i] != null && boatReadyTweens[i].IsActive())
                {
                    boatReadyTweens[i].Kill(false);
                }
            }

            boatReadyTweens = null;
        }

        if (restoreStartPositions)
        {
            RestoreBoatStartPositions();
        }
    }

    private void StopBoatReadyShakeForMovements(List<BoatMovement> movements, bool restoreTweenPosition)
    {
        if (movements == null || boatReadyTweens == null)
        {
            return;
        }

        for (int i = 0; i < movements.Count; i++)
        {
            BoatMovement movement = movements[i];
            StopBoatReadyShakeForBoat(movement != null ? movement.Boat : null, restoreTweenPosition);
        }
    }

    private void StopBoatReadyShakeForBoat(RectTransform boat, bool restoreTweenPosition)
    {
        int boatIndex = GetManagedBoatIndex(boat);
        if (boatIndex < 0 ||
            boatReadyTweens == null ||
            boatIndex >= boatReadyTweens.Length ||
            boatReadyTweens[boatIndex] == null ||
            !boatReadyTweens[boatIndex].IsActive())
        {
            return;
        }

        boatReadyTweens[boatIndex].Kill(false);
        boatReadyTweens[boatIndex] = null;

        if (restoreTweenPosition &&
            boatStartPositions != null &&
            boatIndex < boatStartPositions.Length &&
            boat != null)
        {
            boat.position = boatStartPositions[boatIndex];
        }
    }

    private void RestoreBoatStartPositions()
    {
        if (boatStartPositions == null)
        {
            return;
        }

        int boatCount = Mathf.Min(managedBoats.Count, boatStartPositions.Length);
        for (int i = 0; i < boatCount; i++)
        {
            RectTransform boat = managedBoats[i];
            if (boat != null)
            {
                boat.position = boatStartPositions[i];
            }
        }
    }

    private int GetManagedBoatIndex(RectTransform boat)
    {
        if (boat == null)
        {
            return -1;
        }

        for (int i = 0; i < managedBoats.Count; i++)
        {
            if (managedBoats[i] == boat)
            {
                return i;
            }
        }

        return -1;
    }

    private void ResetToolWithShake(MissionTwoStep step)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
        }

        shakeRoutine = StartCoroutine(ShakeTool(step));
    }

    private IEnumerator ShakeTool(MissionTwoStep step)
    {
        if (step.ToolTransform == null)
        {
            yield break;
        }

        const float duration = 0.25f;
        const float frequency = 48f;
        const float amplitude = 12f;
        float elapsed = 0f;

        step.ToolTransform.gameObject.SetActive(true);
        step.ToolTransform.SetSiblingIndex(step.InitialSiblingIndex);
        step.ToolTransform.localScale = step.InitialToolScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * frequency) * amplitude;
            step.ToolTransform.anchoredPosition = step.InitialToolPosition + new Vector2(offset, 0f);
            yield return null;
        }

        step.ToolTransform.anchoredPosition = step.InitialToolPosition;
        shakeRoutine = null;
    }

    private void ResetActiveDrag()
    {
        if (draggedStep == null)
        {
            return;
        }

        ResetTool(draggedStep, true);
        draggedStep = null;
    }

    private static void ResetTool(MissionTwoStep step, bool visible)
    {
        if (step.ToolTransform == null)
        {
            return;
        }

        step.ToolTransform.anchoredPosition = step.InitialToolPosition;
        step.ToolTransform.localScale = step.InitialToolScale;
        step.ToolTransform.SetSiblingIndex(step.InitialSiblingIndex);
        step.ToolTransform.gameObject.SetActive(visible);
    }

    private static void HideEffect(MissionTwoStep step)
    {
        if (step.EffectObject != null)
        {
            step.EffectObject.transform.localScale = step.InitialEffectScale;
            step.EffectObject.SetActive(false);
        }

        if (step.EffectImage != null)
        {
            step.EffectImage.color = step.InitialEffectColor;
        }
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private static void SetToolRaycast(MissionTwoStep step, bool raycastTarget)
    {
        if (step.ToolImage != null)
        {
            step.ToolImage.raycastTarget = raycastTarget;
        }
    }

    private static bool TryGetPointerWorldPosition(MissionTwoStep step, Vector2 screenPosition, out Vector3 pointerWorldPosition)
    {
        pointerWorldPosition = Vector3.zero;
        if (step == null || step.ToolTransform == null)
        {
            return false;
        }

        RectTransform parentRect = step.ToolTransform.parent as RectTransform;
        return parentRect != null &&
               RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRect, screenPosition, null, out pointerWorldPosition);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private sealed class MissionTwoStep
    {
        public readonly RectTransform ToolTransform;
        public readonly Image ToolImage;
        public readonly GameObject EffectObject;
        public readonly Image EffectImage;

        public Vector2 InitialToolPosition;
        public Vector3 InitialToolScale;
        public int InitialSiblingIndex;
        public Vector3 InitialEffectScale;
        public Color InitialEffectColor;

        public MissionTwoStep(RectTransform toolTransform, Image toolImage, GameObject effectObject, Image effectImage)
        {
            ToolTransform = toolTransform;
            ToolImage = toolImage;
            EffectObject = effectObject;
            EffectImage = effectImage;
        }

        public void CacheInitialState()
        {
            if (ToolTransform != null)
            {
                InitialToolPosition = ToolTransform.anchoredPosition;
                InitialToolScale = ToolTransform.localScale;
                InitialSiblingIndex = ToolTransform.GetSiblingIndex();
            }

            InitialEffectScale = EffectObject != null ? EffectObject.transform.localScale : Vector3.one;
            InitialEffectColor = EffectImage != null ? EffectImage.color : Color.white;
        }
    }

    [Serializable]
    private sealed class BoatMovement
    {
        [SerializeField] private RectTransform boat;
        [SerializeField] private RectTransform target;
        [SerializeField] private GameObject jet;

        public RectTransform Boat => boat;
        public RectTransform Target => target;
        public GameObject Jet => jet;
    }

    [Serializable]
    private sealed class DockPlacement
    {
        [SerializeField] private GameObject dock;
        [SerializeField] private List<BoatMovement> boatMovements = new List<BoatMovement>();

        public GameObject Dock => dock;
        public List<BoatMovement> BoatMovements => boatMovements;
    }
}

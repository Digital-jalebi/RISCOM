using System;
using System.Collections;
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
    private const float ReportDelaySeconds = 2f;

    [SerializeField] private GameObject alertScreen;
    [SerializeField] private Button alertNextButton;
    [SerializeField] private GameObject playRoot;
    [SerializeField] private GameObject completeScreen;
    [SerializeField] private Button reportNextButton;
    [SerializeField] private RectTransform dropArea;
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
    [SerializeField] private RectTransform[] boats;
    [SerializeField] private RectTransform[] boatTargets;

    private readonly MissionTwoStep[] steps = new MissionTwoStep[StepCount];

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
    private Coroutine shakeRoutine;
    private Coroutine effectRoutine;
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
        bool isOnDropArea = dropArea != null && RectTransformUtility.RectangleContainsScreenPoint(dropArea, screenPosition, null);

        if (isExpectedStep && isOnDropArea)
        {
            AcceptTool(droppedStep);
            return;
        }

        ResetToolWithShake(droppedStep);
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
        stepIndex++;
        ResetTool(step, false);

        if (stepIndex >= steps.Length)
        {
            isRunning = false;
        }

        if (effectRoutine != null)
        {
            StopCoroutine(effectRoutine);
        }

        effectRoutine = StartCoroutine(PlayEffect(step));
    }

    private IEnumerator PlayEffect(MissionTwoStep step)
    {
        bool moveBoats = IsBoatDockingStep(step);
        float duration = moveBoats ? Mathf.Max(EffectAnimationDuration, BoatMoveDuration) : EffectAnimationDuration;

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

            if (moveBoats)
            {
                MoveBoats(Mathf.Clamp01(elapsed / BoatMoveDuration));
            }

            yield return null;
        }

        if (moveBoats)
        {
            MoveBoats(1f);
        }

        HideEffect(step);
        effectRoutine = null;

        if (stepIndex >= steps.Length)
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

    private void CacheBoatStartPositions()
    {
        int boatCount = boats != null ? boats.Length : 0;
        boatStartPositions = new Vector3[boatCount];

        for (int i = 0; i < boatCount; i++)
        {
            if (boats[i] != null)
            {
                boatStartPositions[i] = boats[i].position;
            }
        }
    }

    private void ResetBoats()
    {
        if (boats == null || boatStartPositions == null)
        {
            return;
        }

        int boatCount = Mathf.Min(boats.Length, boatStartPositions.Length);
        for (int i = 0; i < boatCount; i++)
        {
            if (boats[i] != null)
            {
                boats[i].position = boatStartPositions[i];
            }
        }
    }

    private void MoveBoats(float progress)
    {
        if (boats == null || boatTargets == null || boatStartPositions == null)
        {
            return;
        }

        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        int boatCount = Mathf.Min(Mathf.Min(boats.Length, boatTargets.Length), boatStartPositions.Length);
        for (int i = 0; i < boatCount; i++)
        {
            RectTransform boat = boats[i];
            RectTransform target = boatTargets[i];
            if (boat != null && target != null)
            {
                boat.position = Vector3.Lerp(boatStartPositions[i], target.position, easedProgress);
            }
        }
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
}

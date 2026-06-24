using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class IndustrialMissionTwoController : MonoBehaviour
{
    private const float DefaultMissionDurationSeconds = 600f;
    private const float WrongDropShakeDuration = 0.25f;
    private const float WrongDropShakeFrequency = 48f;
    private const float WrongDropShakeAmplitude = 12f;
    private const float DisabledToolAlphaMultiplier = 0.5f;

    [SerializeField] private GameObject missionGame;
    [SerializeField] private GameObject alertScreen;
    [SerializeField] private Button alertNextButton;
    [SerializeField] private GameObject playRoot;
    [SerializeField] private GameObject completeScreen;
    [SerializeField] private Button reportNextButton;
    [SerializeField] private RectTransform mapDropTarget;
    [SerializeField] private GameObject smokesRoot;
    [SerializeField] private GameObject ventilatorFanObject;
    [SerializeField] private GameObject barrierObject;
    [SerializeField] private GameObject exitGateObject;
    [SerializeField] private Slider timeRemainingSlider;
    [SerializeField] private TextMeshProUGUI timeRemainingLabel;
    [SerializeField] private float missionDurationSeconds = DefaultMissionDurationSeconds;
    [SerializeField] private float completionDelaySeconds = 1f;
    [SerializeField] private IndustrialMissionTwoTool ppeKitsTool;
    [SerializeField] private IndustrialMissionTwoTool ventilatorFanTool;
    [SerializeField] private IndustrialMissionTwoTool barrierTool;
    [SerializeField] private IndustrialMissionTwoTool exitGateTool;
    [SerializeField] private List<WorkerTarget> workerTargets = new List<WorkerTarget>();

    private System.Action onReportNext;
    private MissionPhase phase;
    private IndustrialMissionTwoTool draggedTool;
    private float timeRemaining;
    private int protectedWorkers;
    private bool configured;
    private bool buttonsWired;
    private bool isRunning;
    private bool isComplete;
    private Coroutine shakeRoutine;
    private Coroutine completionRoutine;

    public void Configure(System.Action reportNextHandler)
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

        ppeKitsTool?.CacheInitialState();
        ventilatorFanTool?.CacheInitialState();
        barrierTool?.CacheInitialState();
        exitGateTool?.CacheInitialState();
        CacheWorkerTargets();
        ConfigureTimerSlider();

        configured = true;
    }

    public void ShowIntro()
    {
        Configure(onReportNext);
        StopMission();

        SetActive(missionGame, true);
        SetActive(alertScreen, true);
        SetActive(playRoot, false);
        SetActive(completeScreen, false);
    }

    public void Hide()
    {
        StopMission();
        SetActive(missionGame, false);
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

    private void StartMissionGame()
    {
        SetActive(alertScreen, false);
        SetActive(playRoot, true);
        SetActive(completeScreen, false);
        BeginMission();
    }

    private void HandleReportNext()
    {
        onReportNext?.Invoke();
    }

    private void BeginMission()
    {
        StopActiveDrag();
        StopMissionRoutines();

        phase = MissionPhase.PpeKits;
        timeRemaining = missionDurationSeconds;
        protectedWorkers = 0;
        isRunning = true;
        isComplete = false;

        ResetTools();
        ResetWorkerTargets();
        SetActive(smokesRoot, true);
        SetActive(ventilatorFanObject, false);
        SetActive(barrierObject, false);
        SetActive(exitGateObject, false);
        SetToolUsable(ppeKitsTool, true);
        SetToolUsable(ventilatorFanTool, false);
        SetToolUsable(barrierTool, false);
        SetToolUsable(exitGateTool, false);
        UpdateTimerDisplay();

        if (GetWorkerTargetCount() == 0)
        {
            EnterVentilatorPhase();
        }
    }

    private void StopMission()
    {
        StopActiveDrag();
        StopMissionRoutines();
        isRunning = false;
        isComplete = false;

        if (!configured)
        {
            return;
        }

        ResetTools();
        ResetWorkerTargets();
        SetActive(smokesRoot, true);
        SetActive(ventilatorFanObject, false);
        SetActive(barrierObject, false);
        SetActive(exitGateObject, false);
    }

    private void UpdateTimer()
    {
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerDisplay();

        if (timeRemaining <= 0f)
        {
            isRunning = false;
            StopActiveDrag();
            Debug.LogWarning("Industrial Mission 2 timer expired.");
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

        IndustrialMissionTwoTool tool = GetActiveTool();
        if (tool == null || !tool.Contains(screenPosition))
        {
            return;
        }

        draggedTool = tool;
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
        if (TryGetPointerWorldPosition(draggedTool.ToolTransform, screenPosition, out pointerWorldPosition))
        {
            draggedTool.ToolTransform.position = pointerWorldPosition;
        }
    }

    private void EndDrag()
    {
        if (draggedTool == null)
        {
            return;
        }

        IndustrialMissionTwoTool droppedTool = draggedTool;
        RectTransform dropTransform = droppedTool.ToolTransform;
        draggedTool = null;

        bool accepted = TryApplyTool(droppedTool, dropTransform);

        if (!accepted)
        {
            ResetToolWithShake(droppedTool);
        }
    }

    private bool TryApplyTool(IndustrialMissionTwoTool tool, RectTransform dropTransform)
    {
        if (phase == MissionPhase.PpeKits && tool == ppeKitsTool)
        {
            return TryPlacePpe(dropTransform);
        }

        if (phase == MissionPhase.VentilatorFan && tool == ventilatorFanTool)
        {
            return TryPlaceVentilatorFan(dropTransform);
        }

        if (phase == MissionPhase.Barrier && tool == barrierTool)
        {
            return TryPlaceBarrier(dropTransform);
        }

        if (phase == MissionPhase.ExitGate && tool == exitGateTool)
        {
            return TryPlaceExitGate(dropTransform);
        }

        return false;
    }

    private bool TryPlacePpe(RectTransform dropTransform)
    {
        WorkerTarget target = FindWorkerTarget(dropTransform);
        if (target == null)
        {
            return false;
        }

        target.MarkProtected();
        protectedWorkers++;
        ResetTool(ppeKitsTool, HasPendingWorkers());

        if (!HasPendingWorkers())
        {
            EnterVentilatorPhase();
        }

        return true;
    }

    private bool TryPlaceVentilatorFan(RectTransform dropTransform)
    {
        if (!CanPlaceOnMap(dropTransform))
        {
            return false;
        }

        SetActive(ventilatorFanObject, true);
        SetActive(smokesRoot, false);
        ResetTool(ventilatorFanTool, false);
        phase = MissionPhase.Barrier;
        SetToolUsable(barrierTool, true);
        return true;
    }

    private bool TryPlaceBarrier(RectTransform dropTransform)
    {
        if (!CanPlaceOnMap(dropTransform))
        {
            return false;
        }

        SetActive(barrierObject, true);
        ResetTool(barrierTool, false);
        phase = MissionPhase.ExitGate;
        SetToolUsable(exitGateTool, true);
        return true;
    }

    private bool TryPlaceExitGate(RectTransform dropTransform)
    {
        if (!CanPlaceOnMap(dropTransform))
        {
            return false;
        }

        SetActive(exitGateObject, true);
        ResetTool(exitGateTool, false);
        DeactivateWorkers();
        CompleteMissionAfterDelay();
        return true;
    }

    private WorkerTarget FindWorkerTarget(RectTransform dropTransform)
    {
        for (int i = 0; i < workerTargets.Count; i++)
        {
            WorkerTarget target = workerTargets[i];
            if (target != null && target.CanAccept(dropTransform))
            {
                return target;
            }
        }

        return null;
    }

    private bool CanPlaceOnMap(RectTransform dropTransform)
    {
        return mapDropTarget == null || RectTransformsOverlap(dropTransform, mapDropTarget);
    }

    private void EnterVentilatorPhase()
    {
        phase = MissionPhase.VentilatorFan;
        SetToolUsable(ppeKitsTool, false);
        SetToolUsable(ventilatorFanTool, true);
        SetToolUsable(barrierTool, false);
        SetToolUsable(exitGateTool, false);
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

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
        }

        completionRoutine = StartCoroutine(ShowCompletionAfterDelay());
    }

    private IEnumerator ShowCompletionAfterDelay()
    {
        yield return new WaitForSeconds(completionDelaySeconds);

        ShowCompletion();
    }

    private void ShowCompletion()
    {
        SetActive(playRoot, false);
        SetActive(completeScreen, true);
        completionRoutine = null;
    }

    private IndustrialMissionTwoTool GetActiveTool()
    {
        if (phase == MissionPhase.PpeKits)
        {
            return ppeKitsTool;
        }

        if (phase == MissionPhase.VentilatorFan)
        {
            return ventilatorFanTool;
        }

        if (phase == MissionPhase.Barrier)
        {
            return barrierTool;
        }

        if (phase == MissionPhase.ExitGate)
        {
            return exitGateTool;
        }

        return null;
    }

    private void StopActiveDrag()
    {
        draggedTool?.ResetPosition();
        draggedTool = null;
    }

    private void ResetTool(IndustrialMissionTwoTool tool, bool usable)
    {
        if (tool == null)
        {
            return;
        }

        tool.ResetPosition();
        tool.SetUsable(usable);
    }

    private void ResetToolWithShake(IndustrialMissionTwoTool tool)
    {
        if (tool == null)
        {
            return;
        }

        tool.ResetPosition();

        StopShake();
        shakeRoutine = StartCoroutine(ShakeTool(tool));
    }

    private IEnumerator ShakeTool(IndustrialMissionTwoTool tool)
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
    }

    private void StopShake()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }
    }

    private bool TryGetPointerWorldPosition(RectTransform targetTransform, Vector2 screenPosition, out Vector3 worldPosition)
    {
        RectTransform parent = targetTransform != null
            ? targetTransform.parent as RectTransform
            : null;

        if (parent != null &&
            RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screenPosition, null, out worldPosition))
        {
            return true;
        }

        worldPosition = Vector3.zero;
        return false;
    }

    private void CacheWorkerTargets()
    {
        for (int i = 0; i < workerTargets.Count; i++)
        {
            workerTargets[i]?.CacheInitialState();
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
        ppeKitsTool?.ResetPosition();
        ventilatorFanTool?.ResetPosition();
        barrierTool?.ResetPosition();
        exitGateTool?.ResetPosition();
    }

    private void ResetWorkerTargets()
    {
        for (int i = 0; i < workerTargets.Count; i++)
        {
            workerTargets[i]?.ResetTarget();
        }
    }

    private void DeactivateWorkers()
    {
        for (int i = 0; i < workerTargets.Count; i++)
        {
            workerTargets[i]?.DeactivateWorker();
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

    private bool HasPendingWorkers()
    {
        return protectedWorkers < GetWorkerTargetCount();
    }

    private int GetWorkerTargetCount()
    {
        int count = 0;
        for (int i = 0; i < workerTargets.Count; i++)
        {
            if (workerTargets[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private static void SetToolUsable(IndustrialMissionTwoTool tool, bool usable)
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
        PpeKits,
        VentilatorFan,
        Barrier,
        ExitGate
    }

    [System.Serializable]
    private sealed class IndustrialMissionTwoTool
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
    private sealed class WorkerTarget
    {
        [SerializeField] private string workerName;
        [SerializeField] private RectTransform workerTransform;
        [SerializeField] private Image workerImage;
        [SerializeField] private Sprite protectedSprite;

        private Sprite initialSprite;
        private Color initialColor;
        private bool isProtected;

        public void CacheInitialState()
        {
            if (workerImage != null)
            {
                initialSprite = workerImage.sprite;
                initialColor = workerImage.color;
                workerImage.raycastTarget = true;
            }
        }

        public bool CanAccept(RectTransform toolTransform)
        {
            return !isProtected &&
                   RectTransformsOverlap(toolTransform, workerTransform);
        }

        public void MarkProtected()
        {
            isProtected = true;

            if (workerImage != null && protectedSprite != null)
            {
                workerImage.sprite = protectedSprite;
                workerImage.preserveAspect = true;
            }
        }

        public void ResetTarget()
        {
            isProtected = false;

            if (workerTransform != null)
            {
                workerTransform.gameObject.SetActive(true);
            }

            if (workerImage != null)
            {
                workerImage.sprite = initialSprite;
                workerImage.color = initialColor;
            }
        }

        public void DeactivateWorker()
        {
            if (workerTransform != null)
            {
                workerTransform.gameObject.SetActive(false);
            }
        }
    }
}

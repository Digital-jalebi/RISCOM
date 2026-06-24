using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class IndustrialMissionFourController : MonoBehaviour
{
    private const int ToolPhaseCount = 3;
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
    [SerializeField] private Image areaImage;
    [SerializeField] private Slider timeRemainingSlider;
    [SerializeField] private TextMeshProUGUI timeRemainingLabel;
    [SerializeField] private float missionDurationSeconds = DefaultMissionDurationSeconds;
    [SerializeField] private float completionDelaySeconds = 2f;
    [SerializeField] private IndustrialMissionFourTool healthTool;
    [SerializeField] private IndustrialMissionFourTool cleanUpTool;
    [SerializeField] private IndustrialMissionFourTool recoveryTool;

    private readonly IndustrialMissionFourTool[] tools = new IndustrialMissionFourTool[ToolPhaseCount];

    private System.Action onReportNext;
    private IndustrialMissionFourTool draggedTool;
    private Vector3 dragWorldOffset;
    private Sprite initialAreaSprite;
    private Color initialAreaColor;
    private bool initialAreaPreserveAspect;
    private int phaseIndex;
    private float timeRemaining;
    private bool configured;
    private bool buttonsWired;
    private bool isRunning;
    private bool isComplete;
    private bool inputLocked;
    private Coroutine shakeRoutine;
    private Coroutine completionRoutine;

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

        if (configured)
        {
            return;
        }

        tools[0] = healthTool;
        tools[1] = cleanUpTool;
        tools[2] = recoveryTool;

        for (int i = 0; i < tools.Length; i++)
        {
            tools[i]?.CacheInitialState();
        }

        CacheAreaInitialState();
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
        SetActive(playRoot, true);
        SetActive(completeScreen, false);
        BeginMission();
    }

    private void BeginMission()
    {
        Configure(onReportNext);
        StopActiveDrag();
        StopMissionRoutines();

        phaseIndex = 0;
        timeRemaining = missionDurationSeconds;
        isRunning = true;
        isComplete = false;
        inputLocked = false;

        ResetArea();
        ResetTools();
        SetActiveToolPhase(phaseIndex);
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

        ResetArea();
        ResetTools();
    }

    private void UpdateTimer()
    {
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerDisplay();

        if (timeRemaining <= 0f)
        {
            StopMission();
            Debug.LogWarning("Industrial Mission 4 timer expired.");
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

        IndustrialMissionFourTool selectedTool = GetActiveTool();
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

        IndustrialMissionFourTool droppedTool = draggedTool;
        draggedTool = null;

        if (TryApplyTool(droppedTool))
        {
            return;
        }

        ResetToolWithShake(droppedTool);
    }

    private bool TryApplyTool(IndustrialMissionFourTool tool)
    {
        if (tool == null || tool != GetActiveTool() || !CanPlaceOnArea(tool.ToolTransform))
        {
            return false;
        }

        ApplyAreaSprite(tool);
        ResetTool(tool, false);
        AdvancePhaseOrComplete();
        return true;
    }

    private void ApplyAreaSprite(IndustrialMissionFourTool tool)
    {
        if (areaImage == null || tool == null)
        {
            return;
        }

        Sprite replacementSprite = tool.AreaSprite != null ? tool.AreaSprite : tool.ToolSprite;
        if (replacementSprite != null)
        {
            areaImage.sprite = replacementSprite;
        }

        areaImage.color = initialAreaColor;
        areaImage.preserveAspect = initialAreaPreserveAspect;
    }

    private void AdvancePhaseOrComplete()
    {
        if (phaseIndex >= ToolPhaseCount - 1)
        {
            CompleteMissionAfterDelay();
            return;
        }

        phaseIndex++;
        SetActiveToolPhase(phaseIndex);
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
        SetActiveToolPhase(-1);

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

    private IndustrialMissionFourTool GetActiveTool()
    {
        return phaseIndex >= 0 && phaseIndex < tools.Length ? tools[phaseIndex] : null;
    }

    private bool CanPlaceOnArea(RectTransform toolTransform)
    {
        return areaDropTarget == null || RectTransformsOverlap(toolTransform, areaDropTarget);
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
        for (int i = 0; i < tools.Length; i++)
        {
            ResetTool(tools[i], i == 0);
        }
    }

    private void ResetTool(IndustrialMissionFourTool tool, bool usable)
    {
        if (tool == null)
        {
            return;
        }

        tool.ResetPosition();
        tool.SetUsable(usable);
    }

    private void ResetToolWithShake(IndustrialMissionFourTool tool)
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

    private IEnumerator ShakeTool(IndustrialMissionFourTool tool)
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

    private void SetActiveToolPhase(int activePhaseIndex)
    {
        for (int i = 0; i < tools.Length; i++)
        {
            SetToolUsable(tools[i], i == activePhaseIndex);
        }
    }

    private void CacheAreaInitialState()
    {
        if (areaImage == null)
        {
            return;
        }

        initialAreaSprite = areaImage.sprite;
        initialAreaColor = areaImage.color;
        initialAreaPreserveAspect = areaImage.preserveAspect;
    }

    private void ResetArea()
    {
        if (areaImage == null)
        {
            return;
        }

        areaImage.sprite = initialAreaSprite;
        areaImage.color = initialAreaColor;
        areaImage.preserveAspect = initialAreaPreserveAspect;
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

    private static void SetToolUsable(IndustrialMissionFourTool tool, bool usable)
    {
        tool?.SetUsable(usable);
    }

    private static bool TryGetPointerWorldPosition(
        IndustrialMissionFourTool tool,
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

    [System.Serializable]
    private sealed class IndustrialMissionFourTool
    {
        [SerializeField] private RectTransform toolTransform;
        [SerializeField] private Image toolImage;
        [SerializeField] private Sprite dragSprite;
        [SerializeField] private Sprite areaSprite;
        [SerializeField, Range(0.1f, 2f)] private float dragSizeMultiplier = 1f;

        private Vector2 initialPosition;
        private Vector2 initialSizeDelta;
        private Vector3 initialScale;
        private Color initialColor;
        private Sprite initialSprite;
        private bool initialPreserveAspect;
        private int initialSiblingIndex;

        public RectTransform ToolTransform => toolTransform;
        public Sprite ToolSprite => toolImage != null ? toolImage.sprite : null;
        public Sprite AreaSprite => areaSprite;
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
}

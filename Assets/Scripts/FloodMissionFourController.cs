using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class FloodMissionFourController : MonoBehaviour
{
    private const float DefaultMissionDurationSeconds = 600f;
    private const float DisabledToolAlphaMultiplier = 0.5f;
    private const float WrongDropShakeDuration = 0.25f;
    private const float WrongDropShakeFrequency = 48f;
    private const float WrongDropShakeAmplitude = 12f;
    private const int ToolPhaseCount = 3;

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
    [SerializeField] private FloodTool reliefCampTool;
    [SerializeField] private FloodTool reliefKitsTool;
    [SerializeField] private FloodTool medicalTeamsTool;
    [SerializeField] private List<BuildingTarget> buildingTargets = new List<BuildingTarget>();

    private readonly FloodTool[] tools = new FloodTool[ToolPhaseCount];

    private Action onReportNext;
    private int phaseIndex;
    private int placedInCurrentPhase;
    private FloodTool draggedTool;
    private Vector3 dragWorldOffset;
    private float timeRemaining;
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

        tools[0] = reliefCampTool;
        tools[1] = reliefKitsTool;
        tools[2] = medicalTeamsTool;

        for (int i = 0; i < tools.Length; i++)
        {
            tools[i]?.CacheInitialState();
        }

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

        phaseIndex = 0;
        placedInCurrentPhase = 0;
        draggedTool = null;
        timeRemaining = missionDurationSeconds;
        isRunning = true;
        isComplete = false;
        inputLocked = false;

        ResetTools();
        ResetBuildingTargets();
        SetActiveToolPhase(phaseIndex);
        UpdateTimerDisplay();

        if (GetBuildingTargetCount() == 0)
        {
            CompleteMissionAfterDelay();
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
        ResetBuildingTargets();
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

    private void UpdateTimer()
    {
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerDisplay();

        if (timeRemaining <= 0f)
        {
            StopMission();
            Debug.LogWarning("Flood Mission 4 timer expired.");
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

        BuildingTarget target = FindBuildingTarget(droppedTool.ToolTransform);
        if (target != null)
        {
            target.ApplyTool(phaseIndex, IsGujaratiEnabled(), droppedTool.ToolSprite);
            placedInCurrentPhase++;

            bool phaseComplete = placedInCurrentPhase >= GetBuildingTargetCount();
            ResetTool(droppedTool, !phaseComplete);

            if (phaseComplete)
            {
                AdvancePhaseOrComplete();
            }

            return;
        }

        ResetToolWithShake(droppedTool);
    }

    private BuildingTarget FindBuildingTarget(RectTransform toolTransform)
    {
        for (int i = 0; i < buildingTargets.Count; i++)
        {
            BuildingTarget target = buildingTargets[i];
            if (target != null && target.CanAcceptPhase(phaseIndex, toolTransform))
            {
                return target;
            }
        }

        return null;
    }

    private void AdvancePhaseOrComplete()
    {
        if (phaseIndex >= ToolPhaseCount - 1)
        {
            CompleteMissionAfterDelay();
            return;
        }

        phaseIndex++;
        placedInCurrentPhase = 0;
        SetActiveToolPhase(phaseIndex);
    }

    private void CompleteMissionAfterDelay()
    {
        isRunning = false;
        isComplete = true;
        inputLocked = true;
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

    private FloodTool GetActiveTool()
    {
        return phaseIndex >= 0 && phaseIndex < tools.Length ? tools[phaseIndex] : null;
    }

    private void SetActiveToolPhase(int activePhaseIndex)
    {
        for (int i = 0; i < tools.Length; i++)
        {
            SetToolUsable(tools[i], i == activePhaseIndex);
        }
    }

    private void ResetTools()
    {
        for (int i = 0; i < tools.Length; i++)
        {
            ResetTool(tools[i], i == 0);
        }
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

    private void CacheBuildingTargets()
    {
        for (int i = 0; i < buildingTargets.Count; i++)
        {
            buildingTargets[i]?.CacheInitialState();
        }
    }

    private void ResetBuildingTargets()
    {
        for (int i = 0; i < buildingTargets.Count; i++)
        {
            buildingTargets[i]?.ResetTarget();
        }
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
        RefreshAppliedToolSprites(useGujarati);
    }

    private bool IsGujaratiEnabled()
    {
        return languageToggleController != null && languageToggleController.IsGujaratiEnabled;
    }

    private void RefreshAppliedToolSprites(bool useGujarati)
    {
        for (int i = 0; i < buildingTargets.Count; i++)
        {
            BuildingTarget target = buildingTargets[i];
            if (target != null)
            {
                target.RefreshAppliedSprite(useGujarati, GetFallbackSprite(target.CompletedPhase));
            }
        }
    }

    private Sprite GetFallbackSprite(int targetPhaseIndex)
    {
        return targetPhaseIndex >= 0 && targetPhaseIndex < tools.Length && tools[targetPhaseIndex] != null
            ? tools[targetPhaseIndex].ToolSprite
            : null;
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
    private sealed class BuildingTarget
    {
        [SerializeField] private string targetName;
        [SerializeField] private RectTransform dropTarget;
        [SerializeField] private GameObject appliedToolObject;
        [SerializeField] private Image appliedToolImage;
        [SerializeField, FormerlySerializedAs("reliefCampSprite")] private Sprite englishReliefCampSprite;
        [SerializeField] private Sprite gujaratiReliefCampSprite;
        [SerializeField, FormerlySerializedAs("reliefKitsSprite")] private Sprite englishReliefKitsSprite;
        [SerializeField] private Sprite gujaratiReliefKitsSprite;
        [SerializeField, FormerlySerializedAs("medicalTeamsSprite")] private Sprite englishMedicalTeamsSprite;
        [SerializeField] private Sprite gujaratiMedicalTeamsSprite;

        private Sprite initialSprite;
        private Color initialColor;
        private bool initialPreserveAspect;
        private int completedPhase = -1;

        public int CompletedPhase => completedPhase;

        public void CacheInitialState()
        {
            if (appliedToolImage != null)
            {
                initialSprite = appliedToolImage.sprite;
                initialColor = appliedToolImage.color;
                initialPreserveAspect = appliedToolImage.preserveAspect;
            }

            SetActive(appliedToolObject, false);
        }

        public bool CanAcceptPhase(int phaseIndex, RectTransform toolTransform)
        {
            return completedPhase == phaseIndex - 1 &&
                   RectTransformsOverlap(toolTransform, dropTarget);
        }

        public void ApplyTool(int phaseIndex, bool useGujarati, Sprite fallbackSprite)
        {
            completedPhase = phaseIndex;
            SetActive(appliedToolObject, true);
            ApplySprite(phaseIndex, useGujarati, fallbackSprite);
        }

        public void RefreshAppliedSprite(bool useGujarati, Sprite fallbackSprite)
        {
            if (completedPhase >= 0)
            {
                ApplySprite(completedPhase, useGujarati, fallbackSprite);
            }
        }

        private void ApplySprite(int phaseIndex, bool useGujarati, Sprite fallbackSprite)
        {
            if (appliedToolImage == null)
            {
                return;
            }

            Sprite replacementSprite = GetReplacementSprite(phaseIndex, useGujarati, fallbackSprite);
            if (replacementSprite != null)
            {
                appliedToolImage.sprite = replacementSprite;
            }

            appliedToolImage.color = initialColor;
            appliedToolImage.preserveAspect = true;
        }

        public void ResetTarget()
        {
            completedPhase = -1;

            if (appliedToolImage != null)
            {
                appliedToolImage.sprite = initialSprite;
                appliedToolImage.color = initialColor;
                appliedToolImage.preserveAspect = initialPreserveAspect;
            }

            SetActive(appliedToolObject, false);
        }

        private Sprite GetReplacementSprite(int phaseIndex, bool useGujarati, Sprite fallbackSprite)
        {
            if (phaseIndex == 0)
            {
                return GetLocalizedSprite(
                    useGujarati,
                    englishReliefCampSprite,
                    gujaratiReliefCampSprite,
                    initialSprite != null ? initialSprite : fallbackSprite);
            }

            if (phaseIndex == 1)
            {
                return GetLocalizedSprite(useGujarati, englishReliefKitsSprite, gujaratiReliefKitsSprite, fallbackSprite);
            }

            return GetLocalizedSprite(useGujarati, englishMedicalTeamsSprite, gujaratiMedicalTeamsSprite, fallbackSprite);
        }

        private static Sprite GetLocalizedSprite(
            bool useGujarati,
            Sprite englishSprite,
            Sprite gujaratiSprite,
            Sprite fallbackSprite)
        {
            if (useGujarati && gujaratiSprite != null)
            {
                return gujaratiSprite;
            }

            return englishSprite != null ? englishSprite : fallbackSprite;
        }
    }
}

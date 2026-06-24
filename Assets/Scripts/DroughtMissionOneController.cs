using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class DroughtMissionOneController : MonoBehaviour
{
    private const float DefaultMissionDurationSeconds = 600f;
    private const float DisabledToolAlphaMultiplier = 0.5f;
    private const float WrongDropShakeDuration = 0.25f;
    private const float WrongDropShakeFrequency = 48f;
    private const float WrongDropShakeAmplitude = 12f;

    [SerializeField] protected string missionLabel = "Drought Mission";
    [SerializeField] protected GameObject introScreen;
    [SerializeField] protected Button introNextButton;
    [SerializeField] protected GameObject gameplayRoot;
    [SerializeField] protected GameObject scoreReportScreen;
    [SerializeField] protected Button scoreReportNextButton;
    [SerializeField] protected GameObject completeScreen;
    [SerializeField] protected Button completeNextButton;
    [SerializeField] protected Slider missionProgressSlider;
    [SerializeField] protected Slider timeRemainingSlider;
    [SerializeField] protected TextMeshProUGUI timeRemainingLabel;
    [SerializeField] protected RISCOMLanguageToggleController languageToggleController;
    [SerializeField] protected float missionDurationSeconds = DefaultMissionDurationSeconds;
    [SerializeField] protected List<DroughtTool> tools = new List<DroughtTool>();
    [SerializeField] protected List<SettlementTarget> highRiskSettlements = new List<SettlementTarget>();
    [SerializeField] protected List<SettlementTarget> lowRiskSettlements = new List<SettlementTarget>();

    private Action onCompleteNext;
    private DroughtTool draggedTool;
    private Vector3 dragWorldOffset;
    private int phaseIndex;
    private int completedPlacements;
    private float timeRemaining;
    private bool configured;
    private bool buttonsWired;
    private bool languageControllerWired;
    private bool isRunning;
    private bool isComplete;
    private bool inputLocked;
    private Coroutine shakeRoutine;
    private readonly Dictionary<SettlementTarget, Coroutine> fillRoutines = new Dictionary<SettlementTarget, Coroutine>();

    public void Configure(Action completeNextHandler = null)
    {
        if (completeNextHandler != null)
        {
            onCompleteNext = completeNextHandler;
        }

        if (!buttonsWired)
        {
            if (introNextButton != null)
            {
                introNextButton.onClick.AddListener(StartMissionGame);
            }

            if (scoreReportNextButton != null)
            {
                scoreReportNextButton.onClick.AddListener(ShowCompleteScreen);
            }

            if (completeNextButton != null)
            {
                completeNextButton.onClick.AddListener(HandleCompleteNext);
            }

            buttonsWired = true;
        }

        WireLanguageController();

        if (configured)
        {
            return;
        }

        CacheTools();
        CacheTargets(highRiskSettlements);
        CacheTargets(lowRiskSettlements);
        ConfigureSliders();

        configured = true;
    }

    public void ShowIntro()
    {
        Configure(onCompleteNext);
        StopMission();

        gameObject.SetActive(true);
        SetActive(introScreen, true);
        SetActive(gameplayRoot, false);
        SetActive(scoreReportScreen, false);
        SetActive(completeScreen, false);
    }

    public void Hide()
    {
        StopMission();
        gameObject.SetActive(false);
    }

    public void BeginMission()
    {
        Configure(onCompleteNext);
        StopMissionRoutines();

        phaseIndex = 0;
        completedPlacements = 0;
        draggedTool = null;
        timeRemaining = missionDurationSeconds;
        isRunning = true;
        isComplete = false;
        inputLocked = false;

        SetActive(introScreen, false);
        SetActive(gameplayRoot, true);
        SetActive(scoreReportScreen, false);
        SetActive(completeScreen, false);
        ResetTools();
        ResetTargets(highRiskSettlements);
        ResetTargets(lowRiskSettlements);
        SetActiveToolPhase(phaseIndex);
        UpdateProgressDisplay();
        UpdateTimerDisplay();

        if (GetToolCount() == 0 || GetTotalTargetCount() == 0)
        {
            CompleteMission();
        }
    }

    private void StartMissionGame()
    {
        SetActive(introScreen, false);
        BeginMission();
    }

    private void StopMission()
    {
        if (draggedTool != null)
        {
            ResetTool(draggedTool, true);
            draggedTool = null;
        }

        StopMissionRoutines();

        isRunning = false;
        isComplete = false;
        inputLocked = false;

        if (!configured)
        {
            return;
        }

        ResetTools();
        ResetTargets(highRiskSettlements);
        ResetTargets(lowRiskSettlements);
    }

    private void OnDisable()
    {
        StopMissionRoutines();
        UnwireLanguageController();
        isRunning = false;
        isComplete = false;
        inputLocked = false;
        draggedTool = null;
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

    private void UpdateTimer()
    {
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerDisplay();

        if (timeRemaining <= 0f)
        {
            isRunning = false;
            inputLocked = true;
            SetActiveToolPhase(-1);
            Debug.LogWarning($"{missionLabel} timer expired.");
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

        DroughtTool selectedTool = GetActiveTool();
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

        DroughtTool droppedTool = draggedTool;
        draggedTool = null;

        SettlementTarget target = FindAcceptingTarget(droppedTool.ToolTransform);
        if (target != null)
        {
            ApplyToolToTarget(droppedTool, target);
            return;
        }

        ResetToolWithShake(droppedTool);
    }

    private SettlementTarget FindAcceptingTarget(RectTransform toolTransform)
    {
        List<SettlementTarget> activeGroup = HasIncompleteTarget(highRiskSettlements, phaseIndex)
            ? highRiskSettlements
            : lowRiskSettlements;

        for (int i = 0; i < activeGroup.Count; i++)
        {
            SettlementTarget target = activeGroup[i];
            if (target != null && target.CanAcceptPhase(phaseIndex, toolTransform))
            {
                return target;
            }
        }

        return null;
    }

    private void ApplyToolToTarget(DroughtTool droppedTool, SettlementTarget target)
    {
        target.ApplyTool(phaseIndex, IsGujaratiEnabled(), droppedTool.ToolSprite);

        completedPlacements++;
        UpdateProgressDisplay();

        bool phaseComplete = IsPhaseComplete(phaseIndex);
        ResetTool(droppedTool, !phaseComplete);

        if (phaseComplete)
        {
            inputLocked = true;
            SetActiveToolPhase(-1);
            StartTargetFill(target, AdvancePhaseOrCompleteAfterFill);
            return;
        }

        StartTargetFill(target);
    }

    private void AdvancePhaseOrCompleteAfterFill()
    {
        if (!isRunning || isComplete)
        {
            return;
        }

        inputLocked = false;
        AdvancePhaseOrComplete();
    }

    private void AdvancePhaseOrComplete()
    {
        if (phaseIndex >= GetToolCount() - 1)
        {
            CompleteMission();
            return;
        }

        phaseIndex++;
        SetActiveToolPhase(phaseIndex);
    }

    private bool IsPhaseComplete(int targetPhaseIndex)
    {
        return !HasIncompleteTarget(highRiskSettlements, targetPhaseIndex) &&
               !HasIncompleteTarget(lowRiskSettlements, targetPhaseIndex);
    }

    private static bool HasIncompleteTarget(List<SettlementTarget> targets, int targetPhaseIndex)
    {
        if (targets == null)
        {
            return false;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            SettlementTarget target = targets[i];
            if (target != null && target.CompletedPhase < targetPhaseIndex)
            {
                return true;
            }
        }

        return false;
    }

    private void CompleteMission()
    {
        isRunning = false;
        isComplete = true;
        inputLocked = true;
        draggedTool = null;
        SetActiveToolPhase(-1);
        UpdateTimerDisplay();

        SetActive(introScreen, false);
        SetActive(gameplayRoot, false);

        if (scoreReportScreen != null)
        {
            SetActive(scoreReportScreen, true);
            SetActive(completeScreen, false);
            return;
        }

        SetActive(completeScreen, true);
    }

    private void ShowCompleteScreen()
    {
        SetActive(scoreReportScreen, false);
        SetActive(completeScreen, true);
    }

    private void HandleCompleteNext()
    {
        onCompleteNext?.Invoke();
    }

    private void UpdateProgressDisplay()
    {
        if (missionProgressSlider == null)
        {
            return;
        }

        missionProgressSlider.value = completedPlacements;
    }

    private void ConfigureSliders()
    {
        if (timeRemainingSlider != null)
        {
            timeRemainingSlider.minValue = 0f;
            timeRemainingSlider.maxValue = missionDurationSeconds;
            timeRemainingSlider.interactable = false;
        }

        if (missionProgressSlider != null)
        {
            missionProgressSlider.minValue = 0f;
            missionProgressSlider.maxValue = Mathf.Max(1, GetToolCount() * GetTotalTargetCount());
            missionProgressSlider.interactable = false;
        }
    }

    private DroughtTool GetActiveTool()
    {
        return GetToolForPhaseIndex(phaseIndex);
    }

    private DroughtTool GetToolForPhaseIndex(int targetPhaseIndex)
    {
        int configuredIndex = 0;
        for (int i = 0; i < tools.Count; i++)
        {
            DroughtTool tool = tools[i];
            if (tool == null || !tool.IsConfigured)
            {
                continue;
            }

            if (configuredIndex == targetPhaseIndex)
            {
                return tool;
            }

            configuredIndex++;
        }

        return null;
    }

    private int GetToolCount()
    {
        int count = 0;
        for (int i = 0; i < tools.Count; i++)
        {
            if (tools[i] != null && tools[i].IsConfigured)
            {
                count++;
            }
        }

        return count;
    }

    private int GetTotalTargetCount()
    {
        return GetTargetCount(highRiskSettlements) + GetTargetCount(lowRiskSettlements);
    }

    private static int GetTargetCount(List<SettlementTarget> targets)
    {
        if (targets == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private void SetActiveToolPhase(int activePhaseIndex)
    {
        int configuredIndex = 0;
        for (int i = 0; i < tools.Count; i++)
        {
            DroughtTool tool = tools[i];
            bool isActiveTool = false;

            if (tool != null && tool.IsConfigured)
            {
                isActiveTool = configuredIndex == activePhaseIndex;
                configuredIndex++;
            }

            tool?.SetUsable(isActiveTool);
        }
    }

    private void ResetTools()
    {
        int configuredIndex = 0;
        for (int i = 0; i < tools.Count; i++)
        {
            DroughtTool tool = tools[i];
            bool isFirstConfiguredTool = false;

            if (tool != null && tool.IsConfigured)
            {
                isFirstConfiguredTool = configuredIndex == 0;
                configuredIndex++;
            }

            ResetTool(tool, isFirstConfiguredTool);
        }
    }

    private static void ResetTool(DroughtTool tool, bool usable)
    {
        if (tool == null)
        {
            return;
        }

        tool.ResetPosition();
        tool.SetUsable(usable);
    }

    private void ResetToolWithShake(DroughtTool tool)
    {
        StopShake();
        shakeRoutine = StartCoroutine(ShakeTool(tool));
    }

    private IEnumerator ShakeTool(DroughtTool tool)
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

    private void StartTargetFill(SettlementTarget target, Action onFilled = null)
    {
        if (target == null)
        {
            onFilled?.Invoke();
            return;
        }

        Coroutine existingRoutine;
        if (fillRoutines.TryGetValue(target, out existingRoutine) && existingRoutine != null)
        {
            StopCoroutine(existingRoutine);
        }

        fillRoutines[target] = StartCoroutine(FillTargetSlider(target, onFilled));
    }

    private IEnumerator FillTargetSlider(SettlementTarget target, Action onFilled)
    {
        const float fillDuration = 0.45f;

        float elapsed = 0f;
        target.SetProgress(0f);

        while (elapsed < fillDuration)
        {
            elapsed += Time.deltaTime;
            target.SetProgress(Mathf.Clamp01(elapsed / fillDuration));
            yield return null;
        }

        target.SetProgress(1f);
        fillRoutines.Remove(target);
        onFilled?.Invoke();
    }

    private void StopMissionRoutines()
    {
        StopShake();

        foreach (Coroutine routine in fillRoutines.Values)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        fillRoutines.Clear();
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
        RefreshAppliedToolSprites(highRiskSettlements, useGujarati);
        RefreshAppliedToolSprites(lowRiskSettlements, useGujarati);
    }

    private bool IsGujaratiEnabled()
    {
        return languageToggleController != null && languageToggleController.IsGujaratiEnabled;
    }

    private void RefreshAppliedToolSprites(List<SettlementTarget> targets, bool useGujarati)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            SettlementTarget target = targets[i];
            if (target != null)
            {
                target.RefreshAppliedSprite(useGujarati, GetFallbackSprite(target.CompletedPhase));
            }
        }
    }

    private Sprite GetFallbackSprite(int targetPhaseIndex)
    {
        DroughtTool tool = GetToolForPhaseIndex(targetPhaseIndex);
        return tool != null ? tool.ToolSprite : null;
    }

    private void CacheTools()
    {
        for (int i = 0; i < tools.Count; i++)
        {
            tools[i]?.CacheInitialState();
        }
    }

    private static void CacheTargets(List<SettlementTarget> targets)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            targets[i]?.CacheInitialState();
        }
    }

    private static void ResetTargets(List<SettlementTarget> targets)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            targets[i]?.ResetTarget();
        }
    }

    private static bool TryGetPointerWorldPosition(DroughtTool tool, Vector2 screenPosition, out Vector3 pointerWorldPosition)
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
    protected sealed class DroughtTool
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
        public bool IsConfigured => toolTransform != null && toolImage != null;

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
    protected sealed class SettlementTarget
    {
        [SerializeField] private string settlementName;
        [SerializeField] private RectTransform dropTarget;
        [SerializeField] private GameObject appliedToolObject;
        [SerializeField] private Image appliedToolImage;
        [SerializeField] private Slider toolFillSlider;
        [SerializeField, FormerlySerializedAs("toolSprites")] private Sprite[] englishToolSprites;
        [SerializeField] private Sprite[] gujaratiToolSprites;

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

            if (toolFillSlider != null)
            {
                toolFillSlider.minValue = 0f;
                toolFillSlider.maxValue = 1f;
                toolFillSlider.interactable = false;
            }

            SetActive(appliedToolObject, false);
            SetProgress(0f);
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
            if (appliedToolImage != null)
            {
                Sprite replacementSprite = GetReplacementSprite(phaseIndex, useGujarati, fallbackSprite);
                if (replacementSprite != null)
                {
                    appliedToolImage.sprite = replacementSprite;
                }

                appliedToolImage.color = initialColor;
                appliedToolImage.preserveAspect = true;
            }
        }

        public void SetProgress(float value)
        {
            if (toolFillSlider != null)
            {
                toolFillSlider.value = Mathf.Clamp01(value);
            }
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
            SetProgress(0f);
        }

        private Sprite GetReplacementSprite(int phaseIndex, bool useGujarati, Sprite fallbackSprite)
        {
            if (useGujarati &&
                gujaratiToolSprites != null &&
                phaseIndex >= 0 &&
                phaseIndex < gujaratiToolSprites.Length &&
                gujaratiToolSprites[phaseIndex] != null)
            {
                return gujaratiToolSprites[phaseIndex];
            }

            if (englishToolSprites != null &&
                phaseIndex >= 0 &&
                phaseIndex < englishToolSprites.Length &&
                englishToolSprites[phaseIndex] != null)
            {
                return englishToolSprites[phaseIndex];
            }

            return fallbackSprite;
        }
    }
}

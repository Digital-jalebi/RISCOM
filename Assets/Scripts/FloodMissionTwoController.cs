using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class FloodMissionTwoController : MonoBehaviour
{
    private const float DefaultMissionDurationSeconds = 600f;
    private const float SirenScaleDuration = 0.45f;
    private const float WrongDropShakeDuration = 0.25f;
    private const float WrongDropShakeFrequency = 48f;
    private const float WrongDropShakeAmplitude = 12f;
    private const float DisabledToolAlphaMultiplier = 0.5f;

    [SerializeField] private GameObject alertScreen;
    [SerializeField] private Button alertNextButton;
    [SerializeField] private GameObject playRoot;
    [SerializeField] private GameObject completeScreen;
    [SerializeField] private Button reportNextButton;
    [SerializeField] private GameObject floodImage;
    [SerializeField] private GameObject cloggedDrainsRoot;
    [SerializeField] private GameObject sirensRoot;
    [SerializeField] private Slider timeRemainingSlider;
    [SerializeField] private TextMeshProUGUI timeRemainingLabel;
    [SerializeField] private RISCOMLanguageToggleController languageToggleController;
    [SerializeField] private RISCOMNotificationPanel notificationPanel = new RISCOMNotificationPanel();
    [SerializeField] private PhaseNotificationSprites sandbagsCompleteNotification = new PhaseNotificationSprites();
    [SerializeField] private PhaseNotificationSprites drainsCompleteNotification = new PhaseNotificationSprites();
    [SerializeField] private PhaseNotificationSprites sirensCompleteNotification = new PhaseNotificationSprites();
    [SerializeField] private float missionDurationSeconds = DefaultMissionDurationSeconds;
    [SerializeField] private float completionDelaySeconds = 2f;
    [SerializeField] private FloodTool sandbagsTool;
    [SerializeField] private FloodTool drainClearanceTool;
    [SerializeField] private List<SandbagTarget> sandbagTargets = new List<SandbagTarget>();
    [SerializeField] private List<DrainTarget> drainTargets = new List<DrainTarget>();
    [SerializeField] private List<SirenTarget> sirenTargets = new List<SirenTarget>();

    private Action onReportNext;
    private MissionPhase phase;
    private FloodTool draggedTool;
    private Vector3 dragWorldOffset;
    private float timeRemaining;
    private int placedSandbags;
    private int clearedDrains;
    private int activatedSirens;
    private bool configured;
    private bool buttonsWired;
    private bool languageControllerWired;
    private bool isRunning;
    private bool isComplete;
    private bool inputLocked;
    private Coroutine shakeRoutine;
    private Coroutine completionRoutine;
    [SerializeField] private FloodToolAlertMessageSequence toolAlertMessages;

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

        WireLanguageController();

        if (configured)
        {
            return;
        }

        sandbagsTool?.CacheInitialState();
        drainClearanceTool?.CacheInitialState();
        CacheSandbagTargets();
        CacheDrainTargets();
        CacheSirenTargets();
        ConfigureTimerSlider();
        notificationPanel.Configure();

        toolAlertMessages?.Configure();
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
        notificationPanel.UpdateScrollInput();
        toolAlertMessages?.UpdatePulse(Time.unscaledTime);

        if (!isRunning || isComplete)
        {
            return;
        }

        if (phase == MissionPhase.Sirens)
        {
            UpdateSirenInput();
            return;
        }

        UpdateDragInput();
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

        phase = MissionPhase.Sandbags;
        placedSandbags = 0;
        clearedDrains = 0;
        activatedSirens = 0;
        timeRemaining = missionDurationSeconds;
        draggedTool = null;
        isRunning = true;
        isComplete = false;
        inputLocked = false;

        ResetTools();
        ResetSandbagTargets();
        ResetDrainTargets();
        ResetSirenTargets();
        ReapplyActiveLanguageAfterTargetReset();
        notificationPanel.Clear();
        SetActive(floodImage, false);
        SetActive(cloggedDrainsRoot, false);
        SetActive(sirensRoot, false);
        SetToolUsable(sandbagsTool, true);
        SetToolUsable(drainClearanceTool, false);
        UpdateTimerDisplay();
        toolAlertMessages?.SetActiveIndex(0);

        if (GetSandbagTargetCount() == 0)
        {
            EnterDrainClearancePhase();
        }
    }

    private void StopMission()
    {
        toolAlertMessages?.HideAll();
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
        ResetSandbagTargets();
        ResetDrainTargets();
        ResetSirenTargets();
        ReapplyActiveLanguageAfterTargetReset();
        notificationPanel.Clear();
        SetActive(floodImage, false);
        SetActive(cloggedDrainsRoot, false);
        SetActive(sirensRoot, false);
    }

    private void StopMissionRoutines()
    {
        StopShake();

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }

        StopSirenTweens();
    }

    private void UpdateTimer()
    {
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerDisplay();

        if (timeRemaining <= 0f)
        {
            StopMission();
            Debug.LogWarning("Flood Mission 2 timer expired.");
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

        if (phase == MissionPhase.Sandbags && droppedTool == sandbagsTool && TryPlaceSandbag(droppedTool.ToolTransform))
        {
            ResetTool(droppedTool, HasPendingSandbags());
            if (!HasPendingSandbags())
            {
                EnterDrainClearancePhase();
            }

            return;
        }

        if (phase == MissionPhase.DrainClearance && droppedTool == drainClearanceTool && TryClearDrain(droppedTool.ToolTransform))
        {
            ResetTool(droppedTool, HasPendingDrains());
            if (!HasPendingDrains())
            {
                EnterSirenPhase();
            }

            return;
        }

        ResetToolWithShake(droppedTool);
    }

    private bool TryPlaceSandbag(RectTransform toolTransform)
    {
        SandbagTarget target = FindSandbagTarget(toolTransform);
        if (target == null)
        {
            return false;
        }

        target.MarkPlaced();
        placedSandbags++;
        if (!HasPendingSandbags())
        {
            ShowNotification(sandbagsCompleteNotification.GetSprite(IsGujaratiEnabled()));
        }

        return true;
    }

    private bool TryClearDrain(RectTransform toolTransform)
    {
        DrainTarget target = FindDrainTarget(toolTransform);
        if (target == null)
        {
            return false;
        }

        target.MarkCleared(IsGujaratiEnabled());
        clearedDrains++;
        if (!HasPendingDrains())
        {
            ShowNotification(drainsCompleteNotification.GetSprite(IsGujaratiEnabled()));
        }

        return true;
    }

    private SandbagTarget FindSandbagTarget(RectTransform toolTransform)
    {
        for (int i = 0; i < sandbagTargets.Count; i++)
        {
            SandbagTarget target = sandbagTargets[i];
            if (target != null && target.CanAccept(toolTransform))
            {
                return target;
            }
        }

        return null;
    }

    private DrainTarget FindDrainTarget(RectTransform toolTransform)
    {
        for (int i = 0; i < drainTargets.Count; i++)
        {
            DrainTarget target = drainTargets[i];
            if (target != null && target.CanAccept(toolTransform))
            {
                return target;
            }
        }

        return null;
    }

    private void EnterDrainClearancePhase()
    {
        phase = MissionPhase.DrainClearance;
        SetActive(floodImage, true);
        SetActive(cloggedDrainsRoot, true);
        SetToolUsable(sandbagsTool, false);
        SetToolUsable(drainClearanceTool, true);

        if (GetDrainTargetCount() == 0)
        {
            EnterSirenPhase();
        }

        toolAlertMessages?.SetActiveIndex(1);
    }

    private void EnterSirenPhase()
    {
        phase = MissionPhase.Sirens;
        SetToolUsable(drainClearanceTool, false);
        SetActive(sirensRoot, true);

        if (GetSirenTargetCount() == 0)
        {
            CompleteMissionAfterDelay();
        }

        toolAlertMessages?.SetActiveIndex(2);
    }

    private void UpdateSirenInput()
    {
        if (inputLocked)
        {
            return;
        }

        if (UpdateTouchSirenInput())
        {
            return;
        }

        UpdateMouseSirenInput();
    }

    private bool UpdateTouchSirenInput()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
        {
            return false;
        }

        foreach (var touch in touchscreen.touches)
        {
            if (touch.press.wasPressedThisFrame)
            {
                TryActivateSiren(touch.position.ReadValue());
                return true;
            }
        }

        return false;
    }

    private void UpdateMouseSirenInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        TryActivateSiren(mouse.position.ReadValue());
    }

    private void TryActivateSiren(Vector2 screenPosition)
    {
        for (int i = 0; i < sirenTargets.Count; i++)
        {
            SirenTarget siren = sirenTargets[i];
            if (siren == null || !siren.CanActivate(screenPosition))
            {
                continue;
            }

            siren.Activate();
            activatedSirens++;

            if (activatedSirens >= GetSirenTargetCount())
            {
                ShowNotification(sirensCompleteNotification.GetSprite(IsGujaratiEnabled()));
                CompleteMissionAfterDelay();
            }

            return;
        }
    }

    private void CompleteMissionAfterDelay()
    {
        toolAlertMessages?.HideAll();
        isRunning = false;
        isComplete = true;
        inputLocked = true;

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

    private IEnumerator ShowCompletionAfterDelay()
    {
        yield return new WaitForSeconds(completionDelaySeconds);

        SetActive(playRoot, false);
        SetActive(completeScreen, true);
        completionRoutine = null;
    }

    private FloodTool GetActiveTool()
    {
        if (phase == MissionPhase.Sandbags)
        {
            return sandbagsTool;
        }

        if (phase == MissionPhase.DrainClearance)
        {
            return drainClearanceTool;
        }

        return null;
    }

    private void ResetTools()
    {
        ResetTool(sandbagsTool, true);
        ResetTool(drainClearanceTool, false);
    }

    private static void ResetTool(FloodTool tool, bool visible)
    {
        if (tool == null)
        {
            return;
        }

        tool.ResetPosition();
        tool.SetUsable(visible);
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

    private void CacheSandbagTargets()
    {
        for (int i = 0; i < sandbagTargets.Count; i++)
        {
            sandbagTargets[i]?.CacheInitialState();
        }
    }

    private void CacheDrainTargets()
    {
        for (int i = 0; i < drainTargets.Count; i++)
        {
            drainTargets[i]?.CacheInitialState();
        }
    }

    private void CacheSirenTargets()
    {
        for (int i = 0; i < sirenTargets.Count; i++)
        {
            sirenTargets[i]?.CacheInitialState();
        }
    }

    private void ResetSandbagTargets()
    {
        for (int i = 0; i < sandbagTargets.Count; i++)
        {
            sandbagTargets[i]?.ResetTarget();
        }
    }

    private void ResetDrainTargets()
    {
        for (int i = 0; i < drainTargets.Count; i++)
        {
            drainTargets[i]?.ResetTarget();
        }
    }

    private void ResetSirenTargets()
    {
        for (int i = 0; i < sirenTargets.Count; i++)
        {
            sirenTargets[i]?.ResetTarget();
        }
    }

    private void StopSirenTweens()
    {
        for (int i = 0; i < sirenTargets.Count; i++)
        {
            sirenTargets[i]?.StopAlertTween();
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
        RefreshUnclearedDrainSprites();
        RefreshClearedDrainSprites(useGujarati);
    }

    private bool IsGujaratiEnabled()
    {
        return languageToggleController != null && languageToggleController.IsGujaratiEnabled;
    }

    private void RefreshClearedDrainSprites(bool useGujarati)
    {
        for (int i = 0; i < drainTargets.Count; i++)
        {
            drainTargets[i]?.RefreshClearedSprite(useGujarati);
        }
    }

    private void ReapplyActiveLanguageAfterTargetReset()
    {
        if (languageToggleController != null)
        {
            languageToggleController.ApplyLanguage(languageToggleController.IsGujaratiEnabled);
            return;
        }

        RefreshUnclearedDrainSprites();
    }

    private void RefreshUnclearedDrainSprites()
    {
        for (int i = 0; i < drainTargets.Count; i++)
        {
            drainTargets[i]?.CacheCurrentDrainState();
        }
    }

    private bool HasPendingSandbags()
    {
        return placedSandbags < GetSandbagTargetCount();
    }

    private bool HasPendingDrains()
    {
        return clearedDrains < GetDrainTargetCount();
    }

    private int GetSandbagTargetCount()
    {
        int count = 0;
        for (int i = 0; i < sandbagTargets.Count; i++)
        {
            if (sandbagTargets[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private int GetDrainTargetCount()
    {
        int count = 0;
        for (int i = 0; i < drainTargets.Count; i++)
        {
            if (drainTargets[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private int GetSirenTargetCount()
    {
        int count = 0;
        for (int i = 0; i < sirenTargets.Count; i++)
        {
            if (sirenTargets[i] != null)
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
        Sandbags,
        DrainClearance,
        Sirens
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
    private sealed class SandbagTarget
    {
        [SerializeField] private string targetName;
        [SerializeField] private RectTransform dropTarget;
        [SerializeField] private GameObject sandbagObject;

        private bool isPlaced;

        public void CacheInitialState()
        {
            SetActive(sandbagObject, false);
        }

        public bool CanAccept(RectTransform toolTransform)
        {
            return !isPlaced &&
                   RectTransformsOverlap(toolTransform, dropTarget);
        }

        public void MarkPlaced()
        {
            isPlaced = true;
            SetActive(sandbagObject, true);
        }

        public void ResetTarget()
        {
            isPlaced = false;
            SetActive(sandbagObject, false);
        }
    }

    [Serializable]
    private sealed class DrainTarget
    {
        [SerializeField] private string targetName;
        [SerializeField] private RectTransform drainTransform;
        [SerializeField] private Image drainImage;
        [SerializeField, FormerlySerializedAs("clearedSprite")] private Sprite englishClearedSprite;
        [SerializeField] private Sprite gujaratiClearedSprite;

        private Sprite initialSprite;
        private Color initialColor;
        private bool isCleared;

        public void CacheInitialState()
        {
            if (drainImage != null)
            {
                CacheCurrentDrainState();
                drainImage.raycastTarget = true;
            }

        }

        public void CacheCurrentDrainState()
        {
            if (drainImage == null || isCleared)
            {
                return;
            }

            initialSprite = drainImage.sprite;
            initialColor = drainImage.color;
        }

        public bool CanAccept(RectTransform toolTransform)
        {
            return !isCleared &&
                   RectTransformsOverlap(toolTransform, drainTransform);
        }

        public void MarkCleared(bool useGujarati)
        {
            isCleared = true;
            ApplyClearedSprite(useGujarati);
        }

        public void RefreshClearedSprite(bool useGujarati)
        {
            if (isCleared)
            {
                ApplyClearedSprite(useGujarati);
            }
        }

        private void ApplyClearedSprite(bool useGujarati)
        {
            if (drainImage == null)
            {
                return;
            }

            Sprite targetSprite = GetClearedSprite(useGujarati);
            if (targetSprite == null)
            {
                return;
            }

            drainImage.sprite = targetSprite;
            drainImage.preserveAspect = true;
        }

        public void ResetTarget()
        {
            isCleared = false;

            if (drainImage != null)
            {
                drainImage.sprite = initialSprite;
                drainImage.color = initialColor;
            }

        }

        private Sprite GetClearedSprite(bool useGujarati)
        {
            if (useGujarati && gujaratiClearedSprite != null)
            {
                return gujaratiClearedSprite;
            }

            return englishClearedSprite;
        }
    }

    [Serializable]
    private sealed class SirenTarget
    {
        [SerializeField] private string targetName;
        [SerializeField] private RectTransform sirenTransform;
        [SerializeField] private GameObject infoObject;

        private Vector3 initialScale;
        private bool isActivated;
        private Tween alertTween;

        public void CacheInitialState()
        {
            if (sirenTransform != null)
            {
                initialScale = sirenTransform.localScale;
            }
        }

        public bool CanActivate(Vector2 screenPosition)
        {
            return !isActivated &&
                   sirenTransform != null &&
                   sirenTransform.gameObject.activeInHierarchy &&
                   RectTransformUtility.RectangleContainsScreenPoint(sirenTransform, screenPosition, null);
        }

        public void Activate()
        {
            isActivated = true;
            SetActive(infoObject, false);
            StopAlertTween();

            if (sirenTransform == null)
            {
                return;
            }

            sirenTransform.localScale = initialScale;
            alertTween = sirenTransform
                .DOScale(initialScale * 1.12f, SirenScaleDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        public void ResetTarget()
        {
            isActivated = false;
            SetActive(infoObject, true);
            StopAlertTween();

            if (sirenTransform != null)
            {
                sirenTransform.localScale = initialScale;
            }
        }

        public void StopAlertTween()
        {
            if (alertTween != null && alertTween.IsActive())
            {
                alertTween.Kill(false);
            }

            alertTween = null;
        }
    }
}

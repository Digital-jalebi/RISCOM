using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class RISCOMCycloneFlowController : MonoBehaviour
{
    private const int MissionTwoStepCount = 3;
    private const float MissionTwoDurationSeconds = 600f;
    private const float EffectAnimationDuration = 0.85f;
    private const float BoatMoveDuration = 0.85f;
    private const float MissionTwoReportDelaySeconds = 2f;

    [SerializeField] private GameObject cycloneSummary;
    [SerializeField] private GameObject summaryBackground;
    [SerializeField] private GameObject instructorImage;
    [SerializeField] private GameObject missionOneGame;
    [SerializeField] private GameObject missionCompleteScreen;
    [SerializeField] private Button missionOneReportNextButton;
    [SerializeField] private GameObject missionTwoGame;
    [SerializeField] private GameObject missionTwoAlertScreen;
    [SerializeField] private Button missionTwoAlertNextButton;
    [SerializeField] private GameObject missionTwoPlayRoot;
    [SerializeField] private GameObject missionTwoCompleteScreen;
    [SerializeField] private RectTransform missionTwoDropArea;
    [SerializeField] private Slider missionTwoTimeRemainingSlider;
    [SerializeField] private TextMeshProUGUI missionTwoTimeRemainingLabel;
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
    [SerializeField] private RectTransform[] missionTwoBoats;
    [SerializeField] private RectTransform[] missionTwoBoatTargets;
    [SerializeField] private GameObject[] directFlowChildren;
    [SerializeField] private GameObject[] summaryScreens;
    [SerializeField] private Button[] summaryNextButtons;
    [SerializeField] private CycloneMissionController missionOneController;

    private readonly MissionTwoStep[] missionTwoSteps = new MissionTwoStep[MissionTwoStepCount];

    private bool buttonsWired;
    private bool missionTwoConfigured;
    private int currentSummaryIndex;
    private int missionTwoStepIndex;
    private float missionTwoTimeRemaining;
    private bool missionTwoRunning;
    private bool missionTwoComplete;
    private bool missionTwoInputLocked;
    private MissionTwoStep draggedMissionTwoStep;
    private Vector3 missionTwoDragWorldOffset;
    private Vector3[] missionTwoBoatStartPositions;
    private Coroutine missionTwoShakeRoutine;
    private Coroutine missionTwoEffectRoutine;
    private Coroutine missionTwoReportDelayRoutine;

    public void BeginFlow()
    {
        WireButtons();
        PrepareMissionOne();
        ConfigureMissionTwo();

        gameObject.SetActive(true);
        HideDirectFlowChildren();

        SetActive(cycloneSummary, true);
        SetActive(summaryBackground, true);
        SetActive(instructorImage, true);
        ShowSummaryScreen(0);
    }

    private void Update()
    {
        if (!missionTwoRunning || missionTwoComplete)
        {
            return;
        }

        UpdateMissionTwoTimer();
        if (missionTwoRunning && !missionTwoComplete)
        {
            UpdateMissionTwoDragInput();
        }
    }

    private void WireButtons()
    {
        if (buttonsWired)
        {
            return;
        }

        WireSummaryButtons();

        if (missionOneReportNextButton != null)
        {
            missionOneReportNextButton.onClick.AddListener(StartMissionTwoIntro);
        }

        if (missionTwoAlertNextButton != null)
        {
            missionTwoAlertNextButton.onClick.AddListener(StartMissionTwoGame);
        }

        buttonsWired = true;
    }

    private void WireSummaryButtons()
    {
        if (summaryScreens == null || summaryNextButtons == null)
        {
            Debug.LogWarning("Cyclone flow summary screens or next buttons are not assigned.");
            return;
        }

        int buttonCount = Mathf.Min(summaryScreens.Length, summaryNextButtons.Length);
        for (int i = 0; i < buttonCount; i++)
        {
            int screenIndex = i;
            Button nextButton = summaryNextButtons[i];
            if (nextButton != null)
            {
                nextButton.onClick.AddListener(() => HandleSummaryNext(screenIndex));
            }
        }
    }

    private void PrepareMissionOne()
    {
        if (missionOneController == null)
        {
            Debug.LogWarning("Cyclone flow is missing its Mission 1 controller reference.");
            return;
        }

        missionOneController.Configure();
    }

    private void ConfigureMissionTwo()
    {
        if (missionTwoConfigured)
        {
            return;
        }

        missionTwoSteps[0] = new MissionTwoStep(radarScannerTool, radarScannerToolImage, radarScannerEffect, radarScannerEffectImage);
        missionTwoSteps[1] = new MissionTwoStep(radioTransmitterTool, radioTransmitterToolImage, radioTransmitterEffect, radioTransmitterEffectImage);
        missionTwoSteps[2] = new MissionTwoStep(boatDockingTool, boatDockingToolImage, boatDockingEffect, boatDockingEffectImage);

        for (int i = 0; i < missionTwoSteps.Length; i++)
        {
            MissionTwoStep step = missionTwoSteps[i];
            step.CacheInitialState();
            SetToolRaycast(step, true);
            HideMissionTwoEffect(step);
        }

        CacheMissionTwoBoatStartPositions();

        if (missionTwoTimeRemainingSlider != null)
        {
            missionTwoTimeRemainingSlider.minValue = 0f;
            missionTwoTimeRemainingSlider.maxValue = MissionTwoDurationSeconds;
            missionTwoTimeRemainingSlider.interactable = false;
        }

        missionTwoConfigured = true;
    }

    private void HandleSummaryNext(int screenIndex)
    {
        if (screenIndex != currentSummaryIndex)
        {
            return;
        }

        if (summaryScreens != null && currentSummaryIndex < summaryScreens.Length - 1)
        {
            ShowSummaryScreen(currentSummaryIndex + 1);
            return;
        }

        StartMissionOne();
    }

    private void ShowSummaryScreen(int index)
    {
        if (summaryScreens == null || summaryScreens.Length == 0)
        {
            return;
        }

        currentSummaryIndex = Mathf.Clamp(index, 0, Mathf.Max(0, summaryScreens.Length - 1));

        for (int i = 0; i < summaryScreens.Length; i++)
        {
            SetActive(summaryScreens[i], i == currentSummaryIndex);
        }
    }

    private void HideSummaryScreens()
    {
        if (summaryScreens == null)
        {
            return;
        }

        for (int i = 0; i < summaryScreens.Length; i++)
        {
            SetActive(summaryScreens[i], false);
        }
    }

    private void StartMissionOne()
    {
        StopMissionTwo();
        SetActive(cycloneSummary, false);
        SetActive(summaryBackground, false);
        SetActive(instructorImage, false);
        SetActive(missionCompleteScreen, false);
        SetActive(missionTwoGame, false);
        SetActive(missionOneGame, true);

        if (missionOneController != null)
        {
            missionOneController.BeginMission(CompleteMissionOne);
        }
    }

    private void CompleteMissionOne()
    {
        SetActive(missionOneGame, false);
        SetActive(instructorImage, false);
        SetActive(missionTwoGame, false);
        SetActive(missionCompleteScreen, true);
    }

    private void StartMissionTwoIntro()
    {
        StopMissionTwo();
        ConfigureMissionTwo();

        SetActive(missionCompleteScreen, false);
        SetActive(missionOneGame, false);
        SetActive(cycloneSummary, true);
        SetActive(summaryBackground, true);
        SetActive(instructorImage, false);
        HideSummaryScreens();
        SetActive(missionTwoGame, true);
        SetActive(missionTwoAlertScreen, true);
        SetActive(missionTwoPlayRoot, false);
        SetActive(missionTwoCompleteScreen, false);
    }

    private void StartMissionTwoGame()
    {
        SetActive(instructorImage, false);
        SetActive(missionTwoAlertScreen, false);
        SetActive(missionTwoCompleteScreen, false);
        SetActive(missionTwoPlayRoot, true);

        BeginMissionTwo();
    }

    private void BeginMissionTwo()
    {
        ConfigureMissionTwo();

        missionTwoStepIndex = 0;
        missionTwoTimeRemaining = MissionTwoDurationSeconds;
        missionTwoRunning = true;
        missionTwoComplete = false;
        missionTwoInputLocked = false;
        draggedMissionTwoStep = null;

        if (missionTwoShakeRoutine != null)
        {
            StopCoroutine(missionTwoShakeRoutine);
            missionTwoShakeRoutine = null;
        }

        if (missionTwoEffectRoutine != null)
        {
            StopCoroutine(missionTwoEffectRoutine);
            missionTwoEffectRoutine = null;
        }

        if (missionTwoReportDelayRoutine != null)
        {
            StopCoroutine(missionTwoReportDelayRoutine);
            missionTwoReportDelayRoutine = null;
        }

        for (int i = 0; i < missionTwoSteps.Length; i++)
        {
            ResetMissionTwoTool(missionTwoSteps[i], true);
            HideMissionTwoEffect(missionTwoSteps[i]);
        }

        ResetMissionTwoBoats();
        UpdateMissionTwoTimerDisplay();
    }

    private void StopMissionTwo()
    {
        ResetActiveMissionTwoDrag();
        missionTwoRunning = false;
        missionTwoComplete = false;
        missionTwoInputLocked = false;

        if (missionTwoShakeRoutine != null)
        {
            StopCoroutine(missionTwoShakeRoutine);
            missionTwoShakeRoutine = null;
        }

        if (missionTwoEffectRoutine != null)
        {
            StopCoroutine(missionTwoEffectRoutine);
            missionTwoEffectRoutine = null;
        }

        if (missionTwoReportDelayRoutine != null)
        {
            StopCoroutine(missionTwoReportDelayRoutine);
            missionTwoReportDelayRoutine = null;
        }

        if (missionTwoConfigured)
        {
            for (int i = 0; i < missionTwoSteps.Length; i++)
            {
                HideMissionTwoEffect(missionTwoSteps[i]);
            }
        }
    }

    private void UpdateMissionTwoTimer()
    {
        missionTwoTimeRemaining = Mathf.Max(0f, missionTwoTimeRemaining - Time.deltaTime);
        UpdateMissionTwoTimerDisplay();

        if (missionTwoTimeRemaining <= 0f)
        {
            StopMissionTwo();
            Debug.LogWarning("Cyclone Mission 2 timer expired.");
        }
    }

    private void UpdateMissionTwoTimerDisplay()
    {
        if (missionTwoTimeRemainingSlider != null)
        {
            missionTwoTimeRemainingSlider.value = missionTwoTimeRemaining;
        }

        if (missionTwoTimeRemainingLabel != null)
        {
            int roundedSeconds = Mathf.CeilToInt(missionTwoTimeRemaining);
            int minutes = roundedSeconds / 60;
            int seconds = roundedSeconds % 60;
            missionTwoTimeRemainingLabel.text = $"{minutes:00}:{seconds:00}s";
        }
    }

    private void UpdateMissionTwoDragInput()
    {
        if (missionTwoInputLocked)
        {
            return;
        }

        if (UpdateMissionTwoTouchInput())
        {
            return;
        }

        UpdateMissionTwoMouseInput();
    }

    private bool UpdateMissionTwoTouchInput()
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
                BeginMissionTwoDrag(touchPosition);
                return true;
            }

            if (touch.press.isPressed)
            {
                MoveMissionTwoDrag(touchPosition);
                return true;
            }

            if (touch.press.wasReleasedThisFrame)
            {
                EndMissionTwoDrag(touchPosition);
                return true;
            }
        }

        return false;
    }

    private void UpdateMissionTwoMouseInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        Vector2 mousePosition = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame)
        {
            BeginMissionTwoDrag(mousePosition);
        }

        if (mouse.leftButton.isPressed)
        {
            MoveMissionTwoDrag(mousePosition);
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            EndMissionTwoDrag(mousePosition);
        }
    }

    private void BeginMissionTwoDrag(Vector2 screenPosition)
    {
        if (draggedMissionTwoStep != null)
        {
            return;
        }

        MissionTwoStep step = FindMissionTwoToolAt(screenPosition);
        if (step == null)
        {
            return;
        }

        draggedMissionTwoStep = step;
        if (draggedMissionTwoStep.ToolTransform != null)
        {
            Vector3 pointerWorldPosition;
            missionTwoDragWorldOffset = TryGetMissionTwoPointerWorldPosition(draggedMissionTwoStep, screenPosition, out pointerWorldPosition)
                ? draggedMissionTwoStep.ToolTransform.position - pointerWorldPosition
                : Vector3.zero;

            draggedMissionTwoStep.ToolTransform.SetAsLastSibling();
        }

        MoveMissionTwoDrag(screenPosition);
    }

    private void MoveMissionTwoDrag(Vector2 screenPosition)
    {
        if (draggedMissionTwoStep == null || draggedMissionTwoStep.ToolTransform == null)
        {
            return;
        }

        Vector3 pointerWorldPosition;
        if (TryGetMissionTwoPointerWorldPosition(draggedMissionTwoStep, screenPosition, out pointerWorldPosition))
        {
            draggedMissionTwoStep.ToolTransform.position = pointerWorldPosition + missionTwoDragWorldOffset;
        }
    }

    private static bool TryGetMissionTwoPointerWorldPosition(MissionTwoStep step, Vector2 screenPosition, out Vector3 pointerWorldPosition)
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

    private void EndMissionTwoDrag(Vector2 screenPosition)
    {
        if (draggedMissionTwoStep == null)
        {
            return;
        }

        MissionTwoStep droppedStep = draggedMissionTwoStep;
        draggedMissionTwoStep = null;

        bool isExpectedStep = missionTwoStepIndex < missionTwoSteps.Length && droppedStep == missionTwoSteps[missionTwoStepIndex];
        bool isOnDropArea = missionTwoDropArea != null && RectTransformUtility.RectangleContainsScreenPoint(missionTwoDropArea, screenPosition, null);

        if (isExpectedStep && isOnDropArea)
        {
            AcceptMissionTwoTool(droppedStep);
            return;
        }

        ResetMissionTwoToolWithShake(droppedStep);
    }

    private MissionTwoStep FindMissionTwoToolAt(Vector2 screenPosition)
    {
        for (int i = 0; i < missionTwoSteps.Length; i++)
        {
            MissionTwoStep step = missionTwoSteps[i];
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

    private void AcceptMissionTwoTool(MissionTwoStep step)
    {
        missionTwoInputLocked = true;
        missionTwoStepIndex++;
        ResetMissionTwoTool(step, false);

        if (missionTwoStepIndex >= missionTwoSteps.Length)
        {
            missionTwoRunning = false;
        }

        if (missionTwoEffectRoutine != null)
        {
            StopCoroutine(missionTwoEffectRoutine);
        }

        missionTwoEffectRoutine = StartCoroutine(PlayMissionTwoEffect(step));
    }

    private IEnumerator PlayMissionTwoEffect(MissionTwoStep step)
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
                MoveMissionTwoBoats(Mathf.Clamp01(elapsed / BoatMoveDuration));
            }

            yield return null;
        }

        if (moveBoats)
        {
            MoveMissionTwoBoats(1f);
        }

        HideMissionTwoEffect(step);
        missionTwoEffectRoutine = null;

        if (missionTwoStepIndex >= missionTwoSteps.Length)
        {
            CompleteMissionTwo();
            yield break;
        }

        missionTwoInputLocked = false;
    }

    private void CompleteMissionTwo()
    {
        missionTwoRunning = false;
        missionTwoComplete = true;
        missionTwoInputLocked = false;
        draggedMissionTwoStep = null;

        if (missionTwoReportDelayRoutine != null)
        {
            StopCoroutine(missionTwoReportDelayRoutine);
        }

        missionTwoReportDelayRoutine = StartCoroutine(ShowMissionTwoReportAfterDelay());
    }

    private IEnumerator ShowMissionTwoReportAfterDelay()
    {
        yield return new WaitForSeconds(MissionTwoReportDelaySeconds);

        SetActive(missionTwoPlayRoot, false);
        SetActive(missionTwoCompleteScreen, true);
        missionTwoReportDelayRoutine = null;
    }

    private bool IsBoatDockingStep(MissionTwoStep step)
    {
        return missionTwoSteps.Length >= MissionTwoStepCount && step == missionTwoSteps[2];
    }

    private void CacheMissionTwoBoatStartPositions()
    {
        int boatCount = missionTwoBoats != null ? missionTwoBoats.Length : 0;
        missionTwoBoatStartPositions = new Vector3[boatCount];

        for (int i = 0; i < boatCount; i++)
        {
            if (missionTwoBoats[i] != null)
            {
                missionTwoBoatStartPositions[i] = missionTwoBoats[i].position;
            }
        }
    }

    private void ResetMissionTwoBoats()
    {
        if (missionTwoBoats == null || missionTwoBoatStartPositions == null)
        {
            return;
        }

        int boatCount = Mathf.Min(missionTwoBoats.Length, missionTwoBoatStartPositions.Length);
        for (int i = 0; i < boatCount; i++)
        {
            if (missionTwoBoats[i] != null)
            {
                missionTwoBoats[i].position = missionTwoBoatStartPositions[i];
            }
        }
    }

    private void MoveMissionTwoBoats(float progress)
    {
        if (missionTwoBoats == null || missionTwoBoatTargets == null || missionTwoBoatStartPositions == null)
        {
            return;
        }

        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        int boatCount = Mathf.Min(Mathf.Min(missionTwoBoats.Length, missionTwoBoatTargets.Length), missionTwoBoatStartPositions.Length);
        for (int i = 0; i < boatCount; i++)
        {
            RectTransform boat = missionTwoBoats[i];
            RectTransform target = missionTwoBoatTargets[i];
            if (boat != null && target != null)
            {
                boat.position = Vector3.Lerp(missionTwoBoatStartPositions[i], target.position, easedProgress);
            }
        }
    }

    private void ResetMissionTwoToolWithShake(MissionTwoStep step)
    {
        if (missionTwoShakeRoutine != null)
        {
            StopCoroutine(missionTwoShakeRoutine);
        }

        missionTwoShakeRoutine = StartCoroutine(ShakeMissionTwoTool(step));
    }

    private IEnumerator ShakeMissionTwoTool(MissionTwoStep step)
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
        missionTwoShakeRoutine = null;
    }

    private void ResetActiveMissionTwoDrag()
    {
        if (draggedMissionTwoStep == null)
        {
            return;
        }

        ResetMissionTwoTool(draggedMissionTwoStep, true);
        draggedMissionTwoStep = null;
    }

    private static void ResetMissionTwoTool(MissionTwoStep step, bool visible)
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

    private static void HideMissionTwoEffect(MissionTwoStep step)
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

    private void HideDirectFlowChildren()
    {
        if (directFlowChildren == null)
        {
            return;
        }

        for (int i = 0; i < directFlowChildren.Length; i++)
        {
            SetActive(directFlowChildren[i], false);
        }
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

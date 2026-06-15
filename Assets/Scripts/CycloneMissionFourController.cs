using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class CycloneMissionFourController : MonoBehaviour
{
    private const float MissionDurationSeconds = 600f;

    [SerializeField] private GameObject alertScreen;
    [SerializeField] private Button alertNextButton;
    [SerializeField] private GameObject playRoot;
    [SerializeField] private GameObject completeScreen;
    [SerializeField] private Button reportNextButton;
    [SerializeField] private Slider timeRemainingSlider;
    [SerializeField] private TextMeshProUGUI timeRemainingLabel;
    [SerializeField] private MissionFourPair[] pairs;

    private Action onReportNext;
    private bool configured;
    private bool buttonsWired;
    private float timeRemaining;
    private bool isRunning;
    private bool isComplete;
    private bool inputLocked;
    private MissionFourPair draggedPair;
    private Vector3 dragWorldOffset;
    private Coroutine shakeRoutine;

    public void Configure(Action reportNextHandler = null)
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

        if (pairs != null)
        {
            for (int i = 0; i < pairs.Length; i++)
            {
                MissionFourPair pair = pairs[i];
                if (pair != null)
                {
                    pair.CacheInitialState();
                    SetToolRaycast(pair, true);
                }
            }
        }

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

        timeRemaining = MissionDurationSeconds;
        isRunning = true;
        isComplete = false;
        inputLocked = false;
        draggedPair = null;

        StopMissionRoutines();
        ResetPairs();
        UpdateTimerDisplay();
    }

    private void StopMission()
    {
        ResetActiveDrag();
        isRunning = false;
        isComplete = false;
        inputLocked = false;
        StopMissionRoutines();
    }

    private void StopMissionRoutines()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }
    }

    private void UpdateTimer()
    {
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerDisplay();

        if (timeRemaining <= 0f)
        {
            StopMission();
            Debug.LogWarning("Cyclone Mission 4 timer expired.");
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
        if (draggedPair != null)
        {
            return;
        }

        MissionFourPair pair = FindToolAt(screenPosition);
        if (pair == null)
        {
            return;
        }

        draggedPair = pair;
        if (draggedPair.ToolTransform != null)
        {
            Vector3 pointerWorldPosition;
            dragWorldOffset = TryGetPointerWorldPosition(draggedPair, screenPosition, out pointerWorldPosition)
                ? draggedPair.ToolTransform.position - pointerWorldPosition
                : Vector3.zero;

            draggedPair.ToolTransform.SetAsLastSibling();
        }

        MoveDrag(screenPosition);
    }

    private void MoveDrag(Vector2 screenPosition)
    {
        if (draggedPair == null || draggedPair.ToolTransform == null)
        {
            return;
        }

        Vector3 pointerWorldPosition;
        if (TryGetPointerWorldPosition(draggedPair, screenPosition, out pointerWorldPosition))
        {
            draggedPair.ToolTransform.position = pointerWorldPosition + dragWorldOffset;
        }
    }

    private void EndDrag(Vector2 screenPosition)
    {
        if (draggedPair == null)
        {
            return;
        }

        MissionFourPair droppedPair = draggedPair;
        draggedPair = null;

        MissionFourPair targetPair = FindNeedAt(screenPosition);
        if (CanFulfillNeedWithTool(targetPair, droppedPair))
        {
            AcceptPair(targetPair);
            return;
        }

        ResetToolWithShake(droppedPair);
    }

    private MissionFourPair FindToolAt(Vector2 screenPosition)
    {
        if (pairs == null)
        {
            return null;
        }

        for (int i = 0; i < pairs.Length; i++)
        {
            MissionFourPair pair = pairs[i];
            if (pair == null || pair.Fulfilled || pair.ToolTransform == null ||
                !pair.ToolTransform.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(pair.ToolTransform, screenPosition, null))
            {
                return pair;
            }
        }

        return null;
    }

    private MissionFourPair FindNeedAt(Vector2 screenPosition)
    {
        if (pairs == null)
        {
            return null;
        }

        for (int i = 0; i < pairs.Length; i++)
        {
            MissionFourPair pair = pairs[i];
            if (pair == null || pair.Fulfilled || pair.NeedTransform == null ||
                !pair.NeedTransform.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(pair.NeedTransform, screenPosition, null))
            {
                return pair;
            }
        }

        return null;
    }

    private void AcceptPair(MissionFourPair pair)
    {
        pair.Fulfilled = true;
        ApplyFulfilledSprite(pair);

        bool toolStillNeeded = HasPendingNeedForTool(pair.ToolTransform);
        ResetTool(pair, toolStillNeeded);
        SetToolRaycast(pair, toolStillNeeded);

        if (AreAllPairsFulfilled())
        {
            CompleteMission();
        }
    }

    private static bool CanFulfillNeedWithTool(MissionFourPair targetPair, MissionFourPair draggedPair)
    {
        return targetPair != null &&
               draggedPair != null &&
               targetPair.ToolTransform != null &&
               targetPair.ToolTransform == draggedPair.ToolTransform;
    }

    private bool HasPendingNeedForTool(RectTransform toolTransform)
    {
        if (pairs == null || toolTransform == null)
        {
            return false;
        }

        for (int i = 0; i < pairs.Length; i++)
        {
            MissionFourPair pair = pairs[i];
            if (pair != null && !pair.Fulfilled && pair.ToolTransform == toolTransform)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyFulfilledSprite(MissionFourPair pair)
    {
        if (pair.NeedImage == null || pair.FulfilledSprite == null)
        {
            return;
        }

        pair.NeedImage.sprite = pair.FulfilledSprite;
        pair.NeedImage.preserveAspect = true;
    }

    private bool AreAllPairsFulfilled()
    {
        if (pairs == null || pairs.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < pairs.Length; i++)
        {
            if (pairs[i] == null || !pairs[i].Fulfilled)
            {
                return false;
            }
        }

        return true;
    }

    private void CompleteMission()
    {
        isRunning = false;
        isComplete = true;
        inputLocked = false;
        draggedPair = null;

        SetActive(playRoot, false);
        SetActive(completeScreen, true);
    }

    private void ResetPairs()
    {
        if (pairs == null)
        {
            return;
        }

        for (int i = 0; i < pairs.Length; i++)
        {
            MissionFourPair pair = pairs[i];
            if (pair == null)
            {
                continue;
            }

            pair.Fulfilled = false;
            ResetTool(pair, true);
            SetToolRaycast(pair, true);
            ResetNeed(pair);
        }
    }

    private void ResetToolWithShake(MissionFourPair pair)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
        }

        shakeRoutine = StartCoroutine(ShakeTool(pair));
    }

    private IEnumerator ShakeTool(MissionFourPair pair)
    {
        if (pair == null || pair.ToolTransform == null)
        {
            yield break;
        }

        const float duration = 0.25f;
        const float frequency = 48f;
        const float amplitude = 12f;
        float elapsed = 0f;

        pair.ToolTransform.gameObject.SetActive(true);
        pair.ToolTransform.SetSiblingIndex(pair.InitialSiblingIndex);
        pair.ToolTransform.localScale = pair.InitialToolScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * frequency) * amplitude;
            pair.ToolTransform.anchoredPosition = pair.InitialToolPosition + new Vector2(offset, 0f);
            yield return null;
        }

        pair.ToolTransform.anchoredPosition = pair.InitialToolPosition;
        shakeRoutine = null;
    }

    private void ResetActiveDrag()
    {
        if (draggedPair == null)
        {
            return;
        }

        ResetTool(draggedPair, true);
        draggedPair = null;
    }

    private static void ResetTool(MissionFourPair pair, bool visible)
    {
        if (pair == null || pair.ToolTransform == null)
        {
            return;
        }

        pair.ToolTransform.anchoredPosition = pair.InitialToolPosition;
        pair.ToolTransform.localScale = pair.InitialToolScale;
        pair.ToolTransform.SetSiblingIndex(pair.InitialSiblingIndex);
        pair.ToolTransform.gameObject.SetActive(visible);
    }

    private static void ResetNeed(MissionFourPair pair)
    {
        if (pair.NeedImage == null)
        {
            return;
        }

        pair.NeedImage.sprite = pair.InitialNeedSprite;
        pair.NeedImage.preserveAspect = pair.InitialNeedPreserveAspect;
    }

    private static void SetToolRaycast(MissionFourPair pair, bool raycastTarget)
    {
        if (pair != null && pair.ToolImage != null)
        {
            pair.ToolImage.raycastTarget = raycastTarget;
        }
    }

    private static bool TryGetPointerWorldPosition(
        MissionFourPair pair,
        Vector2 screenPosition,
        out Vector3 pointerWorldPosition)
    {
        pointerWorldPosition = Vector3.zero;
        if (pair == null || pair.ToolTransform == null)
        {
            return false;
        }

        RectTransform parentRect = pair.ToolTransform.parent as RectTransform;
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

    [Serializable]
    private sealed class MissionFourPair
    {
        [SerializeField] private RectTransform toolTransform;
        [SerializeField] private Image toolImage;
        [SerializeField] private RectTransform needTransform;
        [SerializeField] private Image needImage;
        [SerializeField] private Sprite fulfilledSprite;

        public RectTransform ToolTransform => toolTransform;
        public Image ToolImage => toolImage;
        public RectTransform NeedTransform => needTransform;
        public Image NeedImage => needImage;
        public Sprite FulfilledSprite => fulfilledSprite;

        public bool Fulfilled;
        public Vector2 InitialToolPosition;
        public Vector3 InitialToolScale;
        public int InitialSiblingIndex;
        public Sprite InitialNeedSprite;
        public bool InitialNeedPreserveAspect;

        public void CacheInitialState()
        {
            if (ToolTransform != null)
            {
                InitialToolPosition = ToolTransform.anchoredPosition;
                InitialToolScale = ToolTransform.localScale;
                InitialSiblingIndex = ToolTransform.GetSiblingIndex();
            }

            if (NeedImage != null)
            {
                InitialNeedSprite = NeedImage.sprite;
                InitialNeedPreserveAspect = NeedImage.preserveAspect;
            }
        }
    }
}

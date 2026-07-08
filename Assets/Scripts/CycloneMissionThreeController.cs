using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class CycloneMissionThreeController : MonoBehaviour
{
    private const int BusToolCount = 2;
    private const float MissionDurationSeconds = 600f;

    [SerializeField] private GameObject alertScreen;
    [SerializeField] private Button alertNextButton;
    [SerializeField] private GameObject playRoot;
    [SerializeField] private GameObject completeScreen;
    [SerializeField] private Button reportNextButton;
    [SerializeField] private Slider timeRemainingSlider;
    [SerializeField] private TextMeshProUGUI timeRemainingLabel;
    [SerializeField] private RISCOMLanguageToggleController languageToggleController;
    [SerializeField] private RISCOMNotificationPanel notificationPanel = new RISCOMNotificationPanel();
    [SerializeField] private VillageRescueNotification[] villageRescueNotifications;
    [SerializeField] private Sprite[] englishVillageNotificationSprites;
    [SerializeField] private Sprite[] gujaratiVillageNotificationSprites;
    [SerializeField] private RectTransform busCapacity200Tool;
    [SerializeField] private Image busCapacity200ToolImage;
    [SerializeField] private RectTransform busCapacity100Tool;
    [SerializeField] private Image busCapacity100ToolImage;
    [SerializeField] private RectTransform missionBus;
    [SerializeField] private Image missionBusImage;
    [SerializeField] private BusDirectionSprites missionBusSprites = new BusDirectionSprites();
    [SerializeField] private RectTransform[] villages;
    [SerializeField] private TextMeshProUGUI[] villageCountLabels;
    [SerializeField] private int[] villageInitialCounts;
    [SerializeField] private RectTransform[] shelters;
    [SerializeField] private TextMeshProUGUI[] shelterCapacityLabels;
    [SerializeField] private int[] shelterCapacities;
    [SerializeField] private ShelterRoute[] shelterRoutes;
    [SerializeField] private ShelterOverflowPlan[] shelterOverflowPlans;
    [SerializeField] private ShelterPriority[] villageShelterPriorities;
    [SerializeField] private VillageRoutePlan[] villageRoutePlans;
    [SerializeField] private float busMoveSpeed = 360f;

    private readonly MissionThreeBusTool[] busTools = new MissionThreeBusTool[BusToolCount];
    private readonly List<Vector3> pathBuffer = new List<Vector3>();
    private readonly Vector3[] draggedToolWorldCorners = new Vector3[4];
    private readonly Vector3[] targetWorldCorners = new Vector3[4];
    [SerializeField] private CycloneToolAlertMessageSequence toolAlertMessages;

    private Action onReportNext;
    private bool configured;
    private bool buttonsWired;
    private int[] villageRemainingCounts;
    private int[] shelterOccupiedCounts;
    private float timeRemaining;
    private bool isRunning;
    private bool isComplete;
    private bool inputLocked;
    private MissionThreeBusTool draggedBusTool;
    private Vector3 dragWorldOffset;
    private Vector3 busInitialPosition;
    private Coroutine shakeRoutine;
    private Coroutine tripRoutine;

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

        if (configured)
        {
            return;
        }

        busTools[0] = new MissionThreeBusTool(busCapacity200Tool, busCapacity200ToolImage, 200);
        busTools[1] = new MissionThreeBusTool(busCapacity100Tool, busCapacity100ToolImage, 100);

        for (int i = 0; i < busTools.Length; i++)
        {
            MissionThreeBusTool tool = busTools[i];
            tool.CacheInitialState();
            SetToolRaycast(tool, true);
        }

        if (missionBus != null)
        {
            busInitialPosition = missionBus.position;
            if (missionBusImage == null)
            {
                missionBusImage = missionBus.GetComponent<Image>();
            }

            missionBus.gameObject.SetActive(false);
        }

        villageRemainingCounts = CopyOrCreateCounts(villageInitialCounts, villages);
        shelterOccupiedCounts = CreateZeroCounts(shelters);
        NormalizePreferredDirectShelterRoutes();
        notificationPanel.Configure();
        toolAlertMessages?.Configure();

        if (timeRemainingSlider != null)
        {
            timeRemainingSlider.minValue = 0f;
            timeRemainingSlider.maxValue = MissionDurationSeconds;
            timeRemainingSlider.interactable = false;
        }

        configured = true;
    }

    private void NormalizePreferredDirectShelterRoutes()
    {
        int preferredShelterIndex = FindShelterIndexByCapacity(200);
        if (preferredShelterIndex < 0)
        {
            return;
        }

        if (villageShelterPriorities != null)
        {
            for (int i = 0; i < villageShelterPriorities.Length; i++)
            {
                if (villageShelterPriorities[i] != null)
                {
                    villageShelterPriorities[i].MoveShelterToFront(preferredShelterIndex);
                }
            }
        }

        if (villageRoutePlans == null)
        {
            return;
        }

        for (int i = 0; i < villageRoutePlans.Length; i++)
        {
            if (villageRoutePlans[i] != null)
            {
                villageRoutePlans[i].MovePriorityRouteToFront(preferredShelterIndex);
            }
        }
    }

    private int FindShelterIndexByCapacity(int capacity)
    {
        if (shelterCapacities == null)
        {
            return -1;
        }

        for (int i = 0; i < shelterCapacities.Length; i++)
        {
            if (shelterCapacities[i] == capacity)
            {
                return i;
            }
        }

        return -1;
    }

    private void HandleReportNext()
    {
        onReportNext?.Invoke();
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
        notificationPanel.UpdateScrollInput();
        toolAlertMessages?.UpdatePulse(Time.unscaledTime);
        if (isRunning && !isComplete)
        {
            UpdateDragInput();
        }
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
        draggedBusTool = null;

        StopMissionRoutines();

        villageRemainingCounts = CopyOrCreateCounts(villageInitialCounts, villages);
        shelterOccupiedCounts = CreateZeroCounts(shelters);
        NormalizePreferredDirectShelterRoutes();

        for (int i = 0; i < busTools.Length; i++)
        {
            ResetBusTool(busTools[i], true);
            SetToolRaycast(busTools[i], true);
        }

        if (missionBus != null)
        {
            missionBus.position = busInitialPosition;
            missionBus.gameObject.SetActive(false);
        }

        UpdateVillageLabels();
        UpdateShelterLabels();
        notificationPanel.Clear();
        toolAlertMessages?.SetActiveIndex(0);
        UpdateTimerDisplay();
    }

    private void StopMission()
    {
        ResetActiveDrag();
        isRunning = false;
        isComplete = false;
        inputLocked = false;
        StopMissionRoutines();

        if (missionBus != null)
        {
            missionBus.position = busInitialPosition;
            missionBus.gameObject.SetActive(false);
        }

        notificationPanel.Clear();
        toolAlertMessages?.HideAll();
    }

    private void StopMissionRoutines()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        if (tripRoutine != null)
        {
            StopCoroutine(tripRoutine);
            tripRoutine = null;
        }
    }

    private void UpdateTimer()
    {
        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        UpdateTimerDisplay();

        if (timeRemaining <= 0f)
        {
            StopMission();
            Debug.LogWarning("Cyclone Mission 3 timer expired.");
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
        if (draggedBusTool != null)
        {
            return;
        }

        MissionThreeBusTool tool = FindBusToolAt(screenPosition);
        if (tool == null)
        {
            return;
        }

        draggedBusTool = tool;
        if (draggedBusTool.ToolTransform != null)
        {
            Vector3 pointerWorldPosition;
            dragWorldOffset = TryGetPointerWorldPosition(draggedBusTool, screenPosition, out pointerWorldPosition)
                ? draggedBusTool.ToolTransform.position - pointerWorldPosition
                : Vector3.zero;

            draggedBusTool.ToolTransform.SetAsLastSibling();
        }

        MoveDrag(screenPosition);
    }

    private void MoveDrag(Vector2 screenPosition)
    {
        if (draggedBusTool == null || draggedBusTool.ToolTransform == null)
        {
            return;
        }

        Vector3 pointerWorldPosition;
        if (TryGetPointerWorldPosition(draggedBusTool, screenPosition, out pointerWorldPosition))
        {
            draggedBusTool.ToolTransform.position = pointerWorldPosition + dragWorldOffset;
        }
    }

    private void EndDrag(Vector2 screenPosition)
    {
        if (draggedBusTool == null)
        {
            return;
        }

        MissionThreeBusTool droppedTool = draggedBusTool;
        draggedBusTool = null;

        int villageIndex = FindVillageForDroppedTool(droppedTool, screenPosition);
        if (villageIndex >= 0 && CanEvacuateFromVillage(villageIndex))
        {
            StartEvacuationTrip(droppedTool, villageIndex);
            return;
        }

        ResetBusToolWithShake(droppedTool);
    }

    private MissionThreeBusTool FindBusToolAt(Vector2 screenPosition)
    {
        for (int i = 0; i < busTools.Length; i++)
        {
            MissionThreeBusTool tool = busTools[i];
            if (tool == null || tool.ToolTransform == null || !tool.ToolTransform.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(tool.ToolTransform, screenPosition, null))
            {
                return tool;
            }
        }

        return null;
    }

    private int FindVillageAt(Vector2 screenPosition)
    {
        if (villages == null)
        {
            return -1;
        }

        for (int i = 0; i < villages.Length; i++)
        {
            RectTransform village = villages[i];
            if (village != null && RectTransformUtility.RectangleContainsScreenPoint(village, screenPosition, null))
            {
                return i;
            }
        }

        return -1;
    }

    private int FindVillageForDroppedTool(MissionThreeBusTool tool, Vector2 screenPosition)
    {
        int overlappingVillageIndex = FindBestOverlappingVillage(tool != null ? tool.ToolTransform : null);
        return overlappingVillageIndex >= 0 ? overlappingVillageIndex : FindVillageAt(screenPosition);
    }

    private int FindBestOverlappingVillage(RectTransform draggedTransform)
    {
        if (draggedTransform == null || villages == null)
        {
            return -1;
        }

        Rect draggedRect = GetScreenRect(draggedTransform, draggedToolWorldCorners);
        float bestOverlapArea = 0f;
        int bestIndex = -1;

        for (int i = 0; i < villages.Length; i++)
        {
            RectTransform village = villages[i];
            if (village == null || !village.gameObject.activeInHierarchy)
            {
                continue;
            }

            float overlapArea = GetOverlapArea(draggedRect, GetScreenRect(village, targetWorldCorners));
            if (overlapArea > bestOverlapArea)
            {
                bestOverlapArea = overlapArea;
                bestIndex = i;
            }
        }

        return bestOverlapArea > 0f ? bestIndex : -1;
    }

    private bool CanEvacuateFromVillage(int villageIndex)
    {
        return villageRemainingCounts != null &&
               villageIndex >= 0 &&
               villageIndex < villageRemainingCounts.Length &&
               villageRemainingCounts[villageIndex] > 0 &&
               GetReachableShelterRemainingCapacityFromVillage(villageIndex) > 0;
    }

    private void StartEvacuationTrip(MissionThreeBusTool tool, int villageIndex)
    {
        int passengerCount = Mathf.Min(
            Mathf.Min(tool.Capacity, villageRemainingCounts[villageIndex]),
            GetReachableShelterRemainingCapacityFromVillage(villageIndex));

        if (passengerCount <= 0)
        {
            ResetBusToolWithShake(tool);
            return;
        }

        inputLocked = true;
        SetToolsInteractable(false);
        ResetBusTool(tool, true);
        ShowVillageNotification(villageIndex, passengerCount);

        if (tripRoutine != null)
        {
            StopCoroutine(tripRoutine);
        }

        tripRoutine = StartCoroutine(RunEvacuationTrip(villageIndex, passengerCount));
    }

    private IEnumerator RunEvacuationTrip(int villageIndex, int passengerCount)
    {
        RectTransform village = GetVillage(villageIndex);
        if (missionBus == null || village == null)
        {
            FinishTrip();
            yield break;
        }

        missionBus.position = GetVillageRouteStartPosition(villageIndex, village.position);
        missionBus.gameObject.SetActive(true);

        villageRemainingCounts[villageIndex] = Mathf.Max(0, villageRemainingCounts[villageIndex] - passengerCount);
        UpdateVillageLabels();

        int passengersRemaining = passengerCount;
        int currentShelterIndex = -1;
        bool[] visitedShelters = CreateShelterVisitBuffer();
        while (passengersRemaining > 0)
        {
            int shelterIndex;
            PriorityRoute priorityRoute = null;
            ShelterOverflowRoute overflowRoute = null;

            if (currentShelterIndex >= 0)
            {
                shelterIndex = FindReachableShelterFromOverflow(currentShelterIndex, visitedShelters);
                if (shelterIndex >= 0)
                {
                    overflowRoute = GetShelterOverflowRoute(currentShelterIndex, shelterIndex);
                }
            }
            else
            {
                shelterIndex = FindNearestReachableShelter(village.position, villageIndex, visitedShelters);
                if (shelterIndex >= 0)
                {
                    priorityRoute = GetVillagePriorityRouteForShelter(villageIndex, shelterIndex);
                }
            }

            if (shelterIndex < 0 ||
                currentShelterIndex >= 0 && overflowRoute == null ||
                currentShelterIndex < 0 && HasConfiguredVillageRoutePlan(villageIndex) && priorityRoute == null)
            {
                break;
            }

            RectTransform shelter = shelters[shelterIndex];
            if (currentShelterIndex >= 0)
            {
                yield return MoveBusAlongOverflowRoute(missionBus.position, shelter.position, currentShelterIndex, overflowRoute);
            }
            else
            {
                yield return MoveBusAlongVillageRoute(missionBus.position, shelter.position, villageIndex, shelterIndex, priorityRoute);
            }

            int shelterRemaining = GetShelterRemainingCapacity(shelterIndex);
            int transferred = Mathf.Min(passengersRemaining, shelterRemaining);
            if (transferred > 0)
            {
                shelterOccupiedCounts[shelterIndex] += transferred;
                passengersRemaining -= transferred;
                UpdateShelterLabels();
            }

            currentShelterIndex = shelterIndex;
            MarkShelterVisited(visitedShelters, shelterIndex);
        }

        if (passengersRemaining > 0)
        {
            villageRemainingCounts[villageIndex] += passengersRemaining;
            UpdateVillageLabels();
        }

        missionBus.gameObject.SetActive(false);
        FinishTrip();
    }

    private IEnumerator MoveBusAlongVillageRoute(
        Vector3 from,
        Vector3 to,
        int villageIndex,
        int shelterIndex,
        PriorityRoute priorityRoute)
    {
        BuildVillageRoute(from, to, villageIndex, shelterIndex, priorityRoute);

        yield return MoveBusAlongPath(from);
    }

    private IEnumerator MoveBusAlongOverflowRoute(
        Vector3 from,
        Vector3 to,
        int sourceShelterIndex,
        ShelterOverflowRoute overflowRoute)
    {
        BuildOverflowRoute(from, to, sourceShelterIndex, overflowRoute);

        yield return MoveBusAlongPath(from);
    }

    private IEnumerator MoveBusAlongPath(Vector3 from)
    {
        Vector3 current = from;
        for (int i = 0; i < pathBuffer.Count; i++)
        {
            Vector3 target = pathBuffer[i];
            ApplyMissionBusDirection(current, target);

            while (Vector3.Distance(current, target) > 0.5f)
            {
                current = Vector3.MoveTowards(current, target, busMoveSpeed * Time.deltaTime);
                missionBus.position = current;
                yield return null;
            }

            current = target;
            missionBus.position = current;
        }
    }

    private void ApplyMissionBusDirection(Vector3 from, Vector3 to)
    {
        Sprite sprite = missionBusSprites.GetSprite(from, to);
        if (missionBusImage != null && sprite != null)
        {
            missionBusImage.sprite = sprite;
        }
    }

    private void FinishTrip()
    {
        tripRoutine = null;

        if (AreAllSheltersFull())
        {
            CompleteMission();
            return;
        }

        inputLocked = false;
        SetToolsInteractable(true);
    }

    private void ShowVillageNotification(int villageIndex, int rescuedCount)
    {
        notificationPanel.Show(GetVillageNotificationSprite(villageIndex, rescuedCount));
    }

    private Sprite GetVillageNotificationSprite(int villageIndex, int rescuedCount)
    {
        VillageRescueNotification rescueNotification = GetVillageRescueNotification(villageIndex);
        if (rescueNotification != null)
        {
            Sprite rescuedCountSprite = rescueNotification.GetSprite(rescuedCount, IsGujaratiEnabled());
            if (rescuedCountSprite != null)
            {
                return rescuedCountSprite;
            }
        }

        if (IsGujaratiEnabled())
        {
            Sprite gujaratiSprite = GetSpriteAt(gujaratiVillageNotificationSprites, villageIndex);
            if (gujaratiSprite != null)
            {
                return gujaratiSprite;
            }
        }

        return GetSpriteAt(englishVillageNotificationSprites, villageIndex);
    }

    private VillageRescueNotification GetVillageRescueNotification(int villageIndex)
    {
        return villageRescueNotifications != null &&
               villageIndex >= 0 &&
               villageIndex < villageRescueNotifications.Length
            ? villageRescueNotifications[villageIndex]
            : null;
    }

    private bool IsGujaratiEnabled()
    {
        return languageToggleController != null && languageToggleController.IsGujaratiEnabled;
    }

    private static Sprite GetSpriteAt(Sprite[] sprites, int index)
    {
        return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }

    private void CompleteMission()
    {
        isRunning = false;
        isComplete = true;
        inputLocked = false;
        draggedBusTool = null;
        SetToolsInteractable(false);

        if (missionBus != null)
        {
            missionBus.gameObject.SetActive(false);
        }

        SetActive(playRoot, false);
        SetActive(completeScreen, true);
        toolAlertMessages?.HideAll();
    }

    private void ResetBusToolWithShake(MissionThreeBusTool tool)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
        }

        shakeRoutine = StartCoroutine(ShakeBusTool(tool));
    }

    private IEnumerator ShakeBusTool(MissionThreeBusTool tool)
    {
        if (tool == null || tool.ToolTransform == null)
        {
            yield break;
        }

        const float duration = 0.25f;
        const float frequency = 48f;
        const float amplitude = 12f;
        float elapsed = 0f;

        tool.ToolTransform.gameObject.SetActive(true);
        tool.ToolTransform.SetSiblingIndex(tool.InitialSiblingIndex);
        tool.ToolTransform.localScale = tool.InitialToolScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * frequency) * amplitude;
            tool.ToolTransform.anchoredPosition = tool.InitialToolPosition + new Vector2(offset, 0f);
            yield return null;
        }

        tool.ToolTransform.anchoredPosition = tool.InitialToolPosition;
        shakeRoutine = null;
    }

    private void ResetActiveDrag()
    {
        if (draggedBusTool == null)
        {
            return;
        }

        ResetBusTool(draggedBusTool, true);
        draggedBusTool = null;
    }

    private static void ResetBusTool(MissionThreeBusTool tool, bool visible)
    {
        if (tool == null || tool.ToolTransform == null)
        {
            return;
        }

        tool.ToolTransform.anchoredPosition = tool.InitialToolPosition;
        tool.ToolTransform.localScale = tool.InitialToolScale;
        tool.ToolTransform.SetSiblingIndex(tool.InitialSiblingIndex);
        tool.ToolTransform.gameObject.SetActive(visible);
    }

    private void SetToolsInteractable(bool interactable)
    {
        for (int i = 0; i < busTools.Length; i++)
        {
            SetToolRaycast(busTools[i], interactable);
        }
    }

    private static void SetToolRaycast(MissionThreeBusTool tool, bool raycastTarget)
    {
        if (tool != null && tool.ToolImage != null)
        {
            tool.ToolImage.raycastTarget = raycastTarget;
        }
    }

    private RectTransform GetVillage(int villageIndex)
    {
        if (villages == null || villageIndex < 0 || villageIndex >= villages.Length)
        {
            return null;
        }

        return villages[villageIndex];
    }

    private int GetTotalShelterRemainingCapacity()
    {
        if (shelters == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < shelters.Length; i++)
        {
            total += GetShelterRemainingCapacity(i);
        }

        return total;
    }

    private int GetReachableShelterRemainingCapacityFromVillage(int villageIndex)
    {
        if (shelters == null)
        {
            return 0;
        }

        VillageRoutePlan routePlan = GetVillageRoutePlan(villageIndex);
        if (routePlan != null && routePlan.PriorityRoutes != null && routePlan.PriorityRoutes.Length > 0)
        {
            int routeTotal = 0;
            bool[] countedShelters = CreateShelterVisitBuffer();
            PriorityRoute[] priorityRoutes = routePlan.PriorityRoutes;
            for (int i = 0; i < priorityRoutes.Length; i++)
            {
                PriorityRoute priorityRoute = priorityRoutes[i];
                if (priorityRoute != null && IsValidShelterIndex(priorityRoute.ShelterIndex))
                {
                    routeTotal += GetReachableShelterRemainingCapacity(priorityRoute.ShelterIndex, countedShelters);
                }
            }

            return routeTotal;
        }

        ShelterPriority priority = GetVillageShelterPriority(villageIndex);
        if (priority != null && priority.ShelterIndexes != null && priority.ShelterIndexes.Length > 0)
        {
            int priorityTotal = 0;
            bool[] countedShelters = CreateShelterVisitBuffer();
            int[] shelterIndexes = priority.ShelterIndexes;
            for (int i = 0; i < shelterIndexes.Length; i++)
            {
                priorityTotal += GetReachableShelterRemainingCapacity(shelterIndexes[i], countedShelters);
            }

            return priorityTotal;
        }

        return GetTotalShelterRemainingCapacity();
    }

    private int GetReachableShelterRemainingCapacity(int shelterIndex, bool[] countedShelters)
    {
        if (!IsValidShelterIndex(shelterIndex) ||
            countedShelters == null ||
            shelterIndex >= countedShelters.Length ||
            countedShelters[shelterIndex])
        {
            return 0;
        }

        countedShelters[shelterIndex] = true;

        int total = GetShelterRemainingCapacity(shelterIndex);
        ShelterOverflowPlan overflowPlan = GetShelterOverflowPlan(shelterIndex);
        if (overflowPlan == null || overflowPlan.OverflowRoutes == null)
        {
            return total;
        }

        ShelterOverflowRoute[] overflowRoutes = overflowPlan.OverflowRoutes;
        for (int i = 0; i < overflowRoutes.Length; i++)
        {
            ShelterOverflowRoute overflowRoute = overflowRoutes[i];
            if (overflowRoute != null)
            {
                total += GetReachableShelterRemainingCapacity(overflowRoute.ShelterIndex, countedShelters);
            }
        }

        return total;
    }

    private int GetShelterRemainingCapacity(int shelterIndex)
    {
        if (shelterCapacities == null ||
            shelterOccupiedCounts == null ||
            shelterIndex < 0 ||
            shelterIndex >= shelterCapacities.Length ||
            shelterIndex >= shelterOccupiedCounts.Length)
        {
            return 0;
        }

        return Mathf.Max(0, shelterCapacities[shelterIndex] - shelterOccupiedCounts[shelterIndex]);
    }

    private bool AreAllSheltersFull()
    {
        if (shelters == null || shelters.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < shelters.Length; i++)
        {
            if (GetShelterRemainingCapacity(i) > 0)
            {
                return false;
            }
        }

        return true;
    }

    private int FindNearestReachableShelter(Vector3 priorityOrigin, int villageIndex, bool[] visitedShelters)
    {
        if (shelters == null)
        {
            return -1;
        }

        int priorityShelterIndex = FindReachableShelterFromVillageRoutes(villageIndex, visitedShelters);
        if (priorityShelterIndex >= 0)
        {
            return priorityShelterIndex;
        }

        priorityShelterIndex = FindAvailableShelterFromPriority(villageIndex);
        if (priorityShelterIndex >= 0)
        {
            return priorityShelterIndex;
        }

        return HasConfiguredVillageRoutePlan(villageIndex)
            ? -1
            : FindNearestAvailableShelter(priorityOrigin, visitedShelters);
    }

    private int FindNearestAvailableShelter(Vector3 from, bool[] visitedShelters)
    {
        if (shelters == null)
        {
            return -1;
        }

        int bestIndex = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < shelters.Length; i++)
        {
            if (IsShelterVisited(visitedShelters, i) || !IsShelterAvailable(i))
            {
                continue;
            }

            RectTransform shelter = shelters[i];
            float distance = shelter != null ? Vector3.SqrMagnitude(from - shelter.position) : float.MaxValue;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private int FindReachableShelterFromVillageRoutes(int villageIndex, bool[] visitedShelters)
    {
        VillageRoutePlan routePlan = GetVillageRoutePlan(villageIndex);
        if (routePlan == null || routePlan.PriorityRoutes == null)
        {
            return -1;
        }

        PriorityRoute[] priorityRoutes = routePlan.PriorityRoutes;
        for (int i = 0; i < priorityRoutes.Length; i++)
        {
            PriorityRoute priorityRoute = priorityRoutes[i];
            if (priorityRoute == null)
            {
                continue;
            }

            if (!IsShelterVisited(visitedShelters, priorityRoute.ShelterIndex) &&
                IsShelterAvailable(priorityRoute.ShelterIndex))
            {
                return priorityRoute.ShelterIndex;
            }
        }

        for (int i = 0; i < priorityRoutes.Length; i++)
        {
            PriorityRoute priorityRoute = priorityRoutes[i];
            if (priorityRoute == null || IsShelterVisited(visitedShelters, priorityRoute.ShelterIndex))
            {
                continue;
            }

            bool[] searchVisitedShelters = CopyShelterVisitBuffer(visitedShelters);
            if (HasReachableShelterCapacity(priorityRoute.ShelterIndex, searchVisitedShelters))
            {
                return priorityRoute.ShelterIndex;
            }
        }

        return -1;
    }

    private int FindReachableShelterFromOverflow(int sourceShelterIndex, bool[] visitedShelters)
    {
        ShelterOverflowPlan overflowPlan = GetShelterOverflowPlan(sourceShelterIndex);
        if (overflowPlan == null || overflowPlan.OverflowRoutes == null)
        {
            return -1;
        }

        ShelterOverflowRoute[] overflowRoutes = overflowPlan.OverflowRoutes;
        for (int i = 0; i < overflowRoutes.Length; i++)
        {
            ShelterOverflowRoute overflowRoute = overflowRoutes[i];
            if (overflowRoute != null &&
                !IsShelterVisited(visitedShelters, overflowRoute.ShelterIndex) &&
                IsShelterAvailable(overflowRoute.ShelterIndex))
            {
                return overflowRoute.ShelterIndex;
            }
        }

        for (int i = 0; i < overflowRoutes.Length; i++)
        {
            ShelterOverflowRoute overflowRoute = overflowRoutes[i];
            if (overflowRoute == null || IsShelterVisited(visitedShelters, overflowRoute.ShelterIndex))
            {
                continue;
            }

            bool[] searchVisitedShelters = CopyShelterVisitBuffer(visitedShelters);
            if (HasReachableShelterCapacity(overflowRoute.ShelterIndex, searchVisitedShelters))
            {
                return overflowRoute.ShelterIndex;
            }
        }

        return -1;
    }

    private int FindAvailableShelterFromPriority(int villageIndex)
    {
        ShelterPriority priority = GetVillageShelterPriority(villageIndex);
        if (priority == null || priority.ShelterIndexes == null)
        {
            return -1;
        }

        int[] shelterIndexes = priority.ShelterIndexes;
        for (int i = 0; i < shelterIndexes.Length; i++)
        {
            int shelterIndex = shelterIndexes[i];
            if ((!HasConfiguredVillageRoutePlan(villageIndex) ||
                 GetVillagePriorityRouteForShelter(villageIndex, shelterIndex) != null) &&
                IsShelterAvailable(shelterIndex))
            {
                return shelterIndex;
            }
        }

        return -1;
    }

    private ShelterPriority GetVillageShelterPriority(int villageIndex)
    {
        if (villageShelterPriorities == null ||
            villageIndex < 0 ||
            villageIndex >= villageShelterPriorities.Length)
        {
            return null;
        }

        return villageShelterPriorities[villageIndex];
    }

    private bool IsShelterAvailable(int shelterIndex)
    {
        return IsValidShelterIndex(shelterIndex) &&
               GetShelterRemainingCapacity(shelterIndex) > 0;
    }

    private bool IsValidShelterIndex(int shelterIndex)
    {
        return shelterIndex >= 0 &&
               shelters != null &&
               shelterIndex < shelters.Length &&
               shelters[shelterIndex] != null;
    }

    private bool HasReachableShelterCapacity(int shelterIndex, bool[] visitedShelters)
    {
        if (!IsValidShelterIndex(shelterIndex) || IsShelterVisited(visitedShelters, shelterIndex))
        {
            return false;
        }

        if (IsShelterAvailable(shelterIndex))
        {
            return true;
        }

        MarkShelterVisited(visitedShelters, shelterIndex);

        ShelterOverflowPlan overflowPlan = GetShelterOverflowPlan(shelterIndex);
        if (overflowPlan == null || overflowPlan.OverflowRoutes == null)
        {
            return false;
        }

        ShelterOverflowRoute[] overflowRoutes = overflowPlan.OverflowRoutes;
        for (int i = 0; i < overflowRoutes.Length; i++)
        {
            ShelterOverflowRoute overflowRoute = overflowRoutes[i];
            if (overflowRoute != null && HasReachableShelterCapacity(overflowRoute.ShelterIndex, visitedShelters))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasConfiguredVillageRoutePlan(int villageIndex)
    {
        VillageRoutePlan routePlan = GetVillageRoutePlan(villageIndex);
        return routePlan != null &&
               routePlan.PriorityRoutes != null &&
               routePlan.PriorityRoutes.Length > 0;
    }

    private bool[] CreateShelterVisitBuffer()
    {
        return shelters != null ? new bool[shelters.Length] : null;
    }

    private static bool[] CopyShelterVisitBuffer(bool[] source)
    {
        if (source == null)
        {
            return null;
        }

        bool[] copy = new bool[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            copy[i] = source[i];
        }

        return copy;
    }

    private static bool IsShelterVisited(bool[] visitedShelters, int shelterIndex)
    {
        return visitedShelters != null &&
               shelterIndex >= 0 &&
               shelterIndex < visitedShelters.Length &&
               visitedShelters[shelterIndex];
    }

    private static void MarkShelterVisited(bool[] visitedShelters, int shelterIndex)
    {
        if (visitedShelters != null && shelterIndex >= 0 && shelterIndex < visitedShelters.Length)
        {
            visitedShelters[shelterIndex] = true;
        }
    }

    private static int FindClosestRoutePointIndex(Vector3 from, RectTransform[] points)
    {
        if (points == null || points.Length == 0)
        {
            return -1;
        }

        int bestIndex = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            RectTransform point = points[i];
            if (point == null)
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(from - point.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void BuildVillageRoute(
        Vector3 from,
        Vector3 to,
        int villageIndex,
        int shelterIndex,
        PriorityRoute priorityRoute)
    {
        pathBuffer.Clear();

        if (priorityRoute != null)
        {
            BuildVillagePriorityRoute(from, to, villageIndex, priorityRoute);
            return;
        }

        RectTransform[] pathPoints = GetShelterRoutePoints(shelterIndex);
        if (pathPoints != null && pathPoints.Length > 0)
        {
            BuildAssignedRoute(from, to, pathPoints);
            return;
        }

        if (HasAnyShelterRoute())
        {
            pathBuffer.Add(to);
            return;
        }

        pathBuffer.Add(to);
    }

    private void BuildVillagePriorityRoute(
        Vector3 from,
        Vector3 to,
        int villageIndex,
        PriorityRoute priorityRoute)
    {
        Vector3 routeStart = GetVillageRouteStartPosition(villageIndex, from);

        if (!IsNear(from, routeStart))
        {
            AddRouteTarget(routeStart);
        }

        AddRoutePoints(priorityRoute.Points, false);
        AddRouteTarget(to);
    }

    private void BuildOverflowRoute(
        Vector3 from,
        Vector3 to,
        int sourceShelterIndex,
        ShelterOverflowRoute overflowRoute)
    {
        pathBuffer.Clear();

        if (overflowRoute == null)
        {
            pathBuffer.Add(to);
            return;
        }

        Vector3 routeStart = GetShelterOverflowStartPosition(sourceShelterIndex, from);
        if (!IsNear(from, routeStart))
        {
            AddRouteTarget(routeStart);
        }

        AddRoutePoints(overflowRoute.Points, false);
        AddRouteTarget(to);
    }

    private void AddRoutePoints(RectTransform[] pointsToAdd, bool reverse)
    {
        if (pointsToAdd == null || pointsToAdd.Length == 0)
        {
            return;
        }

        if (reverse)
        {
            for (int i = pointsToAdd.Length - 1; i >= 0; i--)
            {
                AddRoutePoint(pointsToAdd[i]);
            }

            return;
        }

        for (int i = 0; i < pointsToAdd.Length; i++)
        {
            AddRoutePoint(pointsToAdd[i]);
        }
    }

    private void AddRoutePoint(RectTransform point)
    {
        if (point != null)
        {
            AddRouteTarget(point.position);
        }
    }

    private void AddRouteTarget(Vector3 target)
    {
        if (pathBuffer.Count == 0 || !IsNear(pathBuffer[pathBuffer.Count - 1], target))
        {
            pathBuffer.Add(target);
        }
    }

    private static bool IsNear(Vector3 from, Vector3 to)
    {
        return Vector3.SqrMagnitude(from - to) <= 0.25f;
    }

    private void BuildAssignedRoute(Vector3 from, Vector3 to, RectTransform[] pathPoints)
    {
        int pointCount = pathPoints != null ? pathPoints.Length : 0;
        if (pointCount == 0)
        {
            pathBuffer.Add(to);
            return;
        }

        int fromIndex = FindClosestRoutePointIndex(from, pathPoints);
        int shelterEndIndex = FindShelterEndRoutePointIndex(to, pathPoints);
        if (fromIndex < 0 || shelterEndIndex < 0)
        {
            pathBuffer.Add(to);
            return;
        }

        int direction = fromIndex <= shelterEndIndex ? 1 : -1;
        for (int i = fromIndex; i != shelterEndIndex + direction; i += direction)
        {
            RectTransform point = pathPoints[i];
            if (point != null)
            {
                pathBuffer.Add(point.position);
            }
        }

        if (pathBuffer.Count == 0 || Vector3.SqrMagnitude(pathBuffer[pathBuffer.Count - 1] - to) > 0.25f)
        {
            pathBuffer.Add(to);
        }
    }

    private static int FindShelterEndRoutePointIndex(Vector3 shelterPosition, RectTransform[] pathPoints)
    {
        return FindClosestRoutePointIndex(shelterPosition, pathPoints);
    }

    private RectTransform[] GetShelterRoutePoints(int shelterIndex)
    {
        if (shelterRoutes == null ||
            shelterIndex < 0 ||
            shelterIndex >= shelterRoutes.Length ||
            shelterRoutes[shelterIndex] == null)
        {
            return null;
        }

        return shelterRoutes[shelterIndex].Points;
    }

    private Vector3 GetShelterOverflowStartPosition(int shelterIndex, Vector3 fallback)
    {
        ShelterOverflowPlan overflowPlan = GetShelterOverflowPlan(shelterIndex);
        return overflowPlan != null && overflowPlan.StartingPoint != null
            ? overflowPlan.StartingPoint.position
            : fallback;
    }

    private ShelterOverflowRoute GetShelterOverflowRoute(int sourceShelterIndex, int targetShelterIndex)
    {
        ShelterOverflowPlan overflowPlan = GetShelterOverflowPlan(sourceShelterIndex);
        if (overflowPlan == null || overflowPlan.OverflowRoutes == null)
        {
            return null;
        }

        ShelterOverflowRoute[] overflowRoutes = overflowPlan.OverflowRoutes;
        for (int i = 0; i < overflowRoutes.Length; i++)
        {
            ShelterOverflowRoute overflowRoute = overflowRoutes[i];
            if (overflowRoute != null && overflowRoute.ShelterIndex == targetShelterIndex)
            {
                return overflowRoute;
            }
        }

        return null;
    }

    private ShelterOverflowPlan GetShelterOverflowPlan(int shelterIndex)
    {
        if (shelterOverflowPlans == null ||
            shelterIndex < 0 ||
            shelterIndex >= shelterOverflowPlans.Length)
        {
            return null;
        }

        return shelterOverflowPlans[shelterIndex];
    }

    private Vector3 GetVillageRouteStartPosition(int villageIndex, Vector3 fallback)
    {
        VillageRoutePlan routePlan = GetVillageRoutePlan(villageIndex);
        return routePlan != null && routePlan.StartingPoint != null
            ? routePlan.StartingPoint.position
            : fallback;
    }

    private PriorityRoute GetVillagePriorityRouteForShelter(int villageIndex, int shelterIndex)
    {
        VillageRoutePlan routePlan = GetVillageRoutePlan(villageIndex);
        if (routePlan == null || routePlan.PriorityRoutes == null)
        {
            return null;
        }

        PriorityRoute[] priorityRoutes = routePlan.PriorityRoutes;
        for (int i = 0; i < priorityRoutes.Length; i++)
        {
            PriorityRoute priorityRoute = priorityRoutes[i];
            if (priorityRoute != null && priorityRoute.ShelterIndex == shelterIndex)
            {
                return priorityRoute;
            }
        }

        return null;
    }

    private VillageRoutePlan GetVillageRoutePlan(int villageIndex)
    {
        if (villageRoutePlans == null ||
            villageIndex < 0 ||
            villageIndex >= villageRoutePlans.Length)
        {
            return null;
        }

        return villageRoutePlans[villageIndex];
    }

    private bool HasShelterRoute(int shelterIndex)
    {
        RectTransform[] points = GetShelterRoutePoints(shelterIndex);
        return points != null && points.Length > 0;
    }

    private bool HasAnyShelterRoute()
    {
        if (shelterRoutes == null)
        {
            return false;
        }

        for (int i = 0; i < shelterRoutes.Length; i++)
        {
            if (HasShelterRoute(i))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateVillageLabels()
    {
        if (villageCountLabels == null || villageRemainingCounts == null)
        {
            return;
        }

        int labelCount = Mathf.Min(villageCountLabels.Length, villageRemainingCounts.Length);
        for (int i = 0; i < labelCount; i++)
        {
            if (villageCountLabels[i] != null)
            {
                villageCountLabels[i].text = villageRemainingCounts[i].ToString();
            }
        }
    }

    private void UpdateShelterLabels()
    {
        if (shelterCapacityLabels == null ||
            shelterCapacities == null ||
            shelterOccupiedCounts == null)
        {
            return;
        }

        int labelCount = Mathf.Min(
            Mathf.Min(shelterCapacityLabels.Length, shelterCapacities.Length),
            shelterOccupiedCounts.Length);

        for (int i = 0; i < labelCount; i++)
        {
            TextMeshProUGUI label = shelterCapacityLabels[i];
            if (label != null)
            {
                int remainingCapacity = GetShelterRemainingCapacity(i);
                label.text = remainingCapacity <= 0
                    ? "Max"
                    : remainingCapacity.ToString();
            }
        }
    }

    private static Rect GetScreenRect(RectTransform rectTransform, Vector3[] worldCorners)
    {
        rectTransform.GetWorldCorners(worldCorners);

        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, worldCorners[0]);
        Vector2 max = min;
        for (int i = 1; i < worldCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldCorners[i]);
            min = Vector2.Min(min, screenPoint);
            max = Vector2.Max(max, screenPoint);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static float GetOverlapArea(Rect first, Rect second)
    {
        float overlapWidth = Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin);
        float overlapHeight = Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin);
        return overlapWidth > 0f && overlapHeight > 0f ? overlapWidth * overlapHeight : 0f;
    }

    private static bool TryGetPointerWorldPosition(MissionThreeBusTool tool, Vector2 screenPosition, out Vector3 pointerWorldPosition)
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

    private static int[] CopyOrCreateCounts(int[] sourceCounts, RectTransform[] sourceObjects)
    {
        int count = sourceCounts != null ? sourceCounts.Length : sourceObjects != null ? sourceObjects.Length : 0;
        int[] copy = new int[count];
        for (int i = 0; i < count; i++)
        {
            copy[i] = sourceCounts != null && i < sourceCounts.Length ? sourceCounts[i] : 0;
        }

        return copy;
    }

    private static int[] CreateZeroCounts(RectTransform[] sourceObjects)
    {
        int count = sourceObjects != null ? sourceObjects.Length : 0;
        return new int[count];
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    [Serializable]
    private sealed class VillageRescueNotification
    {
        [SerializeField] private Sprite englishRescued100Sprite;
        [SerializeField] private Sprite englishRescued200Sprite;
        [SerializeField] private Sprite gujaratiRescued100Sprite;
        [SerializeField] private Sprite gujaratiRescued200Sprite;

        public Sprite GetSprite(int rescuedCount, bool useGujarati)
        {
            bool useTwoHundredSprite = rescuedCount > 100;
            if (useGujarati)
            {
                Sprite gujaratiSprite = useTwoHundredSprite
                    ? gujaratiRescued200Sprite
                    : gujaratiRescued100Sprite;

                if (gujaratiSprite != null)
                {
                    return gujaratiSprite;
                }
            }

            return useTwoHundredSprite ? englishRescued200Sprite : englishRescued100Sprite;
        }
    }

    [Serializable]
    private sealed class ShelterRoute
    {
        [SerializeField] private RectTransform[] points;

        public RectTransform[] Points => points;
    }

    [Serializable]
    private sealed class ShelterOverflowPlan
    {
        [SerializeField] private RectTransform startingPoint;
        [SerializeField] private ShelterOverflowRoute[] overflowRoutes;

        public RectTransform StartingPoint => startingPoint;
        public ShelterOverflowRoute[] OverflowRoutes => overflowRoutes;
    }

    [Serializable]
    private sealed class ShelterOverflowRoute
    {
        [SerializeField] private int shelterIndex;
        [SerializeField] private RectTransform[] points;

        public int ShelterIndex => shelterIndex;
        public RectTransform[] Points => points;
    }

    [Serializable]
    private sealed class ShelterPriority
    {
        [SerializeField] private int[] shelterIndexes;

        public int[] ShelterIndexes => shelterIndexes;

        public void MoveShelterToFront(int shelterIndex)
        {
            int index = IndexOfShelter(shelterIndex);
            if (index <= 0)
            {
                return;
            }

            int selectedShelterIndex = shelterIndexes[index];
            for (int i = index; i > 0; i--)
            {
                shelterIndexes[i] = shelterIndexes[i - 1];
            }

            shelterIndexes[0] = selectedShelterIndex;
        }

        private int IndexOfShelter(int shelterIndex)
        {
            if (shelterIndexes == null)
            {
                return -1;
            }

            for (int i = 0; i < shelterIndexes.Length; i++)
            {
                if (shelterIndexes[i] == shelterIndex)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    [Serializable]
    private sealed class VillageRoutePlan
    {
        [SerializeField] private RectTransform startingPoint;
        [SerializeField] private PriorityRoute[] priorityRoutes;

        public RectTransform StartingPoint => startingPoint;
        public PriorityRoute[] PriorityRoutes => priorityRoutes;

        public void MovePriorityRouteToFront(int shelterIndex)
        {
            int index = IndexOfPriorityRoute(shelterIndex);
            if (index <= 0)
            {
                return;
            }

            PriorityRoute selectedRoute = priorityRoutes[index];
            for (int i = index; i > 0; i--)
            {
                priorityRoutes[i] = priorityRoutes[i - 1];
            }

            priorityRoutes[0] = selectedRoute;
        }

        private int IndexOfPriorityRoute(int shelterIndex)
        {
            if (priorityRoutes == null)
            {
                return -1;
            }

            for (int i = 0; i < priorityRoutes.Length; i++)
            {
                PriorityRoute route = priorityRoutes[i];
                if (route != null && route.ShelterIndex == shelterIndex)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    [Serializable]
    private sealed class PriorityRoute
    {
        [SerializeField] private int shelterIndex;
        [SerializeField] private RectTransform[] points;

        public int ShelterIndex => shelterIndex;
        public RectTransform[] Points => points;
    }

    [Serializable]
    private sealed class BusDirectionSprites
    {
        [SerializeField] private Sprite upSprite;
        [SerializeField] private Sprite downSprite;
        [SerializeField] private Sprite leftSprite;
        [SerializeField] private Sprite rightSprite;

        public Sprite GetSprite(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude <= 0.01f)
            {
                return null;
            }

            if (Mathf.Approximately(delta.x, 0f))
            {
                return delta.y >= 0f ? upSprite : downSprite;
            }

            if (Mathf.Approximately(delta.y, 0f))
            {
                return delta.x >= 0f ? rightSprite : leftSprite;
            }

            if (delta.y > 0f)
            {
                return delta.x < 0f ? upSprite : rightSprite;
            }

            return delta.x < 0f ? leftSprite : downSprite;
        }
    }

    private sealed class MissionThreeBusTool
    {
        public readonly RectTransform ToolTransform;
        public readonly Image ToolImage;
        public readonly int Capacity;

        public Vector2 InitialToolPosition;
        public Vector3 InitialToolScale;
        public int InitialSiblingIndex;

        public MissionThreeBusTool(RectTransform toolTransform, Image toolImage, int capacity)
        {
            ToolTransform = toolTransform;
            ToolImage = toolImage;
            Capacity = capacity;
        }

        public void CacheInitialState()
        {
            if (ToolTransform != null)
            {
                InitialToolPosition = ToolTransform.anchoredPosition;
                InitialToolScale = ToolTransform.localScale;
                InitialSiblingIndex = ToolTransform.GetSiblingIndex();
            }
        }
    }
}

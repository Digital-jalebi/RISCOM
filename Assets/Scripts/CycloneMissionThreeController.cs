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
    private const float BusMoveSpeed = 520f;

    [SerializeField] private GameObject alertScreen;
    [SerializeField] private Button alertNextButton;
    [SerializeField] private GameObject playRoot;
    [SerializeField] private GameObject completeScreen;
    [SerializeField] private Slider timeRemainingSlider;
    [SerializeField] private TextMeshProUGUI timeRemainingLabel;
    [SerializeField] private RectTransform busCapacity200Tool;
    [SerializeField] private Image busCapacity200ToolImage;
    [SerializeField] private RectTransform busCapacity100Tool;
    [SerializeField] private Image busCapacity100ToolImage;
    [SerializeField] private RectTransform missionBus;
    [SerializeField] private RectTransform[] villages;
    [SerializeField] private TextMeshProUGUI[] villageCountLabels;
    [SerializeField] private int[] villageInitialCounts;
    [SerializeField] private RectTransform[] shelters;
    [SerializeField] private TextMeshProUGUI[] shelterCapacityLabels;
    [SerializeField] private int[] shelterCapacities;
    [SerializeField] private RectTransform[] routePoints;

    private readonly MissionThreeBusTool[] busTools = new MissionThreeBusTool[BusToolCount];
    private readonly List<Vector3> pathBuffer = new List<Vector3>();

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

    public void Configure()
    {
        if (!buttonsWired)
        {
            if (alertNextButton != null)
            {
                alertNextButton.onClick.AddListener(StartMissionGame);
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
            missionBus.gameObject.SetActive(false);
        }

        villageRemainingCounts = CopyOrCreateCounts(villageInitialCounts, villages);
        shelterOccupiedCounts = CreateZeroCounts(shelters);

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

    private void StartMissionGame()
    {
        SetActive(alertScreen, false);
        SetActive(completeScreen, false);
        SetActive(playRoot, true);
        BeginMission();
    }

    private void BeginMission()
    {
        Configure();

        timeRemaining = MissionDurationSeconds;
        isRunning = true;
        isComplete = false;
        inputLocked = false;
        draggedBusTool = null;

        StopMissionRoutines();

        villageRemainingCounts = CopyOrCreateCounts(villageInitialCounts, villages);
        shelterOccupiedCounts = CreateZeroCounts(shelters);

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

        int villageIndex = FindVillageAt(screenPosition);
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

    private bool CanEvacuateFromVillage(int villageIndex)
    {
        return villageRemainingCounts != null &&
               villageIndex >= 0 &&
               villageIndex < villageRemainingCounts.Length &&
               villageRemainingCounts[villageIndex] > 0 &&
               GetTotalShelterRemainingCapacity() > 0;
    }

    private void StartEvacuationTrip(MissionThreeBusTool tool, int villageIndex)
    {
        int passengerCount = Mathf.Min(
            Mathf.Min(tool.Capacity, villageRemainingCounts[villageIndex]),
            GetTotalShelterRemainingCapacity());

        if (passengerCount <= 0)
        {
            ResetBusToolWithShake(tool);
            return;
        }

        inputLocked = true;
        SetToolsInteractable(false);
        ResetBusTool(tool, true);

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

        missionBus.position = village.position;
        missionBus.gameObject.SetActive(true);

        villageRemainingCounts[villageIndex] = Mathf.Max(0, villageRemainingCounts[villageIndex] - passengerCount);
        UpdateVillageLabels();

        int passengersRemaining = passengerCount;
        while (passengersRemaining > 0)
        {
            int shelterIndex = FindNearestAvailableShelter(missionBus.position);
            if (shelterIndex < 0)
            {
                break;
            }

            RectTransform shelter = shelters[shelterIndex];
            yield return MoveBusAlongRoute(missionBus.position, shelter.position);

            int shelterRemaining = GetShelterRemainingCapacity(shelterIndex);
            int transferred = Mathf.Min(passengersRemaining, shelterRemaining);
            shelterOccupiedCounts[shelterIndex] += transferred;
            passengersRemaining -= transferred;
            UpdateShelterLabels();
        }

        missionBus.gameObject.SetActive(false);
        FinishTrip();
    }

    private IEnumerator MoveBusAlongRoute(Vector3 from, Vector3 to)
    {
        BuildRoute(from, to);

        Vector3 current = from;
        for (int i = 0; i < pathBuffer.Count; i++)
        {
            Vector3 target = pathBuffer[i];
            while (Vector3.Distance(current, target) > 0.5f)
            {
                current = Vector3.MoveTowards(current, target, BusMoveSpeed * Time.deltaTime);
                missionBus.position = current;
                yield return null;
            }

            current = target;
            missionBus.position = current;
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

    private int FindNearestAvailableShelter(Vector3 from)
    {
        if (shelters == null)
        {
            return -1;
        }

        int bestIndex = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < shelters.Length; i++)
        {
            RectTransform shelter = shelters[i];
            if (shelter == null || GetShelterRemainingCapacity(i) <= 0)
            {
                continue;
            }

            float distance = CalculateRouteDistance(from, shelter.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private int FindClosestRoutePointIndex(Vector3 from)
    {
        if (routePoints == null || routePoints.Length == 0)
        {
            return -1;
        }

        int bestIndex = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < routePoints.Length; i++)
        {
            RectTransform point = routePoints[i];
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

    private float CalculateRouteDistance(Vector3 from, Vector3 to)
    {
        BuildRoute(from, to);

        float distance = 0f;
        Vector3 current = from;
        for (int i = 0; i < pathBuffer.Count; i++)
        {
            Vector3 target = pathBuffer[i];
            distance += Vector3.Distance(current, target);
            current = target;
        }

        return distance;
    }

    private void BuildRoute(Vector3 from, Vector3 to)
    {
        pathBuffer.Clear();

        int routePointCount = routePoints != null ? routePoints.Length : 0;
        if (routePointCount == 0)
        {
            pathBuffer.Add(to);
            return;
        }

        int fromIndex = FindClosestRoutePointIndex(from);
        int toIndex = FindClosestRoutePointIndex(to);
        if (fromIndex < 0 || toIndex < 0)
        {
            pathBuffer.Add(to);
            return;
        }

        int direction = fromIndex <= toIndex ? 1 : -1;
        for (int i = fromIndex; i != toIndex + direction; i += direction)
        {
            RectTransform point = routePoints[i];
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
                label.text = shelterOccupiedCounts[i] >= shelterCapacities[i]
                    ? "Max"
                    : shelterCapacities[i].ToString();
            }
        }
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

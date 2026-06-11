using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class RISCOMMenuController : MonoBehaviour
{
    [SerializeField] private GameObject startMenuScreen;
    [SerializeField] private GameObject hazardSelectionScreen;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private string hazardButtonsContainerName = "HazardButtons";
    [SerializeField] private string highlightChildName = "Highlight";
    [SerializeField] private string[] hazardNames = { "Cyclone", "Flood", "Drought", "Industrial" };

    private readonly List<HazardOption> hazardOptions = new List<HazardOption>();
    private readonly List<GameObject> tourScreens = new List<GameObject>();

    public string SelectedHazard { get; private set; }

    private void Awake()
    {
        CacheReferences();
        WireButtons();
        ShowStartMenu();
    }

    public void ShowStartMenu()
    {
        SetActive(startMenuScreen, true);
        SetActive(hazardSelectionScreen, false);
        HideTourScreens();
        ClearHazardSelection();
    }

    public void ShowHazardSelection()
    {
        SetActive(startMenuScreen, false);
        SetActive(hazardSelectionScreen, true);
        HideTourScreens();
        ClearHazardSelection();
    }

    public void SelectHazard(string hazardName)
    {
        SelectedHazard = hazardName;

        foreach (HazardOption option in hazardOptions)
        {
            bool isSelected = string.Equals(option.Name, hazardName, StringComparison.OrdinalIgnoreCase);
            SetActive(option.Highlight, isSelected);
        }

        if (nextButton != null)
        {
            nextButton.interactable = true;
        }
    }

    public void StartSelectedHazardTour()
    {
        if (string.IsNullOrWhiteSpace(SelectedHazard))
        {
            Debug.LogWarning("Select a hazard before continuing.");
            return;
        }

        GameObject tourScreen = FindTourScreenFor(SelectedHazard);
        if (tourScreen == null)
        {
            Debug.LogWarning($"No tour screen has been configured yet for {SelectedHazard}.");
            return;
        }

        SetActive(startMenuScreen, false);
        SetActive(hazardSelectionScreen, false);
        HideTourScreens();
        tourScreen.SetActive(true);
        Debug.Log($"Starting {SelectedHazard} tour.");
    }

    private void CacheReferences()
    {
        startMenuScreen = startMenuScreen != null ? startMenuScreen : FindChildGameObject(transform, "StartMenu");
        hazardSelectionScreen = hazardSelectionScreen != null ? hazardSelectionScreen : FindChildGameObject(transform, "SelectHazardPanel");

        if (startGameButton == null && startMenuScreen != null)
        {
            startGameButton = startMenuScreen.GetComponentInChildren<Button>(true);
        }

        if (nextButton == null && hazardSelectionScreen != null)
        {
            Transform nextTransform = FindChildTransform(hazardSelectionScreen.transform, "NextBtn");
            nextButton = nextTransform != null ? nextTransform.GetComponent<Button>() : null;
        }

        CacheHazardOptions();
        CacheTourScreens();
    }

    private void CacheHazardOptions()
    {
        hazardOptions.Clear();

        Transform hazardContainer = FindChildTransform(transform, hazardButtonsContainerName);
        if (hazardContainer == null)
        {
            Debug.LogWarning($"Could not find hazard button container named {hazardButtonsContainerName}.");
            return;
        }

        foreach (string hazardName in hazardNames)
        {
            Transform hazardTransform = FindDirectChild(hazardContainer, hazardName);
            if (hazardTransform == null)
            {
                Debug.LogWarning($"Could not find hazard button named {hazardName}.");
                continue;
            }

            Button hazardButton = hazardTransform.GetComponent<Button>();
            if (hazardButton == null)
            {
                Debug.LogWarning($"{hazardName} is missing a Button component.");
                continue;
            }

            Transform highlightTransform = FindDirectChild(hazardTransform, highlightChildName);
            GameObject highlight = highlightTransform != null ? highlightTransform.gameObject : null;

            DisableChildRaycasts(hazardTransform, hazardButton);
            hazardOptions.Add(new HazardOption(hazardName, hazardButton, highlight));
        }
    }

    private void CacheTourScreens()
    {
        tourScreens.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child == startMenuScreen || child == hazardSelectionScreen)
            {
                continue;
            }

            if (child.name.EndsWith("Summary", StringComparison.OrdinalIgnoreCase) ||
                child.name.EndsWith("Tour", StringComparison.OrdinalIgnoreCase))
            {
                tourScreens.Add(child);
            }
        }
    }

    private void WireButtons()
    {
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(ShowHazardSelection);
        }

        foreach (HazardOption option in hazardOptions)
        {
            HazardOption capturedOption = option;
            capturedOption.Button.onClick.AddListener(() => SelectHazard(capturedOption.Name));
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(StartSelectedHazardTour);
        }
    }

    private void ClearHazardSelection()
    {
        SelectedHazard = string.Empty;

        foreach (HazardOption option in hazardOptions)
        {
            SetActive(option.Highlight, false);
        }

        if (nextButton != null)
        {
            nextButton.interactable = false;
        }
    }

    private void HideTourScreens()
    {
        foreach (GameObject tourScreen in tourScreens)
        {
            SetActive(tourScreen, false);
        }
    }

    private GameObject FindTourScreenFor(string hazardName)
    {
        foreach (GameObject tourScreen in tourScreens)
        {
            if (tourScreen.name.StartsWith(hazardName, StringComparison.OrdinalIgnoreCase))
            {
                return tourScreen;
            }
        }

        return null;
    }

    private static void DisableChildRaycasts(Transform hazardTransform, Button parentButton)
    {
        foreach (Button childButton in hazardTransform.GetComponentsInChildren<Button>(true))
        {
            if (childButton != parentButton)
            {
                childButton.enabled = false;
            }
        }

        foreach (Graphic graphic in hazardTransform.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.gameObject != parentButton.gameObject)
            {
                graphic.raycastTarget = false;
            }
        }
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private static GameObject FindChildGameObject(Transform root, string childName)
    {
        Transform child = FindChildTransform(root, childName);
        return child != null ? child.gameObject : null;
    }

    private static Transform FindChildTransform(Transform root, string childName)
    {
        if (string.Equals(root.name, childName, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChildTransform(root.GetChild(i), childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private sealed class HazardOption
    {
        public HazardOption(string name, Button button, GameObject highlight)
        {
            Name = name;
            Button = button;
            Highlight = highlight;
        }

        public string Name { get; }
        public Button Button { get; }
        public GameObject Highlight { get; }
    }
}

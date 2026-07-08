using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class RISCOMMenuController : MonoBehaviour
{
    [SerializeField] private GameObject languageSelectionScreen;
    [SerializeField] private GameObject startMenuScreen;
    [SerializeField] private GameObject hazardSelectionScreen;
    [SerializeField] private Button englishLanguageButton;
    [SerializeField] private Button gujaratiLanguageButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private RISCOMLanguageToggleController languageController;
    [SerializeField] private RISCOMCycloneFlowController cycloneFlowController;
    [SerializeField] private RISCOMFloodFlowController floodFlowController;
    [SerializeField] private RISCOMDroughtFlowController droughtFlowController;
    [SerializeField] private RISCOMIndustrialFlowController industrialFlowController;
    [SerializeField] private string hazardButtonsContainerName = "HazardButtons";
    [SerializeField] private string highlightChildName = "Highlight";
    [SerializeField] private string[] hazardNames = { "Cyclone", "Flood", "Drought", "Industrial" };

    private readonly List<HazardOption> hazardOptions = new List<HazardOption>();
    private readonly List<GameObject> hazardFlows = new List<GameObject>();

    public string SelectedHazard { get; private set; }

    private void Update()
    {
        if (WasResetShortcutPressed())
        {
            ShowLanguageSelection();
        }
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureDisasterFlowCompletionCallbacks();
        WireButtons();

        if (languageSelectionScreen != null)
        {
            ShowLanguageSelection();
        }
        else
        {
            ShowStartMenu();
        }
    }

    public void ShowLanguageSelection()
    {
        SetActive(languageSelectionScreen, true);
        SetActive(startMenuScreen, false);
        SetActive(hazardSelectionScreen, false);
        ResetAllHazardFlows();
        ClearHazardSelection();
    }

    public void ShowStartMenu()
    {
        SetActive(languageSelectionScreen, false);
        SetActive(startMenuScreen, true);
        SetActive(hazardSelectionScreen, false);
        ResetAllHazardFlows();
        ClearHazardSelection();
    }

    public void ShowHazardSelection()
    {
        SetActive(languageSelectionScreen, false);
        SetActive(startMenuScreen, false);
        SetActive(hazardSelectionScreen, true);
        ResetAllHazardFlows();
        ClearHazardSelection();
    }

    public void SelectEnglishLanguage()
    {
        SelectLanguage(false);
    }

    public void SelectGujaratiLanguage()
    {
        SelectLanguage(true);
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

        StartHazardTour(SelectedHazard);
    }

    private void ConfigureDisasterFlowCompletionCallbacks()
    {
        cycloneFlowController?.Configure(HandleCycloneFlowCompleted);
        floodFlowController?.Configure(HandleFloodFlowCompleted);
        droughtFlowController?.Configure(HandleDroughtFlowCompleted);
        industrialFlowController?.Configure(HandleIndustrialFlowCompleted);
    }

    private void HandleCycloneFlowCompleted()
    {
        StartHazardTour("Flood");
    }

    private void HandleFloodFlowCompleted()
    {
        StartHazardTour("Drought");
    }

    private void HandleDroughtFlowCompleted()
    {
        StartHazardTour("Industrial");
    }

    private void HandleIndustrialFlowCompleted()
    {
        ShowHazardSelection();
    }

    private void StartHazardTour(string hazardName)
    {
        if (string.IsNullOrWhiteSpace(hazardName))
        {
            Debug.LogWarning("No hazard was provided to start.");
            return;
        }

        SelectedHazard = hazardName;

        SetActive(languageSelectionScreen, false);
        SetActive(startMenuScreen, false);
        SetActive(hazardSelectionScreen, false);
        ResetAllHazardFlows();

        if (string.Equals(hazardName, "Cyclone", StringComparison.OrdinalIgnoreCase))
        {
            if (cycloneFlowController == null)
            {
                Debug.LogWarning("Cyclone flow controller is not assigned.");
                return;
            }

            cycloneFlowController.BeginFlow();
            Debug.Log("Starting Cyclone tour.");
            return;
        }

        if (string.Equals(hazardName, "Flood", StringComparison.OrdinalIgnoreCase))
        {
            if (floodFlowController == null)
            {
                Debug.LogWarning("Flood flow controller is not assigned.");
                return;
            }

            floodFlowController.BeginFlow();
            Debug.Log("Starting Flood tour.");
            return;
        }

        if (string.Equals(hazardName, "Drought", StringComparison.OrdinalIgnoreCase))
        {
            if (droughtFlowController == null)
            {
                Debug.LogWarning("Drought flow controller is not assigned.");
                return;
            }

            droughtFlowController.BeginFlow();
            Debug.Log("Starting Drought tour.");
            return;
        }

        if (string.Equals(hazardName, "Industrial", StringComparison.OrdinalIgnoreCase))
        {
            if (industrialFlowController == null)
            {
                Debug.LogWarning("Industrial flow controller is not assigned.");
                return;
            }

            industrialFlowController.BeginFlow();
            Debug.Log("Starting Industrial tour.");
            return;
        }

        GameObject hazardFlow = FindHazardFlowFor(hazardName);
        if (hazardFlow == null)
        {
            Debug.LogWarning($"No game flow has been configured yet for {hazardName}.");
            return;
        }

        hazardFlow.SetActive(true);
        Debug.Log($"Starting {hazardName} tour.");
    }

    private void CacheReferences()
    {
        languageSelectionScreen = languageSelectionScreen != null ? languageSelectionScreen : FindChildGameObject(transform, "LanguageSelectionScreen");
        startMenuScreen = startMenuScreen != null ? startMenuScreen : FindChildGameObject(transform, "StartMenu");
        hazardSelectionScreen = hazardSelectionScreen != null ? hazardSelectionScreen : FindChildGameObject(transform, "SelectHazardPanel");

        if (englishLanguageButton == null && languageSelectionScreen != null)
        {
            Transform englishTransform = FindChildTransform(languageSelectionScreen.transform, "English");
            englishLanguageButton = englishTransform != null ? englishTransform.GetComponent<Button>() : null;
        }

        if (gujaratiLanguageButton == null && languageSelectionScreen != null)
        {
            Transform gujaratiTransform = FindChildTransform(languageSelectionScreen.transform, "Gujarati");
            gujaratiLanguageButton = gujaratiTransform != null ? gujaratiTransform.GetComponent<Button>() : null;
        }

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
        CacheHazardFlows();
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

    private void CacheHazardFlows()
    {
        hazardFlows.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child == languageSelectionScreen || child == startMenuScreen || child == hazardSelectionScreen)
            {
                continue;
            }

            if (child.name.EndsWith("GameFlow", StringComparison.OrdinalIgnoreCase) ||
                child.name.EndsWith("Summary", StringComparison.OrdinalIgnoreCase) ||
                child.name.EndsWith("Tour", StringComparison.OrdinalIgnoreCase))
            {
                hazardFlows.Add(child);
            }
        }
    }

    private void WireButtons()
    {
        if (englishLanguageButton != null)
        {
            englishLanguageButton.onClick.AddListener(SelectEnglishLanguage);
        }

        if (gujaratiLanguageButton != null)
        {
            gujaratiLanguageButton.onClick.AddListener(SelectGujaratiLanguage);
        }

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

    private void SelectLanguage(bool useGujarati)
    {
        if (languageController != null)
        {
            languageController.SetGujaratiEnabled(useGujarati);
        }
        else
        {
            Debug.LogWarning("Language controller is not assigned.");
        }

        ShowStartMenu();
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

    private void HideHazardFlows()
    {
        foreach (GameObject hazardFlow in hazardFlows)
        {
            SetActive(hazardFlow, false);
        }
    }

    private void ResetAllHazardFlows()
    {
        cycloneFlowController?.ResetFlow();
        floodFlowController?.ResetFlow();
        droughtFlowController?.ResetFlow();
        industrialFlowController?.ResetFlow();
        HideHazardFlows();
    }

    private GameObject FindHazardFlowFor(string hazardName)
    {
        foreach (GameObject hazardFlow in hazardFlows)
        {
            if (hazardFlow.name.StartsWith(hazardName, StringComparison.OrdinalIgnoreCase))
            {
                return hazardFlow;
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

    private static bool WasResetShortcutPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        bool ctrlPressed = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        return ctrlPressed && keyboard.rKey.wasPressedThisFrame;
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

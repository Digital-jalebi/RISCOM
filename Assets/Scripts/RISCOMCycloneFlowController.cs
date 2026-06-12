using UnityEngine;
using UnityEngine.UI;

public sealed class RISCOMCycloneFlowController : MonoBehaviour
{
    [SerializeField] private GameObject cycloneSummary;
    [SerializeField] private GameObject summaryBackground;
    [SerializeField] private GameObject instructorImage;
    [SerializeField] private GameObject missionOneGame;
    [SerializeField] private GameObject missionCompleteScreen;
    [SerializeField] private GameObject[] directFlowChildren;
    [SerializeField] private GameObject[] summaryScreens;
    [SerializeField] private Button[] summaryNextButtons;
    [SerializeField] private CycloneMissionController missionOneController;

    private bool buttonsWired;
    private int currentSummaryIndex;

    public void BeginFlow()
    {
        WireSummaryButtons();
        PrepareMissionOne();

        gameObject.SetActive(true);
        HideDirectFlowChildren();

        SetActive(cycloneSummary, true);
        SetActive(summaryBackground, true);
        SetActive(instructorImage, true);
        ShowSummaryScreen(0);
    }

    private void WireSummaryButtons()
    {
        if (buttonsWired)
        {
            return;
        }

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

        buttonsWired = true;
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

    private void StartMissionOne()
    {
        SetActive(cycloneSummary, false);
        SetActive(summaryBackground, false);
        SetActive(instructorImage, false);
        SetActive(missionCompleteScreen, false);
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
        SetActive(missionCompleteScreen, true);
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
}

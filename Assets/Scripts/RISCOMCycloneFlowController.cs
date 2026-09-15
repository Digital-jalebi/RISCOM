using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class RISCOMCycloneFlowController : MonoBehaviour
{
    [SerializeField] private GameObject cycloneSummary;
    [SerializeField] private GameObject summaryBackground;
    [SerializeField] private GameObject instructorImage;
    [SerializeField] private GameObject missionOneGame;
    [SerializeField] private GameObject missionOneGameplayLayout;
    [SerializeField] private GameObject missionCompleteScreen;
    [SerializeField] private Button missionOneReportNextButton;
    [SerializeField] private GameObject[] directFlowChildren;
    [SerializeField] private GameObject[] summaryScreens;
    [SerializeField] private Button[] summaryNextButtons;
    [SerializeField] private CycloneMissionController missionOneController;
    [SerializeField] private CycloneMissionTwoController missionTwoController;
    [SerializeField] private CycloneMissionThreeController missionThreeController;
    [SerializeField] private CycloneMissionFourController missionFourController;

    private bool buttonsWired;
    private int currentSummaryIndex;
    private Action onFlowCompleted;

    public void Configure(Action flowCompletedHandler = null)
    {
        if (flowCompletedHandler != null)
        {
            onFlowCompleted = flowCompletedHandler;
        }

        WireButtons();
    }

    public void BeginFlow()
    {
        WireButtons();
        PrepareMissionOne();
        PrepareMissionTwo();
        PrepareMissionThree();
        PrepareMissionFour();

        gameObject.SetActive(true);
        ResetFlowState(false);
        HideDirectFlowChildren();

        SetActive(cycloneSummary, true);
        SetActive(summaryBackground, true);
        SetActive(instructorImage, true);
        ShowSummaryScreen(0);
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

    private void PrepareMissionTwo()
    {
        if (missionTwoController == null)
        {
            Debug.LogWarning("Cyclone flow is missing its Mission 2 controller reference.");
            return;
        }

        missionTwoController.Configure(StartMissionThreeIntro);
    }

    private void PrepareMissionThree()
    {
        if (missionThreeController == null)
        {
            Debug.LogWarning("Cyclone flow is missing its Mission 3 controller reference.");
            return;
        }

        missionThreeController.Configure(StartMissionFourIntro);
    }

    private void PrepareMissionFour()
    {
        if (missionFourController == null)
        {
            Debug.LogWarning("Cyclone flow is missing its Mission 4 controller reference.");
            return;
        }

        missionFourController.Configure(HandleMissionFourReportNext);
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
        missionTwoController?.Hide();
        missionThreeController?.Hide();
        missionFourController?.Hide();

        SetActive(cycloneSummary, false);
        SetActive(summaryBackground, false);
        SetActive(instructorImage, false);
        SetActive(missionCompleteScreen, false);
        SetActive(missionOneGame, true);
        SetActive(missionOneGameplayLayout, true);

        if (missionOneController != null)
        {
            missionOneController.BeginMission(CompleteMissionOne);
        }
    }

    private void CompleteMissionOne()
    {
        SetActive(missionOneGame, true);
        SetActive(missionOneGameplayLayout, false);
        SetActive(instructorImage, false);
        missionTwoController?.Hide();
        missionThreeController?.Hide();
        missionFourController?.Hide();
        SetActive(missionCompleteScreen, true);
    }

    private void StartMissionTwoIntro()
    {
        PrepareMissionTwo();
        missionThreeController?.Hide();
        missionFourController?.Hide();

        SetActive(missionCompleteScreen, false);
        SetActive(missionOneGame, false);
        SetActive(cycloneSummary, true);
        SetActive(summaryBackground, false);
        SetActive(instructorImage, false);
        HideSummaryScreens();

        missionTwoController?.ShowIntro();
    }

    private void StartMissionThreeIntro()
    {
        PrepareMissionThree();
        missionTwoController?.Hide();
        missionFourController?.Hide();

        SetActive(cycloneSummary, true);
        SetActive(summaryBackground, false);
        SetActive(instructorImage, false);
        HideSummaryScreens();

        missionThreeController?.ShowIntro();
    }

    private void StartMissionFourIntro()
    {
        PrepareMissionFour();
        missionTwoController?.Hide();
        missionThreeController?.Hide();

        SetActive(cycloneSummary, true);
        SetActive(summaryBackground, false);
        SetActive(instructorImage, false);
        HideSummaryScreens();

        missionFourController?.ShowIntro();
    }

    private void HandleMissionFourReportNext()
    {
        ResetFlowState(true);
        onFlowCompleted?.Invoke();
    }

    public void ResetFlow()
    {
        ResetFlowState(true);
    }

    private void ResetFlowState(bool hideRoot)
    {
        missionTwoController?.Hide();
        missionThreeController?.Hide();
        missionFourController?.Hide();

        SetActive(cycloneSummary, false);
        SetActive(summaryBackground, false);
        SetActive(instructorImage, false);
        SetActive(missionOneGame, false);
        SetActive(missionOneGameplayLayout, false);
        SetActive(missionCompleteScreen, false);
        HideSummaryScreens();
        HideDirectFlowChildren();

        currentSummaryIndex = 0;

        if (hideRoot)
        {
            gameObject.SetActive(false);
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
}

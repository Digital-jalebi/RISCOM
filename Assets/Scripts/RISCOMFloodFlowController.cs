using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class RISCOMFloodFlowController : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("cycloneSummary")] private GameObject floodSummary;
    [SerializeField] private GameObject summaryBackground;
    [SerializeField] private GameObject instructorImage;
    [SerializeField] private GameObject missionOneGame;
    [SerializeField] private GameObject missionOneGameplayLayout;
    [SerializeField] private FloodMissionOneController missionOneController;
    [SerializeField] private Button missionOneReportNextButton;
    [SerializeField] private FloodMissionTwoController missionTwoController;
    [SerializeField] private GameObject missionThreeGame;
    [SerializeField] private GameObject missionThreeIntroScreen;
    [SerializeField] private GameObject missionThreeGameplayScreen;
    [SerializeField] private GameObject missionThreeCompleteScreen;
    [SerializeField] private FloodMissionThreeController missionThreeController;
    [SerializeField] private GameObject missionFourGame;
    [SerializeField] private GameObject missionFourIntroScreen;
    [SerializeField] private GameObject missionFourGameplayScreen;
    [SerializeField] private GameObject missionFourCompleteScreen;
    [SerializeField] private FloodMissionFourController missionFourController;
    [SerializeField] private GameObject[] directFlowChildren;
    [SerializeField] private GameObject[] summaryScreens;
    [SerializeField] private Button[] summaryNextButtons;

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

        gameObject.SetActive(true);
        ResetFlowState(false);
        HideDirectFlowChildren();

        SetActive(floodSummary, true);
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

        if (summaryScreens == null || summaryNextButtons == null)
        {
            Debug.LogWarning("Flood flow summary screens or next buttons are not assigned.");
            buttonsWired = true;
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

        if (missionOneReportNextButton != null)
        {
            missionOneReportNextButton.onClick.AddListener(StartMissionTwoIntro);
        }

        if (missionTwoController != null)
        {
            missionTwoController.Configure(StartMissionThreeIntro);
        }

        if (missionThreeController != null)
        {
            missionThreeController.Configure(StartMissionFourIntro);
        }

        if (missionFourController != null)
        {
            missionFourController.Configure(HandleMissionFourReportNext);
        }

        buttonsWired = true;
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

    private void StartMissionOne()
    {
        SetActive(floodSummary, false);
        SetActive(summaryBackground, false);
        SetActive(instructorImage, false);
        SetActive(missionOneGame, true);
        SetActive(missionOneGameplayLayout, true);

        if (missionOneController != null)
        {
            missionOneController.BeginMission();
        }
    }

    private void StartMissionTwoIntro()
    {
        SetActive(missionOneGame, false);

        if (missionTwoController != null)
        {
            missionTwoController.ShowIntro();
            return;
        }

        Debug.LogWarning("Flood flow is missing its Mission 2 controller reference.");
    }

    private void StartMissionThreeIntro()
    {
        if (missionTwoController != null)
        {
            missionTwoController.Hide();
        }

        if (missionThreeController != null)
        {
            missionThreeController.ShowIntro();
            return;
        }

        SetActive(missionThreeGame, true);
        SetActive(missionThreeIntroScreen, true);
        SetActive(missionThreeGameplayScreen, false);
        SetActive(missionThreeCompleteScreen, false);
    }

    private void StartMissionFourIntro()
    {
        if (missionThreeController != null)
        {
            missionThreeController.Hide();
        }

        if (missionFourController != null)
        {
            missionFourController.ShowIntro();
            return;
        }

        SetActive(missionFourGame, true);
        SetActive(missionFourIntroScreen, true);
        SetActive(missionFourGameplayScreen, false);
        SetActive(missionFourCompleteScreen, false);
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

        SetActive(floodSummary, false);
        SetActive(summaryBackground, false);
        SetActive(instructorImage, false);
        SetActive(missionOneGame, false);
        SetActive(missionOneGameplayLayout, false);
        SetActive(missionThreeGame, false);
        SetActive(missionThreeIntroScreen, false);
        SetActive(missionThreeGameplayScreen, false);
        SetActive(missionThreeCompleteScreen, false);
        SetActive(missionFourGame, false);
        SetActive(missionFourIntroScreen, false);
        SetActive(missionFourGameplayScreen, false);
        SetActive(missionFourCompleteScreen, false);
        HideSummaryScreens();
        HideDirectFlowChildren();

        currentSummaryIndex = 0;

        if (hideRoot)
        {
            gameObject.SetActive(false);
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

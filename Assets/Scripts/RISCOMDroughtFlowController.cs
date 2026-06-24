using UnityEngine;
using UnityEngine.UI;

public sealed class RISCOMDroughtFlowController : MonoBehaviour
{
    [SerializeField] private GameObject droughtSummary;
    [SerializeField] private GameObject summaryBackground;
    [SerializeField] private GameObject instructorImage;
    [SerializeField] private GameObject missionOneGame;
    [SerializeField] private GameObject missionOneGameplayRoot;
    [SerializeField] private DroughtMissionOneController missionOneController;
    [SerializeField] private DroughtMissionTwoController missionTwoController;
    [SerializeField] private DroughtMissionThreeController missionThreeController;
    [SerializeField] private DroughtMissionFourController missionFourController;
    [SerializeField] private GameObject[] directFlowChildren;
    [SerializeField] private GameObject[] summaryScreens;
    [SerializeField] private Button[] summaryNextButtons;

    private bool buttonsWired;
    private int currentSummaryIndex;

    public void BeginFlow()
    {
        WireButtons();

        gameObject.SetActive(true);
        HideDirectFlowChildren();

        SetActive(droughtSummary, true);
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

        int buttonCount = Mathf.Min(GetSummaryScreenCount(), summaryNextButtons != null ? summaryNextButtons.Length : 0);
        for (int i = 0; i < buttonCount; i++)
        {
            int screenIndex = i;
            Button nextButton = summaryNextButtons[i];
            if (nextButton != null)
            {
                nextButton.onClick.AddListener(() => HandleSummaryNext(screenIndex));
            }
        }

        if (missionOneController != null)
        {
            missionOneController.Configure(HandleMissionOneReportNext);
        }

        if (missionTwoController != null)
        {
            missionTwoController.Configure(HandleMissionTwoReportNext);
        }

        if (missionThreeController != null)
        {
            missionThreeController.Configure(HandleMissionThreeReportNext);
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

        int screenCount = GetSummaryScreenCount();
        if (currentSummaryIndex < screenCount - 1)
        {
            ShowSummaryScreen(currentSummaryIndex + 1);
            return;
        }

        StartMissionOne();
    }

    private void ShowSummaryScreen(int index)
    {
        int screenCount = GetSummaryScreenCount();
        if (screenCount == 0)
        {
            return;
        }

        currentSummaryIndex = Mathf.Clamp(index, 0, screenCount - 1);

        for (int i = 0; i < summaryScreens.Length; i++)
        {
            SetActive(summaryScreens[i], i == currentSummaryIndex);
        }
    }

    private int GetSummaryScreenCount()
    {
        if (summaryScreens == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < summaryScreens.Length; i++)
        {
            if (summaryScreens[i] != null)
            {
                count++;
            }
        }

        return count;
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
        SetActive(droughtSummary, false);
        SetActive(summaryBackground, false);
        SetActive(instructorImage, false);
        SetActive(missionOneGame, true);
        SetActive(missionOneGameplayRoot, true);

        if (missionOneController != null)
        {
            missionOneController.BeginMission();
        }
    }

    private void HandleMissionOneReportNext()
    {
        SetActive(missionOneGame, false);

        if (missionTwoController != null)
        {
            missionTwoController.ShowIntro();
            return;
        }

        Debug.LogWarning("Drought flow is missing its Mission 2 controller reference.");
    }

    private void HandleMissionTwoReportNext()
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

        Debug.LogWarning("Drought flow is missing its Mission 3 controller reference.");
    }

    private void HandleMissionThreeReportNext()
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

        Debug.LogWarning("Drought flow is missing its Mission 4 controller reference.");
    }

    private void HandleMissionFourReportNext()
    {
        Debug.Log("Drought Mission 4 completed. No next drought screen is configured yet.");
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }
}

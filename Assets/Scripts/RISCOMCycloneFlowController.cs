using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class RISCOMCycloneFlowController : MonoBehaviour
{
    private static readonly string[] SummaryScreenNames =
    {
        "ResponseChallengeWindow",
        "ResponseChallengeWindow 2",
        "SituationScreen",
        "Intelligence Map",
        "CycloneTrackingScreen"
    };

    private readonly List<GameObject> summaryScreens = new List<GameObject>();

    private GameObject cycloneSummary;
    private GameObject summaryBackground;
    private GameObject missionOneGame;
    private GameObject missionCompleteScreen;
    private CycloneMissionController missionOneController;
    private bool isConfigured;
    private bool buttonsWired;
    private int currentSummaryIndex;

    public void BeginFlow()
    {
        Configure();

        gameObject.SetActive(true);
        HideDirectFlowChildren();

        SetActive(cycloneSummary, true);
        SetActive(summaryBackground, true);
        ShowSummaryScreen(0);
    }

    private void Configure()
    {
        if (isConfigured)
        {
            return;
        }

        cycloneSummary = FindDirectChildGameObject(transform, "CycloneSummary");
        missionOneGame = FindDirectChildGameObject(transform, "Mission 1 Game");
        missionCompleteScreen = FindDirectChildGameObject(transform, "Mission Complete Screen") ??
                                FindDirectChildGameObject(transform, "Mission Report Screen");

        Transform summaryRoot = cycloneSummary != null ? cycloneSummary.transform : null;
        summaryBackground = summaryRoot != null ? FindDirectChildGameObject(summaryRoot, "BG") : null;

        CacheSummaryScreens();
        WireSummaryButtons();
        PrepareMissionOne();

        isConfigured = true;
    }

    private void CacheSummaryScreens()
    {
        summaryScreens.Clear();

        if (summaryBackground == null)
        {
            Debug.LogWarning("Cyclone summary BG was not found.");
            return;
        }

        foreach (string screenName in SummaryScreenNames)
        {
            GameObject screen = FindDirectChildGameObject(summaryBackground.transform, screenName);
            if (screen == null)
            {
                Debug.LogWarning($"Cyclone summary screen not found: {screenName}");
                continue;
            }

            summaryScreens.Add(screen);
        }
    }

    private void WireSummaryButtons()
    {
        if (buttonsWired)
        {
            return;
        }

        for (int i = 0; i < summaryScreens.Count; i++)
        {
            int screenIndex = i;
            Button nextButton = FindNextButton(summaryScreens[i]);
            if (nextButton != null)
            {
                nextButton.onClick.AddListener(() => HandleSummaryNext(screenIndex));
            }
        }

        buttonsWired = true;
    }

    private void PrepareMissionOne()
    {
        if (missionOneGame == null)
        {
            Debug.LogWarning("Mission 1 Game was not found under CycloneGameFlow.");
            return;
        }

        missionOneController = missionOneGame.GetComponent<CycloneMissionController>();
        if (missionOneController == null)
        {
            Debug.LogWarning("Mission 1 Game is missing CycloneMissionController.");
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

        if (currentSummaryIndex < summaryScreens.Count - 1)
        {
            ShowSummaryScreen(currentSummaryIndex + 1);
            return;
        }

        StartMissionOne();
    }

    private void ShowSummaryScreen(int index)
    {
        currentSummaryIndex = Mathf.Clamp(index, 0, Mathf.Max(0, summaryScreens.Count - 1));

        for (int i = 0; i < summaryScreens.Count; i++)
        {
            summaryScreens[i].SetActive(i == currentSummaryIndex);
        }
    }

    private void StartMissionOne()
    {
        SetActive(cycloneSummary, false);
        SetActive(summaryBackground, false);
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
        SetActive(missionCompleteScreen, true);
    }

    private void HideDirectFlowChildren()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }
    }

    private static Button FindNextButton(GameObject screen)
    {
        Transform nextTransform = FindChildTransform(screen.transform, "NextBtn") ??
                                  FindChildTransform(screen.transform, "NextBtn (1)");
        return nextTransform != null ? nextTransform.GetComponent<Button>() : screen.GetComponentInChildren<Button>(true);
    }

    private static GameObject FindDirectChildGameObject(Transform parent, string childName)
    {
        Transform child = FindDirectChild(parent, childName);
        return child != null ? child.gameObject : null;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

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

    private static Transform FindChildTransform(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

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
}

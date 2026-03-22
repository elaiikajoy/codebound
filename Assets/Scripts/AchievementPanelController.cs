// ============================================================
// AchievementPanelController.cs
// Purpose: Drives the Achievement panel UI entirely inside Unity.
//          - Pulls per-user achievement state from the backend
//          - Auto-wires row prefab instances from the hierarchy
//          - Enables claim buttons only when the user is eligible
//          - Posts claim requests and refreshes the list after success
//          - Optionally feeds a small debug overlay with lock reasons
//
// Unity Setup:
//   - Attach to the root Achievement panel GameObject.
//   - Put AchievementRowView on each row prefab or row instance.
//   - The controller can auto-discover rows from child objects.
//   - Requires: AchievementService, GameApiManager, ApiClient, TokenManager.
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class AchievementPanelController : MonoBehaviour
{
    [Header("Row Wiring")]
    [Tooltip("If enabled, the controller searches its children for AchievementRowView components.")]
    [SerializeField] private bool autoDiscoverRows = true;

    [Tooltip("Optional explicit row list. Leave empty if you want auto-discovery from the hierarchy.")]
    [SerializeField] private AchievementRowView[] rowViews;

    [Tooltip("Optional parent transform that contains the achievement rows.")]
    [SerializeField] private Transform rowsRoot;

    [Header("Scroll")]
    [Tooltip("Optional ScrollRect that contains the achievement rows. If assigned, the list can snap back to the top after refresh.")]
    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("If enabled, the list scrolls back to the top after data refresh.")]
    [SerializeField] private bool scrollToTopOnRefresh = true;

    [Tooltip("If enabled, the list scrolls to the bottom after data refresh.")]
    [SerializeField] private bool scrollToBottomOnRefresh = false;

    [Header("Optional UI")]
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private GameObject loadingRoot;
    [SerializeField] private TMP_Text emptyStateText;
    [SerializeField] private AchievementDebugOverlay debugOverlay;

    [Header("Behavior")]
    [SerializeField] private bool refreshOnEnable = true;

    private AchievementStateData latestState;
    private bool isRefreshing;

    private void Awake()
    {
        RefreshRowCache();
    }

    private void OnEnable()
    {
        GameApiManager.OnLoginSuccess += HandleSessionChanged;
        GameApiManager.OnSessionRestored += HandleSessionChanged;
        GameApiManager.OnLogout += HandleLogout;

        if (refreshOnEnable)
        {
            StartCoroutine(RefreshAchievements());
        }
    }

    private void OnDisable()
    {
        GameApiManager.OnLoginSuccess -= HandleSessionChanged;
        GameApiManager.OnSessionRestored -= HandleSessionChanged;
        GameApiManager.OnLogout -= HandleLogout;
    }

    /// <summary>
    /// Rebuilds the row cache from the hierarchy when auto-discovery is enabled.
    /// Useful for prefab-based panels and scroll lists.
    /// </summary>
    public void RefreshRowCache()
    {
        if (!autoDiscoverRows)
            return;

        Transform searchRoot = rowsRoot != null ? rowsRoot : transform;
        rowViews = searchRoot.GetComponentsInChildren<AchievementRowView>(true);

        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (scrollRect != null && rowsRoot == null && scrollRect.content != null)
            rowsRoot = scrollRect.content;
    }

    public void RefreshNow()
    {
        StartCoroutine(RefreshAchievements());
    }

    /// <summary>
    /// Returns to the Main scene. Bind this to the Achievement scene back button.
    /// </summary>
    public void BackToMainScene()
    {
        PlayerPrefs.Save();
        SceneManager.LoadSceneAsync("Main");
    }

    private void HandleSessionChanged(UserData _)
    {
        StartCoroutine(RefreshAchievements());
    }

    private void HandleLogout()
    {
        latestState = null;
        SetLoading(false);
        SetEmptyState("Login to view achievements.", true);
        ApplyLockedState("Login to view achievements.");
    }

    private IEnumerator RefreshAchievements()
    {
        if (isRefreshing)
            yield break;

        isRefreshing = true;
        RefreshRowCache();
        SetLoading(true);
        Debug.Log("[AchievementPanelController] RefreshAchievements started.");

        AchievementService service = ResolveAchievementService();

        // If this panel enabled before service Awake, wait one frame and try again.
        if (service == null)
        {
            yield return null;
            service = ResolveAchievementService();
        }

        if (service == null)
        {
            Debug.LogError("[AchievementPanelController] Refresh aborted: AchievementService.Instance is null. Add AchievementService to the persistent GameAPI object.");
            SetLoading(false);
            SetEmptyState("AchievementService is missing from GameAPI.", true);
            if (debugOverlay != null) debugOverlay.SetMessage("AchievementService is missing from GameAPI.");
            isRefreshing = false;
            yield break;
        }

        if (GameApiManager.Instance == null || !GameApiManager.Instance.IsLoggedIn)
        {
            bool hasManager = GameApiManager.Instance != null;
            bool isLoggedIn = hasManager && GameApiManager.Instance.IsLoggedIn;
            Debug.Log($"[AchievementPanelController] Refresh aborted: login state invalid. hasGameApiManager={hasManager}, isLoggedIn={isLoggedIn}");
            SetLoading(false);
            SetEmptyState("Login to view achievements.", true);
            ApplyLockedState("Login to view achievements.");
            Debug.LogWarning("[AchievementPanelController] Refresh aborted: player is not logged in.");
            isRefreshing = false;
            yield break;
        }

        Debug.Log($"[AchievementPanelController] Requesting achievement state for user '{GameApiManager.Instance.CurrentUser?.username ?? "unknown"}'.");

        yield return StartCoroutine(service.GetAchievementState(
            onSuccess: data =>
            {
                latestState = data;
                ApplyState(data);
                Debug.Log($"[AchievementPanelController] Achievement state loaded. Claimable={data.claimableCount}, Unlocked={data.unlockedCount}/{data.total}.");
                SnapScrollAfterRefresh();
                SetLoading(false);
                isRefreshing = false;
            },
            onError: error =>
            {
                Debug.LogWarning($"[AchievementPanelController] {error}");
                SetLoading(false);
                SetEmptyState(error, true);
                if (debugOverlay != null) debugOverlay.SetMessage(error);
                SnapScrollAfterRefresh();
                isRefreshing = false;
            }
        ));
    }

    private void ApplyState(AchievementStateData data)
    {
        if (data == null)
        {
            ApplyLockedState("No achievement data available.");
            SetEmptyState("No achievement data available.", true);
            return;
        }

        SetEmptyState(string.Empty, false);

        if (summaryText != null)
        {
            summaryText.text = $"{data.unlockedCount}/{data.total} unlocked | {data.claimableCount} claimable";
        }

        foreach (AchievementRowView rowView in rowViews)
        {
            if (rowView == null || string.IsNullOrWhiteSpace(rowView.AchievementId))
                continue;

            AchievementStateItem achievement = FindAchievement(data.achievements, rowView.AchievementId);
            string disabledReason = BuildDisabledReason(achievement, data);

            if (rowView.AchievementId == "welcome_gift")
            {
                Debug.Log($"[AchievementPanelController] Row 'welcome_gift' state: found={(achievement != null)}, canClaim={achievement?.canClaim ?? false}, isClaimed={achievement?.isClaimed ?? false}, reason='{disabledReason}'");
            }

            rowView.SetClaimHandler(HandleClaimRequested);
            rowView.ApplyState(achievement, disabledReason);
        }

        if (debugOverlay != null)
        {
            debugOverlay.Render(data, rowViews);
        }
    }

    private void ApplyLockedState(string message)
    {
        foreach (AchievementRowView rowView in rowViews)
        {
            if (rowView == null) continue;
            rowView.SetClaimHandler(HandleClaimRequested);
            rowView.SetLocked(message);
        }

        if (debugOverlay != null)
        {
            debugOverlay.SetMessage(message);
        }
    }

    private AchievementStateItem FindAchievement(AchievementStateItem[] items, string achievementId)
    {
        if (items == null) return null;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].id == achievementId)
                return items[i];
        }

        return null;
    }

    private string BuildDisabledReason(AchievementStateItem achievement, AchievementStateData data)
    {
        if (achievement == null)
            return "Missing in backend";

        if (achievement.isClaimed)
            return "Already claimed";

        if (achievement.canClaim)
            return string.Empty;

        if (achievement.requiredHighestLevel > 0 && data.progress != null && data.progress.highestLevel < achievement.requiredHighestLevel)
            return $"Requires Level {achievement.requiredHighestLevel}";

        if (achievement.requiredTotalTokens > 0 && data.progress != null && data.progress.totalTokens < achievement.requiredTotalTokens)
            return $"Requires {achievement.requiredTotalTokens:N0} Tokens";

        return "Locked";
    }

    private void HandleClaimRequested(string achievementId)
    {
        AchievementService service = ResolveAchievementService();
        if (service == null)
            return;

        Debug.Log($"[AchievementPanelController] Claim requested for '{achievementId}'. Sending POST /achievements/claim...");

        StartCoroutine(service.ClaimAchievement(
            achievementId,
            onSuccess: result =>
            {
                Debug.Log($"[AchievementPanelController] Claim success for '{achievementId}'. Reward={result.rewardTokens}, DB tokens now={result.progress.totalTokens}.");
                TokenManager.SyncFromBackend(result.progress.totalTokens);
                Debug.Log($"[AchievementPanelController] Local token state synced after claim. PlayerTokens={TokenManager.GetTokens()}.");
                StartCoroutine(RefreshAchievements());
            },
            onError: error =>
            {
                Debug.LogWarning($"[AchievementPanelController] Claim failed: {error}");
                if (debugOverlay != null)
                    debugOverlay.SetMessage(error);
            }
        ));
    }

    private void SetLoading(bool isLoading)
    {
        if (loadingRoot != null)
            loadingRoot.SetActive(isLoading);
    }

    private void SetEmptyState(string message, bool visible)
    {
        if (emptyStateText == null) return;

        emptyStateText.gameObject.SetActive(visible);
        emptyStateText.text = message;
    }

    private void SnapScrollAfterRefresh()
    {
        if (scrollRect == null)
            return;

        if (scrollToTopOnRefresh)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            scrollRect.horizontalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            return;
        }

        if (scrollToBottomOnRefresh)
        {
            scrollRect.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
        }
    }

    private AchievementService ResolveAchievementService()
    {
        AchievementService service = AchievementService.GetOrFindInstance();
        if (service == null)
            service = FindObjectOfType<AchievementService>(true);

        return service;
    }
}
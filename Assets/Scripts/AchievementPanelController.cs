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

    [Tooltip("If enabled, buttons named like 'Claim ...' are auto-wired as individual runtime rows.")]
    [SerializeField] private bool autoCreateRowsFromClaimButtons = true;

    [Tooltip("Include inactive claim buttons when auto-wiring rows.")]
    [SerializeField] private bool includeInactiveClaimButtons = true;

    [Header("Scroll")]
    [Tooltip("Optional ScrollRect that contains the achievement rows. If assigned, the list can snap back to the top after refresh.")]
    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("If enabled, the list scrolls back to the top after data refresh.")]
    [SerializeField] private bool scrollToTopOnRefresh = true;

    [Tooltip("If enabled, the list scrolls to the bottom after data refresh.")]
    [SerializeField] private bool scrollToBottomOnRefresh = false;

    [Header("Manual Scroll Fallback")]
    [Tooltip("Used when no ScrollRect exists in the scene. Mouse wheel scrolls this RectTransform vertically.")]
    [SerializeField] private bool enableManualWheelScrollFallback = true;

    [Tooltip("Rows/content RectTransform only. Do not assign the full panel root.")]
    [SerializeField] private RectTransform manualScrollTarget;

    [Tooltip("Optional viewport/mask RectTransform used for dynamic scroll limit calculation.")]
    [SerializeField] private RectTransform manualScrollViewport;

    [Tooltip("Top area reserved for fixed UI (Achievement title/back icon). Rows are not allowed to overlap this zone.")]
    [SerializeField] private float manualViewportTopInset = 100f;

    [Tooltip("Optional bottom area reserved for fixed UI (e.g. footer).")]
    [SerializeField] private float manualViewportBottomInset = 0f;

    [SerializeField] private float manualWheelScrollSpeed = 220f;

    [Tooltip("If enabled, wheel direction is inverted for projects where input feels reversed.")]
    [SerializeField] private bool manualInvertWheel = true;

    [Tooltip("If enabled, min/max are calculated from content and viewport heights.")]
    [SerializeField] private bool autoComputeManualScrollLimits = true;

    [SerializeField] private float manualScrollPadding = 16f;
    [SerializeField] private float manualMinY = -1200f;
    [SerializeField] private float manualMaxY = 120f;

    [Header("Row Visibility Culling")]
    [Tooltip("If enabled, rows outside the viewport are visually hidden while scrolling.")]
    [SerializeField] private bool hideRowsOutsideViewport = true;

    [Tooltip("Hide rows that moved above the viewport.")]
    [SerializeField] private bool hideRowsAboveViewport = true;

    [Tooltip("Hide rows that moved below the viewport.")]
    [SerializeField] private bool hideRowsBelowViewport = true;

    [Tooltip("Extra tolerance around viewport edges before rows are hidden.")]
    [SerializeField] private float rowCullingMargin = 8f;

    [Header("Optional UI")]
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private GameObject loadingRoot;
    [SerializeField] private TMP_Text emptyStateText;
    [SerializeField] private AchievementDebugOverlay debugOverlay;

    [Header("Behavior")]
    [SerializeField] private bool refreshOnEnable = true;

    private AchievementStateData latestState;
    private bool isRefreshing;
    private bool hasManualBaseY;
    private float manualBaseY;

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

    private void Update()
    {
        HandleManualWheelScroll();
        UpdateRowCulling();
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
        List<AchievementRowView> discoveredRows = new List<AchievementRowView>(searchRoot.GetComponentsInChildren<AchievementRowView>(true));

        if (autoCreateRowsFromClaimButtons)
        {
            AutoCreateRowsFromClaimButtons(searchRoot, discoveredRows);
        }

        rowViews = SortRowsTopToBottom(discoveredRows);

        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (scrollRect != null && rowsRoot == null && scrollRect.content != null)
            rowsRoot = scrollRect.content;

        if (manualScrollTarget == null)
            manualScrollTarget = ResolveManualScrollTarget(discoveredRows);

        if (manualScrollViewport == null && scrollRect != null)
            manualScrollViewport = scrollRect.viewport;

        if (manualScrollTarget != null && !hasManualBaseY)
        {
            manualBaseY = manualScrollTarget.anchoredPosition.y;
            hasManualBaseY = true;
        }

        UpdateRowCulling();
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

        AssignRuntimeAchievementIds(data);

        foreach (AchievementRowView rowView in rowViews)
        {
            if (rowView == null)
                continue;

            if (string.IsNullOrWhiteSpace(rowView.AchievementId))
            {
                rowView.SetClaimHandler(HandleClaimRequested);
                rowView.SetLocked("Not mapped to an achievement");
                continue;
            }

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

        UpdateRowCulling();
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
        {
            if (scrollToTopOnRefresh)
            {
                SnapManualTargetToTop();
            }

            return;
        }

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

    private void SnapManualTargetToTop()
    {
        if (manualScrollTarget == null)
            return;

        RectTransform viewport = ResolveManualViewport();
        if (viewport == null)
            return;

        Canvas.ForceUpdateCanvases();

        Bounds relativeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, manualScrollTarget);
        float viewportTop = GetManualViewportTopLimit(viewport) - manualScrollPadding;
        float deltaY = viewportTop - relativeBounds.max.y;

        Vector2 anchored = manualScrollTarget.anchoredPosition;
        anchored.y += deltaY;
        manualScrollTarget.anchoredPosition = anchored;

        // Re-clamp using freshly computed limits.
        float minY = manualMinY;
        float maxY = manualMaxY;
        ComputeManualScrollRange(ref minY, ref maxY);
        anchored = manualScrollTarget.anchoredPosition;
        anchored.y = Mathf.Clamp(anchored.y, minY, maxY);
        manualScrollTarget.anchoredPosition = anchored;

        // Treat snapped top as the fixed upper boundary for future wheel scrolling.
        manualBaseY = anchored.y;
        hasManualBaseY = true;
    }

    private void HandleManualWheelScroll()
    {
        if (!enableManualWheelScrollFallback)
            return;

        if (scrollRect != null)
            return;

        if (manualScrollTarget == null)
            return;

        RectTransform viewport = ResolveManualViewport();
        if (!IsPointerInsideManualScrollArea(viewport))
            return;

        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) <= 0.0001f)
            return;

        float minY = manualMinY;
        float maxY = manualMaxY;
        ComputeManualScrollRange(ref minY, ref maxY);

        float direction = manualInvertWheel ? 1f : -1f;

        Vector2 anchored = manualScrollTarget.anchoredPosition;
        anchored.y = Mathf.Clamp(anchored.y + (direction * wheel * manualWheelScrollSpeed), minY, maxY);
        manualScrollTarget.anchoredPosition = anchored;

        UpdateRowCulling();
    }

    private void ComputeManualScrollRange(ref float minY, ref float maxY)
    {
        if (!autoComputeManualScrollLimits || manualScrollTarget == null)
            return;

        RectTransform viewport = ResolveManualViewport();

        if (viewport == null)
            return;

        Canvas.ForceUpdateCanvases();

        float contentHeight = Mathf.Max(manualScrollTarget.rect.height, LayoutUtility.GetPreferredHeight(manualScrollTarget));

        // Use rendered bounds relative to viewport to include children positioned
        // outside the content rect (common in hand-placed Unity UI hierarchies).
        Bounds relativeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, manualScrollTarget);
        contentHeight = Mathf.Max(contentHeight, relativeBounds.size.y);

        float viewportHeight = GetManualScrollableViewportHeight(viewport);
        if (contentHeight <= viewportHeight + 0.01f)
        {
            // If auto-detection says no overflow, fall back to manual limits.
            // This keeps scrolling usable in hand-placed UI layouts.
            minY = manualMinY;
            maxY = manualMaxY;
            return;
        }

        if (!hasManualBaseY)
        {
            manualBaseY = manualScrollTarget.anchoredPosition.y;
            hasManualBaseY = true;
        }

        // Keep the minimum Y boundary fixed so list rows cannot pass into the header/back button area when scrolled to the top.
        minY = manualBaseY;

        float overflow = Mathf.Max(0f, contentHeight - viewportHeight + manualScrollPadding);
        maxY = manualBaseY + overflow;

        // Safety fallback when bounds are degenerate.
        if (minY > maxY)
        {
            minY = manualMinY;
            maxY = manualMaxY;
        }

        // If bounds collapse to a tiny range, keep manual limits as fallback.
        if (Mathf.Abs(maxY - minY) <= 0.01f)
        {
            minY = manualMinY;
            maxY = manualMaxY;
        }
    }

    private RectTransform ResolveManualViewport()
    {
        if (manualScrollTarget == null)
            return null;

        RectTransform viewport = manualScrollViewport;
        if (viewport == null)
            viewport = manualScrollTarget.parent as RectTransform;

        // Common inspector mistake: viewport is assigned to the same object as content.
        if (viewport == manualScrollTarget)
            viewport = manualScrollTarget.parent as RectTransform;

        return viewport;
    }

    private void UpdateRowCulling()
    {
        if (!hideRowsOutsideViewport || rowViews == null || rowViews.Length == 0)
            return;

        RectTransform viewport = ResolveManualViewport();
        if (viewport == null)
            return;

        float viewportTop = GetManualViewportTopLimit(viewport) + rowCullingMargin;
        float viewportBottom = GetManualViewportBottomLimit(viewport) - rowCullingMargin;

        for (int i = 0; i < rowViews.Length; i++)
        {
            AchievementRowView row = rowViews[i];
            if (row == null)
                continue;

            RectTransform rowRect = row.transform as RectTransform;
            if (rowRect == null)
                continue;

            Bounds rowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, rowRect);
            bool isAbove = rowBounds.min.y > viewportTop;
            bool isBelow = rowBounds.max.y < viewportBottom;

            bool hideByTop = hideRowsAboveViewport && isAbove;
            bool hideByBottom = hideRowsBelowViewport && isBelow;
            bool isVisible = !(hideByTop || hideByBottom);

            if (!hideRowsAboveViewport && isAbove)
                isVisible = true;

            if (!hideRowsBelowViewport && isBelow)
                isVisible = true;

            SetRowVisible(row, isVisible);
        }
    }

    private void SetRowVisible(AchievementRowView row, bool visible)
    {
        if (row == null)
            return;

        CanvasGroup canvasGroup = row.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = row.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private bool IsPointerInsideManualScrollArea(RectTransform viewport)
    {
        if (viewport == null)
            return false;

        Camera eventCamera = ResolveViewportEventCamera(viewport);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, Input.mousePosition, eventCamera, out Vector2 localPoint))
            return false;

        float top = GetManualViewportTopLimit(viewport);
        float bottom = GetManualViewportBottomLimit(viewport);
        return localPoint.y <= top && localPoint.y >= bottom;
    }

    private Camera ResolveViewportEventCamera(RectTransform viewport)
    {
        Canvas canvas = viewport.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    private float GetManualViewportTopLimit(RectTransform viewport)
    {
        if (viewport == null)
            return 0f;

        float maxInset = Mathf.Max(0f, viewport.rect.height - 24f);
        float clampedInset = Mathf.Clamp(manualViewportTopInset, 0f, maxInset);
        return viewport.rect.yMax - clampedInset;
    }

    private float GetManualViewportBottomLimit(RectTransform viewport)
    {
        if (viewport == null)
            return 0f;

        float maxInset = Mathf.Max(0f, viewport.rect.height - 24f);
        float clampedInset = Mathf.Clamp(manualViewportBottomInset, 0f, maxInset);
        return viewport.rect.yMin + clampedInset;
    }

    private float GetManualScrollableViewportHeight(RectTransform viewport)
    {
        float top = GetManualViewportTopLimit(viewport);
        float bottom = GetManualViewportBottomLimit(viewport);
        return Mathf.Max(1f, top - bottom);
    }

    private RectTransform ResolveManualScrollTarget(List<AchievementRowView> discoveredRows)
    {
        if (rowsRoot is RectTransform rowsRootRect)
            return rowsRootRect;

        if (discoveredRows == null || discoveredRows.Count == 0)
            return null;

        RectTransform commonParent = null;

        for (int i = 0; i < discoveredRows.Count; i++)
        {
            AchievementRowView row = discoveredRows[i];
            if (row == null)
                continue;

            RectTransform rowRect = row.transform as RectTransform;
            if (rowRect == null || rowRect.parent == null)
                continue;

            RectTransform rowParentRect = rowRect.parent as RectTransform;
            if (rowParentRect == null)
                continue;

            if (commonParent == null)
            {
                commonParent = rowParentRect;
            }
            else if (commonParent != rowParentRect)
            {
                commonParent = null;
                break;
            }
        }

        return commonParent;
    }

    private void AutoCreateRowsFromClaimButtons(Transform searchRoot, List<AchievementRowView> discoveredRows)
    {
        if (searchRoot == null)
            return;

        Button[] buttons = searchRoot.GetComponentsInChildren<Button>(includeInactiveClaimButtons);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !IsClaimButton(button.name))
                continue;

            AchievementRowView row = button.GetComponentInParent<AchievementRowView>(true);
            if (row == null)
            {
                row = button.gameObject.AddComponent<AchievementRowView>();
            }

            row.ConfigureRuntime(row.AchievementId, button);

            if (!discoveredRows.Contains(row))
                discoveredRows.Add(row);
        }
    }

    private AchievementRowView[] SortRowsTopToBottom(List<AchievementRowView> rows)
    {
        if (rows == null)
            return Array.Empty<AchievementRowView>();

        rows.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            RectTransform aRect = a.transform as RectTransform;
            RectTransform bRect = b.transform as RectTransform;

            float ay = aRect != null ? aRect.anchoredPosition.y : a.transform.position.y;
            float by = bRect != null ? bRect.anchoredPosition.y : b.transform.position.y;
            int yCompare = by.CompareTo(ay);
            if (yCompare != 0)
                return yCompare;

            float ax = aRect != null ? aRect.anchoredPosition.x : a.transform.position.x;
            float bx = bRect != null ? bRect.anchoredPosition.x : b.transform.position.x;
            return ax.CompareTo(bx);
        });

        return rows.ToArray();
    }

    private bool IsClaimButton(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        string lowered = objectName.ToLowerInvariant();
        if (lowered.Contains("exit"))
            return false;

        return lowered.Contains("claim");
    }

    private void AssignRuntimeAchievementIds(AchievementStateData data)
    {
        if (data == null || data.achievements == null || rowViews == null)
            return;

        HashSet<string> usedIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < rowViews.Length; i++)
        {
            AchievementRowView row = rowViews[i];
            if (row == null || !row.HasExplicitAchievementId || string.IsNullOrWhiteSpace(row.AchievementId))
                continue;

            usedIds.Add(row.AchievementId);
        }

        List<AchievementStateItem> unassignedAchievements = new List<AchievementStateItem>();
        for (int i = 0; i < data.achievements.Length; i++)
        {
            AchievementStateItem achievement = data.achievements[i];
            if (achievement == null || string.IsNullOrWhiteSpace(achievement.id))
                continue;

            if (!usedIds.Contains(achievement.id))
                unassignedAchievements.Add(achievement);
        }

        int runtimeIndex = 0;
        for (int i = 0; i < rowViews.Length; i++)
        {
            AchievementRowView row = rowViews[i];
            if (row == null || row.HasExplicitAchievementId)
                continue;

            string runtimeId = runtimeIndex < unassignedAchievements.Count
                ? unassignedAchievements[runtimeIndex].id
                : string.Empty;

            row.ConfigureRuntime(runtimeId);
            runtimeIndex++;
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
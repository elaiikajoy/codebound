// ============================================================
// ProgressService.cs
// Purpose: Syncs player progress with the CodeBound backend.
//            POST /progress/update  — after level completion
//            GET  /progress         — full progress object
//            GET  /progress/stats   — aggregate statistics
//
// Unity Setup:
//   - Attach to the "GameAPI" persistent GameObject.
//
// Quick usage (no reference needed — call from level end logic):
//   ProgressService.SyncAfterLevel(levelNumber: 3, tokensEarned: 120);
//
// Full usage with callbacks:
//   StartCoroutine(ProgressService.Instance.SyncLevelCompletion(
//       new ProgressUpdateRequest { levelCompleted=3, tokensEarned=120, ... },
//       onSuccess: data => Debug.Log("Synced!"),
//       onError:   err  => Debug.LogWarning(err)
//   ));
// ============================================================

using System;
using System.Collections;
using UnityEngine;

public class ProgressService : MonoBehaviour
{
    // Public debug/status fields (readable by on-screen overlays)
    public static string LastSyncStatus = "Never"; // Started | Success | Error | Never
    public static string LastSyncDetails = "";    // JSON request/response or error message
    public static string LastSyncTime = "";       // ISO timestamp of last attempt
    // ─── Singleton ────────────────────────────────────────────
    public static ProgressService Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── Sync level completion ────────────────────────────────
    /// <summary>
    /// POST /progress/update
    /// Call this when a level is won.
    /// Updates the backend, then keeps local PlayerPrefs in sync.
    /// </summary>
    public IEnumerator SyncLevelCompletion(
        ProgressUpdateRequest request,
        Action<UserProgressData> onSuccess = null,
        Action<string> onError = null)
    {
        // Always apply a local unlock for the next level even when offline.
        // This ensures the level selector will allow the player to continue
        // without requiring a successful backend call.
        ApplyLocalProgressForLevelCompletion(request.levelCompleted);

        // Debug: show request and runtime state so we can trace missing backend calls.
        try { Debug.Log($"[ProgressService] SyncLevelCompletion called. req={JsonUtility.ToJson(request)}"); } catch { Debug.Log("[ProgressService] SyncLevelCompletion called (json failed)"); }

        // Update last-sync debug fields for overlay/diagnostics
        try
        {
            LastSyncStatus = "Started";
            LastSyncDetails = JsonUtility.ToJson(request);
            LastSyncTime = System.DateTime.UtcNow.ToString("o");
        }
        catch { /* ignore debug failures */ }

        if (GameApiManager.Instance == null)
        {
            Debug.LogWarning("[ProgressService] No GameApiManager instance available — cannot send authenticated progress.");
            onError?.Invoke("No GameApiManager instance.");
            yield break;
        }

        if (!GameApiManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[ProgressService] Not logged in — progress not synced to backend, but local progress updated.");
            onSuccess?.Invoke(new UserProgressData
            {
                currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1),
                highestLevel = PlayerPrefs.GetInt("HighestLevel", 1),
                totalTokens = PlayerPrefs.GetInt("TotalTokens", 0)
            });
            yield break;
        }

        if (ApiClient.Instance == null)
        {
            Debug.LogError("[ProgressService] ApiClient instance is missing — cannot POST progress.");
            onError?.Invoke("ApiClient instance missing.");
            yield break;
        }

        // Ensure ApiConfig is ready so ApiClient can build a proper URL and headers.
        if (ApiConfig.Instance == null || !ApiConfig.Instance.IsReady || ApiConfig.Instance.Config == null)
        {
            Debug.LogError("[ProgressService] ApiConfig not ready — progress request aborted.");
            onError?.Invoke("ApiConfig not ready.");
            yield break;
        }

        // Attach any coins the player collected in the overworld/level
        // before this sync was triggered. The backend will credit the full
        // sum, and SyncFromBackend (called in the onSuccess below) will
        // reset PendingTokensToSync to 0 — no double-counting.
        int pendingCoins = TokenManager.GetPending();
        if (pendingCoins > 0)
        {
            Debug.Log($"[ProgressService] Attaching {pendingCoins} pending coin(s) to level sync. New tokensEarned: {request.tokensEarned} + {pendingCoins} = {request.tokensEarned + pendingCoins}");
            request.tokensEarned += pendingCoins;
        }

        yield return StartCoroutine(ApiClient.Instance.Post(
        "/progress/update",
        request,
        onSuccess: json =>
        {
            var resp = JsonUtility.FromJson<ProgressResponse>(json);
            if (resp != null && resp.success && resp.data != null)
            {
                // Mirror backend authoritative values into local PlayerPrefs.
                // SyncFromBackend also clears PendingTokensToSync because
                // the level-completion POST now includes the pending coins.
                TokenManager.SyncFromBackend(resp.data.totalTokens);
                PlayerPrefs.SetInt("CurrentLevel", resp.data.currentLevel);
                PlayerPrefs.SetInt("HighestLevel", resp.data.highestLevel);
                PlayerPrefs.Save();

                // Update debug overlay on success
                try
                {
                    LastSyncStatus = "Success";
                    LastSyncDetails = json;
                    LastSyncTime = System.DateTime.UtcNow.ToString("o");
                }
                catch { }

                onSuccess?.Invoke(resp.data);
            }
            else
            {
                // Update debug overlay on failure
                try
                {
                    LastSyncStatus = "Error";
                    LastSyncDetails = resp?.message ?? "Progress sync failed.";
                    LastSyncTime = System.DateTime.UtcNow.ToString("o");
                }
                catch { }

                onError?.Invoke(resp?.message ?? "Progress sync failed.");
            }
        },
        onError: err =>
        {
            // Progress sync failures are non-fatal; game continues offline.
            Debug.LogWarning($"[ProgressService] Sync failed (offline?): {err}");
            try
            {
                LastSyncStatus = "Error";
                LastSyncDetails = err;
                LastSyncTime = System.DateTime.UtcNow.ToString("o");
            }
            catch { }

            onError?.Invoke(err);
        },
        requiresAuth: true
    ));
    }

    private void ApplyLocalProgressForLevelCompletion(int levelCompleted)
    {
        // Unlock the next level locally. This allows users to progress even
        // when the backend is unreachable or when they're not logged in.
        int current = PlayerPrefs.HasKey("CurrentLevel")
            ? PlayerPrefs.GetInt("CurrentLevel")
            : levelCompleted;

        int highest = PlayerPrefs.HasKey("HighestLevel")
            ? PlayerPrefs.GetInt("HighestLevel")
            : levelCompleted;

        int nextLevel = Mathf.Max(current, levelCompleted + 1);
        int newHighest = Mathf.Max(highest, levelCompleted + 1);

        PlayerPrefs.SetInt("CurrentLevel", nextLevel);
        PlayerPrefs.SetInt("HighestLevel", newHighest);
        PlayerPrefs.Save();

        Debug.Log($"[ProgressService] Local progress updated: CurrentLevel={nextLevel}, HighestLevel={newHighest} (completed level {levelCompleted})");
    }

    // ─── Get full progress ────────────────────────────────────
    /// <summary>
    /// GET /progress
    /// Returns the full progress object for the logged-in player.
    /// </summary>
    public IEnumerator GetProgress(
        Action<UserProgressData> onSuccess,
        Action<string> onError = null)
    {
        if (!GameApiManager.Instance.IsLoggedIn) { onError?.Invoke("Not logged in."); yield break; }

        yield return StartCoroutine(ApiClient.Instance.Get(
            "/progress",
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<ProgressResponse>(json);
                if (resp != null && resp.success)
                    onSuccess?.Invoke(resp.data);
                else
                    onError?.Invoke(resp?.message ?? "Failed to get progress.");
            },
            onError: onError ?? (e => Debug.LogWarning(e)),
            requiresAuth: true
        ));
    }

    // ─── Get statistics ───────────────────────────────────────
    /// <summary>
    /// GET /progress/stats
    /// Returns aggregate stats (averages, totals, perfect levels).
    /// </summary>
    public IEnumerator GetStats(
        Action<ProgressStats> onSuccess,
        Action<string> onError = null)
    {
        if (!GameApiManager.Instance.IsLoggedIn) { onError?.Invoke("Not logged in."); yield break; }

        yield return StartCoroutine(ApiClient.Instance.Get(
            "/progress/stats",
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<ProgressStatsResponse>(json);
                if (resp != null && resp.success)
                    onSuccess?.Invoke(resp.data);
                else
                    onError?.Invoke(resp?.message ?? "Failed to get stats.");
            },
            onError: onError ?? (e => Debug.LogWarning(e)),
            requiresAuth: true
        ));
    }

    // ─── Static shortcut ──────────────────────────────────────
    /// <summary>
    /// Fire-and-forget level sync — call from any level-win script.
    /// Example: ProgressService.SyncAfterLevel(5, 150, 42.5f, isPerfect: true);
    /// </summary>
    public static void SyncAfterLevel(
        int levelNumber,
        int tokensEarned,
        float timeSpent = 0f,
        int hintsUsed = 0,
        bool isPerfect = false,
        Action<UserProgressData> onSuccess = null,
        Action<string> onError = null)
    {
        if (Instance == null)
        {
            // Attempt to create a fallback ProgressService so SyncAfterLevel
            // calls from gameplay don't silently fail when the persistent
            // GameAPI object wasn't added to the scene.
            Debug.LogWarning("[ProgressService] No instance in scene — creating fallback ProgressService at runtime.");
            var go = new GameObject("ProgressService");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ProgressService>();
            // Instance.Awake will run and set Instance; log to help debugging.
            Debug.Log("[ProgressService] Fallback instance created.");
            try { LastSyncStatus = "FallbackCreated"; LastSyncTime = System.DateTime.UtcNow.ToString("o"); } catch { }
        }

        var req = new ProgressUpdateRequest
        {
            levelCompleted = levelNumber,
            tokensEarned = tokensEarned,
            timeSpent = timeSpent,
            hintsUsed = hintsUsed,
            isPerfect = isPerfect,
            hasCodeErrors = false
        };

        Debug.Log($"[ProgressService] SyncAfterLevel invoked: level={levelNumber}, tokens={tokensEarned}, isPerfect={isPerfect}");
        try { LastSyncStatus = "Invoked"; LastSyncDetails = JsonUtility.ToJson(req); LastSyncTime = System.DateTime.UtcNow.ToString("o"); } catch { }
        Instance.StartCoroutine(Instance.SyncLevelCompletion(req, onSuccess, onError));
    }

    // ─── Sync pending overworld tokens ────────────────────────
    /// <summary>
    /// POST /progress/sync-tokens
    /// Call this to flush coins the player collected in the overworld
    /// (not from level completion) to the backend.
    /// Safe to call at any time — no-ops when nothing is pending or not logged in.
    /// </summary>
    public IEnumerator SyncPendingTokens(
        Action<UserProgressData> onSuccess = null,
        Action<string> onError = null)
    {
        int pending = TokenManager.GetPending();
        if (pending <= 0)
        {
            Debug.Log("[ProgressService] SyncPendingTokens: nothing to sync.");
            yield break;
        }

        if (GameApiManager.Instance == null || !GameApiManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[ProgressService] SyncPendingTokens: not logged in — pending tokens will sync on next login.");
            yield break;
        }

        if (ApiClient.Instance == null || ApiConfig.Instance == null || !ApiConfig.Instance.IsReady)
        {
            Debug.LogWarning("[ProgressService] SyncPendingTokens: ApiClient or ApiConfig not ready.");
            yield break;
        }

        var body = new SyncTokensRequest { tokensToAdd = pending };
        Debug.Log($"[ProgressService] SyncPendingTokens: posting {pending} pending token(s).");

        yield return StartCoroutine(ApiClient.Instance.Post(
            "/progress/sync-tokens",
            body,
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<ProgressResponse>(json);
                if (resp != null && resp.success && resp.data != null)
                {
                    TokenManager.SyncFromBackend(resp.data.totalTokens);
                    PlayerPrefs.SetInt("CurrentLevel", resp.data.currentLevel);
                    PlayerPrefs.SetInt("HighestLevel", resp.data.highestLevel);
                    PlayerPrefs.Save();
                    Debug.Log($"[ProgressService] SyncPendingTokens succeeded. New total: {resp.data.totalTokens}");
                    onSuccess?.Invoke(resp.data);
                }
                else
                {
                    string msg = resp?.message ?? "SyncPendingTokens failed.";
                    Debug.LogWarning($"[ProgressService] SyncPendingTokens error: {msg}");
                    onError?.Invoke(msg);
                }
            },
            onError: err =>
            {
                Debug.LogWarning($"[ProgressService] SyncPendingTokens failed (offline?): {err}");
                onError?.Invoke(err);
            },
            requiresAuth: true
        ));
    }

    /// <summary>
    /// Fire-and-forget version of SyncPendingTokens for use from non-coroutine code.
    /// </summary>
    public static void FlushPendingTokens(
        Action<UserProgressData> onSuccess = null,
        Action<string> onError = null)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[ProgressService] FlushPendingTokens: no instance available.");
            return;
        }
        Instance.StartCoroutine(Instance.SyncPendingTokens(onSuccess, onError));
    }
}

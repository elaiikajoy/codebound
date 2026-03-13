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
        if (!GameApiManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[ProgressService] Not logged in — progress not synced.");
            yield break;
        }

        yield return StartCoroutine(ApiClient.Instance.Post(
            "/progress/update",
            request,
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<ProgressResponse>(json);
                if (resp != null && resp.success && resp.data != null)
                {
                    // Mirror backend values into local PlayerPrefs so
                    // offline reads (e.g. leaderboard UI) stay accurate.
                    TokenManager.SyncFromBackend(resp.data.totalTokens);
                    PlayerPrefs.SetInt("CurrentLevel", resp.data.currentLevel);
                    PlayerPrefs.SetInt("HighestLevel", resp.data.highestLevel);
                    PlayerPrefs.Save();

                    onSuccess?.Invoke(resp.data);
                }
                else
                {
                    onError?.Invoke(resp?.message ?? "Progress sync failed.");
                }
            },
            onError: err =>
            {
                // Progress sync failures are non-fatal; game continues offline.
                Debug.LogWarning($"[ProgressService] Sync failed (offline?): {err}");
                onError?.Invoke(err);
            },
            requiresAuth: true
        ));
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
            Debug.LogError("[ProgressService] No instance in scene.");
            onError?.Invoke("ProgressService instance is missing.");
            return;
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

        Instance.StartCoroutine(Instance.SyncLevelCompletion(req, onSuccess, onError));
    }
}

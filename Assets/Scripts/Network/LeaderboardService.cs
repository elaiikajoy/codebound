// ============================================================
// LeaderboardService.cs
// Purpose: Fetches leaderboard data from the CodeBound backend.
//            GET /leaderboard           — global (public)
//            GET /leaderboard/top/:n    — top N players (public)
//            GET /leaderboard/rank      — player's own rank (protected)
//
// Unity Setup:
//   - Attach to the "GameAPI" persistent GameObject.
//
// Usage:
//   StartCoroutine(LeaderboardService.Instance.GetLeaderboard(
//       limit: 10,
//       sort: "level",
//       onSuccess: entries =>
//       {
//           foreach (var e in entries)
//               Debug.Log($"#{e.rank}  {e.username}  Lv{e.levelReached}");
//       },
//       onError: err => Debug.LogError(err)
//   ));
// ============================================================

using System;
using System.Collections;
using UnityEngine;

public class LeaderboardService : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────
    public static LeaderboardService Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── Global leaderboard (public) ─────────────────────────
    /// <summary>
    /// GET /leaderboard?limit=&amp;sort=
    /// Public — no login required.
    /// sort: "level" | "tokens" | "playtime" | "recent"
    /// </summary>
    public IEnumerator GetLeaderboard(
        int limit = 100,
        string sort = "level",
        Action<LeaderboardEntry[]> onSuccess = null,
        Action<string> onError = null)
    {
        string endpoint = $"/leaderboard?limit={limit}&sort={sort}";

        yield return StartCoroutine(ApiClient.Instance.Get(
            endpoint,
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<LeaderboardResponse>(json);
                if (resp != null && resp.success && resp.data?.players != null)
                    onSuccess?.Invoke(resp.data.players);
                else
                    onError?.Invoke(resp?.message ?? "Failed to load leaderboard.");
            },
            onError: onError ?? (e => Debug.LogWarning(e))
        ));
    }

    // ─── Top N players (public) ───────────────────────────────
    /// <summary>
    /// GET /leaderboard/top/:count
    /// Public — no login required. Max count: 100.
    /// </summary>
    public IEnumerator GetTopPlayers(
        int count = 10,
        Action<LeaderboardEntry[]> onSuccess = null,
        Action<string> onError = null)
    {
        yield return StartCoroutine(ApiClient.Instance.Get(
            $"/leaderboard/top/{count}",
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<LeaderboardResponse>(json);
                if (resp != null && resp.success && resp.data?.players != null)
                    onSuccess?.Invoke(resp.data.players);
                else
                    onError?.Invoke(resp?.message ?? "Failed to load top players.");
            },
            onError: onError ?? (e => Debug.LogWarning(e))
        ));
    }

    // ─── Player's own rank (protected) ────────────────────────
    /// <summary>
    /// GET /leaderboard/rank
    /// Returns the logged-in player's global rank integer.
    /// </summary>
    public IEnumerator GetPlayerRank(
        Action<int> onSuccess,
        Action<string> onError = null)
    {
        if (!GameApiManager.Instance.IsLoggedIn) { onError?.Invoke("Not logged in."); yield break; }

        yield return StartCoroutine(ApiClient.Instance.Get(
            "/leaderboard/rank",
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<PlayerRankResponse>(json);
                if (resp != null && resp.success)
                    onSuccess?.Invoke(resp.data.rank);
                else
                    onError?.Invoke(resp?.message ?? "Failed to get rank.");
            },
            onError: onError ?? (e => Debug.LogWarning(e)),
            requiresAuth: true
        ));
    }
}

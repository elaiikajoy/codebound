// ============================================================
// TokenManager.cs
// Purpose: Single source-of-truth for the player's token / coin count.
//
//   PlayerPrefs keys used:
//     "PlayerTokens"        – current total (read by UI)
//     "PendingTokensToSync" – accumulated offline coins not yet pushed to backend
//
// Usage:
//   TokenManager.AddTokens(1);        // collect a coin in-world (+pending)
//   TokenManager.SpendTokens(50);     // buy a character (deduct only, no pending)
//   TokenManager.GetTokens();         // read current total for UI
//   TokenManager.GetPending();        // pending amount not yet synced
//   TokenManager.ClearPending();      // call after a successful /sync-tokens POST
//   TokenManager.SyncFromBackend(n);  // called by ProgressService after any /progress response
// ============================================================

using UnityEngine;

public class TokenManager : MonoBehaviour
{
    // ─── Public API ───────────────────────────────────────────

    private static bool _pendingSyncInFlight;

    private static void SetAllTokenKeys(int amount)
    {
        PlayerPrefs.SetInt("PlayerTokens", amount);
        PlayerPrefs.SetInt("TotalTokens", amount);
    }

    /// <summary>
    /// Add <paramref name="amount"/> coins to the player's local total.
    /// Also queues them as pending so they get flushed to the backend on next Save.
    /// </summary>
    public static void AddTokens(int amount)
    {
        if (amount <= 0) return;

        int current = PlayerPrefs.GetInt("PlayerTokens", 0);
        int updated = current + amount;
        SetAllTokenKeys(updated);

        // Also keep a running count of coins not yet pushed to the backend.
        int pending = PlayerPrefs.GetInt("PendingTokensToSync", 0);
        PlayerPrefs.SetInt("PendingTokensToSync", pending + amount);

        PlayerPrefs.Save();
    }

    /// <summary>
    /// Pushes collected coin tokens to the backend as soon as possible.
    /// Calls are serialized so multiple coin pickups do not race each other.
    /// </summary>
    public static void RequestPendingSync()
    {
        if (_pendingSyncInFlight)
            return;

        int pending = GetPending();
        if (pending <= 0)
            return;

        if (ProgressService.Instance == null || GameApiManager.Instance == null || !GameApiManager.Instance.IsLoggedIn)
            return;

        _pendingSyncInFlight = true;
        ProgressService.FlushPendingTokens(
            onSuccess: _ =>
            {
                _pendingSyncInFlight = false;
                if (GetPending() > 0)
                {
                    RequestPendingSync();
                }
            },
            onError: _ =>
            {
                _pendingSyncInFlight = false;
            }
        );
    }

    /// <summary>
    /// Deduct <paramref name="amount"/> tokens from the player's balance.
    /// Used for shop purchases — does NOT add to PendingTokensToSync because
    /// the backend total will be corrected on the next progress sync.
    /// Returns false if the player cannot afford it (balance unchanged).
    /// </summary>
    public static bool SpendTokens(int amount)
    {
        if (amount <= 0) return true; // nothing to spend

        int current = PlayerPrefs.GetInt("PlayerTokens", 0);
        if (current < amount)
        {
            Debug.LogWarning($"[TokenManager] SpendTokens({amount}) failed — insufficient balance ({current}).");
            return false;
        }

        SetAllTokenKeys(current - amount);
        PlayerPrefs.Save();
        Debug.Log($"[TokenManager] Spent {amount} token(s). Remaining: {current - amount}");
        return true;
    }

    /// <summary>
    /// Returns the current local token total (used by UI elements like coinText).
    /// </summary>
    public static int GetTokens()
    {
        if (PlayerPrefs.HasKey("PlayerTokens"))
            return PlayerPrefs.GetInt("PlayerTokens", 0);

        return PlayerPrefs.GetInt("TotalTokens", 0);
    }

    /// <summary>
    /// Returns how many tokens have been collected since the last successful backend sync.
    /// </summary>
    public static int GetPending()
    {
        return PlayerPrefs.GetInt("PendingTokensToSync", 0);
    }

    /// <summary>
    /// Clears the pending counter after a successful POST to /progress/sync-tokens.
    /// </summary>
    public static void ClearPending()
    {
        PlayerPrefs.SetInt("PendingTokensToSync", 0);
        PlayerPrefs.Save();
    }

    // ─── Backend sync ──────────────────────────────────────────
    /// <summary>
    /// Called by ProgressService after a successful /progress/update or /progress response.
    /// Replaces the local total with the authoritative backend value and clears pending.
    /// </summary>
    public static void SyncFromBackend(int totalFromBackend)
    {
        SetAllTokenKeys(totalFromBackend);
        // Backend now has the ground truth — pending coins were included in this response.
        PlayerPrefs.SetInt("PendingTokensToSync", 0);
        PlayerPrefs.Save();
        Debug.Log($"[TokenManager] Synced from backend — TotalTokens={totalFromBackend}");
    }

    // ─── Legacy helpers (kept for compatibility) ───────────────
    /// <summary>Use AddTokens instead. Kept so old references still compile.</summary>
    public void GiveTestTokens()
    {
        AddTokens(100);
        Debug.Log("[TokenManager] Added 100 test tokens. Total: " + GetTokens());
    }

    /// <summary>Resets both local total and pending counter to zero.</summary>
    public void ResetTokens()
    {
        SetAllTokenKeys(0);
        PlayerPrefs.SetInt("PendingTokensToSync", 0);
        PlayerPrefs.Save();
        Debug.Log("[TokenManager] Tokens reset to 0.");
    }
}

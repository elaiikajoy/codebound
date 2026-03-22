// ============================================================
// GameApiManager.cs
// Purpose: Central manager for CodeBound backend integration.
//          - Persists across all scenes (DontDestroyOnLoad)
//          - Stores the auth token and current user data
//          - Tries to restore a previous session on start
//          - Exposes Login / Register / Logout to other scripts
//
// Unity Setup:
//   - Attach to the "GameAPI" persistent GameObject alongside
//     ApiConfig, ApiClient, AuthService, ProgressService,
//     LeaderboardService, and SkinService.
//
// Usage from any script:
//   GameApiManager.Instance.Login(email, pass, onSuccess, onError);
//   GameApiManager.Instance.IsLoggedIn
//   GameApiManager.Instance.CurrentUser.username
//
// Subscribe to events for UI updates:
//   GameApiManager.OnLoginSuccess  += HandleLogin;
//   GameApiManager.OnLogout        += HandleLogout;
//   GameApiManager.OnSessionRestored += HandleRestored;
// ============================================================

using System;
using System.Collections;
using UnityEngine;

public class GameApiManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────
    public static GameApiManager Instance { get; private set; }

    // ─── Events ───────────────────────────────────────────────
    /// <summary>Fired after a successful login or registration.</summary>
    public static event Action<UserData> OnLoginSuccess;

    /// <summary>Fired when login or registration fails.</summary>
    public static event Action<string> OnLoginError;

    /// <summary>Fired after a successful logout.</summary>
    public static event Action OnLogout;

    /// <summary>Fired when a saved token was validated on startup.</summary>
    public static event Action<UserData> OnSessionRestored;

    // ─── State ────────────────────────────────────────────────
    /// <summary>Encrypted JWT returned by the backend. Sent as Bearer token.</summary>
    public string AuthToken { get; private set; }

    /// <summary>Current logged-in user — null when not logged in.</summary>
    public UserData CurrentUser { get; private set; }

    /// <summary>True when a valid token and user data are both available.</summary>
    public bool IsLoggedIn => !string.IsNullOrEmpty(AuthToken) && CurrentUser != null;

    /// <summary>True when a saved auth token exists and session restore should be attempted.</summary>
    public bool HasSavedSession => PlayerPrefs.HasKey(TOKEN_PREF_KEY);

    // Auth token is persisted between game sessions via PlayerPrefs.
    private const string TOKEN_PREF_KEY = "codebound_auth_token";

    // ─── Lifecycle ────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Wait for ApiConfig, then try to restore a saved session.
        StartCoroutine(TryRestoreSession());
    }

    // ─── Public API ───────────────────────────────────────────

    /// <summary>
    /// Log in with username or email + password.
    /// Fires OnLoginSuccess or OnLoginError.
    /// </summary>
    public void Login(
        string identifier,
        string password,
        Action<UserData> onSuccess = null,
        Action<string> onError = null)
    {
        if (AuthService.Instance == null)
        {
            const string msg = "AuthService is missing on GameAPI object.";
            Debug.LogError("[GameApiManager] " + msg);
            onError?.Invoke(msg);
            OnLoginError?.Invoke(msg);
            return;
        }

        StartCoroutine(AuthService.Instance.Login(identifier, password,
            onSuccess: (userData, token) =>
            {
                StoreSession(token, userData);

                // Fetch progress before firing the success event to ensure PlayerPrefs are ready
                if (ProgressService.Instance != null)
                {
                    StartCoroutine(ProgressService.Instance.GetProgress(
                        onSuccess: progress =>
                        {
                            SyncProgressLocally(progress);
                            onSuccess?.Invoke(userData);
                            OnLoginSuccess?.Invoke(userData);
                        },
                        onError: err =>
                        {
                            // Login succeeded but progress fetch failed (e.g. timeout). Still proceed.
                            Debug.LogWarning($"[GameApiManager] Progress fetch failed after login: {err}");
                            onSuccess?.Invoke(userData);
                            OnLoginSuccess?.Invoke(userData);
                        }
                    ));
                }
                else
                {
                    onSuccess?.Invoke(userData);
                    OnLoginSuccess?.Invoke(userData);
                }
            },
            onError: err =>
            {
                onError?.Invoke(err);
                OnLoginError?.Invoke(err);
            }
        ));
    }

    /// <summary>
    /// Register a new account and log in automatically.
    /// Only username + password required — email is optional/null.
    /// Fires OnLoginSuccess or OnLoginError.
    /// </summary>
    public void Register(
        string username,
        string password,
        Action<UserData> onSuccess = null,
        Action<string> onError = null)
    {
        if (AuthService.Instance == null)
        {
            const string msg = "AuthService is missing on GameAPI object.";
            Debug.LogError("[GameApiManager] " + msg);
            onError?.Invoke(msg);
            OnLoginError?.Invoke(msg);
            return;
        }

        StartCoroutine(AuthService.Instance.Register(username, password,
            onSuccess: (userData, token) =>
            {
                StoreSession(token, userData);

                if (ProgressService.Instance != null)
                {
                    StartCoroutine(ProgressService.Instance.GetProgress(
                        onSuccess: progress =>
                        {
                            SyncProgressLocally(progress);
                            onSuccess?.Invoke(userData);
                            OnLoginSuccess?.Invoke(userData);
                        },
                        onError: err =>
                        {
                            Debug.LogWarning($"[GameApiManager] Progress fetch failed after register: {err}");
                            onSuccess?.Invoke(userData);
                            OnLoginSuccess?.Invoke(userData);
                        }
                    ));
                }
                else
                {
                    onSuccess?.Invoke(userData);
                    OnLoginSuccess?.Invoke(userData);
                }
            },
            onError: err =>
            {
                onError?.Invoke(err);
                OnLoginError?.Invoke(err);
            }
        ));
    }

    /// <summary>
    /// Fetch the latest profile for the currently authenticated user.
    /// </summary>
    public void GetProfile(
        Action<UserData> onSuccess = null,
        Action<string> onError = null)
    {
        if (AuthService.Instance == null)
        {
            const string msg = "AuthService is missing on GameAPI object.";
            Debug.LogError("[GameApiManager] " + msg);
            onError?.Invoke(msg);
            return;
        }

        StartCoroutine(AuthService.Instance.GetProfile(
            onSuccess: userData =>
            {
                CurrentUser = userData;
                onSuccess?.Invoke(userData);
            },
            onError: onError
        ));
    }

    /// <summary>
    /// Update username and refresh cached CurrentUser.
    /// </summary>
    public void UpdateUsername(
        string username,
        Action<UserData> onSuccess = null,
        Action<string> onError = null)
    {
        if (AuthService.Instance == null)
        {
            const string msg = "AuthService is missing on GameAPI object.";
            Debug.LogError("[GameApiManager] " + msg);
            onError?.Invoke(msg);
            return;
        }

        StartCoroutine(AuthService.Instance.UpdateUsername(
            username,
            onSuccess: userData =>
            {
                CurrentUser = userData;
                onSuccess?.Invoke(userData);
            },
            onError: onError
        ));
    }

    /// <summary>
    /// Change password for current account.
    /// </summary>
    public void ChangePassword(
        string currentPassword,
        string newPassword,
        Action onSuccess = null,
        Action<string> onError = null)
    {
        if (AuthService.Instance == null)
        {
            const string msg = "AuthService is missing on GameAPI object.";
            Debug.LogError("[GameApiManager] " + msg);
            onError?.Invoke(msg);
            return;
        }

        StartCoroutine(AuthService.Instance.ChangePassword(
            currentPassword,
            newPassword,
            onSuccess: onSuccess,
            onError: onError
        ));
    }

    /// <summary>
    /// Delete current account from backend, then clear local session.
    /// </summary>
    public void DeleteAccount(
        Action onSuccess = null,
        Action<string> onError = null)
    {
        if (AuthService.Instance == null)
        {
            const string msg = "AuthService is missing on GameAPI object.";
            Debug.LogError("[GameApiManager] " + msg);
            onError?.Invoke(msg);
            return;
        }

        StartCoroutine(AuthService.Instance.DeleteAccount(
            onSuccess: () =>
            {
                Logout();
                onSuccess?.Invoke();
            },
            onError: onError
        ));
    }

    /// <summary>
    /// Clear stored credentials and fire OnLogout.
    /// </summary>
    public void Logout()
    {
        AuthToken = null;
        CurrentUser = null;

        PlayerPrefs.DeleteKey(TOKEN_PREF_KEY);

        // ── Clear all local game-progress cache ───────────────────────────
        // This runs on regular logout AND on account deletion (DeleteAccount
        // calls Logout on success), so the UI always starts clean for the
        // next player who logs in on this device.
        PlayerPrefs.DeleteKey("CurrentLevel");
        PlayerPrefs.DeleteKey("HighestLevel");
        PlayerPrefs.DeleteKey("TotalTokens");
        PlayerPrefs.DeleteKey("PlayerTokens");
        PlayerPrefs.DeleteKey("EquippedCharacter");
        // ─────────────────────────────────────────────────────────────────

        PlayerPrefs.Save();

        OnLogout?.Invoke();
        Debug.Log("[GameApiManager] Logged out. All local player data cleared.");
    }

    /// <summary>
    /// Update cached user data after a progress or profile change.
    /// </summary>
    public void UpdateCurrentUser(UserData updated) => CurrentUser = updated;

    // ─── Internal ─────────────────────────────────────────────

    /// <summary>
    /// Checks PlayerPrefs for a saved token on startup.
    /// If found, validates it against the backend via /auth/sessionToken.
    /// If invalid or expired, clears the token silently.
    /// </summary>
    private IEnumerator TryRestoreSession()
    {
        // Wait until ApiConfig has loaded game.config.json
        while (ApiConfig.Instance == null || !ApiConfig.Instance.IsReady)
            yield return null;

        if (AuthService.Instance == null)
        {
            Debug.LogError("[GameApiManager] AuthService is missing on GameAPI object.");
            yield break;
        }

        string saved = PlayerPrefs.GetString(TOKEN_PREF_KEY, string.Empty);
        if (string.IsNullOrEmpty(saved))
        {
            Debug.Log("[GameApiManager] No saved session found.");
            yield break;
        }

        // Set token temporarily so the session request can include it.
        AuthToken = saved;

        yield return StartCoroutine(AuthService.Instance.GetSession(
            onSuccess: userData =>
            {
                CurrentUser = userData;
                Debug.Log($"[GameApiManager] Session restored — {userData.username}. Fetching progress...");

                if (ProgressService.Instance != null)
                {
                    StartCoroutine(ProgressService.Instance.GetProgress(
                        onSuccess: progress =>
                        {
                            SyncProgressLocally(progress);
                            OnSessionRestored?.Invoke(userData);
                        },
                        onError: err =>
                        {
                            Debug.LogWarning($"[GameApiManager] Progress fetch failed after session restore: {err}");
                            OnSessionRestored?.Invoke(userData);
                        }
                    ));
                }
                else
                {
                    OnSessionRestored?.Invoke(userData);
                }
            },
            onError: err =>
            {
                Debug.LogWarning($"[GameApiManager] Session restore failed: {err}");
                AuthToken = null;
                PlayerPrefs.DeleteKey(TOKEN_PREF_KEY);
            }
        ));
    }

    private void StoreSession(string token, UserData user)
    {
        AuthToken = token;
        CurrentUser = user;
        PlayerPrefs.SetString(TOKEN_PREF_KEY, token);
        PlayerPrefs.Save();
        Debug.Log($"[GameApiManager] Session stored for: {user.username}");
    }

    private void SyncProgressLocally(UserProgressData progress)
    {
        if (progress == null) return;

        PlayerPrefs.SetInt("CurrentLevel", progress.currentLevel);
        PlayerPrefs.SetInt("HighestLevel", progress.highestLevel);
        PlayerPrefs.SetInt("TotalTokens", progress.totalTokens);

        if (!string.IsNullOrEmpty(progress.equippedCharacter))
            PlayerPrefs.SetString("EquippedCharacter", progress.equippedCharacter);

        PlayerPrefs.Save();

        TokenManager.SyncFromBackend(progress.totalTokens);

        Debug.Log($"[GameApiManager] Synced progress locally - Level: {progress.currentLevel}, Tokens: {progress.totalTokens}");

        // If the local client has progressed further while offline, push that
        // local progress up to the backend so the database matches the game.
        // Local stored HighestLevel uses the convention of "next playable"
        // (highest = lastCompleted + 1), so convert back to completed level.
        try
        {
            int localHighest = PlayerPrefs.HasKey("HighestLevel") ? PlayerPrefs.GetInt("HighestLevel") : progress.highestLevel;
            int serverHighest = progress.highestLevel;

            if (localHighest > serverHighest)
            {
                int localCompleted = Mathf.Max(1, localHighest - 1);
                Debug.Log($"[GameApiManager] Local progress ({localHighest}) is ahead of server ({serverHighest}). Syncing completed level {localCompleted} to backend.");

                if (ProgressService.Instance != null)
                {
                    // Fire-and-forget; backend will respond and ProgressService will update PlayerPrefs again.
                    ProgressService.SyncAfterLevel(
                        levelNumber: localCompleted,
                        tokensEarned: 0,
                        onSuccess: _ => Debug.Log("[GameApiManager] Local progress pushed to backend."),
                        onError: err => Debug.LogWarning($"[GameApiManager] Failed to push local progress: {err}"));
                }
                else
                {
                    Debug.LogWarning("[GameApiManager] ProgressService instance missing — cannot push local progress.");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GameApiManager] Error while reconciling local progress: {ex.Message}");
        }
    }
}

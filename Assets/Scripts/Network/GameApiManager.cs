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
    /// Log in with email + password.
    /// Fires OnLoginSuccess or OnLoginError.
    /// </summary>
    public void Login(
        string email,
        string password,
        Action<UserData> onSuccess = null,
        Action<string> onError = null)
    {
        StartCoroutine(AuthService.Instance.Login(email, password,
            onSuccess: (userData, token) =>
            {
                StoreSession(token, userData);
                onSuccess?.Invoke(userData);
                OnLoginSuccess?.Invoke(userData);
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
    /// Fires OnLoginSuccess or OnLoginError.
    /// </summary>
    public void Register(
        string username,
        string email,
        string password,
        Action<UserData> onSuccess = null,
        Action<string> onError = null)
    {
        StartCoroutine(AuthService.Instance.Register(username, email, password,
            onSuccess: (userData, token) =>
            {
                StoreSession(token, userData);
                onSuccess?.Invoke(userData);
                OnLoginSuccess?.Invoke(userData);
            },
            onError: err =>
            {
                onError?.Invoke(err);
                OnLoginError?.Invoke(err);
            }
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
        PlayerPrefs.Save();

        OnLogout?.Invoke();
        Debug.Log("[GameApiManager] Logged out.");
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
                Debug.Log($"[GameApiManager] Session restored — {userData.username}");
                OnSessionRestored?.Invoke(userData);
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
}

// ============================================================
// AuthService.cs
// Purpose: Handles all authentication HTTP calls:
//            POST /auth/login
//            POST /auth/register
//            POST /auth/sessionToken  (validate saved token)
//
// Unity Setup:
//   - Attach to the "GameAPI" persistent GameObject.
//   - Do not call this directly from UI scripts.
//     Use GameApiManager.Instance.Login() / .Register() instead.
//
// Internal usage:
//   yield return StartCoroutine(AuthService.Instance.Login(...));
// ============================================================

using System;
using System.Collections;
using UnityEngine;

public class AuthService : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────
    public static AuthService Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── Login ────────────────────────────────────────────────
    /// <summary>
    /// POST /auth/login
    /// Accepts username OR email as the identifier.
    /// Calls onSuccess(UserData, token) or onError(message).
    /// </summary>
    public IEnumerator Login(
        string identifier,
        string password,
        Action<UserData, string> onSuccess,
        Action<string> onError)
    {
        var body = new LoginRequest { identifier = identifier, password = password };

        if (ApiConfig.Instance != null && ApiConfig.Instance.IsReady && ApiConfig.Instance.Config != null && ApiConfig.Instance.Config.debugMode)
            Debug.Log($"[AuthService] Sending login payload: identifier='{identifier}', passwordLength={password?.Length ?? 0}");

        yield return StartCoroutine(ApiClient.Instance.Post(
            "/auth/login",
            body,
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<AuthResponse>(json);
                if (resp != null && resp.success && resp.data != null)
                    onSuccess?.Invoke(resp.data.user, resp.data.token);
                else
                    onError?.Invoke(resp?.message ?? "Login failed. Check your username and password.");
            },
            onError: onError
        ));
    }

    // ─── Register ─────────────────────────────────────────────
    /// <summary>
    /// POST /auth/register
    /// Only requires username + password. Email is not sent.
    /// Calls onSuccess(UserData, token) or onError(message).
    /// </summary>
    public IEnumerator Register(
        string username,
        string password,
        Action<UserData, string> onSuccess,
        Action<string> onError)
    {
        var body = new RegisterRequest
        {
            username = username,
            password = password
        };

        if (ApiConfig.Instance != null && ApiConfig.Instance.IsReady && ApiConfig.Instance.Config != null && ApiConfig.Instance.Config.debugMode)
            Debug.Log($"[AuthService] Sending register payload: username='{username}', passwordLength={password?.Length ?? 0}");

        yield return StartCoroutine(ApiClient.Instance.Post(
            "/auth/register",
            body,
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<AuthResponse>(json);
                if (resp != null && resp.success && resp.data != null)
                    onSuccess?.Invoke(resp.data.user, resp.data.token);
                else
                    onError?.Invoke(resp?.message ?? "Registration failed.");
            },
            onError: onError
        ));
    }

    // ─── Session ──────────────────────────────────────────────
    /// <summary>
    /// POST /auth/sessionToken
    /// Validates the stored Bearer token and returns the current user.
    /// Called automatically by GameApiManager on startup.
    /// </summary>
    public IEnumerator GetSession(
        Action<UserData> onSuccess,
        Action<string> onError)
    {
        // Body is empty; auth comes from the Authorization header via ApiClient.
        yield return StartCoroutine(ApiClient.Instance.Post(
            "/auth/sessionToken",
            new object(),           // empty body — backend only reads the header
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<SessionResponse>(json);
                if (resp != null && resp.success && resp.data?.user != null)
                    onSuccess?.Invoke(resp.data.user);
                else
                    onError?.Invoke(resp?.message ?? "Session invalid or expired.");
            },
            onError: onError,
            requiresAuth: true
        ));
    }

    /// <summary>
    /// GET /auth/profile
    /// Fetches current authenticated user's profile data.
    /// </summary>
    public IEnumerator GetProfile(
        Action<UserData> onSuccess,
        Action<string> onError)
    {
        yield return StartCoroutine(ApiClient.Instance.Get(
            "/auth/profile",
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<ProfileResponse>(json);
                if (resp != null && resp.success && resp.data?.user != null)
                    onSuccess?.Invoke(resp.data.user);
                else
                    onError?.Invoke(resp?.message ?? "Failed to fetch profile.");
            },
            onError: onError,
            requiresAuth: true
        ));
    }

    /// <summary>
    /// PUT /auth/profile with username field.
    /// </summary>
    public IEnumerator UpdateUsername(
        string username,
        Action<UserData> onSuccess,
        Action<string> onError)
    {
        var body = new ProfileUpdateRequest
        {
            username = username
        };

        yield return StartCoroutine(ApiClient.Instance.Put(
            "/auth/profile",
            body,
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<ProfileResponse>(json);
                if (resp != null && resp.success && resp.data?.user != null)
                    onSuccess?.Invoke(resp.data.user);
                else
                    onError?.Invoke(resp?.message ?? "Failed to update username.");
            },
            onError: onError,
            requiresAuth: true
        ));
    }

    /// <summary>
    /// PUT /auth/profile with currentPassword and newPassword fields.
    /// </summary>
    public IEnumerator ChangePassword(
        string currentPassword,
        string newPassword,
        Action onSuccess,
        Action<string> onError)
    {
        var body = new ProfileUpdateRequest
        {
            currentPassword = currentPassword,
            newPassword = newPassword
        };

        yield return StartCoroutine(ApiClient.Instance.Put(
            "/auth/profile",
            body,
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<ApiBaseResponse>(json);
                if (resp != null && resp.success)
                    onSuccess?.Invoke();
                else
                    onError?.Invoke(resp?.message ?? "Failed to change password.");
            },
            onError: onError,
            requiresAuth: true
        ));
    }

    /// <summary>
    /// DELETE /auth/profile
    /// Deletes the current authenticated account.
    /// </summary>
    public IEnumerator DeleteAccount(
        Action onSuccess,
        Action<string> onError)
    {
        yield return StartCoroutine(ApiClient.Instance.Delete(
            "/auth/profile",
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<ApiBaseResponse>(json);
                if (resp != null && resp.success)
                    onSuccess?.Invoke();
                else
                    onError?.Invoke(resp?.message ?? "Failed to delete account.");
            },
            onError: onError,
            requiresAuth: true
        ));
    }
}

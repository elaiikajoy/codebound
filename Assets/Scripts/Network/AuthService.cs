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
    /// Calls onSuccess(UserData, token) or onError(message).
    /// </summary>
    public IEnumerator Login(
        string email,
        string password,
        Action<UserData, string> onSuccess,
        Action<string> onError)
    {
        var body = new LoginRequest { email = email, password = password };

        yield return StartCoroutine(ApiClient.Instance.Post(
            "/auth/login",
            body,
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<AuthResponse>(json);
                if (resp != null && resp.success && resp.data != null)
                    onSuccess?.Invoke(resp.data.user, resp.data.token);
                else
                    onError?.Invoke(resp?.message ?? "Login failed. Check email and password.");
            },
            onError: onError
        ));
    }

    // ─── Register ─────────────────────────────────────────────
    /// <summary>
    /// POST /auth/register
    /// Calls onSuccess(UserData, token) or onError(message).
    /// </summary>
    public IEnumerator Register(
        string username,
        string email,
        string password,
        Action<UserData, string> onSuccess,
        Action<string> onError)
    {
        var body = new RegisterRequest
        {
            username = username,
            email = email,
            password = password
        };

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
}

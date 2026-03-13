// ============================================================
// ApiClient.cs
// Purpose: Core HTTP client for all CodeBound API calls.
//          Wraps UnityWebRequest with proper headers:
//            api-key       — from ApiConfig  (every request)
//            Authorization — from GameApiManager (auth-only)
//
// Unity Setup:
//   - Attach to the same "GameAPI" persistent GameObject as
//     ApiConfig and GameApiManager.
//
// Usage (from any MonoBehaviour):
//   StartCoroutine(ApiClient.Instance.Get(
//       "/leaderboard",
//       onSuccess: json => { ... },
//       onError:   err  => { ... }
//   ));
//
//   StartCoroutine(ApiClient.Instance.Post(
//       "/auth/login", loginRequest,
//       onSuccess: json => { ... },
//       onError:   err  => { ... }
//   ));
// ============================================================

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ApiClient : MonoBehaviour
{
    private const float CONFIG_WAIT_TIMEOUT_SECONDS = 8f;

    // ─── Singleton ────────────────────────────────────────────
    public static ApiClient Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── GET ──────────────────────────────────────────────────
    /// <summary>
    /// Sends a GET request to the given endpoint.
    /// Set requiresAuth = true for protected routes.
    /// </summary>
    public IEnumerator Get(
        string endpoint,
        Action<string> onSuccess,
        Action<string> onError,
        bool requiresAuth = false)
    {
        bool configReady = false;
        yield return StartCoroutine(WaitForConfigOrReport(onError, ready => configReady = ready));
        if (!configReady)
            yield break;

        string url = BuildUrl(endpoint);

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            SetHeaders(req, requiresAuth);
            yield return req.SendWebRequest();
            HandleResponse(req, onSuccess, onError);
        }
    }

    // ─── POST ─────────────────────────────────────────────────
    /// <summary>
    /// Sends a POST request with a JSON-serialised body.
    /// </summary>
    public IEnumerator Post(
        string endpoint,
        object body,
        Action<string> onSuccess,
        Action<string> onError,
        bool requiresAuth = false)
    {
        bool configReady = false;
        yield return StartCoroutine(WaitForConfigOrReport(onError, ready => configReady = ready));
        if (!configReady)
            yield break;

        string url = BuildUrl(endpoint);
        string jsonBody = JsonUtility.ToJson(body);

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            SetHeaders(req, requiresAuth);

            yield return req.SendWebRequest();
            HandleResponse(req, onSuccess, onError);
        }
    }

    // ─── PUT ──────────────────────────────────────────────────
    /// <summary>
    /// Sends a PUT request with a JSON-serialised body.
    /// </summary>
    public IEnumerator Put(
        string endpoint,
        object body,
        Action<string> onSuccess,
        Action<string> onError,
        bool requiresAuth = false)
    {
        bool configReady = false;
        yield return StartCoroutine(WaitForConfigOrReport(onError, ready => configReady = ready));
        if (!configReady)
            yield break;

        string url = BuildUrl(endpoint);
        string jsonBody = JsonUtility.ToJson(body);

        using (UnityWebRequest req = new UnityWebRequest(url, "PUT"))
        {
            byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            SetHeaders(req, requiresAuth);

            yield return req.SendWebRequest();
            HandleResponse(req, onSuccess, onError);
        }
    }

    // ─── DELETE ───────────────────────────────────────────────
    /// <summary>Sends a DELETE request.</summary>
    public IEnumerator Delete(
        string endpoint,
        Action<string> onSuccess,
        Action<string> onError,
        bool requiresAuth = true)
    {
        bool configReady = false;
        yield return StartCoroutine(WaitForConfigOrReport(onError, ready => configReady = ready));
        if (!configReady)
            yield break;

        string url = BuildUrl(endpoint);

        using (UnityWebRequest req = UnityWebRequest.Delete(url))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            SetHeaders(req, requiresAuth);

            yield return req.SendWebRequest();
            HandleResponse(req, onSuccess, onError);
        }
    }

    // ─── Private helpers ──────────────────────────────────────
    /// <summary>
    /// Constructs the full URL.
    /// Example: endpoint "/auth/login" → "http://localhost:3000/auth/login"
    /// </summary>
    private string BuildUrl(string endpoint)
    {
        string baseUrl = ApiConfig.Instance.Config.backendBaseUrl.TrimEnd('/');
        string ep = endpoint.StartsWith("/") ? endpoint : "/" + endpoint;
        return baseUrl + ep;
    }

    /// <summary>
    /// Attaches required headers to every request.
    /// api-key is always sent; Authorization only when requiresAuth is true.
    /// </summary>
    private void SetHeaders(UnityWebRequest req, bool requiresAuth)
    {
        // Required on every endpoint (public and protected)
        string apiKey = ApiConfig.Instance.Config.apiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Debug.LogError("[ApiClient] API key is empty in game.config.json");
        }
        else
        {
            req.SetRequestHeader("api-key", apiKey);
            req.SetRequestHeader("x-api-key", apiKey);
        }

        if (requiresAuth)
        {
            string token = GameApiManager.Instance != null
                ? GameApiManager.Instance.AuthToken
                : string.Empty;

            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", $"Bearer {token}");
            else
                Debug.LogWarning("[ApiClient] requiresAuth=true but no token available.");
        }
    }

    /// <summary>
    /// Fires onSuccess with the raw response JSON, or onError with an error message.
    /// Logs requests in debug mode.
    /// </summary>
    private void HandleResponse(
        UnityWebRequest req,
        Action<string> onSuccess,
        Action<string> onError)
    {
        bool ok = req.result == UnityWebRequest.Result.Success;

        if (ApiConfig.Instance.Config.debugMode)
            Debug.Log($"[ApiClient] {req.method} {req.url} → {req.responseCode} {(ok ? "OK" : req.error)}");

        if (ok)
            onSuccess?.Invoke(req.downloadHandler.text);
        else
        {
            string serverMsg = req.downloadHandler?.text;
            onError?.Invoke(string.IsNullOrEmpty(serverMsg)
                ? $"HTTP {req.responseCode}: {req.error}"
                : $"HTTP {req.responseCode}: {serverMsg}");
        }
    }

    /// <summary>Waits until ApiConfig has finished loading game.config.json.</summary>
    private IEnumerator WaitForConfigOrReport(Action<string> onError, Action<bool> onComplete)
    {
        float elapsed = 0f;

        while (ApiConfig.Instance == null || !ApiConfig.Instance.IsReady || ApiConfig.Instance.Config == null)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= CONFIG_WAIT_TIMEOUT_SECONDS)
            {
                string msg = "ApiConfig is not ready. Check GameAPI object and StreamingAssets/game.config.json.";
                Debug.LogError("[ApiClient] " + msg);
                onError?.Invoke(msg);
                onComplete?.Invoke(false);
                yield break;
            }

            yield return null;
        }

        onComplete?.Invoke(true);
    }
}

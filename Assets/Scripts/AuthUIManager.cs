// ============================================================
// AuthUIManager.cs
// Purpose: Single UI controller for both Login and Register.
//          Sends only username + password to the backend.
//          On success, the returned user.id becomes the global
//          identifier used across all game systems (progress,
//          leaderboard, shop, etc.) via GameApiManager.CurrentUser.
//
// Unity Setup:
//   - Attach to a canvas GameObject in your Main / Auth scene
//     (e.g., "AuthPanel").
//   - Requires: GameApiManager persistent GameObject in the scene
//     (with ApiConfig, ApiClient, AuthService already attached).
//   - Wire all public fields in the Inspector.
//
// Usage flow:
//   Login tab  → player enters username + password → ClickLogin()
//   Register tab → player enters username + password → ClickRegister()
//   On success → game loads next scene automatically
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class AuthUIManager : MonoBehaviour
{
    // ─── Inspector fields ─────────────────────────────────────

    [Header("Panel switching")]
    [Tooltip("The login form panel root")]
    public GameObject loginPanel;
    [Tooltip("The register form panel root")]
    public GameObject registerPanel;

    [Header("Login inputs")]
    [Tooltip("Username or email — both accepted by the backend")]
    public TMP_InputField loginIdentifierInput;
    public TMP_InputField loginPasswordInput;
    public Button loginButton;

    [Header("Register inputs")]
    public TMP_InputField registerUsernameInput;
    public TMP_InputField registerPasswordInput;
    public Button registerButton;

    [Header("Scene navigation")]
    [Tooltip("Scene name to load after successful login or register")]
    public string nextSceneName = "Main";

    // ─── Private state ────────────────────────────────────────
    private bool _isBusy = false;

    // ─── Lifecycle ────────────────────────────────────────────
    private void Start()
    {
        EnsureGameApiReady();

        // If a valid session was already restored on startup, skip auth.
        StartCoroutine(CheckExistingSession());

        WireButtonHandlers();

        // Default: show login panel
        ShowLoginPanel();
    }

    private void WireButtonHandlers()
    {
        if (loginButton == null && registerButton == null)
        {
            Debug.LogWarning("[Auth UI] No login/register button assigned.");
            return;
        }

        if (loginButton != null && registerButton != null && loginButton == registerButton)
        {
            loginButton.onClick.RemoveListener(OnPrimaryButtonClicked);
            loginButton.onClick.AddListener(OnPrimaryButtonClicked);
            return;
        }

        if (loginButton != null)
        {
            loginButton.onClick.RemoveListener(ClickLogin);
            loginButton.onClick.AddListener(ClickLogin);
        }

        if (registerButton != null)
        {
            registerButton.onClick.RemoveListener(ClickRegister);
            registerButton.onClick.AddListener(ClickRegister);
        }
    }

    private void OnPrimaryButtonClicked()
    {
        bool registerVisible = registerPanel != null && registerPanel.activeInHierarchy;
        if (registerVisible)
            ClickRegister();
        else
            ClickLogin();
    }

    // ─── Panel switching ──────────────────────────────────────

    /// <summary>Show the login form and hide the register form.</summary>
    public void ShowLoginPanel()
    {
        loginPanel?.SetActive(true);
        registerPanel?.SetActive(false);
        ClearFeedback();
    }

    /// <summary>Show the register form and hide the login form.</summary>
    public void ShowRegisterPanel()
    {
        loginPanel?.SetActive(false);
        registerPanel?.SetActive(true);
        ClearFeedback();
    }

    // ─── Login ────────────────────────────────────────────────

    /// <summary>
    /// Called by the Login button's OnClick event.
    /// Accepts username OR email in the identifier field.
    /// </summary>
    public void ClickLogin()
    {
        if (_isBusy) return;

        if (!EnsureGameApiReady())
        {
            ShowFeedback("GameApiManager is missing in scene.");
            return;
        }

        string identifier = loginIdentifierInput?.text?.Trim();
        string password = loginPasswordInput?.text;

        if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(password))
        {
            ShowFeedback("Please enter your username and password.");
            return;
        }

        StartCoroutine(DoLoginOrRegister(identifier, password));
    }

    /// <summary>
    /// Single auth flow for game: try login first, auto-register if user does not exist.
    /// </summary>
    private IEnumerator DoLoginOrRegister(string identifier, string password)
    {
        SetBusy(true);
        ShowFeedback("Checking account...");

        if (!EnsureGameApiReady())
        {
            ShowFeedback("GameApiManager is missing in scene.");
            SetBusy(false);
            yield break;
        }

        bool loginDone = false;
        bool loginSuccess = false;
        string loginError = null;

        GameApiManager.Instance.Login(
            identifier,
            password,
            onSuccess: userData =>
            {
                Debug.Log($"[Auth] Login success — id: {userData.id} | username: {userData.username}");
                loginSuccess = true;
                loginDone = true;
            },
            onError: err =>
            {
                loginError = err;
                loginDone = true;
            }
        );

        yield return new WaitUntil(() => loginDone);

        if (loginSuccess && GameApiManager.Instance.IsLoggedIn)
        {
            ShowFeedback("Welcome, " + GameApiManager.Instance.CurrentUser.username + "!");
            yield return new WaitForSeconds(0.5f);
            LoadNextScene();
            yield break;
        }

        bool shouldCreate = ShouldAutoCreateAccount(loginError);
        if (!shouldCreate)
        {
            ShowFeedback(string.IsNullOrEmpty(loginError) ? "Login failed." : loginError);
            SetBusy(false);
            yield break;
        }

        // Create account automatically when login indicates the user does not exist.
        yield return StartCoroutine(DoRegister(identifier, password));
    }

    private IEnumerator DoLogin(string identifier, string password)
    {
        SetBusy(true);
        ShowFeedback("Logging in...");

        bool done = false;

        GameApiManager.Instance.Login(
            identifier,
            password,
            onSuccess: userData =>
            {
                // user.id is now stored in GameApiManager.CurrentUser.id
                // All game systems reference GameApiManager.CurrentUser.id
                Debug.Log($"[Auth] Logged in — id: {userData.id} | username: {userData.username}");
                done = true;
            },
            onError: err =>
            {
                ShowFeedback(err);
                SetBusy(false);
                done = true;
            }
        );

        // Wait for async callback
        yield return new WaitUntil(() => done);

        if (GameApiManager.Instance.IsLoggedIn)
        {
            ShowFeedback("Welcome, " + GameApiManager.Instance.CurrentUser.username + "!");
            yield return new WaitForSeconds(0.5f);
            LoadNextScene();
        }
    }

    // ─── Register ─────────────────────────────────────────────

    /// <summary>
    /// Called by the Register button's OnClick event.
    /// Sends only username + password. Email is null/optional in DB.
    /// </summary>
    public void ClickRegister()
    {
        if (_isBusy) return;

        if (!EnsureGameApiReady())
        {
            ShowFeedback("GameApiManager is missing in scene.");
            return;
        }

        string username = registerUsernameInput?.text?.Trim();
        string password = registerPasswordInput?.text;
        if (string.IsNullOrEmpty(username))
        {
            ShowFeedback("Please enter a username.");
            return;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 6)
        {
            ShowFeedback("Password must be at least 6 characters.");
            return;
        }

        StartCoroutine(DoRegister(username, password));
    }

    private IEnumerator DoRegister(string username, string password)
    {
        SetBusy(true);
        ShowFeedback("Creating account...");

        if (!EnsureGameApiReady())
        {
            ShowFeedback("GameApiManager is missing in scene.");
            SetBusy(false);
            yield break;
        }

        bool done = false;

        GameApiManager.Instance.Register(
            username,
            password,
            onSuccess: userData =>
            {
                // user.id is now the permanent identifier for this player
                // — stored in GameApiManager.CurrentUser.id across all scenes
                Debug.Log($"[Auth] Registered — id: {userData.id} | username: {userData.username}");
                done = true;
            },
            onError: err =>
            {
                ShowFeedback(err);
                SetBusy(false);
                done = true;
            }
        );

        yield return new WaitUntil(() => done);

        if (GameApiManager.Instance.IsLoggedIn)
        {
            ShowFeedback("Account created! Welcome, " + GameApiManager.Instance.CurrentUser.username + "!");
            yield return new WaitForSeconds(0.5f);
            LoadNextScene();
            yield break;
        }

        SetBusy(false);
    }

    // ─── Session check ────────────────────────────────────────

    /// <summary>
    /// If GameApiManager already restored a session from PlayerPrefs,
    /// skip the auth screen entirely.
    /// </summary>
    private IEnumerator CheckExistingSession()
    {
        // Give GameApiManager.Start() time to call TryRestoreSession
        yield return new WaitForSeconds(0.3f);

        if (GameApiManager.Instance != null && GameApiManager.Instance.IsLoggedIn)
        {
            Debug.Log("[Auth] Existing session found — skipping auth screen.");
            LoadNextScene();
        }
    }

    // ─── Helpers ──────────────────────────────────────────────

    private void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadSceneAsync(nextSceneName);
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;

        if (loginButton != null) loginButton.interactable = !busy;
        if (registerButton != null) registerButton.interactable = !busy;
    }

    private void ShowFeedback(string msg)
    {
        Debug.Log("[Auth UI] " + msg);
    }

    private void ClearFeedback()
    {
        // No dedicated feedback label in this UI.
    }

    private bool ShouldAutoCreateAccount(string loginError)
    {
        if (string.IsNullOrEmpty(loginError)) return false;

        string normalized = loginError.ToLowerInvariant();
        return normalized.Contains("user not found")
            || normalized.Contains("not found")
            || normalized.Contains("404");
    }

    private bool EnsureGameApiReady()
    {
        if (GameApiManager.Instance != null) return true;

        var existing = FindObjectOfType<GameApiManager>();
        if (existing != null) return true;

        var gameApiRoot = new GameObject("GameAPI");
        gameApiRoot.AddComponent<ApiConfig>();
        gameApiRoot.AddComponent<ApiClient>();
        gameApiRoot.AddComponent<AuthService>();
        gameApiRoot.AddComponent<GameApiManager>();

        Debug.Log("[Auth UI] Auto-created missing GameAPI stack.");
        return GameApiManager.Instance != null;
    }
}

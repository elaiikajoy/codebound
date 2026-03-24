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

    [Header("Auth Panel")]
    [Tooltip("The login/register form panel root")]
    public GameObject loginPanel;

    [Header("Inputs")]
    [Tooltip("Username accepted by the backend")]
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public Button loginButton;
    public Button registerButton;

    [Header("Scene navigation")]
    [Tooltip("Scene name to load after successful login or register")]
    public string nextSceneName = "Main";

    [Header("Optional in-scene transition")]
    public GameObject mainMenuPanel;

    [Header("Behavior")]
    public bool autoFindSceneReferences = true;

    // ─── Private state ────────────────────────────────────────
    private bool _isBusy = false;
    private bool _isShowingAuthenticatedState;
    private bool _isWaitingForSessionRestore;

    // ─── Lifecycle ────────────────────────────────────────────
    private void OnEnable()
    {
        GameApiManager.OnLoginSuccess += HandleAuthenticated;
        GameApiManager.OnSessionRestored += HandleAuthenticated;
        GameApiManager.OnLogout += HandleLoggedOut;
    }

    private void OnDisable()
    {
        GameApiManager.OnLoginSuccess -= HandleAuthenticated;
        GameApiManager.OnSessionRestored -= HandleAuthenticated;
        GameApiManager.OnLogout -= HandleLoggedOut;
    }

    private void Start()
    {
        EnsureGameApiReady();

        if (autoFindSceneReferences)
            TryAutoFindSceneReferences();

        WireButtonHandlers();

        // If a saved session exists, wait for restore to finish so we do not
        // flash the login panel for returning users.
        if (GameApiManager.Instance != null &&
            (GameApiManager.Instance.HasSavedSession || GameApiManager.Instance.HasRememberedCredentials))
        {
            _isWaitingForSessionRestore = true;
            HideLoginPanelOnly();
            StartCoroutine(ResolveInitialAuthState());
        }
        else if (GameApiManager.Instance != null && GameApiManager.Instance.IsLoggedIn)
        {
            ShowAuthenticatedMainState();
        }
        else
        {
            ShowLoginPanel();
        }
    }

    private void Update()
    {
        if (GameApiManager.Instance == null)
            return;

        // If we are showing the authenticated UI but the user is no longer logged in
        // (e.g. they logged out), we should revert to the login screen.
        if (_isShowingAuthenticatedState && !GameApiManager.Instance.IsLoggedIn)
            HandleLoggedOut();
    }

    private void WireButtonHandlers()
    {
        if (loginButton == null && registerButton == null)
        {
            Debug.LogWarning("[Auth UI] No login/register button assigned.");
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

    /// <summary>
    /// Generic primary-button handler: executes Register if the register
    /// button is visible, otherwise falls back to Login.
    /// Note: this class does not expose a separate registerPanel — tab
    /// visibility is inferred from the registerButton state instead.
    /// </summary>
    private void OnPrimaryButtonClicked()
    {
        bool registerVisible = registerButton != null && registerButton.gameObject.activeInHierarchy;
        if (registerVisible)
            ClickRegister();
        else
            ClickLogin();
    }

    // ─── Panel switching ──────────────────────────────────────

    /// <summary>Show the login/register form panel.</summary>
    public void ShowLoginPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(true);

        // Ensure root background image is enabled to block raycasts/show background
        Image bgImage = GetComponent<Image>();
        if (bgImage != null) bgImage.enabled = true;

        _isShowingAuthenticatedState = false;
        _isWaitingForSessionRestore = false;
        ClearFeedback();
    }

    private void HideLoginPanelOnly()
    {
        if (loginPanel != null)
            loginPanel.SetActive(false);

        _isShowingAuthenticatedState = false;

        Image bgImage = GetComponent<Image>();
        if (bgImage != null)
            bgImage.enabled = false;
    }

    // ─── Login ────────────────────────────────────────────────

    /// <summary>
    /// Called by the Login button's OnClick event.
    /// Accepts username in the identifier field.
    /// </summary>
    public void ClickLogin()
    {
        if (_isBusy) return;

        if (!EnsureGameApiReady())
        {
            ShowFeedback("GameApiManager is missing in scene.");
            return;
        }

        string identifier = usernameInput?.text?.Trim();
        string password = passwordInput?.text;

        if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(password))
        {
            ShowFeedback("Please enter your username and password.");
            return;
        }

        StartCoroutine(DoLogin(identifier, password));
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

        string username = usernameInput?.text?.Trim();
        string password = passwordInput?.text;
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

    // ─── Helpers ──────────────────────────────────────────────

    private void LoadNextScene()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;

        if (string.IsNullOrEmpty(nextSceneName) || nextSceneName == activeSceneName)
        {
            ShowAuthenticatedMainState();
            return;
        }

        SceneManager.LoadSceneAsync(nextSceneName);
    }

    private void ShowAuthenticatedMainState()
    {
        if (autoFindSceneReferences)
            TryAutoFindSceneReferences();

        if (loginPanel != null)
            loginPanel.SetActive(false);

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        // Disable root background so it doesn't block Main Menu buttons
        Image bgImage = GetComponent<Image>();
        if (bgImage != null) bgImage.enabled = false;

        _isShowingAuthenticatedState = true;
        SetBusy(false);
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
        gameApiRoot.AddComponent<ProgressService>();
        gameApiRoot.AddComponent<GameApiManager>();

        Debug.Log("[Auth UI] Auto-created missing GameAPI stack.");
        return GameApiManager.Instance != null;
    }

    private void TryAutoFindSceneReferences()
    {
        if (loginPanel == null)
        {
            if (gameObject.name == "LoginPanel")
                loginPanel = gameObject;
            else
                loginPanel = FindSceneObject("LoginPanel");
        }

        if (mainMenuPanel == null)
            mainMenuPanel = FindSceneObject("Main Menu");
    }

    private void HandleAuthenticated(UserData userData)
    {
        _isWaitingForSessionRestore = false;
        ShowAuthenticatedMainState();
    }

    private void HandleLoggedOut()
    {
        _isWaitingForSessionRestore = false;
        if (autoFindSceneReferences)
            TryAutoFindSceneReferences();

        ShowLoginPanel();
        SetBusy(false);
    }

    private IEnumerator ResolveInitialAuthState()
    {
        float timeout = 5f;
        while (timeout > 0f)
        {
            if (GameApiManager.Instance != null && GameApiManager.Instance.IsLoggedIn)
            {
                ShowAuthenticatedMainState();
                yield break;
            }

            if (GameApiManager.Instance == null ||
                (!GameApiManager.Instance.HasSavedSession && !GameApiManager.Instance.HasRememberedCredentials))
            {
                ShowLoginPanel();
                yield break;
            }

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_isShowingAuthenticatedState)
            ShowLoginPanel();
    }

    private GameObject FindSceneObject(string objectName)
    {
        foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (obj.name == objectName && obj.scene.IsValid())
                return obj;
        }

        return null;
    }
}

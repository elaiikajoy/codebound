// ============================================================
// 1. Script Name: Shop.cs
// 2. Purpose: Manages the character shop UI.
//    - Left/right arrows cycle through characters one by one.
//    - Buy button: deducts tokens via TokenManager and marks
//      the character as locally owned.
//    - Select/Equip button: equips the character locally and POSTs
//      to POST /characters/equip so the selection persists in the DB.
//    - Play button: only active when the displayed character is owned;
//      sets it as the active character and loads the game.
//
// 3. Unity Setup Instructions:
//    - Attach to: the Canvas or Shop manager object in the Shop scene.
//    - Required: a CharacterDatabase ScriptableObject assigned in the Inspector.
//    - Inspector Links:
//        nameText      – character name label (TMP_Text)
//        costText      – cost / owned / free label (TMP_Text)
//        tokenText     – player's current token balance (TMP_Text)
//        artworkSprite – SpriteRenderer that shows the character artwork
//        buyButton     – buy button (Button)
//        selectButton  – equip/select button (Button)
//        playButton    – play button (Button, only active if owned)
//    - Wire NextOption() and BackOption() to the arrow buttons.
//    - Wire BuyCharacter(), SelectCharacter(), PlayCharacter() to the
//      corresponding buttons in the Inspector.
// ============================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class Shop : MonoBehaviour
{
    // ─── Inspector Links ──────────────────────────────────────
    [Header("Data")]
    [Tooltip("ScriptableObject that holds all character definitions.")]
    public CharacterDatabase characterDB;

    [Header("UI – Labels")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI tokenText;

    [Header("UI – Character Preview")]
    [Tooltip("World-space preview. If assigned, its Rigidbody2D will be set to Kinematic to prevent falling.")]
    public SpriteRenderer artworkSprite;

    [Tooltip("UI-space preview (standard for Canvases). If assigned, character sprites are shown here.")]
    public Image artworkImage;

    [Header("UI – Buttons")]
    public Button buyButton;
    public Button selectButton;   // Equip / Select
    public Button playButton;     // Play (only enabled when character is owned)

    [Header("Scene")]
    [Tooltip("Scene to load when the player hits the Play button.")]
    public string levelSelectionScene = "LevelPanel";

    // ─── Private State ────────────────────────────────────────
    private int _selectedIndex = 0;
    private Rigidbody2D _cachedRb; // Cache the RB to avoid GetComponent delay
    private Animator _cachedAnimator;
    private readonly HashSet<string> _ownedCharacterIds = new HashSet<string>();
    private bool _hasBackendCharacterState;
    private bool _isShopUiContext;

    private void OnEnable()
    {
        GameApiManager.OnLoginSuccess += HandleAuthStateChanged;
        GameApiManager.OnSessionRestored += HandleAuthStateChanged;
        GameApiManager.OnLogout += HandleLoggedOut;
    }

    private void OnDisable()
    {
        GameApiManager.OnLoginSuccess -= HandleAuthStateChanged;
        GameApiManager.OnSessionRestored -= HandleAuthStateChanged;
        GameApiManager.OnLogout -= HandleLoggedOut;
    }

    // ─── Lifecycle ────────────────────────────────────────────

    private void Start()
    {
        EnsureGameApiStack();

        // When this script is attached to the in-level player prefab,
        // only artworkSprite is assigned and shop UI references are null.
        // In that mode we should trust locally equipped prefs and avoid
        // backend state pulls that can briefly revert visuals to ranger.
        _isShopUiContext = (buyButton != null) || (selectButton != null) || (playButton != null) ||
                           (nameText != null) || (costText != null) || (tokenText != null);

        // Cache the Rigidbody once at the start to make RefreshDisplay faster
        if (artworkSprite != null)
        {
            _cachedRb = artworkSprite.GetComponent<Rigidbody2D>();
            _cachedAnimator = artworkSprite.GetComponent<Animator>();
            if (_cachedRb != null)
            {
                _cachedRb.bodyType = RigidbodyType2D.Kinematic;
                _cachedRb.velocity = Vector2.zero;
                _cachedRb.angularVelocity = 0;
                _cachedRb.simulated = true; // Still simulated but Kinematic
            }
        }

        // Pre-select the character the player has equipped
        _selectedIndex = FindIndexByEquippedId();
        RefreshDisplay();

        if (!_isShopUiContext)
            ApplyGameplayAnimatorMode();

        if (!_isShopUiContext)
            return;

        if (GameApiManager.Instance != null && SkinService.Instance != null)
        {
            if (GameApiManager.Instance.IsLoggedIn)
                StartCoroutine(LoadCharacterStateFromBackend());
            else if (GameApiManager.Instance.HasSavedSession || GameApiManager.Instance.HasRememberedCredentials)
                StartCoroutine(WaitForSessionThenLoadCharacterState());
        }
    }

    private void HandleAuthStateChanged(UserData _)
    {
        if (!_isShopUiContext) return;
        if (!isActiveAndEnabled) return;
        if (SkinService.Instance == null) return;

        _hasBackendCharacterState = false;
        StartCoroutine(LoadCharacterStateFromBackend());
    }

    private void HandleLoggedOut()
    {
        _ownedCharacterIds.Clear();
        _hasBackendCharacterState = false;

        if (_isShopUiContext)
            RefreshDisplay();
    }

    // ─── Navigation ───────────────────────────────────────────

    /// <summary>Advance to the next character (wraps around).</summary>
    public void NextOption()
    {
        _selectedIndex++;
        if (_selectedIndex >= characterDB.CharacterCount)
            _selectedIndex = 0;

        RefreshDisplay();
    }

    /// <summary>Go back to the previous character (wraps around).</summary>
    public void BackOption()
    {
        _selectedIndex--;
        if (_selectedIndex < 0)
            _selectedIndex = characterDB.CharacterCount - 1;

        RefreshDisplay();
    }

    // ─── Actions ──────────────────────────────────────────────

    /// <summary>
    /// Buy the currently displayed character.
    /// Deducts cost using TokenManager (keeping token balance in sync
    /// and queuing a backend flush on next save).
    /// </summary>
    public void BuyCharacter()
    {
        Characters character = characterDB.GetCharacter(_selectedIndex);
        string characterId = GetCharacterId(character);

        if (character == null) return;
        if (string.IsNullOrEmpty(characterId)) return;
        if (IsOwned(_selectedIndex)) return;                    // already owned
        if (TokenManager.GetTokens() < character.cost) return; // not enough tokens

        EnsureGameApiStack();

        if (GameApiManager.Instance == null || SkinService.Instance == null)
        {
            Debug.LogWarning("[Shop] Buy blocked: GameAPI stack is not ready.");
            return;
        }

        if (!GameApiManager.Instance.IsLoggedIn)
        {
            if (GameApiManager.Instance.HasSavedSession || GameApiManager.Instance.HasRememberedCredentials)
            {
                StartCoroutine(WaitForSessionThenBuy(characterId, character.CharacterName));
            }
            else
            {
                Debug.LogWarning("[Shop] Buy blocked: user must be logged in for DB-persistent purchase.");
            }
            return;
        }

        StartCoroutine(SkinService.Instance.BuyCharacter(
            characterId,
            onSuccess: state =>
            {
                ApplyCharacterState(state);
                Debug.Log($"[Shop] Bought character '{character.CharacterName}' from backend.");
                RefreshDisplay();
            },
            onError: err =>
            {
                Debug.LogWarning($"[Shop] Buy failed: {err}");
                RefreshDisplay();
            }
        ));
    }

    /// <summary>
    /// Equip the currently displayed character (must be owned).
    /// Saves locally and POSTs to POST /characters/equip so the
    /// selection is persisted in user_progress.equippedCharacter.
    /// </summary>
    public void SelectCharacter()
    {
        Characters character = characterDB.GetCharacter(_selectedIndex);
        string characterId = GetCharacterId(character);
        if (character == null || !IsOwned(_selectedIndex)) return;
        if (string.IsNullOrEmpty(characterId)) return;

        // Save locally so CharacterManager in game scenes can read it.
        PlayerPrefs.SetInt("SelectedCharacter", _selectedIndex);
        PlayerPrefs.SetString("EquippedCharacter", characterId);
        PlayerPrefs.Save();

        // Sync to backend — non-blocking, game continues even if offline.
        if (SkinService.Instance != null)
        {
            StartCoroutine(SkinService.Instance.EquipCharacter(
                characterId,
                onSuccess: id => Debug.Log($"[Shop] Equipped '{id}' on backend successfully."),
                onError: err => Debug.LogWarning($"[Shop] Equip backend sync failed (offline?): {err}")
            ));
        }

        Debug.Log($"[Shop] Character selected: {character.CharacterName} (id={characterId})");
        RefreshDisplay();
    }

    /// <summary>
    /// Equip and immediately load the level selection scene.
    /// Only callable when the displayed character is owned.
    /// </summary>
    public void PlayCharacter()
    {
        Characters character = characterDB.GetCharacter(_selectedIndex);
        string characterId = GetCharacterId(character);

        if (character == null || !IsOwned(_selectedIndex) || string.IsNullOrEmpty(characterId))
            return;

        // Apply locally first so gameplay visuals immediately use the selected character.
        PlayerPrefs.SetInt("SelectedCharacter", _selectedIndex);
        PlayerPrefs.SetString("EquippedCharacter", characterId);
        PlayerPrefs.Save();

        // In non-shop contexts (e.g., player prefab inside levels), just continue.
        if (!_isShopUiContext)
        {
            SceneManager.LoadSceneAsync(levelSelectionScene);
            return;
        }

        if (SkinService.Instance == null || GameApiManager.Instance == null)
        {
            SceneManager.LoadSceneAsync(levelSelectionScene);
            return;
        }

        if (!GameApiManager.Instance.IsLoggedIn)
        {
            if (GameApiManager.Instance.HasSavedSession || GameApiManager.Instance.HasRememberedCredentials)
            {
                StartCoroutine(WaitForSessionThenEquipAndPlay(characterId));
            }
            else
            {
                SceneManager.LoadSceneAsync(levelSelectionScene);
            }
            return;
        }

        StartCoroutine(SkinService.Instance.EquipCharacter(
            characterId,
            onSuccess: _ => SceneManager.LoadSceneAsync(levelSelectionScene),
            onError: err =>
            {
                Debug.LogWarning($"[Shop] Equip before play failed: {err}. Continuing with local equipped state.");
                SceneManager.LoadSceneAsync(levelSelectionScene);
            }
        ));
    }

    /// <summary>Return to the main menu.</summary>
    public void ExitShop()
    {
        SceneManager.LoadSceneAsync("Main");
    }

    // ─── Display ──────────────────────────────────────────────

    private void RefreshDisplay()
    {
        if (characterDB == null || characterDB.CharacterCount == 0)
        {
            Debug.LogError("[Shop] CharacterDatabase is null or empty!");
            return;
        }

        Characters character = characterDB.GetCharacter(_selectedIndex);
        if (character == null) return;

        // Character artwork (World Space)
        if (artworkSprite != null)
        {
            artworkSprite.sprite = character.characterSprite;

            // BUG FIX: Ensure character stays put
            if (_cachedRb != null)
            {
                _cachedRb.velocity = Vector2.zero;
            }
        }

        // Character artwork (UI Space / Canvas)
        if (artworkImage != null)
            artworkImage.sprite = character.characterSprite;

        // Name
        if (nameText != null)
            nameText.text = character.CharacterName;

        // Token balance — always read live from TokenManager
        if (tokenText != null)
            tokenText.text = TokenManager.GetTokens().ToString();

        bool owned = IsOwned(_selectedIndex);
        bool isFree = character.cost == 0;
        bool canAfford = TokenManager.GetTokens() >= character.cost;
        bool isEquipped = IsEquipped(GetCharacterId(character));

        // Cost label
        if (costText != null)
        {
            if (owned || isFree)
                costText.text = isEquipped ? "EQUIPPED" : "OWNED";
            else
                costText.text = "$" + character.cost.ToString();
        }

        // Buy button: visible and enabled only if not owned and can afford
        if (buyButton != null)
        {
            buyButton.gameObject.SetActive(!owned && !isFree);
            buyButton.interactable = canAfford;
            SetButtonLabel(buyButton, "BUY");
        }

        // Select button: enabled only if owned (and not already equipped)
        if (selectButton != null)
        {
            selectButton.gameObject.SetActive(owned || isFree);
            selectButton.interactable = !isEquipped;
        }

        // Play button: always visible when owned/free; always interactable
        if (playButton != null)
        {
            playButton.gameObject.SetActive(owned || isFree);
            playButton.interactable = true;
            SetButtonLabel(playButton, "PLAY");
        }

        if (owned && costText != null)
            costText.text = isEquipped ? "EQUIPPED" : "OWNED";

        if (!_isShopUiContext)
            ApplyGameplayAnimatorMode();
    }

    // ─── Helpers ──────────────────────────────────────────────

    /// <summary>
    /// Returns true if the character at <paramref name="index"/> is owned
    /// (index 0 and free characters are always owned).
    /// Checks both the index-based key and the characterId-based key so
    /// purchases survive logout/re-login.
    /// </summary>
    private bool IsOwned(int index)
    {
        if (index == 0) return true;

        Characters character = characterDB.GetCharacter(index);
        string characterId = GetCharacterId(character);
        if (character == null || character.cost == 0) return true;

        // Strict source of truth: backend user_characters only.
        if (!_hasBackendCharacterState || string.IsNullOrEmpty(characterId))
            return false;

        return _ownedCharacterIds.Contains(characterId);
    }

    /// <summary>Returns true if the given characterId is the currently equipped one.</summary>
    private bool IsEquipped(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return false;
        string equipped = NormalizeCharacterId(PlayerPrefs.GetString("EquippedCharacter", "default"));
        return equipped == characterId;
    }

    /// <summary>
    /// Finds the index in the CharacterDatabase for the player's currently
    /// equipped character (read from PlayerPrefs["EquippedCharacter"]).
    /// Falls back to index 0 if not found.
    /// </summary>
    private int FindIndexByEquippedId()
    {
        string equipped = PlayerPrefs.GetString("EquippedCharacter", "default");
        equipped = NormalizeCharacterId(equipped);

        for (int i = 0; i < characterDB.CharacterCount; i++)
        {
            Characters c = characterDB.GetCharacter(i);
            if (c != null && GetCharacterId(c) == equipped)
                return i;
        }

        return 0; // Default to first character if not matched
    }

    private IEnumerator LoadCharacterStateFromBackend()
    {
        yield return StartCoroutine(SkinService.Instance.GetCharacterState(
            onSuccess: state =>
            {
                ApplyCharacterState(state);
                _selectedIndex = FindIndexByEquippedId();
                RefreshDisplay();
            },
            onError: err =>
            {
                Debug.LogWarning($"[Shop] Failed to load backend character state: {err}");
                _hasBackendCharacterState = false;
                RefreshDisplay();
            }
        ));
    }

    private IEnumerator WaitForSessionThenLoadCharacterState()
    {
        float timeout = 6f;
        while (timeout > 0f)
        {
            if (GameApiManager.Instance != null && GameApiManager.Instance.IsLoggedIn)
            {
                yield return StartCoroutine(LoadCharacterStateFromBackend());
                yield break;
            }

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning("[Shop] Session restore timed out before character state load.");
    }

    private IEnumerator WaitForSessionThenBuy(string characterId, string characterName)
    {
        float timeout = 6f;
        while (timeout > 0f)
        {
            if (GameApiManager.Instance != null && GameApiManager.Instance.IsLoggedIn)
            {
                StartCoroutine(SkinService.Instance.BuyCharacter(
                    characterId,
                    onSuccess: state =>
                    {
                        ApplyCharacterState(state);
                        Debug.Log($"[Shop] Bought character '{characterName}' from backend after session restore.");
                        RefreshDisplay();
                    },
                    onError: err =>
                    {
                        Debug.LogWarning($"[Shop] Buy failed: {err}");
                        RefreshDisplay();
                    }
                ));
                yield break;
            }

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning("[Shop] Buy blocked: failed to restore session in time.");
    }

    private IEnumerator WaitForSessionThenEquipAndPlay(string characterId)
    {
        float timeout = 6f;
        while (timeout > 0f)
        {
            if (GameApiManager.Instance != null && GameApiManager.Instance.IsLoggedIn && SkinService.Instance != null)
            {
                yield return StartCoroutine(SkinService.Instance.EquipCharacter(
                    characterId,
                    onSuccess: _ => SceneManager.LoadSceneAsync(levelSelectionScene),
                    onError: err =>
                    {
                        Debug.LogWarning($"[Shop] Equip after session restore failed: {err}. Continuing.");
                        SceneManager.LoadSceneAsync(levelSelectionScene);
                    }
                ));
                yield break;
            }

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        SceneManager.LoadSceneAsync(levelSelectionScene);
    }

    private void EnsureGameApiStack()
    {
        GameObject gameApiRoot = null;

        if (GameApiManager.Instance != null)
        {
            gameApiRoot = GameApiManager.Instance.gameObject;
        }
        else
        {
            var existing = FindObjectOfType<GameApiManager>();
            if (existing != null)
            {
                gameApiRoot = existing.gameObject;
            }
            else
            {
                gameApiRoot = new GameObject("GameAPI");
                gameApiRoot.AddComponent<GameApiManager>();
                Debug.Log("[Shop] Auto-created missing GameAPI root.");
            }
        }

        // Ensure all required network components exist on the same persistent object.
        if (gameApiRoot.GetComponent<ApiConfig>() == null) gameApiRoot.AddComponent<ApiConfig>();
        if (gameApiRoot.GetComponent<ApiClient>() == null) gameApiRoot.AddComponent<ApiClient>();
        if (gameApiRoot.GetComponent<AuthService>() == null) gameApiRoot.AddComponent<AuthService>();
        if (gameApiRoot.GetComponent<ProgressService>() == null) gameApiRoot.AddComponent<ProgressService>();
        if (gameApiRoot.GetComponent<SkinService>() == null) gameApiRoot.AddComponent<SkinService>();

        if (gameApiRoot.GetComponent<LeaderboardService>() == null) gameApiRoot.AddComponent<LeaderboardService>();
        if (gameApiRoot.GetComponent<AchievementService>() == null) gameApiRoot.AddComponent<AchievementService>();
    }

    private void ApplyCharacterState(CharacterStateData state)
    {
        if (state == null) return;

        _ownedCharacterIds.Clear();
        if (state.ownedCharacters != null)
        {
            foreach (var id in state.ownedCharacters)
            {
                string normalized = NormalizeCharacterId(id);
                if (!string.IsNullOrEmpty(normalized))
                    _ownedCharacterIds.Add(normalized);
            }
        }

        _ownedCharacterIds.Add("ranger");
        _hasBackendCharacterState = true;

        if (!string.IsNullOrEmpty(state.equippedCharacter))
            PlayerPrefs.SetString("EquippedCharacter", NormalizeCharacterId(state.equippedCharacter));

        TokenManager.SyncFromBackend(state.totalTokens);
        PlayerPrefs.Save();
    }

    private string GetCharacterId(Characters character)
    {
        if (character == null) return string.Empty;

        if (!string.IsNullOrWhiteSpace(character.characterId))
            return NormalizeCharacterId(character.characterId);

        // Fallback to name-based IDs to support existing ScriptableObject assets.
        return NormalizeCharacterId(character.CharacterName);
    }

    private string NormalizeCharacterId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized == "default") return "ranger";
        return normalized;
    }

    private void SetButtonLabel(Button button, string text)
    {
        if (button == null) return;
        var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
            label.text = text;
    }

    private void ApplyGameplayAnimatorMode()
    {
        if (_cachedAnimator == null && artworkSprite != null)
            _cachedAnimator = artworkSprite.GetComponent<Animator>();

        if (_cachedAnimator == null)
            return;

        string equipped = NormalizeCharacterId(PlayerPrefs.GetString("EquippedCharacter", "default"));
        bool useRangerAnimator = equipped == "ranger" || string.IsNullOrEmpty(equipped);
        _cachedAnimator.enabled = useRangerAnimator;
    }
}

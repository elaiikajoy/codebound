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

    // ─── Lifecycle ────────────────────────────────────────────

    private void Start()
    {
        // Cache the Rigidbody once at the start to make RefreshDisplay faster
        if (artworkSprite != null)
        {
            _cachedRb = artworkSprite.GetComponent<Rigidbody2D>();
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

        if (character == null) return;
        if (IsOwned(_selectedIndex)) return;                    // already owned
        if (TokenManager.GetTokens() < character.cost) return; // not enough tokens

        // Deduct tokens via the unified token manager.
        // SpendTokens returns false if balance is insufficient (shouldn't happen
        // since BuyButton is disabled when balance is too low, but guard anyway).
        if (!TokenManager.SpendTokens(character.cost))
        {
            Debug.LogWarning("[Shop] BuyCharacter: SpendTokens failed — insufficient balance.");
            return;
        }

        // Mark as owned in PlayerPrefs — by both index and characterId so
        // that the owned state survives logout (characterId-based key persists
        // even after the index-based key is cleared on logout).
        PlayerPrefs.SetInt("Character_" + _selectedIndex, 1);
        if (!string.IsNullOrEmpty(character.characterId))
            PlayerPrefs.SetInt("OwnedChar_" + character.characterId, 1);
        PlayerPrefs.Save();

        Debug.Log($"[Shop] Bought character '{character.CharacterName}' for {character.cost} tokens. Remaining: {TokenManager.GetTokens()}");

        RefreshDisplay();
    }

    /// <summary>
    /// Equip the currently displayed character (must be owned).
    /// Saves locally and POSTs to POST /characters/equip so the
    /// selection is persisted in user_progress.equippedCharacter.
    /// </summary>
    public void SelectCharacter()
    {
        Characters character = characterDB.GetCharacter(_selectedIndex);
        if (character == null || !IsOwned(_selectedIndex)) return;

        // Save locally so CharacterManager in game scenes can read it.
        PlayerPrefs.SetInt("SelectedCharacter", _selectedIndex);
        if (!string.IsNullOrEmpty(character.characterId))
            PlayerPrefs.SetString("EquippedCharacter", character.characterId);
        PlayerPrefs.Save();

        // Sync to backend — non-blocking, game continues even if offline.
        if (SkinService.Instance != null && !string.IsNullOrEmpty(character.characterId))
        {
            StartCoroutine(SkinService.Instance.EquipCharacter(
                character.characterId,
                onSuccess: id => Debug.Log($"[Shop] Equipped '{id}' on backend successfully."),
                onError:  err => Debug.LogWarning($"[Shop] Equip backend sync failed (offline?): {err}")
            ));
        }

        Debug.Log($"[Shop] Character selected: {character.CharacterName} (id={character.characterId})");
        RefreshDisplay();
    }

    /// <summary>
    /// Equip and immediately load the level selection scene.
    /// Only callable when the displayed character is owned.
    /// </summary>
    public void PlayCharacter()
    {
        // Equip first (ensures DB is updated even if coming from a fresh selection).
        SelectCharacter();

        SceneManager.LoadSceneAsync(levelSelectionScene);
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
            tokenText.text = "$" + TokenManager.GetTokens().ToString();

        bool owned = IsOwned(_selectedIndex);
        bool isFree = character.cost == 0;
        bool canAfford = TokenManager.GetTokens() >= character.cost;
        bool isEquipped = IsEquipped(character.characterId);

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
        }
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
        if (character == null || character.cost == 0) return true;

        // Primary: index key (set on purchase in this session)
        if (PlayerPrefs.GetInt("Character_" + index, 0) == 1) return true;

        // Fallback: characterId key (survives logout)
        if (!string.IsNullOrEmpty(character.characterId))
            return PlayerPrefs.GetInt("OwnedChar_" + character.characterId, 0) == 1;

        return false;
    }

    /// <summary>Returns true if the given characterId is the currently equipped one.</summary>
    private bool IsEquipped(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return false;
        return PlayerPrefs.GetString("EquippedCharacter", "default") == characterId;
    }

    /// <summary>
    /// Finds the index in the CharacterDatabase for the player's currently
    /// equipped character (read from PlayerPrefs["EquippedCharacter"]).
    /// Falls back to index 0 if not found.
    /// </summary>
    private int FindIndexByEquippedId()
    {
        string equipped = PlayerPrefs.GetString("EquippedCharacter", "default");

        for (int i = 0; i < characterDB.CharacterCount; i++)
        {
            Characters c = characterDB.GetCharacter(i);
            if (c != null && c.characterId == equipped)
                return i;
        }

        return 0; // Default to first character if not matched
    }
}

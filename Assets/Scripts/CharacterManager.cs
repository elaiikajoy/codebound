// ============================================================
// 1. Script Name: CharacterManager.cs
// 2. Purpose: Applies the player's selected character (sprite/config)
//             to the player object in game scenes.
//             On Start:
//               - If logged in, fetches equippedCharacter from the
//                 backend (GET /characters) and writes it to
//                 PlayerPrefs["EquippedCharacter"] before applying.
//               - Otherwise falls back to the local PlayerPrefs value.
//             Also re-applies on login/session-restore events.
//
// 3. Unity Setup Instructions:
//    - Attach to: Ranger prefab (or any player prefab used in levels).
//    - Inspector Links:
//        characterDB   – CharacterDatabase ScriptableObject
//        artworkSprite – SpriteRenderer on the Player
//        nameText      – Optional TMP_Text to display the character name
// ============================================================

using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class CharacterManager : MonoBehaviour
{
    [Header("Data")]
    public CharacterDatabase characterDB;

    [Header("UI / Visuals")]
    [Tooltip("World-space display (SpriteRenderer).")]
    public SpriteRenderer artworkSprite;

    [Tooltip("UI-space display (Image). Used if inside a Canvas.")]
    public UnityEngine.UI.Image artworkImage;

    [Tooltip("Optional: a text label that shows the character name.")]
    public TextMeshProUGUI nameText;

    // ─── Lifecycle ────────────────────────────────────────────

    private void OnEnable()
    {
        GameApiManager.OnLoginSuccess += HandleAccountStateChanged;
        GameApiManager.OnSessionRestored += HandleAccountStateChanged;
    }

    private void OnDisable()
    {
        GameApiManager.OnLoginSuccess -= HandleAccountStateChanged;
        GameApiManager.OnSessionRestored -= HandleAccountStateChanged;
    }

    private void Start()
    {
        // If logged in and SkinService is available, pull the equipped
        // character ID from the backend so the DB value always wins.
        if (SkinService.Instance != null
            && GameApiManager.Instance != null
            && GameApiManager.Instance.IsLoggedIn)
        {
            StartCoroutine(FetchThenApply());
        }
        else if (GameApiManager.Instance != null
            && (GameApiManager.Instance.HasSavedSession
                || GameApiManager.Instance.HasRememberedCredentials))
        {
            // Session is still restoring — wait for the event, then apply.
            // ApplySelectedCharacter() will be called by HandleAccountStateChanged.
            // But also apply immediately from local prefs as a best-effort fallback.
            ApplySelectedCharacter();
        }
        else
        {
            ApplySelectedCharacter();
        }
    }

    // ─── Backend Fetch ────────────────────────────────────────

    /// <summary>
    /// Fetches the equipped character from the backend and writes it
    /// into PlayerPrefs before applying it to the player sprite.
    /// </summary>
    private IEnumerator FetchThenApply()
    {
        // APPLY IMMEDIATELY FROM LOCAL PREFS SO THERE IS NO VISUAL DELAY!
        ApplySelectedCharacter();

        yield return StartCoroutine(SkinService.Instance.GetCurrentCharacter(
            onSuccess: equippedId =>
            {
                if (!string.IsNullOrEmpty(equippedId))
                {
                    string normalized = NormalizeCharacterId(equippedId);
                    PlayerPrefs.SetString("EquippedCharacter", normalized);
                    PlayerPrefs.Save();
                    Debug.Log($"[CharacterManager] Backend equipped: '{normalized}'");
                }
            },
            onError: err =>
            {
                Debug.LogWarning($"[CharacterManager] Backend fetch failed ({err}). Using local PlayerPrefs.");
            }
        ));

        // Always apply after the fetch, just in case the backend DB was different from local prefs.
        ApplySelectedCharacter();
    }

    // ─── Public API ───────────────────────────────────────────

    /// <summary>
    /// Reads the saved character from PlayerPrefs and applies the
    /// correct sprite (and optionally name) to the player object.
    /// Safe to call at any time.
    /// </summary>
    public void ApplySelectedCharacter()
    {
        if (characterDB == null)
        {
            Debug.LogError("[CharacterManager] CharacterDatabase is not assigned in the Inspector!");
            return;
        }

        int index = ResolveCharacterIndex();
        index = Mathf.Clamp(index, 0, characterDB.CharacterCount - 1);

        Characters character = characterDB.GetCharacter(index);
        if (character == null)
        {
            Debug.LogWarning($"[CharacterManager] No character found at index {index}.");
            return;
        }

        // ── Sprite swap ───────────────────────────────────────
        if (artworkSprite != null)
        {
            artworkSprite.sprite = character.characterSprite;

            // Freeze physics if not in a gameplay level (e.g. preview in Main/Shop)
            var rb = artworkSprite.GetComponent<Rigidbody2D>();
            if (rb != null && !SceneManager.GetActiveScene().name.Contains("Level"))
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.velocity = Vector2.zero;
            }
        }

        if (artworkImage != null)
            artworkImage.sprite = character.characterSprite;

        // ── Name label ────────────────────────────────────────
        if (nameText != null)
            nameText.text = character.CharacterName;

        // ── Animator Controller swap ──────────────────────────
        // If a custom animator is assigned, use it and enable the Animator.
        // If left empty, disable the Animator so the static sprite isn't overridden
        // (this keeps Goblin/Skeleton from animating as Ranger).
        Animator anim = null;
        if (artworkSprite != null)
            anim = artworkSprite.GetComponent<Animator>();
        if (anim == null)
            anim = GetComponent<Animator>();

        if (anim != null)
        {
            if (character.animatorController != null)
            {
                anim.runtimeAnimatorController = character.animatorController;
                anim.enabled = true;
                Debug.Log($"[CharacterManager] Animator enabled and swapped for '{character.CharacterName}'.");
            }
            else
            {
                // Disable animator completely so the static sprite shows
                anim.enabled = false;
                Debug.Log($"[CharacterManager] No animator assigned for '{character.CharacterName}'. Animator disabled.");
            }
        }

        Debug.Log($"[CharacterManager] Applied '{character.CharacterName}' (index={index}, id={character.characterId}).");
    }

    private void HandleAccountStateChanged(UserData _)
    {
        if (!isActiveAndEnabled) return;

        // Re-fetch from backend whenever login state changes.
        if (SkinService.Instance != null && GameApiManager.Instance != null && GameApiManager.Instance.IsLoggedIn)
            StartCoroutine(FetchThenApply());
        else
            ApplySelectedCharacter();
    }

    // ─── Index Resolution ─────────────────────────────────────

    private int ResolveCharacterIndex()
    {
        int equippedIndex = FindIndexByEquippedId();
        if (equippedIndex >= 0)
            return equippedIndex;

        if (PlayerPrefs.HasKey("SelectedCharacter"))
            return PlayerPrefs.GetInt("SelectedCharacter", 0);

        return 0;
    }

    private int FindIndexByEquippedId()
    {
        string equipped = NormalizeCharacterId(PlayerPrefs.GetString("EquippedCharacter", string.Empty));
        Debug.Log($"[CharacterManager] Looking for equipped ID: '{equipped}'");

        if (string.IsNullOrEmpty(equipped))
            return -1;

        for (int i = 0; i < characterDB.CharacterCount; i++)
        {
            Characters c = characterDB.GetCharacter(i);
            if (c == null) continue;

            // Priority 1: match explicit characterId field
            string dbId = NormalizeCharacterId(c.characterId);
            if (!string.IsNullOrEmpty(dbId) && dbId == equipped)
            {
                Debug.Log($"[CharacterManager] Matched by characterId at index {i}.");
                return i;
            }

            // Priority 2: match by CharacterName (fallback for legacy databases)
            string dbName = NormalizeCharacterId(c.CharacterName);
            if (!string.IsNullOrEmpty(dbName) && dbName == equipped)
            {
                Debug.Log($"[CharacterManager] Matched by CharacterName at index {i}.");
                return i;
            }
        }

        Debug.LogWarning($"[CharacterManager] Could not find any character matching '{equipped}'.");
        return -1;
    }

    private string NormalizeCharacterId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized == "default") return "ranger";
        if (normalized == "minotaur") return "minatour"; // common typo fix
        return normalized;
    }

    private string GetCharacterId(Characters character)
    {
        if (character == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(character.characterId))
            return NormalizeCharacterId(character.characterId);
        return NormalizeCharacterId(character.CharacterName);
    }

    /// <summary>Scene loader helper — kept for backward-compatible Inspector button wiring.</summary>
    public void ChangeScene(int sceneID)
    {
        SceneManager.LoadSceneAsync(sceneID);
    }
}
